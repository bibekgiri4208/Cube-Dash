using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Additive UI/audio upgrade for all playable maps; never rebuilds their worlds.</summary>
    public static class PauseSoundAuthoring
    {
        public const string MixerPath = "Assets/Resources/Game Audio.mixer";
        public static readonly string[] Maps = { "Level", "Country Side", "City", "Jungle", "Desert", "Beach" };

        [MenuItem("Tools/Cube Dash/Add Pause Sound Controls and Map Music")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            Scene menu = EditorSceneManager.OpenPreviewScene(MainMenuSceneBuilder.ScenePath);
            try
            {
                Slider template = menu.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true))
                    .Single().OptionsPanel.GetComponentsInChildren<Slider>(true).First();
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MainMenuMusic.ClipPath);
                if (clip == null) throw new InvalidOperationException("The main-menu music clip is missing.");
                AudioMixer mixer = EnsureMixer();
                foreach (string map in Maps)
                {
                    Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/" + map + ".unity");
                    ApplyToScene(scene, template, clip, mixer);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Sound controls: " + map);
                }
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.ClosePreviewScene(menu); }
        }

        public static void ApplyToScene(Scene scene, Slider template, AudioClip clip, AudioMixer mixer)
        {
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RunnerHud>(true)).Single();
            var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CubeDashGame>(true)).Single();
            Transform safe = hud.transform.Find("Safe Area");
            Transform card = safe.Find("Menu Overlay/Menu Card");
            if (hud.GetComponent<PauseSoundMenu>() == null) BuildMenu(hud, card, safe, template);
            var soundMenu = hud.GetComponent<PauseSoundMenu>();
            foreach (Slider slider in new[] { soundMenu.MusicSlider, soundMenu.SfxSlider })
            {
                // The main-menu template has a fixed-width track; the map panel is responsive.
                var track = slider.transform.Find("Track").GetComponent<RectTransform>();
                track.anchorMin = new Vector2(0, 0.5f); track.anchorMax = new Vector2(1, 0.5f);
                track.anchoredPosition = Vector2.zero; track.sizeDelta = new Vector2(0, 14);
            }

            MapMusic music = game.GetComponentInChildren<MapMusic>(true);
            if (music == null)
            {
                var go = new GameObject("Map Background Music", typeof(AudioSource), typeof(MapMusic));
                go.transform.SetParent(game.transform, false);
                music = go.GetComponent<MapMusic>();
                var source = go.GetComponent<AudioSource>();
                source.clip = clip; source.loop = true; source.playOnAwake = false;
                source.spatialBlend = 0; source.volume = 1;
            }
            AudioMixerGroup sfx = mixer.FindMatchingGroups("SFX").Single();
            AudioMixerGroup musicGroup = mixer.FindMatchingGroups("Music").Single();
            foreach (AudioSource source in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AudioSource>(true)))
            {
                source.outputAudioMixerGroup = source == music.Source ? musicGroup : sfx;
                EditorUtility.SetDirty(source);
                if (PrefabUtility.IsPartOfPrefabInstance(source)) PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void BuildMenu(RunnerHud hud, Transform card, Transform safe, Slider template)
        {
            var controller = hud.gameObject.AddComponent<PauseSoundMenu>();
            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MainMenu/Fonts/Anton-Regular.ttf");
            var restart = card.Find("Restart").GetComponent<Button>();
            var sound = Object.Instantiate(restart, card); sound.name = "Sound";
            sound.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(sound.onClick, controller.Open);
            sound.GetComponentInChildren<Text>(true).text = "SOUND";
            var buttons = new[] { card.Find("Primary Action").GetComponent<Button>(), restart, sound, card.Find("Quit").GetComponent<Button>() };
            ((RectTransform)card).sizeDelta = new Vector2(((RectTransform)card).sizeDelta.x, 532);
            for (int i = 0; i < buttons.Length; i++)
            {
                ((RectTransform)buttons[i].transform).anchoredPosition = new Vector2(0, 80 - i * 92);
                buttons[i].navigation = Nav(buttons[(i + 3) % 4], buttons[(i + 1) % 4]);
                EditorUtility.SetDirty(buttons[i]);
                if (PrefabUtility.IsPartOfPrefabInstance(buttons[i]))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i].transform);
                }
            }
            if (PrefabUtility.IsPartOfPrefabInstance(card)) PrefabUtility.RecordPrefabInstancePropertyModifications(card);
            sound.gameObject.SetActive(false);

            RectTransform panel = Rect("Sound Panel", safe, Vector2.zero, new Vector2(560, 440));
            var background = panel.gameObject.AddComponent<MenuPanelGraphic>();
            background.color = LevelMenuTheme.Paper; background.BorderColor = LevelMenuTheme.Ink;
            background.BorderWidth = 4; background.CornerCut = 22; background.CornerRadius = 0;
            RectTransform header = Rect("Ribbon header", panel, new Vector2(0, 150), new Vector2(520, 98));
            header.gameObject.AddComponent<MenuRibbonGraphic>().raycastTarget = false;
            Label("Heading", panel, font, "SOUND", new Vector2(-4, 152), new Vector2(480, 86), 46);
            Slider music = Row("Music", panel, template, font, 48, out Text musicValue);
            Slider sfx = Row("SFX", panel, template, font, -56, out Text sfxValue);
            music.onValueChanged = new Slider.SliderEvent(); sfx.onValueChanged = new Slider.SliderEvent();
            UnityEventTools.AddPersistentListener(music.onValueChanged, controller.SetMusicVolume);
            UnityEventTools.AddPersistentListener(sfx.onValueChanged, controller.SetSfxVolume);
            Button back = Object.Instantiate(restart, panel); back.name = "Back";
            ((RectTransform)back.transform).anchoredPosition = new Vector2(0, -158);
            back.GetComponentInChildren<Text>(true).text = "BACK";
            back.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(back.onClick, controller.Close);
            back.gameObject.SetActive(true);
            music.navigation = Nav(back, sfx); sfx.navigation = Nav(music, back); back.navigation = Nav(sfx, music);
            Set(controller, "panel", panel.gameObject); Set(controller, "pauseCard", card.gameObject);
            Set(controller, "soundButton", sound); Set(controller, "backButton", back);
            Set(controller, "musicSlider", music); Set(controller, "sfxSlider", sfx);
            Set(controller, "musicReadout", musicValue); Set(controller, "sfxReadout", sfxValue);
            Set(hud, "soundMenu", controller);
            panel.gameObject.SetActive(false);
        }

        private static Slider Row(string name, Transform parent, Slider template, Font font, float y, out Text readout)
        {
            Text title = Label(name + " label", parent, font, name == "Music" ? "MUSIC" : "SFX", new Vector2(-52, y + 22), new Vector2(300, 40), 26);
            title.alignment = TextAnchor.MiddleLeft;
            readout = Label(name + " value", parent, font, "100%", new Vector2(160, y + 22), new Vector2(90, 40), 24);
            var slider = Object.Instantiate(template, parent); slider.name = name + " Slider";
            var rect = (RectTransform)slider.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = new Vector2(0, y - 18); rect.sizeDelta = new Vector2(410, 32);
            slider.SetValueWithoutNotify(1);
            return slider;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }

        private static Text Label(string name, Transform parent, Font font, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var label = Rect(name, parent, position, size).gameObject.AddComponent<MenuLabelText>();
            label.font = font; label.text = value; label.fontSize = fontSize; label.color = LevelMenuTheme.Ink;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            return label;
        }

        private static Navigation Nav(Selectable up, Selectable down)
            => new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static AudioMixer EnsureMixer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null) { EnsureMixerView(existing); return existing; }
            // Unity has no public mixer-authoring API. Reflection is confined to this editor tool;
            // runtime and the saved asset use the standard AudioMixer/AudioMixerGroup APIs.
            Type type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("UnityEditor.Audio.AudioMixerController"))
                .First(value => value != null);
            var mixer = (AudioMixer)type.GetMethod("CreateMixerControllerAtPath").Invoke(null, new object[] { MixerPath });
            object master = type.GetProperty("masterGroup").GetValue(mixer);
            object sfx = null;
            foreach (string name in new[] { "Music", "SFX" })
            {
                object group = type.GetMethod("CreateNewGroup").Invoke(mixer, new object[] { name, false });
                type.GetMethod("AddChildToParent").Invoke(mixer, new[] { group, master });
                object snapshot = type.GetProperty("startSnapshot").GetValue(mixer);
                group.GetType().GetMethod("SetValueForVolume").Invoke(group, new[] { mixer, snapshot, (object)0f });
                group.GetType().GetMethod("SetValueForPitch").Invoke(group, new[] { mixer, snapshot, (object)1f });
                if (name == "SFX") sfx = group;
                EditorUtility.SetDirty((Object)group);
            }
            PropertyInfo exposed = type.GetProperty("exposedParameters");
            Type parameterType = exposed.PropertyType.GetElementType();
            object parameter = Activator.CreateInstance(parameterType);
            parameterType.GetField("name").SetValue(parameter, GameAudioSettings.SfxParameter);
            parameterType.GetField("guid").SetValue(parameter, sfx.GetType().GetMethod("GetGUIDForVolume").Invoke(sfx, null));
            Array parameters = Array.CreateInstance(parameterType, 1); parameters.SetValue(parameter, 0);
            exposed.SetValue(mixer, parameters);
            mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;
            EnsureMixerView(mixer);
            EditorUtility.SetDirty((Object)master); EditorUtility.SetDirty(mixer);
            AssetDatabase.SaveAssets();
            return mixer;
        }

        private static void EnsureMixerView(AudioMixer mixer)
        {
            Type type = mixer.GetType();
            PropertyInfo views = type.GetProperty("views");
            if (((Array)views.GetValue(mixer)).Length != 0) return;
            // Include all buses in the inspector's default view, not just in the runtime graph.
            object master = type.GetProperty("masterGroup").GetValue(mixer);
            var groups = mixer.FindMatchingGroups("");
            Type guidType = master.GetType().GetProperty("groupID").PropertyType;
            Array guids = Array.CreateInstance(guidType, groups.Length);
            for (int i = 0; i < groups.Length; i++) guids.SetValue(groups[i].GetType().GetProperty("groupID").GetValue(groups[i]), i);
            Type viewType = views.PropertyType.GetElementType();
            object view = Activator.CreateInstance(viewType);
            viewType.GetField("name").SetValue(view, "All Channels"); viewType.GetField("guids").SetValue(view, guids);
            Array items = Array.CreateInstance(viewType, 1); items.SetValue(view, 0); views.SetValue(mixer, items);
            EditorUtility.SetDirty(mixer);
        }
    }
}
