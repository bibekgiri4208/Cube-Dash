using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Updates only player presentation and the menu mascot, preserving the authored environment.</summary>
    public static class PlayerCubePresentationBuilder
    {
        public const string PlayerPath = "Assets/Prefab/CubeDash/PlayerCube.prefab";
        public const string PreviewPath = "Assets/Prefab/CubeDash/PlayerCubeMenuPreview.prefab";
        private const string EyeMaterialPath = "Assets/Material/CubeDashEyesBlack.mat";

        [MenuItem("Tools/Cube Dash/Update Player Eyes and Menu Preview")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes before updating player presentation.");
            PrepareAssets();
            Scene scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            MainMenuController menu = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                MainMenuController candidate = root.GetComponent<MainMenuController>();
                if (candidate != null) menu = candidate;
            }
            if (menu == null || menu.Hero == null) throw new InvalidOperationException("The existing menu mascot reference is required.");
            ReplaceMascot(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the updated menu preview.");
            Debug.Log("Player eyes and jelly idle authored. Main Menu uses a visual-only PlayerCube prefab variant: no gameplay scripts, colliders, vehicle models, or input components. Level scene and menu environment were not rebuilt.");
        }

        public static void ApplyAndCaptureFromCommandLine()
        {
            ApplyFromCommandLine();
            MainMenuSceneBuilder.CaptureFromCommandLine();
        }

        public static void PrepareAssets()
        {
            Material black = AssetDatabase.LoadAssetAtPath<Material>(EyeMaterialPath);
            if (black == null)
            {
                black = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CubeDash Eyes Black", enableInstancing = true };
                black.SetColor("_BaseColor", new Color(0.008f, 0.008f, 0.012f));
                black.SetFloat("_Smoothness", 0.08f); black.SetFloat("_Metallic", 0);
                black.SetColor("_EmissionColor", Color.black); black.DisableKeyword("_EMISSION");
                AssetDatabase.CreateAsset(black, EyeMaterialPath);
            }
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                Transform visual = player.transform.Find("Cube Visual");
                if (visual == null) throw new InvalidOperationException("PlayerCube needs its existing Cube Visual child.");
                Mesh body = visual.GetComponent<MeshFilter>().sharedMesh;
                if (body == null) throw new InvalidOperationException("The player mesh is missing.");
                MeshRenderer[] eyes = { Eye(visual, "Left Eye", -1, body.bounds, black), Eye(visual, "Right Eye", 1, body.bounds, black) };
                PlayerCubeVisual presentation = visual.GetComponent<PlayerCubeVisual>() ?? visual.gameObject.AddComponent<PlayerCubeVisual>();
                var settings = new SerializedObject(presentation);
                SerializedProperty eyeReferences = settings.FindProperty("eyes"); eyeReferences.arraySize = eyes.Length;
                for (int i = 0; i < eyes.Length; i++) eyeReferences.GetArrayElementAtIndex(i).objectReferenceValue = eyes[i];
                settings.FindProperty("autoplayIdle").boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(player, PlayerPath) == null) throw new InvalidOperationException("Could not save the player prefab.");
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            CreatePreviewVariant();
            AssetDatabase.SaveAssetIfDirty(black);
        }

        private static MeshRenderer Eye(Transform visual, string name, int side, Bounds bounds, Material black)
        {
            Transform eye = visual.Find(name);
            if (eye == null)
            {
                eye = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                eye.SetParent(visual, false);
            }
            if (!eye.TryGetComponent(out MeshFilter mesh) || !eye.TryGetComponent(out MeshRenderer renderer))
                throw new InvalidOperationException("An unrelated player object uses the eye's reserved name: " + name);
            mesh.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.sharedMaterial = black;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            eye.localPosition = bounds.center + new Vector3(side * bounds.size.x * 0.193f, bounds.size.y * 0.048f, -bounds.extents.z - 0.012f);
            eye.localRotation = Quaternion.identity;
            eye.localScale = Vector3.Scale(bounds.size, new Vector3(0.117f, 0.262f, 0.024f));
            return renderer;
        }

        private static void CreatePreviewVariant()
        {
            // A variant, not a disconnected copy: the mesh, materials and eyes stay inherited from PlayerCube.
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
                GameObject preview = (GameObject)PrefabUtility.InstantiatePrefab(source, previewScene);
                preview.name = "Player Cube Menu Preview";
                Transform visual = preview.transform.Find("Cube Visual");
                foreach (Component component in preview.GetComponents<Component>())
                    if (!(component is Transform)) Object.DestroyImmediate(component);
                var extras = new List<GameObject>();
                foreach (Transform child in preview.transform) if (child != visual) extras.Add(child.gameObject);
                foreach (GameObject extra in extras) Object.DestroyImmediate(extra);
                foreach (Component component in visual.GetComponentsInChildren<Component>(true))
                    if (!(component is Transform) && !(component is MeshFilter) && !(component is MeshRenderer) && !(component is PlayerCubeVisual))
                        Object.DestroyImmediate(component);
                preview.transform.localPosition = Vector3.zero;
                preview.transform.localRotation = Quaternion.identity;
                preview.transform.localScale = Vector3.one;
                if (PrefabUtility.SaveAsPrefabAsset(preview, PreviewPath) == null) throw new InvalidOperationException("Could not save the visual-only player variant.");
            }
            finally { EditorSceneManager.ClosePreviewScene(previewScene); }
        }

        public static PlayerCubeVisual InstantiatePreview(Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath);
            if (prefab == null) throw new InvalidOperationException("Run Update Player Eyes and Menu Preview first.");
            var preview = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            preview.transform.SetParent(parent, false);
            return preview.GetComponentInChildren<PlayerCubeVisual>();
        }

        private static void ReplaceMascot(MainMenuController menu)
        {
            Renderer oldBody = menu.Hero;
            if (oldBody.GetComponent<PlayerCubeVisual>() != null &&
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(oldBody.gameObject) == PreviewPath) return;
            Transform oldRoot = oldBody.transform.parent;
            if (oldRoot == null || oldRoot.name != "Red cube mascot" || oldRoot.childCount != 3)
                throw new InvalidOperationException("The old mascot was customized; refusing to remove an unrecognized hierarchy.");
            foreach (Transform child in oldRoot)
                if (child != oldBody.transform && child.name != "Black rectangular eye")
                    throw new InvalidOperationException("The old mascot contains additional user objects; remove or relocate those before updating.");
            Transform parent = oldRoot.parent;
            int sibling = oldRoot.GetSiblingIndex();
            Vector3 scale = Vector3.Scale(oldRoot.localScale, oldBody.transform.localScale);
            PlayerCubeVisual preview = InstantiatePreview(parent);
            Transform root = preview.transform.parent;
            root.SetPositionAndRotation(oldBody.transform.position, oldBody.transform.rotation);
            root.localScale = scale; root.SetSiblingIndex(sibling);
            var settings = new SerializedObject(menu);
            settings.FindProperty("hero").objectReferenceValue = preview.Body;
            settings.ApplyModifiedPropertiesWithoutUndo();
            if (Selection.activeGameObject != null && Selection.activeGameObject.transform.IsChildOf(oldRoot))
                Selection.activeGameObject = root.gameObject;
            Object.DestroyImmediate(oldRoot.gameObject);
        }
    }
}
