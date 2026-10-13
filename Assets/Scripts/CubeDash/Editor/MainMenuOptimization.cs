using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CubeDash.Editor
{
    /// <summary>Rendering-only menu optimizations. Never regenerates content or changes shared assets.</summary>
    public static class MainMenuOptimization
    {
        [MenuItem("Tools/Cube Dash/Optimize Main Menu Without Quality Changes")]
        public static void ApplyFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before optimizing the menu.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before optimizing the menu.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save scene edits before optimizing the menu.");
            Scene scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            int batched = ApplyToScene(scene, true);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the optimized menu.");
            Debug.Log($"Menu optimized: {batched} rigid renderers marked for static batching. Geometry, materials, foliage, lighting, effects and gameplay preserved.");
        }

        public static int ApplyToScene(Scene scene, bool recordUndo = false)
        {
            if (scene.path != MainMenuSceneBuilder.ScenePath) return 0;
            MainMenuController menu = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).SingleOrDefault();
            if (menu == null) return 0;
            Transform world = menu.transform.Find("Parking Lot Diorama");
            if (world == null) return 0;
            int changed = 0;
            foreach (MeshRenderer renderer in world.GetComponentsInChildren<MeshRenderer>())
            {
                if (!CanBatch(renderer, world)) continue;
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                if ((flags & StaticEditorFlags.BatchingStatic) != 0) continue;
                if (recordUndo) Undo.RecordObject(renderer.gameObject, "Batch stationary menu scenery");
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags | StaticEditorFlags.BatchingStatic);
                if (recordUndo && PrefabUtility.IsPartOfPrefabInstance(renderer.gameObject))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);
                changed++;
            }

            Camera[] cameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).ToArray();
            // Only opt out transiently. Saving Off into the source scene could break scene-color
            // shaders added later; explicit author overrides and extra/custom cameras are preserved.
            if (!recordUndo && cameras.Length == 1 && cameras[0].TryGetComponent(out UniversalAdditionalCameraData data)
                && data.requiresColorOption == CameraOverrideOption.UsePipelineSettings
                && UsesKnownRenderer(data) && CanDisableOpaqueTexture(scene))
            {
                data.requiresColorOption = CameraOverrideOption.Off;
            }
            if (recordUndo && changed > 0) EditorSceneManager.MarkSceneDirty(scene);
            return changed;
        }

        private static bool CanBatch(MeshRenderer renderer, Transform world)
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.HasPropertyBlock()
                || !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) return false;
            // Animation on an ancestor also moves its otherwise script-free children (e.g. the mascot's eyes).
            for (Transform item = renderer.transform; item != null; item = item.parent)
            {
                if (item.GetComponents<MonoBehaviour>().Any(script => item != world.parent || !(script is MainMenuController))
                    || item.GetComponent<Animator>() != null || item.GetComponent<Animation>() != null
                    || item.GetComponent<Rigidbody>() != null || item.GetComponent<Rigidbody2D>() != null
                    || item.GetComponent<LODGroup>() != null) return false;
            }
            Material[] materials = renderer.sharedMaterials;
            return materials.Length > 0 && materials.All(material => material != null && material.shader != null
                && IsRigidShader(material.shader.name)
                && !string.Equals(material.GetTag("DisableBatching", false, "False"), "True", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsRigidShader(string name) => name == "Universal Render Pipeline/Lit"
            || name == "Universal Render Pipeline/Unlit" || name == "CubeDash/Menu Surface";

        private static bool UsesKnownRenderer(UniversalAdditionalCameraData data)
        {
            // The existing SSAO and runner-only blur are safe; new/custom features need another audit.
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return false;
            // Inspect serialized references without constructing a renderer during scene/build processing.
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            int index = new SerializedObject(data).FindProperty("m_RendererIndex").intValue;
            if (index < 0) index = serialized.FindProperty("m_DefaultRendererIndex").intValue;
            if (list == null || index < 0 || index >= list.arraySize) return false;
            var asset = list.GetArrayElementAtIndex(index).objectReferenceValue as UniversalRendererData;
            string path = AssetDatabase.GetAssetPath(asset);
            return asset != null && (path == "Assets/Settings/PC_Renderer.asset" || path == "Assets/Settings/Mobile_Renderer.asset")
                && asset.rendererFeatures.All(feature => feature != null
                    && (feature.GetType().FullName == "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion" || feature is RunnerMotionBlurFeature));
        }

        private static bool CanDisableOpaqueTexture(Scene scene)
        {
            var shaders = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>())
                .Where(renderer => renderer.enabled).SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null).Select(material => material.shader).ToList();
            // Include hidden modal UI too: opening a panel must not reveal a scene-color dependency.
            shaders.AddRange(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Graphic>(true))
                .Select(graphic => graphic.material).Where(material => material != null).Select(material => material.shader));
            if (RenderSettings.skybox != null) shaders.Add(RenderSettings.skybox.shader);
            foreach (Shader shader in shaders.Distinct())
            {
                if (shader == null) return false;
                if (shader.name == "Universal Render Pipeline/Lit" || shader.name == "Universal Render Pipeline/Unlit"
                    || shader.name == "UI/Default" || shader.name == "GUI/Text Shader"
                    || shader.name == "Skybox/6 Sided" || shader.name == "Skybox/Cubemap"
                    || shader.name == "Skybox/Procedural" || shader.name == "Skybox/Panoramic") continue;
                string path = AssetDatabase.GetAssetPath(shader);
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                foreach (string dependency in AssetDatabase.GetDependencies(path))
                {
                    string extension = Path.GetExtension(dependency);
                    if (extension != ".shader" && extension != ".hlsl" && extension != ".shadergraph"
                        && extension != ".shadersubgraph") continue;
                    if (!File.Exists(dependency)) return false;
                    string source = File.ReadAllText(dependency);
                    if (source.Contains("_CameraOpaqueTexture") || source.Contains("SceneColorNode")
                        || source.Contains("SampleSceneColor") || source.Contains("DeclareOpaqueTexture")) return false;
                    // Unresolved custom includes/functions require a manual audit, not an assumption.
                    if (source.Contains("CustomFunctionNode") && !UsesAuditedCustomLighting(dependency, source)) return false;
                    if (source.Contains("#include") && !IsRigidShader(shader.name)) return false;
                }
            }
            return true;
        }

        private static bool UsesAuditedCustomLighting(string path, string source)
        {
            if (path != "Assets/Idyllic Fantasy Nature/Shader/Vegetation.shadergraph"
                && path != MainMenuSakuraBlossoms.ShaderPath) return false;
            // Both existing graphs have exactly one file-backed MainLight function. Its HLSL is
            // scanned through GetDependencies above; unfamiliar custom functions keep the color copy.
            return Regex.Matches(source, "CustomFunctionNode").Count == 1
                && Regex.Matches(source, "\"m_FunctionSource\": \"ed520734c9dbe4349b81aa7056870bdd\"").Count == 1;
        }
    }

    /// <summary>Optimize only the transient menu build scene; source scenes and shared pipelines are untouched.</summary>
    public sealed class MainMenuBuildOptimization : IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (BuildPipeline.isBuildingPlayer) MainMenuOptimization.ApplyToScene(scene);
        }
    }
}
