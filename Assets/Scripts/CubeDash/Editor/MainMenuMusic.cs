using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CubeDash.Editor
{
    /// <summary>Adds only the menu music source and an independent music-volume row.</summary>
    public static class MainMenuMusic
    {
        public const string ClipPath = "Assets/Audio/Music/Main Menu Bg.mp3";

        [MenuItem("Tools/Cube Dash/Add Main Menu Music and Volume Control")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before adding menu music.");
            Scene scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the menu music.");
        }

        public static void ApplyToScene(Scene scene)
        {
            var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (clip == null) throw new InvalidOperationException("Missing Main Menu Bg.mp3 in Assets/Audio/Music.");
            var serialized = new SerializedObject(menu);
            AudioSource source = serialized.FindProperty("music").objectReferenceValue as AudioSource;
            if (source == null)
            {
                var go = new GameObject("Menu Background Music"); go.transform.SetParent(menu.transform, false);
                Undo.RegisterCreatedObjectUndo(go, "Add menu background music");
                source = Undo.AddComponent<AudioSource>(go);
                source.clip = clip; source.loop = true; source.playOnAwake = false;
                source.spatialBlend = 0; source.volume = 1;
                serialized.FindProperty("music").objectReferenceValue = source;
            }

            if (serialized.FindProperty("musicVolume").objectReferenceValue == null)
            {
                Transform card = menu.OptionsPanel.transform.Find("Card");
                var audio = card.Find("Audio section").GetComponent<RectTransform>();
                var master = serialized.FindProperty("volume").objectReferenceValue as Slider;
                var heading = audio.Find("Volume heading").GetComponent<Text>();
                var badge = audio.Find("Volume value badge").GetComponent<RectTransform>();
                if (master == null || heading == null || badge == null) throw new InvalidOperationException("The existing Audio section is required.");

                var musicHeading = UnityEngine.Object.Instantiate(heading, audio);
                musicHeading.name = "Music volume heading"; musicHeading.text = "MUSIC VOLUME";
                Undo.RegisterCreatedObjectUndo(musicHeading.gameObject, "Add music volume heading");
                var musicBadge = UnityEngine.Object.Instantiate(badge, audio);
                musicBadge.name = "Music volume value badge";
                Text readout = musicBadge.GetComponentInChildren<Text>(true); readout.name = "Music volume value";
                Undo.RegisterCreatedObjectUndo(musicBadge.gameObject, "Add music volume readout");
                var slider = UnityEngine.Object.Instantiate(master, audio);
                slider.name = "Music volume slider";
                Undo.RegisterCreatedObjectUndo(slider.gameObject, "Add music volume slider");
                slider.onValueChanged = new Slider.SliderEvent();
                UnityEventTools.AddPersistentListener(slider.onValueChanged, menu.SetMusicVolume);
                slider.SetValueWithoutNotify(1); readout.text = "100%";

                Undo.RecordObject(audio, "Fit both audio volume rows");
                audio.anchoredPosition = new Vector2(0, 29); audio.sizeDelta = new Vector2(880, 240);
                Position(heading.rectTransform, new Vector2(-100, 90)); Position(badge, new Vector2(362, 90));
                Position(master.GetComponent<RectTransform>(), new Vector2(0, 36));
                Position(musicHeading.rectTransform, new Vector2(-100, -30)); Position(musicBadge, new Vector2(362, -30));
                Position(slider.GetComponent<RectTransform>(), new Vector2(0, -84));
                var display = card.Find("Display section").GetComponent<RectTransform>();
                Undo.RecordObject(display, "Fit display section below music settings");
                display.anchoredPosition = new Vector2(0, -169); display.sizeDelta = new Vector2(880, 118);
                var toggle = serialized.FindProperty("fullscreen").objectReferenceValue as Toggle;
                var back = serialized.FindProperty("optionsClose").objectReferenceValue as Button;
                Undo.RecordObjects(new UnityEngine.Object[] { master, toggle, back }, "Include music volume in menu navigation");
                master.navigation = Nav(back, slider); slider.navigation = Nav(master, toggle);
                toggle.navigation = Nav(slider, back); back.navigation = Nav(toggle, master);
                serialized.FindProperty("musicVolume").objectReferenceValue = slider;
                serialized.FindProperty("musicVolumeReadout").objectReferenceValue = readout;
            }
            Undo.RecordObject(menu, "Wire menu music settings");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            MainMenuSliderFocus.ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void Position(RectTransform rect, Vector2 position)
        {
            Undo.RecordObject(rect, "Position audio volume row"); rect.anchoredPosition = position;
        }

        private static Navigation Nav(Selectable up, Selectable down)
            => new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };
    }
}
