using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CubeDash.Editor
{
    /// <summary>Keeps CPU/rigid batching optimizations while preserving the stable native foliage rendering path.</summary>
    public static class CountrySideOptimization
    {
        [MenuItem("Tools/Cube Dash/Optimize Country Side Without Quality Changes")]
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
            ApplyToScene(scene); AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Country Side.");
        }
        public static void ApplyToScene(Scene scene)
        {
            if (scene.path != CountrySideSceneBuilder.ScenePath) throw new InvalidOperationException("Only Country Side can be optimized by this tool.");
            var scenery = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CountrySideRunnerScenery>(true)).Single();
            string[] shaders = { "Idyllic Fantasy Nature/Surface", "Idyllic Fantasy Nature/Vegetation" };
            Material[] native = scenery.CurvePlants.SelectMany(plant => plant.GetComponentsInChildren<MeshRenderer>(true))
                .SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Distinct().ToArray();
            var templates = new Material[shaders.Length];
            for (int i = 0; i < shaders.Length; i++)
            {
                Material source = native.First(material => material.shader.name == shaders[i]);
                string path = CountrySideSceneBuilder.ArtPath + "Materials/Retained " + (i == 0 ? "Surface" : "Vegetation") + " Instancing.mat";
                templates[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (templates[i] == null)
                {
                    templates[i] = new Material(source) { name = "Retained countryside " + (i == 0 ? "surface" : "vegetation") + " instancing", enableInstancing = true };
                    AssetDatabase.CreateAsset(templates[i],path);
                }
            }
            var data = new SerializedObject(scenery); var list = data.FindProperty("plantInstancingTemplates");
            var instancing = data.FindProperty("instanceNativePlants");
            bool changed = list.arraySize != templates.Length || instancing.boolValue;
            instancing.boolValue = false;
            list.arraySize = templates.Length;
            for (int i = 0; i < templates.Length; i++)
            {
                changed |= list.GetArrayElementAtIndex(i).objectReferenceValue != templates[i];
                list.GetArrayElementAtIndex(i).objectReferenceValue = templates[i];
            }
            if (changed) { data.ApplyModifiedPropertiesWithoutUndo(); EditorSceneManager.MarkSceneDirty(scene); }
        }
    }
}
