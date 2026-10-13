using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Upgrades only Country Side, retaining linked Japanese artwork and all other maps.</summary>
    public static class CountrySideConsistentBuilder
    {
        public const string GameplayPath = "Assets/CountrySide/Prefabs/Country Side Highway Gameplay.prefab";
        public const string SkyPath = "Assets/Maps/Skies/Country Side Sky.mat";
        [MenuItem("Tools/Cube Dash/Match Country Side to Other Maps")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }
        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            Scene scene = SceneManager.GetSceneByPath(CountrySideSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(CountrySideSceneBuilder.ScenePath);
            SceneManager.SetActiveScene(scene); ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Country Side.");
            AssetDatabase.SaveAssets();
        }
        public static void ApplyToScene(Scene scene)
        {
            if (scene.path != CountrySideSceneBuilder.ScenePath) throw new InvalidOperationException("Only Country Side can be upgraded.");
            var oldGame = Find<CubeDashGame>(scene); var oldScenery = Find<CountrySideRunnerScenery>(scene);
            if (oldScenery.FollowsRoadCurves && oldGame.UsesStartCountdown && Find<RunnerHud>(scene).RibbonTheme)
            { Debug.Log("Consistent Country Side already authored; manual scenery/UI/timing edits preserved."); return; }
            GameObject oldRig = scene.GetRootGameObjects().Single(root => root.name == CountrySidePlayableBuilder.RootName);
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(oldRig) != CountrySidePlayableBuilder.GameplayPath)
                throw new InvalidOperationException("An unrecognized gameplay rig requires inspection before replacing it.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GameplayPath) != null || AssetDatabase.LoadAssetAtPath<Material>(SkyPath) != null)
                throw new InvalidOperationException("Unassigned upgraded assets already exist; inspect them first.");
            GameObject environment = scene.GetRootGameObjects().Single(root => root.name == CountrySideSceneBuilder.RootName);
            Transform[] tiles = oldScenery.Tiles.ToArray();
            Camera camera = oldGame.GameCamera;
            Light sun = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true)).Single(light => light.type == LightType.Directional);
            Scene source = EditorSceneManager.OpenPreviewScene(StandaloneMapScenes.SourcePath);
            try
            {
                var group = new GameObject(CountrySidePlayableBuilder.RootName); SceneManager.MoveGameObjectToScene(group, source);
                foreach (string name in new[] { "Main Camera", "Player Cube", "Track", "Game Manager", "Canvas", "EventSystem", "PlayerTrail", "Obstacle Fragments", "Sky Bounce Fill", "City Atmosphere" })
                    source.GetRootGameObjects().Single(root => root.name == name).transform.SetParent(group.transform, true);
                group.transform.Find("City Atmosphere").name = "Runner Atmosphere";
                var track = group.GetComponentInChildren<EndlessTrack>();
                foreach (TrackSegment segment in track.Segments)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(segment.gameObject))
                        PrefabUtility.UnpackPrefabInstance(segment.gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                    foreach (GameObject biome in segment.Environment.Biomes) if (biome != null) Object.DestroyImmediate(biome);
                    Set(segment, "environment", null);
                    foreach (MonoBehaviour component in segment.GetComponents<MonoBehaviour>())
                        if (!(component is TrackSegment)) Object.DestroyImmediate(component);
                }
                var director = track.Environment; Set(director, "sunlight", null);
                var atmosphereData = new SerializedObject(director);
                atmosphereData.FindProperty("useFixedBiome").boolValue = true;
                atmosphereData.FindProperty("fixedBiome").enumValueIndex = (int)EnvironmentBiome.Jungle;
                var spring = atmosphereData.FindProperty("atmospheres").GetArrayElementAtIndex((int)EnvironmentBiome.Jungle);
                spring.FindPropertyRelative("Horizon").colorValue = new Color(0.78f, 0.88f, 0.90f);
                spring.FindPropertyRelative("Zenith").colorValue = new Color(0.32f, 0.57f, 0.77f);
                spring.FindPropertyRelative("Ground").colorValue = new Color(0.27f, 0.39f, 0.31f);
                spring.FindPropertyRelative("Sun").colorValue = new Color(1, 0.94f, 0.84f);
                spring.FindPropertyRelative("SunIntensity").floatValue = 1.35f;
                spring.FindPropertyRelative("AmbientSky").colorValue = new Color(0.66f, 0.78f, 0.86f);
                spring.FindPropertyRelative("AmbientEquator").colorValue = new Color(0.55f, 0.64f, 0.50f);
                spring.FindPropertyRelative("AmbientGround").colorValue = new Color(0.25f, 0.29f, 0.22f);
                atmosphereData.ApplyModifiedPropertiesWithoutUndo();
                var ambience = group.GetComponentInChildren<BiomeAmbience>();
                var ambienceData = new SerializedObject(ambience); var sources = ambienceData.FindProperty("sources");
                for (int i = 0; i < sources.arraySize; i++)
                {
                    if (i == (int)EnvironmentBiome.Jungle) continue;
                    var audio = sources.GetArrayElementAtIndex(i).objectReferenceValue as AudioSource;
                    sources.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    if (audio != null) Object.DestroyImmediate(audio.gameObject);
                }
                ambienceData.ApplyModifiedPropertiesWithoutUndo();
                group.GetComponentInChildren<InputSystemUIInputModule>().UnassignActions();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(group, GameplayPath);
                if (prefab == null) throw new InvalidOperationException("Could not save the consistent countryside rig.");
                GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                rig.name = CountrySidePlayableBuilder.RootName;
                var game = rig.GetComponentInChildren<CubeDashGame>();
                Camera clone = rig.GetComponentInChildren<Camera>();
                EditorUtility.CopySerialized(clone, camera);
                camera.transform.SetPositionAndRotation(clone.transform.position, clone.transform.rotation);
                var cameraData = camera.GetUniversalAdditionalCameraData();
                EditorUtility.CopySerialized(clone.GetUniversalAdditionalCameraData(), cameraData);
                var graphics = camera.GetComponent<RunnerGraphics>();
                if (graphics == null) graphics = camera.gameObject.AddComponent<RunnerGraphics>();
                EditorUtility.CopySerialized(clone.GetComponent<RunnerGraphics>(), graphics); Set(graphics, "game", game);
                Set(game, "gameCamera", camera);
                foreach (Canvas canvas in rig.GetComponentsInChildren<Canvas>(true)) if (canvas.worldCamera == clone) canvas.worldCamera = camera;
                Object.DestroyImmediate(clone.gameObject);
                Light sourceSun = source.GetRootGameObjects().Single(root => root.name == "Directional Light").GetComponent<Light>();
                EditorUtility.CopySerialized(sourceSun, sun); sun.transform.rotation = sourceSun.transform.rotation;
                Set(game.Track.Environment, "sunlight", sun);
                var module = rig.GetComponentInChildren<InputSystemUIInputModule>(); module.actionsAsset = null; module.AssignDefaultActions();
                var scenery = rig.AddComponent<CountrySideRunnerScenery>(); var sceneryData = new SerializedObject(scenery);
                sceneryData.FindProperty("track").objectReferenceValue = game.Track;
                var tileData = sceneryData.FindProperty("tiles"); tileData.arraySize = tiles.Length;
                for (int i = 0; i < tiles.Length; i++)
                {
                    tileData.GetArrayElementAtIndex(i).objectReferenceValue = tiles[i];
                    tiles[i].Find("Runner road - clear 8.4 metre corridor").gameObject.SetActive(false);
                    foreach (GameObject item in tiles[i].GetComponentsInChildren<Transform>(true).Select(t => t.gameObject))
                        if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                }
                sceneryData.FindProperty("followRoadCurves").boolValue = true;
                var plants = environment.GetComponentsInChildren<Transform>(true).Where(item => PrefabUtility.IsAnyPrefabInstanceRoot(item.gameObject)
                    && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item.gameObject).StartsWith("Assets/Idyllic Fantasy Nature/Prefabs/")).ToArray();
                var plantData = sceneryData.FindProperty("curvePlants"); plantData.arraySize = plants.Length;
                for (int i = 0; i < plants.Length; i++) plantData.GetArrayElementAtIndex(i).objectReferenceValue = plants[i];
                environment.transform.Find("Sky - soft distant cloud banks").gameObject.SetActive(false);
                foreach (Volume volume in environment.GetComponentsInChildren<Volume>(true)) volume.enabled = false;
                var surfaces = environment.GetComponentsInChildren<MeshRenderer>(true).Where(renderer => renderer.gameObject.activeInHierarchy
                    && !plants.Any(plant => renderer.transform.IsChildOf(plant))
                    && renderer.transform.childCount == 0 && renderer.sharedMaterials.All(material => material != null &&
                        (material.shader.name == "Universal Render Pipeline/Lit" || material.shader.name == "CubeDash/Countryside Ground"))).ToArray();
                var surfaceData = sceneryData.FindProperty("curvedSurfaces"); surfaceData.arraySize = surfaces.Length;
                for (int i = 0; i < surfaces.Length; i++) surfaceData.GetArrayElementAtIndex(i).objectReferenceValue = surfaces[i];
                sceneryData.ApplyModifiedPropertiesWithoutUndo();
                var sky = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Maps/Skies/Jungle Sky.mat")) { name = "Country Side Sky" };
                sky.SetColor("_HorizonColor", spring.FindPropertyRelative("Horizon").colorValue);
                sky.SetColor("_ZenithColor", spring.FindPropertyRelative("Zenith").colorValue);
                sky.SetColor("_GroundColor", spring.FindPropertyRelative("Ground").colorValue);
                sky.SetColor("_SunColor", spring.FindPropertyRelative("Sun").colorValue * 1.2f);
                sky.SetVector("_SunDirection", -sun.transform.forward); AssetDatabase.CreateAsset(sky, SkyPath);
                RenderSettings.skybox = sky; RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientIntensity = 1; RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 45; RenderSettings.fogEndDistance = 260;
                game.Track.Environment.ApplyDistance(0, 0);
                // Only replace the recognized old rig after the new prefab, wiring and artwork references exist.
                Object.DestroyImmediate(oldRig);
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("Country Side now shares Level's highway, curves, progression, camera, themed HUD, 3/2/1, power-up bars and atmosphere shader. Linked Japanese nature/architecture scenery, original camera/sun identities and scene GUID retained.");
            }
            finally { EditorSceneManager.ClosePreviewScene(source); }
        }
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static void Set(Object target, string name, Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
