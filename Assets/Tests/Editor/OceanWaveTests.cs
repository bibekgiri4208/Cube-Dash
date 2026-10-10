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
        public void BeachFloorSlopesBelowWaveTroughsWithoutChangingTheDryBeachOrScenery()
        {
            GameObject beach = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Beach.prefab");
            Transform floor = beach.transform.Find("Landscape Ground");
            foreach (Vector3 vertex in floor.GetComponent<MeshFilter>().sharedMesh.vertices)
            {
                Vector3 world = floor.TransformPoint(vertex);
                if (world.x <= 5.2f) Assert.That(world.y, Is.EqualTo(-9).Within(0.001f), "Dry beach stays at its original height.");
                if (world.x >= 12) Assert.That(world.y, Is.LessThanOrEqualTo(-12.99f), "The seabed cannot poke through the bigger swells.");
            }
            Assert.That(floor.localScale.x, Is.EqualTo(2048));
            Assert.That(beach.transform.Find("Landscape Details").GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
            Assert.That(floor.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(beach.transform.Find("Forward Horizon/Ground Apron").GetComponent<MeshFilter>().sharedMesh));
            Assert.That(beach.transform.Find("Shoreline").GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/Shoreline Sand.asset")));
        }

        [Test]
        public void WaterAndWetSandShareTideSettingsButTheJungleRiverHasNoOceanSurf()
        {
            Material ocean = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Environments/Lagoon Water.mat");
            Assert.That(ocean.GetFloat("_WaveHeight"), Is.InRange(0.5f, 0.9f));
            Assert.That(ocean.GetFloat("_Choppiness"), Is.InRange(0.4f, 0.7f));
            Assert.That(ocean.GetFloat("_FoamStrength"), Is.GreaterThan(0.7f));
            Assert.That(ocean.GetFloat("_TideHeight"), Is.InRange(0.08f, 0.2f));
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
