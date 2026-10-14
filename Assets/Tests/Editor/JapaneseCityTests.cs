using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class JapaneseCityTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void CityHasEightSceneLocalLayoutsAndNoNewCollidersOrSharedArtEdits()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(JapaneseCityBuilder.ScenePath);
            try
            {
                var track = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EndlessTrack>(true)).Single();
                foreach (var segment in track.Segments)
                {
                    var city = segment.Environment.Biomes[0]; var scenery = city.GetComponent<BiomeScenery>();
                    Assert.That(scenery.Layouts.Length, Is.EqualTo(8));
                    Assert.That(scenery.EntranceLayouts, Is.EqualTo(scenery.Layouts));
                    Assert.That(scenery.ExitLayouts, Is.EqualTo(scenery.Layouts));
                    Assert.That(scenery.VarietyLayouts, Is.Empty);
                    Assert.That(city.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(city.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(1), "Only the baked street renderer is active; old scenery is archived.");
                    var streets = city.transform.Find(JapaneseCityBuilder.StreetsName);
                    Assert.That(streets.position.y, Is.EqualTo(0).Within(0.01f));
                    Assert.That(streets.GetComponent<Renderer>().sharedMaterials.All(material => AssetDatabase.GetAssetPath(material).StartsWith(JapaneseCityBuilder.ArtPath)), Is.True);
                    int count = city.GetComponentsInChildren<Transform>(true).Length;
                    for (int section = 0; section < 32; section++)
                    {
                        segment.Environment.Configure(section, 8, EnvironmentBiome.City);
                        Assert.That(streets.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(scenery.DetailLayout(section, 8, true)));
                        Assert.That(city.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(1), "Recycling must not reactivate the archived skyline.");
                    }
                    Assert.That(city.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
        public void BakedStreetsHaveSakuraSignsWindowsAndClearGameplayCorridor(int layout)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(JapaneseCityBuilder.ArtPath + "/Layouts/Japanese Sakura Street " + layout + ".asset");
            Assert.That(mesh, Is.Not.Null); Assert.That(mesh.vertexCount, Is.InRange(30000, 220000));
            Assert.That(mesh.subMeshCount, Is.EqualTo(22)); Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.uv3.Length, Is.EqualTo(mesh.vertexCount));
            foreach (int surface in new[] { 6, 7, 10, 14, 15, 16, 17, 20, 21 })
                Assert.That(mesh.GetIndexCount(surface), Is.GreaterThan(50), "Windows, lanterns, branches, three blossom tones, signs and real Japanese glyphs are modeled.");
            foreach (Vector3 vertex in mesh.vertices)
            {
                Assert.That(float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsNaN(vertex.z), Is.False);
                Assert.That(Mathf.Abs(vertex.x) > 5 || vertex.y > 8, Is.True, "Street decoration stays outside lanes/guardrails; the pedestrian bridge clears ground and flight modes. Vertex: " + vertex);
            }
            Assert.That(mesh.bounds.max.y, Is.GreaterThan(20));
            float minimumY = mesh.vertices.Min(vertex => vertex.y);
            Assert.That(minimumY, Is.GreaterThan(-1));
            Assert.That(mesh.bounds.min.y, Is.LessThanOrEqualTo(minimumY - 79.9f), "Bounds include shader-driven scenery rise, preventing offscreen culling/popping.");
        }

        [Test]
        public void SavedSkyAndPostEffectsAreCityLocalAndKeepStreetsSharp()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityBuilder.ArtPath + "/Fuji Sky.mat");
            Assert.That(sky.shader.name, Is.EqualTo("CubeDash/Japanese City Clear Sky"));
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(JapaneseCityBuilder.ArtPath + "/Clear Day.asset");
            Assert.That(profile.TryGet<DepthOfField>(out var depth), Is.True);
            Assert.That(depth.mode.value, Is.EqualTo(DepthOfFieldMode.Off));
            Assert.That(profile.TryGet<Vignette>(out var vignette), Is.True);
            Assert.That(vignette.intensity.value, Is.EqualTo(0.08f).Within(0.001f));
            Assert.That(profile.components.Count, Is.EqualTo(2), "Only distance blur and vignette are overridden, not player/HUD colors.");
            var lettering = AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityBuilder.ArtPath + "/Materials/Lettering.mat");
            Assert.That(AssetDatabase.GetAssetPath(lettering.GetTexture("_BaseMap")), Is.EqualTo(JapaneseCityBuilder.ArtPath + "/Japanese Glyphs.png"));
        }

        [Test]
        public void ClearSkyHasNoDirectionalCloudOrMountainArtwork()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityBuilder.ArtPath + "/Fuji Sky.mat");
            Material previousSky = RenderSettings.skybox; var active = RenderTexture.active;
            bool async = ShaderUtil.allowAsyncCompilation;
            var go = new GameObject("City clear sky probe", typeof(Camera)); var camera = go.GetComponent<Camera>();
            var target = new RenderTexture(128, 128, 24); var image = new Texture2D(128, 128, TextureFormat.RGB24, false);
            try
            {
                ShaderUtil.allowAsyncCompilation = false; RenderSettings.skybox = sky;
                camera.cullingMask = 0; camera.clearFlags = CameraClearFlags.Skybox; camera.targetTexture = target;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                Color32[] Render(float yaw)
                {
                    camera.transform.rotation = Quaternion.Euler(-12, yaw, 0);
                    for (int frame = 0; frame < 3; frame++) camera.Render();
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
                    return image.GetPixels32();
                }
                Color32[] forward = Render(0), sideways = Render(90);
                int maximumDifference = 0;
                for (int i = 0; i < forward.Length; i++)
                    maximumDifference = Mathf.Max(maximumDifference, Mathf.Abs(forward[i].r - sideways[i].r),
                        Mathf.Abs(forward[i].g - sideways[i].g), Mathf.Abs(forward[i].b - sideways[i].b));
                Assert.That(maximumDifference, Is.LessThanOrEqualTo(2), "Only an altitude gradient should remain; no front-facing mountain or cloud pattern.");
                Assert.That(forward.Max(pixel => pixel.r) - forward.Min(pixel => pixel.r), Is.GreaterThan(20), "The sky must render a gradient, not an empty/black target.");
                Assert.That(ShaderUtil.ShaderHasError(sky.shader), Is.False);
            }
            finally
            {
                camera.targetTexture = null; RenderSettings.skybox = previousSky; RenderTexture.active = active;
                ShaderUtil.allowAsyncCompilation = async; Object.DestroyImmediate(go); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }

        [Test]
        public void ReapplyingPreservesSavedCityAndArtByteForByte()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var files = Directory.GetFiles(JapaneseCityBuilder.ArtPath, "*", SearchOption.AllDirectories).Concat(new[] { JapaneseCityBuilder.ScenePath }).ToArray();
            string Hash(string path)
            { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return System.BitConverter.ToString(sha.ComputeHash(stream)); }
            var hashes = files.Select(Hash).ToArray();
            JapaneseCityBuilder.ApplyFromCommandLine();
            for (int i = 0; i < files.Length; i++) Assert.That(Hash(files[i]), Is.EqualTo(hashes[i]), files[i]);
        }

        [UnityTest]
        public IEnumerator CityRendersGameplayStorefrontAndRecycledCurvedRoad()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(JapaneseCityBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); game.Track.Reset(7321, CubeColor.Red);
            foreach (var rise in game.Track.GetComponentsInChildren<SceneryRise>()) rise.Settle();
            var camera = game.GameCamera; var canvas = Object.FindAnyObjectByType<Canvas>();
            var mode = canvas.renderMode; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Capture(camera, "with-hud"); canvas.renderMode = mode; game.TogglePause(); canvas.enabled = false;
            Capture(camera, "gameplay");
            Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            camera.transform.position = new Vector3(-4.5f, 2.7f, -5); camera.transform.LookAt(new Vector3(-12, 4, 15));
            Capture(camera, "storefront"); camera.transform.SetPositionAndRotation(position, rotation);
            int count = game.Track.GetComponentsInChildren<Transform>(true).Length;
            for (int step = 0; step < 40; step++)
                game.Track.Advance(42, 0, 0, LevelProgression.Difficulty((step + 1) * 42), Vector2.one * 0.45f, 0, false, false, true, false, out _, out _);
            foreach (var rise in game.Track.GetComponentsInChildren<SceneryRise>()) rise.Settle();
            Assert.That(game.Track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(game.Track.Environment.CurrentBiome, Is.EqualTo(EnvironmentBiome.City));
            Capture(camera, "curved-road");
            canvas.enabled = true;
            foreach (string path in new[] { "Assets/Shaders/JapaneseCityLettering.shader", "Assets/Shaders/JapaneseCitySky.shader" })
                Assert.That(ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>(path)), Is.False, path);
            yield return new ExitPlayMode();
        }

        private static void Capture(Camera camera, string name)
        {
            var previous = camera.targetTexture; var active = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(1440, 1080, 24); var image = new Texture2D(1440, 1080, TextureFormat.RGB24, false);
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target; Canvas.ForceUpdateCanvases();
                for (int frame = 0; frame < 4; frame++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1440, 1080), 0, 0); image.Apply();
                Assert.That(image.GetPixels32().Count(pixel => pixel.r > 230 && pixel.b > 230 && pixel.g < 30), Is.LessThan(20), "No missing shaders.");
                string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_CITY_PREVIEW");
                if (!string.IsNullOrEmpty(prefix)) File.WriteAllBytes(prefix + "-" + name + ".png", image.EncodeToPNG());
            }
            finally
            { camera.targetTexture = previous; RenderTexture.active = active; ShaderUtil.allowAsyncCompilation = async; Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
        }
    }
}
