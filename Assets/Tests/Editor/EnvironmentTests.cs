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
            int count = track.GetComponentsInChildren<Transform>(true).Length;
            bool[] visited = new bool[5];
            for (int step = 0; step < 400; step++)
            {
                track.Advance(5, 1000, 1000, 0, Vector2.one * 0.5f);
                visited[(int)track.Environment.CurrentBiome] = true;
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
            Assert.That(track.Travelled, Is.Zero);
            Assert.That(track.Environment.CurrentBiome, Is.EqualTo(EnvironmentBiome.City));
            foreach (TrackSegment segment in track.Segments)
                Assert.That(segment.Environment.CurrentBiome, Is.EqualTo(EnvironmentBiome.City));
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
