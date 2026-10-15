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
    public sealed class JapaneseCityReferenceTests
    {
        [Test]
        public void ReferenceRoadAndBarriersAreCityLocalWithUnchangedCollisionSurface()
        {
            var scene = EditorSceneManager.OpenPreviewScene(JapaneseCityBuilder.ScenePath);
            try
            {
                var track = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EndlessTrack>(true)).Single();
                foreach (var segment in track.Segments)
                {
                    foreach (string name in new[] { "Road", "Plane", "Highway Shoulders", "Highway Lane Markings", "Highway Guardrails", JapaneseCityReferenceFinish.PedestrianName })
                    {
                        Transform part = segment.transform.Find(name);
                        if (name == "Road" || name == "Plane") { if (part == null) continue; }
                        Assert.That(part, Is.Not.Null, name);
                        Assert.That(part.GetComponent<Renderer>().sharedMaterials.All(m => AssetDatabase.GetAssetPath(m).StartsWith(JapaneseCityReferenceFinish.ArtPath)), Is.True, name);
                        if (name != "Road" && name != "Plane") Assert.That(part.GetComponentsInChildren<Collider>(true), Is.Empty);
                    }
                    var rails = segment.transform.Find("Highway Guardrails").GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(rails.subMeshCount, Is.EqualTo(4));
                    Assert.That(rails.vertices.All(v => Mathf.Abs(v.x) > 4.85f), Is.True);
                    Assert.That(rails.bounds.min.z, Is.EqualTo(0)); Assert.That(rails.bounds.max.z, Is.EqualTo(42));
                    Assert.That(rails.bounds.max.y, Is.LessThan(1));
                    var fence = segment.transform.Find(JapaneseCityReferenceFinish.PedestrianName).GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(fence.vertices.All(v => Mathf.Abs(v.x) > 6.9f), Is.True);
                }
                var mint = AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityReferenceFinish.ArtPath + "/Reference Barrier Mint.mat").GetColor("_BaseColor");
                Assert.That(mint.g, Is.GreaterThan(mint.r)); Assert.That(mint.b, Is.GreaterThan(mint.r));
                var road = AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityReferenceFinish.ArtPath + "/Reference Asphalt.mat");
                Assert.That(road.shader.name, Is.EqualTo("CubeDash/Japanese City Surface"));
                Assert.That(road.GetColor("_BaseColor").r, Is.GreaterThan(road.GetColor("_BaseColor").g));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
        public void RectangularSidewalksLeaveWideContinuousDarkOuterPaths(int layout)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(JapaneseCityBuilder.ArtPath + "/Layouts/Japanese Sakura Street " + layout + ".asset");
            Vector3[] vertices = mesh.vertices;
            foreach (int surface in new[] { 22, 23, 24 })
                foreach (int i in mesh.GetTriangles(surface)) Assert.That(Mathf.Abs(vertices[i].x), Is.GreaterThan(6.8f));
            var paving = new[] { 22, 23, 24 }.SelectMany(mesh.GetTriangles).Select(i => Mathf.Abs(vertices[i].x)).ToArray();
            Assert.That(paving.Max(), Is.EqualTo(JapaneseCityStreetRepair.PavingEdge - 0.012f).Within(0.001f));
            Assert.That(paving.Max() - paving.Min(), Is.GreaterThan(10f), "Widen outward, never into the driving lanes or dark access path.");
            var path = mesh.GetTriangles(28).Select(i => vertices[i]).ToArray();
            Assert.That(path.Min(v => Mathf.Abs(v.x)), Is.EqualTo(5.05f).Within(0.001f));
            Assert.That(path.Max(v => Mathf.Abs(v.x)), Is.EqualTo(6.65f).Within(0.001f));
            Assert.That(path.Min(v => v.z), Is.EqualTo(0)); Assert.That(path.Max(v => v.z), Is.EqualTo(42));
            Assert.That(path.Max(v => v.y), Is.EqualTo(0).Within(0.001f));
        }

        [Test]
        public void ArchivedCutoutMaterialStillUsesOriginalPetalTextureButCityPbrNotMenuGlow()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityReferenceFinish.BlossomPath);
            Assert.That(material.shader.name, Is.EqualTo("CubeDash/Japanese City Sakura"));
            Assert.That(material.HasProperty("_Menu_Blossom_Glow"), Is.False);
            Assert.That(material.GetColor("_BaseColor").maxColorComponent, Is.LessThanOrEqualTo(1));
            Assert.That(material.GetFloat("_Smoothness"), Is.LessThan(0.15f));
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")), Is.EqualTo("Assets/Idyllic Fantasy Nature/Textures/Trees/Tree_Blossoms.png"));
            foreach (string pass in new[] { "ShadowCaster", "DepthOnly", "DepthNormals" }) Assert.That(material.FindPass(pass), Is.GreaterThanOrEqualTo(0));
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
        }

        [Test]
        public void ArchivedCutoutShaderRespondsToSunlightAndDoesNotGlowInDarkness()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Sakura lighting probe", typeof(Camera));
            var lightObject = new GameObject("Probe sun", typeof(Light));
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityReferenceFinish.BlossomPath));
            var target = new RenderTexture(64, 64, 24); var image = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var active = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = RenderSettings.ambientEquatorColor = RenderSettings.ambientGroundColor = Color.black;
                RenderSettings.ambientIntensity = 0; RenderSettings.reflectionIntensity = 0;
                RenderSettings.ambientProbe = new SphericalHarmonicsL2(); RenderSettings.skybox = null;
                material.SetTexture("_BaseMap", Texture2D.whiteTexture); material.SetFloat("_WindStrength", 0);
                quad.GetComponent<Renderer>().sharedMaterial = material;
                var camera = cameraObject.GetComponent<Camera>(); camera.transform.position = Vector3.back * 3;
                camera.orthographic = true; camera.orthographicSize = 1; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black; camera.targetTexture = target;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                var sun = lightObject.GetComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.85f; sun.color = Color.white;
                Color Pixel()
                {
                    for (int frame = 0; frame < 3; frame++) camera.Render();
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); image.Apply(); return image.GetPixel(32, 32);
                }
                Color lit = Pixel(); sun.enabled = false; Color dark = Pixel();
                Assert.That(lit.grayscale, Is.GreaterThan(0.15f));
                Assert.That(dark.maxColorComponent, Is.LessThan(0.04f), "No menu emission or hardcoded bright shade floor.");
                sun.enabled = true; material.SetTexture("_BaseMap", Texture2D.blackTexture);
                var transparent = new Texture2D(1, 1); transparent.SetPixel(0, 0, Color.clear); transparent.Apply();
                try { material.SetTexture("_BaseMap", transparent); Assert.That(Pixel().maxColorComponent, Is.LessThan(0.04f), "Transparent areas of blossom cards are clipped."); }
                finally { Object.DestroyImmediate(transparent); }
            }
            finally
            {
                cameraObject.GetComponent<Camera>().targetTexture = null; RenderTexture.active = active; ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(lightObject); Object.DestroyImmediate(quad);
                Object.DestroyImmediate(material); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
