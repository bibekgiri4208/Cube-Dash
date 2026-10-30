using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class ShopTrailsTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [TestCase(PlayerTrail.NeonAurora)]
        [TestCase(PlayerTrail.SolarFlare)]
        [TestCase(PlayerTrail.FrostWave)]
        [TestCase(PlayerTrail.RainbowRush)]
        public void TrailsCost250PersistAndEquipIndependentlyFromSkins(PlayerTrail trail)
        {
            using var economy = new EconomyTestScope();
            GameEconomy.AddCoins(349); GameEconomy.TryBuy(PlayerSkin.SharkCube); GameEconomy.Equip(PlayerSkin.SharkCube);
            Assert.That(GameEconomy.EquipTrail(trail), Is.False);
            Assert.That(GameEconomy.TryBuyTrail(trail), Is.False); Assert.That(GameEconomy.Coins, Is.EqualTo(249));
            GameEconomy.AddCoins(1); Assert.That(GameEconomy.TryBuyTrail(trail), Is.True);
            Assert.That(GameEconomy.Coins, Is.Zero); Assert.That(GameEconomy.EquippedTrail, Is.EqualTo(PlayerTrail.None));
            Assert.That(GameEconomy.TryBuyTrail(trail), Is.False);
            Assert.That(GameEconomy.EquipTrail(trail), Is.True); Assert.That(GameEconomy.EquippedTrail, Is.EqualTo(trail));
            Assert.That(PlayerPrefs.GetInt(GameEconomy.TrailOwnershipKey(trail)), Is.EqualTo(1));
            Assert.That(GameEconomy.EquippedSkin, Is.EqualTo(PlayerSkin.SharkCube));
            Assert.That(GameEconomy.EquipTrail(PlayerTrail.None), Is.True); Assert.That(GameEconomy.OwnsTrail(trail), Is.True);
            Assert.That(GameEconomy.EquippedSkin, Is.EqualTo(PlayerSkin.SharkCube));
        }

        [Test]
        public void InvalidTrailSavesAndPurchasesNeverGrantAStyleOrSpendCoins()
        {
            using var economy = new EconomyTestScope(); GameEconomy.AddCoins(1000);
            foreach (int id in new[] { -1, 1, 2, 3, 4, 999 })
            {
                PlayerPrefs.SetInt(GameEconomy.EquippedTrailKey, id);
                Assert.That(GameEconomy.EquippedTrail, Is.EqualTo(PlayerTrail.None));
            }
            Assert.That(GameEconomy.TryBuyTrail((PlayerTrail)999), Is.False);
            Assert.That(GameEconomy.EquipTrail((PlayerTrail)999), Is.False);
            Assert.That(GameEconomy.TryBuyTrail(PlayerTrail.None), Is.False);
            Assert.That(GameEconomy.Coins, Is.EqualTo(1000));
        }

        [Test]
        public void SavedCategoriesHaveFourLiveTrailCardsAndTheMenuDoesNotInheritAGameplayTrail()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = FindMenu(scene); var sections = menu.ShopPanel.GetComponent<MenuShopSections>();
                Assert.That(sections, Is.Not.Null); Assert.That(sections.Cards.Length, Is.EqualTo(4));
                Assert.That(sections.TrailsViewport.gameObject.activeSelf, Is.False);
                Assert.That(menu.ShopPanel.transform.Find("Card/Skins").gameObject.activeSelf, Is.True);
                foreach (var card in sections.Cards)
                {
                    Assert.That(TrailStyles.Valid(card.Style), Is.True);
                    Assert.That(card.Action.onClick.GetPersistentTarget(0), Is.SameAs(card));
                    Assert.That(card.transform.parent.Find("Price").GetComponent<Text>().text, Is.EqualTo("250 COINS"));
                    Assert.That(card.transform.parent.GetComponentInChildren<MenuTrailGraphic>(true).Style, Is.EqualTo(card.Style));
                }
                int count = menu.GetComponentsInChildren<Transform>(true).Length;
                ShopTrailsAuthoring.ApplyToScene(scene);
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            foreach (string path in new[] { PlayerCubePresentationBuilder.PreviewPath, SharkCubeAuthoring.PrefabPath })
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<PlayerTrailEffect>(true), Is.Null);
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCubePresentationBuilder.PlayerPath);
            Assert.That(player.GetComponentsInChildren<PlayerTrailEffect>(true).Length, Is.EqualTo(1));
            Assert.That(ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Material>(ShopTrailsAuthoring.MaterialPath).shader), Is.False);
        }

        [UnityTest]
        public IEnumerator TabsBuyEquipSwitchUnequipReloadAndRejectHiddenCallbacks()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope(); GameEconomy.AddCoins(1100);
            GameEconomy.TryBuy(PlayerSkin.SharkCube); GameEconomy.Equip(PlayerSkin.SharkCube);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); var sections = menu.ShopPanel.GetComponent<MenuShopSections>();
            sections.Cards[0].PurchaseOrToggle(); Assert.That(GameEconomy.Coins, Is.EqualTo(1000));
            menu.OpenShop(); sections.ShowTrails(); yield return null;
            Assert.That(sections.TrailsSelected, Is.True);
            var skins = menu.GetComponent<MenuSkinShop>(); skins.Cards[0].PurchaseOrToggle();
            Assert.That(GameEconomy.OwnsSupercar, Is.False);
            foreach (var card in sections.Cards)
            {
                card.PurchaseOrToggle(); Assert.That(GameEconomy.OwnsTrail(card.Style), Is.True);
                card.PurchaseOrToggle(); Assert.That(GameEconomy.EquippedTrail, Is.EqualTo(card.Style));
                Assert.That(GameEconomy.EquippedSkin, Is.EqualTo(PlayerSkin.SharkCube));
            }
            Assert.That(GameEconomy.Coins, Is.Zero); Assert.That(skins.MenuCoins.text, Is.EqualTo("0"));
            sections.ShowSkins(); sections.Cards[3].PurchaseOrToggle();
            Assert.That(GameEconomy.EquippedTrail, Is.EqualTo(PlayerTrail.RainbowRush));
            sections.ShowTrails();
            Assert.That(sections.ActiveTab.navigation.selectOnDown, Is.SameAs(sections.Cards[0].Action));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            menu = Object.FindAnyObjectByType<MainMenuController>(); sections = menu.ShopPanel.GetComponent<MenuShopSections>();
            Assert.That(GameEconomy.EquippedTrail, Is.EqualTo(PlayerTrail.RainbowRush));
            menu.OpenShop(); sections.ShowTrails(); sections.Cards[3].PurchaseOrToggle();
            Assert.That(GameEconomy.EquippedTrail, Is.EqualTo(PlayerTrail.None));
            menu.ClosePanels(); sections.Cards[0].PurchaseOrToggle();
            Assert.That(GameEconomy.EquippedTrail, Is.EqualTo(PlayerTrail.None));
        }

        [UnityTest]
        public IEnumerator AllStylesWorkWithEverySkinOnEveryMapAndResetPauseAndRebaseSafely()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope(); GameEconomy.AddCoins(2000);
            foreach (var skin in new[] { PlayerSkin.Supercar, PlayerSkin.MonsterTruck, PlayerSkin.MilitaryVan, PlayerSkin.SharkCube }) GameEconomy.TryBuy(skin);
            foreach (var style in new[] { PlayerTrail.NeonAurora, PlayerTrail.SolarFlare, PlayerTrail.FrostWave, PlayerTrail.RainbowRush }) GameEconomy.TryBuyTrail(style);
            foreach (string map in PauseSoundAuthoring.Maps)
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + map + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); game.enabled = false;
                var effect = game.CosmeticTrail; Assert.That(effect, Is.Not.Null, map);
                Mesh mesh = effect.TrailMesh; int count = game.Player.GetComponentsInChildren<Transform>(true).Length;
                foreach (var style in new[] { PlayerTrail.NeonAurora, PlayerTrail.SolarFlare, PlayerTrail.FrostWave, PlayerTrail.RainbowRush })
                    foreach (var skin in new[] { PlayerSkin.Cube, PlayerSkin.Supercar, PlayerSkin.MonsterTruck, PlayerSkin.MilitaryVan, PlayerSkin.SharkCube })
                    {
                        GameEconomy.Equip(skin); GameEconomy.EquipTrail(style); Invoke(game, "RefreshEquippedSkin");
                        effect.ResetTrail(); Simulate(game, 32);
                        Assert.That(effect.Style, Is.EqualTo(style)); Assert.That(effect.TrailRenderer.enabled, Is.True, map);
                        Assert.That(effect.TrailMesh, Is.SameAs(mesh)); Assert.That(mesh.bounds.size.magnitude, Is.LessThan(12));
                        Assert.That(mesh.bounds.size.y, Is.GreaterThan(0.5f), "The trail must occupy real vertical space, not sit flat on the road.");
                        Assert.That(mesh.vertexCount, Is.LessThan(3000), "Ribbons and particles share a bounded reusable mesh.");
                        Assert.That(game.Player.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                        Assert.That(Object.FindAnyObjectByType<CubeWake>().Pieces.All(piece => !piece.GetComponent<Renderer>().enabled), Is.True);
                        if (game.VehicleDriving) { game.SupercarPresentation.LightTrail.Tick(game.Speed, game.GameCamera.transform.position); Assert.That(game.SupercarPresentation.LightTrail.TrailRenderer.enabled, Is.False); }
                        Invoke(game, "ApplyPowerUp", PowerUpType.Truck);
                        Assert.That(effect.TrailRenderer.enabled, Is.False, "Truck must hide the trail immediately.");
                        Simulate(game, 3); Assert.That(effect.TrailRenderer.enabled, Is.False);
                        Invoke(game, "UpdateTruckTimers", 30f);
                        Assert.That(effect.TrailRenderer.enabled, Is.False, "No stale ribbon on expiry.");
                        Simulate(game, 2); Assert.That(effect.TrailRenderer.enabled, Is.True);
                        Assert.That(mesh.bounds.size.magnitude, Is.LessThan(2), "Resume with a fresh short ribbon.");
                        Invoke(game, "ApplyPowerUp", PowerUpType.FighterPlane);
                        Assert.That(effect.TrailRenderer.enabled, Is.False, "Flight must hide the trail immediately.");
                        Simulate(game, 3); Assert.That(effect.TrailRenderer.enabled, Is.False);
                        Assert.That(effect.Age, Is.Zero, "Hidden power-up trails must not advance.");
                        Invoke(game, "UpdateFlightTimers", 30f);
                        Simulate(game, 2); Assert.That(effect.TrailRenderer.enabled, Is.True);
                        Assert.That(GameEconomy.EquippedTrail, Is.EqualTo(style), "Power-ups do not unequip the saved trail.");
                    }
                GameEconomy.Equip(PlayerSkin.SharkCube); Invoke(game, "RefreshEquippedSkin");
                game.TogglePause(); float age = effect.Age; Vector3[] before = mesh.vertices;
                Color[] beforeColors = mesh.colors;
                effect.Tick(1); Assert.That(effect.Age, Is.EqualTo(age)); Assert.That(mesh.vertices, Is.EqualTo(before)); Assert.That(mesh.colors, Is.EqualTo(beforeColors)); game.TogglePause();
                effect.Tick(0.1f); Assert.That(mesh.vertices, Is.Not.EqualTo(before), "Airborne particles and streamers animate in three dimensions.");
                game.Player.position += Vector3.back * 4096; effect.ShiftWorldOrigin(4096); effect.Tick(0.02f);
                Assert.That(mesh.bounds.size.magnitude, Is.LessThan(12));
                game.Player.position += Vector3.forward * 100; effect.Tick(0.02f); Assert.That(effect.TrailRenderer.enabled, Is.False);
                Simulate(game, 3); Assert.That(effect.TrailRenderer.enabled, Is.True);
                Invoke(game, "ApplyPowerUp", PowerUpType.FighterPlane); game.Player.position += Vector3.up * 3;
                effect.ResetTrail(); Simulate(game, 5); Assert.That(effect.TrailRenderer.enabled, Is.False);
                Invoke(game, "UpdateFlightTimers", 30f);
                Simulate(game, 2); Assert.That(effect.TrailRenderer.enabled, Is.True);
                GameEconomy.EquipTrail(PlayerTrail.None); Invoke(game, "RefreshEquippedSkin");
                Assert.That(effect.TrailRenderer.enabled, Is.False);
                Assert.That(Object.FindAnyObjectByType<CubeWake>().Pieces.All(piece => piece.GetComponent<Renderer>().enabled), Is.True);
                GameEconomy.EquipTrail(PlayerTrail.NeonAurora); Invoke(game, "RefreshEquippedSkin"); game.StartRun();
                Assert.That(effect.TrailRenderer.enabled, Is.False, "Retry must not bridge the old track.");
            }
        }

        [UnityTest]
        public IEnumerator TrailShopAndActualGameplayRenderWithDistinctEffects()
        {
            yield return new EnterPlayMode(); using var economy = new EconomyTestScope(); GameEconomy.AddCoins(1200);
            foreach (var style in new[] { PlayerTrail.NeonAurora, PlayerTrail.SolarFlare, PlayerTrail.FrostWave, PlayerTrail.RainbowRush }) GameEconomy.TryBuyTrail(style);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); menu.OpenShop(); var sections = menu.ShopPanel.GetComponent<MenuShopSections>(); sections.ShowTrails();
            yield return null; typeof(MenuPanelMotion).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu.ShopPanel.GetComponent<MenuPanelMotion>(), new object[] { 1f });
            Capture(menu.GetComponentInChildren<Canvas>(true), Camera.main, 1672, 940, "shop-desktop", menu);
            Capture(menu.GetComponentInChildren<Canvas>(true), Camera.main, 600, 1000, "shop-portrait", menu);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/City.unity", new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); game.enabled = false;
            foreach (var segment in game.Track.Segments) { foreach (var cube in segment.Cubes) cube.gameObject.SetActive(false); foreach (var pickup in segment.PowerUps) pickup.gameObject.SetActive(false); }
            foreach (var style in new[] { PlayerTrail.NeonAurora, PlayerTrail.SolarFlare, PlayerTrail.FrostWave, PlayerTrail.RainbowRush })
            {
                game.StartRun(); GameEconomy.EquipTrail(style); Invoke(game, "RefreshEquippedSkin"); Simulate(game, 42);
                Invoke(game, "UpdateCamera", 1f); Capture(null, game.GameCamera, 1672, 940, style.ToString(), null);
                Vector3 position = game.GameCamera.transform.position; Quaternion rotation = game.GameCamera.transform.rotation;
                game.GameCamera.transform.position = game.Player.position + new Vector3(3.8f, 2.6f, -5);
                game.GameCamera.transform.LookAt(game.Player.position + new Vector3(0, 0.25f, -2.1f));
                game.CosmeticTrail.Tick(0.02f);
                Capture(null, game.GameCamera, 1672, 940, style + "-angle", null);
                game.GameCamera.transform.SetPositionAndRotation(position, rotation);
            }
            Assert.That(ShaderUtil.ShaderHasError(game.CosmeticTrail.TrailRenderer.sharedMaterial.shader), Is.False);
        }

        private static void Simulate(CubeDashGame game, int frames)
        {
            for (int i = 0; i < frames; i++)
            { game.Player.position += new Vector3(Mathf.Sin(i * 0.12f) * 0.015f, 0, 0.22f); game.CosmeticTrail.Tick(0.02f); }
        }

        private static void Capture(Canvas canvas, Camera camera, int width, int height, string suffix, MainMenuController menu)
        {
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var mode = canvas != null ? canvas.renderMode : RenderMode.ScreenSpaceOverlay; var oldCamera = canvas != null ? canvas.worldCamera : null;
            float plane = canvas != null ? canvas.planeDistance : 0; bool async = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                if (canvas != null) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f; }
                ShaderUtil.allowAsyncCompilation = false; camera.Render(); Canvas.ForceUpdateCanvases();
                if (menu != null)
                {
                    typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas.GetComponent<CanvasScaler>(), null);
                    menu.ShopPanel.GetComponent<MenuSkinsLayout>().RefreshProducts(); Canvas.ForceUpdateCanvases();
                    var sections = menu.ShopPanel.GetComponent<MenuShopSections>(); sections.Cards[0].GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 0;
                    foreach (var graphic in sections.GetComponentsInChildren<MenuTrailGraphic>()) graphic.Sample(1.1f);
                }
                for (int i = 0; i < 4; i++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_TRAILS_PREVIEW");
                if (!string.IsNullOrEmpty(prefix)) File.WriteAllBytes(prefix + "-" + suffix + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                if (canvas != null) { canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; }
                ShaderUtil.allowAsyncCompilation = async; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
        private static MainMenuController FindMenu(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
        private static void Invoke(CubeDashGame game, string method, params object[] args) => typeof(CubeDashGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);
    }
}
