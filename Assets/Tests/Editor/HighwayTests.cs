using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CubeDash.Tests
{
    public sealed class HighwayTests
    {
        private const string TrackPath = "Assets/Prefab/CubeDash/TrackSegment.prefab";

        [Test]
        public void AsphaltHasRestrainedGrainAndReliefInsteadOfRoughSparklingBumps()
        {
            Material asphalt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/CubeDashRoad.mat");
            Assert.That(asphalt.GetFloat("_MicroNormalStrength"), Is.InRange(0, 0.01f));
            Assert.That(asphalt.GetFloat("_Smoothness"), Is.InRange(0.18f, 0.3f), "Smooth, not wet or mirror-like.");
            Color contrast = asphalt.GetColor("_BaseColor") - asphalt.GetColor("_SecondaryColor");
            Assert.That(Mathf.Max(Mathf.Abs(contrast.r), Mathf.Max(Mathf.Abs(contrast.g), Mathf.Abs(contrast.b))), Is.LessThan(0.025f));
            Material shoulder = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Environments/Highway Shoulder.mat");
            Assert.That(shoulder.GetFloat("_MicroNormalStrength"), Is.InRange(0, 0.02f));
        }

        [Test]
        public void HighwayUsesNonBlueAsphaltAndSavedColliderFreeRoadDressing()
        {
            GameObject track = AssetDatabase.LoadAssetAtPath<GameObject>(TrackPath);
            Transform road = track.transform.Find("Road");
            Material asphalt = road.GetComponent<Renderer>().sharedMaterial;
            Assert.That(asphalt.shader.name, Is.EqualTo("CubeDash/Environment Surface"));
            Assert.That(asphalt.GetFloat("_DetailType"), Is.EqualTo(7));
            Color color = asphalt.GetColor("_BaseColor");
            Assert.That(Mathf.Max(color.r, Mathf.Max(color.g, color.b)), Is.LessThan(0.25f));
            Assert.That(Mathf.Abs(color.b - color.r), Is.LessThan(0.05f));
            Assert.That(asphalt.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
            Assert.That(asphalt.FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0));
            Assert.That(ShaderUtil.ShaderHasError(asphalt.shader), Is.False);
            Assert.That(road.localPosition.y + road.localScale.y * 0.5f, Is.EqualTo(0).Within(0.0001f));
            Assert.That(track.transform.Find("Bridge Rails"), Is.Null);
            Assert.That(track.transform.Find("Matte Road Edges"), Is.Null);
            foreach (string name in new[] { "Highway Lane Markings", "Highway Shoulders", "Highway Guardrails" })
            {
                Transform part = track.transform.Find(name);
                Assert.That(part, Is.Not.Null, name);
                Assert.That(part.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(part.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
                Assert.That(part.GetComponent<Renderer>().sharedMaterial, Is.Not.Null);
            }
            Mesh rails = track.transform.Find("Highway Guardrails").GetComponent<MeshFilter>().sharedMesh;
            foreach (Vector3 vertex in rails.vertices) Assert.That(Mathf.Abs(vertex.x), Is.GreaterThan(4.9f));
            Mesh paint = track.transform.Find("Highway Lane Markings").GetComponent<MeshFilter>().sharedMesh;
            Assert.That(paint.vertexCount, Is.GreaterThan(300), "Two continuous edges and fourteen lane dashes are baked.");
        }

        [Test]
        public void AllLandscapesAndTheirModelsShareNearRoadGroundElevation()
        {
            GameObject track = AssetDatabase.LoadAssetAtPath<GameObject>(TrackPath);
            foreach (GameObject biome in track.GetComponent<SegmentEnvironment>().Biomes)
            {
                Assert.That(biome.transform.localPosition.y, Is.EqualTo(8.75f).Within(0.001f));
                Transform floor = biome.transform.Find(biome.name.StartsWith("City") ? "City Ground" : "Landscape Ground");
                float ground = floor.position.y + floor.localScale.y * 0.5f;
                Assert.That(ground, Is.EqualTo(-0.25f).Within(0.001f), biome.name);
                Assert.That(biome.transform.Find("Landscape Details").localPosition, Is.EqualTo(Vector3.zero),
                    "Move models and terrain together; do not move their individual baked parts.");
                foreach (string edge in new[] { "Rear Horizon", "Forward Horizon" })
                {
                    Transform apron = biome.transform.Find(edge + "/Ground Apron");
                    Assert.That(apron.position.y, Is.EqualTo(floor.position.y).Within(0.001f));
                }
                Assert.That(biome.GetComponentsInChildren<Collider>(true), Is.Empty);
            }
            foreach (EnvironmentBiome biome in new[] { EnvironmentBiome.Jungle, EnvironmentBiome.Mountains,
                EnvironmentBiome.Desert, EnvironmentBiome.Beach })
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/" + biome + ".prefab");
                Assert.That(source.transform.localPosition.y, Is.EqualTo(8.75f).Within(0.001f));
            }
        }

        [Test]
        public void ShouldersAndRailsJoinAtSegmentSeamsAndSlopeIntoTheTerrain()
        {
            GameObject track = AssetDatabase.LoadAssetAtPath<GameObject>(TrackPath);
            foreach (string name in new[] { "Highway Shoulders", "Highway Guardrails" })
            {
                Vector3[] vertices = track.transform.Find(name).GetComponent<MeshFilter>().sharedMesh.vertices;
                foreach (Vector3 vertex in vertices)
                    if (Mathf.Abs(vertex.z) < 0.001f)
                        Assert.That(System.Array.Exists(vertices, other => Mathf.Abs(other.x - vertex.x) < 0.001f
                            && Mathf.Abs(other.y - vertex.y) < 0.001f && Mathf.Abs(other.z - 42) < 0.001f), Is.True, name);
            }
            Mesh shoulder = track.transform.Find("Highway Shoulders").GetComponent<MeshFilter>().sharedMesh;
            foreach (Vector3 vertex in shoulder.vertices)
                Assert.That(vertex.y, Is.EqualTo(Mathf.Abs(vertex.x) > 5 ? -0.25f : 0).Within(0.001f));
            Assert.That(shoulder.normals, Has.All.Matches<Vector3>(normal => normal.y > 0.9f));
        }

        [Test]
        public void RaisedRiverAndBeachBoundariesUseTheNewLandElevationAfterRecycling()
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TrackPath));
            try
            {
                root.transform.position = new Vector3(0, 2, 400);
                SegmentEnvironment environment = root.GetComponent<SegmentEnvironment>();
                BiomeBoundaryBlend blend = root.GetComponent<BiomeBoundaryBlend>();
                int count = root.GetComponentsInChildren<Transform>(true).Length;
                foreach (int section in new[] { 8, 15, 32, 39, 40, 11, 36 })
                {
                    environment.Configure(section, 8);
                    foreach (BiomeBoundaryBlend.Region region in blend.Regions)
                        foreach (Renderer floor in region.Ground)
                        {
                            var block = new MaterialPropertyBlock(); floor.GetPropertyBlock(block);
                            Assert.That(block.GetFloat("_BoundaryGroundHeight"), Is.EqualTo(1.75f).Within(0.001f));
                        }
                    foreach (GameObject biome in environment.Biomes)
                        Assert.That(biome.transform.localPosition.y, Is.EqualTo(8.75f).Within(0.001f));
                }
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SavedLevelInheritsTheHighwayWithoutMovingGameplay()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                CubeDashGame game = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.TryGetComponent(out CubeDashGame candidate)) game = candidate;
                Assert.That(game, Is.Not.Null);
                Assert.That(game.Player.position.y, Is.EqualTo(0.595f).Within(0.001f));
                foreach (TrackSegment segment in game.Track.Segments)
                {
                    Assert.That(segment.transform.position.y, Is.EqualTo(0));
                    Assert.That(segment.transform.Find("Highway Lane Markings"), Is.Not.Null);
                    foreach (GameObject biome in segment.Environment.Biomes)
                        Assert.That(biome.transform.localPosition.y, Is.EqualTo(8.75f).Within(0.001f));
                    foreach (BoxCollider obstacle in segment.Obstacles)
                        Assert.That(obstacle.transform.localPosition.y, Is.EqualTo(0.575f).Within(0.001f));
                    Transform plane = segment.transform.Find("Plane");
                    if (plane != null)
                    {
                        Assert.That(plane.position.y, Is.EqualTo(0));
                        Assert.That(plane.GetComponent<Renderer>().sharedMaterial.GetFloat("_DetailType"), Is.EqualTo(7));
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
