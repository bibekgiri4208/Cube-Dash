using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CubeDash.Tests
{
    public sealed class OceanWaveTests
    {
        [Test]
        public void OceanHasDenseValidGeometryAndApronEdgesMatchEachPooledSection()
        {
            GameObject beach = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Beach.prefab");
            Mesh ocean = beach.transform.Find("Ocean").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(ocean.vertexCount, Is.InRange(8000, 16000));
            float minX = float.MaxValue;
            foreach (Vector3 vertex in ocean.vertices) minX = Mathf.Min(minX, vertex.x);
            Assert.That(minX, Is.EqualTo(5.2f).Within(0.001f), "The surface covers the full tidal shoreline range.");
            Assert.That(beach.GetComponentsInChildren<Collider>(true), Is.Empty);
            foreach (string edge in new[] { "Rear Horizon", "Forward Horizon" })
            {
                Transform apron = beach.transform.Find(edge + "/Ocean Apron");
                Mesh mesh = apron.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh, Is.Not.SameAs(ocean), "Horizon geometry must not stretch a sparse ocean tile over a kilometre.");
                Assert.That(mesh.vertexCount, Is.InRange(15000, 30000));
                foreach (Vector3 tileVertex in ocean.vertices)
                    if (Mathf.Abs(tileVertex.z) < 0.001f)
                    {
                        bool foundStart = false, foundEnd = false;
                        foreach (Vector3 vertex in mesh.vertices)
                            if (Mathf.Abs(vertex.x - tileVertex.x) < 0.001f)
                            {
                                foundStart |= Mathf.Abs(vertex.z) < 0.001f;
                                foundEnd |= Mathf.Abs(vertex.z - RunnerRules.SegmentLength) < 0.001f;
                            }
                        Assert.That(foundStart && foundEnd, Is.True, "Both apron seam rings match the regular ocean's columns.");
                    }
                ValidateMesh(mesh);
            }
            ValidateMesh(ocean);
        }

        [Test]
        public void BeachFloorPreservesDryLandAndSeabedDepthRelativeToTheRaisedLandscape()
        {
            GameObject beach = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Beach.prefab");
            Transform floor = beach.transform.Find("Landscape Ground");
            foreach (Vector3 vertex in floor.GetComponent<MeshFilter>().sharedMesh.vertices)
            {
                Vector3 landscape = beach.transform.InverseTransformPoint(floor.TransformPoint(vertex));
                if (landscape.x <= 5.2f) Assert.That(landscape.y, Is.EqualTo(-9).Within(0.001f), "Dry beach stays level relative to its landscape root.");
                if (landscape.x >= 12) Assert.That(landscape.y, Is.LessThanOrEqualTo(-12.99f), "The seabed cannot poke through the bigger swells.");
            }
            Assert.That(floor.localScale.x, Is.EqualTo(2048));
            Assert.That(beach.transform.Find("Landscape Details").GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
            Assert.That(floor.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(beach.transform.Find("Forward Horizon/Ground Apron").GetComponent<MeshFilter>().sharedMesh));
            Assert.That(beach.transform.Find("Shoreline").GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/Shoreline Sand.asset")));
        }

        [Test]
        public void OceanAndItsHorizonStayBelowDryLandEvenAtMaximumTideAndSwell()
        {
            GameObject beach = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Beach.prefab");
            Transform ocean = beach.transform.Find("Ocean");
            Material water = ocean.GetComponent<Renderer>().sharedMaterial;
            float landHeight = beach.transform.TransformPoint(Vector3.down * 9).y;
            // Four shader swell weights sum to 1.77; the shore depression can only lower this bound.
            float maximumRise = water.GetFloat("_WaveHeight") * 1.77f + water.GetFloat("_TideHeight");
            Assert.That(ocean.position.y + maximumRise, Is.LessThan(landHeight),
                "Even combined crests at high tide must not rise above the highway-side dry ground.");
            Assert.That(ocean.position.y, Is.EqualTo(landHeight - 3.4f).Within(0.001f));
            foreach (string edge in new[] { "Rear Horizon", "Forward Horizon" })
                Assert.That(beach.transform.Find(edge + "/Ocean Apron").position.y,
                    Is.EqualTo(ocean.position.y).Within(0.001f), "Horizon water must meet the visible ocean without a height step.");

            Transform floor = beach.transform.Find("Landscape Ground");
            foreach (string name in new[] { "Beach Seabed", "Beach Transition Floor" })
                foreach (Vector3 vertex in AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/" + name + ".asset").vertices)
                {
                    Vector3 world = floor.TransformPoint(vertex);
                    if (world.x >= 12) Assert.That(world.y, Is.LessThan(ocean.position.y - maximumRise),
                        "Both normal and boundary seabeds must stay below the lowered ocean's deepest trough.");
                }
            foreach (Mesh layout in beach.GetComponent<BiomeScenery>().Layouts)
            {
                Vector3[] vertices = layout.vertices;
                var anchors = new System.Collections.Generic.List<Vector2>(); layout.GetUVs(2, anchors);
                Assert.That(anchors.Count, Is.EqualTo(vertices.Length));
                float postTop = float.MinValue, postBottom = float.MaxValue;
                float deckTop = float.MinValue;
                foreach (int index in layout.GetTriangles(9))
                    if (vertices[index].x > 10.5f)
                    {
                        float height = beach.transform.TransformPoint(vertices[index]).y;
                        postTop = Mathf.Max(postTop, height); postBottom = Mathf.Min(postBottom, height);
                        if (Mathf.Abs(vertices[index].z - anchors[index].y) > 1)
                            deckTop = Mathf.Max(deckTop, height);
                    }
                Assert.That(postTop - ocean.position.y, Is.EqualTo(3.82f).Within(0.001f));
                Assert.That(postBottom, Is.LessThan(ocean.position.y), "Jetty support feet must remain submerged, not float above the lowered sea.");
                Assert.That(deckTop, Is.GreaterThan(ocean.position.y), "The wooden deck must remain above water.");
                Assert.That(deckTop, Is.LessThan(landHeight - 0.75f), "The actual deck surface should sit visibly below the highway-side beach.");
                float rockBottom = float.MaxValue;
                foreach (int index in layout.GetTriangles(3))
                    if (vertices[index].x > 8) rockBottom = Mathf.Min(rockBottom, beach.transform.TransformPoint(vertices[index]).y);
                Assert.That(rockBottom, Is.EqualTo(ocean.position.y - 1.2f).Within(0.001f),
                    "Seaside boulders follow the lower water instead of hovering above it.");
            }
        }

        [Test]
        public void WaterAndWetSandShareTideSettingsButTheJungleRiverHasNoOceanSurf()
        {
            Material ocean = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Environments/Lagoon Water.mat");
            Assert.That(ocean.GetFloat("_WaveHeight"), Is.InRange(0.5f, 0.9f));
            Assert.That(ocean.GetFloat("_Choppiness"), Is.InRange(0.4f, 0.7f));
            Assert.That(ocean.GetFloat("_FoamStrength"), Is.GreaterThan(0.7f));
            Assert.That(ocean.GetFloat("_TideHeight"), Is.InRange(0.08f, 0.2f));
            Assert.That(ocean.GetFloat("_ShoreX"), Is.EqualTo(9), "Foam and wet sand follow the lowered sea's shoreline.");
            foreach (string name in new[] { "Coastal Sand", "Wet Sand" })
            {
                Material sand = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Environments/" + name + ".mat");
                Assert.That(sand.GetFloat("_CoastalWetness"), Is.EqualTo(1));
                foreach (string setting in new[] { "_ShoreX", "_TideDistance", "_TidePeriod" })
                    Assert.That(sand.GetFloat(setting), Is.EqualTo(ocean.GetFloat(setting)));
            }
            Material dry = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Environments/Golden Sand.mat");
            Assert.That(dry.GetFloat("_CoastalWetness"), Is.Zero, "The Desert must not acquire animated wet sand.");
            Material river = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Environments/River Water.mat");
            Assert.That(river.GetFloat("_ShoreMode"), Is.Zero);
            Assert.That(river.GetFloat("_WaveHeight"), Is.EqualTo(0.035f).Within(0.001f));
            Assert.That(ocean.FindPass("DepthOnly"), Is.GreaterThanOrEqualTo(0));
            Assert.That(ocean.FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0));
        }

        [UnityTest]
        public IEnumerator WavesAndTidesFreezeTogetherOnPauseAndResetWithoutGrowingThePool()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun();
            foreach (TrackSegment segment in game.Track.Segments)
            {
                GameObject beach = segment.Environment.Biomes[(int)EnvironmentBiome.Beach];
                Assert.That(beach.transform.Find("Ocean").GetComponent<MeshFilter>().sharedMesh.vertexCount,
                    Is.GreaterThan(8000), "The saved playable scene must inherit the improved ocean without a setup command.");
                Assert.That(beach.transform.Find("Landscape Ground").GetComponent<MeshFilter>().sharedMesh,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/Beach Seabed.asset")));
                Assert.That(beach.transform.Find("Landscape Ground").GetComponent<Renderer>().sharedMaterial.GetFloat("_CoastalWetness"),
                    Is.EqualTo(1));
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
            int objects = game.Track.GetComponentsInChildren<Transform>(true).Length;
            game.Track.Environment.ApplyDistance(1400, 12);
            float movingTime = Shader.GetGlobalFloat("_CubeDashEnvironmentTime");
            game.TogglePause(); yield return null; yield return null;
            Assert.That(Shader.GetGlobalFloat("_CubeDashEnvironmentTime"), Is.EqualTo(movingTime));
            game.TogglePause(); yield return null; yield return null;
            Assert.That(Shader.GetGlobalFloat("_CubeDashEnvironmentTime"), Is.GreaterThan(movingTime));
            game.StartRun(); Assert.That(Shader.GetGlobalFloat("_CubeDashEnvironmentTime"), Is.Zero);
            Assert.That(game.Track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
        }

        private static void ValidateMesh(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices; int[] indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
                Assert.That(Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]],
                    vertices[indices[i + 2]] - vertices[indices[i]]).sqrMagnitude, Is.GreaterThan(1e-10f));
            Assert.That(mesh.bounds.size.y, Is.GreaterThanOrEqualTo(6));
        }
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
