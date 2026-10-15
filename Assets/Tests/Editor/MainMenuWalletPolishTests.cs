using System.Collections;
using System.IO;
using System.Linq;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class MainMenuWalletPolishTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void WalletHasLayeredGoldCoinArtworkLargerBalanceAndNoInputBlockingDecorations()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var shop = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MenuSkinShop>(true)).Single();
                Transform wallet = shop.MenuCoins.transform.parent;
                Assert.That(wallet.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(320, 90)));
                Assert.That(wallet.Find("Gold Star Coins").GetComponent<MenuCoinGraphic>(), Is.Not.Null);
                Assert.That(wallet.Find("Coin").gameObject.activeSelf, Is.False);
                Assert.That(wallet.Find("Gold Accent"), Is.Null, "The unwanted yellow accent is removed.");
                Assert.That(wallet.Find("Caption").GetComponent<Text>().text, Is.EqualTo("YOUR COINS"));
                Assert.That(shop.MenuCoins.fontSize, Is.EqualTo(38));
                Assert.That(shop.MenuCoins.resizeTextForBestFit, Is.True);
                Assert.That(wallet.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget), Is.True);
                Assert.That(wallet.GetComponentsInChildren<Selectable>(true), Is.Empty);
                Assert.That(wallet.parent.Find("Wallet Drop Shadow").GetSiblingIndex(), Is.LessThan(wallet.GetSiblingIndex()));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ReapplicationKeepsReferencesAndShopUnchangedAndDoesNotDuplicateOrMoveShadowInFront()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var shop = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MenuSkinShop>(true)).Single();
                Text balance = shop.MenuCoins;
                string shopWallet = EditorJsonUtility.ToJson(shop.ShopCoins.transform.parent.GetComponent<RectTransform>());
                string shopLabel = EditorJsonUtility.ToJson(shop.ShopCoins);
                int count = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length);
                for (int i = 0; i < 3; i++) MainMenuWalletPolish.ApplyToScene(scene);
                Assert.That(scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Transform>(true).Length), Is.EqualTo(count));
                Assert.That(shop.MenuCoins, Is.SameAs(balance));
                Assert.That(EditorJsonUtility.ToJson(shop.ShopCoins.transform.parent.GetComponent<RectTransform>()), Is.EqualTo(shopWallet));
                Assert.That(EditorJsonUtility.ToJson(shop.ShopCoins), Is.EqualTo(shopLabel));
                Transform wallet = balance.transform.parent;
                Assert.That(wallet.parent.Find("Wallet Drop Shadow").GetSiblingIndex(), Is.LessThan(wallet.GetSiblingIndex()));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator BalanceStillUpdatesAndWalletFitsSixteenNineAndSixteenTenIncludingLargeBalances()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("GPU required.");
            yield return new EnterPlayMode(); using var economy = new EconomyTestScope();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var shop = Object.FindAnyObjectByType<MenuSkinShop>();
            Object.FindAnyObjectByType<MainMenuEntrance>().Complete();
            Assert.That(shop.MenuCoins.text, Is.EqualTo("0"));
            GameEconomy.AddCoins(1250); Assert.That(shop.MenuCoins.text, Is.EqualTo(GameEconomy.Coins.ToString("N0")));
            Capture(shop, 1672, 940, "16-9"); Capture(shop, 1920, 1200, "16-10"); Capture(shop, 1280, 800, "1280-800");
            GameEconomy.AddCoins(int.MaxValue); Assert.That(shop.MenuCoins.text, Is.EqualTo(int.MaxValue.ToString("N0")));
            Capture(shop, 1280, 800, "large-balance");
        }

        private static void Capture(MenuSkinShop shop, int width, int height, string suffix)
        {
            Camera camera = Camera.main; Canvas canvas = shop.GetComponentInChildren<Canvas>(true);
            var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                camera.targetTexture = target; Canvas.ForceUpdateCanvases(); shop.GetComponent<MenuEconomyLayout>().Refresh(); Canvas.ForceUpdateCanvases();
                for (int i = 0; i < 3; i++) camera.Render();
                var corners = new Vector3[4]; shop.MenuCoins.transform.parent.GetComponent<RectTransform>().GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 screen = camera.WorldToScreenPoint(corner);
                    Assert.That(screen.x, Is.InRange(0, width)); Assert.That(screen.y, Is.InRange(0, height));
                }
                Assert.That(shop.MenuCoins.cachedTextGenerator.characterCountVisible, Is.EqualTo(shop.MenuCoins.text.Length), "The full balance must fit, not clip.");
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_WALLET_PREVIEW");
                if (!string.IsNullOrEmpty(prefix)) File.WriteAllBytes(prefix + "-" + suffix + ".png", image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive; ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
