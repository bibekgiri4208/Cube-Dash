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
                    Assert.That(scenery.VarietyLayouts.All(mesh => AssetDatabase.GetAssetPath(mesh).StartsWith(JungleNatureBuilder.ArtPath)), Is.True);
                    var varietyPalette = jungle.transform.Find("Landscape Variety").GetComponent<Renderer>().sharedMaterials;
                    Assert.That(varietyPalette.Length, Is.EqualTo(18));
                    Assert.That(varietyPalette.Skip(15).All(material => material != null && material.shader.name == "CubeDash/Jungle Vegetation"), Is.True);
                    var palette = jungle.transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials;
                    Assert.That(palette.Take(15), Is.EqualTo(sourcePalette));
                    foreach (Material material in palette.Skip(15))
                    {
                        Assert.That(material.shader.name, Is.EqualTo("CubeDash/Jungle Vegetation"));
                        Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                        Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")), Does.StartWith(material.name.Contains("Stone_Medium")
                            ? JungleSceneryRepair.StonePath + "textures/" : "Assets/Idyllic Fantasy Nature/Textures/"));
                        Assert.That(material.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
                        Assert.That(material.FindPass("DepthOnly"), Is.GreaterThanOrEqualTo(0));
                        Assert.That(material.FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0));
                        Assert.That(material.GetFloat("_BushLighting"), Is.EqualTo(material.name.Contains("Bush_") ? 1 : 0),
                            "Only bushes use the corrected opposing-leaf lighting; trees, grass and rocks retain their existing finish.");
                        if (material.name.Contains("Bark") || material.name.Contains("Rock") || material.name.Contains("Stone"))
                        {
                            Assert.That(material.GetFloat("_WindStrength"), Is.Zero);
                            Assert.That(material.GetFloat("_Foliage"), Is.Zero);
                            if (material.name.Contains("Stone"))
                            {
                                Color stone = material.GetColor("_BaseColor"), originalStone = sourcePalette[3].GetColor("_BaseColor");
                                Color.RGBToHSV(stone, out float stoneHue, out float stoneSaturation, out float stoneValue);
                                Color.RGBToHSV(originalStone, out float originalHue, out float originalSaturation, out float originalValue);
                                Assert.That(stoneHue, Is.EqualTo(originalHue).Within(0.06f), "Keep the environment's grey-green stone family.");
                                Assert.That(stoneSaturation, Is.LessThanOrEqualTo(originalSaturation));
                                Assert.That(stoneValue / originalValue, Is.InRange(0.5f, 0.7f), "Compensate for the new pack's white AO texture rather than rendering pale chalk rocks.");
                                Assert.That(material.GetFloat("_Smoothness"), Is.LessThan(0.1f));
                            }
                            continue;
                        }
                        Assert.That(material.GetFloat("_Foliage"), Is.EqualTo(1), "Leaf cards must use wrapped, softly shadowed lighting.");
                        Color.RGBToHSV(material.GetColor("_BaseColor"), out float hue, out float saturation, out float value);
                        Color.RGBToHSV(sourcePalette[1].GetColor("_BaseColor"), out float oldHue, out float oldSaturation, out float oldValue);
                        Assert.That(hue, Is.EqualTo(oldHue).Within(0.005f));
                        float rainforestValue = oldValue * (material.name.Contains("Grass_") ? 0.58f : material.name.Contains("Bush_") ? 0.60f : 0.72f);
                        Assert.That(value, Is.EqualTo(rainforestValue).Within(0.0001f), "Retain the deeper rainforest tone without bright/neon foliage.");
                        Assert.That(material.GetFloat("_LeafLightGain"), Is.EqualTo(material.name.Contains("Grass_") ? 0.72f : 0.76f).Within(0.0001f),
                            "Direct sun must not wash the rainforest foliage back into light green.");
                        Assert.That(saturation, Is.LessThan(oldSaturation * 0.83f), "Keep the family green, but less saturated.");
                        if (material.name.Contains("Bush_"))
                        {
                            Color.RGBToHSV(material.GetColor("_TopColor"), out float topHue, out float topSaturation, out float topValue);
                            Color.RGBToHSV(sourcePalette[2].GetColor("_BaseColor"), out float sourceTopHue, out float sourceTopSaturation, out float sourceTopValue);
                            Assert.That(topHue, Is.EqualTo(Mathf.Repeat(sourceTopHue - 0.018f, 1)).Within(0.0001f));
                            Assert.That(topSaturation, Is.EqualTo(sourceTopSaturation * 0.88f).Within(0.0001f));
                            Assert.That(topValue, Is.EqualTo(sourceTopValue * 0.78f).Within(0.0001f));
                            Assert.That(topValue, Is.GreaterThan(value * 1.4f), "Bushes need two distinct rainforest greens, not nearly identical tints.");
                        }
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
                Assert.That(mesh.GetIndexCount(3), Is.Zero, "The old circular boulders are gone.");
                Assert.That(mesh.GetIndexCount(13), Is.Zero, "No separate green caps on the rocks.");
                for (int surface = 7; surface <= 7; surface++)
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
                var stoneFeet = new HashSet<Vector2>();
                Scene preview = EditorSceneManager.OpenPreviewScene(JungleNatureBuilder.ScenePath);
                Material[] palette;
                try { palette = Find<EndlessTrack>(preview).Segments[0].Environment.Biomes[(int)EnvironmentBiome.Jungle]
                    .transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials; }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
                for (int surface = 15; surface < mesh.subMeshCount; surface++)
                    foreach (int index in mesh.GetTriangles(surface).Distinct())
                    {
                        Vector2 anchor = anchors[index];
                        Assert.That(Mathf.Abs(vertices[index].x), Is.GreaterThan(6.5f), "Foliage must never obscure the road or guardrails.");
                        Assert.That(Mathf.Abs(anchor.x + 29), Is.GreaterThan(3), "Plant feet remain outside the river channel.");
                        Assert.That(wind[index].x, Is.InRange(0, 1));
                        if (palette[surface].name.Contains("Stone_Medium")) stoneFeet.Add(anchor);
                        else feet[anchor] = feet.TryGetValue(anchor, out float minimum) ? Mathf.Min(minimum, vertices[index].y) : vertices[index].y;
                    }
                Assert.That(feet.Count, Is.EqualTo(236), "Preserve the existing planting; ten riverbank bushes share grass-skirt anchors.");
                Assert.That(stoneFeet.Count, Is.EqualTo(10), "Ten footprint-grounded package stones; validated separately from point-grounded plants.");
                int[] terrain = mesh.GetTriangles(7);
                foreach (var foot in feet)
                    Assert.That(foot.Value, Is.EqualTo(Ground(vertices, terrain, foot.Key.x, foot.Key.y) - 0.1f).Within(0.25f), "No floating trunks or grass on the hills.");
            }
        }

        [Test]
        public void NewStonesAreBuriedAtTheirSkirtsAndClearEveryTreeTrunk()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(JungleNatureBuilder.ScenePath);
            try
            {
                var jungle = Find<EndlessTrack>(scene).Segments[0].Environment.Biomes[(int)EnvironmentBiome.Jungle];
                var palette = jungle.transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials;
                foreach (Mesh mesh in jungle.GetComponent<BiomeScenery>().Layouts)
                {
                    Vector3[] vertices = mesh.vertices; Vector2[] anchors = mesh.uv3; int[] terrain = mesh.GetTriangles(7);
                    int[] bark = Enumerable.Range(15, palette.Length - 15).Where(i => palette[i].name.Contains("Bark"))
                        .SelectMany(mesh.GetTriangles).Distinct().ToArray();
                    Bounds[] trunks = bark.GroupBy(index => anchors[index]).Select(group =>
                    {
                        float bottom = group.Min(index => vertices[index].y);
                        return BoundsOf(group.Where(index => vertices[index].y < bottom + 3).Select(index => vertices[index]));
                    }).ToArray();
                    int[] stones = Enumerable.Range(15, palette.Length - 15).Where(i => palette[i].name.Contains("Stone_Medium"))
                        .SelectMany(mesh.GetTriangles).Distinct().ToArray();
                    var groups = stones.GroupBy(index => anchors[index]).ToArray();
                    Assert.That(groups.Length, Is.EqualTo(10));
                    var footprints = new List<Bounds>();
                    foreach (var group in groups)
                    {
                        Vector3[] points = group.Select(index => vertices[index]).ToArray();
                        Bounds bounds = BoundsOf(points);
                        Assert.That(Mathf.Max(bounds.size.x, bounds.size.z), Is.InRange(2, 6));
                        Assert.That(bounds.min.z, Is.GreaterThan(1.5f), "Keep stones clear of section seams.");
                        Assert.That(bounds.max.z, Is.LessThan(40.5f));
                        Vector3[] skirt = points.Where(point => point.y <= bounds.min.y + bounds.size.y * 0.22f + 0.0001f).ToArray();
                        foreach (Vector3 point in skirt)
                            Assert.That(point.y - Ground(vertices, terrain, point.x, point.z), Is.LessThan(-0.075f), "No hovering lower edges on uneven ground.");
                        Vector3 top = points.OrderByDescending(point => point.y).First();
                        Assert.That(top.y - Ground(vertices, terrain, top.x, top.z), Is.GreaterThan(0.45f), "Do not hide the whole stone underground.");
                        bounds.Expand(new Vector3(1.4f, 0, 1.4f));
                        foreach (Bounds trunk in trunks) Assert.That(OverlapsXZ(bounds, trunk), Is.False, "No rocks intersecting trunks or root flares.");
                        foreach (Bounds other in footprints) Assert.That(OverlapsXZ(bounds, other), Is.False, "Keep individual stones separate.");
                        footprints.Add(bounds);
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void BambooHasSevenGroundedSegmentedCulmsAndNarrowLeavesWithoutFanPalms()
        {
            for (int layout = 0; layout < 8; layout++)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(JungleNatureBuilder.ArtPath + "/Layouts/Jungle Understory " + (layout + 1) + ".asset");
                Mesh land = AssetDatabase.LoadAssetAtPath<Mesh>(JungleNatureBuilder.ArtPath + "/Layouts/Idyllic Jungle Layout " + (layout + 1) + ".asset");
                Assert.That(mesh.subMeshCount, Is.EqualTo(18));
                Assert.That(mesh.GetIndexCount(1), Is.Zero, "Retire the palm-frond bamboo and umbrella fan palms.");
                Assert.That(mesh.GetIndexCount(2), Is.Zero);
                Vector3[] vertices = mesh.vertices; Vector2[] anchors = mesh.uv3;
                var stems = mesh.GetTriangles(15).Distinct().GroupBy(index => anchors[index]).ToArray();
                Assert.That(stems.Length, Is.EqualTo(7));
                foreach (var stem in stems)
                {
                    Assert.That(stem.Min(index => vertices[index].y), Is.EqualTo(Ground(land.vertices, land.GetTriangles(7), stem.Key.x, stem.Key.y) - 0.12f).Within(0.02f));
                    float height = stem.Max(index => vertices[index].y) - stem.Min(index => vertices[index].y);
                    Assert.That(height, Is.InRange(6.1f, 9.1f));
                }
                Assert.That(mesh.GetIndexCount(16), Is.GreaterThan(10000), "Visible nodes along all seven culms.");
                int[] leaves = mesh.GetTriangles(17); Assert.That(leaves.Length, Is.EqualTo(7 * 6 * 7 * 12));
                for (int i = 0; i < leaves.Length; i += 12)
                {
                    Bounds leaf = BoundsOf(leaves.Skip(i).Take(12).Select(index => vertices[index]));
                    Assert.That(leaf.size.magnitude, Is.LessThan(0.9f), "Bamboo has slender lance leaves, not oversized palm fans.");
                }
                foreach (int index in mesh.GetTriangles(0))
                {
                    Vector2 anchor = anchors[index];
                    Assert.That(Mathf.Abs(anchor.x - 13.5f) < 0.01f || (layout % 3 == 0 && Mathf.Abs(anchor.x - 28) < 0.01f), Is.True,
                        "Only the fallen log and occasional gateway retain legacy geometry; no palm trunks.");
                }
            }
        }

        private static Bounds BoundsOf(IEnumerable<Vector3> points)
        {
            var bounds = new Bounds(points.First(), Vector3.zero);
            foreach (Vector3 point in points) bounds.Encapsulate(point);
            return bounds;
        }

        private static bool OverlapsXZ(Bounds a, Bounds b) => a.min.x < b.max.x && a.max.x > b.min.x && a.min.z < b.max.z && a.max.z > b.min.z;

        [Test]
        public void BananaGeometryAndGreenMossCapsAreRemovedInEveryVarietyLayout()
        {
            for (int layout = 0; layout < 8; layout++)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(JungleNatureBuilder.ArtPath + "/Layouts/Jungle Understory " + (layout + 1) + ".asset");
                Assert.That(mesh, Is.Not.Null); Assert.That(mesh.GetIndexCount(13), Is.Zero);
                Vector2[] anchors = mesh.uv3;
                foreach (int index in mesh.triangles)
                    Assert.That(Mathf.Abs(anchors[index].x + 12.5f) < 0.01f && anchors[index].y > 24 && anchors[index].y < 30,
                        Is.False, "Remove the entire banana plant, including its trunk and fruit.");
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
            float fieldOfView = camera.fieldOfView;
            var canvas = Object.FindAnyObjectByType<Canvas>(); canvas.enabled = false;
            Capture(camera, "gameplay");
            Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            camera.transform.position = new Vector3(-22, 3.5f, -6);
            camera.transform.LookAt(new Vector3(-29, 0, 45));
            Capture(camera, "riverbank");
            FocusOnScenery(game, camera, true);
            Capture(camera, "bamboo-closeup");
            FocusOnScenery(game, camera, false);
            Capture(camera, "stone-closeup");
            FocusOnBush(game, camera);
            Capture(camera, "bush-closeup");
            camera.fieldOfView = fieldOfView;
            camera.transform.SetPositionAndRotation(position, rotation);
            for (int step = 0; step < 16; step++)
                game.Track.Advance(42, 0, 0, LevelProgression.Difficulty((step + 1) * 42), Vector2.one * 0.45f,
                    0, false, false, true, false, out _, out _);
            foreach (var rise in game.Track.GetComponentsInChildren<SceneryRise>()) rise.Settle();
            Capture(camera, "curved-road");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/JungleVegetation.shader");
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            AssertBushLeafSidesMatch(shader);
            canvas.enabled = true;
            yield return new ExitPlayMode();
        }

        private static void FocusOnScenery(CubeDashGame game, Camera camera, bool bamboo)
        {
            var segment = game.Track.Segments.OrderBy(item => Mathf.Abs(item.transform.position.z)).First();
            Transform details = segment.Environment.Biomes[(int)EnvironmentBiome.Jungle].transform.Find(bamboo ? "Landscape Variety" : "Landscape Details");
            Mesh mesh = details.GetComponent<MeshFilter>().sharedMesh;
            Material[] materials = details.GetComponent<Renderer>().sharedMaterials;
            int surface = bamboo ? 15 : System.Array.FindIndex(materials, material => material.name.Contains("Stone_Medium"));
            Vector2[] anchors = mesh.uv3; Vector3[] vertices = mesh.vertices;
            int[] indices = mesh.GetTriangles(surface).Distinct().ToArray();
            Vector2 anchor = anchors[indices.OrderBy(index => (anchors[index] - new Vector2(18, 10)).sqrMagnitude).First()];
            IEnumerable<int> target = bamboo ? Enumerable.Range(15, 3).SelectMany(mesh.GetTriangles).Distinct()
                : indices.Where(index => anchors[index] == anchor);
            Bounds bounds = BoundsOf(target.Select(index => details.TransformPoint(vertices[index])));
            camera.transform.position = bounds.center + (bamboo ? new Vector3(8, 2, -12) : new Vector3(4, 2, -6));
            camera.transform.LookAt(bounds.center); camera.fieldOfView = bamboo ? 48 : 45;
        }

        private static void FocusOnBush(CubeDashGame game, Camera camera)
        {
            var segment = game.Track.Segments.OrderBy(item => Mathf.Abs(item.transform.position.z)).First();
            Transform details = segment.Environment.Biomes[(int)EnvironmentBiome.Jungle].transform.Find("Landscape Details");
            Mesh mesh = details.GetComponent<MeshFilter>().sharedMesh;
            Material[] materials = details.GetComponent<Renderer>().sharedMaterials;
            int surface = System.Array.FindIndex(materials, material => material.name == "Jungle Idyllic Bush_01");
            Vector2[] anchors = mesh.uv3; Vector3[] vertices = mesh.vertices;
            int[] indices = mesh.GetTriangles(surface).Distinct().ToArray();
            Vector2 anchor = anchors[indices.OrderBy(index => (anchors[index] - new Vector2(-14, 10)).sqrMagnitude).First()];
            var points = indices.Where(index => anchors[index] == anchor).Select(index => details.TransformPoint(vertices[index])).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 point in points) bounds.Encapsulate(point);
            camera.transform.position = bounds.center + new Vector3(3, 1.4f, -4.5f);
            camera.transform.LookAt(bounds.center); camera.fieldOfView = 45;
        }

        private static void AssertBushLeafSidesMatch(Shader shader)
        {
            var material = new Material(shader);
            material.SetColor("_BaseColor", new Color(0.15f, 0.32f, 0.21f));
            material.SetColor("_TopColor", new Color(0.15f, 0.32f, 0.21f));
            material.SetFloat("_BushLighting", 1); material.SetFloat("_WindStrength", 0);
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.normals = Enumerable.Repeat(Vector3.forward, 4).ToArray();
            Mesh reverse = Object.Instantiate(mesh); reverse.normals = Enumerable.Repeat(Vector3.back, 4).ToArray();
            var root = new GameObject("Temporary bush leaf-side lighting probe");
            var camera = root.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 1;
            camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(0, 0, -10);
            var target = new RenderTexture(256, 128, 24); var image = new Texture2D(256, 128, TextureFormat.RGB24, false);
            Vector4 curve = Shader.GetGlobalVector("_CubeDashRoadCurve"); var active = RenderTexture.active;
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                Shader.SetGlobalVector("_CubeDashRoadCurve", Vector4.zero);
                ShaderUtil.allowAsyncCompilation = false;
                for (int side = 0; side < 2; side++)
                {
                    var leaf = new GameObject("Leaf side", typeof(MeshFilter), typeof(MeshRenderer));
                    leaf.transform.SetParent(root.transform, false); leaf.layer = 31;
                    leaf.transform.position = new Vector3(side == 0 ? -0.75f : 0.75f, 0, 0);
                    leaf.GetComponent<MeshFilter>().sharedMesh = side == 0 ? mesh : reverse;
                    leaf.GetComponent<Renderer>().sharedMaterial = material;
                    leaf.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                camera.targetTexture = target;
                for (int frame = 0; frame < 4; frame++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 256, 128), 0, 0); image.Apply();
                Color front = image.GetPixel(80, 64), back = image.GetPixel(176, 64);
                Assert.That(front.g, Is.GreaterThan(0.08f), "Bush leaves must not render black.");
                Assert.That(front.g, Is.GreaterThan(front.r));
                Assert.That(back.g, Is.EqualTo(front.g).Within(0.025f), "Opposing leaf normals must not produce the pale-green/black checker pattern.");

                // Exercise the saved two-tone palette without changing normals or lighting.
                var bush = AssetDatabase.LoadAssetAtPath<Material>(JungleNatureBuilder.ArtPath + "/Materials/Jungle Idyllic Bush_01.mat");
                material.CopyPropertiesFromMaterial(bush); material.SetFloat("_WindStrength", 0);
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                mesh.colors = Enumerable.Repeat(new Color(0, 0, 1, 1), 4).ToArray();
                reverse.colors = Enumerable.Repeat(new Color(0.7f, 0, 1, 1), 4).ToArray();
                camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 256, 128), 0, 0); image.Apply();
                Color lower = image.GetPixel(80, 64), upper = image.GetPixel(176, 64);
                Assert.That(upper.g, Is.GreaterThan(lower.g * 1.2f), "Upper and lower bush foliage must render visibly different greens.");
                Assert.That(lower.g, Is.GreaterThan(lower.r));
                Assert.That(upper.g, Is.GreaterThan(upper.r));
            }
            finally
            {
                Shader.SetGlobalVector("_CubeDashRoadCurve", curve); RenderTexture.active = active;
                ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(root); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                Object.DestroyImmediate(mesh); Object.DestroyImmediate(reverse); Object.DestroyImmediate(material);
            }
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
