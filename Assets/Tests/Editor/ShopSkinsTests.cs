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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class ShopSkinsTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [TestCase(PlayerSkin.Supercar)]
        [TestCase(PlayerSkin.MonsterTruck)]
        [TestCase(PlayerSkin.MilitaryVan)]
        public void EachSkinCostsExactly100AndOwnershipIsIndependentPersistentAndNotDoubleCharged(PlayerSkin skin)
        {
            using var economy = new EconomyTestScope();
            Assert.That(GameEconomy.Owns(skin), Is.False);
            Assert.That(GameEconomy.Equip(skin), Is.False);
            GameEconomy.AddCoins(99); Assert.That(GameEconomy.TryBuy(skin), Is.False);
            Assert.That(GameEconomy.Coins, Is.EqualTo(99));
            GameEconomy.AddCoins(101); Assert.That(GameEconomy.TryBuy(skin), Is.True);
            Assert.That(GameEconomy.Coins, Is.EqualTo(100)); Assert.That(GameEconomy.EquippedSkin, Is.EqualTo(PlayerSkin.Cube));
            Assert.That(GameEconomy.TryBuy(skin), Is.False); Assert.That(GameEconomy.Coins, Is.EqualTo(100));
            Assert.That(GameEconomy.Equip(skin), Is.True);
            Assert.That(PlayerPrefs.GetInt(GameEconomy.EquippedKey), Is.EqualTo((int)skin));
            foreach (var other in new[] { PlayerSkin.Supercar, PlayerSkin.MonsterTruck, PlayerSkin.MilitaryVan }.Where(other => other != skin))
                Assert.That(GameEconomy.Owns(other), Is.False);
            GameEconomy.Equip(PlayerSkin.Cube); Assert.That(GameEconomy.Owns(skin), Is.True);
        }

        [Test]
        public void LegacySupercarSaveStillWorksAndInvalidSelectionsCannotGrantSkins()
        {
            using var economy = new EconomyTestScope();
            PlayerPrefs.SetInt(GameEconomy.SupercarOwnedKey, 1); PlayerPrefs.SetInt(GameEconomy.EquippedKey, 1);
            Assert.That(GameEconomy.SupercarEquipped, Is.True);
            Assert.That(GameEconomy.EquippedSkin, Is.EqualTo(PlayerSkin.Supercar));
            foreach (int invalid in new[] { -1, 2, 3, 999 })
            { PlayerPrefs.SetInt(GameEconomy.EquippedKey, invalid); Assert.That(GameEconomy.EquippedSkin, Is.EqualTo(PlayerSkin.Cube)); }
            Assert.That(GameEconomy.TryBuy((PlayerSkin)999), Is.False);
            Assert.That(GameEconomy.Equip((PlayerSkin)999), Is.False);
        }

        [Test]
        public void SavedShopHasOneSkinsGridThreeIllustratedProductsAndNoRaritySections()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = Find<MainMenuController>(scene); var shop = menu.GetComponent<MenuSkinShop>();
                Assert.That(menu.ShopPanel.transform.Find("Card/Heading").GetComponent<Text>().text, Is.EqualTo("SKINS"));
                Assert.That(menu.ShopPanel.GetComponent<MenuSkinsLayout>(), Is.Not.Null);
                foreach (string rarity in new[] { "Common Skins", "Rare Skins", "Epic Skins" })
                    Assert.That(menu.ShopPanel.transform.Find("Card/" + rarity), Is.Null);
                Assert.That(shop.Cards.Select(card => card.Skin), Is.EqualTo(new[] { PlayerSkin.Supercar, PlayerSkin.MonsterTruck, PlayerSkin.MilitaryVan }));
                foreach (var item in shop.Cards)
                {
                    Transform contents = item.transform.parent;
                    Assert.That(contents.Find("Price").GetComponent<Text>().text, Is.EqualTo("100 COINS"));
                    var illustration = contents.Find("Model Illustration").GetComponent<Image>();
                    Assert.That(illustration.sprite, Is.Not.Null);
                    Assert.That(AssetDatabase.GetAssetPath(illustration.sprite), Is.EqualTo(MainMenuSkinsRevamp.IllustrationPath(item.Skin)));
                    Assert.That(item.Action.onClick.GetPersistentTarget(0), Is.SameAs(item));
                    Assert.That(item.Action.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(MenuSkinCard.PurchaseOrToggle)));
                    Assert.That(item.Preview.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(item.Preview.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                }
                int count = menu.GetComponentsInChildren<Transform>(true).Length;
                MainMenuSkinsRevamp.ApplyToScene(scene);
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void PlayerUsesActualUserPrefabsWithForwardFacingGroundedLaneSizedModelsAndFourWheels()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuSkinsRevamp.PlayerPath);
            var models = player.GetComponent<PlayerSupercar>().AdditionalSkins;
            Assert.That(models.Length, Is.EqualTo(2));
            foreach (var model in models)
            {
                string source = model.skin == PlayerSkin.MonsterTruck ? MainMenuSkinsRevamp.MonsterPath : MainMenuSkinsRevamp.MilitaryPath;
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model.root), Is.EqualTo(source));
                Assert.That(model.root.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(model.root.localScale, Is.EqualTo(Vector3.one * 0.55f));
                Assert.That(model.root.gameObject.activeSelf, Is.False);
                Bounds bounds = MainMenuSkinsRevamp.LocalBounds(model.root, player.transform);
                Assert.That(bounds.min.y, Is.EqualTo(-0.575f).Within(0.002f));
                Assert.That(bounds.center.x, Is.EqualTo(0).Within(0.002f));
                Assert.That(bounds.center.z, Is.EqualTo(0).Within(0.002f));
                Assert.That(bounds.size.x, Is.LessThanOrEqualTo(2.201f));
                Assert.That(bounds.size.z, Is.LessThanOrEqualTo(3.601f));
                Assert.That(model.wheels.Length, Is.EqualTo(4));
                Assert.That(model.wheels.All(wheel => wheel != null), Is.True);
                Assert.That(model.root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(model.root.GetComponentsInChildren<MonoBehaviour>(true).All(component => component is SupercarLightTrail), Is.True);
            }
            var preview = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/PlayerCubeMenuPreview.prefab");
            Assert.That(preview.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(3), "Menu cube contains only its body and two eyes.");
        }

        [Test]
        public void DesertMapCardReferencesItsFullResolutionActualSceneIllustration()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = Find<MainMenuController>(scene);
                var preview = menu.MapPanel.transform.Find("Card/Desert/Map illustration").GetComponent<MapPreviewGraphic>();
                Assert.That(preview.Illustration, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(preview.Illustration), Is.EqualTo(MainMenuMapIllustrations.ImagePath("Desert")));
                Assert.That(preview.Illustration.width, Is.EqualTo(MainMenuMapIllustrations.Width));
                Assert.That(preview.Illustration.height, Is.EqualTo(MainMenuMapIllustrations.Height));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator NewCardsBuyEquipSwitchUnequipAndIgnoreHiddenOrClosingCallbacks()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); var shop = menu.GetComponent<MenuSkinShop>();
            GameEconomy.AddCoins(300);
            foreach (var item in shop.Cards) item.PurchaseOrToggle();
            Assert.That(GameEconomy.Coins, Is.EqualTo(300));
            menu.OpenShop();
            foreach (var item in shop.Cards)
            {
                ExecuteEvents.Execute(item.Action.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Assert.That(GameEconomy.Owns(item.Skin), Is.True);
                Assert.That(item.Action.GetComponentInChildren<Text>().text, Is.EqualTo("EQUIP"));
                item.Action.onClick.Invoke(); Assert.That(GameEconomy.EquippedSkin, Is.EqualTo(item.Skin));
                Assert.That(shop.CubePreview.activeSelf, Is.False);
                foreach (var other in shop.Cards) Assert.That(other.Preview.activeSelf, Is.EqualTo(other == item));
            }
            Assert.That(GameEconomy.Coins, Is.Zero); Assert.That(shop.MenuCoins.text, Is.EqualTo("0")); Assert.That(shop.ShopCoins.text, Is.EqualTo("0"));
            menu.ClosePanels(); shop.Cards[2].PurchaseOrToggle();
            Assert.That(GameEconomy.EquippedSkin, Is.EqualTo(PlayerSkin.MilitaryVan));
            yield return MainMenuSectionTests.WaitForPanelClosed(menu.ShopPanel);
            menu.OpenShop(); shop.Cards[2].Action.onClick.Invoke();
            Assert.That(GameEconomy.EquippedSkin, Is.EqualTo(PlayerSkin.Cube)); Assert.That(shop.CubePreview.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator BothNewSkinsWorkOnEveryMapRestoreAfterPowerUpsAndRetryAndKeepCollisionsAndPause()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
            GameEconomy.AddCoins(200); GameEconomy.TryBuy(PlayerSkin.MonsterTruck); GameEconomy.TryBuy(PlayerSkin.MilitaryVan);
            foreach (string map in PauseSoundAuthoring.Maps)
            {
                GameEconomy.Equip(PlayerSkin.MonsterTruck);
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + map + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun();
                foreach (var segment in game.Track.Segments)
                { foreach (var cube in segment.Cubes) cube.gameObject.SetActive(false); foreach (var pickup in segment.PowerUps) pickup.gameObject.SetActive(false); }
                var collider = game.Player.GetComponent<BoxCollider>(); Vector3 size = collider.size, center = collider.center;
                foreach (var skin in new[] { PlayerSkin.MonsterTruck, PlayerSkin.MilitaryVan })
                {
                    GameEconomy.Equip(skin);
                    Assert.That(game.VehicleDriving, Is.True, map); Assert.That(game.SupercarEnabled, Is.False);
                    Assert.That(game.EquippedSkin, Is.EqualTo(skin));
                    var presentation = game.SupercarPresentation;
                    Assert.That(presentation.Car, Is.SameAs(presentation.AdditionalSkins.Single(model => model.skin == skin).root));
                    Assert.That(presentation.Car.gameObject.activeSelf, Is.True);
                    Assert.That(game.Player.Find("Cube Visual").GetComponent<Renderer>().enabled, Is.False);
                    presentation.Tick(0.2f, game.Speed, 8);
                    Assert.That(Quaternion.Angle(presentation.Wheels[0].localRotation, Quaternion.identity), Is.GreaterThan(0.1f));
                    game.TogglePause(); Quaternion wheel = presentation.Wheels[0].localRotation; Vector3 position = presentation.Car.localPosition;
                    yield return null; yield return null;
                    Assert.That(presentation.Wheels[0].localRotation, Is.EqualTo(wheel)); Assert.That(presentation.Car.localPosition, Is.EqualTo(position));
                    game.TogglePause();
                    Invoke(game, "ApplyPowerUp", PowerUpType.Truck); Assert.That(game.VehicleDriving, Is.False);
                    Assert.That(presentation.Car.gameObject.activeSelf, Is.False);
                    Invoke(game, "UpdateTruckTimers", 30f); Assert.That(game.VehicleDriving, Is.True);
                    Invoke(game, "ApplyPowerUp", PowerUpType.FighterPlane); Assert.That(game.VehicleDriving, Is.False);
                    Invoke(game, "UpdateFlightTimers", 30f); Assert.That(game.VehicleDriving, Is.True);
                    game.StartRun(); Assert.That(game.EquippedSkin, Is.EqualTo(skin)); Assert.That(presentation.Car.gameObject.activeSelf, Is.True);
                    Assert.That(collider.size, Is.EqualTo(size)); Assert.That(collider.center, Is.EqualTo(center));
                    Assert.That(game.Trucking || game.Flying, Is.False, "These are cosmetic skins, not permanent power-ups.");
                }
            }
        }

        [UnityTest]
        public IEnumerator ShopAndDesertCardRenderAndAllProductsRemainReachableInLandscapeAndPortrait()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("GPU required.");
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            menu.OpenShop();
            typeof(MenuPanelMotion).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu.ShopPanel.GetComponent<MenuPanelMotion>(), new object[] { 1f });
            Capture(menu, 1672, 940, "shop-desktop"); Capture(menu, 600, 1000, "shop-portrait"); Capture(menu, 1280, 800, "shop-16-10");
            menu.StartGame();
            typeof(MenuPanelMotion).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu.MapPanel.GetComponent<MenuPanelMotion>(), new object[] { 1f });
            Capture(menu, 1672, 940, "maps-desktop");
        }

        private static void Capture(MainMenuController menu, int width, int height, string suffix)
        {
            var camera = Camera.main; var canvas = menu.GetComponentInChildren<Canvas>(true);
            var oldTarget = camera.targetTexture; var active = RenderTexture.active;
            var target = new RenderTexture(width, height, 24); var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                camera.targetTexture = target; camera.Render();
                typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas.GetComponent<CanvasScaler>(), null);
                Canvas.ForceUpdateCanvases();
                menu.ShopPanel.GetComponent<MenuSkinsLayout>().Refresh(); menu.MapPanel.GetComponent<MenuMapLayout>().Refresh(); Canvas.ForceUpdateCanvases();
                if (menu.CurrentPanel == menu.ShopPanel)
                {
                    var card = menu.ShopPanel.transform.Find("Card");
                    Bounds title = RectTransformUtility.CalculateRelativeRectTransformBounds(card, card.Find("Heading"));
                    Bounds icon = RectTransformUtility.CalculateRelativeRectTransformBounds(card, card.Find("Section icon"));
                    Assert.That(title.min.x, Is.GreaterThan(icon.max.x), "The Skins heading must not overlap its shopping-cart icon.");
                    var shop = menu.GetComponent<MenuSkinShop>();
                    foreach (var item in shop.Cards)
                    {
                        EventSystem.current.SetSelectedGameObject(item.Action.gameObject); Canvas.ForceUpdateCanvases();
                        var scroll = item.GetComponentInParent<ScrollRect>();
                        Bounds button = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, item.Action.transform);
                        Assert.That(button.min.y, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 2));
                        Assert.That(button.max.y, Is.LessThanOrEqualTo(scroll.viewport.rect.yMax + 2));
                    }
                    ScrollToTop(shop);
                }
                for (int i = 0; i < 6; i++) camera.Render();
                RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_SHOP_PREVIEW");
                if (!string.IsNullOrEmpty(prefix)) File.WriteAllBytes(prefix + "-" + suffix + ".png", texture.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; camera.targetTexture = oldTarget;
                RenderTexture.active = active; ShaderUtil.allowAsyncCompilation = async; Object.DestroyImmediate(target); Object.DestroyImmediate(texture);
            }
        }
        private static void ScrollToTop(MenuSkinShop shop)
        {
            EventSystem.current.SetSelectedGameObject(null);
            var scroll = shop.Cards[0].GetComponentInParent<ScrollRect>(); scroll.StopMovement(); scroll.verticalNormalizedPosition = 1;
            Canvas.ForceUpdateCanvases();
        }
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static void Invoke(CubeDashGame game, string method, params object[] args)
            => typeof(CubeDashGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);
    }
}
