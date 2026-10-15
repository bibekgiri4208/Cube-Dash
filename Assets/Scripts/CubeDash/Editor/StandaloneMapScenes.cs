using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Save-as Level copies with one retained biome. Never saves Level or edits shared gameplay/scenery prefabs.</summary>
    public static class StandaloneMapScenes
    {
        public const string SourcePath = "Assets/Scenes/Level.unity";
        public static readonly string[] MapNames = { "City", "Jungle", "Desert", "Beach" };
        public static readonly EnvironmentBiome[] MapBiomes = { EnvironmentBiome.City, EnvironmentBiome.Jungle, EnvironmentBiome.Desert, EnvironmentBiome.Beach };
        public static string ScenePath(int index) => "Assets/Scenes/" + MapNames[index] + ".unity";
        public static string SkyPath(int index) => "Assets/Maps/Skies/" + MapNames[index] + " Sky.mat";

        [MenuItem("Tools/Cube Dash/Separate Level into Four Maps")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }
        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            // Inspect every existing target before creating anything. Unknown/user-authored scenes are never replaced.
            for (int i = 0; i < MapNames.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath(i)) == null && AssetDatabase.LoadAssetAtPath<Material>(SkyPath(i)) != null)
                    throw new InvalidOperationException("Unassigned map sky requires inspection: " + SkyPath(i));
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath(i)) != null)
                {
                    var existing = EditorSceneManager.OpenPreviewScene(ScenePath(i));
                    try
                    {
                        var director = Find<EnvironmentDirector>(existing);
                        if (!director.HasFixedBiome || director.FixedBiome != MapBiomes[i])
                            throw new InvalidOperationException("Existing scene requires inspection: " + ScenePath(i));
                    }
                    finally { EditorSceneManager.ClosePreviewScene(existing); }
                }
            }
            Folder("Assets/Maps"); Folder("Assets/Maps/Skies");
            for (int i = 0; i < MapNames.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath(i)) != null)
                {
                    Debug.Log("Preserved existing standalone map edits: " + MapNames[i]); continue;
                }
                // Copy first: even interrupted authoring cannot leave the user's original Level dirty.
                if (!AssetDatabase.CopyAsset(SourcePath, ScenePath(i))) throw new InvalidOperationException("Could not copy Level for " + MapNames[i]);
                Scene scene = EditorSceneManager.OpenScene(ScenePath(i));
                ConfigureCopy(scene, MapBiomes[i]);
                SaveAtmosphere(scene, i);
                if (!EditorSceneManager.SaveScene(scene, ScenePath(i))) throw new InvalidOperationException("Could not save " + MapNames[i]);
            }
            var builds = EditorBuildSettings.scenes.ToList();
            for (int i = 0; i < MapNames.Length; i++)
            {
                string path = ScenePath(i); int entry = builds.FindIndex(item => item.path == path);
                var buildScene = new EditorBuildSettingsScene(path, true) { guid = new GUID(AssetDatabase.AssetPathToGUID(path)) };
                if (entry < 0) builds.Add(buildScene); else builds[entry] = buildScene;
            }
            // Unity can retain a stale build GUID when a scene is recreated at the same path.
            // Remove only these entries before reinserting them; unrelated build entries/order stay intact.
            if (EditorBuildSettings.scenes.Any(old => builds.Any(next => next.path == old.path && next.guid != old.guid)))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(entry => !Enumerable.Range(0, MapNames.Length)
                    .Any(index => entry.path == ScenePath(index))).ToArray();
            EditorBuildSettings.scenes = builds.ToArray();
            var menuScene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            MainMenuMapSelection.ApplyToScene(menuScene);
            if (!EditorSceneManager.SaveScene(menuScene)) throw new InvalidOperationException("Could not save map selection targets.");
            AssetDatabase.SaveAssets();
            Debug.Log("Four standalone maps saved from Level: same roads, progression, player and UI; each retains only its chosen biome. Dynamic loads the original mixed-biome Level; no standalone Mountain is authored. Country Side and shared prefabs preserved. All six menu maps enabled.");
        }

        private static void ConfigureCopy(Scene scene, EnvironmentBiome biome)
        {
            var director = Find<EnvironmentDirector>(scene);
            var settings = new SerializedObject(director); settings.FindProperty("useFixedBiome").boolValue = true;
            settings.FindProperty("fixedBiome").enumValueIndex = (int)biome; settings.ApplyModifiedPropertiesWithoutUndo();
            var track = Find<EndlessTrack>(scene);
            foreach (TrackSegment segment in track.Segments)
            {
                // Unpack only this copy's outer track instance; nested selected scenery/obstacles remain linked.
                if (PrefabUtility.IsPartOfPrefabInstance(segment.gameObject))
                    PrefabUtility.UnpackPrefabInstance(segment.gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                var environment = segment.Environment;
                GameObject chosen = environment.Biomes[(int)biome];
                if (chosen == null) throw new InvalidOperationException("The source Level is missing " + biome + " scenery.");
                var unused = environment.Biomes.Where((root, index) => index != (int)biome && root != null).ToArray();
                var biomeData = new SerializedObject(environment); var roots = biomeData.FindProperty("biomes");
                for (int i = 0; i < roots.arraySize; i++) if (i != (int)biome) roots.GetArrayElementAtIndex(i).objectReferenceValue = null;
                biomeData.ApplyModifiedPropertiesWithoutUndo();
                TrimBoundaryReferences(segment.GetComponent<BiomeBoundaryBlend>(), biome);
                TrimRiseReferences(segment.GetComponent<SceneryRise>(), chosen.transform);
                foreach (GameObject root in unused) Object.DestroyImmediate(root);
                environment.Configure(Array.IndexOf(track.Segments, segment) - 1, director.SegmentsPerBiome, biome);
                segment.GetComponent<SceneryRise>()?.Settle();
                foreach (Component component in segment.GetComponentsInChildren<Component>(true))
                    if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            var ambience = Find<BiomeAmbience>(scene);
            var audioData = new SerializedObject(ambience); var sources = audioData.FindProperty("sources");
            for (int i = 0; i < sources.arraySize; i++)
            {
                if (i == (int)biome) continue;
                var source = sources.GetArrayElementAtIndex(i).objectReferenceValue as AudioSource;
                sources.GetArrayElementAtIndex(i).objectReferenceValue = null;
                if (source != null) Object.DestroyImmediate(source.gameObject);
            }
            audioData.ApplyModifiedPropertiesWithoutUndo();
            director.ApplyDistance(0, 0);
        }

        private static void TrimBoundaryReferences(BiomeBoundaryBlend boundary, EnvironmentBiome biome)
        {
            if (boundary == null) return;
            var data = new SerializedObject(boundary); var regions = data.FindProperty("regions");
            for (int i = 0; i < regions.arraySize; i++)
            {
                if (i == (int)biome) continue;
                var region = regions.GetArrayElementAtIndex(i);
                foreach (string field in new[] { "Ground", "Water", "NormalGround", "BoundaryGround" }) region.FindPropertyRelative(field).arraySize = 0;
                foreach (string field in new[] { "Floor", "EntranceDressing", "ExitDressing" }) region.FindPropertyRelative(field).objectReferenceValue = null;
            }
            if (biome != EnvironmentBiome.City) { data.FindProperty("cityBuildings").arraySize = 0; data.FindProperty("cityBuildingScales").arraySize = 0; }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void TrimRiseReferences(SceneryRise rise, Transform chosen)
        {
            if (rise == null) return;
            var data = new SerializedObject(rise); var renderers = data.FindProperty("scenery");
            var keep = Enumerable.Range(0, renderers.arraySize).Select(i => renderers.GetArrayElementAtIndex(i).objectReferenceValue as Renderer)
                .Where(renderer => renderer != null && renderer.transform.IsChildOf(chosen)).ToArray();
            renderers.arraySize = keep.Length;
            for (int i = 0; i < keep.Length; i++) renderers.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
            if (!chosen.GetComponentsInChildren<CityFlyover>(true).Any())
            {
                data.FindProperty("buildings").arraySize = 0; data.FindProperty("buildingHomes").arraySize = 0;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SaveAtmosphere(Scene scene, int index)
        {
            Material original = RenderSettings.skybox;
            if (original == null) throw new InvalidOperationException("Source Level skybox is required.");
            string path = SkyPath(index);
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) throw new InvalidOperationException("Unassigned map sky requires inspection: " + path);
            var sky = new Material(original) { name = MapNames[index] + " Sky" };
            var director = Find<EnvironmentDirector>(scene);
            var atmosphere = new SerializedObject(director).FindProperty("atmospheres").GetArrayElementAtIndex((int)MapBiomes[index]);
            sky.SetColor("_HorizonColor", atmosphere.FindPropertyRelative("Horizon").colorValue);
            sky.SetColor("_ZenithColor", atmosphere.FindPropertyRelative("Zenith").colorValue);
            sky.SetColor("_GroundColor", atmosphere.FindPropertyRelative("Ground").colorValue);
            sky.SetColor("_SunColor", atmosphere.FindPropertyRelative("Sun").colorValue * 1.2f);
            AssetDatabase.CreateAsset(sky, path); RenderSettings.skybox = sky;
        }
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
