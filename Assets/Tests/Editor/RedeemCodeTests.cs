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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class RedeemCodeTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("gift")]
        [TestCase("jestergift123")]
        public void InvalidCodesCannotChangeTheWallet(string code)
        {
            using (new EconomyTestScope())
            {
                GameEconomy.AddCoins(17);
                Assert.That(GameEconomy.TryRedeemCode(code), Is.False);
                Assert.That(GameEconomy.Coins, Is.EqualTo(17));
            }
        }

        [Test]
        public void GiftIsRepeatableSavedCaseInsensitiveAndOverflowSafe()
        {
            using (new EconomyTestScope())
            {
                foreach (string code in new[] { "jestergift", " JESTERGIFT ", "JesterGift", "jestergift" })
                    Assert.That(GameEconomy.TryRedeemCode(code), Is.True);
                Assert.That(GameEconomy.Coins, Is.EqualTo(20000));
                Assert.That(PlayerPrefs.GetInt(GameEconomy.CoinsKey), Is.EqualTo(20000));
                GameEconomy.AddCoins(int.MaxValue - 20001);
                Assert.That(GameEconomy.TryRedeemCode("jestergift"), Is.True);
                Assert.That(GameEconomy.Coins, Is.EqualTo(int.MaxValue));
            }
        }

        [Test]
        public void SavedOptionsButtonDialogNavigationAndAttributionAreWired()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = FindMenu(scene); var dialog = menu.RedeemPanel;
                Assert.That(dialog, Is.Not.Null); Assert.That(dialog.gameObject.activeSelf, Is.False);
                var opener = Opener(menu);
                Assert.That(opener.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(MainMenuController.OpenRedeem)));
                Assert.That(opener.onClick.GetPersistentTarget(0), Is.SameAs(menu));
                Assert.That(opener.navigation.selectOnUp, Is.TypeOf<Toggle>());
                Assert.That(opener.navigation.selectOnUp.navigation.selectOnDown, Is.SameAs(opener));
                Assert.That(opener.navigation.selectOnDown.name, Is.EqualTo("BACK"));
                Assert.That(dialog.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(dialog.GetComponent<MenuDialogLayout>(), Is.Not.Null);
                Assert.That(dialog.GetComponent<MenuPanelMotion>(), Is.Not.Null);
                Assert.That(dialog.CodeInput.characterLimit, Is.EqualTo(64));
                Assert.That(dialog.CodeInput.onSubmit.GetPersistentMethodName(0), Is.EqualTo(nameof(MenuRedeemCode.SubmitCode)));
                var message = dialog.GetComponentsInChildren<Text>(true).Single(text => text.name == "Gift attribution");
                Assert.That(message.text, Is.EqualTo("Gift from Bibek Giri Productions"));
                Assert.That(dialog.Gift.Artwork, Is.Not.Null);
                Assert.That(dialog.Gift.Artwork.raycastTarget, Is.False);
                Assert.That(dialog.GetComponentsInChildren<AudioSource>(true), Is.Empty, "Reveal must be silent.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ReapplyingPreservesManualEditsAndDoesNotDuplicateControls()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = FindMenu(scene); var dialog = menu.RedeemPanel;
                var heading = dialog.transform.Find("Card/Heading").GetComponent<Text>(); heading.text = "MY GIFT";
                int count = menu.GetComponentsInChildren<Transform>(true).Length;
                MainMenuRedeemAuthoring.ApplyToScene(scene);
                Assert.That(menu.RedeemPanel, Is.SameAs(dialog)); Assert.That(heading.text, Is.EqualTo("MY GIFT"));
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void IllustratedGiftHasValidDistinctMeshesThroughoutTheReveal()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var gift = FindMenu(scene).RedeemPanel.Gift;
                Assert.That(gift.Artwork, Is.Not.Null);
                var rect = gift.Artwork.rectTransform; rect.sizeDelta = new Vector2(560, 300);
                int previous = 0;
                foreach (float time in new[] { 0f, 0.4f, 0.95f, 1.3f, MenuGiftReveal.Duration })
                {
                    gift.Artwork.Frame = time;
                    using (var mesh = new VertexHelper())
                    {
                        typeof(MenuGiftGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null,
                            new[] { typeof(VertexHelper) }, null)
                            .Invoke(gift.Artwork, new object[] { mesh });
                        Assert.That(mesh.currentVertCount, Is.GreaterThan(1000));
                        Assert.That(mesh.currentVertCount, Is.LessThan(60000));
                        var vertex = new UIVertex();
                        for (int i = 0; i < mesh.currentVertCount; i++)
                        {
                            mesh.PopulateUIVertex(ref vertex, i);
                            Assert.That(float.IsNaN(vertex.position.x) || float.IsNaN(vertex.position.y), Is.False);
                        }
                        if (time == 0.95f) Assert.That(mesh.currentVertCount, Is.GreaterThan(previous), "Burst adds coins and confetti.");
                        previous = mesh.currentVertCount;
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void RenderGiftKeyframesWithoutEnteringPlayModeOrStartingMusic()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = FindMenu(scene); var dialog = menu.RedeemPanel;
                menu.OpenOptions(); menu.OpenRedeem();
                typeof(MenuPanelMotion).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(dialog.GetComponent<MenuPanelMotion>(), null);
                Settle(dialog.gameObject);
                dialog.transform.Find("Card/Code form").gameObject.SetActive(false);
                dialog.Gift.gameObject.SetActive(true);
                var again = dialog.transform.Find("Card/REDEEM AGAIN").GetComponent<Button>(); again.gameObject.SetActive(true);
                foreach (var sample in new[] { (0.4f, "refined-closed"), (0.95f, "refined-burst"), (MenuGiftReveal.Duration, "refined-open") })
                {
                    again.interactable = sample.Item1 >= MenuGiftReveal.Duration;
                    SampleGift(dialog.Gift, sample.Item1);
                    Capture(menu, dialog.gameObject, 1672, 940, sample.Item2);
                }
                Capture(menu, dialog.gameObject, 600, 1000, "refined-portrait");
                Assert.That(menu.Music.isPlaying, Is.False, "Edit-mode preview must never start music.");
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        [UnityTest]
        public IEnumerator RedeemBanksImmediatelyAnimatesAtZeroTimeScaleAndCanRepeatWithoutAllocations()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            using (new EconomyTestScope())
            {
                var menu = Object.FindAnyObjectByType<MainMenuController>(); var dialog = menu.RedeemPanel;
                dialog.Redeem(); Assert.That(GameEconomy.Coins, Is.Zero, "Hidden stale callback cannot grant currency.");
                menu.OpenOptions(); Opener(menu).onClick.Invoke(); yield return null;
                Settle(dialog.gameObject); Canvas.ForceUpdateCanvases();
                // Unity's InputField creates its reusable caret on first activation; the gift itself never spawns objects.
                int objects = menu.GetComponentsInChildren<Transform>(true).Length;
                Assert.That(menu.CurrentPanel, Is.SameAs(dialog.gameObject));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(dialog.CodeInput.gameObject));
                dialog.Redeem(); Assert.That(GameEconomy.Coins, Is.Zero);
                Assert.That(dialog.GetComponentsInChildren<Text>().Single(text => text.name == "Status").text, Does.Contain("Enter"));
                dialog.CodeInput.text = "wrong"; dialog.Redeem(); Assert.That(GameEconomy.Coins, Is.Zero);
                Assert.That(dialog.GetComponentsInChildren<Text>().Single(text => text.name == "Status").text, Does.Contain("Invalid"));
                Time.timeScale = 0;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    dialog.CodeInput.text = "jestergift";
                    // Input-field Enter/Done and pointer Redeem share the same guarded callback.
                    dialog.CodeInput.onSubmit.Invoke(dialog.CodeInput.text);
                    Assert.That(GameEconomy.Coins, Is.EqualTo(attempt * 5000));
                    Assert.That(menu.GetComponent<MenuSkinShop>().MenuCoins.text, Is.EqualTo((attempt * 5000).ToString("N0")));
                    Assert.That(dialog.Gift.Playing, Is.True);
                    dialog.Redeem(); dialog.RedeemAgain();
                    Assert.That(GameEconomy.Coins, Is.EqualTo(attempt * 5000), "Double submit during reveal is not another redemption.");
                    Assert.That(dialog.ShowingGift, Is.True);
                    float deadline = Time.realtimeSinceStartup + 12;
                    while (dialog.Gift.Playing && Time.realtimeSinceStartup < deadline) yield return null;
                    yield return null;
                    Assert.That(dialog.Gift.Playing, Is.False);
                    Assert.That(dialog.Gift.Amount.text, Is.EqualTo("+5,000 COINS"));
                    var again = dialog.transform.Find("Card/REDEEM AGAIN").GetComponent<Button>();
                    Assert.That(again.interactable, Is.True);
                    Assert.That(again.navigation.selectOnRight.navigation.selectOnLeft, Is.SameAs(again));
                    Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(again.gameObject));
                    again.onClick.Invoke();
                    Assert.That(dialog.ShowingGift, Is.False); Assert.That(dialog.CodeInput.text, Is.Empty);
                }
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
                menu.ClosePanels(); dialog.CodeInput.text = "jestergift"; dialog.Redeem();
                Assert.That(GameEconomy.Coins, Is.EqualTo(15000));
                Assert.That(menu.CurrentPanel, Is.SameAs(menu.OptionsPanel));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(Opener(menu).gameObject));
                menu.ClosePanels();
                Assert.That(menu.CurrentPanel, Is.Null);
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("OPTIONS"));
                Time.timeScale = 1;
            }
        }

        [UnityTest]
        public IEnumerator ClosingMidRevealKeepsAwardAndSwitchingPanelsRejectsStaleCallbacks()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            using (new EconomyTestScope())
            {
                var menu = Object.FindAnyObjectByType<MainMenuController>(); var dialog = menu.RedeemPanel;
                menu.OpenRedeem(); Assert.That(menu.CurrentPanel, Is.Null, "Only Options opens its redeem dialog.");
                menu.OpenOptions(); menu.OpenRedeem(); dialog.CodeInput.text = "jestergift"; dialog.Redeem();
                menu.ClosePanels();
                Assert.That(GameEconomy.Coins, Is.EqualTo(5000));
                yield return MainMenuSectionTests.WaitForPanelClosed(dialog.gameObject);
                menu.OpenRedeem(); Assert.That(dialog.ShowingGift, Is.False);
                dialog.CodeInput.text = "jestergift"; menu.OpenShop(); dialog.Redeem();
                Assert.That(GameEconomy.Coins, Is.EqualTo(5000));
                Assert.That(menu.CurrentPanel, Is.SameAs(menu.ShopPanel));
                yield return MainMenuSectionTests.WaitForPanelClosed(dialog.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator EscapeAndGamepadBackReturnToOptionsThenMainMenu()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            var background = InputSystem.settings.backgroundBehavior; var focus = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>("RedeemKeyboard"); var pad = InputSystem.AddDevice<Gamepad>("RedeemPad");
            try
            {
                menu.OpenOptions(); menu.OpenRedeem(); keyboard.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null; yield return null;
                Assert.That(menu.CurrentPanel, Is.SameAs(menu.OptionsPanel));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(Opener(menu).gameObject));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                menu.OpenRedeem(); pad.MakeCurrent();
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
                Assert.That(menu.CurrentPanel, Is.SameAs(menu.OptionsPanel));
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
                Assert.That(menu.CurrentPanel, Is.Null); Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("OPTIONS"));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(pad);
                InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = focus;
            }
        }

        [UnityTest]
        public IEnumerator PointerRoutingAndGiftRenderFitLandscapeAndPortrait()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            using (new EconomyTestScope())
            {
                var menu = Object.FindAnyObjectByType<MainMenuController>(); var dialog = menu.RedeemPanel;
                menu.OpenOptions(); yield return null; Settle(menu.OptionsPanel);
                Capture(menu, menu.OptionsPanel, 1672, 940, "options");
                AssertPointerHits(menu, Opener(menu).gameObject);
                ExecuteEvents.Execute(Opener(menu).gameObject, new PointerEventData(EventSystem.current)
                    { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                yield return null; Settle(dialog.gameObject);
                Capture(menu, dialog.gameObject, 1672, 940, "code-landscape");
                AssertPointerHits(menu, dialog.CodeInput.gameObject);
                var redeem = dialog.transform.Find("Card/Code form/REDEEM").GetComponent<Button>();
                AssertPointerHits(menu, redeem.gameObject);
                Capture(menu, dialog.gameObject, 600, 1000, "code-portrait");
                dialog.CodeInput.text = "jestergift";
                ExecuteEvents.Execute(redeem.gameObject, new PointerEventData(EventSystem.current)
                    { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                Assert.That(GameEconomy.Coins, Is.EqualTo(5000));
                SampleGift(dialog.Gift, 0.4f); Capture(menu, dialog.gameObject, 1672, 940, "gift-shake");
                SampleGift(dialog.Gift, 0.95f); Capture(menu, dialog.gameObject, 1672, 940, "gift-opening");
                SampleGift(dialog.Gift, MenuGiftReveal.Duration); Capture(menu, dialog.gameObject, 1672, 940, "gift-open");
                Capture(menu, dialog.gameObject, 600, 1000, "gift-portrait");
            }
        }

        private static void SampleGift(MenuGiftReveal gift, float time) => typeof(MenuGiftReveal)
            .GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(gift, new object[] { time });

        private static void AssertPointerHits(MainMenuController menu, GameObject target)
        {
            Canvas.ForceUpdateCanvases(); var rect = target.GetComponent<RectTransform>(); var hits = new List<RaycastResult>();
            var pointer = new PointerEventData(EventSystem.current)
                { position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            menu.GetComponentInChildren<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(target));
        }

        private static void Capture(MainMenuController menu, GameObject panel, int width, int height, string suffix)
        {
            var camera = menu.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Single(item => item.CompareTag("MainCamera"));
            var canvas = menu.GetComponentInChildren<Canvas>();
            var previous = camera.targetTexture; var active = RenderTexture.active; var mode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            float plane = canvas.planeDistance; bool async = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                ShaderUtil.allowAsyncCompilation = false; Canvas.ForceUpdateCanvases();
                if (panel.TryGetComponent(out MenuDialogLayout layout)) layout.Refresh();
                Canvas.ForceUpdateCanvases();
                if (layout != null)
                {
                    var rect = panel.GetComponent<RectTransform>(); var card = panel.transform.Find("Card").GetComponent<RectTransform>();
                    var corners = new Vector3[4]; card.GetWorldCorners(corners);
                    Assert.That(corners.All(corner => rect.rect.Contains(rect.InverseTransformPoint(corner))), Is.True);
                }
                foreach (var text in panel.GetComponentsInChildren<MenuLabelText>().Where(item => !string.IsNullOrEmpty(item.text)))
                    using (var mesh = new VertexHelper())
                    {
                        typeof(MenuLabelText).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null,
                            new[] { typeof(VertexHelper) }, null).Invoke(text, new object[] { mesh });
                        Assert.That(mesh.currentVertCount, Is.GreaterThan(0), text.text);
                    }
                for (int frame = 0; frame < 4; frame++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_REDEEM_PREVIEW");
                if (!string.IsNullOrEmpty(prefix)) File.WriteAllBytes(prefix + "-" + suffix + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active; canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane;
                ShaderUtil.allowAsyncCompilation = async; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }

        private static MainMenuController FindMenu(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
        private static Button Opener(MainMenuController menu) => menu.OptionsPanel.transform.Find("Card/REDEEM CODE").GetComponent<Button>();
        private static void Settle(GameObject panel) => typeof(MenuPanelMotion).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(panel.GetComponent<MenuPanelMotion>(), new object[] { 1f });
    }
}
