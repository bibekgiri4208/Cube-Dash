using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using G = CubeDash.Editor.CountrySideMeshes.Geometry;

namespace CubeDash.Editor
{
    /// <summary>Standalone Desert artwork. Shared biome art and gameplay are never rebuilt.</summary>
    public static class DesertSceneryBuilder
    {
        public const string ScenePath = "Assets/Scenes/Desert.unity";
        public const string ArtPath = "Assets/Maps/Desert Canyon";
        public const string PrefabPath = ArtPath + "/Desert Canyon.prefab";
        private const string SourcePath = "Assets/Prefab/CubeDash/Environments/Desert.prefab";

        [MenuItem("Tools/Cube Dash/Improve Standalone Desert Environment")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }
        public static void ApplyFromCommandLine() => Apply(false);
        public static void RebuildFromCommandLine() => Apply(true);

        private static void Apply(bool rebuild)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before updating Desert.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath);
            var track = Find<EndlessTrack>(scene);
            if (!track.Environment.HasFixedBiome || track.Environment.FixedBiome != EnvironmentBiome.Desert)
                throw new InvalidOperationException("Expected the standalone Desert, not Dynamic.");
            var roots = track.Segments.Select(segment => segment.Environment.Biomes[(int)EnvironmentBiome.Desert]).ToArray();
            if (roots.Length != 8 || roots.Any(root => root == null)) throw new InvalidOperationException("Expected eight saved Desert sections.");
            bool[] upgraded = roots.Select(root =>
            {
                var scenery = root.GetComponent<BiomeScenery>();
                return scenery.Layouts.Length == 8 && scenery.VarietyLayouts.Length == 8 &&
                    scenery.Layouts.Concat(scenery.VarietyLayouts).Concat(scenery.EntranceLayouts).Concat(scenery.ExitLayouts)
                        .All(mesh => mesh != null && AssetDatabase.GetAssetPath(mesh).StartsWith(ArtPath + "/", StringComparison.Ordinal));
            }).ToArray();
            if (!rebuild && upgraded.All(value => value)) { Debug.Log("Preserved detailed Desert and saved manual edits."); return; }
            if (!rebuild && (upgraded.Any(value => value) || AssetDatabase.IsValidFolder(ArtPath)))
                throw new InvalidOperationException("Partial/existing Desert Canyon assets require inspection before rebuilding.");
            string originalScene = File.ReadAllText(ScenePath);
            Folder(ArtPath); Folder(ArtPath + "/Models"); Folder(ArtPath + "/Layouts"); Folder(ArtPath + "/Materials"); Folder(ArtPath + "/Textures");
            Texture2D texture = SurfaceTexture();
            Material[] palette = Palette(texture);
            var models = DesertSceneryModels.Names.ToDictionary(name => name,
                name => Save(DesertSceneryModels.Build(name), ArtPath + "/Models/" + name + ".asset"));
            var layouts = new Mesh[8]; var variety = new Mesh[8];
            Mesh dunes = Dunes();
            try
            {
                for (int layout = 0; layout < 8; layout++)
                {
                    var land = new LayoutMesh(); var plants = new LayoutMesh();
                    land.Add(dunes, Matrix4x4.identity, new Vector2(0, 21), false);
                    var random = new System.Random(56293 + layout * 719);
                    var occupied = new List<Bounds>();
                    void PlaceCliff(string name, float x, float z, Vector3 scale, float yaw)
                    {
                        Mesh model = models[name];
                        Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(x, GroundHeight(x, z) - 1.0f, z), Quaternion.Euler(0, yaw, 0), scale);
                        // Cliff footprints can be broad in X, but never cross a pooled section seam.
                        Bounds footprint = Footprint(model.vertices.Select(matrix.MultiplyPoint3x4));
                        float depth = footprint.size.z;
                        if (depth > 40) { scale.z *= 39 / depth; scale.x *= 0.95f; }
                        matrix = Matrix4x4.TRS(new Vector3(x, GroundHeight(x, z) - 1, z), Quaternion.Euler(0, yaw, 0), scale);
                        land.Add(model, matrix, new Vector2(x, z), false);
                    }
                    foreach (int side in new[] { -1, 1 })
                    {
                        bool broad = (layout + (side > 0 ? 1 : 0)) % 3 != 0;
                        PlaceCliff(broad ? "Layered Mesa" : "Eroded Butte", side * (52 + layout % 3 * 4), 21,
                            new Vector3(broad ? 1.05f : 1.20f, 0.85f + (layout + (side > 0 ? 2 : 0)) % 4 * 0.12f, 0.95f), 0);
                        PlaceCliff("Sandstone Spire", side * (34 + layout % 4 * 2.5f), 11 + layout % 3 * 8,
                            new Vector3(0.85f, 0.65f + layout % 3 * 0.17f, 0.95f), layout * 11);
                        PlaceCliff(layout % 2 == 0 ? "Eroded Butte" : "Layered Mesa", side * (112 + layout % 3 * 11), 21,
                            new Vector3(2.0f, 1.35f + layout % 3 * 0.19f, 0.92f), 0);
                    }

                    bool Plant(string name, float x, float z, float scale, float yaw, bool reserve)
                    {
                        Mesh model = models[name];
                        Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(x, 0, z), Quaternion.Euler(0, yaw, 0), Vector3.one * scale);
                        Bounds footprint = Footprint(model.vertices.Select(matrix.MultiplyPoint3x4));
                        if (Mathf.Min(Mathf.Abs(footprint.min.x), Mathf.Abs(footprint.max.x)) < 6.7f || footprint.min.z < 0.6f || footprint.max.z > 41.4f) return false;
                        Bounds clearance = footprint; clearance.Expand(new Vector3(0.60f, 0, 0.60f));
                        if (occupied.Any(bounds => Overlap(clearance, bounds))) return false;
                        float low = float.PositiveInfinity, high = float.NegativeInfinity;
                        foreach (float px in new[] { footprint.min.x, footprint.center.x, footprint.max.x })
                            foreach (float pz in new[] { footprint.min.z, footprint.center.z, footprint.max.z })
                            { float h = GroundHeight(px, pz); low = Mathf.Min(low, h); high = Mathf.Max(high, h); }
                        if (high - low > (name.Contains("Boulder") ? 0.80f : 0.48f)) return false;
                        matrix.m13 = low - model.bounds.min.y * scale - 0.12f;
                        bool grass = name == "Desert Bunchgrass" || name == "Yucca";
                        plants.Add(model, matrix, new Vector2(x, z), grass);
                        if (reserve) occupied.Add(clearance);
                        return true;
                    }
                    foreach (int side in new[] { -1, 1 })
                    {
                        // Deliberate groups, with open sand between them, rather than a uniform scatter.
                        string[] heroes = { "Saguaro", "Flowering Prickly Pear", "Young Saguaro", "Red Boulder", "Split Boulder", "Flowering Prickly Pear", "Barrel Cactus", "Red Boulder" };
                        for (int p = 0; p < heroes.Length; p++)
                        {
                            bool placed = false;
                            for (int attempt = 0; attempt < 240 && !placed; attempt++)
                                placed = Plant(heroes[p], side * Range(random, 9.5f, 27.5f), Range(random, 3.5f, 38.5f), Range(random, 0.78f, 1.2f), Range(random, 0, 360), true);
                            if (!placed) throw new InvalidOperationException("Could not safely place " + heroes[p] + " in Desert layout " + layout);
                        }
                        for (int p = 0; p < 42; p++)
                        {
                            bool placed = false;
                            for (int attempt = 0; attempt < 240 && !placed; attempt++)
                            {
                                int cluster = p % 5;
                                float z = 4.5f + cluster * 8 + Range(random, -3.4f, 3.4f);
                                float x = side * (9.8f + (cluster + layout) % 4 * 4.0f + Range(random, -3.3f, 3.3f));
                                // A crowded group can spill onto nearby open sand, never into a prop.
                                if (attempt >= 80) { x = side * Range(random, 8, 28); z = Range(random, 2, 40); }
                                placed = Plant(p % 9 == 0 ? "Yucca" : "Desert Bunchgrass", x, z, Range(random, 0.65f, 1.15f), Range(random, 0, 360), true);
                            }
                            if (!placed) throw new InvalidOperationException("Could not place dry planting " + p + " on side " + side + " in layout " + layout);
                        }
                        for (int p = 0; p < 12; p++)
                            for (int attempt = 0; attempt < 80; attempt++)
                                if (Plant("Sandstone Chips", side * Range(random, 7.5f, 28.5f), Range(random, 2, 40), Range(random, 0.35f, 0.85f), Range(random, 0, 360), true)) break;
                    }
                    layouts[layout] = Save(land.Bake("Canyon Landscape " + (layout + 1)), ArtPath + "/Layouts/Canyon Landscape " + (layout + 1) + ".asset");
                    variety[layout] = Save(plants.Bake("Desert Planting " + (layout + 1)), ArtPath + "/Layouts/Desert Planting " + (layout + 1) + ".asset");
                }
            }
            finally { Object.DestroyImmediate(dunes); }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null && !AssetDatabase.CopyAsset(SourcePath, PrefabPath))
                throw new InvalidOperationException("Could not create Desert-local prefab.");
            GameObject template = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var data = new SerializedObject(template.GetComponent<BiomeScenery>());
                foreach (string field in new[] { "layouts", "entranceLayouts", "exitLayouts", "varietyLayouts" })
                {
                    Mesh[] values = field == "varietyLayouts" ? variety : layouts;
                    var array = data.FindProperty(field); array.arraySize = values.Length;
                    for (int j = 0; j < values.Length; j++) array.GetArrayElementAtIndex(j).objectReferenceValue = values[j];
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach (Renderer renderer in template.GetComponentsInChildren<Renderer>(true).Where(renderer => !renderer.name.StartsWith("Boundary ", StringComparison.Ordinal)))
                    renderer.sharedMaterials = renderer.name == "Landscape Details" || renderer.name == "Landscape Variety" ? palette : new[] { palette[0] };
                template.GetComponent<BiomeScenery>().SelectLayout(0, 8, true);
                PrefabUtility.SaveAsPrefabAsset(template, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(template); }
            var localPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            for (int i = 0; i < roots.Length; i++)
            {
                PrefabUtility.ReplacePrefabAssetOfPrefabInstance(roots[i], localPrefab, new PrefabReplacingSettings
                {
                    objectMatchMode = ObjectMatchMode.ByHierarchy,
                    prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides,
                    changeRootNameToAssetName = false
                }, InteractionMode.AutomatedAction);
                var scenery = roots[i].GetComponent<BiomeScenery>();
                foreach (string field in new[] { "layouts", "entranceLayouts", "exitLayouts", "varietyLayouts" })
                    PrefabUtility.RevertPropertyOverride(new SerializedObject(scenery).FindProperty(field), InteractionMode.AutomatedAction);
                foreach (Renderer renderer in roots[i].GetComponentsInChildren<Renderer>(true).Where(renderer => !renderer.name.StartsWith("Boundary ", StringComparison.Ordinal)))
                {
                    // Scene-added terrain has no source-prefab property override to revert.
                    if (PrefabUtility.GetCorrespondingObjectFromSource(renderer) != null)
                        PrefabUtility.RevertPropertyOverride(new SerializedObject(renderer).FindProperty("m_Materials"), InteractionMode.AutomatedAction);
                    else
                    {
                        renderer.sharedMaterials = renderer.name == "Landscape Details" || renderer.name == "Landscape Variety" ? palette : new[] { palette[0] };
                        if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                    if (renderer.name == "Landscape Details" || renderer.name == "Landscape Variety")
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (PrefabUtility.GetCorrespondingObjectFromSource(filter) != null)
                            PrefabUtility.RevertPropertyOverride(new SerializedObject(filter).FindProperty("m_Mesh"), InteractionMode.AutomatedAction);
                    }
                }
                scenery.SelectLayout(i - 1, 8, true);
                foreach (Component component in new Component[] { scenery, roots[i].transform.Find("Landscape Details").GetComponent<MeshFilter>(), roots[i].transform.Find("Landscape Variety").GetComponent<MeshFilter>() })
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            Lighting(scene, track.Environment);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Desert.");
            PreserveUnrelatedSerialization(originalScene);
            Debug.Log("Detailed Desert saved: layered canyons, faceted sandstone, ribbed saguaros, flowering prickly pears, bunchgrass, rippled sand and warm daylight. Shared art and gameplay untouched.");
        }

        private static void PreserveUnrelatedSerialization(string original)
        {
            // Unity's scene writer also emits newly added default fields on unrelated scripts.
            // Retain those saved gameplay/HUD documents rather than bundling incidental migrations.
            var documents = new Regex(@"(?ms)^--- !u!\d+ &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)");
            var saved = documents.Matches(original).Cast<Match>().ToDictionary(match => match.Groups[1].Value, match => match.Value);
            string updated = File.ReadAllText(ScenePath);
            updated = documents.Replace(updated, match =>
            {
                bool unrelated = Regex.IsMatch(match.Value, @"m_EditorClassIdentifier: CubeDash::CubeDash\.(ArcadeButton|EndlessTrack|ObstacleDebris)\r?\n");
                return unrelated && saved.TryGetValue(match.Groups[1].Value, out string value) ? value : match.Value;
            });
            File.WriteAllText(ScenePath, updated);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
        }

        // Periodic, identical edge rows ensure every one of the eight layouts joins without cracks.
        public static float GroundHeight(float x, float z)
        {
            float edge = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Mathf.Abs(x) - 10) / 24));
            float apron = Mathf.SmoothStep(0, 0.1f, Mathf.Clamp01((Mathf.Abs(x) - 6.5f) / 3.5f));
            return -9.3f + apron + edge * (1.5f + 1.0f * Mathf.Sin(Mathf.Abs(x) * 0.14f + Mathf.Cos(z * Mathf.PI / 21) * 0.6f)
                + 0.55f * Mathf.Cos(z * Mathf.PI / 21) * Mathf.Sin(Mathf.Abs(x) * 0.065f));
        }
        private static Mesh Dunes()
        {
            var g = new G(DesertSceneryModels.Slots);
            foreach (int sign in new[] { -1, 1 }) for (int col = 0; col < 58; col++) for (int row = 0; row < 14; row++)
            {
                Vector3 P(int c, int r) { float x = sign * (6.5f + c * 3), z = r * 3; return new Vector3(x, GroundHeight(x, z), z); }
                float warmth = Mathf.PerlinNoise(col * 0.28f, Mathf.Cos(row * Mathf.PI / 7) * 0.2f + 3);
                g.Quad(P(col, row), P(col + 1, row), P(col + 1, row + 1), P(col, row + 1), 0, Vector3.up,
                    Color.Lerp(new Color(0.91f, 0.84f, 0.69f), Color.white, warmth));
            }
            return g.Finish("Continuous sculpted sand dunes");
        }

        private static Material[] Palette(Texture2D texture)
        {
            string[] names = { "Golden sand", "Terracotta sandstone", "Sunlit sandstone ledges", "Recessed red strata", "Saguaro green", "Cactus ridge green", "Dry amber grass", "Prickly pear flowers", "Cactus areoles" };
            string[] hex = { "EFB95D", "BE623B", "E29A50", "A45A3C", "4B7428", "648736", "B39A4F", "D54C58", "B5AC64" };
            var result = new Material[names.Length]; Shader shader = Shader.Find("CubeDash/Desert Surface");
            if (shader == null) throw new InvalidOperationException("Missing Desert shader.");
            for (int i = 0; i < names.Length; i++)
            {
                var material = new Material(shader) { name = names[i], enableInstancing = true };
                material.SetColor("_BaseColor", Hex(hex[i])); material.SetTexture("_SurfaceMap", texture);
                material.SetFloat("_SurfaceType", i == 0 ? 0 : i <= 3 ? 1 : i == 6 ? 3 : 2);
                material.SetFloat("_Smoothness", i == 4 || i == 5 ? 0.15f : 0.06f);
                material.SetFloat("_DetailStrength", i <= 3 ? 0.75f : 0.22f);
                material.SetFloat("_WindStrength", i == 6 ? 0.065f : i == 5 ? 0.025f : 0);
                result[i] = Save(material, ArtPath + "/Materials/" + names[i] + ".mat");
            }
            return result;
        }
        private static Texture2D SurfaceTexture()
        {
            const int size = 512;
            string path = ArtPath + "/Textures/Desert Mineral Detail.png";
            var image = new Texture2D(size, size, TextureFormat.RGB24, false, true);
            var pixels = new Color[size * size];
            // Tileable packed detail: granular mineral, broad winding ripple and soft mottling.
            float Noise(float u, float v, float frequency)
            {
                float a = Mathf.PerlinNoise(u * frequency + 17, v * frequency + 23);
                float b = Mathf.PerlinNoise((u - 1) * frequency + 17, v * frequency + 23);
                float c = Mathf.PerlinNoise(u * frequency + 17, (v - 1) * frequency + 23);
                float d = Mathf.PerlinNoise((u - 1) * frequency + 17, (v - 1) * frequency + 23);
                return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
            }
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float grain = Noise(u, v, 79) * 0.6f + Noise(u, v, 173) * 0.4f;
                float ripple = 0.5f + 0.38f * Mathf.Sin(u * Mathf.PI * 8 + Mathf.Sin(v * Mathf.PI * 2) * 1.4f);
                pixels[y * size + x] = new Color(grain, ripple, Noise(u, v, 7));
            }
            image.SetPixels(pixels); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false; importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Lighting(Scene scene, EnvironmentDirector director)
        {
            var data = new SerializedObject(director); var a = data.FindProperty("atmospheres").GetArrayElementAtIndex((int)EnvironmentBiome.Desert);
            void ColorField(string field, string hex) => a.FindPropertyRelative(field).colorValue = Hex(hex);
            ColorField("Horizon", "E7C9A3"); ColorField("Zenith", "3286C5"); ColorField("Ground", "B88A5C");
            ColorField("Sun", "FFE1AC"); ColorField("AmbientSky", "849FB8"); ColorField("AmbientEquator", "918879"); ColorField("AmbientGround", "77604E");
            a.FindPropertyRelative("SunIntensity").floatValue = 2.15f; data.ApplyModifiedPropertiesWithoutUndo();
            var sun = (Light)data.FindProperty("sunlight").objectReferenceValue;
            sun.transform.rotation = Quaternion.Euler(32, -38, 0); sun.shadows = LightShadows.Soft; sun.shadowStrength = 1;
            sun.shadowBias = 0.25f; sun.shadowNormalBias = 0.25f;
            var extra = sun.GetUniversalAdditionalLightData(); extra.usePipelineSettings = false; extra.softShadowQuality = SoftShadowQuality.High;
            var fill = (Light)data.FindProperty("skyFill").objectReferenceValue; if (fill != null) fill.intensity = 0.045f;
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientIntensity = 1;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 85; RenderSettings.fogEndDistance = 330;
            var sky = new Material(RenderSettings.skybox) { name = "Desert afternoon sky" };
            sky.SetColor("_HorizonColor", a.FindPropertyRelative("Horizon").colorValue); sky.SetColor("_ZenithColor", a.FindPropertyRelative("Zenith").colorValue);
            sky.SetColor("_GroundColor", a.FindPropertyRelative("Ground").colorValue); sky.SetColor("_SunColor", Hex("FFE1AC"));
            sky.SetVector("_SunDirection", -sun.transform.forward); sky.SetFloat("_SunSize", 1.7f);
            sky.SetColor("_CloudColor", Hex("FFF0D6")); sky.SetFloat("_CloudCoverage", 0.36f); sky.SetFloat("_CloudOpacity", 0.82f);
            sky.SetFloat("_GradientPower", 0.22f); sky.SetFloat("_CloudScale", 1.7f);
            RenderSettings.skybox = Save(sky, ArtPath + "/Desert Afternoon Sky.mat"); director.ApplyDistance(0, 0);
            foreach (Volume volume in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Volume>(true)))
            {
                if (!volume.isGlobal || volume.sharedProfile == null) continue;
                string path = ArtPath + "/Desert Afternoon.asset";
                if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) == null && !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(volume.sharedProfile), path))
                    throw new InvalidOperationException("Could not clone the Desert volume.");
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path); volume.sharedProfile = profile;
                T Effect<T>() where T : VolumeComponent
                {
                    if (!profile.TryGet(out T effect)) { effect = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(effect, profile); }
                    return effect;
                }
                Effect<DepthOfField>().mode.Override(DepthOfFieldMode.Off);
                Effect<ChromaticAberration>().intensity.Override(0); Effect<FilmGrain>().intensity.Override(0);
                Effect<Tonemapping>().mode.Override(TonemappingMode.ACES);
                var grade = Effect<ColorAdjustments>(); grade.postExposure.Override(0.20f); grade.contrast.Override(10); grade.saturation.Override(4);
                var bloom = Effect<Bloom>(); bloom.intensity.Override(0.13f); bloom.threshold.Override(1.3f); bloom.scatter.Override(0.5f);
                Effect<Vignette>().intensity.Override(0.10f);
                EditorUtility.SetDirty(profile); foreach (VolumeComponent effect in profile.components) EditorUtility.SetDirty(effect);
            }
        }

        public static void CaptureFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath); var game = Find<CubeDashGame>(scene); Camera camera = game.GameCamera;
            game.Track.Environment.ResetRun();
            Vector3 gameplayPosition = camera.transform.position, gameplayLook = gameplayPosition + camera.transform.forward * 40;
            float gameplayFov = camera.fieldOfView;
            foreach (SceneryRise rise in game.Track.GetComponentsInChildren<SceneryRise>(true)) rise.Settle();
            RoadCurve.Clear();
            string prefix = Environment.GetEnvironmentVariable("CUBE_DASH_DESERT_PREVIEW") ?? Path.Combine(Path.GetDirectoryName(Application.dataPath), "desert");
            var target = new RenderTexture(1600, 900, 24); var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            RenderTexture old = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target;
                foreach (Canvas canvas in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true)))
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                }
                Canvas.ForceUpdateCanvases();
                Capture("gameplay", gameplayPosition, gameplayLook);
                foreach (Canvas canvas in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true))) canvas.gameObject.SetActive(false);
                Transform root = game.Track.Segments.Select(s => s.Environment.Biomes[(int)EnvironmentBiome.Desert].transform).OrderBy(t => Mathf.Abs(t.position.z)).First();
                camera.fieldOfView = 55;
                Capture("canyon", root.TransformPoint(new Vector3(-8, -3, 0)), root.TransformPoint(new Vector3(-45, 6, 33)));
                Capture("plants", root.TransformPoint(new Vector3(-8, -6, 4)), root.TransformPoint(new Vector3(-19, -7, 19)));
                camera.fieldOfView = gameplayFov;
                RoadCurve.Apply(560, RoadCurve.Phase(7321), 1, game.Player.position.z);
                Capture("curved", gameplayPosition, gameplayLook);
                void Capture(string name, Vector3 position, Vector3 look)
                {
                    camera.transform.position = position; camera.transform.LookAt(look);
                    for (int i = 0; i < 6; i++) camera.Render();
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
                    File.WriteAllBytes(prefix + "-" + name + ".png", image.EncodeToPNG());
                }
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = old; ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                RoadCurve.Clear();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private sealed class LayoutMesh
        {
            private readonly List<Vector3> points = new List<Vector3>(), normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>(), wind = new List<Vector2>(), anchors = new List<Vector2>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<int>[] triangles = Enumerable.Range(0, DesertSceneryModels.Slots).Select(_ => new List<int>()).ToArray();
            internal void Add(Mesh mesh, Matrix4x4 matrix, Vector2 anchor, bool flex)
            {
                Vector3[] vertices = mesh.vertices, directions = mesh.normals; Color32[] tints = mesh.colors32;
                Matrix4x4 normalMatrix = matrix.inverse.transpose;
                for (int surface = 0; surface < mesh.subMeshCount; surface++)
                {
                    var map = new Dictionary<int, int>();
                    foreach (int index in mesh.GetTriangles(surface))
                    {
                        if (!map.TryGetValue(index, out int destination))
                        {
                            destination = points.Count; map.Add(index, destination); Vector3 p = matrix.MultiplyPoint3x4(vertices[index]);
                            points.Add(p); normals.Add(normalMatrix.MultiplyVector(directions[index]).normalized);
                            uv.Add(new Vector2(p.x, p.z));
                            float height = flex ? Mathf.Clamp01((vertices[index].y - mesh.bounds.min.y) / Mathf.Max(0.001f, mesh.bounds.size.y)) : 0;
                            wind.Add(new Vector2(height * height, anchor.x * 0.31f + anchor.y * 0.73f));
                            anchors.Add(anchor); colors.Add(tints.Length == vertices.Length ? tints[index] : new Color32(255, 255, 255, 255));
                        }
                        triangles[surface].Add(destination);
                    }
                }
            }
            internal Mesh Bake(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(points); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(1, wind); mesh.SetUVs(2, anchors); mesh.SetColors(colors);
                mesh.subMeshCount = triangles.Length;
                for (int surface = 0; surface < triangles.Length; surface++) mesh.SetTriangles(triangles[surface], surface, false);
                mesh.RecalculateBounds(); Bounds bounds = mesh.bounds; bounds.Encapsulate(new Vector3(bounds.center.x, bounds.min.y - 80, bounds.center.z)); mesh.bounds = bounds;
                return mesh;
            }
        }
        private static bool Overlap(Bounds a, Bounds b) => a.min.x < b.max.x && a.max.x > b.min.x && a.min.z < b.max.z && a.max.z > b.min.z;
        private static Bounds Footprint(IEnumerable<Vector3> points)
        {
            bool first = true; var bounds = new Bounds();
            foreach (Vector3 p in points) { var flat = new Vector3(p.x, 0, p.z); if (first) { bounds = new Bounds(flat, Vector3.zero); first = false; } else bounds.Encapsulate(flat); }
            return bounds;
        }
        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);
        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color result); return result; }
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static void Folder(string path) { if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path)); }
        private static T Save<T>(T asset, string path) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
            EditorUtility.CopySerialized(asset, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(asset); return existing;
        }
    }
}
