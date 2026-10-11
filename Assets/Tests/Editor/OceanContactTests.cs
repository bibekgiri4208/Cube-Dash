using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class OceanContactTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ActualWaterShaderMeetsTheSavedTriangleBedAtShoreAndBothCurvedBaysThroughoutTheTide(int mode)
        {
            Shader shader = Shader.Find("Hidden/CubeDash/Beach Water Probe");
            Assert.That(shader, Is.Not.Null);
            bool asynchronous = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var probe = new Material(shader);
            var target = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            RenderTexture oldTarget = RenderTexture.active;
            try
            {
                Material ocean = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Environments/Lagoon Water.mat");
                foreach (string name in new[] { "_WaveHeight", "_ShoreMode", "_ShoreX", "_ShallowWidth", "_WaveSpeed", "_Choppiness",
                    "_FoamStrength", "_TideHeight", "_TideDistance", "_TidePeriod", "_BeachLandOffset", "_BeachBedDepth", "_ShoreContactWidth" })
                    probe.SetFloat(name, ocean.GetFloat(name));
                probe.SetFloat("_WaterY", -12.4f);
                probe.SetVector("_CoastLimits", new Vector4(mode == 1 ? 1 : 0, mode == 2 ? 1 : 0, 0, 42));
                Mesh bed = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/Environments/" + (mode == 0 ? "Beach Seabed" : "Beach Transition Floor") + ".asset");
                Vector3[] ground = bed.vertices;
                for (int i = 0; i < ground.Length; i++)
                {
                    ground[i] = new Vector3(ground[i].x * 2048, ground[i].y * 0.6f - 9.3f, ground[i].z * 42 + 21);
                    float distance = Mathf.Abs(ground[i].z - (mode == 1 ? 0 : 42));
                    float blend = mode == 0 ? 0 : 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(distance / 18));
                    ground[i].y = Mathf.Lerp(ground[i].y, Mathf.Max(ground[i].y, -9), blend);
                }
                int[] indices = bed.triangles;
                foreach (float time in new[] { 0f, 5, 10, 20, 30, 39 })
                {
                    probe.SetFloat("_CubeDashEnvironmentTime", time);
                    foreach (float z in new[] { 4f, 9, 14, 21, 28, 34, 38 })
                    {
                        float tide = Mathf.Sin(time * 6.2831853f / ocean.GetFloat("_TidePeriod"));
                        float x = ocean.GetFloat("_ShoreX") - tide * ocean.GetFloat("_TideDistance")
                            + Mathf.Sin(z * 0.16f + time * 0.9f) * 0.28f + Mathf.Sin(z * 0.37f + time * 0.43f) * 0.12f;
                        Check(probe, target, readback, ground, indices, x, z);
                    }
                    if (mode == 0) continue;
                    foreach (float x in new[] { 11f, 18, 28, 42, 60, 82, 110 })
                    {
                        float bank = 8 + (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(7, 120, x))) * 22
                            + Mathf.Sin(x * 0.055f) * 2.8f + Mathf.Sin(x * 0.17f) * 1.2f;
                        Check(probe, target, readback, ground, indices, x, mode == 1 ? bank : 42 - bank);
                        foreach (float shoreZ in new[] { 28f, 30, 32, 34 })
                            Check(probe, target, readback, ground, indices, x, mode == 1 ? 42 - shoreZ : shoreZ);
                    }
                }
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
                Assert.That(ShaderUtil.ShaderHasError(ocean.shader), Is.False);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asynchronous;
                RenderTexture.active = oldTarget;
                Object.DestroyImmediate(readback); Object.DestroyImmediate(target); Object.DestroyImmediate(probe);
            }
        }

        private static void Check(Material probe, RenderTexture target, Texture2D readback, Vector3[] ground, int[] indices, float x, float z)
        {
            probe.SetVector("_SampleArea", new Vector4(x, z, 0, 0));
            probe.SetFloat("_ExportCoverage", 0);
            Graphics.Blit(Texture2D.blackTexture, target, probe);
            RenderTexture.active = target; readback.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); readback.Apply();
            Color result = readback.GetPixel(0, 0);
            float bed = Height(ground, indices, x, z);
            Assert.That(result.r, Is.EqualTo(bed).Within(0.003f), "GPU contact sampler must match the saved triangle bed at " + new Vector2(x, z));
            float displacedBed = Height(ground, indices, x + result.b, z + result.a);
            probe.SetFloat("_ExportCoverage", 1);
            Graphics.Blit(Texture2D.blackTexture, target, probe);
            RenderTexture.active = target; readback.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); readback.Apply();
            Color coverage = readback.GetPixel(0, 0);
            Assert.That(coverage.r, Is.EqualTo(displacedBed).Within(0.003f));
            Assert.That(coverage.g, Is.EqualTo(result.g - displacedBed).Within(0.003f));
            Assert.That(coverage.b, Is.EqualTo(result.g - displacedBed).Within(0.003f),
                "No arbitrary shoreline cut is allowed above the terrain: water is clipped only at the actual height intersection.");
        }

        private static float Height(Vector3[] vertices, int[] indices, float x, float z)
        {
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(d) < 1e-8f) continue;
                float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                if (u >= -0.00001f && v >= -0.00001f && u + v <= 1.00001f) return u * a.y + v * b.y + (1 - u - v) * c.y;
            }
            Assert.Fail("Contact point outside the saved bed."); return 0;
        }
    }
}
