using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Replaces only menu tree visuals with linked Idyllic Fantasy Nature prefab instances.</summary>
    public static class MainMenuAssetStoreTrees
    {
        public const string PrefabPath = "Assets/Idyllic Fantasy Nature/Prefabs/";
        public const string RootName = "Idyllic Fantasy Nature Trees";
        private static readonly string[] GreenTrees = { "BroadleafTree_01_Green", "Fir_02", "BroadleafTree_03_Green",
            "BroadleafTree_04_Green", "Fir_04", "BroadleafTree_02_Green", "BroadleafTree_05_Green", "Fir_01",
            "BroadleafTree_03_Green", "BroadleafTree_01_Green", "Fir_03" };
        private static readonly string[] BlossomTrees = { "BlossomTree_01", "BlossomTree_03", "BlossomTree_05" };

        [MenuItem("Tools/Cube Dash/Use Idyllic Fantasy Nature Menu Trees")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before replacing trees.");
            Scene scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the menu trees.");
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
                Debug.Log("Asset Store menu trees are already present. Existing tree edits were preserved."); return;
            }

            var originals = new List<Transform>();
            foreach (Transform tree in garden.GetComponentsInChildren<Transform>())
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(tree.gameObject)) continue;
                if (!tree.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled) continue;
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tree.gameObject);
                if (path == MainMenuJapaneseGarden.ArtPath + "Prefabs/Japanese Pine.prefab" ||
                    path == MainMenuJapaneseGarden.ArtPath + "Prefabs/Sakura Tree.prefab") originals.Add(tree);
            }
            if (originals.Count == 0) throw new InvalidOperationException("No active original menu tree visuals were found.");
            // Validate all dependencies before changing the authored scene.
            foreach (string name in GreenTrees) ValidatePrefab(name);
            foreach (string name in BlossomTrees) ValidatePrefab(name);
            Transform trees = new GameObject(RootName).transform;
            trees.SetParent(garden, false); Undo.RegisterCreatedObjectUndo(trees.gameObject, "Replace menu trees");
            int green = 0, blossom = 0;
            foreach (Transform original in originals)
            {
                bool sakura = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(original.gameObject).EndsWith("/Sakura Tree.prefab", StringComparison.Ordinal);
                string name = sakura ? BlossomTrees[blossom++ % BlossomTrees.Length] : GreenTrees[green++ % GreenTrees.Length];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + name + ".prefab");
                Bounds nativeBounds = BoundsOf(prefab);
                Bounds oldBounds = original.GetComponent<MeshFilter>().sharedMesh.bounds;
                float heightFit = oldBounds.size.y / nativeBounds.size.y;
                float widthFit = Mathf.Max(oldBounds.size.x, oldBounds.size.z) * 1.10f / Mathf.Max(nativeBounds.size.x, nativeBounds.size.z);
                float fit = Mathf.Min(heightFit, widthFit);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(trees, false);
                instance.transform.SetPositionAndRotation(original.position, original.rotation);
                Vector3 sourceScale = original.lossyScale, parentScale = trees.lossyScale;
                instance.transform.localScale = new Vector3(sourceScale.x / parentScale.x, sourceScale.y / parentScale.y, sourceScale.z / parentScale.z) * fit;
                instance.transform.position -= instance.transform.up * (nativeBounds.min.y * instance.transform.lossyScale.y);
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0); // Keep shader-driven foliage out of static batching.
                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                // Disable only the old renderer, not its root: the sakura planter/grass children stay intact.
                MeshRenderer oldRenderer = original.GetComponent<MeshRenderer>();
                Undo.RecordObject(oldRenderer, "Hide previous tree mesh"); oldRenderer.enabled = false;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Replaced {green} green menu trees and {blossom} sakura trees with linked Idyllic Fantasy Nature prefabs. Source prefabs/meshes/materials, shrubs/grass, planters, petals, lighting, camera, UI and gameplay were not changed.");
        }

        private static void ValidatePrefab(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing downloaded tree prefab: " + name);
            if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("A tree prefab unexpectedly contains scripts; inspect it before using it in the menu: " + name);
            Bounds bounds = BoundsOf(prefab);
            if (bounds.size.y <= 0 || Mathf.Max(bounds.size.x, bounds.size.z) <= 0)
                throw new InvalidOperationException("The tree has no valid mesh bounds: " + name);
        }

        internal static Bounds BoundsOf(GameObject prefab)
        {
            LODGroup lod = prefab.GetComponent<LODGroup>();
            Renderer[] renderers = lod != null && lod.GetLODs().Length > 0 ? lod.GetLODs()[0].renderers : prefab.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds(); bool first = true;
            foreach (Renderer renderer in renderers)
            {
                Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                Bounds local = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 sign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    Vector3 point = toRoot.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, sign));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }
    }
}
