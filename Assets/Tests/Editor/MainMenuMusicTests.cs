using System.Collections;
using System.Linq;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class MainMenuMusicTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void MenuHasLoopingTwoDimensionalMusicAndAnIndependentWiredSlider()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                var data = new SerializedObject(menu);
                var master = (Slider)data.FindProperty("volume").objectReferenceValue;
                var music = (Slider)data.FindProperty("musicVolume").objectReferenceValue;
                Assert.That(menu.Music, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(menu.Music.clip), Is.EqualTo(MainMenuMusic.ClipPath));
                Assert.That(menu.Music.loop, Is.True); Assert.That(menu.Music.playOnAwake, Is.False);
                Assert.That(menu.Music.spatialBlend, Is.Zero);
                Assert.That(menu.Music.transform.IsChildOf(menu.transform), Is.True);
                Assert.That(music, Is.Not.Null); Assert.That(music, Is.Not.SameAs(master));
                Assert.That(music.minValue, Is.Zero); Assert.That(music.maxValue, Is.EqualTo(1));
                Assert.That(music.onValueChanged.GetPersistentEventCount(), Is.EqualTo(1));
                Assert.That(music.onValueChanged.GetPersistentTarget(0), Is.SameAs(menu));
                Assert.That(music.onValueChanged.GetPersistentMethodName(0), Is.EqualTo("SetMusicVolume"));
                Assert.That(master.onValueChanged.GetPersistentMethodName(0), Is.EqualTo("SetVolume"));
                Assert.That(music.navigation.selectOnUp, Is.SameAs(master));
                Assert.That(music.fillRect, Is.Not.SameAs(master.fillRect));
                Assert.That(music.handleRect, Is.Not.SameAs(master.handleRect));
                Text readout = (Text)data.FindProperty("musicVolumeReadout").objectReferenceValue;
                Assert.That(readout, Is.Not.Null);
                var audio = menu.OptionsPanel.transform.Find("Card/Audio section");
                Assert.That(audio.Find("Music volume heading").GetComponent<Text>().text, Is.EqualTo("MUSIC VOLUME"));
                Assert.That(audio.Find("Volume heading").GetComponent<Text>().text, Is.EqualTo("MASTER VOLUME"));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ReapplyingMusicUpgradePreservesAuthoredEditsAndDoesNotDuplicateControlsOrSources()
        {
            var scene = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                AudioSource music = menu.Music; music.volume = 0.27f;
                Text heading = menu.OptionsPanel.transform.Find("Card/Audio section/Music volume heading").GetComponent<Text>();
                heading.text = "MY MUSIC";
                int count = menu.GetComponentsInChildren<Transform>(true).Length;
                MainMenuMusic.ApplyToScene(scene);
                Assert.That(menu.Music, Is.SameAs(music)); Assert.That(music.volume, Is.EqualTo(0.27f));
                Assert.That(heading.text, Is.EqualTo("MY MUSIC"));
                Assert.That(menu.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(menu.GetComponentsInChildren<AudioSource>(true).Length, Is.EqualTo(1));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator MusicVolumeRestoresClampsMutesAndChangesOnlyMusicWhileTheMenuPlays()
        {
            bool hadMusic = PlayerPrefs.HasKey(MainMenuController.MusicVolumePreference);
            float savedMusic = PlayerPrefs.GetFloat(MainMenuController.MusicVolumePreference, 1);
            float oldListener = AudioListener.volume;
            try
            {
                PlayerPrefs.SetFloat(MainMenuController.MusicVolumePreference, 0.36f);
                yield return new EnterPlayMode();
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var menu = Object.FindAnyObjectByType<MainMenuController>();
                var data = new SerializedObject(menu);
                var slider = (Slider)data.FindProperty("musicVolume").objectReferenceValue;
                var readout = (Text)data.FindProperty("musicVolumeReadout").objectReferenceValue;
                Assert.That(menu.Music.volume, Is.EqualTo(0.36f).Within(0.001f));
                Assert.That(slider.value, Is.EqualTo(0.36f).Within(0.001f)); Assert.That(readout.text, Is.EqualTo("36%"));
                float deadline = Time.realtimeSinceStartup + 5;
                while (!menu.Music.isPlaying && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(menu.Music.isPlaying, Is.True);
                menu.OpenOptions(); yield return null;
                float master = AudioListener.volume;
                slider.value = 0.42f;
                Assert.That(menu.Music.volume, Is.EqualTo(0.42f).Within(0.001f)); Assert.That(readout.text, Is.EqualTo("42%"));
                Assert.That(AudioListener.volume, Is.EqualTo(master));
                Assert.That(PlayerPrefs.GetFloat(MainMenuController.MusicVolumePreference), Is.EqualTo(0.42f).Within(0.001f));
                menu.SetMusicVolume(-1);
                Assert.That(menu.Music.volume, Is.Zero); Assert.That(slider.value, Is.Zero); Assert.That(readout.text, Is.EqualTo("0%"));
                Assert.That(menu.Music.isPlaying, Is.True, "Mute should not restart the track when volume returns.");
                menu.SetMusicVolume(2);
                Assert.That(menu.Music.volume, Is.EqualTo(1)); Assert.That(slider.value, Is.EqualTo(1)); Assert.That(readout.text, Is.EqualTo("100%"));
                Assert.That(AudioListener.volume, Is.EqualTo(master));
                menu.ClosePanels(); Assert.That(menu.Music.isPlaying, Is.True);
                // A fresh menu scene reads the saved music setting again.
                menu.SetMusicVolume(0.23f);
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                menu = Object.FindAnyObjectByType<MainMenuController>();
                Assert.That(menu.Music.volume, Is.EqualTo(0.23f).Within(0.001f));
                AudioSource source = menu.Music;
                menu.StartGame(); Assert.That(source.isPlaying, Is.True, "Map selection keeps menu music playing.");
                menu.StartMap(menu.GameplayScene); Assert.That(source.isPlaying, Is.False);
                deadline = Time.realtimeSinceStartup + 30;
                while ((SceneManager.GetActiveScene().name != "Level" || SceneLoader.IsLoading) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level"));
                Assert.That(source == null, Is.True, "Menu music must not persist into gameplay.");
            }
            finally
            {
                if (hadMusic) PlayerPrefs.SetFloat(MainMenuController.MusicVolumePreference, savedMusic);
                else PlayerPrefs.DeleteKey(MainMenuController.MusicVolumePreference);
                AudioListener.volume = oldListener;
            }
        }
    }
}
