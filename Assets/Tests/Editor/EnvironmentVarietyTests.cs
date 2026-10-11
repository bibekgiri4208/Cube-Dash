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
        public void BeachBoatsHaveClosedEndWallsAndFurnitureHasGroundReachingSupports()
        {
            Mesh boat = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/Beach Fishing Boat.asset");
            Vector3[] vertices = boat.vertices; int[] indices = boat.GetTriangles(9);
            foreach (float end in new[] { -4f, 4f })
            {
                int faces = 0;
                for (int i = 0; i < indices.Length; i += 3)
                    if (Mathf.Abs(vertices[indices[i]].z - end) < 0.001f
                        && Mathf.Abs(vertices[indices[i + 1]].z - end) < 0.001f
                        && Mathf.Abs(vertices[indices[i + 2]].z - end) < 0.001f) faces++;
                Assert.That(faces, Is.GreaterThanOrEqualTo(6), "Close both sides of each end wall without closing the open deck.");
            }
            Mesh chairs = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/Beach Beach Chairs.asset");
            vertices = chairs.vertices; indices = chairs.GetTriangles(9);
            bool tableLeg = false;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 center = (vertices[indices[i]] + vertices[indices[i + 1]] + vertices[indices[i + 2]]) / 3;
                if (Mathf.Abs(center.x) < 0.1f && Mathf.Abs(center.z + 0.5f) < 0.1f && center.y > 0.1f && center.y < 0.5f) tableLeg = true;
            }
            Assert.That(tableLeg, Is.True, "The table needs a leg, not a floating tabletop.");
            Mesh hut = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/Beach Lifeguard Hut.asset");
            Assert.That(System.Array.Exists(hut.vertices, v => v.z < -6.4f && v.y <= 0.02f), Is.True,
                "The bottom stair/stringers must meet the sand.");
            Mesh umbrella = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/Beach Striped Umbrella.asset");
            float poleTop = float.MinValue;
            foreach (int index in umbrella.GetTriangles(9)) poleTop = Mathf.Max(poleTop, umbrella.vertices[index].y);
            Assert.That(poleTop, Is.GreaterThanOrEqualTo(4.14f), "The pole must reach the canopy hub, not stop below it.");
        }

        [Test]
        public void BeachHousesPalmsAndDocksRiseWhole()
        {
            GameObject beach = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Beach.prefab");
            foreach (Mesh mesh in beach.GetComponent<BiomeScenery>().Layouts)
            {
                Vector3[] vertices = mesh.vertices;
                var anchors = new List<Vector2>(); mesh.GetUVs(2, anchors);
                var huts = new HashSet<Vector2>(); var docks = new HashSet<Vector2>();
                var trunks = new HashSet<Vector2>(); var leaves = new HashSet<Vector2>(); var coconuts = new HashSet<Vector2>();
                foreach (int index in mesh.GetTriangles(0))
                    if (vertices[index].x > -30) trunks.Add(anchors[index]);
                foreach (int surface in new[] { 1, 2 })
                    foreach (int index in mesh.GetTriangles(surface))
                        if (vertices[index].y > 0) leaves.Add(anchors[index]);
                foreach (int surface in new[] { 9, 10, 11, 12 })
                    foreach (int index in mesh.GetTriangles(surface))
                    {
                        Vector3 v = vertices[index];
                        if (v.x < -30) huts.Add(anchors[index]);
                        if (v.x > 10.5f) docks.Add(anchors[index]);
                        if (surface == 9 && v.y > 0 && v.x > -28 && v.x < 0) coconuts.Add(anchors[index]);
                    }
                Assert.That(huts.Count, Is.EqualTo(2), "Two whole huts, not independently rising siding, roofs and stairs.");
                Assert.That(docks.Count, Is.EqualTo(1));
                Assert.That(trunks.Count, Is.EqualTo(4));
                Assert.That(leaves, Is.EquivalentTo(trunks)); Assert.That(coconuts, Is.EquivalentTo(trunks));
            }
        }

        [TestCase(2)]
        [TestCase(4)]
        [TestCase(8)]
        [TestCase(12)]
        public void BeachEntranceAndExitSelectSavedBaySafeDocksWithoutChangingThePool(int segmentsPerBiome)
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab"));
            try
            {
                SegmentEnvironment environment = root.GetComponent<SegmentEnvironment>();
                BiomeScenery beach = environment.Biomes[4].GetComponent<BiomeScenery>();
                Assert.That(beach.EntranceLayouts.Length, Is.EqualTo(8)); Assert.That(beach.ExitLayouts.Length, Is.EqualTo(8));
                int count = root.GetComponentsInChildren<Transform>(true).Length;
                int start = 4 * segmentsPerBiome, nextCycle = 9 * segmentsPerBiome;
                foreach (int section in new[] { start, start + 1, start + segmentsPerBiome - 2,
                    start + segmentsPerBiome - 1, nextCycle, nextCycle + segmentsPerBiome - 1, start })
                {
                    environment.Configure(section, segmentsPerBiome);
                    int within = section % segmentsPerBiome;
                    int layout = BiomeRules.Variation(section, 8);
                    Mesh expected = within == 0 ? beach.EntranceLayouts[layout]
                        : within == segmentsPerBiome - 1 ? beach.ExitLayouts[layout] : beach.Layouts[layout];
                    Assert.That(beach.transform.Find("Landscape Details").GetComponent<MeshFilter>().sharedMesh, Is.SameAs(expected));
                    if (within != 0 && within != segmentsPerBiome - 1) continue;
                    Vector3[] vertices = expected.vertices;
                    foreach (int surface in new[] { 9, 12 })
                        foreach (int index in expected.GetTriangles(surface))
                        {
                            Vector3 v = vertices[index]; if (v.x <= 10.5f) continue;
                            float bank = 8 + (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(7, 120, v.x))) * 22
                                + Mathf.Sin(v.x * 0.055f) * 2.8f + Mathf.Sin(v.x * 0.17f) * 1.2f;
                            Assert.That(within == 0 ? v.z - bank : 42 - bank - v.z, Is.GreaterThan(0.5f),
                                "Every dock part stays inside the conservative bay envelope, clear of the true bed/sea intersection.");
                        }
                }
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase("City Entrance Blend", 0, 1, 2, 9)]
        [TestCase("Beach Exit Blend", 15, 16, 16, 16)]
        public void CoastalCityJoinTreesHaveSharedTrunkCrownAndDetailAnchors(string name, int bark, int leaf, int highlight, int detail)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/" + name + ".asset");
            var anchors = new List<Vector2>(); mesh.GetUVs(2, anchors);
            var trunks = new HashSet<Vector2>();
            foreach (int index in mesh.GetTriangles(bark)) trunks.Add(anchors[index]);
            Assert.That(trunks, Is.Not.Empty);
            foreach (int surface in new[] { leaf, highlight, detail })
            {
                var parts = new HashSet<Vector2>();
                foreach (int index in mesh.GetTriangles(surface)) parts.Add(anchors[index]);
                Assert.That(parts, Is.EquivalentTo(trunks), "Trees must not separate at the Beach/City join while rising.");
            }
        }

        [Test]
        public void DesertArchHasOutwardFacesOnBothSidesAndPositiveSolidVolume()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/Desert Sandstone Arch.asset");
            Vector3[] vertices = mesh.vertices;
            int[] indices = mesh.GetTriangles(6);
            double volume = 0;
            int front = 0, back = 0;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
                if (Mathf.Min(a.y, Mathf.Min(b.y, c.y)) < 1) continue; // Exclude foot boulders.
                if (Mathf.Abs(a.z + 2.25f) < 0.001f && Mathf.Abs(b.z + 2.25f) < 0.001f && Mathf.Abs(c.z + 2.25f) < 0.001f)
                {
                    Assert.That(normal.z, Is.LessThan(-0.99f), "Front faces must not be backface-culled.");
                    front++;
                }
                if (Mathf.Abs(a.z - 2.25f) < 0.001f && Mathf.Abs(b.z - 2.25f) < 0.001f && Mathf.Abs(c.z - 2.25f) < 0.001f)
                {
                    Assert.That(normal.z, Is.GreaterThan(0.99f), "Back faces must remain visible from the opposite side.");
                    back++;
                }
            }
            Assert.That(front, Is.GreaterThan(10)); Assert.That(back, Is.GreaterThan(10));
            Assert.That(volume, Is.GreaterThan(150), "Opening, crown and foot caps must also wind outward.");
        }

        [Test]
        public void PricklyPearPadsFormOneOverlappingPlantInsteadOfFloatingIslands()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/Desert Prickly Pear.asset");
            Vector3[] vertices = mesh.vertices;
            int[] parent = new int[vertices.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Root(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
            var used = new HashSet<int>();
            for (int surface = 1; surface <= 2; surface++)
            {
                int[] triangles = mesh.GetTriangles(surface);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int root = Root(triangles[i]);
                    parent[Root(triangles[i + 1])] = root; parent[Root(triangles[i + 2])] = root;
                    used.Add(triangles[i]); used.Add(triangles[i + 1]); used.Add(triangles[i + 2]);
                }
            }
            var groups = new Dictionary<int, Bounds>();
            foreach (int index in used)
            {
                int root = Root(index);
                if (!groups.TryGetValue(root, out Bounds bounds)) bounds = new Bounds(vertices[index], Vector3.zero);
                bounds.Encapsulate(vertices[index]); groups[root] = bounds;
            }
            var pads = new List<Bounds>(groups.Values);
            Assert.That(pads.Count, Is.EqualTo(8));
            var connected = new HashSet<int> { 0 };
            for (int pass = 0; pass < pads.Count; pass++)
                for (int a = 0; a < pads.Count; a++)
                    for (int b = 0; b < pads.Count; b++)
                    {
                        if (!connected.Contains(a)) continue;
                        Vector3 distance = pads[a].center - pads[b].center;
                        Vector3 reach = pads[a].extents + pads[b].extents;
                        // Flattened oval pads must substantially overlap in their broad plane;
                        // merely overlapping the corners of their boxes is insufficient.
                        float overlap = Mathf.Pow(distance.x / reach.x, 2) + Mathf.Pow(distance.y / reach.y, 2);
                        if (overlap < 0.95f) connected.Add(b);
                    }
            Assert.That(connected.Count, Is.EqualTo(8), "Every pad must join the rooted cactus silhouette.");
        }

        [Test]
        public void DesertCactusTrunksAndArmsUseOneRigidRiseAnchor()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Desert.prefab");
            foreach (Mesh mesh in prefab.GetComponent<BiomeScenery>().Layouts)
            {
                Vector3[] vertices = mesh.vertices;
                var anchors = new List<Vector2>(); mesh.GetUVs(2, anchors);
                var cactusAnchors = new HashSet<Vector2>();
                for (int surface = 1; surface <= 2; surface++)
                    foreach (int index in mesh.GetTriangles(surface))
                    {
                        cactusAnchors.Add(anchors[index]);
                        Assert.That(Mathf.Abs(anchors[index].y - vertices[index].z), Is.LessThan(1.5f));
                    }
                Assert.That(cactusAnchors.Count, Is.EqualTo(6), "Six complete cacti, not eighteen separately rising trunk/arm pieces.");
            }
        }

        [TestCase("Mountains Exit Blend")]
        [TestCase("Beach Entrance Blend")]
        public void BoundaryCactiKeepTheirHighlightedArmsAttachedDuringTheRise(string name)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/" + name + ".asset");
            var anchors = new List<Vector2>(); mesh.GetUVs(2, anchors);
            var trunks = new HashSet<Vector2>(); var arms = new HashSet<Vector2>();
            foreach (int index in mesh.GetTriangles(1)) trunks.Add(anchors[index]);
            foreach (int index in mesh.GetTriangles(2)) arms.Add(anchors[index]);
            Assert.That(trunks, Is.Not.Empty);
            Assert.That(arms, Is.EquivalentTo(trunks), "Highlighted cactus arms must share the dark trunk's reveal timing.");
        }

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
                    Assert.That(biome.transform.Find("Landscape Details").GetComponent<MeshFilter>().sharedMesh, Is.SameAs(scenery.DetailLayout(section, 8)));
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
