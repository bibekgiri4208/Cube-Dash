using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CubeDash.Tests
{
    public sealed class EnvironmentTests
    {
        [TestCase(-42, EnvironmentBiome.City)]
        [TestCase(0, EnvironmentBiome.City)]
        [TestCase(335.99f, EnvironmentBiome.City)]
        [TestCase(336, EnvironmentBiome.Jungle)]
        [TestCase(672, EnvironmentBiome.Mountains)]
        [TestCase(1008, EnvironmentBiome.Desert)]
        [TestCase(1344, EnvironmentBiome.Beach)]
        [TestCase(1680, EnvironmentBiome.City)]
        public void BiomesCycleAtSectionAlignedDistances(float distance, EnvironmentBiome expected)
            => Assert.That(BiomeRules.AtDistance(distance, 8), Is.EqualTo(expected));

        [Test]
        public void SectionLayoutsAreStableAndVaried()
        {
            bool[] seen = new bool[3];
            for (int section = 0; section < 100; section++)
            {
                int layout = BiomeRules.Variation(section, 3);
                Assert.That(layout, Is.InRange(0, 2));
                Assert.That(BiomeRules.Variation(section, 3), Is.EqualTo(layout));
                seen[layout] = true;
            }
            Assert.That(seen, Is.All.True);
            Assert.That(BiomeRules.Length(0), Is.EqualTo(84));
        }

        [Test]
        public void NaturalLandscapesUseDetailedValidMeshesAndKeepTheRoadClear()
        {
            foreach (EnvironmentBiome biome in new[] { EnvironmentBiome.Jungle, EnvironmentBiome.Mountains,
                EnvironmentBiome.Desert, EnvironmentBiome.Beach })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefab/CubeDash/Environments/" + biome + ".prefab");
                Assert.That(prefab, Is.Not.Null);
                foreach (Mesh mesh in prefab.GetComponent<BiomeScenery>().Layouts)
                {
                    Assert.That(mesh.vertexCount, Is.InRange(4000, 90000), "Detailed scenery must remain within its mesh budget.");
                    Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                    Assert.That(normals.Length, Is.EqualTo(vertices.Length));
                    int[] indices = mesh.triangles;
                    for (int triangle = 0; triangle < indices.Length; triangle += 3)
                    {
                        Vector3 a = vertices[indices[triangle]], b = vertices[indices[triangle + 1]], c = vertices[indices[triangle + 2]];
                        Assert.That(Vector3.Cross(b - a, c - a).sqrMagnitude, Is.GreaterThan(1e-12f),
                            "Model triangles must not collapse into lines.");
                        for (int corner = 0; corner < 3; corner++)
                        {
                            int index = indices[triangle + corner];
                            Vector3 vertex = vertices[index];
                            Assert.That(float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsNaN(vertex.z) ||
                                float.IsInfinity(vertex.x) || float.IsInfinity(vertex.y) || float.IsInfinity(vertex.z), Is.False);
                            Assert.That(normals[index].sqrMagnitude, Is.InRange(0.9f, 1.1f));
                            Assert.That(Mathf.Abs(vertex.x), Is.GreaterThan(5f), "Scenery must not cover the playable road.");
                        }
                    }
                    if (biome == EnvironmentBiome.Mountains)
                        Assert.That(mesh.GetIndexCount(4), Is.GreaterThan(100), "Snow should follow the rugged peaks.");
                    if (biome == EnvironmentBiome.Beach)
                    {
                        float canopyTop = Mathf.Max(SurfaceTop(mesh, 1), SurfaceTop(mesh, 2));
                        Assert.That(SurfaceTop(mesh, 0), Is.LessThanOrEqualTo(canopyTop + 0.15f),
                            "Palm trunks must end inside their crowns, not stretch into spikes above the leaves.");
                    }
                }
            }
        }

        private static float SurfaceTop(Mesh mesh, int surface)
        {
            float top = float.MinValue;
            Vector3[] vertices = mesh.vertices;
            foreach (int index in mesh.GetTriangles(surface)) top = Mathf.Max(top, vertices[index].y);
            return top;
        }

        [Test]
        public void CoastlineExtendsPastTheViewAndWaterHasContinuousWaveGeometry()
        {
            GameObject beach = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Beach.prefab");
            Transform ground = beach.transform.Find("Landscape Ground");
            Assert.That(ground.localScale.x, Is.GreaterThanOrEqualTo(2048));
            Transform ocean = beach.transform.Find("Ocean");
            Mesh mesh = ocean.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertexCount, Is.InRange(300, 2000), "Waves need a subdivided surface, not a thin cube.");
            Assert.That(mesh.bounds.min.x, Is.EqualTo(7).Within(0.01f));
            Assert.That(mesh.bounds.max.x, Is.GreaterThanOrEqualTo(1024));
            Assert.That(mesh.bounds.min.z, Is.EqualTo(0).Within(0.01f));
            Assert.That(mesh.bounds.max.z, Is.EqualTo(RunnerRules.SegmentLength).Within(0.01f));
            Assert.That(mesh.bounds.size.y, Is.GreaterThan(0.5f), "Animated waves must stay inside the culling bounds.");
            Vector3[] vertices = mesh.vertices;
            foreach (Vector3 vertex in vertices)
                if (Mathf.Abs(vertex.z) < 0.01f)
                    Assert.That(System.Array.Exists(vertices, v => Mathf.Abs(v.x - vertex.x) < 0.01f &&
                        Mathf.Abs(v.z - RunnerRules.SegmentLength) < 0.01f), Is.True,
                        "Every wave edge vertex must match the next pooled section.");
            Material water = ocean.GetComponent<Renderer>().sharedMaterial;
            Assert.That(water.shader.name, Is.EqualTo("CubeDash/Biome Water"));
            Assert.That(water.FindPass("DepthOnly"), Is.GreaterThanOrEqualTo(0));
            Assert.That(water.FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0));
            Assert.That(water.GetFloat("_ShoreMode"), Is.EqualTo(1));
            Assert.That(water.GetFloat("_WaveHeight"), Is.InRange(0.08f, 0.3f));
            GameObject jungle = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Jungle.prefab");
            Material river = jungle.transform.Find("River").GetComponent<Renderer>().sharedMaterial;
            Assert.That(river, Is.Not.SameAs(water));
            Assert.That(river.GetFloat("_ShoreMode"), Is.Zero, "River should not have coastal foam bands.");
        }

        [Test]
        public void BeachHasSavedApronsBeyondBothLongitudinalPoolEdges()
        {
            GameObject beach = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Beach.prefab");
            Assert.That(beach.GetComponent<HorizonBackdrop>(), Is.Not.Null);
            foreach (string edge in new[] { "Rear Horizon", "Forward Horizon" })
            {
                Transform apron = beach.transform.Find(edge);
                Assert.That(apron, Is.Not.Null);
                Assert.That(apron.gameObject.activeSelf, Is.False, "Only the actual pool ends activate their aprons.");
                Assert.That(apron.Find("Ground Apron").localScale.z, Is.GreaterThanOrEqualTo(1024));
                Assert.That(apron.Find("Ocean Apron").localScale.z * RunnerRules.SegmentLength, Is.GreaterThanOrEqualTo(1023.9f));
                Assert.That(apron.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
        }

        [Test]
        public void ArchitectureAndNaturalSurfacesHaveSavedDetailWithoutExtraColliders()
        {
            for (int variant = 1; variant <= 3; variant++)
            {
                GameObject building = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Skyscraper" + variant + ".prefab");
                Transform facade = building.transform.Find("Facade Details");
                Assert.That(facade, Is.Not.Null);
                Assert.That(facade.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.InRange(1500, 15000));
                Assert.That(building.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            GameObject beach = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Beach.prefab");
            foreach (Mesh layout in beach.GetComponent<BiomeScenery>().Layouts)
            {
                Assert.That(layout.GetIndexCount(10), Is.GreaterThanOrEqualTo(48), "Houses need hipped roofs and visible thatch.");
                Assert.That(layout.GetIndexCount(11), Is.GreaterThan(100), "Houses need inset glass doors and windows.");
            }
            foreach (string name in new[] { "Bark", "Golden Sand", "Mountain Slate", "Beach Timber", "Deep Foliage" })
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Environments/" + name + ".mat");
                Assert.That(material.shader.name, Is.EqualTo("CubeDash/Environment Surface"));
                Assert.That(material.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0));
            }
        }

        [Test]
        public void LevelHasFiveSavedBiomesAndThreeLayoutsWithoutSceneryColliders()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                EndlessTrack track = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.GetComponent<EndlessTrack>() != null) track = root.GetComponent<EndlessTrack>();
                    if (root.TryGetComponent(out RunnerHud hud))
                        Assert.That(hud.transform.Find("Safe Area/Environment Label"), Is.Null);
                }
                Assert.That(track, Is.Not.Null);
                Assert.That(track.Environment, Is.Not.Null);
                Assert.That(track.Environment.BiomeLength, Is.EqualTo(336));
                var settings = new SerializedObject(track.Environment);
                Assert.That(settings.FindProperty("sunlight").objectReferenceValue, Is.Not.Null);
                Assert.That(settings.FindProperty("atmospheres").arraySize, Is.EqualTo(5));
                foreach (TrackSegment segment in track.Segments)
                {
                    Assert.That(segment.Environment, Is.Not.Null);
                    Assert.That(segment.Environment.Biomes.Length, Is.EqualTo(5));
                    foreach (GameObject biome in segment.Environment.Biomes)
                    {
                        Assert.That(biome, Is.Not.Null);
                        Assert.That(biome.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
                        Assert.That(biome.GetComponentsInChildren<Collider>(true), Is.Empty);
                        if (!biome.TryGetComponent(out BiomeScenery scenery)) continue;
                        Assert.That(scenery.Layouts.Length, Is.EqualTo(3));
                        foreach (Mesh mesh in scenery.Layouts)
                        {
                            Assert.That(mesh, Is.Not.Null);
                            Assert.That(mesh.vertexCount, Is.GreaterThan(100));
                        }
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator RecyclingVisitsEveryBiomeAndRestartRestoresCityWithoutGrowingThePool()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            EndlessTrack track = Object.FindAnyObjectByType<EndlessTrack>();
            track.Reset(42);
            AssertHorizonCoverage(track);
            int count = track.GetComponentsInChildren<Transform>(true).Length;
            bool[] visited = new bool[5];
            for (int step = 0; step < 400; step++)
            {
                track.Advance(5, 1000, 1000, 0, Vector2.one * 0.5f);
                visited[(int)track.Environment.CurrentBiome] = true;
                if (step % 17 == 0) AssertHorizonCoverage(track);
                foreach (TrackSegment segment in track.Segments)
                {
                    SegmentEnvironment scenery = segment.Environment;
                    Assert.That(scenery.CurrentBiome, Is.EqualTo(BiomeRules.AtDistance(
                        scenery.SectionIndex * RunnerRules.SegmentLength, track.Environment.SegmentsPerBiome)));
                    int active = 0;
                    for (int i = 0; i < scenery.Biomes.Length; i++)
                    {
                        if (!scenery.Biomes[i].activeSelf) continue;
                        active++;
                        Assert.That(i, Is.EqualTo((int)scenery.CurrentBiome));
                    }
                    Assert.That(active, Is.EqualTo(1));
                }
            }
            Assert.That(visited, Is.All.True);
            Assert.That(track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            track.Reset(42);
            AssertHorizonCoverage(track);
            Assert.That(track.Travelled, Is.Zero);
            Assert.That(track.Environment.CurrentBiome, Is.EqualTo(EnvironmentBiome.City));
            foreach (TrackSegment segment in track.Segments)
                Assert.That(segment.Environment.CurrentBiome, Is.EqualTo(EnvironmentBiome.City));
        }

        private static void AssertHorizonCoverage(EndlessTrack track)
        {
            TrackSegment rear = track.Segments[0], front = rear;
            foreach (TrackSegment segment in track.Segments)
            {
                if (segment.transform.localPosition.z < rear.transform.localPosition.z) rear = segment;
                if (segment.transform.localPosition.z > front.transform.localPosition.z) front = segment;
            }
            foreach (TrackSegment segment in track.Segments)
                foreach (GameObject biome in segment.Environment.Biomes)
                {
                    HorizonBackdrop backdrop = biome.GetComponent<HorizonBackdrop>();
                    Assert.That(backdrop, Is.Not.Null);
                    Assert.That(backdrop.CoversBehind, Is.EqualTo(biome.activeSelf && segment == rear));
                    Assert.That(backdrop.CoversAhead, Is.EqualTo(biome.activeSelf && segment == front));
                }
        }

        [UnityTest]
        public IEnumerator SceneryDoesNotChangeSeededGameplayPatterns()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            EndlessTrack track = Object.FindAnyObjectByType<EndlessTrack>();
            EnvironmentDirector director = track.Environment;
            int[] withScenery = PatternSignature(track);
            var settings = new SerializedObject(track);
            settings.FindProperty("environment").objectReferenceValue = null;
            settings.ApplyModifiedPropertiesWithoutUndo();
            int[] withoutScenery = PatternSignature(track);
            Assert.That(withScenery, Is.EqualTo(withoutScenery));
            settings.FindProperty("environment").objectReferenceValue = director;
            settings.ApplyModifiedPropertiesWithoutUndo();
            track.Reset(42);
        }

        private static int[] PatternSignature(EndlessTrack track)
        {
            track.Reset(31415);
            int[] signature = new int[60];
            for (int step = 0; step < signature.Length; step++)
            {
                track.Advance(42, 1000, 1000, 0.5f, Vector2.one * 0.5f);
                int hash = 17;
                foreach (TrackSegment segment in track.Segments)
                {
                    foreach (RunnerCube cube in segment.Cubes) hash = unchecked(hash * 31 + (int)cube.Color);
                    foreach (PowerUpPickup pickup in segment.PowerUps)
                        hash = unchecked(hash * 31 + (pickup.gameObject.activeSelf ? (int)pickup.Type + 1 : 0));
                }
                signature[step] = hash;
            }
            return signature;
        }

        [UnityTest]
        public IEnumerator AtmosphereBlendsContinuouslyAndPauseFreezesCloudsAndWater()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            EnvironmentDirector director = game.Track.Environment;
            Material savedSky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/CubeDashHorizon.mat");
            Color savedHorizon = savedSky.GetColor("_HorizonColor");
            Assert.That(RenderSettings.skybox, Is.Not.SameAs(savedSky), "Runtime sky must not modify the saved asset.");
            director.ApplyDistance(335.99f, 0);
            Color before = RenderSettings.fogColor;
            director.ApplyDistance(336.01f, 0);
            Color after = RenderSettings.fogColor;
            Assert.That(Vector4.Distance(before, after), Is.LessThan(0.002f));
            Assert.That(RenderSettings.skybox.GetColor("_HorizonColor"), Is.EqualTo(after));
            Assert.That(savedSky.GetColor("_HorizonColor"), Is.EqualTo(savedHorizon));
            game.StartRun();
            yield return null;
            game.TogglePause();
            float time = Shader.GetGlobalFloat("_CubeDashEnvironmentTime");
            Color fog = RenderSettings.fogColor;
            yield return null;
            yield return null;
            Assert.That(Shader.GetGlobalFloat("_CubeDashEnvironmentTime"), Is.EqualTo(time));
            Assert.That(RenderSettings.fogColor, Is.EqualTo(fog));
            game.StartRun();
            Assert.That(Shader.GetGlobalFloat("_CubeDashEnvironmentTime"), Is.Zero);
            Assert.That(director.CurrentBiome, Is.EqualTo(EnvironmentBiome.City));
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
