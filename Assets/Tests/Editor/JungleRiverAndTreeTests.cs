using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CubeDash.Tests
{
    public sealed class JungleRiverAndTreeTests
    {
        private const string JunglePath = "Assets/Prefab/CubeDash/Environments/Jungle.prefab";

        [Test]
        public void RiverHasDownstreamCurrentAndBankShadingWithoutOceanTides()
        {
            GameObject jungle = AssetDatabase.LoadAssetAtPath<GameObject>(JunglePath);
            Transform river = jungle.transform.Find("River");
            Material material = river.GetComponent<Renderer>().sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("CubeDash/Biome Water"));
            Assert.That(material.GetFloat("_ShoreMode"), Is.Zero);
            Assert.That(material.GetFloat("_TideHeight"), Is.Zero);
            Assert.That(material.GetFloat("_TideDistance"), Is.Zero);
            Assert.That(material.GetFloat("_FlowSpeed"), Is.InRange(0.8f, 1.8f));
            Assert.That(material.GetFloat("_RiverCenter"), Is.EqualTo(-29));
            Assert.That(material.GetFloat("_RiverHalfWidth"), Is.EqualTo(2.5f));
            Assert.That(material.GetFloat("_RiverFoam"), Is.InRange(0.2f, 0.5f));
            Assert.That(material.GetColor("_ShallowColor").g, Is.GreaterThan(material.GetColor("_BaseColor").g));
            Assert.That(river.localPosition.y, Is.EqualTo(-9.12f).Within(0.001f));
            Assert.That(material.FindPass("DepthOnly"), Is.GreaterThanOrEqualTo(0));
            Assert.That(material.FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void RiverMeshSeamsAndHorizonApronsAreContinuousAndTheBedMeetsBothBanks()
        {
            GameObject jungle = AssetDatabase.LoadAssetAtPath<GameObject>(JunglePath);
            Mesh river = jungle.transform.Find("River").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(river.vertexCount, Is.InRange(1500, 3000));
            Vector3[] vertices = river.vertices;
            foreach (Vector3 vertex in vertices)
                if (Mathf.Abs(vertex.z) < 0.001f)
                {
                    bool found = false;
                    foreach (Vector3 end in vertices)
                        found |= Vector3.Distance(vertex + Vector3.forward * 42, end) < 0.001f;
                    Assert.That(found, Is.True);
                }
            Transform floor = jungle.transform.Find("Landscape Ground");
            Mesh bed = floor.GetComponent<MeshFilter>().sharedMesh;
            foreach (Vector3 vertex in bed.vertices)
            {
                Vector3 world = floor.TransformPoint(vertex);
                float distance = Mathf.Abs(world.x + 29);
                if (distance >= 3) Assert.That(world.y, Is.EqualTo(-9).Within(0.001f));
                if (Mathf.Abs(distance - 2.5f) < 0.001f) Assert.That(world.y, Is.EqualTo(-9.12f).Within(0.001f));
                if (distance <= 1.4f) Assert.That(world.y, Is.LessThan(-10.3f));
            }
            foreach (string edge in new[] { "Rear Horizon", "Forward Horizon" })
            {
                Transform apron = jungle.transform.Find(edge + "/River Apron");
                Assert.That(apron, Is.Not.Null);
                Assert.That(apron.localPosition.y, Is.EqualTo(-9.12f).Within(0.001f));
                Assert.That(apron.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(5000));
                Assert.That(apron.GetComponent<Renderer>().sharedMaterial,
                    Is.SameAs(jungle.transform.Find("River").GetComponent<Renderer>().sharedMaterial));
                Assert.That(jungle.transform.Find(edge + "/Ground Apron").GetComponent<MeshFilter>().sharedMesh, Is.SameAs(bed));
            }
            Assert.That(jungle.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void AllJungleLayoutsUseTheSameCohesiveTreeFamilyAndDedicatedPalette()
        {
            GameObject jungle = AssetDatabase.LoadAssetAtPath<GameObject>(JunglePath);
            Material[] palette = jungle.transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials;
            Assert.That(palette[0].name, Is.EqualTo("Jungle Bark"));
            Assert.That(palette[1].name, Is.EqualTo("Jungle Foliage"));
            Assert.That(palette[2].name, Is.EqualTo("Jungle Canopy Highlights"));
            Color.RGBToHSV(palette[1].GetColor("_BaseColor"), out float hue, out _, out float value);
            Color.RGBToHSV(palette[2].GetColor("_BaseColor"), out float upperHue, out _, out float upperValue);
            Assert.That(Mathf.Abs(upperHue - hue), Is.LessThan(0.035f), "Canopy parts must not look like differently colored tree species.");
            Assert.That(upperValue - value, Is.InRange(0.02f, 0.08f));
            Assert.That(palette[1].GetFloat("_Smoothness"), Is.LessThan(0.15f));
            Mesh[] layouts = jungle.GetComponent<BiomeScenery>().Layouts;
            foreach (Mesh layout in layouts)
            {
                Assert.That(layout.subMeshCount, Is.EqualTo(palette.Length));
                for (int surface = 0; surface < 3; surface++)
                    Assert.That(layout.GetIndexCount(surface), Is.EqualTo(layouts[0].GetIndexCount(surface)),
                        "Tree topology and material assignments stay consistent in every pooled layout.");
                Assert.That(layout.GetIndexCount(2), Is.LessThan(layout.GetIndexCount(1) / 3), "Highlights should be accents, not alternating bright canopy balls.");
                Vector3[] vertices = layout.vertices;
                float barkTop = float.MinValue, canopyTop = float.MinValue;
                foreach (int index in layout.GetTriangles(0)) barkTop = Mathf.Max(barkTop, vertices[index].y);
                foreach (int index in layout.GetTriangles(2)) canopyTop = Mathf.Max(canopyTop, vertices[index].y);
                Assert.That(barkTop, Is.LessThan(canopyTop), "Trunk and branch ends remain inside their crowns.");
            }
            foreach (string other in new[] { "Mountains", "Beach", "Desert" })
            {
                Material[] otherPalette = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/" + other + ".prefab")
                    .transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials;
                Assert.That(otherPalette[1], Is.Not.SameAs(palette[1]), "Jungle corrections must not recolor another biome.");
            }
        }

        [Test]
        public void TreeTrunksAndIndividualRootFeetConformToTheActualHillTriangles()
        {
            foreach (Mesh layout in AssetDatabase.LoadAssetAtPath<GameObject>(JunglePath).GetComponent<BiomeScenery>().Layouts)
            {
                Vector3[] vertices = layout.vertices; int[] terrain = layout.GetTriangles(7);
                // Each authored tree has a 62-vertex trunk and nine 26-vertex branch/root tubes.
                const int treeVertices = 62 + 9 * 26;
                for (int tree = 0; tree < 20; tree++)
                {
                    int start = tree * treeVertices;
                    AssertGrounded(vertices[start + 60], vertices, terrain);
                    for (int side = 0; side < 10; side++) AssertGrounded(vertices[start + side], vertices, terrain);
                    for (int root = 0; root < 4; root++)
                    {
                        AssertGrounded(vertices[start + 62 + (5 + root) * 26 + 24], vertices, terrain);
                        for (int side = 0; side < 8; side++) AssertGrounded(vertices[start + 62 + (5 + root) * 26 + side], vertices, terrain);
                    }
                }
            }
        }

        private static void AssertGrounded(Vector3 foot, Vector3[] vertices, int[] terrain)
        {
            float ground = -9;
            for (int tile = -1; tile <= 1; tile++)
                for (int i = 0; i < terrain.Length; i += 3)
                {
                    Vector3 a = vertices[terrain[i]], b = vertices[terrain[i + 1]], c = vertices[terrain[i + 2]];
                    float z = foot.z + tile * 42;
                    float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(denominator) < 1e-8f) continue;
                    float u = ((b.z - c.z) * (foot.x - c.x) + (c.x - b.x) * (z - c.z)) / denominator;
                    float v = ((c.z - a.z) * (foot.x - c.x) + (a.x - c.x) * (z - c.z)) / denominator;
                    if (u >= -0.00001f && v >= -0.00001f && u + v <= 1.00001f)
                        ground = Mathf.Max(ground, a.y * u + b.y * v + c.y * (1 - u - v));
                }
            Assert.That(foot.y, Is.EqualTo(ground - 0.12f).Within(0.015f), "No floating roots or trunks on the hillside.");
        }

        [Test]
        public void SavedLevelInheritsTheImprovedJungleWithoutSetupOrGrowingTheSceneryPool()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                EndlessTrack track = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if ((track = root.GetComponentInChildren<EndlessTrack>(true)) != null) break;
                Assert.That(track, Is.Not.Null);
                foreach (TrackSegment segment in track.Segments)
                {
                    GameObject jungle = segment.Environment.Biomes[(int)EnvironmentBiome.Jungle];
                    Assert.That(jungle.transform.Find("River").GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(1500));
                    Assert.That(jungle.transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials[1].name, Is.EqualTo("Jungle Foliage"));
                    int count = jungle.GetComponentsInChildren<Transform>(true).Length;
                    for (int i = 8; i < 16; i++) segment.Environment.Configure(i, 8);
                    Assert.That(jungle.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
