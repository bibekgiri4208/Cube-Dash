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
                    var nativeTrees = city.transform.Find(JapaneseCitySidewalks.TreesName);
                    Assert.That(nativeTrees, Is.Not.Null);
                    int rendererCount = 1 + nativeTrees.GetComponentsInChildren<Renderer>(true).Length;
                    Assert.That(city.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(rendererCount), "Only the street and saved native tree LOD renderers are active; old scenery is archived.");
                    var streets = city.transform.Find(JapaneseCityBuilder.StreetsName);
                    Assert.That(streets.position.y, Is.EqualTo(0).Within(0.01f));
                    Assert.That(streets.GetComponent<Renderer>().sharedMaterials.All(material => AssetDatabase.GetAssetPath(material).StartsWith(JapaneseCityBuilder.ArtPath)), Is.True);
                    int count = city.GetComponentsInChildren<Transform>(true).Length;
                    for (int section = 0; section < 32; section++)
                    {
                        segment.Environment.Configure(section, 8, EnvironmentBiome.City);
                        Assert.That(streets.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(scenery.DetailLayout(section, 8, true)));
                        Assert.That(city.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(rendererCount), "Recycling must not reactivate the archived skyline or create more trees.");
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
            Assert.That(mesh.subMeshCount, Is.EqualTo(JapaneseCitySidewalks.SurfaceCount)); Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.uv3.Length, Is.EqualTo(mesh.vertexCount));
            foreach (int surface in new[] { 6, 7, 10, 16, 17, 20, 21 })
                Assert.That(mesh.GetIndexCount(surface), Is.GreaterThan(50), "Windows, lanterns, fallen petals, signs and real Japanese glyphs are modeled.");
            Assert.That(mesh.GetIndexCount(14), Is.Zero, "Original faceted sakura branches are retired.");
            Assert.That(mesh.GetIndexCount(15), Is.Zero, "Original faceted sakura crowns are retired.");
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
            Assert.That(vignette.intensity.value, Is.EqualTo(0.11f).Within(0.001f));
            Assert.That(profile.TryGet<Bloom>(out var bloom), Is.True);
            Assert.That(bloom.intensity.value, Is.LessThan(0.3f)); Assert.That(bloom.threshold.value, Is.GreaterThan(1));
            Assert.That(profile.TryGet<ColorAdjustments>(out var grade), Is.True); Assert.That(grade.contrast.value, Is.InRange(6, 18));
            Assert.That(profile.TryGet<Tonemapping>(out var tone), Is.True); Assert.That(tone.mode.value, Is.EqualTo(TonemappingMode.ACES));
            Assert.That(profile.components.Count, Is.EqualTo(7), "City-local lighting grade, not shared graphics defaults.");
            var lettering = AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityBuilder.ArtPath + "/Materials/Lettering.mat");
            Assert.That(AssetDatabase.GetAssetPath(lettering.GetTexture("_BaseMap")), Is.EqualTo(JapaneseCityBuilder.ArtPath + "/Japanese Glyphs.png"));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
        public void CityHasSavedCreviceShadingWithoutChangingTopologyOrAddingRuntimeColliders(int layout)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(JapaneseCityBuilder.ArtPath + "/Layouts/Japanese Sakura Street " + layout + ".asset");
            Color32[] colors = mesh.colors32;
            Assert.That(colors.Length, Is.EqualTo(mesh.vertexCount));
            Assert.That(colors.Min(c => c.r), Is.LessThan(180), "Shaded shop recesses, foliage interiors and contacts.");
            Assert.That(colors.Max(c => c.r), Is.EqualTo(255), "Open, unoccluded surfaces remain clean.");
            Assert.That(colors.Count(c => c.r < 230), Is.GreaterThan(mesh.vertexCount / 25), "Retained shop AO; the native canopies no longer occupy this baked mesh.");
            Assert.That(colors.All(c => c.r == c.g && c.g == c.b && c.a == 255), Is.True, "AO does not repaint the material palette.");
            var glass = AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityBuilder.ArtPath + "/Materials/Window blue.mat");
            Assert.That(glass.shader.name, Is.EqualTo("CubeDash/Japanese City Surface"));
            Assert.That(glass.GetFloat("_Smoothness"), Is.GreaterThan(0.65f));
            Assert.That(glass.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
            Assert.That(glass.FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0));
            Assert.That(ShaderUtil.ShaderHasError(glass.shader), Is.False);
        }

        [Test]
        public void CitySunAndAmbientHaveDirectionalWarmCoolSeparation()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(JapaneseCityBuilder.ScenePath);
            try
            {
                var track = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EndlessTrack>(true)).Single();
                var data = new SerializedObject(track.Environment);
                var sun = (Light)data.FindProperty("sunlight").objectReferenceValue;
                var fill = (Light)data.FindProperty("skyFill").objectReferenceValue;
                Assert.That(sun.shadowStrength, Is.EqualTo(1)); Assert.That(fill.intensity, Is.LessThan(0.13f));
                var a = data.FindProperty("atmospheres").GetArrayElementAtIndex(0);
                Assert.That(a.FindPropertyRelative("SunIntensity").floatValue, Is.GreaterThan(1.6f));
                Color ambient = a.FindPropertyRelative("AmbientEquator").colorValue;
                Assert.That(ambient.b, Is.GreaterThan(ambient.r)); Assert.That(ambient.maxColorComponent, Is.LessThan(0.75f));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
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
        public void CityUsesDedicatedLowPolySakuraWithLodsClearRoadAndNoRuntimeColliders()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(JapaneseCityBuilder.ScenePath);
            try
            {
                var track = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EndlessTrack>(true)).Single();
                var blossom = AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityLowPolySakura.CanopyPath);
                Assert.That(blossom, Is.Not.Null);
                int total = 0;
                foreach (var segment in track.Segments)
                {
                    var avenue = segment.Environment.Biomes[0].transform.Find(JapaneseCitySidewalks.TreesName);
                    var presentation = avenue.GetComponent<CitySakuraTrees>();
                    Assert.That(presentation.Trees.Length, Is.EqualTo(4));
                    Assert.That(presentation.Homes.Length, Is.EqualTo(4));
                    Assert.That(avenue.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(avenue.GetComponentsInChildren<MonoBehaviour>(true).Length, Is.EqualTo(1), "Only the pooled presentation component, no imported scripts.");
                    foreach (var tree in presentation.Trees)
                    {
                        total++;
                        string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tree.gameObject);
                        Assert.That(Enumerable.Range(0, JapaneseCityLowPolySakura.Variants).Any(v => source == JapaneseCityLowPolySakura.PrefabPath(v)), Is.True, source);
                        Assert.That(tree.GetComponent<LODGroup>().GetLODs().Length, Is.GreaterThanOrEqualTo(3));
                        Assert.That(tree.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetStaticEditorFlags(t.gameObject) == 0), Is.True, "Pooled/bent trees must not be statically batched in builds.");
                        Assert.That(tree.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials), Does.Contain(blossom));
                        Assert.That(tree.GetComponentsInChildren<MeshFilter>(true).All(f => AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith(JapaneseCityLowPolySakura.ArtPath + "/")), Is.True);
                        Bounds bounds = JapaneseCitySidewalks.BoundsOfTree(tree.gameObject, true);
                        Assert.That(bounds.min.y, Is.EqualTo(0.225f).Within(0.003f));
                        Assert.That(bounds.size.y, Is.InRange(3, 7.41f));
                        Assert.That(bounds.min.x > 5.9f || bounds.max.x < -5.9f, Is.True, "Every solid-geometry LOD stays outside the road: " + bounds);
                    }
                }
                Assert.That(total, Is.EqualTo(32));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
        public void SidewalksHaveReferenceRectangularSlabsJointsCurbsOuterPathsAndSoilBeds(int layout)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(JapaneseCityBuilder.ArtPath + "/Layouts/Japanese Sakura Street " + layout + ".asset");
            for (int surface = 22; surface < JapaneseCitySidewalks.SurfaceCount; surface++)
                Assert.That(mesh.GetIndexCount(surface), Is.GreaterThan(30), "Slab color variants, mortar, curb, soil and gutter: " + surface);
            Vector3[] vertices = mesh.vertices;
            Assert.That(mesh.GetTriangles(22).Select(i => vertices[i].y).Max(), Is.EqualTo(0.23f).Within(0.001f));
            Assert.That(mesh.GetTriangles(25).Select(i => vertices[i].y).Max(), Is.EqualTo(0.2f).Within(0.001f), "Joints are physically recessed, not painted on.");
            Assert.That(mesh.GetTriangles(26).Select(i => vertices[i].y).Max(), Is.EqualTo(0.255f).Within(0.001f));
            foreach (int surface in new[] { 22, 23, 24 })
                foreach (int i in mesh.GetTriangles(surface))
                {
                    Vector3 v = vertices[i]; float x = Mathf.Abs(v.x);
                    Assert.That(x > 8.301f && x < 10.299f && (Mathf.Abs(v.z - 8.05f) < 0.999f || Mathf.Abs(v.z - 29.05f) < 0.999f), Is.False, "Paving is cut away for open tree beds.");
                }
            Vector3[] normals = mesh.normals;
            int[] curb = mesh.GetTriangles(26);
            for (int triangle = 0; triangle < curb.Length; triangle += 3)
            {
                Vector3 a = vertices[curb[triangle]], b = vertices[curb[triangle + 1]], c = vertices[curb[triangle + 2]];
                Vector3 geometric = Vector3.Cross(b - a, c - a).normalized;
                Assert.That(Vector3.Dot(geometric, normals[curb[triangle]]), Is.GreaterThan(0.99f));
                if (a.y > 0.23f && b.y > 0.23f && c.y > 0.23f)
                    Assert.That(geometric.y, Is.GreaterThan(0), "Top and bevel faces must point upward/outward to catch the daylight.");
            }
            var slab = AssetDatabase.LoadAssetAtPath<Material>(JapaneseCityBuilder.ArtPath + "/Materials/Sidewalk sandstone.mat");
            Color color = slab.GetColor("_BaseColor");
            Assert.That(color.r, Is.GreaterThan(color.g)); Assert.That(color.g, Is.GreaterThan(color.b));
        }

        [Test]
        public void LowPolySakuraFollowsBendRiseAndOriginShiftWithoutCumulativeDrift()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(JapaneseCityBuilder.ScenePath);
            Vector4 curve = Shader.GetGlobalVector("_CubeDashRoadCurve"), origin = Shader.GetGlobalVector("_CubeDashRoadOrigin");
            float time = Shader.GetGlobalFloat("_CubeDashEnvironmentTime");
            try
            {
                var track = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EndlessTrack>(true)).Single();
                var segment = track.Segments[4];
                var trees = segment.Environment.Biomes[0].GetComponentInChildren<CitySakuraTrees>(true);
                var rise = segment.GetComponent<SceneryRise>();
                rise.Settle(); RoadCurve.Apply(800, 2.3f, 1, 0); trees.SyncPresentation();
                Vector3[] positions = trees.Trees.Select(t => t.position).ToArray();
                for (int i = 0; i < trees.Trees.Length; i++)
                {
                    Vector3 home = trees.transform.TransformPoint(trees.Homes[i]);
                    float bend = RoadCurve.Offset(home.z, 800, 2.3f);
                    Assert.That(trees.Trees[i].position.x, Is.EqualTo(home.x + bend).Within(0.001f));
                    Assert.That(trees.Trees[i].position.y, Is.EqualTo(home.y).Within(0.001f));
                }
                trees.SyncPresentation();
                for (int i = 0; i < positions.Length; i++) Assert.That(Vector3.Distance(trees.Trees[i].position, positions[i]), Is.LessThan(0.001f));
                segment.transform.position -= Vector3.forward * 4096;
                RoadCurve.Apply(800, 2.3f, 1, -4096); trees.SyncPresentation();
                for (int i = 0; i < positions.Length; i++) Assert.That(Vector3.Distance(trees.Trees[i].position, positions[i] - Vector3.forward * 4096), Is.LessThan(0.002f));
                Shader.SetGlobalFloat("_CubeDashEnvironmentTime", 20); rise.Begin(true, 20); trees.SyncPresentation();
                for (int i = 0; i < trees.Trees.Length; i++) Assert.That(trees.Trees[i].localPosition.y, Is.LessThan(trees.Homes[i].y - 79));
                rise.Settle(); RoadCurve.Clear(); trees.SyncPresentation();
                for (int i = 0; i < trees.Trees.Length; i++) Assert.That(Vector3.Distance(trees.Trees[i].localPosition, trees.Homes[i]), Is.LessThan(0.001f));
            }
            finally
            {
                Shader.SetGlobalVector("_CubeDashRoadCurve", curve); Shader.SetGlobalVector("_CubeDashRoadOrigin", origin);
                Shader.SetGlobalFloat("_CubeDashEnvironmentTime", time); EditorSceneManager.ClosePreviewScene(scene);
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
            JapaneseCitySidewalks.ApplyFromCommandLine();
            JapaneseCityReferenceFinish.ApplyFromCommandLine();
            JapaneseCityStreetRepair.ApplyFromCommandLine();
            JapaneseCityLowPolySakura.ApplyFromCommandLine();
            for (int i = 0; i < files.Length; i++) Assert.That(Hash(files[i]), Is.EqualTo(hashes[i]), files[i]);
        }

        [UnityTest]
        public IEnumerator CityRendersGameplayStorefrontAndRecycledCurvedRoad()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(JapaneseCityBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); game.Track.ResetForward(7321, CubeColor.Red, game.Player.position.z);
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
            {
                game.Player.position += Vector3.forward * 42; camera.transform.position += Vector3.forward * 42;
                game.Track.AdvanceForward(42, 0, 0, LevelProgression.Difficulty((step + 1) * 42), Vector2.one * 0.45f,
                    game.Player.position.z, false, false, true, false, false, 8, game.Player.position, 0.1f, out _, out _);
            }
            foreach (var rise in game.Track.GetComponentsInChildren<SceneryRise>()) rise.Settle();
            Assert.That(game.Track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(game.Track.Environment.CurrentBiome, Is.EqualTo(EnvironmentBiome.City));
            Capture(camera, "curved-road");
            canvas.enabled = true;
            foreach (string path in new[] { "Assets/Shaders/JapaneseCityLettering.shader", "Assets/Shaders/JapaneseCitySky.shader", "Assets/Shaders/JapaneseCitySurface.shader", JapaneseCityLowPolySakura.ShaderPath })
                Assert.That(ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>(path)), Is.False, path);
            yield return new ExitPlayMode();
        }

        private static void Capture(Camera camera, string name)
        {
            foreach (var trees in Object.FindObjectsByType<CitySakuraTrees>(FindObjectsSortMode.None)) trees.SyncPresentation();
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
