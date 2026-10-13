using System.Collections;
using System.Linq;
using System.Reflection;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class MainMenuToggleFocusTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void SavedFullscreenFocusIsSeparateFromTheTickAndPreservesItsCallbackAndGeometry()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = FindMenu(scene);
                Toggle toggle = menu.OptionsPanel.GetComponentInChildren<Toggle>(true);
                var feedback = toggle.GetComponent<MenuToggleFocus>();
                Assert.That(feedback, Is.Not.Null);
                var frame = (MenuPanelGraphic)feedback.FocusFrame;
                Assert.That(frame.enabled, Is.False); Assert.That(frame.raycastTarget, Is.False);
                Assert.That(frame.transform.GetSiblingIndex(), Is.Zero);
                Assert.That(frame.BorderWidth, Is.EqualTo(3));
                Assert.That(frame.BorderColor, Is.EqualTo(new Color(0.95f, 0.02f, 0.11f)));
                Assert.That(frame, Is.Not.SameAs(toggle.graphic));
                Assert.That(toggle.targetGraphic.rectTransform.sizeDelta, Is.EqualTo(new Vector2(56, 56)));
                Assert.That(frame.rectTransform.sizeDelta, Is.EqualTo(new Vector2(72, 72)));
                Assert.That(frame.rectTransform.anchoredPosition, Is.EqualTo(toggle.targetGraphic.rectTransform.anchoredPosition));
                Assert.That(toggle.graphic.GetComponent<MenuGlyphGraphic>().Symbol, Is.EqualTo(MenuGlyphGraphic.Glyph.Check));
                Assert.That(toggle.onValueChanged.GetPersistentEventCount(), Is.EqualTo(1));
                Assert.That(toggle.onValueChanged.GetPersistentTarget(0), Is.SameAs(menu));
                Assert.That(toggle.onValueChanged.GetPersistentMethodName(0), Is.EqualTo(nameof(MainMenuController.SetFullscreen)));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ReapplyingOptionsFocusKeepsFullscreenStateGraphicsNavigationAndManualFrameStyles()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = FindMenu(scene); var toggle = menu.OptionsPanel.GetComponentInChildren<Toggle>(true);
                var frame = (MenuPanelGraphic)toggle.GetComponent<MenuToggleFocus>().FocusFrame;
                frame.BorderColor = Color.blue; toggle.SetIsOnWithoutNotify(true);
                var tick = toggle.graphic; var box = toggle.targetGraphic; var nav = toggle.navigation;
                int count = menu.GetComponentsInChildren<Transform>(true).Length;
                MainMenuSliderFocus.ApplyToScene(scene); MainMenuSliderFocus.ApplyToScene(scene);
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(toggle.GetComponents<MenuToggleFocus>().Length, Is.EqualTo(1));
                Assert.That(toggle.isOn, Is.True); Assert.That(toggle.graphic, Is.SameAs(tick));
                Assert.That(toggle.targetGraphic, Is.SameAs(box)); Assert.That(toggle.navigation, Is.EqualTo(nav));
                Assert.That(frame.BorderColor, Is.EqualTo(Color.blue));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator GamepadHighlightsFullscreenWhetherCheckedOrUncheckedAndClearsOnBack()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            var toggle = menu.OptionsPanel.GetComponentInChildren<Toggle>(true);
            var feedback = toggle.GetComponent<MenuToggleFocus>();
            var callbacks = toggle.onValueChanged; var transition = toggle.toggleTransition; bool originalValue = toggle.isOn;
            var background = InputSystem.settings.backgroundBehavior;
            var inputFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var pad = InputSystem.AddDevice<Gamepad>("FullscreenFocusGamepad");
            try
            {
                // Verify submit/tick behavior without changing the test machine's display mode.
                toggle.onValueChanged = new Toggle.ToggleEvent(); toggle.toggleTransition = Toggle.ToggleTransition.None;
                int changes = 0; toggle.onValueChanged.AddListener(value => changes++);
                toggle.SetIsOnWithoutNotify(false); menu.OpenOptions(); yield return null;
                for (int i = 0; i < 3; i++)
                {
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadDown)); yield return null; yield return null;
                    InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                }
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(toggle.gameObject));
                Assert.That(feedback.FocusFrame.enabled, Is.True);
                Assert.That(menu.OptionsPanel.GetComponentsInChildren<MenuSliderFocus>().All(item => !item.Focused), Is.True);
                Assert.That(toggle.graphic.canvasRenderer.GetAlpha(), Is.Zero);
                Capture(menu, "fullscreen-off-focus");
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South)); yield return null; yield return null;
                Assert.That(toggle.isOn, Is.True); Assert.That(changes, Is.EqualTo(1));
                Assert.That(toggle.graphic.canvasRenderer.GetAlpha(), Is.EqualTo(1));
                Assert.That(feedback.FocusFrame.enabled, Is.True);
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                Capture(menu, "fullscreen-on-focus");
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South)); yield return null; yield return null;
                Assert.That(toggle.isOn, Is.False); Assert.That(changes, Is.EqualTo(2));
                Assert.That(feedback.FocusFrame.enabled, Is.True);
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadDown)); yield return null; yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("BACK"));
                Assert.That(feedback.FocusFrame.enabled, Is.False);
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(toggle.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                Assert.That(feedback.FocusFrame.enabled, Is.True);
                toggle.interactable = false; yield return null;
                Assert.That(feedback.FocusFrame.enabled, Is.False);
                toggle.interactable = true;
                EventSystem.current.SetSelectedGameObject(toggle.gameObject);
                Assert.That(feedback.FocusFrame.enabled, Is.True);
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
                Assert.That(menu.OptionsPanel.activeSelf, Is.False); Assert.That(feedback.FocusFrame.enabled, Is.False);
                menu.OpenOptions(); yield return null;
                Assert.That(feedback.FocusFrame.enabled, Is.False);
            }
            finally
            {
                toggle.SetIsOnWithoutNotify(originalValue); toggle.onValueChanged = callbacks; toggle.toggleTransition = transition;
                menu.ClosePanels(); InputSystem.RemoveDevice(pad);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = inputFocus;
            }
        }

        private static MainMenuController FindMenu(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();

        private static void Capture(MainMenuController menu, string suffix)
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CUBE_DASH_MENU_SECTIONS_PLAY_PREVIEW"))) return;
            typeof(MainMenuSectionTests).GetMethod("CaptureIfRequested", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { menu, suffix });
        }
    }
}
