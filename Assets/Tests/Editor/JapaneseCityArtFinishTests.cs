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
    public sealed class JapaneseCityArtFinishTests
    {
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
        public void AllRenderedLoosePetalsAreGroundedAndCleanupIsIdempotent(int layout)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(JapaneseCityBuilder.ArtPath + "/Layouts/Japanese Sakura Street " + layout + ".asset");
            Vector3[] vertices = mesh.vertices;
            foreach (int surface in new[] { 16, 17 })
            {
                int[] indices = mesh.GetTriangles(surface);
                Assert.That(indices.Length, Is.GreaterThan(0), "Keep the deliberate ground scatter.");
                Assert.That(indices.All(i => vertices[i].y < 0.5f), Is.True, "No static petal remains suspended above the sidewalk.");
                Assert.That(indices.All(i => vertices[i].y > 0.2f), Is.True, "Resting petals stay on the paving, not under it.");
            }
            string before = EditorJsonUtility.ToJson(mesh);
            Assert.That(JapaneseCityArtFinish.RemoveAirbornePetals(mesh), Is.Zero);
            Assert.That(EditorJsonUtility.ToJson(mesh), Is.EqualTo(before));
        }

        [Test]
        public void MaterialsUseMipmappedLinearCityLocalTextureAndDistinctSurfaceResponses()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(JapaneseCityArtFinish.TexturePath);
            Assert.That(texture, Is.Not.Null);
            var importer = (TextureImporter)AssetImporter.GetAtPath(JapaneseCityArtFinish.TexturePath);
            Assert.That(importer.sRGBTexture, Is.False); Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat)); Assert.That(importer.anisoLevel, Is.GreaterThanOrEqualTo(4));
            string[] names = { "Timber", "Ivory plaster", "Roof tiles", "Sidewalk sandstone" };
            int[] types = { 1, 2, 8, 5 };
            for (int i = 0; i < names.Length; i++)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityBuilder.ArtPath + "/Materials/" + names[i] + ".mat");
                Assert.That(material.GetTexture("_SurfaceMap"), Is.EqualTo(texture));
                Assert.That(material.GetFloat("_SurfaceType"), Is.EqualTo(types[i]));
                Assert.That(material.GetFloat("_DetailStrength"), Is.GreaterThan(0.4f));
                Assert.That(material.GetFloat("_ReliefStrength"), Is.InRange(0.01f, 0.1f));
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            }
            var road = AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityReferenceFinish.ArtPath + "/Reference Asphalt.mat");
            Assert.That(road.GetTexture("_SurfaceMap"), Is.EqualTo(texture)); Assert.That(road.GetFloat("_SurfaceType"), Is.EqualTo(9));
            Assert.That(road.GetFloat("_Smoothness"), Is.LessThan(0.2f), "Dry aggregate, not glossy plastic asphalt.");
        }

        [Test]
        public void FinishIsRuntimePersistentWithWarmKeyCoolShadowsAndSharpFilmicGrade()
        {
            var scene = EditorSceneManager.OpenScene(JapaneseCityBuilder.ScenePath);
            try
            {
                var track = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EndlessTrack>(true)).Single();
                var data = new SerializedObject(track.Environment);
                var sun = (Light)data.FindProperty("sunlight").objectReferenceValue;
                var fill = (Light)data.FindProperty("skyFill").objectReferenceValue;
                Assert.That(sun.transform.eulerAngles.x, Is.InRange(40, 45));
                Assert.That(sun.intensity, Is.GreaterThan(2)); Assert.That(sun.color.r, Is.GreaterThan(sun.color.b));
                Assert.That(fill.intensity, Is.LessThan(0.1f));
                Color ambient = RenderSettings.ambientEquatorColor; Color sunlight = sun.color; float intensity = sun.intensity;
                track.Environment.ApplyDistance(10500, 1);
                Assert.That(RenderSettings.ambientEquatorColor, Is.EqualTo(ambient));
                Assert.That(sun.color, Is.EqualTo(sunlight)); Assert.That(sun.intensity, Is.EqualTo(intensity));
                Assert.That(RenderSettings.fogStartDistance, Is.GreaterThanOrEqualTo(95));
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(JapaneseCityBuilder.ArtPath + "/Clear Day.asset");
                Assert.That(profile.TryGet<Tonemapping>(out var tone) && tone.mode.value == TonemappingMode.ACES, Is.True);
                Assert.That(profile.TryGet<DepthOfField>(out var depth) && depth.mode.value == DepthOfFieldMode.Off, Is.True);
                Assert.That(profile.TryGet<ColorAdjustments>(out var grade) && grade.contrast.value >= 12 && grade.postExposure.value < 0.4f, Is.True);
                var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First(c => c.CompareTag("MainCamera"));
                Assert.That(camera.GetUniversalAdditionalCameraData().renderPostProcessing, Is.True);
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); }
        }

        [Test]
        public void SurfaceTextureActuallyRendersAndGlassHasNoHardcodedGlow()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var cameraObject = new GameObject("City texture probe", typeof(Camera));
            var sunObject = new GameObject("Texture probe sun", typeof(Light));
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = new Material(Shader.Find("CubeDash/Japanese City Surface"));
            var target = new RenderTexture(128, 128, 24); var image = new Texture2D(128, 128, TextureFormat.RGB24, false);
            Mesh baked = null;
            RenderTexture active = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            Vector4 curve = Shader.GetGlobalVector("_CubeDashRoadCurve");
            try
            {
                ShaderUtil.allowAsyncCompilation = false; Shader.SetGlobalVector("_CubeDashRoadCurve", Vector4.zero);
                RenderSettings.fog = false; RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = RenderSettings.ambientEquatorColor = RenderSettings.ambientGroundColor = Color.black;
                RenderSettings.ambientIntensity = 0; RenderSettings.reflectionIntensity = 0; RenderSettings.ambientProbe = new SphericalHarmonicsL2();
                var camera = cameraObject.GetComponent<Camera>(); camera.transform.position = Vector3.back * 3;
                camera.orthographic = true; camera.orthographicSize = 0.6f; camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                var sun = sunObject.GetComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.85f; RenderSettings.sun = sun;
                quad.GetComponent<Renderer>().sharedMaterial = material;
                quad.transform.localScale = new Vector3(2, 2, 1);
                material.SetColor("_BaseColor", Color.gray); material.SetColor("_ShadeColor", Color.white);
                material.SetFloat("_SurfaceType", 9); material.SetFloat("_DetailStrength", 0.65f); material.SetFloat("_ReliefStrength", 0.012f);
                material.SetTexture("_SurfaceMap", AssetDatabase.LoadAssetAtPath<Texture2D>(JapaneseCityArtFinish.TexturePath));
                Color[] Pixels()
                {
                    for (int frame = 0; frame < 3; frame++) camera.Render();
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply(); return image.GetPixels();
                }
                Color[] textured = Pixels(); material.SetTexture("_SurfaceMap", Texture2D.grayTexture); Color[] plain = Pixels();
                float difference = textured.Zip(plain, (a, b) => Mathf.Abs(a.grayscale - b.grayscale)).Average();
                Assert.That(difference, Is.GreaterThan(0.0005f), "Packed grain must affect actual lit pixels, not just exist as an unused material reference.");
                // The road is a scaled primitive, unlike the metre-sized streetscape meshes. Both must tile identically.
                material.SetTexture("_SurfaceMap", AssetDatabase.LoadAssetAtPath<Texture2D>(JapaneseCityArtFinish.TexturePath));
                baked = Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);
                baked.vertices = baked.vertices.Select(v => Vector3.Scale(v, quad.transform.localScale)).ToArray(); baked.RecalculateBounds();
                quad.GetComponent<MeshFilter>().sharedMesh = baked; quad.transform.localScale = Vector3.one;
                Assert.That(textured.Zip(Pixels(), (a, b) => Mathf.Abs(a.grayscale - b.grayscale)).Average(), Is.LessThan(0.001f),
                    "Texture scale is in metres for scaled road primitives as well as baked scenery; no stretched road grain or section seams.");
                material.SetFloat("_SurfaceType", 3); sun.enabled = false;
                Assert.That(Pixels().Max(c => c.maxColorComponent), Is.LessThan(0.04f), "Glass reflections follow scene radiance instead of glowing blue in darkness.");
            }
            finally
            {
                cameraObject.GetComponent<Camera>().targetTexture = null; RenderTexture.active = active;
                ShaderUtil.allowAsyncCompilation = async; Shader.SetGlobalVector("_CubeDashRoadCurve", curve);
                foreach (Object value in new Object[] { cameraObject, sunObject, quad, material, target, image, baked }) if (value != null) Object.DestroyImmediate(value);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
        }
    }
}
