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
    public sealed class MainMenuQuitConfirmationTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void SavedExitDialogMatchesTheRibbonThemeAndOnlyYesCanConfirm()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = FindMenu(scene); var panel = menu.QuitPanel; Assert.That(panel, Is.Not.Null);
                Assert.That(panel.activeSelf, Is.False); Assert.That(panel.name, Is.EqualTo(MainMenuQuitConfirmation.PanelName));
                Assert.That(panel.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(panel.GetComponent<CanvasGroup>(), Is.Not.Null);
                Assert.That(panel.GetComponent<MenuPanelMotion>(), Is.Not.Null);
                Assert.That(panel.GetComponent<MenuDialogLayout>(), Is.Not.Null);
                var card = panel.transform.Find("Card"); var face = card.GetComponent<MenuPanelGraphic>();
                Assert.That(face.color, Is.EqualTo(LevelMenuTheme.Paper)); Assert.That(face.CornerCut, Is.EqualTo(22));
                Assert.That(face.BorderWidth, Is.EqualTo(4));
                Assert.That(card.Find("Ribbon header").GetComponent<MenuRibbonGraphic>().raycastTarget, Is.False);
                Assert.That(card.Find("Heading").GetComponent<Text>().text, Is.EqualTo("Confirm exit?"));
                Assert.That(panel.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(2));
                var yes = card.Find("YES").GetComponent<Button>(); var no = card.Find("NO").GetComponent<Button>();
                Assert.That(yes.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(MainMenuController.ConfirmQuit)));
                Assert.That(no.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(MainMenuController.ClosePanels)));
                foreach (Button button in new[] { yes, no })
                {
                    Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(menu));
                    Assert.That(button.targetGraphic, Is.SameAs(button.GetComponent<MenuRibbonGraphic>()));
                    Assert.That(button.GetComponent<ArcadeButton>().LivelyFeedback, Is.True);
                    Assert.That(button.GetComponentInChildren<MenuLabelText>(true).font.name, Is.EqualTo("Anton-Regular"));
                    Assert.That(button.navigation.selectOnLeft, Is.SameAs(button == yes ? no : yes));
                    Assert.That(button.navigation.selectOnRight, Is.SameAs(button == yes ? no : yes));
                }
                Assert.That(new SerializedObject(menu).FindProperty("quitCancel").objectReferenceValue, Is.SameAs(no));
                var quit = QuitButton(menu);
                Assert.That(quit.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(MainMenuController.Quit)));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ReapplyingPreservesExistingDialogAndManualEdits()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = FindMenu(scene); GameObject panel = menu.QuitPanel;
                var heading = panel.transform.Find("Card/Heading").GetComponent<Text>(); heading.text = "MY EXIT PROMPT";
                int count = menu.GetComponentsInChildren<Transform>(true).Length;
                MainMenuQuitConfirmation.ApplyToScene(scene);
                Assert.That(menu.QuitPanel, Is.SameAs(panel)); Assert.That(heading.text, Is.EqualTo("MY EXIT PROMPT"));
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator QuitOpensTheDialogAndNoCancelsWithoutStoppingMusicOrAllocatingObjects()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); var quit = QuitButton(menu);
            int count = menu.GetComponentsInChildren<Transform>(true).Length;
            menu.ConfirmQuit(); Assert.That(menu.Loading, Is.False, "Stale confirmation outside the dialog must not quit.");
            for (int attempt = 0; attempt < 3; attempt++)
            {
                ExecuteEvents.Execute(quit.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                Assert.That(menu.CurrentPanel, Is.SameAs(menu.QuitPanel)); Assert.That(menu.Loading, Is.False);
                Assert.That(menu.Music.isPlaying, Is.True);
                var no = menu.QuitPanel.transform.Find("Card/NO").GetComponent<Button>();
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(no.gameObject), "Default to the safe action.");
                yield return null; // Newly enabled graphics receive canvas depth on the next UI update.
                Settle(menu.QuitPanel); Canvas.ForceUpdateCanvases();
                AssertPointerHits(menu, no);
                AssertPointerHits(menu, menu.QuitPanel.transform.Find("Card/YES").GetComponent<Button>());
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(no.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(menu.CurrentPanel, Is.Null);
                Assert.That(menu.QuitPanel.GetComponent<CanvasGroup>().interactable, Is.False);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(quit.gameObject));
                menu.ConfirmQuit(); Assert.That(menu.Loading, Is.False, "A fading-out dialog cannot confirm.");
                yield return MainMenuSectionTests.WaitForPanelClosed(menu.QuitPanel);
                Assert.That(menu.Music.isPlaying, Is.True);
            }
            Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        }

        [UnityTest]
        public IEnumerator EscapeAndControllerBackCancelAndFocusReturnsToQuit()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            var background = InputSystem.settings.backgroundBehavior; var focus = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>("QuitConfirmationKeyboard"); var pad = InputSystem.AddDevice<Gamepad>("QuitConfirmationPad");
            try
            {
                menu.Quit(); keyboard.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null; yield return null;
                yield return MainMenuSectionTests.WaitForPanelClosed(menu.QuitPanel);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(QuitButton(menu).gameObject));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                menu.Quit(); pad.MakeCurrent();
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadLeft)); yield return null; yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("YES"));
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
                yield return MainMenuSectionTests.WaitForPanelClosed(menu.QuitPanel);
                Assert.That(menu.CurrentPanel, Is.Null); Assert.That(menu.Loading, Is.False);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(QuitButton(menu).gameObject));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(pad);
                InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = focus;
            }
        }

        [UnityTest]
        public IEnumerator DialogSwitchingDisablesTheOldPanelAndRendersInLandscapeAndPortrait()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>(); menu.OpenShop(); menu.Quit();
            Assert.That(menu.ShopPanel.GetComponent<CanvasGroup>().interactable, Is.False);
            Settle(menu.QuitPanel);
            Capture(menu, 1672, 940, "landscape"); Capture(menu, 600, 1000, "portrait");
            menu.OpenOptions(); Assert.That(menu.QuitPanel.GetComponent<CanvasGroup>().interactable, Is.False);
            menu.ConfirmQuit(); Assert.That(menu.Loading, Is.False);
            yield return MainMenuSectionTests.WaitForPanelClosed(menu.QuitPanel);
            menu.ClosePanels(); yield return MainMenuSectionTests.WaitForPanelClosed(menu.OptionsPanel);
        }

        private static MainMenuController FindMenu(Scene scene) => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
        private static Button QuitButton(MainMenuController menu) => menu.GetComponentInChildren<Canvas>(true).transform.Find("Left menu ribbons/QUIT").GetComponent<Button>();
        private static void Settle(GameObject panel) => typeof(MenuPanelMotion).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(panel.GetComponent<MenuPanelMotion>(), new object[] { 1f });

        private static void AssertPointerHits(MainMenuController menu, Button button)
        {
            var point = button.GetComponent<RectTransform>(); var hits = new List<RaycastResult>();
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, point.TransformPoint(point.rect.center)) };
            menu.GetComponentInChildren<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject), Is.SameAs(button.gameObject));
        }

        private static void Capture(MainMenuController menu, int width, int height, string suffix)
        {
            var camera = Camera.main; var canvas = menu.GetComponentInChildren<Canvas>();
            var previous = camera.targetTexture; var active = RenderTexture.active; var mode = canvas.renderMode; var oldCamera = canvas.worldCamera;
            float plane = canvas.planeDistance; bool async = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                ShaderUtil.allowAsyncCompilation = false; Canvas.ForceUpdateCanvases(); menu.QuitPanel.GetComponent<MenuDialogLayout>().Refresh(); Canvas.ForceUpdateCanvases();
                var panel = menu.QuitPanel.GetComponent<RectTransform>(); var card = menu.QuitPanel.transform.Find("Card").GetComponent<RectTransform>();
                var corners = new Vector3[4]; card.GetWorldCorners(corners);
                Assert.That(corners.All(corner => panel.rect.Contains(panel.InverseTransformPoint(corner))), Is.True);
                foreach (var text in menu.QuitPanel.GetComponentsInChildren<MenuLabelText>())
                    using (var mesh = new VertexHelper())
                    {
                        typeof(MenuLabelText).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null).Invoke(text, new object[] { mesh });
                        Assert.That(mesh.currentVertCount, Is.GreaterThan(0), text.text);
                    }
                for (int frame = 0; frame < 4; frame++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_QUIT_PREVIEW");
                if (!string.IsNullOrEmpty(prefix)) File.WriteAllBytes(prefix + "-" + suffix + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active; canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane;
                ShaderUtil.allowAsyncCompilation = async; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
