using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class EnvironmentVarietyTests
    {
        [Test]
        public void EightLayoutsVisitEveryCompositionWithoutAdjacentRepeatsOrGameplayRandomness()
        {
            for (int block = -2; block < 200; block++)
            {
                var seen = new HashSet<int>();
                for (int within = 0; within < BiomeRules.SceneryLayoutCount; within++)
                {
                    int section = block * BiomeRules.SceneryLayoutCount + within;
                    int layout = BiomeRules.Variation(section, BiomeRules.SceneryLayoutCount);
                    Assert.That(layout, Is.InRange(0, BiomeRules.SceneryLayoutCount - 1));
                    Assert.That(seen.Add(layout), Is.True);
                    Assert.That(BiomeRules.Variation(section, BiomeRules.SceneryLayoutCount), Is.EqualTo(layout));
                    Assert.That(BiomeRules.Variation(section + 1, BiomeRules.SceneryLayoutCount), Is.Not.EqualTo(layout));
                }
            }
            Assert.That(BiomeRules.Variation(5, 0), Is.Zero);
            Assert.That(BiomeRules.Variation(5, 1), Is.Zero);
        }

        [Test]
        public void EveryBiomeHasDistinctReusableNativeModelsWithoutColliders()
        {
            string[][] names = {
                new[] { "Park Fountain", "Glass Pavilion", "Market Stalls", "Water Tower", "Garden Pergola" },
                new[] { "Bamboo Grove", "Banana Plant", "Fan Palm", "Mossy Fallen Log", "Ruined Gateway" },
                new[] { "Snow Fir", "Alpine Cabin", "Trail Cairn", "Bare Pine", "Rock Spires" },
                new[] { "Barrel Cactus", "Prickly Pear", "Agave", "Sandstone Arch", "Desert Ruins", "Oasis Palms" },
                new[] { "Striped Umbrella", "Fishing Boat", "Lifeguard Hut", "Lighthouse", "Driftwood", "Beach Chairs" }
            };
            for (int biome = 0; biome < BiomeRules.Count; biome++)
            {
                var meshes = new HashSet<Mesh>();
                foreach (string name in names[biome])
                {
                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/Prefab/CubeDash/Environments/Variety Models/" + (EnvironmentBiome)biome + "/" + name + ".prefab");
                    Assert.That(model, Is.Not.Null, name);
                    Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Mesh mesh = model.GetComponentInChildren<MeshFilter>().sharedMesh;
                    Assert.That(meshes.Add(mesh), Is.True, "Variety uses distinct geometry, not renamed copies.");
                    Assert.That(mesh.vertexCount, Is.InRange(30, 18000), name);
                    Assert.That(mesh.subMeshCount, Is.EqualTo(model.GetComponentInChildren<Renderer>().sharedMaterials.Length));
                    AssertValid(mesh);
                }
            }
        }

        [Test]
        public void SavedVarietyCompositionsHaveRoadClearanceRigidAnchorsAndBoundedMeshes()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab");
            foreach (GameObject biome in prefab.GetComponent<SegmentEnvironment>().Biomes)
            {
                BiomeScenery scenery = biome.GetComponent<BiomeScenery>();
                Assert.That(scenery.Layouts.Length, Is.EqualTo(BiomeRules.SceneryLayoutCount));
                Assert.That(scenery.VarietyLayouts.Length, Is.EqualTo(BiomeRules.SceneryLayoutCount));
                Renderer renderer = biome.transform.Find("Landscape Variety").GetComponent<Renderer>();
                var materials = renderer.sharedMaterials;
                foreach (Mesh mesh in scenery.VarietyLayouts)
                {
                    Assert.That(mesh.vertexCount, Is.InRange(300, 24000));
                    Assert.That(mesh.subMeshCount, Is.EqualTo(materials.Length));
                    AssertValid(mesh);
                    var anchors = new List<Vector2>(); mesh.GetUVs(2, anchors);
                    Assert.That(anchors.Count, Is.EqualTo(mesh.vertexCount));
                    Assert.That(new HashSet<Vector2>(anchors).Count, Is.GreaterThanOrEqualTo(3));
                    Vector3[] vertices = mesh.vertices; int[] indices = mesh.triangles;
                    float minimum = float.MaxValue;
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        Assert.That(anchors[indices[i]], Is.EqualTo(anchors[indices[i + 1]]));
                        Assert.That(anchors[indices[i]], Is.EqualTo(anchors[indices[i + 2]]));
                        for (int j = 0; j < 3; j++)
                        {
                            Vector3 vertex = vertices[indices[i + j]];
                            Assert.That(Mathf.Abs(vertex.x), Is.GreaterThan(5), "Keep flight and ground lanes clear.");
                            minimum = Mathf.Min(minimum, vertex.y);
                        }
                    }
                    Assert.That(mesh.bounds.min.y, Is.LessThanOrEqualTo(minimum - 79.9f));
                }
            }
        }

        [Test]
        public void RecyclingSwapsPairedSavedMeshesWithoutAddingObjectsAndRiseFreezesWithItsClock()
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab"));
            try
            {
                SegmentEnvironment environment = root.GetComponent<SegmentEnvironment>();
                int count = root.GetComponentsInChildren<Transform>(true).Length;
                var block = new MaterialPropertyBlock();
                for (int section = 0; section < 160; section++)
                {
                    environment.Configure(section, 8);
                    GameObject biome = environment.Biomes[(int)environment.CurrentBiome];
                    BiomeScenery scenery = biome.GetComponent<BiomeScenery>();
                    int layout = BiomeRules.Variation(section, BiomeRules.SceneryLayoutCount);
                    Assert.That(biome.transform.Find("Landscape Details").GetComponent<MeshFilter>().sharedMesh, Is.SameAs(scenery.Layouts[layout]));
                    Transform variety = biome.transform.Find("Landscape Variety");
                    Assert.That(variety.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(scenery.VarietyLayouts[layout]));
                    SceneryRise rise = root.GetComponent<SceneryRise>();
                    rise.Begin(true, 10); rise.Tick(10.4f);
                    variety.GetComponent<Renderer>().GetPropertyBlock(block); Vector4 value = block.GetVector("_SceneryRise");
                    Assert.That(value.x, Is.EqualTo(1));
                    rise.Tick(10.4f); variety.GetComponent<Renderer>().GetPropertyBlock(block);
                    Assert.That(block.GetVector("_SceneryRise"), Is.EqualTo(value));
                    rise.Settle(); variety.GetComponent<Renderer>().GetPropertyBlock(block);
                    Assert.That(block.GetVector("_SceneryRise"), Is.EqualTo(Vector4.zero));
                }
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                foreach (GameObject biome in environment.Biomes) Assert.That(biome.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void AssertValid(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices, normals = mesh.normals; int[] indices = mesh.triangles;
            Assert.That(normals.Length, Is.EqualTo(vertices.Length));
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).sqrMagnitude, Is.GreaterThan(1e-12f), mesh.name);
                for (int j = 0; j < 3; j++)
                {
                    Vector3 v = vertices[indices[i + j]];
                    Assert.That(float.IsNaN(v.x + v.y + v.z) || float.IsInfinity(v.x + v.y + v.z), Is.False);
                    Assert.That(normals[indices[i + j]].sqrMagnitude, Is.InRange(0.9f, 1.1f), mesh.name);
                }
            }
        }
    }
}
