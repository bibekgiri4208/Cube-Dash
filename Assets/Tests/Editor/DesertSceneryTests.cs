using System.Collections.Generic;
using System.IO;
using System.Linq;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CubeDash.Tests
{
    public sealed class DesertSceneryTests
    {
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
        public void LandscapeAndPlantingCarryValidMeshChannelsAndBoundedBudgets(int layout)
        {
            foreach (string name in new[] { "Canyon Landscape", "Desert Planting" })
            {
                Mesh mesh = Load(name, layout);
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.InRange(4000, 180000));
                Assert.That(mesh.triangles.Length / 3, Is.LessThan(90000));
                Assert.That(mesh.subMeshCount, Is.EqualTo(9));
                Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.uv2.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.uv3.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.colors32.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.vertices.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)), Is.True);
                Assert.That(mesh.normals.All(v => v.sqrMagnitude > 0.9f && v.sqrMagnitude < 1.1f), Is.True);
                Assert.That(mesh.triangles.All(i => i >= 0 && i < mesh.vertexCount), Is.True);
                Assert.That(mesh.bounds.min.y, Is.LessThan(mesh.vertices.Min(v => v.y) - 79));
                int[] indices = mesh.triangles; Vector2[] anchors = mesh.uv3;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Assert.That(anchors[indices[i]], Is.EqualTo(anchors[indices[i + 1]]));
                    Assert.That(anchors[indices[i]], Is.EqualTo(anchors[indices[i + 2]]), "Objects rise rigidly, without disconnected cactus arms.");
                }
            }
            Mesh landscape = Load("Canyon Landscape", layout);
            Vector3[] points = landscape.vertices;
            Assert.That(landscape.GetTriangles(2).Length, Is.GreaterThan(500), "Modeled caprock/strata, not just a painted texture.");
            Assert.That(points.Max(p => p.y), Is.GreaterThan(25));
            Assert.That(points.Where(p => Mathf.Abs(p.x) < 6.5f).Count(), Is.Zero, "No replacement terrain enters the highway.");
            for (int col = 0; col <= 58; col++) foreach (int side in new[] { -1, 1 })
            {
                float x = side * (6.5f + col * 3);
                var start = points.Where(p => Mathf.Abs(p.x - x) < 0.001f && Mathf.Abs(p.z) < 0.001f).ToArray();
                var end = points.Where(p => Mathf.Abs(p.x - x) < 0.001f && Mathf.Abs(p.z - 42) < 0.001f).ToArray();
                Assert.That(start.Length, Is.GreaterThan(0)); Assert.That(end.Length, Is.GreaterThan(0));
                Assert.That(start[0].y, Is.EqualTo(end[0].y).Within(0.00001f), "Continuous section-edge height.");
            }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)] [TestCase(8)]
        public void PlantClustersAreGroundedSeparatedAndOutsideRoadAndPoolSeams(int layout)
        {
            Mesh mesh = Load("Desert Planting", layout);
            Vector3[] points = mesh.vertices; Vector2[] anchors = mesh.uv3;
            var footprints = new Dictionary<Vector2, Bounds>();
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 key = anchors[i];
                if (!footprints.TryGetValue(key, out Bounds bounds)) bounds = new Bounds(points[i], Vector3.zero);
                bounds.Encapsulate(points[i]); footprints[key] = bounds;
            }
            Assert.That(footprints.Count, Is.GreaterThanOrEqualTo(100));
            foreach (var pair in footprints)
            {
                Bounds b = pair.Value;
                Assert.That(Mathf.Min(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)), Is.GreaterThanOrEqualTo(6.69f));
                Assert.That(b.min.z, Is.GreaterThanOrEqualTo(0.59f)); Assert.That(b.max.z, Is.LessThanOrEqualTo(41.41f));
                float floor = float.PositiveInfinity;
                foreach (float x in new[] { b.min.x, b.center.x, b.max.x }) foreach (float z in new[] { b.min.z, b.center.z, b.max.z })
                    floor = Mathf.Min(floor, DesertSceneryBuilder.GroundHeight(x, z));
                Assert.That(b.min.y, Is.EqualTo(floor - 0.12f).Within(0.002f), "Lower skirts must embed in actual dunes, not float on a pivot.");
                foreach (var other in footprints)
                {
                    if (pair.Key == other.Key) continue;
                    Bounds c = other.Value;
                    Assert.That(b.min.x < c.max.x && b.max.x > c.min.x && b.min.z < c.max.z && b.max.z > c.min.z, Is.False, "Reserved plant/stone footprints overlap.");
                }
            }
            Assert.That(mesh.GetTriangles(4).Length, Is.GreaterThan(3000));
            Assert.That(mesh.GetTriangles(6).Length, Is.GreaterThan(10000));
            Assert.That(mesh.GetTriangles(7).Length, Is.GreaterThan(500));
            Assert.That(mesh.uv2.Any(w => w.x > 0.9f), Is.True, "Grass retains height-weighted wind.");
        }

        [Test]
        public void AllEightSectionsUseOneLocalPrefabAndSwapPairedMeshesWithoutSpawning()
        {
            var scene = EditorSceneManager.OpenPreviewScene(DesertSceneryBuilder.ScenePath);
            try
            {
                var track = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EndlessTrack>(true)).Single();
                int count = track.GetComponentsInChildren<Transform>(true).Length;
                var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Desert.prefab");
                foreach (TrackSegment segment in track.Segments)
                {
                    var rails = segment.transform.Find("Highway Guardrails").GetComponent<MeshFilter>().sharedMesh.bounds;
                    Assert.That(Mathf.Max(Mathf.Abs(rails.min.x), Mathf.Abs(rails.max.x)), Is.LessThan(5.7f), "Planting leaves at least a metre beyond the actual guardrail geometry.");
                    GameObject root = segment.Environment.Biomes[(int)EnvironmentBiome.Desert];
                    Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root), Is.EqualTo(DesertSceneryBuilder.PrefabPath));
                    var scenery = root.GetComponent<BiomeScenery>();
                    foreach (Mesh mesh in scenery.Layouts.Concat(scenery.EntranceLayouts).Concat(scenery.ExitLayouts).Concat(scenery.VarietyLayouts))
                        Assert.That(AssetDatabase.GetAssetPath(mesh), Does.StartWith(DesertSceneryBuilder.ArtPath + "/"));
                    for (int index = -1; index < 100; index++)
                    {
                        segment.Environment.Configure(index, 8, EnvironmentBiome.Desert);
                        int variation = BiomeRules.Variation(index, 8);
                        Assert.That(root.transform.Find("Landscape Details").GetComponent<MeshFilter>().sharedMesh, Is.SameAs(scenery.Layouts[variation]));
                        Assert.That(root.transform.Find("Landscape Variety").GetComponent<MeshFilter>().sharedMesh, Is.SameAs(scenery.VarietyLayouts[variation]));
                    }
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer.name.StartsWith("Boundary ", System.StringComparison.Ordinal))
                        {
                            Assert.That(renderer.gameObject.activeSelf, Is.False, "Cross-biome dressing stays disabled in standalone Desert.");
                            Assert.That(renderer.sharedMaterials.Length, Is.GreaterThan(1), "Retain the original inactive boundary palette.");
                            Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterials[0]), Does.Not.StartWith(DesertSceneryBuilder.ArtPath));
                            continue;
                        }
                        foreach (Material material in renderer.sharedMaterials)
                            Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith(DesertSceneryBuilder.ArtPath + "/"));
                    }
                    var rise = new SerializedObject(segment.GetComponent<SceneryRise>()).FindProperty("scenery");
                    foreach (string name in new[] { "Landscape Details", "Landscape Variety" })
                    {
                        var renderer = root.transform.Find(name).GetComponent<Renderer>();
                        Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount));
                        Assert.That(Enumerable.Range(0, rise.arraySize).Any(i => rise.GetArrayElementAtIndex(i).objectReferenceValue == renderer), Is.True);
                    }
                    Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                    var ground = root.transform.Find("Landscape Ground"); var old = original.transform.Find("Landscape Ground");
                    Assert.That(ground.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(old.GetComponent<MeshFilter>().sharedMesh));
                    Assert.That(ground.localPosition, Is.EqualTo(old.localPosition)); Assert.That(ground.localScale, Is.EqualTo(old.localScale));
                }
                Assert.That(track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(AssetDatabase.GetAssetPath(original.GetComponent<BiomeScenery>().Layouts[0]), Does.Not.StartWith(DesertSceneryBuilder.ArtPath));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void DesertKeepsItsLocalArtThroughTenKilometresOfForwardPoolingAndOriginResets()
        {
            var scene = EditorSceneManager.OpenPreviewScene(DesertSceneryBuilder.ScenePath);
            try
            {
                var track = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EndlessTrack>(true)).Single();
                track.ResetForward(7321, CubeColor.Red, 0);
                int count = track.GetComponentsInChildren<Transform>(true).Length;
                float z = 0;
                for (int step = 0; step < 250; step++)
                {
                    z += 42;
                    track.AdvanceForward(42, 0, 0, LevelProgression.Difficulty((step + 1) * 42), Vector2.one * 0.45f,
                        z, false, false, true, false, false, 8, new Vector3(0, 0.575f, z), 0.1f, out _, out _);
                    if (z >= 4096) { track.ShiftWorldOrigin(4096); z -= 4096; }
                    foreach (TrackSegment segment in track.Segments)
                    {
                        Assert.That(segment.Environment.CurrentBiome, Is.EqualTo(EnvironmentBiome.Desert));
                        var root = segment.Environment.Biomes[(int)EnvironmentBiome.Desert];
                        var scenery = root.GetComponent<BiomeScenery>();
                        int variation = BiomeRules.Variation(segment.Environment.SectionIndex, 8);
                        Assert.That(root.transform.Find("Landscape Details").GetComponent<MeshFilter>().sharedMesh, Is.SameAs(scenery.Layouts[variation]));
                        Assert.That(root.transform.Find("Landscape Variety").GetComponent<MeshFilter>().sharedMesh, Is.SameAs(scenery.VarietyLayouts[variation]));
                    }
                }
                Assert.That(track.Travelled, Is.EqualTo(10500)); Assert.That(track.WorldOriginOffset, Is.EqualTo(8192));
                Assert.That(track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); RoadCurve.Clear(); }
        }

        [TestCase("Layered Mesa")] [TestCase("Eroded Butte")] [TestCase("Sandstone Spire")]
        [TestCase("Red Boulder")] [TestCase("Split Boulder")] [TestCase("Sandstone Chips")]
        [TestCase("Saguaro")] [TestCase("Young Saguaro")] [TestCase("Flowering Prickly Pear")]
        [TestCase("Barrel Cactus")] [TestCase("Desert Bunchgrass")] [TestCase("Yucca")]
        public void ModelsAreOriginalDetailedSavedMeshes(string name)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(DesertSceneryBuilder.ArtPath + "/Models/" + name + ".asset");
            Assert.That(mesh, Is.Not.Null); Assert.That(mesh.vertexCount, Is.GreaterThan(150));
            Assert.That(mesh.subMeshCount, Is.EqualTo(9));
            Assert.That(mesh.triangles.Length / 3, Is.LessThan(6000));
            Assert.That(mesh.colors32.Distinct().Count(), Is.GreaterThan(1), "Authored facet/root color variation.");
        }

        [Test]
        public void MaterialsHaveLocalFilteredTextureAndCompleteShaderPasses()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { DesertSceneryBuilder.ArtPath + "/Materials" }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(material.shader.name, Is.EqualTo("CubeDash/Desert Surface"));
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                Assert.That(material.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.FindPass("DepthOnly"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.FindPass("DepthNormals"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.GetFloat("_Smoothness"), Is.LessThan(0.2f));
                string path = AssetDatabase.GetAssetPath(material.GetTexture("_SurfaceMap"));
                Assert.That(path, Does.StartWith(DesertSceneryBuilder.ArtPath + "/Textures/"));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.mipmapEnabled, Is.True); Assert.That(importer.sRGBTexture, Is.False);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat)); Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear));
            }
        }

        [Test]
        public void WarmAtmospherePersistsAtDistanceAndOnlyDesertUsesTheSharpLocalGrade()
        {
            var scene = EditorSceneManager.OpenScene(DesertSceneryBuilder.ScenePath);
            try
            {
                var director = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnvironmentDirector>(true)).Single();
                Color fog = RenderSettings.fogColor;
                director.ApplyDistance(9000, 0);
                Assert.That(RenderSettings.fogColor, Is.EqualTo(fog));
                Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.Linear));
                Assert.That(RenderSettings.fogEndDistance, Is.GreaterThan(300));
                Assert.That(AssetDatabase.GetAssetPath(RenderSettings.skybox), Does.StartWith(DesertSceneryBuilder.ArtPath));
                Assert.That(RenderSettings.skybox.GetFloat("_GradientPower"), Is.LessThan(0.35f), "Warm fog must not turn most of the visible sky grey.");
                Assert.That(RenderSettings.skybox.GetFloat("_CloudCoverage"), Is.LessThan(0.42f));
                var volume = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Volume>(true)).Single(v => v.isGlobal);
                Assert.That(AssetDatabase.GetAssetPath(volume.sharedProfile), Does.StartWith(DesertSceneryBuilder.ArtPath));
                Assert.That(volume.sharedProfile.TryGet(out DepthOfField focus), Is.True); Assert.That(focus.mode.value, Is.EqualTo(DepthOfFieldMode.Off));
                Assert.That(volume.sharedProfile.TryGet(out Tonemapping tone), Is.True); Assert.That(tone.mode.value, Is.EqualTo(TonemappingMode.ACES));
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        [Test]
        public void ReapplicationPreservesEverySavedDesertFile()
        {
            string[] files = Directory.GetFiles(DesertSceneryBuilder.ArtPath, "*", SearchOption.AllDirectories).Append(DesertSceneryBuilder.ScenePath).ToArray();
            string Digest(string path)
            {
                using (var hash = System.Security.Cryptography.SHA256.Create())
                using (var stream = File.OpenRead(path)) return System.Convert.ToBase64String(hash.ComputeHash(stream));
            }
            var before = files.ToDictionary(path => path, Digest);
            try
            {
                DesertSceneryBuilder.ApplyFromCommandLine();
                Assert.That(Directory.GetFiles(DesertSceneryBuilder.ArtPath, "*", SearchOption.AllDirectories).Length + 1, Is.EqualTo(files.Length));
                foreach (string path in files) Assert.That(Digest(path), Is.EqualTo(before[path]), path);
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        [Test]
        public void PartialUpgradesAreRejectedWithoutTouchingSavedFiles()
        {
            var scene = EditorSceneManager.OpenScene(DesertSceneryBuilder.ScenePath);
            byte[] before = File.ReadAllBytes(DesertSceneryBuilder.ScenePath);
            try
            {
                var scenery = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<BiomeScenery>(true)).First();
                scenery.VarietyLayouts[0] = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/Environments/Desert.prefab").GetComponent<BiomeScenery>().VarietyLayouts[0];
                Assert.Throws<System.InvalidOperationException>(() => DesertSceneryBuilder.ApplyFromCommandLine());
                Assert.That(File.ReadAllBytes(DesertSceneryBuilder.ScenePath), Is.EqualTo(before));
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }

        [Test]
        public void RenderDesertWhenVisualChecksAreRequested()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CUBE_DASH_DESERT_PREVIEW")))
                Assert.Ignore("Opt-in GPU visual check.");
            DesertSceneryBuilder.CaptureFromCommandLine();
            Shader shader = Shader.Find("CubeDash/Desert Surface");
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }

        [TestCase("Saguaro")] [TestCase("Red Boulder")] [TestCase("Desert Bunchgrass")]
        public void DesertModelsActuallyRenderAndReceiveSunlightWithoutEmission(string name)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("GPU lighting check.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Desert lighting probe", typeof(Camera));
            var sunObject = new GameObject("Probe sun", typeof(Light));
            var model = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            var target = new RenderTexture(128, 128, 24); var image = new Texture2D(128, 128, TextureFormat.RGB24, false);
            var active = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false; RoadCurve.Clear();
                RenderSettings.fog = false; RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = RenderSettings.ambientEquatorColor = RenderSettings.ambientGroundColor = Color.black;
                RenderSettings.ambientIntensity = 0; RenderSettings.reflectionIntensity = 0; RenderSettings.ambientProbe = new SphericalHarmonicsL2();
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(DesertSceneryBuilder.ArtPath + "/Models/" + name + ".asset");
                model.GetComponent<MeshFilter>().sharedMesh = mesh;
                model.GetComponent<Renderer>().sharedMaterials = AssetDatabase.LoadAssetAtPath<GameObject>(DesertSceneryBuilder.PrefabPath)
                    .transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials;
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y) * 0.64f;
                camera.transform.position = mesh.bounds.center + new Vector3(0, 0.1f, -20); camera.transform.LookAt(mesh.bounds.center);
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
                Assert.That(lit.Count(c => c.grayscale > 0.08f), Is.GreaterThan(100), "Real opaque silhouette must render under the sun.");
                Assert.That(lit.Count(c => c.r > 0.8f && c.b > 0.8f && c.g < 0.1f), Is.Zero, "No pink error shader.");
                Assert.That(dark.Max(c => c.maxColorComponent), Is.LessThan(0.04f), "Cactus and sand must not glow in darkness.");
            }
            finally
            {
                cameraObject.GetComponent<Camera>().targetTexture = null; RenderTexture.active = active; ShaderUtil.allowAsyncCompilation = async;
                foreach (Object value in new Object[] { cameraObject, sunObject, model, target, image }) Object.DestroyImmediate(value);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void CactusCastsRealShadowsOntoTexturedSand()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("GPU shadow check.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Desert shadow probe", typeof(Camera));
            var sunObject = new GameObject("Probe sun", typeof(Light));
            var cactus = new GameObject("Cactus", typeof(MeshFilter), typeof(MeshRenderer));
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            var target = new RenderTexture(256, 256, 24); var image = new Texture2D(256, 256, TextureFormat.RGB24, false);
            var active = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false; RoadCurve.Clear();
                RenderSettings.fog = false; RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = RenderSettings.ambientEquatorColor = RenderSettings.ambientGroundColor = Color.black;
                RenderSettings.ambientIntensity = 0; RenderSettings.reflectionIntensity = 0; RenderSettings.ambientProbe = new SphericalHarmonicsL2();
                Material[] palette = AssetDatabase.LoadAssetAtPath<GameObject>(DesertSceneryBuilder.PrefabPath)
                    .transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials;
                cactus.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(DesertSceneryBuilder.ArtPath + "/Models/Saguaro.asset");
                var renderer = cactus.GetComponent<Renderer>(); renderer.sharedMaterials = palette;
                ground.transform.localScale = Vector3.one * 4; ground.GetComponent<Renderer>().sharedMaterial = palette[0];
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 9;
                camera.transform.position = new Vector3(-9, 10, -13); camera.transform.LookAt(new Vector3(0, 2, 1));
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.targetTexture = target;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                var sun = sunObject.GetComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.85f;
                sun.shadows = LightShadows.Soft; sun.shadowStrength = 1; sun.shadowBias = 0.25f; sun.shadowNormalBias = 0.25f;
                sun.GetUniversalAdditionalLightData().usePipelineSettings = false;
                sun.transform.rotation = Quaternion.Euler(45, -35, 0); RenderSettings.sun = sun;
                Color[] Pixels()
                {
                    for (int frame = 0; frame < 4; frame++) camera.Render();
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply(); return image.GetPixels();
                }
                renderer.shadowCastingMode = ShadowCastingMode.Off; Color[] bare = Pixels();
                renderer.shadowCastingMode = ShadowCastingMode.On; Color[] shaded = Pixels();
                int shadow = Enumerable.Range(0, bare.Length).Count(i => bare[i].r > bare[i].g * 1.1f && bare[i].g > bare[i].b * 1.1f && bare[i].grayscale - shaded[i].grayscale > 0.04f);
                Assert.That(shadow, Is.GreaterThan(60), "The real cactus shadow must darken the golden sand, not merely its own green surface.");
            }
            finally
            {
                cameraObject.GetComponent<Camera>().targetTexture = null; RenderTexture.active = active; ShaderUtil.allowAsyncCompilation = async;
                foreach (Object value in new Object[] { cameraObject, sunObject, cactus, ground, target, image }) Object.DestroyImmediate(value);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static Mesh Load(string name, int layout) => AssetDatabase.LoadAssetAtPath<Mesh>(DesertSceneryBuilder.ArtPath + "/Layouts/" + name + " " + layout + ".asset");
    }
}
