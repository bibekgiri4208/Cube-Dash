using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CubeDash.Tests
{
    public sealed class HighwayProgressionTests
    {
        [TestCase(0, 1)]
        [TestCase(179, 1)]
        [TestCase(180, 2)]
        [TestCase(480, 3)]
        [TestCase(900, 4)]
        [TestCase(1500, 5)]
        public void ProgressionIntroducesEncountersByDistance(float distance, int stage)
        {
            Assert.That(LevelProgression.Stage(distance), Is.EqualTo(stage));
            Assert.That(LevelProgression.Difficulty(distance), Is.EqualTo(Mathf.Clamp01(distance / 1500)));
            Assert.That(LevelProgression.Difficulty(-10), Is.Zero);
            Assert.That(LevelProgression.Difficulty(5000), Is.EqualTo(1));
        }

        [Test]
        public void PatternsAreDeterministicAndLeaveAnAdjacentRouteFromEveryOpenLane()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var patterns = new HighwayPatterns(seed);
                var same = new HighwayPatterns(seed);
                int previousLane = 1, previousMask = 0;
                for (int i = 0; i < 600; i++)
                {
                    HighwayPatterns.Row row = patterns.Next(i * RunnerRules.RowSpacing);
                    Assert.That(same.Next(i * RunnerRules.RowSpacing), Is.EqualTo(row));
                    Assert.That(row.BlockedMask, Is.InRange(0, 6));
                    Assert.That(row.BlockedMask & (1 << row.SafeLane), Is.Zero);
                    Assert.That(Mathf.Abs(row.SafeLane - previousLane), Is.LessThanOrEqualTo(1));
                    Assert.That(LevelProgression.ReachableFromEveryOpenLane(previousMask, row.BlockedMask), Is.True);
                    previousLane = row.SafeLane; previousMask = row.BlockedMask;
                }
            }
        }

        [Test]
        public void LaterEncountersContainTrafficGatesSlalomsAndRegularRecovery()
        {
            var patterns = new HighwayPatterns(42);
            int seen = 0, recovery = 0, narrow = 0, sinceRecovery = 0;
            for (int i = 0; i < 400; i++)
            {
                HighwayPatterns.Row row = patterns.Next(1500 + i * 14);
                seen |= 1 << (int)row.Hazard;
                if (row.Encounter == HighwayEncounter.Recovery)
                {
                    Assert.That(row.BlockedMask, Is.Zero);
                    recovery++; sinceRecovery = 0;
                }
                else sinceRecovery++;
                Assert.That(sinceRecovery, Is.LessThanOrEqualTo(18));
                if ((row.BlockedMask & (row.BlockedMask - 1)) != 0) narrow++;
            }
            Assert.That(seen & 14, Is.EqualTo(14), "Barriers, vans and trucks all appear.");
            Assert.That(recovery, Is.GreaterThan(40));
            Assert.That(narrow, Is.GreaterThan(100));
        }

        [Test]
        public void TheOpeningHasNoTrafficAndGivesBreathingRoom()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                HighwayPatterns.Row first = new HighwayPatterns(seed).Next(21);
                Assert.That(first.SafeLane, Is.EqualTo(1));
                Assert.That(first.BlockedMask, Is.Zero);
            }
            var patterns = new HighwayPatterns(7);
            int open = 0;
            for (int i = 0; i < 13; i++)
            {
                HighwayPatterns.Row row = patterns.Next(i * 14);
                Assert.That(row.Hazard, Is.EqualTo(HighwayHazard.Cube));
                if (i < 3) Assert.That(row.BlockedMask, Is.Zero);
                if (row.BlockedMask == 0) open++;
            }
            Assert.That(open, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void LongVehiclesStillAllowSafeLaneChangesAtTheSavedMaximumSpeed()
        {
            var patterns = new HighwayPatterns(42);
            HighwayPatterns.Row previous = patterns.Next(1500);
            const float speed = 30, dt = 1f / 240;
            for (int i = 1; i < 500; i++)
            {
                HighwayPatterns.Row next = patterns.Next(1500 + i * 14);
                float oldDepth = HighwayPresentation.HazardSize(previous.Hazard).z;
                float newDepth = HighwayPresentation.HazardSize(next.Hazard).z;
                float rowZ = (oldDepth + RunnerRules.PlayerSize) * 0.5f + 0.01f;
                float x = (previous.SafeLane - 1) * 2.6f, velocity = 0;
                for (float elapsed = 0; elapsed < RunnerRules.RowSpacing / speed; elapsed += dt)
                {
                    float nextX = Mathf.SmoothDamp(x, (next.SafeLane - 1) * 2.6f, ref velocity, 0.085f, 18, dt);
                    for (int lane = 0; lane < 3; lane++)
                    {
                        if ((previous.BlockedMask & (1 << lane)) != 0)
                            Assert.That(RunnerRules.SweptHit(new Vector2(x, 0), new Vector2(nextX, 0),
                                new Vector2((lane - 1) * 2.6f, -rowZ), new Vector2((lane - 1) * 2.6f, -rowZ - speed * dt),
                                new Vector2((HighwayPresentation.HazardSize(previous.Hazard).x + 0.9f) * 0.5f, (oldDepth + 0.9f) * 0.5f)), Is.False);
                        if ((next.BlockedMask & (1 << lane)) != 0)
                            Assert.That(RunnerRules.SweptHit(new Vector2(x, 0), new Vector2(nextX, 0),
                                new Vector2((lane - 1) * 2.6f, 14 - rowZ), new Vector2((lane - 1) * 2.6f, 14 - rowZ - speed * dt),
                                new Vector2((HighwayPresentation.HazardSize(next.Hazard).x + 0.9f) * 0.5f, (newDepth + 0.9f) * 0.5f)), Is.False,
                                $"Unsafe {previous.Hazard} to {next.Hazard} row {i}");
                    }
                    x = nextX; rowZ += speed * dt;
                }
                previous = next;
            }
        }

        [Test]
        public void BendsLeaveContactSpaceUntouchedAndTurnBothDirections()
        {
            bool left = false, right = false;
            float phase = RoadCurve.Phase(42);
            for (float distance = 0; distance < 3000; distance += 5)
            {
                foreach (float z in new[] { -30f, 0, 2, 6 }) Assert.That(RoadCurve.Offset(z, distance, phase), Is.Zero);
                float offset = RoadCurve.Offset(100, distance, phase);
                Assert.That(float.IsNaN(offset), Is.False);
                Assert.That(Mathf.Abs(RoadCurve.Offset(180, distance, phase)), Is.LessThan(170));
                Assert.That(Mathf.Abs(RoadCurve.Offset(100, distance + 0.1f, phase) - offset), Is.LessThan(0.15f));
                Assert.That(RoadCurve.Offset(100, distance, phase, 0), Is.Zero);
                left |= offset < -5; right |= offset > 5;
            }
            Assert.That(left && right, Is.True);
        }

        [Test]
        public void LongitudinalSlicesKeepMeshChannelsSubmeshesAndRoadSeams()
        {
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            Mesh mesh = RoadMeshSubdivision.Create(cube, new Vector3(0, 0, 42));
            try
            {
                Assert.That(mesh, Is.Not.SameAs(cube));
                Assert.That(mesh.subMeshCount, Is.EqualTo(cube.subMeshCount));
                Assert.That(mesh.bounds.size, Is.EqualTo(cube.bounds.size));
                Vector3[] vertices = mesh.vertices;
                int[] indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    float a = vertices[indices[i]].z * 42, b = vertices[indices[i + 1]].z * 42, c = vertices[indices[i + 2]].z * 42;
                    Assert.That(Mathf.Max(a, Mathf.Max(b, c)) - Mathf.Min(a, Mathf.Min(b, c)), Is.LessThanOrEqualTo(3.001f));
                }
                Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.uv3.Length, Is.EqualTo(mesh.vertexCount));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void CurvedMaterialCopiesPreserveAuthoredColorsAndSurfaceProperties()
        {
            var presentation = new HighwayPresentation();
            try
            {
                foreach (string path in new[] { "Assets/Material/CubeDashRed.mat", "Assets/Material/CubeDashBlue.mat", "Assets/Material/Coins/Coin Gold.mat" })
                {
                    Material source = AssetDatabase.LoadAssetAtPath<Material>(path);
                    Material curved = presentation.MaterialFor(source);
                    Assert.That(Vector4.Distance(curved.GetColor("_BaseColor"), source.GetColor("_BaseColor")), Is.LessThan(0.00001f));
                    Assert.That(Vector4.Distance(curved.GetColor("_EmissionColor"), source.GetColor("_EmissionColor")), Is.LessThan(0.00001f));
                    Assert.That(curved.GetFloat("_Metallic"), Is.EqualTo(source.GetFloat("_Metallic")));
                    Assert.That(curved.GetFloat("_Smoothness"), Is.EqualTo(source.GetFloat("_Smoothness")));
                    Assert.That(presentation.MaterialFor(source), Is.SameAs(curved));
                }
            }
            finally { presentation.Dispose(); }
        }

        [Test]
        public void CurvedShadersCompileAndOpaqueShadersKeepDepthAndShadowPasses()
        {
            foreach (string name in new[] { "CubeDash/Curved Lit", "CubeDash/Environment Surface", "CubeDash/Biome Water", "CubeDash/City Glass", "CubeDash/Supercar Glass", "CubeDash/Wake Glow" })
            {
                Shader shader = Shader.Find(name);
                Assert.That(shader, Is.Not.Null, name);
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, name);
            }
            var material = new Material(Resources.Load<Shader>("CubeDashCurvedLit"));
            try
            {
                material.color = Color.red;
                Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.red));
                foreach (string pass in new[] { "ShadowCaster", "DepthOnly", "DepthNormals" }) Assert.That(material.FindPass(pass), Is.GreaterThanOrEqualTo(0));
            }
            finally { Object.DestroyImmediate(material); }
        }
    }
}
