using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    public static partial class CubeDashEnvironments
    {
        private const string VarietyModels = Prefabs + "/Variety Models";
        private static readonly string[][] VarietyNames = {
            new[] { "Park Fountain", "Glass Pavilion", "Market Stalls", "Water Tower", "Garden Pergola" },
            new[] { "Bamboo Grove", "Banana Plant", "Fan Palm", "Mossy Fallen Log", "Ruined Gateway" },
            new[] { "Snow Fir", "Alpine Cabin", "Trail Cairn", "Bare Pine", "Rock Spires" },
            new[] { "Barrel Cactus", "Prickly Pear", "Agave", "Sandstone Arch", "Desert Ruins", "Oasis Palms" },
            new[] { "Striped Umbrella", "Fishing Boat", "Lifeguard Hut", "Lighthouse", "Driftwood", "Beach Chairs" }
        };

        [MenuItem("Tools/Cube Dash/Add Environment Model Variety")]
        public static void ImproveVarietyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ImproveVarietyFromCommandLine();
        }

        public static void ImproveVarietyFromCommandLine()
        {
            // Update only decoration assets. Keep the authored road, terrain/water, transitions,
            // palettes, atmosphere, audio, level, and all gameplay tuning intact.
            Folder(VarietyModels); InitializeModels();
            try
            {
                for (int i = 1; i < BiomeRules.Count; i++)
                {
                    var biome = (EnvironmentBiome)i;
                    string path = Prefabs + "/" + biome + ".prefab";
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        Transform details = root.transform.Find("Landscape Details");
                        Material[] palette = details.GetComponent<Renderer>().sharedMaterials;
                        UseNaturalPalette(palette, biome);
                        Mesh[] layouts = BakeLayouts(biome, palette);
                        details.GetComponent<MeshFilter>().sharedMesh = layouts[0];
                        SetArray(root.GetComponent<BiomeScenery>(), "layouts", layouts);
                        AddVarietyLayouts(root.transform, biome, palette);
                        if (biome == EnvironmentBiome.Beach)
                        {
                            CubeDashOceanWaves.SetOceanHeight(root.transform);
                            BakeBeachBayLayouts(root.GetComponent<BiomeScenery>(), layouts);
                        }
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                const string trackPath = "Assets/Prefab/CubeDash/TrackSegment.prefab";
                GameObject track = PrefabUtility.LoadPrefabContents(trackPath);
                try
                {
                    Transform city = track.GetComponent<SegmentEnvironment>().Biomes[0].transform;
                    Transform details = city.Find("Landscape Details");
                    cityPalette = details.GetComponent<Renderer>().sharedMaterials;
                    var models = new Dictionary<string, Mesh>();
                    foreach (string name in new[] { "Detached House", "Town House", "Market Shop", "Corner Cafe", "Bakery Shop",
                        "Balcony Apartments", "Terraced Office", "Clock Hall", "Street Furniture", "Bus Shelter" })
                        models[name] = AssetDatabase.LoadAssetAtPath<Mesh>(Meshes + "/City " + name + ".asset")
                            ?? throw new InvalidOperationException("Missing authored city model: " + name);
                    Mesh[] layouts = new Mesh[BiomeRules.SceneryLayoutCount];
                    for (int i = 0; i < layouts.Length; i++)
                        layouts[i] = SaveMesh(BuildCityLayout(i, models), "City District Layout " + (i + 1));
                    details.GetComponent<MeshFilter>().sharedMesh = layouts[0];
                    SetArray(city.GetComponent<BiomeScenery>(), "layouts", layouts);
                    AddVarietyLayouts(city, EnvironmentBiome.City, cityPalette);
                    PrefabUtility.SaveAsPrefabAsset(track, trackPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(track); }
                AssetDatabase.SaveAssets();
                CubeDashSceneryRise.ApplyFromCommandLine();
                Debug.Log("Environment variety saved: eight non-repeating compositions per biome, 27 reusable native plant/rock/landmark models, and rigid-object reveal anchors. Road, terrain, water, biome joins, audio and gameplay preserved.");
            }
            finally { ReleaseModels(); cityPalette = null; }
        }

        private static void UseNaturalPalette(Material[] p, EnvironmentBiome biome)
        {
            bark = p[0]; leaf = p[1]; lightLeaf = p[2]; stone = p[3]; snow = p[4]; sand = p[5];
            redStone = p[6]; grass = p[7]; foam = p[8]; wood = p[9]; thatch = p[10]; glass = p[11];
            trim = p[12]; moss = p[13]; dryGrass = p[14];
            if (biome == EnvironmentBiome.Jungle) { jungleBark = bark; jungleLeaf = leaf; jungleHighlight = lightLeaf; }
        }

        [MenuItem("Tools/Cube Dash/Repair Desert Models")]
        public static void RepairDesertFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before repairing desert models.");
            InitializeModels();
            try
            {
                // Update existing assets in place: no scene save, terrain rebuild or other biome edits.
                string path = Prefabs + "/Desert.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Material[] palette = root.transform.Find("Landscape Details").GetComponent<Renderer>().sharedMaterials;
                    UseNaturalPalette(palette, EnvironmentBiome.Desert);
                    AddVarietyLayouts(root.transform, EnvironmentBiome.Desert, palette, repairOnly: true);
                    BiomeScenery scenery = root.GetComponent<BiomeScenery>();
                    foreach (Mesh mesh in scenery.Layouts) CubeDashSceneryRise.BakeAnchors(mesh);
                    foreach (Mesh mesh in scenery.VarietyLayouts) CubeDashSceneryRise.BakeAnchors(mesh);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                // Cacti also appear in the two adjacent biome joins.
                foreach (string name in new[] { "Mountains Exit Blend", "Beach Entrance Blend" })
                {
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Meshes + "/" + name + ".asset");
                    if (mesh != null) CubeDashSceneryRise.BakeAnchors(mesh);
                }
                AssetDatabase.SaveAssets();
                Debug.Log("Desert models repaired: outward-facing sandstone arches, connected prickly-pear pads and synchronized cactus arms. Scene, terrain, other biomes and gameplay preserved.");
            }
            finally { ReleaseModels(); }
        }

        [MenuItem("Tools/Cube Dash/Repair Beach Models and City Join")]
        public static void RepairBeachFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before repairing beach models.");
            InitializeModels();
            try
            {
                const string path = Prefabs + "/Beach.prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform details = root.transform.Find("Landscape Details");
                    Material[] palette = details.GetComponent<Renderer>().sharedMaterials;
                    UseNaturalPalette(palette, EnvironmentBiome.Beach);
                    Mesh[] layouts = BakeLayouts(EnvironmentBiome.Beach, palette);
                    details.GetComponent<MeshFilter>().sharedMesh = layouts[0];
                    SetArray(root.GetComponent<BiomeScenery>(), "layouts", layouts);
                    AddVarietyLayouts(root.transform, EnvironmentBiome.Beach, palette, repairOnly: true);
                    CubeDashOceanWaves.SetOceanHeight(root.transform);
                    BakeBeachBayLayouts(root.GetComponent<BiomeScenery>(), layouts);
                    foreach (Mesh mesh in layouts) CubeDashSceneryRise.BakeAnchors(mesh);
                    foreach (Mesh mesh in root.GetComponent<BiomeScenery>().VarietyLayouts) CubeDashSceneryRise.BakeAnchors(mesh);
                    CubeDashSceneryRise.BakeAnchors(BakeCoastalHeadland());
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                // Keep the saved boundary palette, ground/water blending and track references.
                GameObject track = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/TrackSegment.prefab");
                Material[] boundaryPalette = track.GetComponent<SegmentEnvironment>().Biomes[0].transform
                    .Find("Boundary Entrance Scenery").GetComponent<Renderer>().sharedMaterials;
                UseNaturalPalette(boundaryPalette, EnvironmentBiome.City);
                jungleBark = boundaryPalette[15]; jungleLeaf = boundaryPalette[16]; jungleHighlight = boundaryPalette[17];
                CubeDashSceneryRise.BakeAnchors(SaveMesh(BoundaryDressing(EnvironmentBiome.City, EnvironmentBiome.Beach, true,
                    boundaryPalette), "City Entrance Blend"));
                CubeDashSceneryRise.BakeAnchors(SaveMesh(BoundaryDressing(EnvironmentBiome.Beach, EnvironmentBiome.City, false,
                    boundaryPalette), "Beach Exit Blend"));
                AssetDatabase.SaveAssets();
                Debug.Log("Beach models and City join repaired: closed boat ends, supported beach furniture/stairs, bay-safe docks and rigid house/palm/dock/landmark reveals. Ocean settings, road, other biomes, scene and audio preserved.");
            }
            finally { ReleaseModels(); }
        }

        private static void BakeBeachBayLayouts(BiomeScenery scenery, Mesh[] layouts)
        {
            Mesh[] entrance = new Mesh[layouts.Length], exit = new Mesh[layouts.Length];
            for (int i = 0; i < layouts.Length; i++)
                for (int edge = 0; edge < 2; edge++)
                {
                    Mesh mesh = Object.Instantiate(layouts[i]);
                    Vector3[] vertices = mesh.vertices;
                    var anchors = new List<Vector2>(); mesh.GetUVs(2, anchors);
                    var dock = new HashSet<int>();
                    foreach (int surface in new[] { 9, 12 })
                        foreach (int index in mesh.GetTriangles(surface)) if (vertices[index].x > 10.5f) dock.Add(index);
                    float z = edge == 0 ? 36 : 6.5f;
                    foreach (int index in dock)
                    {
                        vertices[index].z += z - anchors[index].y;
                        anchors[index] = new Vector2(20, z);
                    }
                    mesh.SetUVs(2, anchors);
                    GroundBeachLayoutRocks(mesh, vertices, edge == 0, edge == 1);
                    mesh.vertices = vertices;
                    Mesh saved = SaveMesh(mesh, "Beach Layout " + (i + 1) + (edge == 0 ? " Entrance" : " Exit"));
                    CubeDashSceneryRise.BakeAnchors(saved);
                    if (edge == 0) entrance[i] = saved; else exit[i] = saved;
                }
            SetArray(scenery, "entranceLayouts", entrance); SetArray(scenery, "exitLayouts", exit);
        }

        private static void AddVarietyLayouts(Transform root, EnvironmentBiome biome, Material[] palette, bool repairOnly = false)
        {
            Folder(VarietyModels);
            string directory = VarietyModels + "/" + biome; Folder(directory);
            string[] names = VarietyNames[(int)biome];
            var models = new Dictionary<string, Mesh>();
            foreach (string name in names)
            {
                bool repaired = biome == EnvironmentBiome.Desert ? name == "Sandstone Arch" || name == "Prickly Pear"
                    : name == "Fishing Boat" || name == "Beach Chairs" || name == "Lifeguard Hut" || name == "Striped Umbrella";
                Mesh mesh = repairOnly && !repaired
                    ? AssetDatabase.LoadAssetAtPath<Mesh>(Meshes + "/" + biome + " " + name + ".asset")
                        ?? throw new InvalidOperationException("Missing authored " + biome + " model: " + name)
                    : SaveMesh(BuildVarietyModel(biome, name, palette), biome + " " + name);
                models[name] = mesh;
                // Existing reusable prefabs inherit the repaired shared mesh. Do not rewrite
                // their hierarchy/file IDs or overwrite custom work on unaffected models.
                if (repairOnly) continue;
                GameObject model = new GameObject(name);
                try
                {
                    Part(model.transform, "Model", mesh, palette, Vector3.zero, Vector3.one);
                    PrefabUtility.SaveAsPrefabAsset(model, directory + "/" + name + ".prefab");
                }
                finally { Object.DestroyImmediate(model); }
            }
            Mesh[] layouts = new Mesh[BiomeRules.SceneryLayoutCount];
            for (int layout = 0; layout < layouts.Length; layout++)
            {
                Geometry geometry = new Geometry(palette);
                PopulateVariety(geometry, biome, layout, palette, models, root.GetComponent<BiomeScenery>().Layouts[layout]);
                layouts[layout] = SaveMesh(geometry.Bake(), biome + " Variety Layout " + (layout + 1));
            }
            Transform details = root.Find("Landscape Variety");
            if (details == null) details = Part(root, "Landscape Variety", layouts[0], palette, Vector3.zero, Vector3.one).transform;
            details.GetComponent<MeshFilter>().sharedMesh = layouts[0];
            details.GetComponent<Renderer>().sharedMaterials = palette;
            BiomeScenery scenery = root.GetComponent<BiomeScenery>();
            Set(scenery, "variety", details.GetComponent<MeshFilter>()); SetArray(scenery, "varietyLayouts", layouts);
        }

        private static Mesh BuildVarietyModel(EnvironmentBiome biome, string name, Material[] palette)
        {
            Geometry g = new Geometry(palette);
            void Box(int m, Vector3 p, Vector3 s, float yaw = 0) => g.Add(cube, palette[m], p, s, yaw);
            void Round(int m, Vector3 p, Vector3 s) => g.Add(cylinder, palette[m], p, s);
            void Rock(int m, Vector3 p, Vector3 s) => g.Add(rock, palette[m], p, s, 17);
            void Beam(int m, Vector3 a, Vector3 b, float thickness)
                => g.Add(branch, palette[m], a, new Vector3(thickness, Vector3.Distance(a, b), thickness),
                    Quaternion.FromToRotation(Vector3.up, (b - a).normalized));
            Mesh blade = null;
            Mesh Blade() => blade != null ? blade : blade = Own(EnvironmentModelMeshes.BroadLeaf());

            switch (name)
            {
                case "Bamboo Grove":
                    for (int stem = 0; stem < 7; stem++)
                    {
                        Vector3 p = new Vector3((stem % 3 - 1) * 0.85f, 0, (stem / 3 - 1) * 0.85f);
                        float h = 8.5f + stem % 3 * 1.5f;
                        Round(1, p + Vector3.up * h * 0.5f, new Vector3(0.26f, h * 0.5f, 0.26f));
                        for (int node = 1; node < 7; node++) Round(2, p + Vector3.up * node * h / 7, new Vector3(0.34f, 0.045f, 0.34f));
                        for (int spray = 0; spray < 3; spray++)
                            g.Add(frond, palette[1], p + Vector3.up * (h - 0.9f - spray), new Vector3(3.2f, 2.8f, 2.8f),
                                Quaternion.Euler(0, stem * 51 + spray * 120, 15));
                    }
                    break;
                case "Banana Plant":
                    g.Add(trunk, palette[0], Vector3.zero, new Vector3(0.65f, 3.3f, 0.65f));
                    for (int i = 0; i < 7; i++)
                        g.Add(Blade(), palette[1], new Vector3(0, 3.3f, 0), new Vector3(4.6f, 3.5f, 3.7f), Quaternion.Euler(0, i * 51, i % 2 == 0 ? 12 : -12));
                    for (int i = 0; i < 5; i++) Rock(5, new Vector3(0.3f, 2.5f - i * 0.14f, 0), new Vector3(0.5f, 0.16f, 0.2f));
                    break;
                case "Fan Palm":
                    g.Add(trunk, palette[0], Vector3.zero, new Vector3(0.8f, 7.3f, 0.8f));
                    Mesh fan = Own(EnvironmentModelMeshes.FanLeaf());
                    for (int i = 0; i < 8; i++) g.Add(fan, palette[1], new Vector3(0, 7.1f, 0), Vector3.one * 4.1f, Quaternion.Euler(0, i * 45, i % 2 == 0 ? 12 : -8));
                    break;
                case "Mossy Fallen Log":
                case "Driftwood":
                    Beam(name == "Driftwood" ? 9 : 0, new Vector3(-2.6f, 0.45f, 0), new Vector3(2.8f, 0.7f, 0.8f), 1.1f);
                    for (int i = 0; i < 3; i++)
                    {
                        Beam(name == "Driftwood" ? 9 : 0, new Vector3(i - 1, 0.6f, 0.4f), new Vector3(i - 1.5f, 1.7f, 1.5f), 0.3f);
                        if (name != "Driftwood") Rock(13, new Vector3(i - 1, 0.95f, 0.3f), new Vector3(1.5f, 0.3f, 0.9f));
                    }
                    break;
                case "Ruined Gateway":
                    for (int side = -1; side <= 1; side += 2)
                        for (int block = 0; block < 4; block++) Box(3, new Vector3(side * 2.4f, 0.7f + block * 1.25f, 0), new Vector3(1.7f, 1.15f, 2.1f), block % 2 * 3);
                    Box(3, new Vector3(0, 5.3f, 0), new Vector3(6.6f, 1.2f, 2.4f));
                    Box(13, new Vector3(0, 5.95f, 0), new Vector3(5.5f, 0.18f, 1.9f));
                    for (int i = 0; i < 4; i++) Rock(3, new Vector3(-3.5f + i * 2, 0.3f, 2), new Vector3(1.5f, 0.8f, 1.1f));
                    break;
                case "Snow Fir":
                    g.Add(trunk, palette[0], Vector3.zero, new Vector3(0.8f, 10, 0.8f));
                    for (int tier = 0; tier < 6; tier++)
                    {
                        float w = 7 - tier * 0.85f, y = 1.8f + tier * 1.45f;
                        g.Add(pineTier, palette[1], Vector3.up * y, new Vector3(w, 2.6f, w), tier * 33);
                        g.Add(pineTier, palette[4], Vector3.up * (y + 0.65f), new Vector3(w * 0.77f, 2.05f, w * 0.77f), tier * 33);
                    }
                    break;
                case "Bare Pine":
                    g.Add(trunk, palette[0], Vector3.zero, new Vector3(0.85f, 11, 0.85f));
                    for (int limb = 0; limb < 8; limb++)
                    {
                        float angle = limb * 137 * Mathf.Deg2Rad;
                        Vector3 a = Vector3.up * (3 + limb * 0.8f), b = a + new Vector3(Mathf.Cos(angle), 0.7f, Mathf.Sin(angle)) * (3 - limb * 0.2f);
                        Beam(0, a, b, 0.32f); Beam(0, b, b + new Vector3(0.2f, 0.9f, 0.3f), 0.14f);
                    }
                    break;
                case "Trail Cairn":
                    for (int i = 0; i < 5; i++) Rock(i == 4 ? 4 : 3, new Vector3(i % 2 * 0.15f, 0.45f + i * 0.65f, 0), new Vector3(2.5f - i * 0.32f, 0.8f, 2.1f - i * 0.28f));
                    Box(9, new Vector3(2.3f, 1.5f, 0), new Vector3(0.15f, 3, 0.15f));
                    Box(12, new Vector3(2.3f, 2.6f, 0), new Vector3(1.6f, 0.5f, 0.2f));
                    break;
                case "Rock Spires":
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 p = new Vector3((i - 1) * 2.6f, 0, (i % 2) * 1.7f), scale = new Vector3(3.8f, 9 + i * 3, 4);
                        g.AddSurface(peaks[i], 0, palette[3], p, scale, i * 37);
                        g.AddSurface(peaks[i], 1, palette[4], p, scale, i * 37);
                    }
                    break;
                case "Alpine Cabin":
                case "Lifeguard Hut":
                    bool beach = name == "Lifeguard Hut"; float baseY = beach ? 2.2f : 0;
                    Box(9, new Vector3(0, baseY + 2.1f, 0), new Vector3(6, 4.2f, 5));
                    g.Add(roof, palette[beach ? 6 : 4], new Vector3(0, baseY + 4.2f, 0), new Vector3(7.5f, 2.4f, 6.5f));
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Box(12, new Vector3(side * 1.7f, baseY + 2.6f, -2.55f), new Vector3(1.65f, 1.8f, 0.2f));
                        Box(11, new Vector3(side * 1.7f, baseY + 2.6f, -2.68f), new Vector3(1.35f, 1.5f, 0.1f));
                        if (beach) for (int end = -1; end <= 1; end += 2) Box(9, new Vector3(side * 2.4f, 1, end * 2), new Vector3(0.4f, 2.4f, 0.4f));
                    }
                    Box(0, new Vector3(0, baseY + 1.4f, -2.56f), new Vector3(1.1f, 2.8f, 0.2f));
                    Box(9, new Vector3(0, baseY + 0.05f, -3.25f), new Vector3(7, 0.25f, 1.9f));
                    if (!beach) Box(3, new Vector3(2, 5.1f, 1), new Vector3(0.8f, 3, 0.8f));
                    else
                    {
                        for (int step = 0; step < 7; step++) Box(9, new Vector3(0, baseY - step * 0.37f, -4 - step * 0.43f), new Vector3(1.6f, 0.2f, 0.5f));
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector3 a = new Vector3(side * 0.63f, 2.1f, -3.85f), b = new Vector3(side * 0.63f, -0.08f, -6.75f);
                            g.Add(cube, palette[9], (a + b) * 0.5f, new Vector3(0.16f, Vector3.Distance(a, b) + 0.16f, 0.16f),
                                Quaternion.FromToRotation(Vector3.up, (b - a).normalized));
                        }
                    }
                    break;
                case "Barrel Cactus":
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 p = new Vector3((i - 1) * 1.4f, 1.2f - i * 0.17f, i % 2 * 0.8f);
                        Rock(1, p, new Vector3(1.9f, 2.8f - i * 0.3f, 1.9f));
                        for (int rib = 0; rib < 10; rib++)
                        {
                            float a = rib * Mathf.PI / 5;
                            Beam(2, p + new Vector3(Mathf.Cos(a) * 0.7f, -0.8f, Mathf.Sin(a) * 0.7f),
                                p + new Vector3(Mathf.Cos(a) * 0.7f, 0.7f, Mathf.Sin(a) * 0.7f), 0.055f);
                        }
                        Rock(5, p + Vector3.up * 1.1f, new Vector3(0.5f, 0.2f, 0.5f));
                    }
                    break;
                case "Prickly Pear":
                    // Each child pad overlaps its parent instead of jumping across a sine wave.
                    Vector3[] pads = { new Vector3(0, 0.75f, 0), new Vector3(-0.3f, 1.85f, 0),
                        new Vector3(0.45f, 1.9f, 0.06f), new Vector3(-0.85f, 2.85f, 0),
                        new Vector3(0.95f, 2.9f, 0.06f), new Vector3(-1.3f, 3.9f, 0),
                        new Vector3(1.4f, 3.95f, 0.06f), new Vector3(0, 2.95f, 0.1f) };
                    for (int pad = 0; pad < 8; pad++)
                    {
                        Vector3 p = pads[pad];
                        g.Add(crowns[0], palette[pad % 3 == 0 ? 2 : 1], p, new Vector3(1.2f, 1.7f, 0.38f), pad % 3 * 17);
                        if (pad > 4) Rock(6, p + Vector3.up * 0.8f, Vector3.one * 0.22f);
                    }
                    break;
                case "Agave":
                    for (int i = 0; i < 12; i++) g.Add(Blade(), palette[i % 4 == 0 ? 2 : 1], new Vector3(0, 0.2f, 0),
                        new Vector3(2.9f, 3.7f, 1.2f), Quaternion.Euler(0, i * 30, 30 + i % 3 * 14));
                    break;
                case "Sandstone Arch":
                    Mesh arch = Own(EnvironmentModelMeshes.RockArch());
                    g.Add(arch, palette[6], Vector3.zero, new Vector3(11, 10, 4.5f));
                    for (int i = 0; i < 3; i++) Rock(6, new Vector3(-3 + i * 3, 0.35f, 1.8f), new Vector3(2.5f, 1, 1.8f));
                    break;
                case "Desert Ruins":
                    Box(6, new Vector3(0, 0.3f, 0), new Vector3(7.5f, 0.6f, 5.5f));
                    for (int i = 0; i < 4; i++)
                    {
                        float h = 3 + i % 3 * 1.4f;
                        Box(6, new Vector3(-3 + i * 2, h * 0.5f, 1.8f), new Vector3(1.5f, h, 1.4f));
                        Box(5, new Vector3(-3 + i * 2, h, 1.8f), new Vector3(1.8f, 0.35f, 1.8f));
                    }
                    Box(6, new Vector3(-3, 1.2f, -0.5f), new Vector3(1.3f, 2.4f, 4));
                    for (int i = 0; i < 5; i++) Rock(6, new Vector3(-2 + i, 0.5f, -1), new Vector3(1.2f, 1, 1.4f));
                    break;
                case "Oasis Palms":
                    Palm(g, new Vector3(-2, 0, 0), 9, 30); Palm(g, new Vector3(2, 0, 1), 7, 210);
                    Rock(13, new Vector3(0, 0.5f, 0), new Vector3(4, 1, 3));
                    break;
                case "Striped Umbrella":
                    // The canopy's hub is at 3.5 + 0.32 * 2 = 4.14 metres, not at its rim.
                    Round(9, new Vector3(0, 2.05f, 0), new Vector3(0.14f, 2.1f, 0.14f));
                    Mesh umbrella = Own(EnvironmentModelMeshes.UmbrellaCanopy());
                    g.AddSurface(umbrella, 0, palette[12], new Vector3(0, 3.5f, 0), new Vector3(5, 2, 5), 0);
                    g.AddSurface(umbrella, 1, palette[6], new Vector3(0, 3.5f, 0), new Vector3(5, 2, 5), 0);
                    Box(12, new Vector3(0, 0.025f, 0), new Vector3(3.5f, 0.05f, 2.2f));
                    break;
                case "Fishing Boat":
                    g.Add(Own(EnvironmentModelMeshes.BoatHull()), palette[9], Vector3.zero, new Vector3(3.3f, 2.4f, 8));
                    for (int bench = -1; bench <= 1; bench++) Box(12, new Vector3(0, 0.78f, bench * 1.7f), new Vector3(2.7f, 0.16f, 0.6f));
                    Round(9, new Vector3(0, 3.7f, 0), new Vector3(0.14f, 3.2f, 0.14f));
                    g.Add(Own(EnvironmentModelMeshes.TriangleSail()), palette[12], new Vector3(0, 1.4f, 0), new Vector3(3.1f, 5.1f, 1), 0);
                    Beam(9, new Vector3(0, 1.5f, 0), new Vector3(3.1f, 1.65f, 0), 0.12f);
                    break;
                case "Lighthouse":
                    Round(12, new Vector3(0, 6, 0), new Vector3(3.1f, 6, 3.1f));
                    for (int i = 0; i < 3; i++) Round(6, new Vector3(0, 2 + i * 3.5f, 0), new Vector3(3.15f, 0.42f, 3.15f));
                    Round(12, new Vector3(0, 12.2f, 0), new Vector3(4.2f, 0.3f, 4.2f));
                    Round(11, new Vector3(0, 13.5f, 0), new Vector3(2.4f, 1.1f, 2.4f));
                    g.Add(roof, palette[6], new Vector3(0, 14.6f, 0), new Vector3(4.1f, 1.6f, 4.1f));
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4;
                        Round(12, new Vector3(Mathf.Cos(a) * 1.3f, 13.4f, Mathf.Sin(a) * 1.3f), new Vector3(0.13f, 1.2f, 0.13f));
                    }
                    Box(11, new Vector3(0, 1.6f, -1.57f), new Vector3(1.1f, 2.8f, 0.12f));
                    break;
                case "Beach Chairs":
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Box(12, new Vector3(side * 1.2f, 0.6f, 0), new Vector3(1, 0.14f, 2));
                        g.Add(cube, palette[10], new Vector3(side * 1.2f, 1.3f, 0.95f), new Vector3(1, 1.6f, 0.15f), Quaternion.Euler(20, 0, 0));
                        for (int end = -1; end <= 1; end += 2) Box(9, new Vector3(side * 1.2f, 0.28f, end * 0.7f), new Vector3(0.8f, 0.55f, 0.12f));
                    }
                    Round(9, new Vector3(0, 0.65f, -0.5f), new Vector3(0.85f, 0.07f, 0.85f));
                    Round(9, new Vector3(0, 0.3f, -0.5f), new Vector3(0.16f, 0.28f, 0.16f));
                    Round(9, new Vector3(0, 0.05f, -0.5f), new Vector3(0.55f, 0.05f, 0.55f));
                    break;
                default:
                    BuildCityVariety(g, name);
                    break;
            }
            Mesh result = g.Bake(); SetCityAnchor(result, Vector2.zero); return result;
        }

        private static void BuildCityVariety(Geometry g, string name)
        {
            if (name == "Park Fountain")
            {
                for (int tier = 0; tier < 3; tier++)
                {
                    float radius = 7 - tier * 1.8f;
                    g.Add(cylinder, cityPalette[5], new Vector3(0, 0.25f + tier * 1.4f, 0), new Vector3(radius, 0.25f, radius));
                    g.Add(cylinder, cityPalette[4], new Vector3(0, 0.53f + tier * 1.4f, 0), new Vector3(radius * 0.86f, 0.035f, radius * 0.86f));
                }
                g.Add(cylinder, cityPalette[5], new Vector3(0, 1.6f, 0), new Vector3(0.7f, 1.6f, 0.7f));
            }
            else if (name == "Water Tower")
            {
                for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                {
                    CityBox(g, 9, x * 2, 5, z * 2, 0.28f, 10, 0.28f);
                    g.Add(cube, cityPalette[9], new Vector3(0, 4.5f, z * 2), new Vector3(0.18f, 9, 0.18f), Quaternion.Euler(0, 0, x * 26));
                }
                g.Add(cylinder, cityPalette[2], new Vector3(0, 11.5f, 0), new Vector3(5.6f, 2.1f, 5.6f));
                g.Add(roof, cityPalette[9], new Vector3(0, 13.6f, 0), new Vector3(6.1f, 1.7f, 6.1f));
            }
            else if (name == "Garden Pergola")
            {
                for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2) CityBox(g, 11, x * 3.1f, 2.2f, z * 2.4f, 0.35f, 4.4f, 0.35f);
                for (int slat = 0; slat < 9; slat++) CityBox(g, 11, -3.6f + slat * 0.9f, 4.5f, 0, 0.24f, 0.2f, 6.4f);
                for (int i = 0; i < 3; i++) g.Add(crowns[i], cityPalette[10], new Vector3(i * 2.4f - 2.4f, 4.5f, 1.4f), new Vector3(3.2f, 0.7f, 2.3f));
                CityBox(g, 11, 0, 0.85f, 1.5f, 5.5f, 0.18f, 0.9f);
            }
            else if (name == "Glass Pavilion")
            {
                CityBox(g, 5, 0, 0.15f, 0, 7, 0.3f, 6);
                CityBox(g, 4, 0, 2.6f, 2.8f, 6.6f, 4.8f, 0.13f);
                for (int side = -1; side <= 1; side += 2)
                {
                    CityBox(g, 4, side * 3.3f, 2.6f, 0, 0.13f, 4.8f, 5.6f);
                    for (int post = 0; post < 4; post++) CityBox(g, 5, side * 3.3f, 2.6f, -2.8f + post * 1.86f, 0.16f, 4.9f, 0.16f);
                }
                g.Add(roof, cityPalette[7], new Vector3(0, 5.1f, 0), new Vector3(7.5f, 2.2f, 6.5f));
                CityBox(g, 5, 0, 5.1f, 0, 7.1f, 0.2f, 6.1f);
            }
            else if (name == "Market Stalls")
            {
                for (int stall = -1; stall <= 1; stall += 2)
                {
                    CityBox(g, 11, stall * 2.5f, 0.85f, 0, 3.3f, 1.7f, 2.2f);
                    for (int side = -1; side <= 1; side += 2) CityBox(g, 9, stall * 2.5f + side * 1.5f, 2, 0.8f, 0.14f, 4, 0.14f);
                    for (int stripe = 0; stripe < 7; stripe++) CityBox(g, stripe % 2 == 0 ? 8 : 5, stall * 2.5f - 1.5f + stripe * 0.5f, 4.1f, 0, 0.49f, 0.25f, 3.1f);
                    for (int crate = 0; crate < 3; crate++) CityBox(g, crate % 2 == 0 ? 10 : 8, stall * 2.5f - 1 + crate, 1.9f, 0, 0.7f, 0.5f, 0.8f);
                }
            }
            else throw new InvalidOperationException("Unknown variety model: " + name);
        }

        private static void PopulateVariety(Geometry g, EnvironmentBiome biome, int layout, Material[] palette, Dictionary<string, Mesh> models, Mesh landscape)
        {
            var random = new System.Random(8491 + (int)biome * 503 + layout * 149);
            void Place(string name, float x, float z, float yaw = 0, float scale = 1, bool afloat = false)
            {
                float y = biome == EnvironmentBiome.Jungle ? JungleGroundHeight(x, z) : -9;
                if (biome == EnvironmentBiome.Desert) y = VarietyGroundHeight(landscape, 5, x, z);
                if (afloat) y = CubeDashOceanWaves.OceanLocalHeight - 0.15f;
                AddVarietyModel(g, models[name], palette, new Vector3(x, y, z), Vector3.one * scale, yaw);
            }
            float jitter = Range(random, -1.4f, 1.4f);
            switch (biome)
            {
                case EnvironmentBiome.City:
                    Place(layout == 6 ? "Water Tower" : layout % 2 == 0 ? "Park Fountain" : "Garden Pergola", -18, 21, 0, 0.85f);
                    Place(layout % 2 == 0 ? "Glass Pavilion" : "Market Stalls", 18, 7, 90, 0.95f);
                    Place(layout % 3 == 0 ? "Water Tower" : "Garden Pergola", -59, 31 + jitter, 90);
                    break;
                case EnvironmentBiome.Jungle:
                    Place("Bamboo Grove", layout % 2 == 0 ? 12.5f : -13, 8 + jitter, layout * 39, 0.85f);
                    Place("Banana Plant", -12.5f, 27 + jitter, layout * 51, 0.85f);
                    Place(layout % 3 == 0 ? "Ruined Gateway" : "Fan Palm", 28, 20 + jitter, 90, layout % 3 == 0 ? 1 : 1.2f);
                    Place("Mossy Fallen Log", 13.5f, 33 + jitter, 15 + layout * 13, 0.8f);
                    if (layout >= 4) Place("Fan Palm", -20, 16, 160, 1.25f);
                    break;
                case EnvironmentBiome.Mountains:
                    Place(layout % 2 == 0 ? "Alpine Cabin" : "Rock Spires", -15.5f, 20 + jitter, 90, 0.9f);
                    Place("Snow Fir", 15, 8 + jitter, layout * 25, 0.8f + layout % 3 * 0.1f);
                    Place("Trail Cairn", 12, 28 + jitter, -90);
                    Place("Bare Pine", -13, 35 + jitter, layout * 51, 0.9f);
                    if (layout >= 4) Place("Rock Spires", 31, 19, 27, 1.2f);
                    break;
                case EnvironmentBiome.Desert:
                    Place(layout % 2 == 0 ? "Barrel Cactus" : "Prickly Pear", 11.5f, 9 + jitter, layout * 37);
                    Place("Agave", -12, 28 + jitter, layout * 27);
                    Place(layout % 3 == 0 ? "Oasis Palms" : layout % 3 == 1 ? "Sandstone Arch" : "Desert Ruins", -20, 17 + jitter, 90);
                    Place("Prickly Pear", 14, 33 + jitter, 180, 0.8f);
                    if (layout >= 4) Place("Sandstone Arch", 32, 24, 90, 1.1f);
                    break;
                case EnvironmentBiome.Beach:
                    Place("Striped Umbrella", -12.5f, 8 + jitter, layout * 35, 0.9f);
                    Place("Beach Chairs", -12.5f, 13 + jitter, 90);
                    Place(layout % 3 == 0 ? "Lighthouse" : "Lifeguard Hut", -27, 26 + jitter, 90, layout % 3 == 0 ? 1 : 0.9f);
                    Place("Fishing Boat", 38 + layout % 3 * 4, 9 + jitter, -15 + layout * 13, 0.9f, true);
                    Place("Driftwood", -10, 33 + jitter, 70, 0.75f);
                    if (layout >= 4) Place("Striped Umbrella", -13, 23, 30, 0.85f);
                    break;
            }
        }

        private static float VarietyGroundHeight(Mesh mesh, int surface, float x, float z)
        {
            Vector3[] vertices = mesh.vertices; int[] indices = mesh.GetTriangles(surface);
            float height = -9;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(denominator) < 1e-8f) continue;
                float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / denominator;
                float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / denominator;
                if (u >= 0 && v >= 0 && u + v <= 1) height = Mathf.Max(height, a.y * u + b.y * v + c.y * (1 - u - v));
            }
            return height;
        }

        private static void AddVarietyModel(Geometry g, Mesh source, Material[] palette, Vector3 position, Vector3 scale, float yaw)
        {
            Vector3[] sourceVertices = source.vertices, sourceNormals = source.normals;
            for (int surface = 0; surface < palette.Length; surface++)
            {
                int[] indices = source.GetTriangles(surface);
                if (indices.Length == 0) continue;
                var remap = new Dictionary<int, int>(); var vertices = new List<Vector3>(); var normals = new List<Vector3>();
                for (int i = 0; i < indices.Length; i++)
                {
                    int old = indices[i];
                    if (!remap.TryGetValue(old, out int mapped))
                    {
                        mapped = vertices.Count; remap[old] = mapped;
                        vertices.Add(sourceVertices[old]); normals.Add(sourceNormals[old]);
                    }
                    indices[i] = mapped;
                }
                Mesh part = Own(new Mesh { indexFormat = IndexFormat.UInt32 });
                part.SetVertices(vertices); part.SetNormals(normals); part.SetTriangles(indices, 0);
                SetCityAnchor(part, new Vector2(position.x, position.z));
                g.Add(part, palette[surface], position, scale, yaw);
            }
        }
    }
}
