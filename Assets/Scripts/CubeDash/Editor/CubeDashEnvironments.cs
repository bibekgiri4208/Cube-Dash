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
        private static Mesh cube, cylinder, cone, rock, dune;
        private static Material bark, leaf, lightLeaf, stone, snow, sand, redStone, grass, water, foam, wood;

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
            cube = PrimitiveMesh(PrimitiveType.Cube);
            cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
            cone = MakeCone(); rock = MakeRock(); dune = MakeDune();
            try
            {
                MakeMaterials();
                GameObject[] scenery = new GameObject[4];
                for (int i = 0; i < scenery.Length; i++) scenery[i] = BuildScenery((EnvironmentBiome)(i + 1));
                UpdateTrackPrefab(scenery);

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
            finally { Object.DestroyImmediate(cone); Object.DestroyImmediate(rock); Object.DestroyImmediate(dune); }
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
            water = Material("Lagoon Water", new Color(0.04f, 0.43f, 0.55f), "CubeDash/Biome Water");
            foam = Material("Shore Foam", new Color(0.74f, 0.90f, 0.86f));
            wood = Material("Beach Timber", new Color(0.51f, 0.36f, 0.23f));
        }

        private static Material Material(string name, Color color, string shader = "Universal Render Pipeline/Lit")
        {
            string path = Materials + "/" + name + ".mat";
            Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                result = new Material(Shader.Find(shader)) { name = name };
                AssetDatabase.CreateAsset(result, path);
            }
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
            Material[] palette = { bark, leaf, lightLeaf, stone, snow, sand, redStone, grass, foam, wood };
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
                Material floor = biome == EnvironmentBiome.Jungle ? grass : biome == EnvironmentBiome.Mountains ? stone : sand;
                Part(root.transform, "Landscape Ground", cube, floor, new Vector3(0, -9.3f, 21), new Vector3(124, 0.6f, 42));
                if (biome == EnvironmentBiome.Beach)
                {
                    Part(root.transform, "Ocean", cube, water, new Vector3(40, -8.92f, 21), new Vector3(66, 0.08f, 42), false);
                    Part(root.transform, "Shoreline", cube, foam, new Vector3(7.4f, -8.86f, 21), new Vector3(1.3f, 0.05f, 42), false);
                }
                if (biome == EnvironmentBiome.Jungle)
                    Part(root.transform, "River", cube, water, new Vector3(-29, -8.9f, 21), new Vector3(5, 0.1f, 42), false);
                GameObject details = Part(root.transform, "Landscape Details", layouts[0], palette, Vector3.zero, Vector3.one);
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
                    Tree(g, new Vector3(side * Range(random, 12, 19), -9, z), Range(random, 12, 19), false);
                    Tree(g, new Vector3(side * Range(random, 34, 49), -9, z), Range(random, 19, 27), false);
                    g.Add(rock, stone, new Vector3(side * Range(random, 10, 23), -7, z + 2), new Vector3(4, 3, 4), Range(random, 0, 180));
                    for (int bush = 0; bush < 2; bush++)
                        g.Add(rock, lightLeaf, new Vector3(side * Range(random, 8, 23), -7, z + bush * 3), new Vector3(5, 4, 5));
                }
                // A distant continuous ridge frames the canopy without intruding into the lanes.
                g.Add(dune, grass, new Vector3(side * 46, -9, 21), new Vector3(28, 16, 46));
            }
        }

        private static void Mountains(Geometry g, System.Random random)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int peak = 0; peak < 3; peak++)
                {
                    float height = Range(random, 32, 52), width = Range(random, 20, 29);
                    Vector3 basePosition = new Vector3(side * Range(random, 35, 45), -9, 6 + peak * 14);
                    float angle = Range(random, 0, 70);
                    g.Add(cone, stone, basePosition, new Vector3(width, height, 22), angle);
                    g.Add(cone, snow, basePosition + Vector3.up * height * 0.68f,
                        new Vector3(width * 0.32f + 0.1f, height * 0.32f + 0.1f, 7.15f), angle);
                }
                for (int row = 0; row < 4; row++)
                {
                    Tree(g, new Vector3(side * Range(random, 12, 23), -9, 4 + row * 10), Range(random, 9, 15), true);
                    g.Add(rock, stone, new Vector3(side * Range(random, 13, 24), -5, 8 + row * 9), new Vector3(7, 9, 6), Range(random, 0, 180));
                }
            }
        }

        private static void Desert(Geometry g, System.Random random)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 3; row++)
                {
                    float z = 5 + row * 14;
                    g.Add(dune, sand, new Vector3(side * Range(random, 24, 34), -9, z), new Vector3(28, Range(random, 7, 13), 25), Range(random, -15, 15));
                    Vector3 mesa = new Vector3(side * Range(random, 42, 51), -9, z);
                    float height = Range(random, 16, 26);
                    g.Add(cube, redStone, mesa + Vector3.up * height * 0.5f, new Vector3(12, height, 11), 8);
                    g.Add(cube, sand, mesa + Vector3.up * (height + 0.3f), new Vector3(14, 1.4f, 13), 8);
                    Cactus(g, new Vector3(side * Range(random, 12, 20), -9, z), Range(random, 5, 9));
                    g.Add(rock, redStone, new Vector3(side * Range(random, 11, 23), -7.5f, z + 5), new Vector3(4, 3, 3));
                }
            }
        }

        private static void Beach(Geometry g, System.Random random)
        {
            for (int row = 0; row < 4; row++)
            {
                float z = 4 + row * 10;
                Palm(g, new Vector3(-Range(random, 13, 24), -9, z), Range(random, 13, 18), Range(random, 0, 180));
                g.Add(rock, stone, new Vector3(Range(random, 12, 20), -7, z), new Vector3(6, 5, 5));
                g.Add(rock, lightLeaf, new Vector3(-Range(random, 10, 28), -7.7f, z + 3), new Vector3(4, 3, 4));
            }
            // Stilt huts and a wooden jetty give the coastline a recognizable silhouette.
            float hutZ = Range(random, 10, 28);
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = new Vector3(-34 - i * 16, -5.5f, hutZ);
                g.Add(cube, wood, p, new Vector3(7, 5, 6));
                g.Add(cone, sand, p + Vector3.up * 2.5f, new Vector3(11, 4.5f, 10), 45);
                for (int leg = -1; leg <= 1; leg += 2)
                    g.Add(cylinder, bark, p + new Vector3(leg * 2.7f, -3, 0), new Vector3(0.7f, 2, 0.7f));
            }
            g.Add(cube, wood, new Vector3(20, -6.5f, hutZ), new Vector3(18, 0.4f, 3));
            for (int post = 0; post < 4; post++)
                g.Add(cylinder, wood, new Vector3(13 + post * 4.5f, -7.4f, hutZ), new Vector3(0.5f, 2.5f, 0.5f));
            g.Add(dune, sand, new Vector3(-43, -9, 21), new Vector3(36, 7, 44));
        }

        private static void Tree(Geometry g, Vector3 p, float height, bool pine)
        {
            g.Add(cylinder, bark, p + Vector3.up * height * 0.36f, new Vector3(1.2f, height * 0.36f, 1.2f));
            if (pine)
            {
                for (int tier = 0; tier < 3; tier++)
                    g.Add(cone, tier == 1 ? lightLeaf : leaf, p + Vector3.up * height * (0.22f + tier * 0.2f),
                        new Vector3(height * (0.6f - tier * 0.12f), height * 0.38f, height * (0.6f - tier * 0.12f)), tier * 35);
            }
            else
            {
                g.Add(rock, leaf, p + Vector3.up * height * 0.78f, new Vector3(height * 0.72f, height * 0.6f, height * 0.66f));
                g.Add(rock, lightLeaf, p + new Vector3(height * 0.18f, height * 0.86f, 0), new Vector3(height * 0.52f, height * 0.38f, height * 0.52f), 25);
            }
        }

        private static void Cactus(Geometry g, Vector3 p, float height)
        {
            g.Add(cylinder, leaf, p + Vector3.up * height * 0.5f, new Vector3(1.3f, height * 0.5f, 1.3f));
            for (int side = -1; side <= 1; side += 2)
            {
                g.Add(cube, leaf, p + new Vector3(side * 1.5f, height * 0.5f, 0), new Vector3(3, 1.1f, 1.1f));
                g.Add(cylinder, lightLeaf, p + new Vector3(side * 2.6f, height * 0.64f, 0), new Vector3(1.1f, height * 0.16f, 1.1f));
            }
        }

        private static void Palm(Geometry g, Vector3 p, float height, float yaw)
        {
            g.Add(cylinder, bark, p + Vector3.up * height * 0.5f, new Vector3(1, height * 0.5f, 1), yaw);
            for (int frond = 0; frond < 7; frond++)
            {
                Quaternion rotation = Quaternion.Euler(0, yaw + frond * 360f / 7, -14);
                Vector3 center = p + Vector3.up * height + rotation * new Vector3(3, -0.2f, 0);
                g.Add(rock, frond % 2 == 0 ? leaf : lightLeaf, center, new Vector3(9, 0.7f, 2), rotation);
            }
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

        private static Mesh MakeCone()
        {
            List<Vector3> vertices = new List<Vector3>();
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4, b = (i + 1) * Mathf.PI / 4;
                Vector3 first = new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f);
                Vector3 second = new Vector3(Mathf.Cos(b) * 0.5f, 0, Mathf.Sin(b) * 0.5f);
                vertices.Add(first); vertices.Add(Vector3.up); vertices.Add(second);
                vertices.Add(Vector3.zero); vertices.Add(first); vertices.Add(second);
            }
            return FlatMesh(vertices);
        }

        private static Mesh MakeRock()
        {
            Vector3[] ring = { new Vector3(0.5f, 0, 0), new Vector3(0, 0, 0.5f), new Vector3(-0.5f, 0, 0), new Vector3(0, 0, -0.5f) };
            List<Vector3> vertices = new List<Vector3>();
            for (int i = 0; i < 4; i++)
            {
                vertices.Add(ring[i]); vertices.Add(Vector3.up * 0.5f); vertices.Add(ring[(i + 1) % 4]);
                vertices.Add(ring[i]); vertices.Add(ring[(i + 1) % 4]); vertices.Add(Vector3.down * 0.5f);
            }
            return FlatMesh(vertices);
        }

        private static Mesh MakeDune()
        {
            List<Vector3> vertices = new List<Vector3>();
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6, b = (i + 1) * Mathf.PI / 6;
                Vector3 first = new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f);
                Vector3 second = new Vector3(Mathf.Cos(b) * 0.5f, 0, Mathf.Sin(b) * 0.5f);
                Vector3 innerA = first * 0.52f + Vector3.up * 0.75f;
                Vector3 innerB = second * 0.52f + Vector3.up * 0.75f;
                vertices.Add(first); vertices.Add(innerA); vertices.Add(second);
                vertices.Add(second); vertices.Add(innerA); vertices.Add(innerB);
                vertices.Add(innerA); vertices.Add(new Vector3(-0.07f, 1, 0)); vertices.Add(innerB);
            }
            return FlatMesh(vertices);
        }

        private static Mesh FlatMesh(List<Vector3> vertices)
        {
            Mesh mesh = new Mesh();
            mesh.SetVertices(vertices);
            int[] triangles = new int[vertices.Count];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
            mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
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
