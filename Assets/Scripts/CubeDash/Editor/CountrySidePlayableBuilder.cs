using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    public static class CountrySidePlayableBuilder
    {
        public const string RootName = "Country Side Gameplay";
        public const string GameplayPath = CountrySideSceneBuilder.ArtPath + "Prefabs/Country Side Gameplay.prefab";

        [MenuItem("Tools/Cube Dash/Make Country Side Playable")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before authoring gameplay.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            Scene scene = SceneManager.GetSceneByPath(CountrySideSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(CountrySideSceneBuilder.ScenePath);
            SceneManager.SetActiveScene(scene);
            ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save playable Country Side.");
            var builds = EditorBuildSettings.scenes.ToList();
            int entry = builds.FindIndex(item => item.path == CountrySideSceneBuilder.ScenePath);
            if (entry < 0) builds.Add(new EditorBuildSettingsScene(CountrySideSceneBuilder.ScenePath, true));
            else builds[entry].enabled = true;
            EditorBuildSettings.scenes = builds.ToArray();
            AssetDatabase.SaveAssets();
        }

        public static void ApplyToScene(Scene scene)
        {
            if (scene.path != CountrySideSceneBuilder.ScenePath) throw new InvalidOperationException("Only Country Side can be upgraded by this tool.");
            if (scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CubeDashGame>(true)).Any())
            {
                Debug.Log("Country Side already has gameplay; existing placements and tuning were preserved."); return;
            }
            GameObject environment = scene.GetRootGameObjects().Single(r => r.name == CountrySideSceneBuilder.RootName);
            Camera camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GameplayPath) != null ||
                AssetDatabase.LoadAssetAtPath<GameObject>(CountrySideSceneBuilder.ArtPath + "Prefabs/Playable Countryside Route.prefab") != null)
                throw new InvalidOperationException("Playable assets already exist without their scene rig; inspect them before replacing anything.");
            Scene level = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                var sourceGroup = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(sourceGroup, level);
                string[] roots = { "Main Camera", "Player Cube", "Track", "Game Manager", "Canvas", "EventSystem", "PlayerTrail", "Obstacle Fragments" };
                foreach (string name in roots)
                    level.GetRootGameObjects().Single(root => root.name == name).transform.SetParent(sourceGroup.transform, true);
                var sourceTrack = sourceGroup.GetComponentInChildren<EndlessTrack>();
                Set(sourceTrack, "environment", null);
                foreach (TrackSegment segment in sourceTrack.Segments)
                {
                    Set(segment, "environment", null);
                    foreach (MonoBehaviour behaviour in segment.GetComponents<MonoBehaviour>())
                        if (!(behaviour is TrackSegment)) Object.DestroyImmediate(behaviour);
                    foreach (Transform child in segment.transform.Cast<Transform>().ToArray())
                        if (!child.GetComponentsInChildren<RunnerCube>(true).Any() && !child.GetComponentsInChildren<PowerUpPickup>(true).Any())
                            Object.DestroyImmediate(child.gameObject);
                }
                Object.DestroyImmediate(sourceTrack.GetComponent<EnvironmentDirector>());
                var sourceGame = sourceGroup.GetComponentInChildren<CubeDashGame>();
                Set(sourceGame, "ambiencePresentation", null);
                var ambience = sourceGame.GetComponent<BiomeAmbience>();
                if (ambience != null)
                {
                    var serialized = new SerializedObject(ambience);
                    var sources = serialized.FindProperty("sources");
                    for (int i = 0; i < sources.arraySize; i++)
                    {
                        var audio = sources.GetArrayElementAtIndex(i).objectReferenceValue as AudioSource;
                        if (audio != null) Object.DestroyImmediate(audio.gameObject);
                    }
                    Object.DestroyImmediate(ambience);
                }

                // Save a linked rig instead of flattening every nested vehicle/particle prefab into the scene.
                sourceGroup.GetComponentInChildren<InputSystemUIInputModule>().UnassignActions();
                GameObject rig = PrefabUtility.SaveAsPrefabAsset(sourceGroup, GameplayPath);
                if (rig == null) throw new InvalidOperationException("Could not save the Country Side gameplay rig.");
                GameObject gameplay = (GameObject)PrefabUtility.InstantiatePrefab(rig, scene);
                gameplay.name = RootName;
                Camera clonedCamera = gameplay.GetComponentInChildren<Camera>();
                camera.transform.SetPositionAndRotation(clonedCamera.transform.position, clonedCamera.transform.rotation);
                camera.fieldOfView = clonedCamera.fieldOfView;
                if (clonedCamera.TryGetComponent(out RunnerGraphics sourceGraphics))
                {
                    RunnerGraphics graphics = camera.GetComponent<RunnerGraphics>();
                    if (graphics == null) graphics = camera.gameObject.AddComponent<RunnerGraphics>();
                    EditorUtility.CopySerialized(sourceGraphics, graphics);
                }
                Set(gameplay.GetComponentInChildren<CubeDashGame>(), "gameCamera", camera);
                foreach (var canvas in gameplay.GetComponentsInChildren<Canvas>(true))
                    if (canvas.worldCamera == clonedCamera) canvas.worldCamera = camera;
                Object.DestroyImmediate(clonedCamera.gameObject);
                var module = gameplay.GetComponentInChildren<InputSystemUIInputModule>();
                module.actionsAsset = null; module.AssignDefaultActions();
                var track = gameplay.GetComponentInChildren<EndlessTrack>();
                var settings = new SerializedObject(track);
                settings.FindProperty("roadCurveStrength").floatValue = 0;
                settings.ApplyModifiedPropertiesWithoutUndo();
                track.Reset(42, gameplay.GetComponentInChildren<CubeDashGame>().PlayerCubeColor);
                MakeScrollingScenery(environment, gameplay.transform, track);
                Undo.RegisterCreatedObjectUndo(gameplay, "Make Country Side playable");
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("Country Side is playable with Level's player, input, HUD, eight pooled encounter segments, vehicle/power-up presentations, sounds, pause and retry. Two saved countryside route tiles scroll indefinitely; hills, sky and lighting are retained. Level and menu navigation are unchanged.");
            }
            finally { EditorSceneManager.ClosePreviewScene(level); }
        }

        private static void MakeScrollingScenery(GameObject environment, Transform gameplay, EndlessTrack track)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(environment))
                PrefabUtility.UnpackPrefabInstance(environment, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            var tile = new GameObject("Countryside Route Tile 01");
            tile.transform.SetParent(environment.transform, false);
            string[] moving = { "Runner road - clear 8.4 metre corridor", "Rural details - canals fences and farm paths", "Flower fields - linked Idyllic meadows",
                "Village - original Japanese minka and kura", "Roadside shrine and stone lanterns", "Rice paddies - flooded seedling terraces", "Roadside grass bushes rocks and flowers" };
            foreach (string name in moving) environment.transform.Find(name).SetParent(tile.transform, true);
            // Keep the road below the inherited player's feet and wake ribbon, not across the cubes.
            tile.transform.Find(moving[0]).localPosition += Vector3.down * 0.05f;
            Transform treeGroup = environment.transform.Find("Trees - sakura avenue and green woodland");
            var avenue = new GameObject("Sakura avenue"); avenue.transform.SetParent(tile.transform, false);
            foreach (Transform tree in treeGroup.Cast<Transform>().ToArray())
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tree.gameObject).Contains("BlossomTree")) tree.SetParent(avenue.transform, true);
            // These objects move; baked scene batching must not treat them as stationary.
            foreach (Transform item in tile.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(item.gameObject, 0);
            string routePath = CountrySideSceneBuilder.ArtPath + "Prefabs/Playable Countryside Route.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(routePath) != null)
                throw new InvalidOperationException("A saved playable route already exists; inspect it before replacing scenery.");
            GameObject route = PrefabUtility.SaveAsPrefabAssetAndConnect(tile, routePath, InteractionMode.AutomatedAction);
            if (route == null) throw new InvalidOperationException("Could not save the playable countryside route.");
            GameObject second = (GameObject)PrefabUtility.InstantiatePrefab(route, environment.scene);
            second.transform.SetParent(environment.transform, false);
            second.name = "Countryside Route Tile 02"; second.transform.localPosition = Vector3.forward * 284;
            var scenery = gameplay.gameObject.AddComponent<CountrySideRunnerScenery>();
            Set(scenery, "track", track);
            var serializedScenery = new SerializedObject(scenery);
            var tiles = serializedScenery.FindProperty("tiles"); tiles.arraySize = 2;
            tiles.GetArrayElementAtIndex(0).objectReferenceValue = tile.transform;
            tiles.GetArrayElementAtIndex(1).objectReferenceValue = second.transform;
            serializedScenery.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string name, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ApplyAndCaptureFromCommandLine()
        {
            ApplyFromCommandLine(); CountrySideSceneBuilder.CaptureFromCommandLine();
        }

    }
}
