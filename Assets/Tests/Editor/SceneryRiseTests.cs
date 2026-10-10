using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class SceneryRiseTests
    {
        [Test]
        public void SmoothStaggerStartsBelowGroundAndFinishesWithoutOvershoot()
        {
            Assert.That(SceneryRise.Progress(0, 0, 1.25f), Is.Zero);
            Assert.That(SceneryRise.Progress(0.4f, 0.55f, 1.25f), Is.Zero);
            Assert.That(SceneryRise.Progress(3, 0.55f, 1.25f), Is.EqualTo(1));
            Assert.That(SceneryRise.Delay(12, 38, 0.55f), Is.GreaterThan(SceneryRise.Delay(12, 4, 0.55f)));
            float previous = 0;
            for (float age = 0; age < 3; age += 0.03f)
            {
                float progress = SceneryRise.Progress(age, 0.2f, 1.25f);
                Assert.That(progress, Is.InRange(previous, 1)); previous = progress;
            }
        }

        [Test]
        public void SavedObjectsHaveRigidRevealAnchorsAndRoomForDisplacement()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab");
            Assert.That(prefab.GetComponent<SceneryRise>(), Is.Not.Null);
            foreach (GameObject biome in prefab.GetComponent<SegmentEnvironment>().Biomes)
            {
                var meshes = new System.Collections.Generic.HashSet<Mesh>();
                BiomeScenery scenery = biome.GetComponent<BiomeScenery>();
                if (scenery != null) foreach (Mesh mesh in scenery.Layouts) meshes.Add(mesh);
                foreach (MeshFilter filter in biome.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.name.StartsWith("Boundary ") || filter.name == "Coastal Headland") meshes.Add(filter.sharedMesh);
                foreach (Mesh mesh in meshes)
                {
                    var anchors = new System.Collections.Generic.List<Vector2>(); mesh.GetUVs(2, anchors);
                    Assert.That(anchors.Count, Is.EqualTo(mesh.vertexCount));
                    float minimum = float.MaxValue;
                    foreach (Vector3 vertex in mesh.vertices) minimum = Mathf.Min(minimum, vertex.y);
                    Assert.That(mesh.bounds.min.y, Is.LessThanOrEqualTo(minimum - 79.9f));
                    int[] triangles = mesh.triangles;
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        Assert.That(anchors[triangles[i]], Is.EqualTo(anchors[triangles[i + 1]]));
                        Assert.That(anchors[triangles[i]], Is.EqualTo(anchors[triangles[i + 2]]), "Connected objects must rise rigidly, not stretch between rows.");
                    }
                }
            }
        }

        [Test]
        public void RiseOnlyMovesSceneryAndPreservesBoundaryPropertyBlocksAndCityScale()
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab"));
            try
            {
                SegmentEnvironment environment = root.GetComponent<SegmentEnvironment>(); environment.Configure(7, 8);
                SceneryRise rise = root.GetComponent<SceneryRise>();
                Transform building = null;
                foreach (Transform child in environment.Biomes[0].transform)
                    if (child.name.Contains("Skyscraper") && child.gameObject.activeSelf) { building = child; break; }
                Assert.That(building, Is.Not.Null);
                Vector3 home = building.localPosition, scale = building.localScale;
                var ground = environment.Biomes[0].transform.Find("City Ground"); Vector3 groundHome = ground.localPosition;
                Renderer dressing = environment.Biomes[0].transform.Find("Boundary Exit Scenery").GetComponent<Renderer>();
                var block = new MaterialPropertyBlock(); dressing.GetPropertyBlock(block);
                block.SetFloat("_TestPreserved", 0.73f); dressing.SetPropertyBlock(block);
                rise.Begin(true, 10);
                Assert.That(building.localPosition.y, Is.EqualTo(home.y - 80).Within(0.001f));
                rise.Tick(10.9f); Vector3 midway = building.localPosition;
                Assert.That(midway.y, Is.GreaterThan(home.y - 80)); Assert.That(midway.y, Is.LessThan(home.y));
                rise.Tick(10.9f); Assert.That(building.localPosition, Is.EqualTo(midway), "Frozen shared clock freezes reveal.");
                rise.Tick(13); Assert.That(rise.Rising, Is.False); Assert.That(building.localPosition, Is.EqualTo(home));
                Assert.That(building.localScale, Is.EqualTo(scale)); Assert.That(ground.localPosition, Is.EqualTo(groundHome));
                dressing.GetPropertyBlock(block); Assert.That(block.GetFloat("_TestPreserved"), Is.EqualTo(0.73f));
                rise.Begin(true, 15); rise.Begin(false, 0); Assert.That(building.localPosition, Is.EqualTo(home));
                Assert.That(root.GetComponent<TrackSegment>().Cubes[0].transform.localPosition.y, Is.GreaterThan(-1));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator RecycledSceneryRisesAndPauseAndRestartPreserveThePool()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun();
            foreach (TrackSegment segment in game.Track.Segments)
            {
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
            int count = game.Track.GetComponentsInChildren<Transform>(true).Length;
            for (int i = 0; i < game.Track.Segments.Length; i++)
                Assert.That(game.Track.Segments[i].GetComponent<SceneryRise>().Rising,
                    Is.EqualTo(i == game.Track.Segments.Length - 1), "Only the farthest starting section may rise.");
            float originalSpawnZ = game.Track.Segments[game.Track.Segments.Length - 1].transform.position.z;
            Assert.That(originalSpawnZ, Is.GreaterThanOrEqualTo(250), "Keep the original distant spawn slot.");
            game.Track.Advance(50, 1000, 1000, 0, Vector2.one * 0.5f);
            SceneryRise recycled = null;
            foreach (TrackSegment segment in game.Track.Segments)
                if (segment.Environment.SectionIndex >= 7) recycled = segment.GetComponent<SceneryRise>();
            Assert.That(recycled, Is.Not.Null); Assert.That(recycled.Rising, Is.True);
            Assert.That(recycled.transform.position.z, Is.EqualTo(originalSpawnZ - 50 + RunnerRules.SegmentLength).Within(0.001f),
                "Recycling keeps the original far-ahead placement.");
            game.TogglePause(); float time = Shader.GetGlobalFloat("_CubeDashEnvironmentTime");
            yield return null; yield return null;
            Assert.That(Shader.GetGlobalFloat("_CubeDashEnvironmentTime"), Is.EqualTo(time));
            Assert.That(recycled.Rising, Is.True);
            game.StartRun();
            for (int i = 0; i < game.Track.Segments.Length; i++)
                Assert.That(game.Track.Segments[i].GetComponent<SceneryRise>().Rising, Is.EqualTo(i == game.Track.Segments.Length - 1));
            Assert.That(game.Track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
