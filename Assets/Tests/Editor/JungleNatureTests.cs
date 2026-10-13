using System.Collections;
using System.Collections.Generic;
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
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class JungleNatureTests
    {
        private const string Original = "Assets/Prefab/CubeDash/Environments/Jungle.prefab";

        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        [Test]
        public void SavedJungleUsesIdyllicTexturesAndOriginalForestPaletteWithoutAddingObjects()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(JungleNatureBuilder.ScenePath);
            Scene level = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Original);
                GameObject oldJungle = Find<EndlessTrack>(level).Segments[0].Environment.Biomes[(int)EnvironmentBiome.Jungle];
                var sourcePalette = source.transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials;
                foreach (var segment in Find<EndlessTrack>(scene).Segments)
                {
                    GameObject jungle = segment.Environment.Biomes[(int)EnvironmentBiome.Jungle];
                    Assert.That(jungle.GetComponentsInChildren<Transform>(true).Length,
                        Is.EqualTo(oldJungle.GetComponentsInChildren<Transform>(true).Length), "Keep the existing small scenery pool, including its boundary dressing.");
                    Assert.That(jungle.GetComponentsInChildren<Collider>(true), Is.Empty);
                    var scenery = jungle.GetComponent<BiomeScenery>();
                    Assert.That(scenery.Layouts.Length, Is.EqualTo(8));
                    Assert.That(scenery.Layouts.All(mesh => AssetDatabase.GetAssetPath(mesh).StartsWith(JungleNatureBuilder.ArtPath + "/Layouts/")), Is.True);
                    Assert.That(scenery.EntranceLayouts, Is.EqualTo(scenery.Layouts));
                    Assert.That(scenery.ExitLayouts, Is.EqualTo(scenery.Layouts));
                    Assert.That(scenery.VarietyLayouts, Is.EqualTo(source.GetComponent<BiomeScenery>().VarietyLayouts));
                    var palette = jungle.transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials;
                    Assert.That(palette.Take(15), Is.EqualTo(sourcePalette));
                    foreach (Material material in palette.Skip(15))
                    {
                        Assert.That(material.shader.name, Is.EqualTo("CubeDash/Jungle Vegetation"));
                        Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                        Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")), Does.StartWith("Assets/Idyllic Fantasy Nature/Textures/"));
                        Assert.That(material.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
                        Assert.That(material.FindPass("DepthOnly"), Is.GreaterThanOrEqualTo(0));
                        Assert.That(material.FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0));
                        if (material.name.Contains("Bark")) continue;
                        Color.RGBToHSV(material.GetColor("_BaseColor"), out float hue, out _, out float value);
                        Color.RGBToHSV(sourcePalette[1].GetColor("_BaseColor"), out float oldHue, out _, out float oldValue);
                        Assert.That(hue, Is.EqualTo(oldHue).Within(0.005f));
                        Assert.That(value, Is.InRange(oldValue, oldValue * 1.1f));
                        Assert.That(material.GetFloat("_Smoothness"), Is.LessThan(0.15f));
                    }
                    foreach (string name in new[] { "River", "Landscape Ground" })
                    {
                        var saved = jungle.transform.Find(name); var old = source.transform.Find(name);
                        Assert.That(saved.localPosition, Is.EqualTo(old.localPosition));
                        Assert.That(saved.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(old.GetComponent<MeshFilter>().sharedMesh));
                        Assert.That(saved.GetComponent<Renderer>().sharedMaterials, Is.EqualTo(old.GetComponent<Renderer>().sharedMaterials));
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); EditorSceneManager.ClosePreviewScene(level); }
        }

        [Test]
        public void AllEightLayoutsReplaceOldPlantsPreserveLandAndKeepNewPlantingGroundedAndOffRoad()
        {
            var originals = AssetDatabase.LoadAssetAtPath<GameObject>(Original).GetComponent<BiomeScenery>().Layouts;
            for (int layout = 0; layout < 8; layout++)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(JungleNatureBuilder.ArtPath + "/Layouts/Idyllic Jungle Layout " + (layout + 1) + ".asset");
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.LessThan(250000), "Use the pack's middle LOD, not full-resolution trees in every section.");
                for (int surface = 0; surface < 3; surface++) Assert.That(mesh.GetIndexCount(surface), Is.Zero);
                Vector3[] vertices = mesh.vertices, oldVertices = originals[layout].vertices;
                for (int surface = 3; surface < 15; surface++)
                {
                    int[] saved = mesh.GetTriangles(surface), old = originals[layout].GetTriangles(surface);
                    Assert.That(saved.Length, Is.EqualTo(old.Length));
                    for (int i = 0; i < old.Length; i++)
                        Assert.That(vertices[saved[i]], Is.EqualTo(oldVertices[old[i]]), "Preserve terrain, moss and boulder geometry exactly.");
                }
                var feet = new Dictionary<Vector2, float>();
                Vector2[] anchors = mesh.uv3, wind = mesh.uv2;
                Assert.That(anchors.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                for (int surface = 15; surface < mesh.subMeshCount; surface++)
                    foreach (int index in mesh.GetTriangles(surface).Distinct())
                    {
                        Vector2 anchor = anchors[index];
                        Assert.That(Mathf.Abs(vertices[index].x), Is.GreaterThan(6.5f), "Foliage must never obscure the road or guardrails.");
                        Assert.That(Mathf.Abs(anchor.x + 29), Is.GreaterThan(3), "Plant feet remain outside the river channel.");
                        Assert.That(wind[index].x, Is.InRange(0, 1));
                        feet[anchor] = feet.TryGetValue(anchor, out float minimum) ? Mathf.Min(minimum, vertices[index].y) : vertices[index].y;
                    }
                Assert.That(feet.Count, Is.EqualTo(236), "246 plants; ten riverbank bushes share their grass-skirt anchors.");
                int[] terrain = mesh.GetTriangles(7);
                foreach (var foot in feet)
                    Assert.That(foot.Value, Is.EqualTo(Ground(vertices, terrain, foot.Key.x, foot.Key.y) - 0.1f).Within(0.25f), "No floating trunks or grass on the hills.");
            }
        }

        [Test]
        public void SectionRecyclingAlwaysSelectsNewLayoutsWithoutGrowingHierarchy()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(JungleNatureBuilder.ScenePath);
            try
            {
                foreach (var segment in Find<EndlessTrack>(scene).Segments)
                {
                    var jungle = segment.Environment.Biomes[(int)EnvironmentBiome.Jungle];
                    var scenery = jungle.GetComponent<BiomeScenery>();
                    int count = jungle.GetComponentsInChildren<Transform>(true).Length;
                    var filter = jungle.transform.Find("Landscape Details").GetComponent<MeshFilter>();
                    for (int section = -1; section < 96; section++)
                    {
                        segment.Environment.Configure(section, 8, EnvironmentBiome.Jungle);
                        Assert.That(filter.sharedMesh, Is.SameAs(scenery.DetailLayout(section, 8, true)));
                        Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Does.StartWith(JungleNatureBuilder.ArtPath));
                    }
                    Assert.That(jungle.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator JungleRendersNewFoliageInGameplayRiverAndCurvedRoadViews()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(JungleNatureBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); game.TogglePause();
            game.Track.Reset(7321, CubeColor.Red);
            foreach (var rise in game.Track.GetComponentsInChildren<SceneryRise>()) rise.Settle();
            var camera = game.GameCamera;
            var canvas = Object.FindAnyObjectByType<Canvas>(); canvas.enabled = false;
            Capture(camera, "gameplay");
            Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            camera.transform.position = new Vector3(-22, 3.5f, -6);
            camera.transform.LookAt(new Vector3(-29, 0, 45));
            Capture(camera, "riverbank");
            camera.transform.SetPositionAndRotation(position, rotation);
            for (int step = 0; step < 16; step++)
                game.Track.Advance(42, 0, 0, LevelProgression.Difficulty((step + 1) * 42), Vector2.one * 0.45f,
                    0, false, false, true, false, out _, out _);
            foreach (var rise in game.Track.GetComponentsInChildren<SceneryRise>()) rise.Settle();
            Capture(camera, "curved-road");
            canvas.enabled = true;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/JungleVegetation.shader");
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            yield return new ExitPlayMode();
        }

        [Test]
        public void ReapplyingPreservesSavedSceneAndPlantingEdits()
        {
            var paths = Directory.GetFiles(JungleNatureBuilder.ArtPath, "*", SearchOption.AllDirectories)
                .Concat(new[] { JungleNatureBuilder.ScenePath }).ToArray();
            var before = paths.ToDictionary(path => path, Fingerprint);
            JungleNatureBuilder.ApplyFromCommandLine();
            foreach (string path in paths) Assert.That(Fingerprint(path), Is.EqualTo(before[path]), path);
        }

        private static string Fingerprint(string path)
        {
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(path)) return System.BitConverter.ToString(hash.ComputeHash(stream));
        }

        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        private static float Ground(Vector3[] vertices, int[] triangles, float x, float z)
        {
            float height = -9;
            for (int tile = -1; tile <= 1; tile++)
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    float sampleZ = z + tile * 42;
                    float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(denominator) < 0.000001f) continue;
                    float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (sampleZ - c.z)) / denominator;
                    float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (sampleZ - c.z)) / denominator;
                    if (u >= -0.00001f && v >= -0.00001f && u + v <= 1.00001f)
                        height = Mathf.Max(height, a.y * u + b.y * v + c.y * (1 - u - v));
                }
            return height;
        }

        private static void Capture(Camera camera, string name)
        {
            var previous = camera.targetTexture; var active = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(1440, 900, 24); var image = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target;
                for (int frame = 0; frame < 4; frame++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); image.Apply();
                Assert.That(image.GetPixels32().Count(pixel => pixel.r > 230 && pixel.b > 230 && pixel.g < 30), Is.LessThan(20), "No missing/pink foliage shaders.");
                string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_JUNGLE_PREVIEW");
                if (!string.IsNullOrEmpty(prefix)) File.WriteAllBytes(prefix + "-" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active; ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
