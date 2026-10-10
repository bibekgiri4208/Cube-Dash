using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class CityDistrictTests
    {
        [Test]
        public void CityHasReusableHousesShopsMidrisesAndStreetModels()
        {
            foreach (string name in new[] { "Detached House", "Town House", "Market Shop", "Corner Cafe", "Bakery Shop",
                "Balcony Apartments", "Terraced Office", "Clock Hall", "Street Furniture", "Bus Shelter", "Flyover" })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/City Models/" + name + ".prefab");
                Assert.That(prefab, Is.Not.Null, name);
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
                Mesh mesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertexCount, Is.InRange(100, 20000), name);
                Assert.That(mesh.subMeshCount, Is.EqualTo(prefab.GetComponentInChildren<Renderer>().sharedMaterials.Length));
                if (name.EndsWith("Shop") || name == "Corner Cafe")
                {
                    Assert.That(mesh.GetIndexCount(7), Is.GreaterThan(30), "Shops have a colored storefront and signage backing.");
                    Assert.That(mesh.GetIndexCount(8), Is.GreaterThan(100), "Striped awnings distinguish the shopfronts.");
                    Assert.That(mesh.GetIndexCount(5), Is.GreaterThan(1000), "Readable modeled lettering and window frames.");
                }
            }
        }

        [Test]
        public void NeighborhoodLayoutsAreVariedRoadClearAndKeepOnlyASparseDistantSkyline()
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab"));
            try
            {
                SegmentEnvironment environment = root.GetComponent<SegmentEnvironment>();
                Transform city = environment.Biomes[0].transform;
                BiomeScenery scenery = city.GetComponent<BiomeScenery>(); Assert.That(scenery, Is.Not.Null);
                Assert.That(scenery.Layouts.Length, Is.EqualTo(3));
                Assert.That(scenery.Layouts[0], Is.Not.SameAs(scenery.Layouts[1]));
                int towers = 0;
                foreach (Transform child in city)
                    if (child.name.Contains("Skyscraper"))
                    {
                        towers++; Assert.That(Mathf.Abs(child.localPosition.x), Is.GreaterThanOrEqualTo(60));
                    }
                Assert.That(towers, Is.EqualTo(4), "Towers are background accents, not the entire streetscape.");
                int count = root.GetComponentsInChildren<Transform>(true).Length;
                for (int section = 0; section < 8; section++)
                {
                    environment.Configure(section, 8);
                    Assert.That(city.Find("Landscape Details").GetComponent<MeshFilter>().sharedMesh,
                        Is.SameAs(scenery.Layouts[BiomeRules.Variation(section, 3)]));
                }
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                foreach (Mesh mesh in scenery.Layouts)
                {
                    Assert.That(mesh.vertexCount, Is.InRange(10000, 90000));
                    var anchors = new System.Collections.Generic.List<Vector2>(); mesh.GetUVs(2, anchors);
                    Assert.That(anchors.Count, Is.EqualTo(mesh.vertexCount));
                    Vector3[] vertices = mesh.vertices;
                    foreach (int index in mesh.triangles)
                        Assert.That(Mathf.Abs(vertices[index].x), Is.GreaterThan(5), "Buildings cannot intrude into flight or ground lanes.");
                }
                Assert.That(city.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FlyoverSelectsConnectedEntranceAndExitRampsAndRestoresTheLevelDeck()
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab"));
            try
            {
                var environment = root.GetComponent<SegmentEnvironment>();
                Transform city = environment.Biomes[0].transform; var flyover = city.GetComponent<CityFlyover>();
                Assert.That(flyover, Is.Not.Null); Assert.That(flyover.Profiles.Length, Is.EqualTo(3));
                foreach (int section in new[] { 0, 3, 7, 40, 43, 47, 3 })
                {
                    environment.Configure(section, 8);
                    int within = section % 8;
                    Assert.That(city.Find("City Flyover").GetComponent<MeshFilter>().sharedMesh,
                        Is.SameAs(flyover.Profiles[within == 0 ? 1 : within == 7 ? 2 : 0]));
                }
                foreach (Mesh mesh in flyover.Profiles)
                {
                    Vector3[] vertices = mesh.vertices;
                    foreach (int index in mesh.triangles) Assert.That(vertices[index].x, Is.GreaterThan(26));
                }
                Assert.That(RoadHeight(flyover.Profiles[1], 0), Is.EqualTo(-9).Within(0.15f));
                Assert.That(RoadHeight(flyover.Profiles[1], 42), Is.EqualTo(RoadHeight(flyover.Profiles[0], 0)).Within(0.05f));
                Assert.That(RoadHeight(flyover.Profiles[2], 0), Is.EqualTo(RoadHeight(flyover.Profiles[0], 42)).Within(0.05f));
                Assert.That(RoadHeight(flyover.Profiles[2], 42), Is.EqualTo(-9).Within(0.15f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static float RoadHeight(Mesh mesh, float z)
        {
            Vector3[] vertices = mesh.vertices; float sum = 0; int count = 0;
            foreach (int index in mesh.GetTriangles(6))
                if (Mathf.Abs(vertices[index].z - z) < 0.12f) { sum += vertices[index].y; count++; }
            Assert.That(count, Is.GreaterThan(0)); return sum / count;
        }
    }
}
