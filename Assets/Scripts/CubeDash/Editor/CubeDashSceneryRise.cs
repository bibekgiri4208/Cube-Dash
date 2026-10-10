using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    public static class CubeDashSceneryRise
    {
        [MenuItem("Tools/Cube Dash/Apply Staggered Scenery Rise")]
        public static void ApplyFromCommandLine()
        {
            const string path = "Assets/Prefab/CubeDash/TrackSegment.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var renderers = new List<Renderer>(); var meshes = new HashSet<Mesh>();
                SegmentEnvironment environment = root.GetComponent<SegmentEnvironment>();
                foreach (GameObject biome in environment.Biomes)
                {
                    foreach (Renderer renderer in biome.GetComponentsInChildren<Renderer>(true))
                        if (renderer.name == "Landscape Details" || renderer.name.StartsWith("Boundary ", StringComparison.Ordinal)
                            || renderer.name == "Coastal Headland")
                        {
                            renderers.Add(renderer);
                            Mesh mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                            if (mesh != null) meshes.Add(mesh);
                        }
                    BiomeScenery scenery = biome.GetComponent<BiomeScenery>();
                    if (scenery != null) foreach (Mesh mesh in scenery.Layouts) meshes.Add(mesh);
                }
                foreach (Mesh mesh in meshes) BakeAnchors(mesh);
                AssetDatabase.SaveAssets();
                SceneryRise rise = root.GetComponent<SceneryRise>();
                if (rise == null) rise = root.AddComponent<SceneryRise>();
                var settings = new SerializedObject(rise);
                var sources = settings.FindProperty("scenery"); sources.arraySize = renderers.Count;
                for (int i = 0; i < renderers.Count; i++) sources.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                var buildings = new List<Transform>();
                foreach (Transform child in environment.Biomes[0].transform) if (child.name.Contains("Skyscraper")) buildings.Add(child);
                var refs = settings.FindProperty("buildings"); refs.arraySize = buildings.Count;
                var homes = settings.FindProperty("buildingHomes"); homes.arraySize = buildings.Count;
                for (int i = 0; i < buildings.Count; i++)
                {
                    refs.GetArrayElementAtIndex(i).objectReferenceValue = buildings[i];
                    homes.GetArrayElementAtIndex(i).vector3Value = buildings[i].localPosition;
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path); AssetDatabase.SaveAssets();
                Debug.Log("Staggered scenery rise saved: whole connected objects rise from below, tree parts share anchors, buildings rise together. Ground, water, road and gameplay are untouched; shared run clock freezes animation on pause.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BakeAnchors(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices; int[] parent = new int[vertices.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Root(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
            void Join(int a, int b) { a = Root(a); b = Root(b); if (a != b) parent[b] = a; }
            var positions = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3Int key = Vector3Int.RoundToInt(vertices[i] * 10000);
                if (positions.TryGetValue(key, out int other)) Join(i, other); else positions[key] = i;
            }
            for (int surface = 0; surface < mesh.subMeshCount; surface++)
            {
                int[] triangles = mesh.GetTriangles(surface);
                for (int i = 0; i < triangles.Length; i += 3) { Join(triangles[i], triangles[i + 1]); Join(triangles[i], triangles[i + 2]); }
            }
            var bounds = new Dictionary<int, Bounds>(); var materials = new Dictionary<int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                int group = Root(i);
                if (!bounds.TryGetValue(group, out Bounds b)) b = new Bounds(vertices[i], Vector3.zero);
                b.Encapsulate(vertices[i]); bounds[group] = b;
            }
            for (int surface = 0; surface < mesh.subMeshCount; surface++)
                foreach (int index in mesh.GetTriangles(surface)) materials[Root(index)] = surface;
            var stems = new List<int>(); var mountains = new List<int>();
            foreach (var item in bounds)
            {
                if (!materials.TryGetValue(item.Key, out int material)) continue;
                if ((material == 0 || material == 15) && item.Value.size.y > 4 && item.Value.size.x < item.Value.size.y * 0.4f) stems.Add(item.Key);
                if (material == 3 && item.Value.size.y > 12 && item.Value.size.x > 12) mountains.Add(item.Key);
            }
            var anchors = new Dictionary<int, Vector2>();
            foreach (var item in bounds)
            {
                Vector3 center = item.Value.center;
                Vector3 originalCenter = center;
                int material = materials.TryGetValue(item.Key, out int m) ? m : -1;
                var candidates = material == 4 ? mountains : stems;
                bool treePart = material == 0 || material == 1 || material == 2 || material == 15 || material == 16 || material == 17;
                float closest = float.MaxValue;
                if (treePart || material == 4)
                    foreach (int candidate in candidates)
                    {
                        Bounds target = bounds[candidate];
                        float distance = Vector2.Distance(new Vector2(originalCenter.x, originalCenter.z), new Vector2(target.center.x, target.center.z));
                        bool attaches = material == 4 ? distance < target.size.x * 0.6f
                            : (material == 0 || material == 15) ? distance < target.size.y * 0.2f
                            : originalCenter.y > target.min.y + target.size.y * 0.3f && distance < target.size.y * 0.6f;
                        if (attaches && distance < closest) { closest = distance; center = new Vector3(target.center.x, item.Value.center.y, target.center.z); }
                    }
                anchors[item.Key] = new Vector2(center.x, center.z);
            }
            var uv = new List<Vector2>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++) uv.Add(anchors[Root(i)]);
            mesh.SetUVs(2, uv);
            mesh.RecalculateBounds(); Bounds expanded = mesh.bounds;
            expanded.Encapsulate(new Vector3(expanded.center.x, expanded.min.y - 80, expanded.center.z)); mesh.bounds = expanded;
            EditorUtility.SetDirty(mesh);
        }
    }
}
