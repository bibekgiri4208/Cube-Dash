using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Bakes reusable low-poly landscapes into assets, never into the running game.</summary>
    public static class CubeDashEnvironments
    {
        private const string Prefabs = "Assets/Prefab/CubeDash/Environments";
        private const string Materials = "Assets/Material/Environments";
        private const string Meshes = "Assets/3D Models/Environments";
        private static Mesh cube, cylinder, rock, dune, hill, trunk, branch, pineTier, palmTrunk, frond, cactusStem, cactusArm, roof;
        private static Mesh[] crowns, peaks, mesas;
        private static readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private static Material bark, leaf, lightLeaf, stone, snow, sand, redStone, grass, water, foam, wood;
        private static Material riverWater, wetSand, thatch, glass, trim, moss, dryGrass;
        private const float LandscapeExtent = 1024f;

        [MenuItem("Tools/Cube Dash/Apply Dynamic Environments")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            if (game == null) throw new InvalidOperationException("The authored Cube Dash level is required.");
            Folder(Prefabs); Folder(Materials); Folder(Meshes);
            InitializeModels();
            try
            {
                MakeMaterials();
                GameObject[] scenery = new GameObject[4];
                for (int i = 0; i < scenery.Length; i++) scenery[i] = BuildScenery((EnvironmentBiome)(i + 1));
                UpdateTrackPrefab(scenery);
                PolishCityBuildings();
                ExtendCityGround();

                EnvironmentDirector director = game.Track.GetComponent<EnvironmentDirector>();
                if (director == null) director = game.Track.gameObject.AddComponent<EnvironmentDirector>();
                Set(game.Track, "environment", director);
                Set(director, "sunlight", GameObject.Find("Directional Light").GetComponent<Light>());
                Set(director, "skyFill", GameObject.Find("Sky Bounce Fill")?.GetComponent<Light>());
                SetAtmospheres(director);
                foreach (TrackSegment segment in game.Track.Segments)
                    segment.Environment.Configure(Array.IndexOf(game.Track.Segments, segment) - 1, director.SegmentsPerBiome);

                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Dynamic environments saved: city, jungle, mountains, desert and beach; three landscape layouts each, pooled scenery and distance-blended atmosphere.");
            }
            finally { ReleaseModels(); }
        }

        [MenuItem("Tools/Cube Dash/Rebuild Environment Models")]
        public static void RebuildModelsFromCommandLine()
        {
            // Only rebake model assets. Preserve the open scene, tuning, atmosphere and HUD.
            Folder(Prefabs); Folder(Materials); Folder(Meshes);
            InitializeModels();
            try
            {
                MakeMaterials();
                for (int i = 1; i < BiomeRules.Count; i++) BuildScenery((EnvironmentBiome)i);
                PolishCityBuildings();
                ExtendCityGround();
                AssetDatabase.SaveAssets();
                Debug.Log("Environment assets rebuilt: wide coastlines, animated surf, detailed natural surfaces, richer foliage, timber houses and city facades.");
            }
            finally { ReleaseModels(); }
        }

        private static void InitializeModels()
        {
            cube = PrimitiveMesh(PrimitiveType.Cube);
            cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
            roof = Own(EnvironmentModelMeshes.HipRoof());
            rock = Own(EnvironmentModelMeshes.Foliage(37, true));
            dune = Own(EnvironmentModelMeshes.Hill(29, true));
            hill = Own(EnvironmentModelMeshes.Hill(17, false));
            trunk = Own(EnvironmentModelMeshes.Tube(new[]
            {
                Vector3.zero, new Vector3(0.01f, 0.07f, 0), new Vector3(0.10f, 0.28f, 0.02f),
                new Vector3(-0.06f, 0.53f, 0.04f), new Vector3(0.12f, 0.78f, 0.03f), new Vector3(0.18f, 1, 0)
            }, new[] { 0.65f, 0.43f, 0.31f, 0.24f, 0.17f, 0.08f }, levelRings: true));
            branch = Own(EnvironmentModelMeshes.Tube(new[] { Vector3.zero, new Vector3(0.04f, 0.55f, 0), Vector3.up },
                new[] { 0.5f, 0.26f, 0.07f }, 8));
            pineTier = Own(EnvironmentModelMeshes.PineTier(13));
            frond = Own(EnvironmentModelMeshes.PalmFrond());
            Vector3[] palmCenters = new Vector3[17];
            float[] palmRadii = new float[palmCenters.Length];
            for (int ring = 0; ring < palmCenters.Length; ring++)
            {
                float t = ring / (float)(palmCenters.Length - 1);
                palmCenters[ring] = new Vector3(t * t * 2.2f, t, t * t * 0.3f);
                palmRadii[ring] = Mathf.Lerp(0.52f, 0.25f, t) * (ring % 2 == 0 ? 1.06f : 0.96f);
            }
            palmTrunk = Own(EnvironmentModelMeshes.Tube(palmCenters, palmRadii, 10, levelRings: true));
            cactusStem = Own(EnvironmentModelMeshes.Tube(new[]
            {
                Vector3.zero, new Vector3(0, 0.18f, 0), new Vector3(0.035f, 0.5f, 0),
                new Vector3(0.025f, 0.85f, 0), new Vector3(0.02f, 0.97f, 0), new Vector3(0.02f, 1, 0)
            }, new[] { 0.48f, 0.52f, 0.5f, 0.47f, 0.31f, 0.03f }, 12, levelRings: true));
            cactusArm = Own(EnvironmentModelMeshes.Tube(new[]
            {
                Vector3.zero, new Vector3(0.16f, 0.01f, 0), new Vector3(0.30f, 0.06f, 0),
                new Vector3(0.36f, 0.16f, 0), new Vector3(0.36f, 0.38f, 0),
                new Vector3(0.36f, 0.48f, 0), new Vector3(0.36f, 0.51f, 0)
            }, new[] { 0.115f, 0.115f, 0.11f, 0.10f, 0.10f, 0.07f, 0.01f }, 10));
            crowns = new Mesh[3]; peaks = new Mesh[3]; mesas = new Mesh[3];
            for (int i = 0; i < 3; i++)
            {
                crowns[i] = Own(EnvironmentModelMeshes.Foliage(11 + i * 17));
                peaks[i] = Own(EnvironmentModelMeshes.Mountain(23 + i * 13));
                mesas[i] = Own(EnvironmentModelMeshes.Mesa(7 + i * 11));
            }
        }

        private static Mesh Own(Mesh mesh) { ownedMeshes.Add(mesh); return mesh; }
        private static void ReleaseModels()
        {
            foreach (Mesh mesh in ownedMeshes) Object.DestroyImmediate(mesh);
            ownedMeshes.Clear();
        }

        private static void MakeMaterials()
        {
            bark = Material("Bark", new Color(0.28f, 0.19f, 0.12f));
            leaf = Material("Deep Foliage", new Color(0.08f, 0.32f, 0.22f));
            lightLeaf = Material("Sunlit Foliage", new Color(0.24f, 0.52f, 0.31f));
            stone = Material("Mountain Slate", new Color(0.40f, 0.49f, 0.55f));
            snow = Material("Snow", new Color(0.91f, 0.96f, 0.98f));
            sand = Material("Golden Sand", new Color(0.83f, 0.66f, 0.40f));
            redStone = Material("Sandstone", new Color(0.68f, 0.36f, 0.22f));
            grass = Material("Forest Floor", new Color(0.19f, 0.34f, 0.25f));
            water = Material("Lagoon Water", new Color(0.025f, 0.24f, 0.34f), "CubeDash/Biome Water");
            foam = Material("Shore Foam", new Color(0.74f, 0.90f, 0.86f));
            wood = Material("Beach Timber", new Color(0.51f, 0.36f, 0.23f));
            riverWater = Material("River Water", new Color(0.04f, 0.28f, 0.29f), "CubeDash/Biome Water");
            wetSand = Material("Wet Sand", new Color(0.54f, 0.45f, 0.30f));
            thatch = Material("Roof Thatch", new Color(0.62f, 0.49f, 0.28f));
            glass = Material("Facade Glass", new Color(0.13f, 0.25f, 0.29f));
            trim = Material("Ivory Trim", new Color(0.76f, 0.80f, 0.72f));
            moss = Material("Rock Moss", new Color(0.27f, 0.39f, 0.24f));
            dryGrass = Material("Dry Grass", new Color(0.54f, 0.48f, 0.25f));
            Surface(bark, 1, 1.4f); Surface(leaf, 2, 1.7f); Surface(lightLeaf, 2, 1.7f);
            Surface(stone, 0, 1.3f); Surface(snow, 5, 2); Surface(sand, 3, 0.8f);
            Surface(redStone, 0, 1.5f); Surface(grass, 6, 1.3f); Surface(wood, 4, 1.2f);
            Surface(wetSand, 3, 1); Surface(thatch, 4, 2.5f); Surface(trim, 0, 0.7f);
            Surface(moss, 6, 2); Surface(dryGrass, 6, 2); Surface(glass, 0, 0.2f);
            glass.SetFloat("_Smoothness", 0.78f); glass.SetFloat("_Metallic", 0.18f);
            wetSand.SetFloat("_Smoothness", 0.42f);
            water.SetColor("_ShallowColor", new Color(0.12f, 0.64f, 0.63f));
            CubeDashOceanWaves.ConfigureWater(water);
            CubeDashOceanWaves.ConfigureSand(wetSand);
            riverWater.SetColor("_ShallowColor", new Color(0.13f, 0.45f, 0.39f));
            riverWater.SetFloat("_WaveHeight", 0.035f); riverWater.SetFloat("_ShoreMode", 0);
            AssetDatabase.SaveAssets();
        }

        private static void Surface(Material material, float type, float scale)
        {
            material.SetFloat("_DetailType", type); material.SetFloat("_DetailScale", scale);
            material.SetColor("_SecondaryColor", Color.Lerp(material.GetColor("_BaseColor"), new Color(0.17f, 0.20f, 0.16f), 0.24f));
            EditorUtility.SetDirty(material);
        }

        private static Material Material(string name, Color color, string shader = "CubeDash/Environment Surface")
        {
            string path = Materials + "/" + name + ".mat";
            Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                result = new Material(Shader.Find(shader)) { name = name };
                AssetDatabase.CreateAsset(result, path);
            }
            result.shader = Shader.Find(shader);
            result.SetColor("_BaseColor", color);
            result.enableInstancing = true;
            if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", 0.18f);
            if (result.HasProperty("_SpecularHighlights")) result.SetFloat("_SpecularHighlights", 0);
            if (result.HasProperty("_EnvironmentReflections")) result.SetFloat("_EnvironmentReflections", 0);
            EditorUtility.SetDirty(result);
            return result;
        }

        private static GameObject BuildScenery(EnvironmentBiome biome)
        {
            Material[] palette = { bark, leaf, lightLeaf, stone, snow, sand, redStone, grass, foam, wood,
                thatch, glass, trim, moss, dryGrass };
            Mesh[] layouts = new Mesh[3];
            for (int layout = 0; layout < layouts.Length; layout++)
            {
                Geometry geometry = new Geometry(palette);
                System.Random random = new System.Random(1979 + (int)biome * 101 + layout * 37);
                switch (biome)
                {
                    case EnvironmentBiome.Jungle: Jungle(geometry, random); break;
                    case EnvironmentBiome.Mountains: Mountains(geometry, random); break;
                    case EnvironmentBiome.Desert: Desert(geometry, random); break;
                    case EnvironmentBiome.Beach: Beach(geometry, random); break;
                }
                layouts[layout] = SaveMesh(geometry.Bake(), biome + " Layout " + (layout + 1));
            }

            GameObject root = new GameObject(biome + " Surroundings");
            try
            {
                Material floor = biome == EnvironmentBiome.Jungle ? grass : biome == EnvironmentBiome.Mountains ? stone
                    : biome == EnvironmentBiome.Beach ? CubeDashOceanWaves.CoastalSand() : sand;
                Mesh floorMesh = biome == EnvironmentBiome.Beach ? SaveMesh(EnvironmentModelMeshes.BeachSeabed(), "Beach Seabed") : cube;
                Part(root.transform, "Landscape Ground", floorMesh, floor, new Vector3(0, -9.3f, 21), new Vector3(LandscapeExtent * 2, 0.6f, 42));
                if (biome == EnvironmentBiome.Beach)
                {
                    Mesh ocean = SaveMesh(EnvironmentModelMeshes.OceanSurface(), "Ocean Surface");
                    Part(root.transform, "Ocean", ocean, water, new Vector3(0, -8.72f, 0), Vector3.one, false);
                    Part(root.transform, "Shoreline", SaveMesh(EnvironmentModelMeshes.BeachSeabed(true), "Shoreline Sand"),
                        wetSand, new Vector3(5.9f, -9, 21), new Vector3(2.2f, 0.06f, 42), false);
                }
                if (biome == EnvironmentBiome.Jungle)
                {
                    Mesh river = SaveMesh(EnvironmentModelMeshes.WaterSurface(new[] { -31.5f, -30.5f, -29.5f, -28.5f, -27.5f, -26.5f }), "River Surface");
                    Part(root.transform, "River", river, riverWater, new Vector3(0, -8.8f, 0), Vector3.one, false);
                }
                GameObject details = Part(root.transform, "Landscape Details", layouts[0], palette, Vector3.zero, Vector3.one);
                AddHorizonAprons(root.transform, floor, biome == EnvironmentBiome.Beach);
                if (biome == EnvironmentBiome.Beach) AddCoastalTransition(root.transform);
                BiomeScenery scenery = root.AddComponent<BiomeScenery>();
                Set(scenery, "details", details.GetComponent<MeshFilter>());
                SetArray(scenery, "layouts", layouts);
                return PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + biome + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Jungle(Geometry g, System.Random random)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 5; row++)
                {
                    float z = 4 + row * 8 + Range(random, -2, 2);
                    Tree(g, new Vector3(side * Range(random, 16, 23), -9, z), Range(random, 14, 20), false, random);
                    float forestX = side * Range(random, 36, 49);
                    float forestFloor = -9 + EnvironmentModelMeshes.HillHeight(
                        (forestX - side * 46) / 16, (z - 21) / 23, 17, false) * 13;
                    Tree(g, new Vector3(forestX, forestFloor, z), Range(random, 18, 25), false, random);
                    Boulder(g, stone, new Vector3(side * Range(random, 13, 24), -9, z + 2), new Vector3(4, 3, 4), random);
                    Bush(g, new Vector3(side * Range(random, 11, 24), -9, z + 3), random);
                    Fern(g, new Vector3(side * Range(random, 10, 17), -8.3f, z + 1), random);
                    g.Add(crowns[row % 3], moss, new Vector3(side * Range(random, 14, 24), -7.9f, z + 2), new Vector3(2.2f, 0.7f, 2.3f));
                }
                // A distant continuous ridge frames the canopy without intruding into the lanes.
                g.Add(hill, grass, new Vector3(side * 46, -9, 21), new Vector3(32, 13, 46));
                g.Add(hill, grass, new Vector3(side * 115, -9, 21), new Vector3(135, 20, 65));
            }
        }

        private static void Mountains(Geometry g, System.Random random)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int peak = 0; peak < 2; peak++)
                {
                    float height = Range(random, 32, 47), width = Range(random, 38, 48);
                    Vector3 basePosition = new Vector3(side * Range(random, 45, 53), -9, 8 + peak * 22);
                    float angle = Range(random, -20, 20);
                    Mesh form = peaks[random.Next(peaks.Length)];
                    Vector3 scale = new Vector3(width, height, 30);
                    g.AddSurface(form, 0, stone, basePosition, scale, angle);
                    g.AddSurface(form, 1, snow, basePosition, scale, angle);
                    Vector3 spur = basePosition + new Vector3(-side * 7, 0, 6);
                    Vector3 spurScale = new Vector3(width * 0.65f, height * 0.38f, 26);
                    g.AddSurface(form, 0, stone, spur, spurScale, angle + 25);
                    g.AddSurface(form, 1, stone, spur, spurScale, angle + 25);
                }
                for (int row = 0; row < 4; row++)
                {
                    Tree(g, new Vector3(side * Range(random, 15, 24), -9, 4 + row * 10), Range(random, 11, 17), true, random);
                    Boulder(g, stone, new Vector3(side * Range(random, 15, 26), -9, 8 + row * 9), new Vector3(7, 7, 6), random);
                    for (int rubble = 0; rubble < 3; rubble++)
                        Boulder(g, row % 2 == 0 ? snow : stone, new Vector3(side * Range(random, 19, 32), -9, 7 + row * 9 + rubble),
                            new Vector3(1.6f, 1.2f, 1.8f), random);
                }
                g.Add(hill, stone, new Vector3(side * 38, -9, 21), new Vector3(31, 8, 44));
                Mesh farPeak = peaks[random.Next(peaks.Length)];
                g.AddSurface(farPeak, 0, stone, new Vector3(side * 105, -9, 21), new Vector3(110, 43, 60), 5);
                g.AddSurface(farPeak, 1, snow, new Vector3(side * 105, -9, 21), new Vector3(110, 43, 60), 5);
            }
        }

        private static void Desert(Geometry g, System.Random random)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 3; row++)
                {
                    float z = 5 + row * 14;
                    g.Add(dune, sand, new Vector3(side * Range(random, 26, 34), -9, z), new Vector3(32, Range(random, 6, 11), 27), Range(random, -15, 15));
                    Vector3 mesa = new Vector3(side * Range(random, 42, 51), -9, z);
                    float height = Range(random, 16, 26);
                    Mesh bluff = mesas[random.Next(mesas.Length)];
                    Vector3 scale = new Vector3(21, height, 19);
                    g.AddSurface(bluff, 0, redStone, mesa, scale, 8);
                    g.AddSurface(bluff, 1, sand, mesa, scale, 8);
                    Cactus(g, new Vector3(side * Range(random, 12, 20), -9, z), Range(random, 5, 9));
                    Boulder(g, redStone, new Vector3(side * Range(random, 12, 23), -9, z + 5), new Vector3(4, 3, 3), random);
                    for (int pebble = 0; pebble < 3; pebble++)
                        Boulder(g, redStone, new Vector3(side * Range(random, 11, 19), -9, z + 2 + pebble), Vector3.one * Range(random, 0.7f, 1.4f), random);
                    GrassTuft(g, new Vector3(side * Range(random, 12, 22), -9, z + 3), 1.2f, random);
                }
                g.Add(dune, sand, new Vector3(side * 100, -9, 21), new Vector3(130, 10, 65));
            }
        }

        private static void Beach(Geometry g, System.Random random)
        {
            for (int row = 0; row < 4; row++)
            {
                float z = 4 + row * 10;
                Palm(g, new Vector3(-Range(random, 18, 25), -9, z), Range(random, 14, 19), Range(random, 0, 180));
                Boulder(g, stone, new Vector3(Range(random, 13, 21), -9, z), new Vector3(6, 4, 5), random);
                Bush(g, new Vector3(-Range(random, 12, 28), -9, z + 3), random);
            }
            // Stilt huts and a wooden jetty give the coastline a recognizable silhouette.
            float hutZ = Range(random, 10, 28);
            for (int i = 0; i < 2; i++)
            {
                float hutX = -34 - i * 16;
                float groundHeight = -9 + EnvironmentModelMeshes.HillHeight((hutX + 46) / 15, (hutZ - 21) / 22, 29, true) * 4;
                House(g, new Vector3(hutX, groundHeight + 1, hutZ));
            }
            for (int plank = 0; plank < 15; plank++)
                g.Add(cube, wood, new Vector3(11.5f + plank * 1.2f, -6.5f, hutZ), new Vector3(1.14f, 0.32f, 3.2f));
            for (int post = 0; post < 4; post++)
                g.Add(cylinder, wood, new Vector3(13 + post * 4.5f, -7.4f, hutZ), new Vector3(0.5f, 2.5f, 0.5f));
            g.Add(cube, trim, new Vector3(20, -5.6f, hutZ + 1.5f), new Vector3(18, 0.14f, 0.14f));
            g.Add(dune, sand, new Vector3(-46, -9, 21), new Vector3(30, 4, 44));
            g.Add(dune, sand, new Vector3(-130, -9, 21), new Vector3(160, 5, 60));
            for (int clump = 0; clump < 5; clump++)
                GrassTuft(g, new Vector3(-Range(random, 12, 28), -9, 3 + clump * 8), 0.8f, random);
        }

        private static void Tree(Geometry g, Vector3 p, float height, bool pine, System.Random random)
        {
            float yaw = Range(random, 0, 360), diameter = height * (pine ? 0.065f : 0.09f);
            g.Add(trunk, bark, p, new Vector3(diameter, height * 0.83f, diameter), yaw);
            if (pine)
            {
                for (int tier = 0; tier < 5; tier++)
                {
                    float width = height * (0.55f - tier * 0.075f);
                    g.Add(pineTier, tier % 3 == 1 ? lightLeaf : leaf,
                        p + Vector3.up * height * (0.18f + tier * 0.145f),
                        new Vector3(width, height * 0.29f, width), yaw + tier * 47);
                }
            }
            else
            {
                for (int limb = 0; limb < 5; limb++)
                {
                    float angle = (yaw + limb * 72) * Mathf.Deg2Rad;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    Vector3 end = p + direction * height * Range(random, 0.13f, 0.19f)
                        + Vector3.up * height * Range(random, 0.68f, 0.81f);
                    Limb(g, p + Vector3.up * height * (0.39f + limb * 0.045f), end, diameter * 0.5f);
                    g.Add(crowns[random.Next(crowns.Length)], limb % 3 == 0 ? lightLeaf : leaf, end,
                        new Vector3(height * 0.38f, height * Range(random, 0.23f, 0.30f), height * 0.34f), yaw + limb * 33);
                    if (limb % 2 == 0)
                        g.Add(crowns[(limb + 1) % crowns.Length], lightLeaf,
                            end + direction * height * 0.07f + Vector3.up * height * 0.055f,
                            new Vector3(height * 0.19f, height * 0.16f, height * 0.18f), yaw + limb * 47);
                }
                g.Add(crowns[random.Next(crowns.Length)], lightLeaf, p + Vector3.up * height * 0.91f,
                    new Vector3(height * 0.43f, height * 0.32f, height * 0.40f), yaw);
                for (int root = 0; root < 4; root++)
                {
                    float angle = (yaw + root * 90) * Mathf.Deg2Rad;
                    Limb(g, p + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * diameter * 1.25f,
                        p + Vector3.up * diameter * 1.9f, diameter * 0.46f);
                }
            }
        }

        private static void GrassTuft(Geometry g, Vector3 p, float size, System.Random random)
        {
            for (int blade = 0; blade < 5; blade++)
                g.Add(frond, dryGrass, p + Vector3.up * 0.25f,
                    Vector3.one * size, Quaternion.Euler(0, Range(random, 0, 360), Range(random, 42, 72)));
        }

        private static void House(Geometry g, Vector3 p)
        {
            g.Add(cube, wood, p + Vector3.up * 2.5f, new Vector3(7, 5, 6));
            g.Add(roof, thatch, p + Vector3.up * 5, new Vector3(9.4f, 3.2f, 8));
            g.Add(cube, trim, p + Vector3.up * 5, new Vector3(7.7f, 0.22f, 6.8f));
            // Timber siding and roof battens add relief, not just painted-on detail.
            for (int board = 0; board < 9; board++)
                g.Add(cube, wood, p + new Vector3(3.53f, 0.35f + board * 0.51f, 0), new Vector3(0.10f, 0.09f, 6.1f));
            for (int band = 1; band < 6; band++)
            {
                float t = band / 6f;
                for (int side = -1; side <= 1; side += 2)
                    g.Add(cube, wood, p + new Vector3(0, 5 + t * 3.2f, side * (1 - t) * 4),
                        new Vector3(9.4f * (1 - t * 0.54f), 0.07f, 0.10f));
            }
            g.Add(cube, glass, p + new Vector3(3.58f, 1.5f, 0), new Vector3(0.14f, 2.8f, 1.2f));
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 window = p + new Vector3(3.60f, 2.8f, side * 2.0f);
                g.Add(cube, glass, window, new Vector3(0.12f, 1.45f, 1.3f));
                g.Add(cube, trim, window, new Vector3(0.20f, 0.08f, 1.4f));
                g.Add(cube, trim, window, new Vector3(0.20f, 1.5f, 0.08f));
                for (int edge = -1; edge <= 1; edge += 2)
                    g.Add(cube, trim, window + Vector3.up * edge * 0.76f, new Vector3(0.20f, 0.10f, 1.5f));
                for (int corner = -1; corner <= 1; corner += 2)
                    g.Add(cube, bark, p + new Vector3(corner * 2.8f, -0.6f, side * 2.4f), new Vector3(0.55f, 3.3f, 0.55f));
                g.Add(cube, trim, p + new Vector3(4.9f, 1.35f, side * 3), new Vector3(0.16f, 2.5f, 0.16f));
                g.Add(cube, wood, p + new Vector3(4.5f, 1.6f, side * 3), new Vector3(2, 0.13f, 0.13f));
            }
            g.Add(cube, wood, p + new Vector3(4.1f, 0.05f, 0), new Vector3(2.6f, 0.25f, 6.8f));
            for (int step = 0; step < 3; step++)
                g.Add(cube, wood, p + new Vector3(5.6f + step * 0.38f, -0.18f - step * 0.26f, 0), new Vector3(0.45f, 0.22f, 1.6f));
        }

        private static void PolishCityBuildings()
        {
            for (int variant = 0; variant < 3; variant++)
            {
                string path = "Assets/Prefab/CubeDash/Skyscraper" + (variant + 1) + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform architecture = root.transform.Find("Matte Architecture");
                    Transform body = architecture.Find("Tower Body");
                    float h = body.localScale.y, w = body.localScale.x, depth = body.localScale.z;
                    Geometry g = new Geometry(new[] { glass, trim });
                    var boundaries = new List<float>();
                    foreach (Transform part in architecture)
                        if (part.name.StartsWith("Floor Band ", StringComparison.Ordinal)) boundaries.Add(part.localPosition.y);
                    boundaries.Add(h);
                    boundaries.Sort();
                    // Place complete windows between the actual floor bands, never through them.
                    for (int interval = 0; interval < boundaries.Count - 1; interval++)
                    {
                        float span = boundaries[interval + 1] - boundaries[interval];
                        int floors = Mathf.Max(1, Mathf.FloorToInt(span / 2.4f));
                        float spacing = span / floors, paneHeight = Mathf.Min(1.6f, spacing - 0.7f);
                        for (int floor = 0; floor < floors; floor++)
                        {
                            float y = boundaries[interval] + spacing * (floor + 0.5f);
                            for (int side = -1; side <= 1; side += 2)
                                for (int column = -1; column <= 1; column++)
                                {
                                    g.Add(cube, glass, new Vector3(column * w * 0.28f, y, side * (depth * 0.5f + 0.07f)),
                                        new Vector3(w * 0.21f, paneHeight, 0.12f));
                                    g.Add(cube, trim, new Vector3(column * w * 0.28f, y - paneHeight * 0.5f - 0.1f, side * (depth * 0.5f + 0.16f)),
                                        new Vector3(w * 0.24f, 0.10f, 0.30f));
                                    // The short tower's east annex covers this wall below its roof.
                                    if (variant == 2 && side == 1 && y - paneHeight * 0.5f < 9.6f) continue;
                                    g.Add(cube, glass, new Vector3(side * (w * 0.5f + 0.07f), y, column * depth * 0.30f),
                                        new Vector3(0.12f, paneHeight, depth * 0.19f));
                                    g.Add(cube, trim, new Vector3(side * (w * 0.5f + 0.16f), y - paneHeight * 0.5f - 0.1f, column * depth * 0.30f),
                                        new Vector3(0.30f, 0.10f, depth * 0.21f));
                                }
                        }
                    }
                    for (int side = -1; side <= 1; side += 2)
                    {
                        g.Add(cube, glass, new Vector3(0, 1.4f, side * 3.77f), new Vector3(2.2f, 2.7f, 0.12f));
                        g.Add(cube, trim, new Vector3(0, 2.95f, side * 4.2f), new Vector3(3.2f, 0.20f, 1.5f));
                        g.Add(cube, trim, new Vector3(side * (w * 0.5f + 0.06f), h * 0.5f, -2.88f), new Vector3(0.15f, h - 3, 0.2f));
                    }
                    Transform plant = architecture.Find("Roof Unit");
                    for (int vent = 0; vent < 6; vent++)
                        g.Add(cube, glass, plant.localPosition + new Vector3(0, -0.43f + vent * 0.16f, plant.localScale.z * 0.5f + 0.04f),
                            new Vector3(plant.localScale.x * 0.8f, 0.06f, 0.06f));
                    Mesh detailMesh = SaveMesh(g.Bake(), "City Facade " + (variant + 1));
                    Transform existing = root.transform.Find("Facade Details");
                    GameObject details = existing != null ? existing.gameObject
                        : Part(root.transform, "Facade Details", detailMesh, new[] { glass, trim }, Vector3.zero, Vector3.one, false);
                    details.GetComponent<MeshFilter>().sharedMesh = detailMesh;
                    details.GetComponent<MeshRenderer>().sharedMaterials = new[] { glass, trim };
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static void AddCoastalTransition(Transform root)
        {
            Geometry g = new Geometry(new[] { stone, sand });
            var random = new System.Random(8819);
            foreach (float x in new[] { 11f, 18, 28, 42, 60, 82, 110 })
            {
                float nearRoad = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(7, 120, x));
                float bank = 8 + nearRoad * 22 + Mathf.Sin(x * 0.055f) * 2.8f + Mathf.Sin(x * 0.17f) * 1.2f;
                float z = RunnerRules.SegmentLength - bank;
                Boulder(g, stone, new Vector3(x, -9, z + 0.7f), new Vector3(4.5f, 3, 4.5f), random);
                g.Add(hill, sand, new Vector3(x, -9, z + 3), new Vector3(7, 0.65f, 8));
            }
            Mesh mesh = SaveMesh(g.Bake(), "Coastal Headland");
            GameObject headland = Part(root, "Coastal Headland", mesh, new[] { stone, sand }, Vector3.zero, Vector3.one);
            headland.SetActive(false);
            CoastalTransition transition = root.gameObject.AddComponent<CoastalTransition>();
            Set(transition, "headland", headland);
            SetArray(transition, "waterSurfaces", new[]
            {
                root.Find("Ocean").GetComponent<Renderer>(),
                root.Find("Rear Horizon/Ocean Apron").GetComponent<Renderer>(),
                root.Find("Forward Horizon/Ocean Apron").GetComponent<Renderer>()
            });
            SetArray(transition, "landSurfaces", new[]
            {
                root.Find("Landscape Ground").GetComponent<Renderer>(), root.Find("Shoreline").GetComponent<Renderer>(),
                root.Find("Rear Horizon/Ground Apron").GetComponent<Renderer>(), root.Find("Forward Horizon/Ground Apron").GetComponent<Renderer>()
            });
        }

        private static void ExtendCityGround()
        {
            const string path = "Assets/Prefab/CubeDash/TrackSegment.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform floor = root.transform.Find("City Surroundings/City Ground");
                if (floor != null) floor.localScale = new Vector3(LandscapeExtent * 2, 0.6f, 42);
                if (floor != null) AddHorizonAprons(floor.parent, floor.GetComponent<Renderer>().sharedMaterial, false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void AddHorizonAprons(Transform parent, Material floor, bool ocean)
        {
            GameObject[] ends = new GameObject[2];
            for (int edge = 0; edge < ends.Length; edge++)
            {
                string name = edge == 0 ? "Rear Horizon" : "Forward Horizon";
                Transform existing = parent.Find(name);
                GameObject end = existing != null ? existing.gameObject : new GameObject(name);
                end.transform.SetParent(parent, false);
                ends[edge] = end;
                // These contain only broad surfaces; no duplicate trees, buildings or gameplay.
                if (existing == null)
                {
                    float center = edge == 0 ? -LandscapeExtent * 0.5f : 42 + LandscapeExtent * 0.5f;
                    Mesh floorMesh = ocean ? SaveMesh(EnvironmentModelMeshes.BeachSeabed(), "Beach Seabed") : cube;
                    Part(end.transform, "Ground Apron", floorMesh, floor, new Vector3(0, -9.3f, center),
                        new Vector3(LandscapeExtent * 2, 0.6f, LandscapeExtent), false);
                    if (ocean)
                    {
                        Mesh surface = SaveMesh(EnvironmentModelMeshes.OceanSurface(true), "Ocean Apron Surface");
                        Part(end.transform, "Ocean Apron", surface, water,
                            new Vector3(0, -8.72f, edge == 0 ? -LandscapeExtent : 42),
                            new Vector3(1, 1, LandscapeExtent / RunnerRules.SegmentLength), false);
                    }
                }
                end.SetActive(false);
            }
            HorizonBackdrop backdrop = parent.GetComponent<HorizonBackdrop>();
            if (backdrop == null) backdrop = parent.gameObject.AddComponent<HorizonBackdrop>();
            Set(backdrop, "behind", ends[0]); Set(backdrop, "ahead", ends[1]);
        }

        private static void Limb(Geometry g, Vector3 start, Vector3 end, float diameter)
        {
            Vector3 direction = end - start;
            g.Add(branch, bark, start, new Vector3(diameter, direction.magnitude, diameter),
                Quaternion.FromToRotation(Vector3.up, direction.normalized));
        }

        private static void Boulder(Geometry g, Material material, Vector3 basePosition, Vector3 size, System.Random random)
            => g.Add(rock, material, basePosition + Vector3.up * size.y * 0.38f, size,
                Quaternion.Euler(Range(random, -12, 12), Range(random, 0, 360), Range(random, -9, 9)));

        private static void Bush(Geometry g, Vector3 p, System.Random random)
        {
            for (int cluster = 0; cluster < 3; cluster++)
                g.Add(crowns[cluster], cluster == 1 ? lightLeaf : leaf,
                    p + new Vector3((cluster - 1) * 1.15f, 1.4f + cluster * 0.18f, 0),
                    new Vector3(3.4f, 3, 3.1f), Range(random, 0, 180));
        }

        private static void Fern(Geometry g, Vector3 p, System.Random random)
        {
            float yaw = Range(random, 0, 360);
            for (int leafIndex = 0; leafIndex < 5; leafIndex++)
                g.Add(frond, leafIndex % 2 == 0 ? lightLeaf : leaf, p,
                    Vector3.one * 2.3f, Quaternion.Euler(0, yaw + leafIndex * 72, 12));
        }

        private static void Cactus(Geometry g, Vector3 p, float height)
        {
            g.Add(cactusStem, leaf, p, new Vector3(1.4f, height, 1.4f));
            for (int side = -1; side <= 1; side += 2)
            {
                g.Add(cactusArm, side == 1 ? lightLeaf : leaf, p + Vector3.up * height * (side == 1 ? 0.35f : 0.43f),
                    Vector3.one * height * (side == 1 ? 0.7f : 0.58f), side == 1 ? 0 : 180);
            }
        }

        private static void Palm(Geometry g, Vector3 p, float height, float yaw)
        {
            float diameter = height * 0.07f;
            Quaternion orientation = Quaternion.Euler(0, yaw, 0);
            g.Add(palmTrunk, bark, p, new Vector3(diameter, height, diameter), orientation);
            Vector3 crown = p + orientation * new Vector3(2.2f * diameter, height, 0.3f * diameter);
            g.Add(crowns[0], leaf, crown, new Vector3(1.4f, 0.7f, 1.4f), yaw);
            for (int leafIndex = 0; leafIndex < 9; leafIndex++)
            {
                Quaternion rotation = Quaternion.Euler(0, yaw + leafIndex * 40, leafIndex % 3 == 0 ? 12 : -4);
                float length = height * (leafIndex % 2 == 0 ? 0.40f : 0.35f);
                g.Add(frond, leafIndex % 3 == 0 ? lightLeaf : leaf, crown, Vector3.one * length, rotation);
            }
            for (int fruit = 0; fruit < 3; fruit++)
                g.Add(rock, wood, crown + new Vector3((fruit - 1) * 0.28f, -0.4f, 0), Vector3.one * 0.7f);
        }

        private static void UpdateTrackPrefab(GameObject[] scenery)
        {
            const string path = "Assets/Prefab/CubeDash/TrackSegment.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform city = root.transform.Find("City Surroundings");
                if (city == null) throw new InvalidOperationException("Existing city scenery was not found.");
                Transform ground = root.transform.Find("Bridge Rails/City Ground");
                if (ground != null) ground.SetParent(city, true);
                GameObject[] biomes = new GameObject[BiomeRules.Count];
                biomes[0] = city.gameObject;
                for (int i = 1; i < biomes.Length; i++)
                {
                    string name = ((EnvironmentBiome)i) + " Surroundings";
                    Transform existing = root.transform.Find(name);
                    if (existing == null)
                    {
                        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(scenery[i - 1], root.scene);
                        instance.transform.SetParent(root.transform, false);
                        instance.name = name;
                        existing = instance.transform;
                    }
                    biomes[i] = existing.gameObject;
                    biomes[i].SetActive(false);
                }
                SegmentEnvironment environment = root.GetComponent<SegmentEnvironment>();
                if (environment == null) environment = root.AddComponent<SegmentEnvironment>();
                SetArray(environment, "biomes", biomes);
                Set(root.GetComponent<TrackSegment>(), "environment", environment);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void SetAtmospheres(EnvironmentDirector director)
        {
            Material sky = RenderSettings.skybox;
            EnvironmentDirector.Atmosphere city = new EnvironmentDirector.Atmosphere
            {
                Horizon = RenderSettings.fogColor, Zenith = sky.GetColor("_ZenithColor"), Ground = sky.GetColor("_GroundColor"),
                Sun = new Color(1, 0.91f, 0.80f), SunIntensity = 1.85f,
                AmbientSky = RenderSettings.ambientSkyColor, AmbientEquator = RenderSettings.ambientEquatorColor,
                AmbientGround = RenderSettings.ambientGroundColor
            };
            var settings = new SerializedObject(director);
            var array = settings.FindProperty("atmospheres");
            array.arraySize = BiomeRules.Count;
            EnvironmentDirector.Atmosphere[] values =
            {
                city,
                Look(new Color(0.65f, 0.82f, 0.70f), new Color(0.31f, 0.62f, 0.67f), new Color(0.24f, 0.40f, 0.28f), new Color(1, 0.94f, 0.78f), 1.6f),
                Look(new Color(0.78f, 0.88f, 0.97f), new Color(0.35f, 0.58f, 0.82f), new Color(0.49f, 0.60f, 0.68f), new Color(0.94f, 0.97f, 1), 1.7f),
                Look(new Color(0.96f, 0.81f, 0.62f), new Color(0.52f, 0.73f, 0.87f), new Color(0.72f, 0.52f, 0.31f), new Color(1, 0.87f, 0.65f), 2.0f),
                Look(new Color(0.72f, 0.90f, 0.93f), new Color(0.27f, 0.65f, 0.87f), new Color(0.43f, 0.70f, 0.70f), new Color(1, 0.95f, 0.83f), 1.85f)
            };
            string[] colors = { "Horizon", "Zenith", "Ground", "Sun", "AmbientSky", "AmbientEquator", "AmbientGround" };
            for (int i = 0; i < values.Length; i++)
            {
                EnvironmentDirector.Atmosphere value = values[i];
                Color[] palette = { value.Horizon, value.Zenith, value.Ground, value.Sun, value.AmbientSky, value.AmbientEquator, value.AmbientGround };
                for (int c = 0; c < colors.Length; c++) array.GetArrayElementAtIndex(i).FindPropertyRelative(colors[c]).colorValue = palette[c];
                array.GetArrayElementAtIndex(i).FindPropertyRelative("SunIntensity").floatValue = value.SunIntensity;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static EnvironmentDirector.Atmosphere Look(Color horizon, Color zenith, Color ground, Color sun, float intensity)
        {
            return new EnvironmentDirector.Atmosphere
            {
                Horizon = horizon, Zenith = zenith, Ground = ground, Sun = sun, SunIntensity = intensity,
                AmbientSky = Color.Lerp(horizon, Color.white, 0.22f),
                AmbientEquator = Color.Lerp(horizon, ground, 0.3f), AmbientGround = Color.Lerp(ground, Color.white, 0.2f)
            };
        }

        private sealed class Geometry
        {
            private readonly Material[] palette;
            private readonly List<CombineInstance>[] groups;
            public Geometry(Material[] materials)
            {
                palette = materials;
                groups = new List<CombineInstance>[palette.Length];
                for (int i = 0; i < groups.Length; i++) groups[i] = new List<CombineInstance>();
            }
            public void Add(Mesh mesh, Material material, Vector3 position, Vector3 scale, float yaw = 0)
                => Add(mesh, material, position, scale, Quaternion.Euler(0, yaw, 0));
            public void Add(Mesh mesh, Material material, Vector3 position, Vector3 scale, Quaternion rotation)
            {
                groups[Array.IndexOf(palette, material)].Add(new CombineInstance
                { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, scale) });
            }
            public void AddSurface(Mesh mesh, int surface, Material material, Vector3 position, Vector3 scale, float yaw)
            {
                groups[Array.IndexOf(palette, material)].Add(new CombineInstance
                { mesh = mesh, subMeshIndex = surface, transform = Matrix4x4.TRS(position, Quaternion.Euler(0, yaw, 0), scale) });
            }
            public Mesh Bake()
            {
                Mesh result = new Mesh { indexFormat = IndexFormat.UInt32 };
                CombineInstance[] submeshes = new CombineInstance[groups.Length];
                for (int i = 0; i < groups.Length; i++)
                {
                    Mesh merged = new Mesh { indexFormat = IndexFormat.UInt32 };
                    if (groups[i].Count > 0) merged.CombineMeshes(groups[i].ToArray(), true, true);
                    else
                    {
                        // Retain consistent submesh/material indices even for an unused color.
                        merged.vertices = new[] { Vector3.zero };
                        merged.triangles = new int[0];
                    }
                    submeshes[i] = new CombineInstance { mesh = merged, transform = Matrix4x4.identity };
                }
                result.CombineMeshes(submeshes, false, false);
                result.RecalculateBounds();
                foreach (CombineInstance instance in submeshes) Object.DestroyImmediate(instance.mesh);
                return result;
            }
        }

        private static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = Meshes + "/" + name + ".asset";
            mesh.name = name;
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject temporary = GameObject.CreatePrimitive(type);
            Mesh mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temporary);
            return mesh;
        }

        private static GameObject Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, bool shadows = true)
            => Part(parent, name, mesh, new[] { material }, position, scale, shadows);

        private static GameObject Part(Transform parent, string name, Mesh mesh, Material[] materials, Vector3 position, Vector3 scale, bool shadows = true)
        {
            GameObject part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return part;
        }

        private static float Range(System.Random random, float minimum, float maximum) => Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
        private static void Set(Object target, string name, Object value)
        {
            var settings = new SerializedObject(target);
            settings.FindProperty(name).objectReferenceValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetArray<T>(Object target, string name, T[] values) where T : Object
        {
            var settings = new SerializedObject(target);
            var array = settings.FindProperty(name);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
