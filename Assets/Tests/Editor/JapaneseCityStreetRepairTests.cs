using System.Collections.Generic;
using System.Linq;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class JapaneseCityStreetRepairTests
    {
        [Test]
        public void EverySakuraLodClearsArchitectureAndBridgeSupportsAcrossAllRecycledLayouts()
        {
            var scene = EditorSceneManager.OpenPreviewScene(JapaneseCityBuilder.ScenePath);
            try
            {
                var track = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EndlessTrack>(true)).Single();
                var scenery = track.Segments[0].Environment.Biomes[0].GetComponent<BiomeScenery>();
                foreach (Mesh mesh in scenery.Layouts)
                {
                    Vector3[] vertices = mesh.vertices; Vector2[] anchors = mesh.uv3;
                    var architecture = new List<Bounds>();
                    for (int surface = 0; surface < JapaneseCitySidewalks.OriginalSurfaces; surface++)
                    {
                        int[] triangles = mesh.GetTriangles(surface);
                        for (int i = 0; i < triangles.Length; i += 3)
                        {
                            Vector2 anchor = anchors[triangles[i]];
                            bool bridge = Mathf.Abs(Mathf.Abs(anchor.x) - 12 * JapaneseCityStreetRepair.BridgeWidthScale) < 0.001f && Mathf.Abs(anchor.y - 29) < 0.001f;
                            if (!bridge && Mathf.Abs(anchor.x) < 15.5f) continue;
                            var bounds = new Bounds(vertices[triangles[i]], Vector3.zero);
                            bounds.Encapsulate(vertices[triangles[i + 1]]); bounds.Encapsulate(vertices[triangles[i + 2]]);
                            architecture.Add(bounds);
                        }
                    }
                    foreach (var segment in track.Segments)
                    {
                        var city = segment.Environment.Biomes[0].transform;
                        Transform streets = city.Find(JapaneseCityBuilder.StreetsName);
                        var trees = city.Find(JapaneseCitySidewalks.TreesName).GetComponent<CitySakuraTrees>();
                        foreach (Transform tree in trees.Trees)
                        {
                            Bounds crown = JapaneseCitySidewalks.BoundsOfTree(tree.gameObject, true);
                            crown.center -= streets.position;
                            // Include wind and keep a visible air gap, not merely non-intersecting trunks.
                            crown.Expand(0.5f);
                            foreach (int neighbor in new[] { -1, 0, 1 })
                            {
                                Bounds adjacent = crown; adjacent.center += Vector3.forward * (neighbor * 42);
                                Assert.That(architecture.Any(b => b.Intersects(adjacent)), Is.False,
                                    mesh.name + ": " + tree.name + " overlaps a shop, awning, sign, planter or bridge.");
                            }
                        }
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void CitySunUsesEffectiveLocalSoftShadowSettingsNotIgnoredLegacyBias()
        {
            var scene = EditorSceneManager.OpenPreviewScene(JapaneseCityBuilder.ScenePath);
            try
            {
                var director = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnvironmentDirector>(true)).Single();
                var sun = (Light)new SerializedObject(director).FindProperty("sunlight").objectReferenceValue;
                var settings = sun.GetUniversalAdditionalLightData();
                Assert.That(sun.shadows, Is.EqualTo(LightShadows.Soft));
                Assert.That(settings.usePipelineSettings, Is.False);
                Assert.That(settings.softShadowQuality, Is.EqualTo(SoftShadowQuality.High));
                Assert.That(sun.shadowBias, Is.EqualTo(0.35f));
                Assert.That(sun.shadowNormalBias, Is.EqualTo(0.3f));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void CityGpuCastsAndReceivesShadowsIncludingLegacyCutout(int style)
        {
            bool petals = style == 1;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("City shadow probe", typeof(Camera));
            var sunObject = new GameObject("Shadow probe sun", typeof(Light));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var caster = GameObject.CreatePrimitive(petals ? PrimitiveType.Quad : PrimitiveType.Cube);
            var paving = new Material(Shader.Find("CubeDash/Japanese City Surface"));
            var material = new Material(Shader.Find(petals ? "CubeDash/Japanese City Sakura" : style == 2 ? "CubeDash/City Low Poly Sakura" : "CubeDash/Japanese City Surface"));
            var transparent = new Texture2D(1, 1); transparent.SetPixel(0, 0, Color.clear); transparent.Apply();
            var target = new RenderTexture(256, 256, 24);
            var image = new Texture2D(256, 256, TextureFormat.RGB24, false);
            RenderTexture active = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            Vector4 curve = Shader.GetGlobalVector("_CubeDashRoadCurve");
            try
            {
                ShaderUtil.allowAsyncCompilation = false; Shader.SetGlobalVector("_CubeDashRoadCurve", Vector4.zero);
                RenderSettings.fog = false; RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = RenderSettings.ambientEquatorColor = RenderSettings.ambientGroundColor = Color.black;
                RenderSettings.ambientProbe = new SphericalHarmonicsL2(); RenderSettings.reflectionIntensity = 0;
                ground.GetComponent<Renderer>().sharedMaterial = paving;
                ground.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                caster.transform.position = new Vector3(0, 2, 0);
                caster.transform.localScale = Vector3.one * 3;
                if (petals) { caster.transform.rotation = Quaternion.Euler(90, 0, 0); material.SetFloat("_WindStrength", 0); }
                caster.GetComponent<Renderer>().sharedMaterial = material;
                var sun = sunObject.GetComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.8f;
                sun.transform.rotation = Quaternion.Euler(55, -30, 0); sun.shadows = LightShadows.Soft;
                sun.shadowBias = 0.35f; sun.shadowNormalBias = 0.3f;
                sun.GetUniversalAdditionalLightData().usePipelineSettings = false; RenderSettings.sun = sun;
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 6;
                camera.transform.position = new Vector3(0, 10, -8); camera.transform.LookAt(Vector3.zero);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.targetTexture = target;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                Color[] Pixels()
                {
                    for (int frame = 0; frame < 4; frame++) camera.Render();
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply(); return image.GetPixels();
                }
                var renderer = caster.GetComponent<Renderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off;
                Color[] unshadowed = Pixels(); renderer.shadowCastingMode = ShadowCastingMode.On;
                Color[] shadowed = Pixels();
                int count = Enumerable.Range(0, shadowed.Length).Count(i => unshadowed[i].grayscale - shadowed[i].grayscale > 0.08f);
                Assert.That(count, Is.GreaterThan(100), "Actual GPU shadow must reach the City paving, not just expose a named shader pass.");
                if (petals)
                {
                    material.SetTexture("_BaseMap", transparent);
                    Color[] clear = Pixels(); renderer.shadowCastingMode = ShadowCastingMode.Off;
                    Color[] clearWithoutShadow = Pixels();
                    Assert.That(Enumerable.Range(0, clear.Length).Count(i => Mathf.Abs(clear[i].grayscale - clearWithoutShadow[i].grayscale) > 0.025f),
                        Is.LessThan(10), "Transparent petal cards must not leave rectangular shadows.");
                }
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                Assert.That(ShaderUtil.ShaderHasError(paving.shader), Is.False);
            }
            finally
            {
                cameraObject.GetComponent<Camera>().targetTexture = null;
                RenderTexture.active = active; ShaderUtil.allowAsyncCompilation = async; Shader.SetGlobalVector("_CubeDashRoadCurve", curve);
                foreach (Object value in new Object[] { cameraObject, sunObject, ground, caster, paving, material, transparent, target, image }) Object.DestroyImmediate(value);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
