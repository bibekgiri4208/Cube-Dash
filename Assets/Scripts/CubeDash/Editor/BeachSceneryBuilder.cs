using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Scene-local Beach art. Keeps the existing pool, terrain, ocean and all gameplay intact.</summary>
    public static class BeachSceneryBuilder
    {
        public const string ScenePath = "Assets/Scenes/Beach.unity";
        public const string ArtPath = "Assets/Maps/Beach Coast";
        public const string PrefabPath = ArtPath + "/Beach Coast.prefab";
        private const string SourcePath = "Assets/Prefab/CubeDash/Environments/Beach.prefab";
        private static readonly string[] Bushes = { "Bush_01_01", "Bush_02_01", "Bush_03_01", "Bush_01_02", "Bush_02_02", "Bush_03_02" };
        private static readonly string[] Grasses = { "Grass_01", "Grass_02", "Grass_03" };
        private static readonly string[] Rocks = { "Stone_Medium_01", "Stone_Medium_02", "Stone_Medium_03" };

        [MenuItem("Tools/Cube Dash/Improve Standalone Beach Scenery")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }
        public static void ApplyFromCommandLine() => Apply(false);
        // Explicit rebake is separate from safe, non-destructive reapplication.
        public static void RebuildFromCommandLine() => Apply(true);

        public static void CaptureFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            var game = Find<CubeDashGame>(scene); Camera camera = game.GameCamera;
            foreach (Canvas canvas in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true))) canvas.gameObject.SetActive(false);
            game.Player.gameObject.SetActive(false);
            foreach (CubeWake wake in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CubeWake>(true))) wake.gameObject.SetActive(false);
            foreach (SceneryRise rise in game.Track.GetComponentsInChildren<SceneryRise>(true)) rise.Settle();
            RoadCurve.Clear();
            var profiles = new List<VolumeProfile>();
            foreach (Volume volume in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Volume>(true)))
            {
                if (volume.sharedProfile == null) continue;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.components = volume.sharedProfile.components.Select(component => Object.Instantiate(component)).ToList();
                volume.profile = profile; profiles.Add(profile);
                if (profile.TryGet(out DepthOfField focus)) focus.active = false;
                if (profile.TryGet(out MotionBlur blur)) blur.active = false;
            }
            Transform coast = game.Track.Segments.Select(segment => segment.Environment.Biomes[(int)EnvironmentBiome.Beach].transform)
                .OrderBy(root => Mathf.Abs(root.position.z)).First();
            string prefix = Environment.GetEnvironmentVariable("CUBE_DASH_BEACH_PREVIEW") ?? Path.Combine(Path.GetDirectoryName(Application.dataPath), "beach-coast");
            var target = new RenderTexture(1200, 750, 24); var image = new Texture2D(1200, 750, TextureFormat.RGB24, false);
            RenderTexture old = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target; camera.fieldOfView = 55;
                Capture("overview", new Vector3(4, 3, -8), new Vector3(-18, -5, 24));
                Capture("seating", new Vector3(-5, -3.6f, 0), new Vector3(-17, -7.7f, 13));
                Capture("jetty", new Vector3(4, -2, 4), new Vector3(21, -10, 27));
                void Capture(string name, Vector3 position, Vector3 look)
                {
                    camera.transform.position = coast.TransformPoint(position); camera.transform.LookAt(coast.TransformPoint(look));
                    camera.Render(); camera.Render();
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1200, 750), 0, 0); image.Apply();
                    File.WriteAllBytes(prefix + "-" + name + ".png", image.EncodeToPNG());
                }
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = old; ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                foreach (VolumeProfile profile in profiles)
                {
                    foreach (VolumeComponent component in profile.components) Object.DestroyImmediate(component);
                    Object.DestroyImmediate(profile);
                }
            }
            // Capture changes are intentionally not saved to the gameplay scene.
        }

        private static void Apply(bool rebuild)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before updating Beach.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath);
            var track = Find<EndlessTrack>(scene);
            if (!track.Environment.HasFixedBiome || track.Environment.FixedBiome != EnvironmentBiome.Beach)
                throw new InvalidOperationException("Expected the standalone Beach map, not the mixed-biome Level.");
            var roots = track.Segments.Select(segment => segment.Environment.Biomes[(int)EnvironmentBiome.Beach]).ToArray();
            if (roots.Any(root => root == null)) throw new InvalidOperationException("A Beach section is missing.");
            bool[] upgraded = roots.Select(root => root.GetComponent<BiomeScenery>().Layouts.All(mesh =>
                AssetDatabase.GetAssetPath(mesh).StartsWith(ArtPath + "/", StringComparison.Ordinal))).ToArray();
            if (!rebuild && upgraded.All(value => value)) { Debug.Log("Preserved the existing detailed Beach art and manual edits."); return; }
            if (!rebuild && (upgraded.Any(value => value) || AssetDatabase.IsValidFolder(ArtPath)))
                throw new InvalidOperationException("Partial/existing Beach Coast art requires inspection before rebaking.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            var originals = source.GetComponent<BiomeScenery>().Layouts;
            if (originals.Length != 8) throw new InvalidOperationException("Expected eight saved Beach layouts.");
            var plants = Bushes.Concat(Grasses).Concat(Rocks).ToDictionary(name => name, LoadPlant);
            Folder(ArtPath); Folder(ArtPath + "/Materials"); Folder(ArtPath + "/Textures"); Folder(ArtPath + "/Models"); Folder(ArtPath + "/Layouts");
            var palette = MakePalette(source.transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials);
            var surfaces = new Dictionary<Material, int>();
            foreach (Plant plant in plants.Values)
                foreach (Material material in plant.Renderers.SelectMany(renderer => renderer.sharedMaterials))
                {
                    if (surfaces.ContainsKey(material)) continue;
                    surfaces.Add(material, palette.Count); palette.Add(PlantMaterial(material));
                }
            var models = BeachSceneryModels.Names.ToDictionary(name => name,
                name => Save(BeachSceneryModels.Build(name), ArtPath + "/Models/" + name + ".asset"));
            var details = new Mesh[8]; var variety = new Mesh[8];
            for (int layout = 0; layout < 8; layout++)
            {
                var natural = new LayoutMesh(palette.Count); var props = new LayoutMesh(palette.Count);
                // Retain the original sculpted dunes, but none of the old round shrub/prop geometry.
                natural.Add(originals[layout], 5, 5, Matrix4x4.identity, Vector2.zero, default, false);
                var ground = new Ground(originals[layout]); var occupied = new List<Bounds>();
                var random = new System.Random(96317 + layout * 397);
                void Model(string name, float x, float z, float yaw = 0, float scale = 1, bool reserve = true)
                {
                    Mesh model = models[name];
                    float y = name == "Timber Jetty" ? 0 : name == "Sailing Dinghy" ? CubeDashOceanWaves.OceanLocalHeight - 0.16f : ground.Height(x, z) - 0.025f;
                    var matrix = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0, yaw, 0), Vector3.one * scale);
                    var target = name == "Coastal Palm" ? natural : props;
                    for (int surface = 0; surface < model.subMeshCount; surface++)
                        target.Add(model, surface, surface, matrix, new Vector2(x, z), model.bounds, name == "Coastal Palm");
                    if (reserve)
                    {
                        Bounds footprint = Footprint(model.vertices.Select(matrix.MultiplyPoint3x4));
                        if (name == "Coastal Palm") footprint = new Bounds(new Vector3(x, 0, z), new Vector3(3, 1, 3));
                        footprint.Expand(new Vector3(1.8f, 0, 1.8f)); occupied.Add(footprint);
                    }
                }
                // Entire furnished bays are authored together. Plants cannot enter their clearances.
                Model("Shaded Loungers", -14.8f, 10 + layout % 3 * 1.4f, -90);
                if (layout >= 4) Model("Shaded Loungers", -17.8f, 30, -80, 0.92f);
                Model("Weatherboard Hut", -38, 13 + layout % 2 * 3, -90, 1.15f);
                Model(layout % 3 == 0 ? "Lighthouse" : "Weatherboard Hut", -31.5f, 30, -90, layout % 3 == 0 ? 1 : 0.9f);
                Model("Timber Jetty", 0, 20 + layout % 3 * 2.5f, 0, 1, false);
                Model("Sailing Dinghy", 38 + layout % 3 * 5, 9 + layout % 2 * 23, -24 + layout * 13, 0.90f, false);
                Model("Coastal Palm", -23.5f, 4, layout * 43, 0.96f);
                Model("Coastal Palm", -28, 17.5f, 40 + layout * 51, 1.08f);
                Model("Coastal Palm", -23, 38, 130 + layout * 27, 0.89f);
                Model("Coastal Palm", -49, 31, 210 + layout * 31, 1.12f);

                bool PlantAt(string name, float x, float z, float height, float width, bool reserve)
                {
                    Plant plant = plants[name];
                    float scale = Mathf.Min(height / plant.Bounds.size.y, width / Mathf.Max(plant.Bounds.size.x, plant.Bounds.size.z));
                    var matrix = Matrix4x4.TRS(new Vector3(x, 0, z), Quaternion.Euler(0, Range(random, 0, 360), 0), Vector3.one * scale);
                    var points = plant.Points.Select(matrix.MultiplyPoint3x4).ToArray();
                    Bounds footprint = Footprint(points); footprint.Expand(new Vector3(0.65f, 0, 0.65f));
                    if (footprint.max.x > -9 || footprint.min.z < 0.5f || footprint.max.z > 41.5f || occupied.Any(bounds => Overlap(footprint, bounds))) return false;
                    float y = ground.Height(x, z) - plant.Bounds.min.y * scale - 0.07f;
                    matrix.m13 = y;
                    foreach (Renderer renderer in plant.Renderers)
                    {
                        Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                        Matrix4x4 local = plant.Prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                        for (int surface = 0; surface < mesh.subMeshCount; surface++)
                            natural.Add(mesh, surface, surfaces[renderer.sharedMaterials[surface]], matrix * local, new Vector2(x, z), plant.Bounds,
                                !name.StartsWith("Stone", StringComparison.Ordinal), local);
                    }
                    if (reserve) occupied.Add(footprint);
                    return true;
                }
                for (int bush = 0; bush < 10; bush++)
                {
                    bool placed = false;
                    for (int attempt = 0; attempt < 160 && !placed; attempt++)
                        placed = PlantAt(Bushes[(bush + layout) % Bushes.Length], Range(random, -34, -20.5f), Range(random, 2, 40), Range(random, 1.25f, 2.1f), 3.3f, true);
                    if (!placed) throw new InvalidOperationException("Could not place coastal shrub safely in layout " + (layout + 1));
                }
                for (int grass = 0; grass < 30; grass++)
                    for (int attempt = 0; attempt < 80; attempt++)
                        if (PlantAt(Grasses[(grass + layout) % Grasses.Length], Range(random, -32, -10.7f), Range(random, 1.5f, 40.5f), Range(random, 0.5f, 1.0f), 1.65f, false)) break;
                // Angular, textured dune stones are embedded, not floating green-capped spheres.
                for (int rock = 0; rock < 4; rock++)
                    for (int attempt = 0; attempt < 100; attempt++)
                        if (PlantAt(Rocks[(rock + layout) % Rocks.Length], Range(random, -29, -21), Range(random, 3, 39), Range(random, 1.1f, 1.8f), 2.7f, true)) break;
                details[layout] = Save(natural.Bake("Coastal Planting " + (layout + 1)), ArtPath + "/Layouts/Coastal Planting " + (layout + 1) + ".asset");
                variety[layout] = Save(props.Bake("Coastal Architecture " + (layout + 1)), ArtPath + "/Layouts/Coastal Architecture " + (layout + 1) + ".asset");
            }
            // A map-local copy keeps one shared art definition, instead of hundreds of
            // duplicated material/array overrides on every pooled section. Original file IDs
            // and hierarchy are retained so rise/horizon/transition references stay connected.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null && !AssetDatabase.CopyAsset(SourcePath, PrefabPath))
                throw new InvalidOperationException("Could not create the map-local Beach scenery prefab.");
            GameObject template = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var data = new SerializedObject(template.GetComponent<BiomeScenery>());
                foreach (string field in new[] { "layouts", "entranceLayouts", "exitLayouts", "varietyLayouts" })
                {
                    Mesh[] values = field == "varietyLayouts" ? variety : details;
                    var array = data.FindProperty(field); array.arraySize = values.Length;
                    for (int j = 0; j < values.Length; j++) array.GetArrayElementAtIndex(j).objectReferenceValue = values[j];
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach (string name in new[] { "Landscape Details", "Landscape Variety" })
                    template.transform.Find(name).GetComponent<Renderer>().sharedMaterials = palette.ToArray();
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
                foreach (string name in new[] { "Landscape Details", "Landscape Variety" })
                {
                    var child = roots[i].transform.Find(name);
                    PrefabUtility.RevertPropertyOverride(new SerializedObject(child.GetComponent<Renderer>()).FindProperty("m_Materials"), InteractionMode.AutomatedAction);
                    PrefabUtility.RevertPropertyOverride(new SerializedObject(child.GetComponent<MeshFilter>()).FindProperty("m_Mesh"), InteractionMode.AutomatedAction);
                }
                scenery.SelectLayout(i - 1, track.Environment.SegmentsPerBiome, true);
                foreach (Component component in new Component[] { scenery, roots[i].transform.Find("Landscape Details").GetComponent<MeshFilter>(),
                    roots[i].transform.Find("Landscape Variety").GetComponent<MeshFilter>(), roots[i].transform.Find("Landscape Details").GetComponent<Renderer>(),
                    roots[i].transform.Find("Landscape Variety").GetComponent<Renderer>() })
                    if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Beach scenery.");
            Debug.Log("Detailed Beach saved: eight pooled layouts, clear furnished bays, leafy planting, pinnate palms, weatherboard huts, closed boats, supported jetties and original textures. Other maps, ocean and gameplay untouched.");
        }

        private static List<Material> MakePalette(Material[] original)
        {
            Texture2D wood = Texture("Salt-weathered timber", 0), fabric = Texture("Woven canvas", 1), paint = Texture("Coastal painted finish", 2);
            var colors = new[] { new Color(0.36f, 0.24f, 0.14f), new Color(0.16f, 0.35f, 0.16f), new Color(0.32f, 0.46f, 0.20f),
                new Color(0.52f, 0.51f, 0.44f), new Color(0.92f, 0.89f, 0.76f), Color.white, new Color(0.72f, 0.27f, 0.16f),
                new Color(0.20f, 0.47f, 0.46f), new Color(0.60f, 0.48f, 0.29f), new Color(0.55f, 0.40f, 0.25f),
                new Color(0.29f, 0.46f, 0.48f), new Color(0.10f, 0.25f, 0.29f), new Color(0.86f, 0.84f, 0.72f),
                new Color(0.22f, 0.29f, 0.29f), new Color(0.51f, 0.50f, 0.29f) };
            var names = new[] { "Palm bark", "Palm leaves", "Leaf highlights", "Foundation stone", "Ivory canvas", "Dune sand", "Coral canvas",
                "Sea glass paint", "Hemp rope", "Weathered teak", "Standing seam roof", "Dark window glass", "Chalk trim", "Marine metal", "Dune grass" };
            var result = new List<Material>();
            for (int i = 0; i < 15; i++)
            {
                if (i == 5) { result.Add(original[5]); continue; }
                Texture2D map = i == 0 || i == 9 ? wood : i == 4 || i == 6 || i == 8 ? fabric : paint;
                Material material = NewMaterial(names[i], map, colors[i], colors[i] * 1.04f, false);
                material.SetFloat("_Smoothness", i == 11 ? 0.55f : i == 13 ? 0.32f : 0.07f);
                result.Add(Save(material, ArtPath + "/Materials/" + names[i] + ".mat"));
            }
            return result;
        }

        private static Material PlantMaterial(Material original)
        {
            bool stone = original.name.StartsWith("Stone", StringComparison.Ordinal) || original.name.StartsWith("Rock", StringComparison.Ordinal);
            bool grass = original.name.StartsWith("Grass", StringComparison.Ordinal);
            Texture texture = new[] { "_Texture", "_Base_Map", "_BaseMap" }.Where(original.HasProperty).Select(original.GetTexture).FirstOrDefault(value => value != null);
            if (texture == null) throw new InvalidOperationException("Missing plant texture: " + original.name);
            var material = NewMaterial("Coastal " + original.name, texture, stone ? new Color(0.45f, 0.44f, 0.36f) : grass ? new Color(0.30f, 0.36f, 0.18f) : new Color(0.17f, 0.32f, 0.19f),
                stone ? new Color(0.52f, 0.50f, 0.40f) : grass ? new Color(0.60f, 0.57f, 0.32f) : new Color(0.38f, 0.49f, 0.24f), !stone);
            material.SetFloat("_BushLighting", !stone && !grass ? 1 : 0);
            return Save(material, ArtPath + "/Materials/" + material.name + ".mat");
        }
        private static Material NewMaterial(string name, Texture texture, Color color, Color top, bool foliage)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/JungleVegetation.shader");
            if (shader == null) throw new InvalidOperationException("Missing curve/reveal-compatible vegetation shader.");
            var material = new Material(shader) { name = name, enableInstancing = true, doubleSidedGI = foliage };
            color.a = top.a = 1;
            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", color); material.SetColor("_TopColor", top);
            material.SetFloat("_Cutoff", foliage ? 0.4f : 0); material.SetFloat("_Foliage", foliage ? 1 : 0);
            material.SetFloat("_WindStrength", foliage ? 0.09f : 0); material.SetFloat("_LeafLightGain", foliage ? 0.85f : 1);
            material.SetFloat("_Smoothness", 0.06f); return material;
        }
        private static Texture2D Texture(string name, int kind)
        {
            string path = ArtPath + "/Textures/" + name + ".png";
            var texture = new Texture2D(256, 256, TextureFormat.RGB24, false);
            var pixels = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float u = x / 256f, v = y / 256f;
                float noise = Mathf.PerlinNoise(u * 19.0f + 3, v * 17.0f + 9);
                float value = kind == 0 ? 0.68f + 0.14f * Mathf.Sin(u * Mathf.PI * 48 + Mathf.Sin(v * Mathf.PI * 2) * 0.7f) + noise * 0.18f
                    : kind == 1 ? 0.85f + (x % 4 == 0 ? -0.055f : 0.025f) + (y % 4 == 0 ? -0.045f : 0.02f) + noise * 0.065f
                    : 0.88f + noise * 0.10f;
                pixels[y * 256 + x] = new Color(value, value, value);
            }
            texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true; importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private sealed class Plant
        {
            internal GameObject Prefab; internal Renderer[] Renderers; internal Bounds Bounds; internal Vector3[] Points;
        }
        private static Plant LoadPlant(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuAssetStoreTrees.PrefabPath + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing downloaded coastal plant: " + name);
            var lod = prefab.GetComponent<LODGroup>();
            Renderer[] renderers = lod != null ? lod.GetLODs()[Mathf.Min(1, lod.GetLODs().Length - 1)].renderers : prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0 || renderers.Any(renderer => renderer == null || renderer.GetComponent<MeshFilter>()?.sharedMesh == null))
                throw new InvalidOperationException("Plant has no usable geometry: " + name);
            Vector3[] points = renderers.SelectMany(renderer => renderer.GetComponent<MeshFilter>().sharedMesh.vertices.Select(
                (prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix).MultiplyPoint3x4)).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero); foreach (Vector3 point in points) bounds.Encapsulate(point);
            return new Plant { Prefab = prefab, Renderers = renderers, Bounds = bounds, Points = points };
        }
        private static bool Overlap(Bounds a, Bounds b) => a.min.x < b.max.x && a.max.x > b.min.x && a.min.z < b.max.z && a.max.z > b.min.z;
        private static Bounds Footprint(IEnumerable<Vector3> points)
        {
            var bounds = new Bounds(); bool first = true;
            foreach (Vector3 p in points) { var flat = new Vector3(p.x, 0, p.z); if (first) { bounds = new Bounds(flat, Vector3.zero); first = false; } else bounds.Encapsulate(flat); }
            return bounds;
        }
        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static void Folder(string path) { if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path)); }
        private static T Save<T>(T asset, string path) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
            EditorUtility.CopySerialized(asset, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(asset); return existing;
        }

        private sealed class Ground
        {
            private readonly Vector3[] vertices; private readonly int[] triangles;
            internal Ground(Mesh source) { vertices = source.vertices; triangles = source.GetTriangles(5); }
            internal float Height(float x, float z)
            {
                float height = EnvironmentModelMeshes.BeachFloorHeight(x);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(d) < 0.000001f) continue;
                    float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                    float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                    if (u >= 0 && v >= 0 && u + v <= 1) height = Mathf.Max(height, u * a.y + v * b.y + (1 - u - v) * c.y);
                }
                return height;
            }
        }
        private sealed class LayoutMesh
        {
            private readonly List<Vector3> points = new List<Vector3>(), normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>(), wind = new List<Vector2>(), anchors = new List<Vector2>();
            private readonly List<Color32> colors = new List<Color32>(); private readonly List<int>[] triangles;
            internal LayoutMesh(int count) { triangles = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray(); }
            internal void Add(Mesh mesh, int surface, int target, Matrix4x4 matrix, Vector2 anchor, Bounds bounds, bool foliage, Matrix4x4? local = null)
            {
                Vector3[] vertices = mesh.vertices, directions = mesh.normals; Vector2[] textures = mesh.uv;
                var map = new Dictionary<int, int>(); Matrix4x4 normalMatrix = matrix.inverse.transpose;
                foreach (int index in mesh.GetTriangles(surface))
                {
                    if (!map.TryGetValue(index, out int destination))
                    {
                        destination = points.Count; map.Add(index, destination);
                        points.Add(matrix.MultiplyPoint3x4(vertices[index])); normals.Add(normalMatrix.MultiplyVector(directions[index]).normalized);
                        uv.Add(textures.Length == vertices.Length ? textures[index] : Vector2.zero);
                        float height = foliage ? Mathf.Clamp01(((local ?? Matrix4x4.identity).MultiplyPoint3x4(vertices[index]).y - bounds.min.y) / Mathf.Max(0.001f, bounds.size.y)) : 0;
                        wind.Add(new Vector2(height * height, anchor.x * 0.31f + anchor.y * 0.73f)); anchors.Add(anchor);
                        colors.Add(foliage ? (Color32)new Color(height * 0.7f, 0.5f, 1, 1) : new Color32(255, 255, 255, 255));
                    }
                    triangles[target].Add(destination);
                }
            }
            internal Mesh Bake(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(points); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetUVs(1, wind); mesh.SetUVs(2, anchors); mesh.SetColors(colors);
                mesh.subMeshCount = triangles.Length;
                for (int surface = 0; surface < triangles.Length; surface++) mesh.SetTriangles(triangles[surface], surface, false);
                mesh.RecalculateBounds(); Bounds bounds = mesh.bounds;
                bounds.Encapsulate(new Vector3(bounds.center.x, bounds.min.y - 80, bounds.center.z)); mesh.bounds = bounds; return mesh;
            }
        }
    }
}
