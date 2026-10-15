using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Persists compact obstacle art in the existing pooled prefabs and saved gameplay scenes.</summary>
    public static class HighwayObstacleEditorVisuals
    {
        public const string PreviewPath = HighwayHazardArtBuilder.ArtPath + "/Saved Previews";
        public static readonly string[] ScenePaths = {
            "Assets/Scenes/Level.unity", "Assets/Scenes/City.unity", "Assets/Scenes/Jungle.unity",
            "Assets/Scenes/Desert.unity", "Assets/Scenes/Beach.unity", "Assets/Scenes/Country Side.unity"
        };
        private const string ObstaclePath = "Assets/Prefab/CubeDash/ObstacleCube.prefab";
        private const string TrackPath = "Assets/Prefab/CubeDash/TrackSegment.prefab";

        [MenuItem("Tools/Cube Dash/Apply Saved Obstacle Models (Edit Mode)")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before applying saved obstacle visuals.");
            var art = Resources.Load<HighwayHazardVisuals>(HighwayHazardVisuals.ResourcePath);
            if (art == null || art.MeshFor(HighwayHazard.Cube) == null) throw new InvalidOperationException("Bake detailed highway obstacle art first.");
            if (!AssetDatabase.IsValidFolder(PreviewPath)) AssetDatabase.CreateFolder(HighwayHazardArtBuilder.ArtPath, "Saved Previews");
            var meshes = new Mesh[art.VariantCount(HighwayHazard.Cube)];
            for (int variant = 0; variant < meshes.Length; variant++)
            {
                Mesh mesh = Object.Instantiate(art.MeshFor(HighwayHazard.Cube, variant));
                mesh.name = art.MeshFor(HighwayHazard.Cube, variant).name + " (saved slot)";
                // The prefab's original 1.15 scale and centered 1 m collider are deliberately untouched.
                mesh.vertices = mesh.vertices.Select(p => (p - Vector3.up * 0.575f) / 1.15f).ToArray();
                mesh.RecalculateBounds();
                meshes[variant] = Save(mesh, PreviewPath + "/" + mesh.name + ".asset");
            }
            var palettes = new Dictionary<Color, Material[]>();
            Material[] Palette(RunnerCube slot)
            {
                Material original = slot.GetComponent<Renderer>().sharedMaterial;
                Color color = original.GetColor("_BaseColor");
                if (palettes.TryGetValue(color, out Material[] palette)) return palette;
                palette = (Material[])art.Materials.Clone();
                string id = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original));
                // Use the original material GUID for custom tints, avoiding rounded-color filename collisions.
                string label = original.name.StartsWith("Traffic Editor Paint ") ? original.name.Substring("Traffic Editor Paint ".Length) : id;
                var paint = new Material(palette[0]) { name = "Traffic Editor Paint " + label, enableInstancing = true };
                paint.SetColor("_BaseColor", color);
                palette[0] = Save(paint, PreviewPath + "/" + paint.name + ".mat");
                palettes.Add(color, palette); return palette;
            }
            void Apply(RunnerCube slot, int variant)
            {
                if (Vector3.Distance(slot.transform.lossyScale, Vector3.one * 1.15f) > 0.001f)
                    throw new InvalidOperationException("Unexpected obstacle scale; inspect manually: " + slot.name);
                MeshFilter filter = slot.GetComponent<MeshFilter>(); Renderer renderer = slot.GetComponent<Renderer>();
                Material[] palette = Palette(slot);
                filter.sharedMesh = meshes[variant % meshes.Length]; renderer.sharedMaterials = palette;
                EditorUtility.SetDirty(filter); EditorUtility.SetDirty(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            foreach (string path in new[] { ObstaclePath, TrackPath })
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (path == ObstaclePath) Apply(root.GetComponent<RunnerCube>(), 0);
                    else
                    {
                        RunnerCube[] slots = root.GetComponent<TrackSegment>().Cubes;
                        for (int i = 0; i < slots.Length; i++) Apply(slots[i], i % meshes.Length);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in ScenePaths)
                {
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CubeDashGame>(true)).Single();
                    foreach (TrackSegment segment in game.Track.Segments)
                        for (int i = 0; i < segment.Cubes.Length; i++) Apply(segment.Cubes[i], i % meshes.Length);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Could not save obstacle visuals: " + path);
                }
            }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
            AssetDatabase.SaveAssets();
            Debug.Log("Saved obstacle previews in both pooled prefabs and all seven gameplay scenes. Only obstacle mesh/material references changed; player, coins, transforms, colliders and gameplay settings preserved.");
        }

        private static T Save<T>(T value, string path) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(value); return existing;
        }
    }
}
