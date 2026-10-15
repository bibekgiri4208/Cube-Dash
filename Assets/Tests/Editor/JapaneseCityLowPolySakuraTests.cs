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
    public sealed class JapaneseCityLowPolySakuraTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void OriginalModelsHaveSolidFacetedCrownsTaperedBranchesAndThreeCheaperLods(int variant)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JapaneseCityLowPolySakura.PrefabPath(variant));
            Assert.That(prefab, Is.Not.Null);
            var lod = prefab.GetComponent<LODGroup>(); Assert.That(lod, Is.Not.Null);
            Assert.That(lod.GetLODs().Length, Is.EqualTo(3));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            int previous = int.MaxValue;
            foreach (LOD level in lod.GetLODs())
            {
                Assert.That(level.renderers.Length, Is.EqualTo(1));
                var renderer = level.renderers[0]; var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.subMeshCount, Is.EqualTo(2), "One bark and one solid canopy draw, not hundreds of flower objects.");
                int triangles = (int)(mesh.GetIndexCount(0) + mesh.GetIndexCount(1)) / 3;
                Assert.That(triangles, Is.InRange(500, 6000)); Assert.That(triangles, Is.LessThan(previous)); previous = triangles;
                Assert.That(mesh.colors32.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.colors32.Distinct().Count(), Is.GreaterThan(20), "Designed rose/blush facets, not a single flat pink ball.");
                Assert.That(mesh.bounds.min.y, Is.EqualTo(0).Within(0.0001f));
                Assert.That(mesh.bounds.size.y, Is.InRange(6.5f, 7.5f));
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On)); Assert.That(renderer.receiveShadows, Is.True);
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                foreach (int surface in new[] { 0, 1 })
                {
                    int[] indices = mesh.GetTriangles(surface);
                    Assert.That(indices.Length, Is.GreaterThan(100));
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        Vector3 cross = Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]], vertices[indices[i + 2]] - vertices[indices[i]]);
                        Assert.That(cross.sqrMagnitude, Is.GreaterThan(0.000000001f), "No degenerate triangles.");
                        for (int c = 0; c < 3; c++) Assert.That(Vector3.Dot(cross.normalized, normals[indices[i + c]]), Is.GreaterThan(0.999f), "True flat geometric facets, not mismatched smooth normals.");
                    }
                }
            }
            foreach (string dependency in AssetDatabase.GetDependencies(JapaneseCityLowPolySakura.PrefabPath(variant), true))
                Assert.That(dependency.StartsWith("Assets/Idyllic Fantasy Nature/"), Is.False, "City no longer depends on the rejected card-based tree models.");
        }

        [Test]
        public void PaletteIsOpaqueMatteNonEmissiveAndCityLocal()
        {
            foreach (string path in new[] { JapaneseCityLowPolySakura.CanopyPath, JapaneseCityLowPolySakura.BarkPath })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material.shader.name, Is.EqualTo("CubeDash/City Low Poly Sakura"));
                Assert.That(material.GetTag("RenderType", false), Is.EqualTo("Opaque"));
                Assert.That(material.HasProperty("_BaseMap"), Is.False, "No alpha cards or pack textures.");
                Assert.That(material.HasProperty("_EmissionColor"), Is.False);
                Assert.That(material.GetFloat("_Smoothness"), Is.LessThan(0.2f));
                foreach (string pass in new[] { "ShadowCaster", "DepthOnly", "DepthNormals" }) Assert.That(material.FindPass(pass), Is.GreaterThanOrEqualTo(0));
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            }
        }

        [Test]
        public void SolidSakuraGpuRespondsToSunlightAndDoesNotGlowInDarkness()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Solid sakura lighting probe", typeof(Camera));
            var sunObject = new GameObject("Probe sun", typeof(Light));
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(JapaneseCityLowPolySakura.PrefabPath(0)));
            var target = new RenderTexture(128, 128, 24); var image = new Texture2D(128, 128, TextureFormat.RGB24, false);
            var active = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                RenderSettings.fog = false; RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = RenderSettings.ambientEquatorColor = RenderSettings.ambientGroundColor = Color.black;
                RenderSettings.ambientIntensity = 0; RenderSettings.reflectionIntensity = 0; RenderSettings.ambientProbe = new SphericalHarmonicsL2();
                tree.GetComponent<LODGroup>().ForceLOD(0);
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 4.1f;
                camera.transform.position = new Vector3(0, 4, -12); camera.transform.LookAt(Vector3.up * 3.5f);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.targetTexture = target;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                var sun = sunObject.GetComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.85f;
                sun.transform.rotation = Quaternion.Euler(35, -25, 0); RenderSettings.sun = sun;
                Color[] Pixels()
                {
                    for (int frame = 0; frame < 3; frame++) camera.Render();
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply(); return image.GetPixels();
                }
                Color[] lit = Pixels(); sun.enabled = false; Color[] dark = Pixels();
                Assert.That(lit.Count(c => c.r > c.g * 1.1f && c.grayscale > 0.1f), Is.GreaterThan(1000), "A substantial pink volumetric crown actually renders.");
                Assert.That(dark.Max(c => c.maxColorComponent), Is.LessThan(0.04f), "No menu glow or hardcoded ambient illumination.");
            }
            finally
            {
                cameraObject.GetComponent<Camera>().targetTexture = null; RenderTexture.active = active; ShaderUtil.allowAsyncCompilation = async;
                foreach (Object value in new Object[] { cameraObject, sunObject, tree, target, image }) Object.DestroyImmediate(value);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
