using System.Collections.Generic;
using System.Linq;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeDash.Tests
{
    public sealed class BeachSceneryTests
    {
        [Test]
        public void ReapplicationPreservesTheSavedSceneAndLocalArt()
        {
            byte[] scene = System.IO.File.ReadAllBytes(BeachSceneryBuilder.ScenePath);
            byte[] prefab = System.IO.File.ReadAllBytes(BeachSceneryBuilder.PrefabPath);
            int count = System.IO.Directory.GetFiles(BeachSceneryBuilder.ArtPath, "*", System.IO.SearchOption.AllDirectories).Length;
            try
            {
                BeachSceneryBuilder.ApplyFromCommandLine();
                Assert.That(System.IO.File.ReadAllBytes(BeachSceneryBuilder.ScenePath), Is.EqualTo(scene));
                Assert.That(System.IO.File.ReadAllBytes(BeachSceneryBuilder.PrefabPath), Is.EqualTo(prefab));
                Assert.That(System.IO.Directory.GetFiles(BeachSceneryBuilder.ArtPath, "*", System.IO.SearchOption.AllDirectories).Length, Is.EqualTo(count));
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        [Test]
        public void RenderDetailedBeachWhenPreviewsAreRequested()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CUBE_DASH_BEACH_PREVIEW")))
                Assert.Ignore("Opt-in GPU visual check.");
            try
            {
                MainMenuMapIllustrations.RenderBeachFromCommandLine();
                BeachSceneryBuilder.CaptureFromCommandLine();
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        [Test]
        public void EveryPooledBeachSectionUsesLocalArtAndPreservesOceanGroundAndPool()
        {
            var scene = EditorSceneManager.OpenPreviewScene(BeachSceneryBuilder.ScenePath);
            try
            {
                var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CubeDashGame>(true)).Single();
                var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Beach.prefab");
                int count = game.Track.GetComponentsInChildren<Transform>(true).Length;
                foreach (TrackSegment segment in game.Track.Segments)
                {
                    GameObject root = segment.Environment.Biomes[(int)EnvironmentBiome.Beach];
                    var scenery = root.GetComponent<BiomeScenery>();
                    Assert.That(scenery.Layouts.Length, Is.EqualTo(8)); Assert.That(scenery.VarietyLayouts.Length, Is.EqualTo(8));
                    foreach (Mesh mesh in scenery.Layouts.Concat(scenery.EntranceLayouts).Concat(scenery.ExitLayouts).Concat(scenery.VarietyLayouts))
                        Assert.That(AssetDatabase.GetAssetPath(mesh), Does.StartWith(BeachSceneryBuilder.ArtPath + "/"));
                    foreach (string name in new[] { "Ocean", "Shoreline", "Landscape Ground" })
                    {
                        Transform current = root.transform.Find(name), old = original.transform.Find(name);
                        Assert.That(current.localPosition, Is.EqualTo(old.localPosition)); Assert.That(current.localScale, Is.EqualTo(old.localScale));
                        Assert.That(current.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(old.GetComponent<MeshFilter>().sharedMesh));
                        Assert.That(current.GetComponent<Renderer>().sharedMaterial, Is.SameAs(old.GetComponent<Renderer>().sharedMaterial));
                    }
                    for (int section = -1; section < 100; section++)
                    {
                        segment.Environment.Configure(section, 8, EnvironmentBiome.Beach);
                        Assert.That(root.transform.Find("Landscape Details").GetComponent<MeshFilter>().sharedMesh,
                            Is.SameAs(scenery.Layouts[BiomeRules.Variation(section, 8)]));
                    }
                    Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                    foreach (string name in new[] { "Landscape Details", "Landscape Variety" })
                    {
                        var renderer = root.transform.Find(name).GetComponent<Renderer>();
                        Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(root.transform.Find(name).GetComponent<MeshFilter>().sharedMesh.subMeshCount));
                        Assert.That(renderer.sharedMaterials.All(material => material != null && material.shader != null), Is.True);
                        var rise = new SerializedObject(segment.GetComponent<SceneryRise>()).FindProperty("scenery");
                        Assert.That(Enumerable.Range(0, rise.arraySize).Any(i => rise.GetArrayElementAtIndex(i).objectReferenceValue == renderer), Is.True);
                    }
                }
                Assert.That(game.Track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count), "Swapping all layouts must never spawn scenery.");
                Assert.That(AssetDatabase.GetAssetPath(original.GetComponent<BiomeScenery>().Layouts[0]), Does.Not.StartWith(BeachSceneryBuilder.ArtPath));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
        public void DetailedLayoutsHaveNoPlantFurnitureOverlapsOrRoadIntrusionAndCarryValidChannels(int layout)
        {
            Mesh plants = AssetDatabase.LoadAssetAtPath<Mesh>(BeachSceneryBuilder.ArtPath + "/Layouts/Coastal Planting " + layout + ".asset");
            Mesh props = AssetDatabase.LoadAssetAtPath<Mesh>(BeachSceneryBuilder.ArtPath + "/Layouts/Coastal Architecture " + layout + ".asset");
            Assert.That(plants, Is.Not.Null); Assert.That(props, Is.Not.Null);
            var furniture = Footprints(props, Enumerable.Range(0, props.subMeshCount), anchor => anchor.x < 0);
            var vegetation = Footprints(plants, Enumerable.Range(15, plants.subMeshCount - 15), _ => true);
            Assert.That(vegetation.Count, Is.GreaterThanOrEqualTo(35));
            foreach (Bounds plant in vegetation.Values)
            {
                Assert.That(plant.max.x, Is.LessThan(-9), "Planting must leave road, shoulders and guardrails clear.");
                Assert.That(plant.min.z, Is.GreaterThanOrEqualTo(0.5f)); Assert.That(plant.max.z, Is.LessThanOrEqualTo(41.5f));
                foreach (Bounds prop in furniture.Values)
                    Assert.That(plant.min.x < prop.max.x && plant.max.x > prop.min.x && plant.min.z < prop.max.z && plant.max.z > prop.min.z,
                        Is.False, "Actual rotated leaf geometry must not enter seating, huts or lighthouse footprints.");
            }
            foreach (Mesh mesh in new[] { plants, props })
            {
                Assert.That(mesh.vertexCount, Is.InRange(1000, 220000));
                Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount)); Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.uv2.Length, Is.EqualTo(mesh.vertexCount)); Assert.That(mesh.uv3.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.colors32.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.vertices.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)), Is.True);
                Assert.That(mesh.bounds.min.y, Is.LessThan(mesh.vertices.Min(v => v.y) - 70), "Reveal culling margin.");
                Assert.That(mesh.triangles.All(index => index >= 0 && index < mesh.vertexCount), Is.True);
            }
        }

        [Test]
        public void CoastalModelsHaveDetailedGeometryGroundedFurnitureAndSeabedReachingPiles()
        {
            string folder = BeachSceneryBuilder.ArtPath + "/Models/";
            foreach (string name in new[] { "Coastal Palm", "Shaded Loungers", "Weatherboard Hut", "Lighthouse", "Timber Jetty", "Sailing Dinghy" })
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(folder + name + ".asset");
                Assert.That(mesh, Is.Not.Null); Assert.That(mesh.vertexCount, Is.GreaterThan(500), name);
                Assert.That(mesh.subMeshCount, Is.EqualTo(15));
            }
            Mesh loungers = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "Shaded Loungers.asset");
            Assert.That(loungers.vertices.Where(v => Mathf.Abs(v.x) < 0.12f && Mathf.Abs(v.z - 1.65f) < 0.12f).Max(v => v.y), Is.GreaterThan(3.6f), "Pole must touch canopy hub.");
            Mesh jetty = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "Timber Jetty.asset");
            foreach (float x in Enumerable.Range(0, 7).Select(i => 6.6f + i * 3.65f))
                Assert.That(jetty.vertices.Where(v => Mathf.Abs(v.x - x) < 0.2f).Min(v => v.y), Is.LessThan(SeabedHeight(x)), "Every pile reaches the actual seabed.");
            Mesh palm = AssetDatabase.LoadAssetAtPath<Mesh>(folder + "Coastal Palm.asset");
            Assert.That(palm.GetTriangles(1).Length + palm.GetTriangles(2).Length, Is.GreaterThan(1500), "Individual pinnate leaflets replace ribbon/umbrella fronds.");
            var materials = AssetDatabase.FindAssets("t:Material", new[] { BeachSceneryBuilder.ArtPath + "/Materials" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)));
            foreach (Material material in materials)
            {
                Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null, material.name);
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False, material.name);
            }
        }

        private static float SeabedHeight(float x)
        {
            Mesh seabed = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/Beach Seabed.asset");
            Vector3[] row = seabed.vertices.Where(v => v.z < 0).OrderBy(v => v.x).ToArray();
            for (int i = 1; i < row.Length; i++)
                if (row[i].x * 2048 >= x)
                    return -9.3f + 0.6f * Mathf.Lerp(row[i - 1].y, row[i].y, Mathf.InverseLerp(row[i - 1].x * 2048, row[i].x * 2048, x));
            Assert.Fail("Seabed does not cover pile."); return 0;
        }

        private static Dictionary<Vector2, Bounds> Footprints(Mesh mesh, IEnumerable<int> surfaces, System.Func<Vector2, bool> keep)
        {
            var result = new Dictionary<Vector2, Bounds>(); Vector3[] points = mesh.vertices; Vector2[] anchors = mesh.uv3;
            foreach (int surface in surfaces) foreach (int index in mesh.GetTriangles(surface))
            {
                Vector2 anchor = anchors[index]; if (!keep(anchor)) continue;
                var p = new Vector3(points[index].x, 0, points[index].z);
                if (!result.TryGetValue(anchor, out Bounds bounds)) bounds = new Bounds(p, Vector3.zero);
                bounds.Encapsulate(p); result[anchor] = bounds;
            }
            return result;
        }
    }
}
