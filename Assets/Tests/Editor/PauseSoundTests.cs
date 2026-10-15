using System.Collections;
using System.Linq;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class PauseSoundTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void EveryMapHasSavedSoundControlsSharedMusicAndSeparateSfxRouting()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MainMenuMusic.ClipPath);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(PauseSoundAuthoring.MixerPath);
            Assert.That(mixer, Is.Not.Null);
            foreach (string map in PauseSoundAuthoring.Maps)
            {
                var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + map + ".unity");
                try
                {
                    var roots = scene.GetRootGameObjects();
                    var sound = roots.SelectMany(root => root.GetComponentsInChildren<PauseSoundMenu>(true)).Single();
                    var music = roots.SelectMany(root => root.GetComponentsInChildren<MapMusic>(true)).Single();
                    Assert.That(sound.Panel.activeSelf, Is.False, map);
                    Assert.That(sound.SoundButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("SOUND"));
                    Assert.That(sound.SoundButton.onClick.GetPersistentTarget(0), Is.SameAs(sound));
                    Assert.That(sound.MusicSlider.onValueChanged.GetPersistentMethodName(0), Is.EqualTo(nameof(PauseSoundMenu.SetMusicVolume)));
                    Assert.That(sound.SfxSlider.onValueChanged.GetPersistentMethodName(0), Is.EqualTo(nameof(PauseSoundMenu.SetSfxVolume)));
                    Assert.That(sound.MusicSlider.navigation.selectOnDown, Is.SameAs(sound.SfxSlider));
                    Assert.That(sound.MusicSlider.GetComponent<MenuSliderFocus>().FocusFrame.raycastTarget, Is.False);
                    foreach (Slider slider in new[] { sound.MusicSlider, sound.SfxSlider })
                    {
                        var rail = slider.transform.Find("Track").GetComponent<RectTransform>();
                        Assert.That(rail.anchorMin.x, Is.Zero); Assert.That(rail.anchorMax.x, Is.EqualTo(1));
                        Assert.That(rail.sizeDelta.x, Is.Zero, "Slider rails must resize with the panel, not overflow it.");
                    }
                    Assert.That(music.Source.clip, Is.SameAs(clip), map);
                    Assert.That(music.Source.loop, Is.True); Assert.That(music.Source.playOnAwake, Is.False);
                    foreach (AudioSource source in roots.SelectMany(root => root.GetComponentsInChildren<AudioSource>(true)))
                    {
                        Assert.That(source.outputAudioMixerGroup, Is.Not.Null, map + ": " + source.name);
                        Assert.That(source.outputAudioMixerGroup.audioMixer, Is.SameAs(mixer));
                        Assert.That(source.outputAudioMixerGroup.name, Is.EqualTo(source == music.Source ? "Music" : "SFX"));
                    }
                    Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<InputSystemUIInputModule>(true)).Single().deselectOnBackgroundClick, Is.False);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }

        [UnityTest]
        public IEnumerator SoundPanelKeepsPauseSupportsControllerAndPersistsIndependentLevelsBetweenMaps()
        {
            yield return new EnterPlayMode();
            bool hadMusic = PlayerPrefs.HasKey(MainMenuController.MusicVolumePreference), hadSfx = PlayerPrefs.HasKey(GameAudioSettings.SfxPreference);
            float oldMusic = GameAudioSettings.MusicVolume, oldSfx = GameAudioSettings.SfxVolume;
            float oldListener = AudioListener.volume;
            var background = InputSystem.settings.backgroundBehavior;
            var focus = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var pad = InputSystem.AddDevice<Gamepad>("PauseSoundPad");
            var keyboard = InputSystem.AddDevice<Keyboard>("PauseSoundKeyboard");
            var mouse = InputSystem.AddDevice<Mouse>("PauseSoundMouse");
            try
            {
                GameAudioSettings.SetMusicVolume(0.7f); GameAudioSettings.SetSfxVolume(0.8f);
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/City.unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var game = Object.FindAnyObjectByType<CubeDashGame>();
                var hud = Object.FindAnyObjectByType<RunnerHud>();
                var sound = Object.FindAnyObjectByType<PauseSoundMenu>();
                var music = Object.FindAnyObjectByType<MapMusic>();
                Assert.That(music.Source.isPlaying, Is.True);
                Assert.That(music.Source.volume, Is.EqualTo(0.7f).Within(0.001f));
                game.TogglePause(); // Pausing the countdown must work too.
                float countdown = game.CountdownRemaining, distance = game.Distance;
                yield return Pad(pad, GamepadButton.DpadDown);
                yield return Pad(pad, GamepadButton.DpadDown);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(sound.SoundButton.gameObject));
                yield return Pad(pad, GamepadButton.South);
                Assert.That(sound.IsOpen, Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(sound.MusicSlider.gameObject));
                yield return Pad(pad, GamepadButton.DpadLeft);
                Assert.That(GameAudioSettings.MusicVolume, Is.LessThan(0.7f));
                Assert.That(GameAudioSettings.SfxVolume, Is.EqualTo(0.8f));
                yield return Pad(pad, GamepadButton.South);
                Assert.That(sound.IsOpen, Is.True, "Confirm on a slider must never resume the run.");
                yield return Pad(pad, GamepadButton.DpadDown);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(sound.SfxSlider.gameObject));
                yield return Pad(pad, GamepadButton.DpadLeft);
                Assert.That(GameAudioSettings.SfxVolume, Is.LessThan(0.8f));
                sound.MusicSlider.value = 0.35f; sound.SfxSlider.value = 0;
                Assert.That(music.Source.volume, Is.EqualTo(0.35f).Within(0.001f));
                var mixer = Resources.Load<AudioMixer>("Game Audio");
                Assert.That(mixer.GetFloat(GameAudioSettings.SfxParameter, out float gain), Is.True);
                Assert.That(gain, Is.EqualTo(-80).Within(0.001f));
                Assert.That(music.Source.outputAudioMixerGroup.name, Is.EqualTo("Music"));

                Vector2 corner = new Vector2(Screen.width - 2, Screen.height - 2);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = corner }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = corner }); yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(sound.SfxSlider.gameObject));
                yield return Pad(pad, GamepadButton.East);
                Assert.That(sound.IsOpen, Is.False); Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Paused));
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(sound.SoundButton.gameObject));
                Assert.That(game.CountdownRemaining, Is.EqualTo(countdown)); Assert.That(game.Distance, Is.EqualTo(distance));
                Assert.That(music.Source.isPlaying, Is.True, "Music stays audible while adjusting paused audio.");

                sound.Open();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null; yield return null;
                Assert.That(sound.IsOpen, Is.False); Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Paused));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                sound.Open(); hud.CycleMenuSelection(); hud.CycleMenuSelection(); hud.ConfirmSelection();
                Assert.That(sound.IsOpen, Is.False, "Tab and confirm can activate Back.");

                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Beach.unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                Assert.That(Object.FindObjectsByType<MapMusic>().Length, Is.EqualTo(1), "Scene changes must not stack music loops.");
                music = Object.FindAnyObjectByType<MapMusic>();
                Assert.That(music.Source.volume, Is.EqualTo(0.35f).Within(0.001f));
                Assert.That(GameAudioSettings.SfxVolume, Is.Zero);
                sound = Object.FindAnyObjectByType<PauseSoundMenu>();
                Object.FindAnyObjectByType<CubeDashGame>().TogglePause(); sound.Open();
                Assert.That(sound.MusicSlider.value, Is.EqualTo(0.35f).Within(0.001f)); Assert.That(sound.SfxSlider.value, Is.Zero);
                sound.SetMusicVolume(0); sound.SetSfxVolume(1);
                Assert.That(music.Source.volume, Is.Zero);
                Assert.That(mixer.GetFloat(GameAudioSettings.SfxParameter, out gain), Is.True); Assert.That(gain, Is.Zero.Within(0.001f));

                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                Assert.That(Object.FindAnyObjectByType<MainMenuController>().Music.volume, Is.Zero, "Pause and main-menu Music share the saved setting.");
                Assert.That(Object.FindAnyObjectByType<MapMusic>(), Is.Null);
            }
            finally
            {
                GameAudioSettings.SetMusicVolume(oldMusic); GameAudioSettings.SetSfxVolume(oldSfx);
                if (!hadMusic) PlayerPrefs.DeleteKey(MainMenuController.MusicVolumePreference);
                if (!hadSfx) PlayerPrefs.DeleteKey(GameAudioSettings.SfxPreference);
                PlayerPrefs.Save(); AudioListener.volume = oldListener;
                InputSystem.RemoveDevice(pad); InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = focus;
            }
        }

        private static IEnumerator Pad(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button)); yield return null; yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator PauseAndSoundPanelsRenderInLandscapeAndPortrait()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("GPU rendering required.");
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/City.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>();
            var hud = Object.FindAnyObjectByType<RunnerHud>();
            var sound = Object.FindAnyObjectByType<PauseSoundMenu>();
            game.TogglePause();
            // Reuse the theme's optional GPU capture path, including its camera/canvas cleanup.
            var capture = typeof(LevelMenuThemeTests).GetMethod("Capture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            capture.Invoke(null, new object[] { hud, "pause-with-sound", 1672, 940 });
            sound.Open();
            capture.Invoke(null, new object[] { hud, "sound-landscape", 1672, 940 });
            sound.Layout(300);
            Assert.That(sound.MusicSlider.GetComponent<RectTransform>().rect.width, Is.EqualTo(200));
            Assert.That(sound.SfxSlider.GetComponent<RectTransform>().rect.width, Is.EqualTo(200));
            capture.Invoke(null, new object[] { hud, "sound-portrait", 600, 1000 });
            sound.Close();
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Paused));
        }
    }
}
