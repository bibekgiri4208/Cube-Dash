using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    public static partial class CubeDashEnvironments
    {
        [MenuItem("Tools/Cube Dash/Blend Environment Boundaries")]
        public static void BlendBoundariesFromCommandLine()
        {
            InitializeModels();
            try
            {
                string[] names = { "Bark", "Deep Foliage", "Sunlit Foliage", "Mountain Slate", "Snow", "Golden Sand", "Sandstone",
                    "Forest Floor", "Shore Foam", "Beach Timber", "Roof Thatch", "Facade Glass", "Ivory Trim", "Rock Moss", "Dry Grass",
                    "Jungle Bark", "Jungle Foliage", "Jungle Canopy Highlights" };
                Material[] palette = new Material[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    palette[i] = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/" + names[i] + ".mat");
                    if (palette[i] == null) throw new InvalidOperationException("Missing material: " + names[i]);
                }
                bark = palette[0]; leaf = palette[1]; lightLeaf = palette[2]; stone = palette[3]; snow = palette[4];
                sand = palette[5]; redStone = palette[6]; grass = palette[7]; wood = palette[9];
                jungleBark = palette[15]; jungleLeaf = palette[16]; jungleHighlight = palette[17];

                const string path = "Assets/Prefab/CubeDash/TrackSegment.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    SegmentEnvironment environment = root.GetComponent<SegmentEnvironment>();
                    BiomeBoundaryBlend component = root.GetComponent<BiomeBoundaryBlend>();
                    if (component == null) component = root.AddComponent<BiomeBoundaryBlend>();
                    var serialized = new SerializedObject(component);
                    var buildings = new List<Transform>();
                    foreach (Transform child in environment.Biomes[0].transform)
                        if (child.name.Contains("Skyscraper")) buildings.Add(child);
                    var buildingRefs = serialized.FindProperty("cityBuildings"); buildingRefs.arraySize = buildings.Count;
                    var buildingScales = serialized.FindProperty("cityBuildingScales"); buildingScales.arraySize = buildings.Count;
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        buildingRefs.GetArrayElementAtIndex(i).objectReferenceValue = buildings[i];
                        buildingScales.GetArrayElementAtIndex(i).vector3Value = buildings[i].localScale;
                    }
                    var regions = serialized.FindProperty("regions"); regions.arraySize = BiomeRules.Count;
                    for (int biome = 0; biome < BiomeRules.Count; biome++)
                    {
                        Transform region = environment.Biomes[biome].transform;
                        Transform floor = region.Find(biome == 0 ? "City Ground" : "Landscape Ground");
                        if (biome == 0) ConfigureCityFloor(region, floor);
                        var grounds = new List<Renderer>(); var waters = new List<Renderer>();
                        var normalMeshes = new List<Mesh>(); var boundaryMeshes = new List<Mesh>();
                        foreach (Renderer renderer in region.GetComponentsInChildren<Renderer>(true))
                        {
                            if (renderer.name == "City Ground" || renderer.name == "Landscape Ground" || renderer.name == "Ground Apron" || renderer.name == "Shoreline")
                            {
                                grounds.Add(renderer);
                                // Enough longitudinal rows to raise underwater floors gently into land.
                                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                                normalMeshes.Add(filter.sharedMesh);
                                Mesh transitionMesh = filter.sharedMesh;
                                if ((biome == 1 || biome == 4) && renderer.name != "Shoreline")
                                    transitionMesh = BoundaryFloor(filter.sharedMesh, biome == 1 ? "Jungle Transition Floor" : "Beach Transition Floor");
                                boundaryMeshes.Add(transitionMesh);
                            }
                            if (renderer.name == "River" || renderer.name == "River Apron") waters.Add(renderer);
                        }
                        var item = regions.GetArrayElementAtIndex(biome);
                        AssignRenderers(item.FindPropertyRelative("Ground"), grounds);
                        AssignRenderers(item.FindPropertyRelative("Water"), waters);
                        var originals = item.FindPropertyRelative("NormalGround"); originals.arraySize = normalMeshes.Count;
                        var transitions = item.FindPropertyRelative("BoundaryGround"); transitions.arraySize = boundaryMeshes.Count;
                        for (int i = 0; i < normalMeshes.Count; i++)
                        {
                            originals.GetArrayElementAtIndex(i).objectReferenceValue = normalMeshes[i];
                            transitions.GetArrayElementAtIndex(i).objectReferenceValue = boundaryMeshes[i];
                        }
                        item.FindPropertyRelative("Floor").objectReferenceValue = floor.GetComponent<Renderer>().sharedMaterial;
                        for (int edge = 0; edge < 2; edge++)
                        {
                            bool entrance = edge == 0;
                            int other = (biome + (entrance ? 4 : 1)) % 5;
                            string name = entrance ? "Boundary Entrance Scenery" : "Boundary Exit Scenery";
                            Mesh mesh = SaveMesh(BoundaryDressing((EnvironmentBiome)biome, (EnvironmentBiome)other, entrance, palette),
                                ((EnvironmentBiome)biome) + (entrance ? " Entrance Blend" : " Exit Blend"));
                            Transform child = region.Find(name);
                            if (child == null) child = Part(region, name, mesh, palette, Vector3.zero, Vector3.one).transform;
                            child.GetComponent<MeshFilter>().sharedMesh = mesh; child.GetComponent<Renderer>().sharedMaterials = palette;
                            child.gameObject.SetActive(false);
                            item.FindPropertyRelative(entrance ? "EntranceDressing" : "ExitDressing").objectReferenceValue = child.gameObject;
                        }
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                AssetDatabase.SaveAssets();
                Debug.Log("All five biome joins saved: paired 84m ground/texture blends, mixed native vegetation and rock dressing, river end pools and raised shoreline beds. Runtime only selects saved scenery and per-instance property blocks.");
            }
            finally { ReleaseModels(); }
        }

        private static void ConfigureCityFloor(Transform city, Transform floor)
        {
            const string path = Materials + "/City Transition Ground.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Material source = floor.GetComponent<Renderer>().sharedMaterial;
                material = new Material(Shader.Find("CubeDash/Environment Surface")) { name = "City Transition Ground", enableInstancing = true };
                Color color = source.GetColor("_BaseColor");
                material.SetColor("_BaseColor", color); material.SetColor("_SecondaryColor", color * 0.94f);
                material.SetFloat("_DetailType", 0); material.SetFloat("_DetailScale", 0.7f); material.SetFloat("_Smoothness", 0.12f);
                AssetDatabase.CreateAsset(material, path);
            }
            floor.GetComponent<Renderer>().sharedMaterial = material;
            foreach (string edge in new[] { "Rear Horizon", "Forward Horizon" })
                city.Find(edge + "/Ground Apron").GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void AssignRenderers(SerializedProperty property, List<Renderer> renderers)
        {
            property.arraySize = renderers.Count;
            for (int i = 0; i < renderers.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        }

        private static Mesh BoundaryFloor(Mesh source, string name)
        {
            string path = Meshes + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            Vector3[] original = source.vertices;
            var firstRow = new List<Vector3>();
            foreach (Vector3 vertex in original) if (Mathf.Abs(vertex.z + 0.5f) < 0.001f) firstRow.Add(vertex);
            if (firstRow.Count < 2) throw new InvalidOperationException("Expected a normalized floor cross section: " + source.name);
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int row = 0; row <= 42; row++)
                foreach (Vector3 vertex in firstRow) vertices.Add(new Vector3(vertex.x, vertex.y, row / 42f - 0.5f));
            for (int row = 0; row < 42; row++)
                for (int col = 0; col < firstRow.Count - 1; col++)
                {
                    int a = row * firstRow.Count + col, b = a + firstRow.Count;
                    triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            Mesh result = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            result.SetVertices(vertices); result.SetTriangles(triangles, 0); result.RecalculateNormals(); result.RecalculateBounds();
            return SaveMesh(result, name);
        }

        private static Mesh BoundaryDressing(EnvironmentBiome biome, EnvironmentBiome other, bool entrance, Material[] palette)
        {
            var geometry = new Geometry(palette);
            int from = entrance ? (int)other : (int)biome;
            var random = new System.Random(5929 + from * 149 + (entrance ? 1 : 0));
            // Foreign species appear progressively toward the join, on both halves of the boundary.
            for (int row = 0; row < 6; row++)
            {
                float z = 3.5f + row * 7;
                float proximity = entrance ? 1 - z / 42 : z / 42;
                for (int side = -1; side <= 1; side += 2)
                {
                    if ((biome == EnvironmentBiome.Beach || other == EnvironmentBiome.Beach) && side > 0) continue;
                    if (random.NextDouble() > 0.15f + proximity * 0.75f) continue;
                    Vector3 p = new Vector3(side * Range(random, 11, 14), -9, z);
                    switch (other)
                    {
                        case EnvironmentBiome.Jungle:
                            BoundaryBroadleaf(geometry, p, Range(random, 7, 10), random); break;
                        case EnvironmentBiome.Mountains:
                            Tree(geometry, p, Range(random, 7, 11), true, random); break;
                        case EnvironmentBiome.Desert:
                            Cactus(geometry, p, Range(random, 3, 5)); break;
                        case EnvironmentBiome.Beach:
                            Palm(geometry, p, Range(random, 8, 11), Range(random, 0, 360)); break;
                        default:
                            // Park trees and stone planters make the city edge a landscaped outskirts.
                            geometry.Add(cube, stone, p + Vector3.up * 0.3f, new Vector3(3, 0.6f, 3));
                            BoundaryBroadleaf(geometry, p + Vector3.up * 0.5f, 6, random); break;
                    }
                    Material rockMaterial = other == EnvironmentBiome.Desert ? redStone : stone;
                    Boulder(geometry, rockMaterial, p + new Vector3(side * 4, 0, 2), new Vector3(3.5f, 2.1f, 3), random);
                }
            }
            return geometry.Bake();
        }

        private static void BoundaryBroadleaf(Geometry geometry, Vector3 position, float height, System.Random random)
        {
            float yaw = Range(random, 0, 360);
            geometry.Add(trunk, jungleBark, position, new Vector3(height * 0.08f, height * 0.78f, height * 0.08f), yaw);
            geometry.Add(crowns[0], jungleLeaf, position + Vector3.up * height * 0.75f,
                new Vector3(height * 0.6f, height * 0.5f, height * 0.55f), yaw);
        }
    }
}
