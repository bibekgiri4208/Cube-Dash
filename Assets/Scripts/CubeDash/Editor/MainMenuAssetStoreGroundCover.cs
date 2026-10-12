using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Replaces rounded shrub meshes and grass only, preserving authored trees and scene settings.</summary>
    public static class MainMenuAssetStoreGroundCover
    {
        public const string RootName = "Idyllic Fantasy Nature Bushes and Grass";
        private static readonly string[] Bushes = { "Bush_01_01", "Bush_02_01", "Bush_03_01", "Bush_01_02", "Bush_02_02", "Bush_03_02" };
        private static readonly string[] Grasses = { "Grass_01", "Grass_02", "Grass_03" };

        [MenuItem("Tools/Cube Dash/Use Idyllic Fantasy Nature Menu Bushes and Grass")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before replacing plants.");
            Scene scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the menu plants.");
        }

        public static void ApplyAndCaptureFromCommandLine()
        {
            ApplyFromCommandLine(); MainMenuSceneBuilder.CaptureFromCommandLine();
        }

        public static void ApplyToScene(Scene scene)
        {
            MainMenuController menu = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.TryGetComponent(out MainMenuController candidate)) menu = candidate;
            Transform garden = menu != null ? menu.transform.Find("Parking Lot Diorama/" + MainMenuJapaneseGarden.RootName) : null;
            if (garden == null) throw new InvalidOperationException("The existing Japanese menu garden is required.");
            if (garden.Find(RootName) != null)
            {
                Debug.Log("Asset Store menu ground cover is already present. Existing plant edits were preserved."); return;
            }
            var originals = new List<MeshRenderer>();
            foreach (MeshRenderer renderer in garden.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || !PrefabUtility.IsAnyPrefabInstanceRoot(renderer.gameObject)) continue;
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject);
                if (path == MainMenuJapaneseGarden.ArtPath + "Prefabs/Azalea Shrub.prefab" ||
                    path == MainMenuJapaneseGarden.ArtPath + "Prefabs/Susuki Grass.prefab" ||
                    path == MainMenuJapaneseGarden.ArtPath + "Prefabs/Roadside Grass.prefab") originals.Add(renderer);
            }
            if (originals.Count == 0) throw new InvalidOperationException("No active original shrubs or grass were found.");
            foreach (string name in Bushes) ValidatePrefab(name);
            foreach (string name in Grasses) ValidatePrefab(name);
            Transform plants = new GameObject(RootName).transform;
            plants.SetParent(garden, false); Undo.RegisterCreatedObjectUndo(plants.gameObject, "Replace menu ground cover");
            int bushes = 0, grasses = 0;
            foreach (MeshRenderer original in originals)
            {
                bool bush = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(original.gameObject).EndsWith("/Azalea Shrub.prefab", StringComparison.Ordinal);
                string name = bush ? Bushes[bushes++ % Bushes.Length] : Grasses[grasses++ % Grasses.Length];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuAssetStoreTrees.PrefabPath + name + ".prefab");
                Bounds nativeBounds = MainMenuAssetStoreTrees.BoundsOf(prefab);
                Bounds oldBounds = original.GetComponent<MeshFilter>().sharedMesh.bounds;
                // Uniform scaling keeps the leafy silhouettes natural, within the original planting footprint.
                float widthFit = Mathf.Max(oldBounds.size.x, oldBounds.size.z) * 1.10f / Mathf.Max(nativeBounds.size.x, nativeBounds.size.z);
                float heightFit = oldBounds.size.y * (bush ? 1.40f : 1f) / nativeBounds.size.y;
                float fit = Mathf.Min(widthFit, heightFit);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(plants, false);
                instance.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
                Vector3 sourceScale = original.transform.lossyScale, parentScale = plants.lossyScale;
                instance.transform.localScale = new Vector3(sourceScale.x / parentScale.x, sourceScale.y / parentScale.y, sourceScale.z / parentScale.z) * fit;
                instance.transform.position -= instance.transform.up * (nativeBounds.min.y * instance.transform.lossyScale.y);
                // Bush prefabs include demo interaction triggers and leaf particles; keep only their visual LODs.
                Transform trigger = instance.transform.Find("LeafTrigger");
                if (trigger != null) Object.DestroyImmediate(trigger.gameObject);
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                Undo.RecordObject(original, "Hide previous plant mesh"); original.enabled = false;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Replaced {bushes} round menu shrubs and {grasses} grass tufts with linked Idyllic Fantasy Nature prefabs. Trees, planters, fallen petals, lighting, vehicles, player and UI were preserved.");
        }

        private static void ValidatePrefab(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuAssetStoreTrees.PrefabPath + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing downloaded plant prefab: " + name);
            Transform trigger = prefab.transform.Find("LeafTrigger");
            if (trigger != null && trigger.GetComponentsInChildren<MeshFilter>(true).Length != 0)
                throw new InvalidOperationException("The interaction trigger unexpectedly contains plant geometry: " + name);
            foreach (MonoBehaviour script in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                if (script == null || trigger == null || !script.transform.IsChildOf(trigger) || script.GetType().FullName != "IdyllicFantasyNature.BushLeafTrigger")
                    throw new InvalidOperationException("Unexpected plant script; inspect the prefab before replacing foliage: " + name);
            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
                if (trigger == null || !collider.transform.IsChildOf(trigger)) throw new InvalidOperationException("Unexpected plant collider: " + name);
            foreach (ParticleSystem particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
                if (trigger == null || !particles.transform.IsChildOf(trigger)) throw new InvalidOperationException("Unexpected plant particles: " + name);
            Bounds bounds = MainMenuAssetStoreTrees.BoundsOf(prefab);
            if (bounds.size.y <= 0 || Mathf.Max(bounds.size.x, bounds.size.z) <= 0)
                throw new InvalidOperationException("The plant has no valid mesh bounds: " + name);
        }
    }
}
