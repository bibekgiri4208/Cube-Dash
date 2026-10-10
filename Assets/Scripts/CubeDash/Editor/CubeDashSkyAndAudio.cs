using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    public static class CubeDashSkyAndAudio
    {
        [MenuItem("Tools/Cube Dash/Improve Sky and Power-Up Audio")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            if (game == null) throw new InvalidOperationException("The authored Cube Dash level is required.");
            if (!AssetDatabase.IsValidFolder("Assets/Audio")) AssetDatabase.CreateFolder("Assets", "Audio");
            RunnerAudio audio = game.GetComponent<RunnerAudio>();
            if (audio == null) audio = game.gameObject.AddComponent<RunnerAudio>();
            var settings = new SerializedObject(audio);
            AudioSource pickup = Source(game.transform, "Power-Up Audio", false, null);
            AudioSource flight = Source(game.transform, "Airplane Engine Audio", true, RunnerSoundDesign.Engine(true));
            AudioSource truck = Source(game.transform, "Truck Engine Audio", true, RunnerSoundDesign.Engine(false));
            settings.FindProperty("pickupSource").objectReferenceValue = pickup;
            settings.FindProperty("flightSource").objectReferenceValue = flight;
            settings.FindProperty("truckSource").objectReferenceValue = truck;
            var clips = settings.FindProperty("pickupClips"); clips.arraySize = 5;
            for (int i = 0; i < 5; i++) clips.GetArrayElementAtIndex(i).objectReferenceValue = RunnerSoundDesign.Pickup((PowerUpType)i);
            settings.ApplyModifiedPropertiesWithoutUndo();
            var manager = new SerializedObject(game);
            manager.FindProperty("audioPresentation").objectReferenceValue = audio;
            manager.ApplyModifiedPropertiesWithoutUndo();

            Material sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/CubeDashHorizon.mat");
            sky.SetFloat("_GradientPower", 0.65f); sky.SetFloat("_SunSize", 0.65f);
            sky.SetFloat("_CloudCoverage", 0.48f); sky.SetFloat("_CloudScale", 0.9f);
            sky.SetFloat("_CloudOpacity", 0.78f); sky.SetFloat("_CloudSpeed", 0.012f);
            sky.SetColor("_CloudColor", new Color(0.96f, 0.98f, 1f));
            var atmosphere = new SerializedObject(game.Track.Environment);
            var sun = (Light)atmosphere.FindProperty("sunlight").objectReferenceValue;
            if (sun != null) { sky.SetVector("_SunDirection", -sun.transform.forward); sky.SetColor("_SunColor", sun.color * 1.2f); }
            Color[] zeniths = { new Color(0.2f, 0.5f, 0.78f), new Color(0.14f, 0.44f, 0.64f),
                new Color(0.27f, 0.53f, 0.78f), new Color(0.3f, 0.55f, 0.76f), new Color(0.16f, 0.56f, 0.83f) };
            var palettes = atmosphere.FindProperty("atmospheres");
            for (int i = 0; i < Mathf.Min(zeniths.Length, palettes.arraySize); i++)
                palettes.GetArrayElementAtIndex(i).FindPropertyRelative("Zenith").colorValue = zeniths[i];
            atmosphere.ApplyModifiedPropertiesWithoutUndo();
            sky.SetColor("_ZenithColor", zeniths[0]);
            RenderSettings.skybox = sky;
            EditorUtility.SetDirty(sky);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("Sky and audio saved: layered clouds, deeper biome skies, aligned sun, five original pickup cues and two engine loops. Coins, gameplay, lighting and biome fog preserved.");
        }

        [MenuItem("Tools/Cube Dash/Refine Vehicle Engine Audio")]
        public static void RefineVehicleAudioFromCommandLine()
        {
            RunnerSoundDesign.Engine(true);
            RunnerSoundDesign.Engine(false);
            AssetDatabase.SaveAssets();
            Debug.Log("Vehicle audio refined from CC0 recordings: six-second crossfaded F-22 airflow and heavy truck engine loops. Pickup sounds, volume settings, sky and level unchanged.");
        }

        private static AudioSource Source(Transform parent, string name, bool loop, AudioClip clip)
        {
            Transform child = parent.Find(name);
            if (child == null) { child = new GameObject(name, typeof(AudioSource)).transform; child.SetParent(parent, false); }
            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null) source = child.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0;
            source.dopplerLevel = 0; source.volume = loop ? 0 : 1;
            source.pitch = 1; source.clip = clip; source.priority = loop ? 160 : 80;
            return source;
        }
    }
}
