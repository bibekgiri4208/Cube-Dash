using System.Collections;
using System.Collections.Generic;
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
    public sealed class EconomyTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void WalletPurchasesAndEquipmentPersistWithoutNegativeBalanceOrRepeatCharges()
        {
            using var economy = new EconomyTestScope();
            Assert.That(GameEconomy.Coins, Is.Zero); Assert.That(GameEconomy.SupercarEquipped, Is.False);
            Assert.That(GameEconomy.EquipSupercar(true), Is.False);
            GameEconomy.AddCoins(-10); Assert.That(GameEconomy.Coins, Is.Zero);
            GameEconomy.AddCoins(99); Assert.That(GameEconomy.TryBuySupercar(), Is.False);
            Assert.That(GameEconomy.Coins, Is.EqualTo(99)); Assert.That(GameEconomy.OwnsSupercar, Is.False);
            GameEconomy.AddCoins(1); Assert.That(GameEconomy.TryBuySupercar(), Is.True);
            Assert.That(GameEconomy.Coins, Is.Zero); Assert.That(GameEconomy.OwnsSupercar, Is.True);
            Assert.That(GameEconomy.SupercarEquipped, Is.False, "Buying and equipping are separate choices.");
            Assert.That(GameEconomy.TryBuySupercar(), Is.False); Assert.That(GameEconomy.Coins, Is.Zero);
            Assert.That(GameEconomy.EquipSupercar(true), Is.True);
            Assert.That(PlayerPrefs.GetInt(GameEconomy.EquippedKey), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt(GameEconomy.SupercarOwnedKey), Is.EqualTo(1));
            Assert.That(GameEconomy.EquipSupercar(false), Is.True);
            Assert.That(GameEconomy.OwnsSupercar, Is.True); Assert.That(GameEconomy.Coins, Is.Zero);
            PlayerPrefs.SetInt(GameEconomy.EquippedKey, 1); PlayerPrefs.SetInt(GameEconomy.SupercarOwnedKey, 0);
            Assert.That(GameEconomy.SupercarEquipped, Is.False, "An invalid saved selection cannot grant an unowned skin.");
            PlayerPrefs.SetInt(GameEconomy.CoinsKey, -100); Assert.That(GameEconomy.Coins, Is.Zero);
            GameEconomy.AddCoins(int.MaxValue); GameEconomy.AddCoins(1);
            Assert.That(GameEconomy.Coins, Is.EqualTo(int.MaxValue), "Overflow must not turn the balance negative.");
        }

        [Test]
        public void MenuHasThemedWalletsEpicModelIllustrationAndSafeNavigationAndReauthoring()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                var shop = menu.GetComponent<MenuSkinShop>(); Assert.That(shop, Is.Not.Null);
                Assert.That(shop.MenuCoins.transform.parent.name, Is.EqualTo("Coin Wallet"));
                Assert.That(shop.ShopCoins.transform.parent.name, Is.EqualTo("Shop Coin Wallet"));
                Assert.That(shop.MenuCoins.transform.parent.GetComponent<MenuPanelGraphic>().CornerCut, Is.GreaterThan(0));
                Assert.That(shop.ShopCoins.transform.parent.GetComponent<RectTransform>().anchoredPosition.x, Is.LessThan(0));
                Assert.That(shop.ShopCoins.transform.parent.GetComponent<RectTransform>().anchoredPosition.y, Is.LessThan(-200));
                Transform product = menu.ShopPanel.transform.Find("Card/Epic Skins/Skin viewport/Skin slots/Epic Supercar");
                Assert.That(product.Find("Price").GetComponent<Text>().text, Is.EqualTo("100 COINS"));
                Assert.That(AssetDatabase.GetAssetPath(product.Find("Car Model Illustration").GetComponent<Image>().sprite), Is.EqualTo(MainMenuEconomyAuthoring.IllustrationPath));
                Assert.That(shop.CarPreview.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(shop.CarPreview.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                var back = menu.ShopPanel.transform.Find("Card/BACK").GetComponent<Button>();
                Assert.That(back.navigation.selectOnUp, Is.SameAs(shop.Action)); Assert.That(shop.Action.navigation.selectOnDown, Is.SameAs(back));
                int count = menu.GetComponentsInChildren<Transform>(true).Length;
                MainMenuEconomyAuthoring.ApplyToScene(scene);
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator ShopBuysOnceEquipsUnequipsUpdatesBothWalletsAndRestoresAcrossSceneLoads()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); var shop = menu.GetComponent<MenuSkinShop>();
            Assert.That(shop.MenuCoins.text, Is.EqualTo("0")); Assert.That(shop.ShopCoins.text, Is.EqualTo("0"));
            shop.PurchaseOrToggle(); Assert.That(GameEconomy.OwnsSupercar, Is.False, "A hidden shop cannot buy.");
            menu.OpenShop(); Assert.That(shop.Action.interactable, Is.False);
            GameEconomy.AddCoins(99); Assert.That(shop.MenuCoins.text, Is.EqualTo("99")); Assert.That(shop.Action.interactable, Is.False);
            GameEconomy.AddCoins(1); Assert.That(shop.Action.interactable, Is.True);
            EventSystem.current.SetSelectedGameObject(shop.Action.gameObject);
            ExecuteEvents.Execute(shop.Action.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(GameEconomy.OwnsSupercar, Is.True); Assert.That(GameEconomy.Coins, Is.Zero);
            Assert.That(shop.Action.GetComponentInChildren<Text>().text, Is.EqualTo("EQUIP"));
            Assert.That(shop.MenuCoins.text, Is.EqualTo("0")); Assert.That(shop.ShopCoins.text, Is.EqualTo("0"));
            Canvas.ForceUpdateCanvases();
            var graphic = shop.Action.targetGraphic;
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, graphic.rectTransform.TransformPoint(graphic.rectTransform.rect.center))
            };
            var hits = new List<RaycastResult>();
            menu.GetComponentInChildren<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(shop.Action.gameObject));
            ExecuteEvents.Execute(shop.Action.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(GameEconomy.SupercarEquipped, Is.True);
            Assert.That(shop.CarPreview.activeInHierarchy, Is.True); Assert.That(shop.CubePreview.activeSelf, Is.False);
            Assert.That(shop.Action.GetComponentInChildren<Text>().text, Is.EqualTo("UNEQUIP"));
            menu.ClosePanels(); shop.PurchaseOrToggle(); Assert.That(GameEconomy.SupercarEquipped, Is.True, "A closing panel cannot change equipment.");
            yield return MainMenuSectionTests.WaitForPanelClosed(menu.ShopPanel);
            menu.OpenShop(); shop.Action.onClick.Invoke(); Assert.That(GameEconomy.SupercarEquipped, Is.False);
            Assert.That(shop.CubePreview.activeInHierarchy, Is.True); Assert.That(shop.CarPreview.activeSelf, Is.False);
            shop.Action.onClick.Invoke();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/City.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Assert.That(Object.FindAnyObjectByType<CubeDashGame>().SupercarDriving, Is.True);
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            shop = Object.FindAnyObjectByType<MenuSkinShop>();
            Assert.That(shop.CarPreview.activeInHierarchy, Is.True); Assert.That(GameEconomy.Coins, Is.Zero);
            Assert.That(GameEconomy.OwnsSupercar, Is.True);
        }

        [UnityTest]
        public IEnumerator EquippedSkinSpawnsOnEveryMapAndReturnsAfterTruckAndFlightAndRetry()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
            GameEconomy.AddCoins(100); GameEconomy.TryBuySupercar(); GameEconomy.EquipSupercar(true);
            foreach (string map in PauseSoundAuthoring.Maps)
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + map + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var game = Object.FindAnyObjectByType<CubeDashGame>();
                Assert.That(game.SupercarDriving, Is.True, "The equipped car must spawn even before the countdown ends: " + map);
                game.enabled = false; game.StartRun();
                Assert.That(game.SupercarPresentation.Car.gameObject.activeSelf, Is.True, map);
                Assert.That(game.Player.Find("Cube Visual").GetComponent<Renderer>().enabled, Is.False, map);
                Vector3 size = game.Player.GetComponent<BoxCollider>().size;
                Invoke(game, "ApplyPowerUp", PowerUpType.Truck); Assert.That(game.SupercarDriving, Is.False);
                Invoke(game, "UpdateTruckTimers", 11f); Assert.That(game.SupercarDriving, Is.True);
                Invoke(game, "ApplyPowerUp", PowerUpType.FighterPlane); Assert.That(game.SupercarDriving, Is.False);
                Invoke(game, "UpdateFlightTimers", 11f); Assert.That(game.SupercarDriving, Is.True);
                game.RequestRun(); Assert.That(game.SupercarDriving, Is.True);
                Assert.That(game.Player.GetComponent<BoxCollider>().size, Is.EqualTo(size));
                GameEconomy.EquipSupercar(false); game.StartRun();
                Assert.That(game.SupercarDriving, Is.False); Assert.That(game.Player.Find("Cube Visual").GetComponent<Renderer>().enabled, Is.True);
                GameEconomy.EquipSupercar(true);
            }
        }

        [UnityTest]
        public IEnumerator PhysicalCoinBanksOnceNotMultipliedScoreAndSurvivesRetryAndMenuReturn()
        {
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.enabled = false; game.StartRun();
            Clear(game); Invoke(game, "ApplyPowerUp", PowerUpType.DoublePoints);
            PlaceCoin(game); Invoke(game, "Update");
            Assert.That(game.Score, Is.EqualTo(2)); Assert.That(GameEconomy.Coins, Is.EqualTo(1));
            Invoke(game, "Update"); Assert.That(GameEconomy.Coins, Is.EqualTo(1));
            game.StartRun(); Clear(game); Assert.That(GameEconomy.Coins, Is.EqualTo(1));
            game.ToggleInvincibility(); PlaceCoin(game); Invoke(game, "Update");
            Assert.That(GameEconomy.Coins, Is.EqualTo(1), "Testing invincibility cannot farm shop currency.");
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Assert.That(Object.FindAnyObjectByType<MenuSkinShop>().MenuCoins.text, Is.EqualTo("1"));
        }

        private static void PlaceCoin(CubeDashGame game)
        {
            var coin = game.Track.Segments[1].Cubes[0]; coin.Configure(game.PlayerCubeColor, game.Track.ColorMaterial(game.PlayerCubeColor), true);
            coin.transform.position = game.Player.position + Vector3.forward * 0.2f; coin.gameObject.SetActive(true);
        }
        private static void Clear(CubeDashGame game)
        {
            foreach (var segment in game.Track.Segments)
            {
                foreach (var cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (var pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
        }
        private static void Invoke(CubeDashGame game, string method, params object[] args)
            => typeof(CubeDashGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args);

        [UnityTest]
        public IEnumerator EconomyScreensRenderInLandscapeAndPortrait()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("GPU required.");
            yield return new EnterPlayMode();
            using var economy = new EconomyTestScope();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); menu.GetComponentInChildren<MainMenuEntrance>().Complete();
            GameEconomy.AddCoins(1250); GameEconomy.TryBuySupercar(); GameEconomy.EquipSupercar(true);
            Capture(menu, 1672, 940, "menu-equipped");
            menu.OpenShop();
            typeof(MenuPanelMotion).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu.ShopPanel.GetComponent<MenuPanelMotion>(), new object[] { 1f });
            Capture(menu, 1672, 940, "shop-epic"); Capture(menu, 600, 1000, "shop-portrait");
        }
        private static void Capture(MainMenuController menu, int width, int height, string suffix)
        {
            var camera = Camera.main; var canvas = menu.GetComponentInChildren<Canvas>(true);
            var oldTarget = camera.targetTexture; var active = RenderTexture.active;
            var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                camera.targetTexture = target; Canvas.ForceUpdateCanvases(); menu.GetComponent<MenuEconomyLayout>().Refresh(); Canvas.ForceUpdateCanvases();
                for (int i = 0; i < 4; i++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_ECONOMY_PREVIEW");
                if (!string.IsNullOrEmpty(prefix)) File.WriteAllBytes(prefix + "-" + suffix + ".png", image.EncodeToPNG());
                Assert.That(menu.GetComponent<MenuSkinShop>().MenuCoins.text, Is.EqualTo("1,150"));
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; camera.targetTexture = oldTarget;
                RenderTexture.active = active; ShaderUtil.allowAsyncCompilation = async; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
