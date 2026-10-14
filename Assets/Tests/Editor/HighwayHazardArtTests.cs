using System.Collections;
using System.IO;
using System.Linq;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class HighwayHazardArtTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [TestCase(HighwayHazard.Barrier, 1500, 5000)]
        [TestCase(HighwayHazard.Van, 6000, 20000)]
        public void DetailedMeshesHaveValidShadingAndStayInsideTheExistingHitboxes(HighwayHazard type, int minimum, int maximum)
        {
            var assets = Resources.Load<HighwayHazardVisuals>(HighwayHazardVisuals.ResourcePath);
            Assert.That(assets, Is.Not.Null);
            Mesh mesh = assets.MeshFor(type); Vector3 size = HighwayPresentation.HazardSize(type);
            Assert.That(mesh.vertexCount, Is.InRange(minimum, maximum));
            Assert.That(mesh.subMeshCount, Is.EqualTo(assets.Materials.Length));
            Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.tangents.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.colors32.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.bounds.min.y, Is.EqualTo(0).Within(0.001f));
            foreach (Vector3 point in mesh.vertices)
            {
                Assert.That(Mathf.Abs(point.x), Is.LessThanOrEqualTo(size.x * 0.5f + 0.002f));
                Assert.That(Mathf.Abs(point.z), Is.LessThanOrEqualTo(size.z * 0.5f + 0.002f));
                Assert.That(point.y, Is.InRange(-0.001f, size.y + 0.001f));
            }
            foreach (Vector3 normal in mesh.normals) Assert.That(normal.sqrMagnitude, Is.EqualTo(1).Within(0.01f), "No zero normals on tiny bevels or wheel faces.");
            foreach (Vector4 tangent in mesh.tangents)
            {
                Assert.That(float.IsNaN(tangent.x) || float.IsNaN(tangent.y) || float.IsNaN(tangent.z), Is.False);
                Assert.That(new Vector3(tangent.x, tangent.y, tangent.z).sqrMagnitude, Is.GreaterThan(0.9f));
            }
            Vector3[] positions = mesh.vertices; int[] paint = mesh.GetTriangles(0);
            float paintedArea = 0;
            for (int i = 0; i < paint.Length; i += 3)
                paintedArea += Vector3.Cross(positions[paint[i + 1]] - positions[paint[i]], positions[paint[i + 2]] - positions[paint[i]]).magnitude * 0.5f;
            Assert.That(paintedArea, Is.GreaterThan(type == HighwayHazard.Barrier ? 1.5f : 5), "Keep the gameplay color over broad, readable painted body panels.");
            if (type == HighwayHazard.Van)
            {
                foreach (int surface in new[] { 2, 3, 4, 5, 6, 7, 8 }) Assert.That(mesh.GetIndexCount(surface), Is.GreaterThan(0), "Model tires, rims, glazing, trim and separate lamps.");
                Vector3[] points = mesh.vertices;
                Assert.That(mesh.GetTriangles(2).Select(index => points[index].y).Min(), Is.EqualTo(0).Within(0.001f));
            }
        }

        [Test]
        public void AllSurfacesUseAuthoredAlbedoNormalAndPackedTexturesWithCurvedDepthAndShadowPasses()
        {
            var assets = Resources.Load<HighwayHazardVisuals>(HighwayHazardVisuals.ResourcePath);
            Assert.That(assets.Materials.Length, Is.EqualTo(9));
            foreach (Material material in assets.Materials)
            {
                Assert.That(material.shader.name, Is.EqualTo("CubeDash/Highway Hazard"));
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                foreach (string pass in new[] { "ShadowCaster", "DepthOnly", "DepthNormals" }) Assert.That(material.FindPass(pass), Is.GreaterThanOrEqualTo(0));
                foreach (string property in new[] { "_BaseMap", "_BumpMap", "_SurfaceMap" })
                {
                    Texture texture = material.GetTexture(property); Assert.That(texture, Is.Not.Null);
                    string path = AssetDatabase.GetAssetPath(texture); Assert.That(path, Does.StartWith(HighwayHazardArtBuilder.ArtPath + "/Textures/"));
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    Assert.That(importer.mipmapEnabled, Is.True); Assert.That(importer.isReadable, Is.False);
                    Assert.That(importer.sRGBTexture, Is.EqualTo(property == "_BaseMap"));
                    Assert.That(importer.textureType, Is.EqualTo(property == "_BumpMap" ? TextureImporterType.NormalMap : TextureImporterType.Default));
                    Assert.That(texture.width, Is.EqualTo(256));
                }
            }
            Assert.That(assets.Materials[0].GetColor("_BaseColor"), Is.EqualTo(Color.white), "Do not bake a particular gameplay color into the source paint.");
            Assert.That(assets.Materials[1].GetFloat("_Smoothness"), Is.LessThan(0.2f), "Concrete must not look glossy or plastic.");
            Assert.That(assets.Materials[4].GetFloat("_Smoothness"), Is.GreaterThan(0.75f), "Windows need distinct glass response, not tire-black flat panels.");
        }

        [Test]
        public void PooledDetailedHazardsShareArtPreserveColorsAndRestoreCoinsWithoutExtraObjects()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/ObstacleCube.prefab");
            GameObject first = Object.Instantiate(prefab), second = Object.Instantiate(prefab);
            var presentation = new HighwayPresentation();
            try
            {
                first.transform.SetPositionAndRotation(new Vector3(0, 0.575f, 0), Quaternion.identity);
                second.transform.SetPositionAndRotation(new Vector3(3, 0.575f, 0), Quaternion.identity);
                first.transform.localScale = second.transform.localScale = Vector3.one * 1.15f;
                var a = first.GetComponent<RunnerCube>(); var b = second.GetComponent<RunnerCube>();
                Material blue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/CubeDashBlue.mat");
                a.Configure(CubeColor.Blue, blue, false); b.Configure(CubeColor.Blue, blue, false);
                Mesh home = first.GetComponent<MeshFilter>().sharedMesh; Vector3 homeCollider = a.Collider.size;
                int count = first.GetComponentsInChildren<Transform>(true).Length;
                presentation.Prewarm(a); presentation.Prewarm(b);
                foreach (HighwayHazard type in new[] { HighwayHazard.Barrier, HighwayHazard.Van })
                {
                    a.Configure(CubeColor.Blue, blue, false); b.Configure(CubeColor.Blue, blue, false);
                    presentation.ConfigureHazard(a, type); presentation.ConfigureHazard(b, type); Physics.SyncTransforms();
                    Mesh mesh = first.GetComponent<MeshFilter>().sharedMesh;
                    Material paint = first.GetComponent<Renderer>().sharedMaterial;
                    Assert.That(second.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                    Assert.That(second.GetComponent<Renderer>().sharedMaterial, Is.SameAs(paint));
                    Assert.That(Vector4.Distance(paint.GetColor("_BaseColor"), blue.GetColor("_BaseColor")), Is.LessThan(0.00001f));
                    Assert.That(Vector3.Distance(a.Collider.bounds.size, HighwayPresentation.HazardSize(type)), Is.LessThan(0.001f));
                    Assert.That(a.Collider.bounds.min.y, Is.EqualTo(0).Within(0.001f));
                    for (int repeat = 0; repeat < 50; repeat++)
                    {
                        a.Configure(CubeColor.Blue, blue, false); presentation.ConfigureHazard(a, type);
                        Assert.That(first.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                        Assert.That(first.GetComponent<Renderer>().sharedMaterial, Is.SameAs(paint));
                    }
                }
                a.Configure(CubeColor.Red, blue, true);
                Assert.That(first.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(home));
                Assert.That(a.Collider.size, Is.EqualTo(homeCollider)); Assert.That(a.CoinVisible, Is.True);
                Assert.That(first.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(first.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(Resources.Load<HighwayHazardVisuals>(HighwayHazardVisuals.ResourcePath).Materials[0].GetColor("_BaseColor"), Is.EqualTo(Color.white));
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); presentation.Dispose(); }
        }

        [UnityTest]
        public IEnumerator DetailedTrafficRendersInJungleWithReadableColorAndShapedSurfaces()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Jungle.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); game.TogglePause();
            game.Track.Reset(7321, CubeColor.Red);
            foreach (var rise in game.Track.GetComponentsInChildren<SceneryRise>()) rise.Settle();
            foreach (var segment in game.Track.Segments)
            {
                foreach (var cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (var power in segment.PowerUps) power.gameObject.SetActive(false);
            }
            var section = game.Track.Segments.OrderBy(segment => Mathf.Abs(segment.transform.position.z)).First();
            RunnerCube van = section.Cubes[0], barrier = section.Cubes[1];
            van.gameObject.SetActive(true); barrier.gameObject.SetActive(true);
            van.transform.localPosition = new Vector3(-2.6f, 0.575f, 8); barrier.transform.localPosition = new Vector3(2.6f, 0.575f, 8);
            van.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue), false);
            barrier.Configure(CubeColor.Green, game.Track.ColorMaterial(CubeColor.Green), false);
            var presentation = new HighwayPresentation();
            try
            {
                presentation.ConfigureHazard(van, HighwayHazard.Van); presentation.ConfigureHazard(barrier, HighwayHazard.Barrier);
                Object.FindAnyObjectByType<Canvas>().enabled = false; Physics.SyncTransforms();
                Camera camera = game.GameCamera;
                Capture(camera, "gameplay");
                camera.fieldOfView = 44;
                camera.transform.position = van.Collider.bounds.center + new Vector3(3.3f, 1.35f, -4.3f);
                camera.transform.LookAt(van.Collider.bounds.center); Capture(camera, "van-front");
                camera.transform.position = van.Collider.bounds.center + new Vector3(-3.2f, 1.3f, 4.3f);
                camera.transform.LookAt(van.Collider.bounds.center); Capture(camera, "van-rear");
                camera.transform.position = barrier.Collider.bounds.center + new Vector3(2.2f, 1.1f, -2.8f);
                camera.transform.LookAt(barrier.Collider.bounds.center); Capture(camera, "barrier");
                Assert.That(ShaderUtil.ShaderHasError(Resources.Load<HighwayHazardVisuals>(HighwayHazardVisuals.ResourcePath).Materials[0].shader), Is.False);
            }
            finally { presentation.Dispose(); }
            yield return new ExitPlayMode();
        }

        private static void Capture(Camera camera, string name)
        {
            RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            bool async = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(1280, 800, 24); var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target;
                for (int frame = 0; frame < 4; frame++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
                Assert.That(image.GetPixels32().Count(pixel => pixel.r > 230 && pixel.b > 230 && pixel.g < 30), Is.LessThan(20), "No missing hazard shader variants.");
                string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_HAZARD_PREVIEW");
                if (!string.IsNullOrEmpty(prefix)) File.WriteAllBytes(prefix + "-" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive; ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
