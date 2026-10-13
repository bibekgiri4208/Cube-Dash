using System.Collections;
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

namespace CubeDash.Tests
{
    public sealed class MainMenuSliderFocusTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void BothSavedVolumeSlidersHaveNonBlockingFocusFramesAndKeepTheirOriginalGeometry()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                var sliders = menu.OptionsPanel.GetComponentsInChildren<Slider>(true);
                Assert.That(sliders.Length, Is.EqualTo(2));
                foreach (Slider slider in sliders)
                {
                    var feedback = slider.GetComponent<MenuSliderFocus>();
                    Assert.That(feedback, Is.Not.Null); Assert.That(feedback.FocusFrame, Is.Not.Null);
                    Assert.That(feedback.FocusFrame.enabled, Is.False);
                    Assert.That(feedback.FocusFrame.raycastTarget, Is.False);
                    Assert.That(feedback.FocusFrame.transform.GetSiblingIndex(), Is.Zero);
                    var frame = (MenuPanelGraphic)feedback.FocusFrame;
                    Assert.That(frame.BorderWidth, Is.EqualTo(3));
                    Assert.That(frame.BorderColor, Is.EqualTo(new Color(0.95f, 0.02f, 0.11f)));
                    Assert.That(slider.fillRect.parent.GetComponent<RectTransform>().sizeDelta.y, Is.EqualTo(14));
                    Assert.That(slider.handleRect.sizeDelta.x, Is.EqualTo(28));
                    Assert.That(slider.transform.localScale, Is.EqualTo(Vector3.one));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ReapplyingFeedbackPreservesControlsValuesGeometryAndAuthoredFrameStyles()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                var sliders = menu.OptionsPanel.GetComponentsInChildren<Slider>(true);
                int count = menu.GetComponentsInChildren<Transform>(true).Length;
                var frame = (MenuPanelGraphic)sliders[0].GetComponent<MenuSliderFocus>().FocusFrame;
                frame.BorderColor = Color.blue;
                var fill = sliders[0].fillRect; var handle = sliders[0].handleRect;
                sliders[0].SetValueWithoutNotify(0.42f);
                MainMenuSliderFocus.ApplyToScene(scene); MainMenuSliderFocus.ApplyToScene(scene);
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(frame.BorderColor, Is.EqualTo(Color.blue));
                Assert.That(sliders[0].fillRect, Is.SameAs(fill)); Assert.That(sliders[0].handleRect, Is.SameAs(handle));
                Assert.That(sliders[0].value, Is.EqualTo(0.42f));
                Assert.That(sliders.All(slider => slider.GetComponents<MenuSliderFocus>().Length == 1), Is.True);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator GamepadHighlightsOnlyTheSelectedSliderAndAdjustsThatVolumeWithoutMovingTheLayout()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            bool hadMaster = PlayerPrefs.HasKey(MainMenuController.VolumePreference), hadMusic = PlayerPrefs.HasKey(MainMenuController.MusicVolumePreference);
            float masterValue = PlayerPrefs.GetFloat(MainMenuController.VolumePreference, 1), musicValue = PlayerPrefs.GetFloat(MainMenuController.MusicVolumePreference, 1);
            float listenerVolume = AudioListener.volume;
            var background = InputSystem.settings.backgroundBehavior;
            var inputFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var pad = InputSystem.AddDevice<Gamepad>("MenuSliderFocusGamepad");
            try
            {
                menu.SetVolume(0.5f); menu.SetMusicVolume(0.5f); menu.OpenOptions(); yield return null;
                var sliders = menu.OptionsPanel.GetComponentsInChildren<Slider>();
                Slider master = sliders.Single(slider => slider.name == "Volume slider"), music = sliders.Single(slider => slider.name == "Music volume slider");
                var masterFeedback = master.GetComponent<MenuSliderFocus>(); var musicFeedback = music.GetComponent<MenuSliderFocus>();
                Vector3 position = master.transform.localPosition, scale = master.transform.localScale;
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadDown)); yield return null; yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(master.gameObject));
                Assert.That(masterFeedback.FocusFrame.enabled, Is.True); Assert.That(musicFeedback.FocusFrame.enabled, Is.False);
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                Capture(menu, "master-focus");
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadLeft)); yield return null; yield return null;
                Assert.That(master.value, Is.LessThan(0.5f)); Assert.That(music.value, Is.EqualTo(0.5f));
                Assert.That(masterFeedback.FocusFrame.enabled, Is.True);
                Assert.That(master.transform.localPosition, Is.EqualTo(position)); Assert.That(master.transform.localScale, Is.EqualTo(scale));
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadDown)); yield return null; yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(music.gameObject));
                Assert.That(masterFeedback.FocusFrame.enabled, Is.False); Assert.That(musicFeedback.FocusFrame.enabled, Is.True);
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                Capture(menu, "music-focus");
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.DpadRight)); yield return null; yield return null;
                Assert.That(music.value, Is.GreaterThan(0.5f));
                Assert.That(AudioListener.volume, Is.EqualTo(master.value));
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                music.interactable = false; yield return null;
                Assert.That(musicFeedback.FocusFrame.enabled, Is.False);
                music.interactable = true; yield return null;
                Assert.That(musicFeedback.FocusFrame.enabled, Is.False, "Unity clears selection when a control becomes non-interactable.");
                EventSystem.current.SetSelectedGameObject(music.gameObject);
                Assert.That(musicFeedback.FocusFrame.enabled, Is.True);
                music.enabled = false; yield return null;
                Assert.That(musicFeedback.FocusFrame.enabled, Is.False);
                music.enabled = true; yield return null;
                EventSystem.current.SetSelectedGameObject(music.gameObject);
                Assert.That(musicFeedback.FocusFrame.enabled, Is.True);
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(master.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                Assert.That(masterFeedback.FocusFrame.enabled, Is.True);
                Assert.That(musicFeedback.FocusFrame.enabled, Is.False);
                menu.ClosePanels();
                Assert.That(masterFeedback.FocusFrame.enabled || musicFeedback.FocusFrame.enabled, Is.False);
                menu.OpenOptions(); yield return null;
                Assert.That(masterFeedback.FocusFrame.enabled || musicFeedback.FocusFrame.enabled, Is.False);
            }
            finally
            {
                menu.SetVolume(masterValue); menu.SetMusicVolume(musicValue); AudioListener.volume = listenerVolume;
                if (!hadMaster) PlayerPrefs.DeleteKey(MainMenuController.VolumePreference);
                if (!hadMusic) PlayerPrefs.DeleteKey(MainMenuController.MusicVolumePreference);
                menu.ClosePanels(); InputSystem.RemoveDevice(pad);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = inputFocus;
            }
        }

        private static void Capture(MainMenuController menu, string suffix)
        {
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CUBE_DASH_MENU_SECTIONS_PLAY_PREVIEW"))) return;
            typeof(MainMenuSectionTests).GetMethod("CaptureIfRequested", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { menu, suffix });
        }
    }
}
