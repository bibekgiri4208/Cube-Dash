using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CubeDash.Tests
{
    public sealed class BiomeBoundaryTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void EveryOrdinaryBeachBoulderHasABuriedFootInAllEightSavedLayouts(int mode)
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab"));
            try
            {
                SegmentEnvironment environment = root.GetComponent<SegmentEnvironment>();
                environment.Configure(mode == 1 ? 32 : mode == 2 ? 39 : 33, 8);
                Transform beach = environment.Biomes[4].transform;
                Renderer floor = beach.Find("Landscape Ground").GetComponent<Renderer>();
                Vector3[] ground = RenderedGround(floor);
                int[] triangles = floor.GetComponent<MeshFilter>().sharedMesh.triangles;
                BiomeScenery scenery = beach.GetComponent<BiomeScenery>();
                Mesh[] layouts = mode == 1 ? scenery.EntranceLayouts : mode == 2 ? scenery.ExitLayouts : scenery.Layouts;
                Assert.That(layouts.Length, Is.EqualTo(8));
                foreach (Mesh mesh in layouts)
                {
                    Transform details = beach.Find("Landscape Details");
                    Vector3[] vertices = mesh.vertices;
                    var anchors = new List<Vector2>(); mesh.GetUVs(2, anchors);
                    var lowest = new Dictionary<Vector2, float>();
                    var buried = new Dictionary<Vector2, int>();
                    var rockBounds = new Dictionary<Vector2, Bounds>();
                    var footBounds = new Dictionary<Vector2, Bounds>();
                    foreach (int index in mesh.GetTriangles(3))
                    {
                        Vector3 world = details.TransformPoint(vertices[index]);
                        float gap = world.y - SurfaceHeight(ground, triangles, world.x, world.z);
                        Vector2 anchor = anchors[index];
                        lowest[anchor] = lowest.TryGetValue(anchor, out float low) ? Mathf.Min(low, gap) : gap;
                        Bounds all = rockBounds.TryGetValue(anchor, out Bounds previous) ? previous : new Bounds(world, Vector3.zero);
                        all.Encapsulate(world); rockBounds[anchor] = all;
                        if (gap < -0.02f)
                        {
                            buried[anchor] = buried.TryGetValue(anchor, out int count) ? count + 1 : 1;
                            Bounds foot = footBounds.TryGetValue(anchor, out Bounds oldFoot) ? oldFoot : new Bounds(world, Vector3.zero);
                            foot.Encapsulate(world); footBounds[anchor] = foot;
                        }
                    }
                    Assert.That(lowest.Count, Is.EqualTo(4), "Check the four ordinary seaside boulders, not only the headland.");
                    foreach (var pair in lowest)
                    {
                        Assert.That(pair.Value, Is.EqualTo(-0.65f).Within(0.002f), mesh.name + " floating at " + pair.Key);
                        Assert.That(buried[pair.Key], Is.GreaterThan(10), "Bury multiple lower faces, not a single touching tip.");
                        Assert.That(footBounds[pair.Key].size.x, Is.GreaterThan(rockBounds[pair.Key].size.x * 0.35f), "The rock needs a broad supported foot.");
                        Assert.That(footBounds[pair.Key].size.z, Is.GreaterThan(rockBounds[pair.Key].size.z * 0.35f), "Do not leave the downhill underside hovering.");
                    }
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SavedPlayableJoinHasNoFloatingRockGroupsInAnyBeachSceneryRenderer()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                EndlessTrack track = null;
                foreach (GameObject root in scene.GetRootGameObjects()) if ((track = root.GetComponentInChildren<EndlessTrack>(true)) != null) break;
                Assert.That(track, Is.Not.Null);
                foreach (TrackSegment segment in track.Segments)
                {
                    segment.Environment.Configure(39, 8);
                    Transform beach = segment.Environment.Biomes[4].transform;
                    Renderer floor = beach.Find("Landscape Ground").GetComponent<Renderer>();
                    Vector3[] ground = RenderedGround(floor);
                    int[] triangles = floor.GetComponent<MeshFilter>().sharedMesh.triangles;
                    int groups = 0;
                    foreach (string name in new[] { "Landscape Details", "Coastal Headland", "Boundary Exit Scenery" })
                    {
                        Renderer renderer = beach.Find(name).GetComponent<Renderer>();
                        Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                        Vector3[] vertices = mesh.vertices;
                        var anchors = new List<Vector2>(); mesh.GetUVs(2, anchors);
                        for (int surface = 0; surface < renderer.sharedMaterials.Length; surface++)
                        {
                            if (renderer.sharedMaterials[surface].name != "Mountain Slate") continue;
                            var lowest = new Dictionary<Vector2, float>();
                            foreach (int index in mesh.GetTriangles(surface))
                            {
                                Vector3 world = renderer.transform.TransformPoint(vertices[index]);
                                float gap = world.y - SurfaceHeight(ground, triangles, world.x, world.z);
                                Vector2 anchor = anchors[index];
                                lowest[anchor] = lowest.TryGetValue(anchor, out float low) ? Mathf.Min(low, gap) : gap;
                            }
                            foreach (var pair in lowest)
                                Assert.That(pair.Value, Is.LessThanOrEqualTo(0.002f), "Saved Level: " + name + " floats at " + pair.Key);
                            groups += lowest.Count;
                        }
                    }
                    Assert.That(groups, Is.GreaterThanOrEqualTo(11));
                    segment.Environment.Configure(40, 8);
                    Transform city = segment.Environment.Biomes[0].transform;
                    float cityGroundHeight = city.Find("City Ground").GetComponent<Renderer>().bounds.max.y;
                    Renderer cityDressing = city.Find("Boundary Entrance Scenery").GetComponent<Renderer>();
                    Mesh cityMesh = cityDressing.GetComponent<MeshFilter>().sharedMesh;
                    Vector3[] cityVertices = cityMesh.vertices;
                    var cityAnchors = new List<Vector2>(); cityMesh.GetUVs(2, cityAnchors);
                    var cityFeet = new Dictionary<Vector2, float>();
                    for (int surface = 0; surface < cityDressing.sharedMaterials.Length; surface++)
                    {
                        if (cityDressing.sharedMaterials[surface].name != "Mountain Slate") continue;
                        foreach (int index in cityMesh.GetTriangles(surface))
                        {
                            float gap = cityDressing.transform.TransformPoint(cityVertices[index]).y - cityGroundHeight;
                            Vector2 anchor = cityAnchors[index];
                            cityFeet[anchor] = cityFeet.TryGetValue(anchor, out float old) ? Mathf.Min(old, gap) : gap;
                        }
                    }
                    Assert.That(cityFeet, Is.Not.Empty);
                    foreach (var pair in cityFeet)
                        Assert.That(pair.Value, Is.LessThanOrEqualTo(0.002f), "City side of saved join floats at " + pair.Key);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Vector3[] RenderedGround(Renderer floor)
        {
            var block = new MaterialPropertyBlock(); floor.GetPropertyBlock(block);
            Vector4 boundary = block.GetVector("_BiomeBoundary");
            Vector3[] ground = floor.GetComponent<MeshFilter>().sharedMesh.vertices;
            for (int i = 0; i < ground.Length; i++)
            {
                float metresZ = floor.transform.localToWorldMatrix.MultiplyVector(ground[i]).z;
                ground[i] = floor.transform.TransformPoint(ground[i]);
                float blend = boundary.x * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Abs(metresZ - boundary.y) / 18)));
                ground[i].y = Mathf.Lerp(ground[i].y, Mathf.Max(ground[i].y, block.GetFloat("_BoundaryGroundHeight")), blend);
            }
            return ground;
        }

        [TestCase(39)]
        [TestCase(79)]
        public void BeachCityShoreRocksAndSandSkirtsIntersectTheRenderedTransitionGround(int section)
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab"));
            try
            {
                SegmentEnvironment environment = root.GetComponent<SegmentEnvironment>();
                environment.Configure(section, 8);
                Transform beach = environment.Biomes[4].transform;
                Transform headland = beach.Find("Coastal Headland");
                Assert.That(headland.gameObject.activeSelf, Is.True);
                Renderer floor = beach.Find("Landscape Ground").GetComponent<Renderer>();
                Mesh groundMesh = floor.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(groundMesh.name, Is.EqualTo("Beach Transition Floor"));
                var block = new MaterialPropertyBlock(); floor.GetPropertyBlock(block);
                Vector4 boundary = block.GetVector("_BiomeBoundary");
                Vector3[] ground = groundMesh.vertices;
                for (int i = 0; i < ground.Length; i++)
                {
                    float metresZ = floor.transform.localToWorldMatrix.MultiplyVector(ground[i]).z;
                    ground[i] = floor.transform.TransformPoint(ground[i]);
                    float blend = boundary.x * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Abs(metresZ - boundary.y) / 18)));
                    ground[i].y = Mathf.Lerp(ground[i].y, Mathf.Max(ground[i].y, block.GetFloat("_BoundaryGroundHeight")), blend);
                }
                int[] triangles = groundMesh.triangles;
                Mesh mesh = headland.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] vertices = mesh.vertices;
                var anchors = new List<Vector2>(); mesh.GetUVs(2, anchors);
                var rockAnchors = new HashSet<Vector2>();
                for (int surface = 0; surface < 2; surface++)
                {
                    var lowest = new Dictionary<Vector2, float>();
                    var highest = new Dictionary<Vector2, float>();
                    foreach (int index in mesh.GetTriangles(surface))
                    {
                        Vector3 world = headland.TransformPoint(vertices[index]);
                        float clearance = world.y - SurfaceHeight(ground, triangles, world.x, world.z);
                        Vector2 anchor = anchors[index];
                        lowest[anchor] = lowest.TryGetValue(anchor, out float low) ? Mathf.Min(low, clearance) : clearance;
                        highest[anchor] = highest.TryGetValue(anchor, out float high) ? Mathf.Max(high, clearance) : clearance;
                    }
                    Assert.That(lowest.Count, Is.EqualTo(7));
                    foreach (var pair in lowest)
                    {
                        Assert.That(pair.Value, Is.LessThan(-0.05f), "No floating rock or sand island at " + pair.Key);
                        Assert.That(highest[pair.Key], Is.GreaterThan(0.1f), "Keep the shoreline cluster visible, not completely buried.");
                        if (surface == 1) Assert.That(pair.Value, Is.EqualTo(-0.12f).Within(0.002f), "Sand skirts must follow the actual rendered terrain.");
                    }
                    if (surface == 0) rockAnchors.UnionWith(lowest.Keys);
                    else Assert.That(lowest.Keys, Is.EquivalentTo(rockAnchors));
                }
                int count = root.GetComponentsInChildren<Transform>(true).Length;
                environment.Configure(section - 1, 8);
                Assert.That(headland.gameObject.activeSelf, Is.False);
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static float SurfaceHeight(Vector3[] vertices, int[] indices, float x, float z)
        {
            // Boundary boulders can straddle the adjoining City tile by a few centimetres.
            // Its level land continues the fully raised beach endpoint, not empty space.
            float firstZ = vertices[0].z, lastZ = vertices[vertices.Length - 1].z;
            if (z < firstZ || z > lastZ)
            {
                Assert.That(z, Is.InRange(firstZ - 2, lastZ + 2), "Unexpected scenery beyond the neighboring join.");
                z = Mathf.Clamp(z, firstZ, lastZ);
            }
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                float determinant = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(determinant) < 1e-8f) continue;
                float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / determinant;
                float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / determinant;
                if (u >= -0.00001f && v >= -0.00001f && u + v <= 1.00001f)
                    return u * a.y + v * b.y + (1 - u - v) * c.y;
            }
            Assert.Fail("Shore dressing lies outside the saved transition ground.");
            return 0;
        }

        [Test]
        public void EveryJoinUsesPairedGroundSettingsAndMixedSceneryAndResetsOnRecycle()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab");
            GameObject a = Object.Instantiate(prefab), b = Object.Instantiate(prefab);
            try
            {
                var outgoing = a.GetComponent<SegmentEnvironment>(); var incoming = b.GetComponent<SegmentEnvironment>();
                var blendA = a.GetComponent<BiomeBoundaryBlend>(); var blendB = b.GetComponent<BiomeBoundaryBlend>();
                Assert.That(blendA, Is.Not.Null); Assert.That(blendA.Regions.Length, Is.EqualTo(5));
                int count = a.GetComponentsInChildren<Transform>(true).Length;
                for (int join = 1; join <= 10; join++)
                {
                    outgoing.Configure(join * 8 - 1, 8); incoming.Configure(join * 8, 8);
                    Assert.That(blendA.Exit, Is.True); Assert.That(blendB.Entrance, Is.True);
                    var from = blendA.Regions[(join - 1) % 5]; var to = blendB.Regions[join % 5];
                    Assert.That(from.ExitDressing.activeSelf, Is.True); Assert.That(to.EntranceDressing.activeSelf, Is.True);
                    Assert.That(from.ExitDressing.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(100));
                    Assert.That(to.EntranceDressing.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(100));
                    foreach (Renderer surface in from.Ground)
                    {
                        var block = new MaterialPropertyBlock(); surface.GetPropertyBlock(block);
                        Vector4 boundary = block.GetVector("_BiomeBoundary");
                        Assert.That(boundary.x, Is.EqualTo(1)); Assert.That(boundary.z, Is.EqualTo(1));
                        float offset = a.transform.InverseTransformPoint(surface.transform.position).z;
                        Assert.That(boundary.y + offset, Is.EqualTo(42).Within(0.001f));
                        Assert.That(Vector4.Distance(block.GetColor("_NeighborColor"), to.Floor.GetColor("_BaseColor")), Is.LessThan(0.00001f));
                    }
                    foreach (Renderer surface in to.Ground)
                    {
                        var block = new MaterialPropertyBlock(); surface.GetPropertyBlock(block);
                        Vector4 boundary = block.GetVector("_BiomeBoundary");
                        Assert.That(boundary.x, Is.EqualTo(1)); Assert.That(boundary.z, Is.EqualTo(-1));
                        float offset = b.transform.InverseTransformPoint(surface.transform.position).z;
                        Assert.That(boundary.y + offset, Is.EqualTo(0).Within(0.001f));
                        Assert.That(Vector4.Distance(block.GetColor("_NeighborColor"), from.Floor.GetColor("_BaseColor")), Is.LessThan(0.00001f));
                    }
                }
                outgoing.Configure(11, 8);
                Assert.That(blendA.Entrance || blendA.Exit, Is.False);
                foreach (var region in blendA.Regions)
                {
                    Assert.That(region.EntranceDressing.activeSelf || region.ExitDressing.activeSelf, Is.False);
                    for (int i = 0; i < region.Ground.Length; i++)
                    {
                        var block = new MaterialPropertyBlock(); region.Ground[i].GetPropertyBlock(block);
                        Assert.That(block.GetVector("_BiomeBoundary").x, Is.Zero);
                        Assert.That(region.Ground[i].GetComponent<MeshFilter>().sharedMesh, Is.SameAs(region.NormalGround[i]));
                        Assert.That(region.Ground[i].sharedMaterial.GetVector("_BiomeBoundary").x, Is.Zero, "Do not edit shared materials.");
                    }
                }
                Assert.That(a.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                outgoing.Configure(0, 8); Assert.That(blendA.Entrance, Is.False);
                outgoing.Configure(-1, 8); Assert.That(blendA.Exit, Is.False);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void RiverEndsTaperAtBothBoundariesAndTransitionPropsKeepTheRoadClear()
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab"));
            try
            {
                var environment = root.GetComponent<SegmentEnvironment>(); var blend = root.GetComponent<BiomeBoundaryBlend>();
                foreach (int section in new[] { 8, 15, 11 })
                {
                    environment.Configure(section, 8);
                    foreach (Renderer river in blend.Regions[1].Water)
                    {
                        var block = new MaterialPropertyBlock(); river.GetPropertyBlock(block);
                        Vector4 ends = block.GetVector("_RiverEnds");
                        Assert.That(ends.x, Is.EqualTo(section == 8 ? 1 : 0));
                        Assert.That(ends.y, Is.EqualTo(section == 15 ? 1 : 0));
                    }
                }
                foreach (var region in blend.Regions)
                    foreach (GameObject dressing in new[] { region.EntranceDressing, region.ExitDressing })
                    {
                        Assert.That(dressing.GetComponentsInChildren<Collider>(true), Is.Empty);
                        Mesh mesh = dressing.GetComponent<MeshFilter>().sharedMesh; Vector3[] vertices = mesh.vertices;
                        foreach (int index in mesh.triangles) Assert.That(Mathf.Abs(vertices[index].x), Is.GreaterThan(5));
                        Assert.That(mesh.subMeshCount, Is.EqualTo(dressing.GetComponent<Renderer>().sharedMaterials.Length));
                    }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CityOutskirtsStepDownAndRestoreOriginalBuildingsAfterRecycling()
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab"));
            try
            {
                var environment = root.GetComponent<SegmentEnvironment>();
                Transform city = environment.Biomes[0].transform;
                var originals = new System.Collections.Generic.Dictionary<Transform, Vector3>();
                foreach (Transform child in city) if (child.name.Contains("Skyscraper")) originals[child] = child.localScale;
                Assert.That(originals.Count, Is.GreaterThan(0));
                environment.Configure(7, 8);
                int smaller = 0, hidden = 0;
                foreach (var pair in originals)
                {
                    if (pair.Key.localScale.y < pair.Value.y) smaller++;
                    if (!pair.Key.gameObject.activeSelf) hidden++;
                    Assert.That(pair.Key.localPosition.y, Is.EqualTo(-9));
                }
                Assert.That(smaller, Is.GreaterThan(0)); Assert.That(hidden, Is.GreaterThan(0));
                environment.Configure(3, 8);
                foreach (var pair in originals)
                {
                    Assert.That(pair.Key.localScale, Is.EqualTo(pair.Value));
                    Assert.That(pair.Key.gameObject.activeSelf, Is.True);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SavedLevelInheritsAllFiveTransitions()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                EndlessTrack track = null;
                foreach (GameObject root in scene.GetRootGameObjects()) if ((track = root.GetComponentInChildren<EndlessTrack>(true)) != null) break;
                Assert.That(track, Is.Not.Null);
                foreach (TrackSegment segment in track.Segments)
                {
                    Assert.That(segment.GetComponent<BiomeBoundaryBlend>().Regions.Length, Is.EqualTo(5));
                    Assert.That(segment.Environment.Biomes[4].transform.Find("Coastal Headland").GetComponent<MeshFilter>().sharedMesh,
                        Is.SameAs(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/Coastal Headland.asset")));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
