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
    public sealed class SharkCubeTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void SharkHasAStableNewSaveIdAndIsNotAVehicle()
        {
            Assert.That((int)PlayerSkin.Supercar, Is.EqualTo(1));
            Assert.That((int)PlayerSkin.MonsterTruck, Is.EqualTo(2));
            Assert.That((int)PlayerSkin.MilitaryVan, Is.EqualTo(3));
            Assert.That((int)PlayerSkin.SharkCube, Is.EqualTo(4));
            Assert.That(GameEconomy.SkinName(PlayerSkin.SharkCube), Is.EqualTo("SHARK CUBE"));
            Assert.That(GameEconomy.IsVehicleSkin(PlayerSkin.SharkCube), Is.False);
            Assert.That(GameEconomy.IsVehicleSkin(PlayerSkin.Cube), Is.False);
            foreach (var skin in new[] { PlayerSkin.Supercar, PlayerSkin.MonsterTruck, PlayerSkin.MilitaryVan })
                Assert.That(GameEconomy.IsVehicleSkin(skin), Is.True);
        }

        [Test]
        public void AuthoredMeshHasFinsWhiteBellyAndGillsWithTheOriginalGroundPlane()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCubePresentationBuilder.PlayerPath);
            var visual = player.transform.Find("Cube Visual").GetComponent<PlayerCubeVisual>();
            Assert.That(AssetDatabase.GetAssetPath(visual.SharkMesh), Is.EqualTo(SharkCubeAuthoring.MeshPath));
            Assert.That(visual.SharkMesh.subMeshCount, Is.EqualTo(3));
            Assert.That(visual.SharkMaterials.Length, Is.EqualTo(3));
            Assert.That(visual.SharkMesh.bounds.min.y, Is.EqualTo(visual.GetComponent<MeshFilter>().sharedMesh.bounds.min.y).Within(0.00001f));
            Assert.That(visual.SharkMesh.bounds.max.y, Is.GreaterThan(0.78f), "Raised dorsal fin.");
            Assert.That(visual.SharkMesh.bounds.max.x, Is.GreaterThan(0.74f));
            Assert.That(visual.SharkMesh.bounds.min.x, Is.LessThan(-0.74f), "Matching side fins.");
            Assert.That(visual.SharkMesh.vertexCount, Is.LessThan(3000));
            for (int i = 0; i < 3; i++) Assert.That(visual.SharkMesh.GetTriangles(i).Length, Is.GreaterThan(0));
            Color blue = visual.SharkMaterials[0].GetColor("_BaseColor");
            Color belly = visual.SharkMaterials[1].GetColor("_BaseColor");
            Color gills = visual.SharkMaterials[2].GetColor("_BaseColor");
            Assert.That(blue.b, Is.GreaterThan(blue.r * 2));
            Assert.That(Mathf.Min(belly.r, Mathf.Min(belly.g, belly.b)), Is.GreaterThan(0.85f));
            Assert.That(gills.grayscale, Is.LessThan(blue.grayscale));
            Assert.That(visual.GetComponent<MeshFilter>().sharedMesh, Is.Not.SameAs(visual.SharkMesh), "Default cube stays authored as the original cube.");
        }

        [Test]
        public void SwappingTheSkinKeepsEyesBlinkingAndNeverChangesTheColliderOrHierarchy()
        {
            var player = PrefabUtility.LoadPrefabContents(PlayerCubePresentationBuilder.PlayerPath);
            try
            {
                var visual = player.transform.Find("Cube Visual").GetComponent<PlayerCubeVisual>();
                var collider = player.GetComponent<BoxCollider>(); Vector3 size = collider.size, center = collider.center;
                Mesh original = visual.GetComponent<MeshFilter>().sharedMesh;
                Material[] materials = visual.Body.sharedMaterials;
                int count = player.GetComponentsInChildren<Transform>(true).Length;
                visual.UseGameplayAnimation(); visual.ApplySkin(PlayerSkin.SharkCube);
                Assert.That(visual.SharkEquipped, Is.True);
                Assert.That(visual.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(visual.SharkMesh));
                Assert.That(visual.Body.sharedMaterials, Is.EqualTo(visual.SharkMaterials));
                visual.TickBlink(PlayerCubeVisual.BlinkStart + 0.07f);
                foreach (var eye in visual.Eyes) Assert.That(eye.transform.localScale.y, Is.LessThan(0.03f));
                visual.Body.enabled = false; visual.SyncEyes(); Assert.That(visual.Eyes.All(eye => !eye.enabled), Is.True);
                visual.Body.enabled = true; visual.SyncEyes(); Assert.That(visual.Eyes.All(eye => eye.enabled), Is.True);
                visual.ApplySkin(PlayerSkin.Cube);
                Assert.That(visual.SharkEquipped, Is.False); Assert.That(visual.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(original));
                Assert.That(visual.Body.sharedMaterials, Is.EqualTo(materials));
                Assert.That(collider.size, Is.EqualTo(size)); Assert.That(collider.center, Is.EqualTo(center));
                Assert.That(player.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }

        [Test]
        public void DedicatedPreviewHasOnlyTheBlinkingCubeAndTheShopUpgradeIsRepeatable()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharkCubeAuthoring.PrefabPath);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).All(item => item is PlayerCubeVisual), Is.True);
            var visual = prefab.GetComponentInChildren<PlayerCubeVisual>(true);
            Assert.That(visual.AutoplayIdle, Is.True); Assert.That(visual.Eyes.Length, Is.EqualTo(2));
            Assert.That(visual.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(visual.SharkMesh));
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = FindMenu(scene); var shop = menu.GetComponent<MenuSkinShop>();
                var card = shop.Cards.Single(item => item.Skin == PlayerSkin.SharkCube);
                Assert.That(card.Preview.activeSelf, Is.False);
                Assert.That(card.Action.onClick.GetPersistentTarget(0), Is.SameAs(card));
                Assert.That(AssetDatabase.GetAssetPath(card.transform.parent.Find("Model Illustration").GetComponent<Image>().sprite), Is.EqualTo(SharkCubeAuthoring.IllustrationPath));
                var name = card.transform.parent.Find("Name").GetComponent<Text>(); name.text = "MY SHARK";
                int count = menu.GetComponentsInChildren<Transform>(true).Length;
                SharkCubeAuthoring.ApplyToScene(scene);
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(name.text, Is.EqualTo("MY SHARK"));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator SharkBuyEquipUnequipAndSavedSelectionSurviveMenuReload()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); var shop = menu.GetComponent<MenuSkinShop>();
            var shark = shop.Cards.Single(item => item.Skin == PlayerSkin.SharkCube);
            GameEconomy.AddCoins(99); menu.OpenShop(); shark.PurchaseOrToggle();
            Assert.That(GameEconomy.Owns(PlayerSkin.SharkCube), Is.False); Assert.That(GameEconomy.Coins, Is.EqualTo(99));
            GameEconomy.AddCoins(1); shark.PurchaseOrToggle();
            Assert.That(GameEconomy.Owns(PlayerSkin.SharkCube), Is.True); Assert.That(GameEconomy.Coins, Is.Zero);
            Assert.That(GameEconomy.EquippedSkin, Is.EqualTo(PlayerSkin.Cube));
            shark.PurchaseOrToggle(); Assert.That(shark.Preview.activeSelf, Is.True); Assert.That(shop.CubePreview.activeSelf, Is.False);
            Assert.That(GameEconomy.EquippedSkin, Is.EqualTo(PlayerSkin.SharkCube));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            menu = Object.FindAnyObjectByType<MainMenuController>(); shop = menu.GetComponent<MenuSkinShop>();
            shark = shop.Cards.Single(item => item.Skin == PlayerSkin.SharkCube);
            Assert.That(shark.Preview.activeSelf, Is.True); Assert.That(GameEconomy.Coins, Is.Zero);
            menu.OpenShop(); shark.PurchaseOrToggle();
            Assert.That(GameEconomy.EquippedSkin, Is.EqualTo(PlayerSkin.Cube));
            Assert.That(shop.CubePreview.activeSelf, Is.True); Assert.That(shark.Preview.activeSelf, Is.False);
            Assert.That(GameEconomy.Owns(PlayerSkin.SharkCube), Is.True);
        }

        [UnityTest]
        public IEnumerator SharkWorksOnEveryMapAndSurvivesPowerUpsPauseSkinSwitchingAndRetry()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
            GameEconomy.AddCoins(200); GameEconomy.TryBuy(PlayerSkin.SharkCube); GameEconomy.TryBuy(PlayerSkin.Supercar);
            foreach (string map in PauseSoundAuthoring.Maps)
            {
                GameEconomy.Equip(PlayerSkin.SharkCube);
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + map + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun();
                foreach (var segment in game.Track.Segments)
                { foreach (var cube in segment.Cubes) cube.gameObject.SetActive(false); foreach (var pickup in segment.PowerUps) pickup.gameObject.SetActive(false); }
                var visual = game.Player.Find("Cube Visual").GetComponent<PlayerCubeVisual>();
                var collider = game.Player.GetComponent<BoxCollider>(); Vector3 size = collider.size, center = collider.center;
                Assert.That(visual.SharkEquipped && visual.Body.enabled, Is.True, map);
                Assert.That(game.VehicleDriving || game.SupercarDriving || game.SupercarPresentation.Driving, Is.False, map);
                Assert.That(visual.Body.sharedMaterials, Is.EqualTo(visual.SharkMaterials));
                Invoke(game, "ApplyPowerUp", PowerUpType.Shield);
                Assert.That(game.Shields, Is.EqualTo(1)); Assert.That(game.SupercarPresentation.Driving, Is.False);
                Invoke(game, "ApplyPowerUp", PowerUpType.Truck); Assert.That(visual.Body.enabled, Is.False);
                Invoke(game, "UpdateTruckTimers", 30f); Assert.That(visual.Body.enabled && visual.SharkEquipped, Is.True);
                Invoke(game, "ApplyPowerUp", PowerUpType.FighterPlane); Assert.That(visual.Body.enabled, Is.False);
                Invoke(game, "UpdateFlightTimers", 30f); Assert.That(visual.Body.enabled && visual.SharkEquipped, Is.True);
                game.TogglePause(); Vector3 scale = visual.transform.localScale; float blink = visual.BlinkAge;
                yield return null; yield return null;
                Assert.That(visual.transform.localScale, Is.EqualTo(scale)); Assert.That(visual.BlinkAge, Is.EqualTo(blink));
                game.TogglePause();
                GameEconomy.Equip(PlayerSkin.Supercar); Assert.That(game.VehicleDriving, Is.True); Assert.That(visual.SharkEquipped, Is.False);
                GameEconomy.Equip(PlayerSkin.SharkCube); Assert.That(game.VehicleDriving, Is.False); Assert.That(visual.Body.enabled, Is.True);
                game.StartRun(); yield return null;
                Assert.That(visual.SharkEquipped, Is.True); Assert.That(visual.Body.sharedMaterials, Is.EqualTo(visual.SharkMaterials));
                var block = new MaterialPropertyBlock(); visual.Body.GetPropertyBlock(block);
                Assert.That(block.isEmpty, Is.True, "Gameplay red/emission must not overwrite the blue/white shark materials.");
                Assert.That(collider.size, Is.EqualTo(size)); Assert.That(collider.center, Is.EqualTo(center));
                Assert.That(visual.Eyes.All(eye => eye.enabled), Is.True);
            }
        }

        [Test]
        public void RenderSharkMenuAndShopWithoutStartingAudio()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = FindMenu(scene); var shop = menu.GetComponent<MenuSkinShop>();
                var shark = shop.Cards.Single(item => item.Skin == PlayerSkin.SharkCube);
                shop.CubePreview.SetActive(false); shark.Preview.SetActive(true);
                Capture(menu, 1672, 940, "menu", false);
                menu.OpenShop(); var motion = menu.ShopPanel.GetComponent<MenuPanelMotion>();
                typeof(MenuPanelMotion).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(motion, null);
                typeof(MenuPanelMotion).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(motion, new object[] { 1f });
                Capture(menu, 1672, 940, "shop-desktop", true);
                Capture(menu, 600, 1000, "shop-portrait", true);
                Assert.That(menu.Music.isPlaying, Is.False);
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        private static void Capture(MainMenuController menu, int width, int height, string suffix, bool shopVisible)
        {
            Camera camera = menu.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single(item => item.CompareTag("MainCamera"));
            Canvas canvas = menu.GetComponentInChildren<Canvas>(true);
            var previous = camera.targetTexture; var active = RenderTexture.active; var mode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            float plane = canvas.planeDistance; bool async = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                ShaderUtil.allowAsyncCompilation = false; Canvas.ForceUpdateCanvases();
                typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas.GetComponent<CanvasScaler>(), null);
                if (shopVisible)
                {
                    menu.ShopPanel.GetComponent<MenuSkinsLayout>().RefreshProducts(); Canvas.ForceUpdateCanvases();
                    var scroll = menu.GetComponent<MenuSkinShop>().Cards[0].GetComponentInParent<ScrollRect>();
                    scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
                    var shark = menu.GetComponent<MenuSkinShop>().Cards.Single(item => item.Skin == PlayerSkin.SharkCube);
                    Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, shark.Action.transform);
                    Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 2));
                    Assert.That(bounds.max.y, Is.LessThanOrEqualTo(scroll.viewport.rect.yMax + 2));
                }
                for (int i = 0; i < 5; i++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_SHARK_PREVIEW");
                if (!string.IsNullOrEmpty(prefix)) File.WriteAllBytes(prefix + "-" + suffix + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active; canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane;
                ShaderUtil.allowAsyncCompilation = async; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }

        private static MainMenuController FindMenu(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
        private static void Invoke(CubeDashGame game, string method, params object[] args)
            => typeof(CubeDashGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);
    }
}
