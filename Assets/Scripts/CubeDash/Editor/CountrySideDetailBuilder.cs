using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Additive, saved scenery upgrade. Does not replace the gameplay rig or native nature artwork.</summary>
    public static class CountrySideDetailBuilder
    {
        public const string DetailName = "Country Side - farmyards and natural verges";
        public const string DetailPath = CountrySideSceneBuilder.ArtPath + "Prefabs/Countryside Details.prefab";
        private const string Art = CountrySideSceneBuilder.ArtPath;
        private const string NaturePath = "Assets/Idyllic Fantasy Nature/Prefabs/";

        [MenuItem("Tools/Cube Dash/Upgrade Country Side Scenery")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            Scene scene = SceneManager.GetSceneByPath(CountrySideSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(CountrySideSceneBuilder.ScenePath);
            ApplyToScene(scene); AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Country Side.");
        }

        public static void ApplyToScene(Scene scene)
        {
            if (scene.path != CountrySideSceneBuilder.ScenePath) throw new InvalidOperationException("Only Country Side can be upgraded.");
            var scenery = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CountrySideRunnerScenery>(true)).Single();
            if (scenery.LandscapeTiles.Length > 0 && scenery.Tiles.All(tile => tile.Find(DetailName) != null)) return;
            PrepareArtwork();
            CreateDetailPrefab(scenery.Tiles[0]);
            foreach (Transform tile in scenery.Tiles)
                if (tile.Find(DetailName) == null)
                {
                    var detail = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DetailPath), scene);
                    detail.name = DetailName;
                    detail.transform.SetParent(tile, false);
                    Undo.RegisterCreatedObjectUndo(detail, "Detail countryside farms and vegetation");
                }
            Transform environment = scene.GetRootGameObjects().Single(root => root.name == CountrySideSceneBuilder.RootName).transform;
            if (scenery.LandscapeTiles.Length == 0) MakeTravellingLandscape(environment, scenery);
            RefreshCurveReferences(environment, scenery);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void RefreshCurveReferences(Transform environment, CountrySideRunnerScenery scenery)
        {
            // Preserve native wind/LOD materials. Only rigid, baked artwork is prepared by HighwayPresentation.
            var plants = environment.GetComponentsInChildren<Transform>(true).Where(item => PrefabUtility.IsAnyPrefabInstanceRoot(item.gameObject)
                && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item.gameObject).StartsWith(NaturePath)).ToArray();
            var nativeRenderers = plants.SelectMany(plant => plant.GetComponentsInChildren<Renderer>(true)).ToHashSet();
            var surfaces = environment.GetComponentsInChildren<MeshRenderer>(true).Where(renderer => renderer.gameObject.activeInHierarchy
                && !nativeRenderers.Contains(renderer) && renderer.transform.childCount == 0
                && renderer.sharedMaterials.All(material => material != null && (material.shader.name == "Universal Render Pipeline/Lit"
                    || material.shader.name == "CubeDash/Countryside Ground"))).ToArray();
            var data = new SerializedObject(scenery);
            Array(data.FindProperty("curvePlants"), plants); Array(data.FindProperty("curvedSurfaces"), surfaces);
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (Transform item in environment.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(item.gameObject, 0);
        }

        private static void MakeTravellingLandscape(Transform environment, CountrySideRunnerScenery scenery)
        {
            Transform landscape = environment.Find("Landscape - green valley and layered hills");
            Transform woodland = environment.Find("Trees - sakura avenue and green woodland");
            Transform first = Group("Travelling Landscape 01", landscape);
            foreach (Transform child in landscape.Cast<Transform>().ToArray())
            {
                if (child == first) continue;
                if (child.name == "Rolling valley ground") child.gameObject.SetActive(false);
                else child.SetParent(first, true);
            }
            woodland.SetParent(first, true);
            FitTravellingValley(first);
            var tiles = new Transform[3]; tiles[0] = first;
            for (int i = 1; i < tiles.Length; i++)
            {
                tiles[i] = CopyLandscape(first, landscape, i);
                tiles[i].localPosition = Vector3.forward * (568 * i);
            }
            var data = new SerializedObject(scenery); Array(data.FindProperty("landscapeTiles"), tiles);
            data.FindProperty("landscapeLength").floatValue = 568;
            data.FindProperty("landscapeFront").floatValue = 700;
            data.FindProperty("landscapeRecycleBehind").floatValue = -200;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void FitTravellingValley(Transform tile)
        {
            // The old center-of-horizon mound was safe only while stationary. Every travelling
            // hill must have its entire footprint outside the road, farms and irrigation channel.
            Transform[] hills = tile.Cast<Transform>().Where(child => child.name.Contains("Hills")).ToArray();
            foreach (Transform hill in hills)
            {
                Vector3 p = hill.localPosition;
                float radius = Mathf.Max(hill.localScale.x, hill.localScale.z);
                float side = p.x < 0 ? -1 : 1;
                p.x = side * Mathf.Max(Mathf.Abs(p.x), radius + 42);
                hill.localPosition = p;
            }
            foreach (Transform tree in tile.Find("Trees - sakura avenue and green woodland"))
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tree.gameObject);
                Bounds bounds = MainMenuAssetStoreTrees.BoundsOf(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                Vector3 p = tree.localPosition;
                float edge = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Mathf.Abs(p.x) - 44) / 100));
                float height = edge * (4 + 2 * Mathf.Sin((p.z + 36) / 284 * Mathf.PI * 2 + p.x * 0.017f)) - 0.035f;
                foreach (Transform hill in hills)
                {
                    Vector3 q = hill.InverseTransformPoint(tile.TransformPoint(new Vector3(p.x, 0, p.z)));
                    float radius = new Vector2(q.x, q.z).magnitude;
                    if (radius >= 1) continue;
                    float angle = Mathf.Atan2(q.z, q.x);
                    float ripple = 1 + Mathf.SmoothStep(0, 1, radius * 3) * (0.055f * Mathf.Sin(angle * 3) + 0.035f * Mathf.Sin(angle * 5 + radius * 3));
                    height = Mathf.Max(height, hill.localPosition.y + hill.localScale.y * Mathf.Pow(1 - radius * radius, 1.65f) * ripple);
                }
                p.y = height - bounds.min.y * tree.localScale.y - 0.08f;
                tree.localPosition = p;
            }
        }

        private static Transform CopyLandscape(Transform first, Transform parent, int index)
        {
            Transform tile = Group("Travelling Landscape " + (index + 1).ToString("00"), parent);
            foreach (Transform hill in first)
            {
                if (hill.name != "Trees - sakura avenue and green woodland") { Object.Instantiate(hill.gameObject, tile); continue; }
                Transform forest = Group(hill.name, tile);
                foreach (Transform tree in hill)
                {
                    string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tree.gameObject);
                    var copy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent.gameObject.scene);
                    copy.transform.SetParent(forest, false);
                    copy.transform.SetLocalPositionAndRotation(tree.localPosition, tree.localRotation); copy.transform.localScale = tree.localScale;
                    Transform trigger = copy.transform.Find("LeafTrigger"); if (trigger != null) Object.DestroyImmediate(trigger.gameObject);
                    foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                }
            }
            return tile;
        }

        private static void PrepareArtwork()
        {
            // Update meshes in place so every linked house keeps its identity, position and prefab connection.
            ReplaceMesh("Minka Farmhouse", CountrySideMeshes.Farmhouse(false));
            ReplaceMesh("Kura Storehouse", CountrySideMeshes.Farmhouse(true));
            Surface("Warm Plaster", 0, new Color(0.81f, 0.75f, 0.61f));
            Surface("Dark Timber", 1, new Color(0.21f, 0.17f, 0.13f));
            Surface("Cedar Timber", 1, new Color(0.49f, 0.34f, 0.20f));
            Surface("Kawara Roof Tiles", 2, new Color(0.24f, 0.30f, 0.33f));
            Surface("Weathered Stone", 3, new Color(0.45f, 0.47f, 0.40f));
            Surface("Farm Path", 4, new Color(0.56f, 0.46f, 0.32f));
            Surface("Field Soil", 4, new Color(0.32f, 0.32f, 0.19f));
        }

        private static void ReplaceMesh(string name, Mesh replacement)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "Meshes/" + name + ".asset");
            if (mesh == null) AssetDatabase.CreateAsset(replacement, Art + "Meshes/" + name + ".asset");
            else
            {
                // Native mesh buffers must be refreshed as well as serialized submesh ranges.
                mesh.Clear(); mesh.indexFormat = replacement.indexFormat; mesh.name = replacement.name;
                mesh.vertices = replacement.vertices; mesh.normals = replacement.normals;
                mesh.uv = replacement.uv; mesh.colors = replacement.colors; mesh.subMeshCount = replacement.subMeshCount;
                for (int i = 0; i < mesh.subMeshCount; i++) mesh.SetTriangles(replacement.GetTriangles(i), i);
                mesh.RecalculateBounds(); mesh.UploadMeshData(false);
                EditorUtility.SetDirty(mesh); Object.DestroyImmediate(replacement);
            }
        }

        private static void Surface(string name, int type, Color tint)
        {
            const int size = 128;
            string path = Art + "Materials/" + name + " Detail.png";
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float fine = Mathf.PerlinNoise(x * 0.42f + 11, y * 0.42f + 7);
                float blotch = Mathf.PerlinNoise(x * 0.035f + 3, y * 0.035f + 9);
                float value = 0.80f + fine * 0.18f + blotch * 0.18f;
                if (type == 1)
                {
                    float grain = Mathf.Sin(x * 0.9f + Mathf.Sin(y * 0.055f) * 2 + blotch * 4);
                    value = 0.78f + grain * 0.08f + blotch * 0.24f + fine * 0.10f;
                    if (x % 32 < 1) value *= 0.6f;
                }
                if (type == 2) value = 0.85f + blotch * 0.22f + fine * 0.1f;
                if (type == 3 && (y % 32 < 2 || (x + ((y / 32) % 2) * 32) % 64 < 2)) value *= 0.73f;
                pixels[y * size + x] = new Color(value, value, value, 1);
            }
            texture.SetPixels(pixels); texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true; importer.anisoLevel = 4; importer.SaveAndReimport();
            Material material = M(name); material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            material.SetColor("_BaseColor", tint); material.SetFloat("_Smoothness", type == 2 ? 0.25f : 0.12f);
            material.SetTextureScale("_BaseMap", type == 1 ? new Vector2(2, 1) : new Vector2(3, 3));
            EditorUtility.SetDirty(material);
        }

        private static void CreateDetailPrefab(Transform sourceTile)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DetailPath) != null) return;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(DetailName); SceneManager.MoveGameObjectToScene(root, preview);
                Part("Textured rolling turf", root.transform, SaveMesh("Travelling Turf", Turf()), new[] { M("Valley Grass") });
                Mesh yard = SaveMesh("Farmyard Details", Farmyard());
                foreach (Transform house in sourceTile.Find("Village - original Japanese minka and kura"))
                {
                    var part = Part("Farmyard - " + house.name, root.transform, yard, Palette());
                    part.transform.SetLocalPositionAndRotation(house.localPosition, house.localRotation);
                    part.transform.localScale = house.localScale;
                }
                Part("Stone-lined irrigation and broken field edges", root.transform, SaveMesh("Country Ground Details", GroundDetails()),
                    new[] { M("Weathered Stone"), M("Cedar Timber"), M("Farm Path"), M("Paddy Embankment") });
                PlantVerge(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, DetailPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static Mesh Turf()
        {
            // Identical end rows and periodic height ensure recycled ground has no cracks.
            const int columns = 64, rows = 72;
            var vertices = new Vector3[(columns + 1) * (rows + 1)]; var colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length]; var triangles = new int[columns * rows * 6];
            for (int r = 0; r <= rows; r++) for (int c = 0; c <= columns; c++)
            {
                float x = Mathf.Lerp(-190, 190, c / (float)columns), z = Mathf.Lerp(-36, 248, r / (float)rows);
                float edge = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Mathf.Abs(x) - 44) / 100));
                float phase = (z + 36) / 284 * Mathf.PI * 2;
                int i = r * (columns + 1) + c;
                vertices[i] = new Vector3(x, edge * (4 + 2 * Mathf.Sin(phase + x * 0.017f)) - 0.035f, z);
                float patch = Mathf.PerlinNoise(x * 0.055f + 12, Mathf.Sin(phase) * 1.3f + 8);
                colors[i] = Color.Lerp(new Color(0.22f, 0.35f, 0.12f), new Color(0.47f, 0.55f, 0.25f), patch);
                uv[i] = new Vector2(x, z);
            }
            int t = 0;
            for (int r = 0; r < rows; r++) for (int c = 0; c < columns; c++)
            {
                int a = r * (columns + 1) + c, b = a + columns + 1;
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
                triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
            }
            var mesh = new Mesh { name = "Periodic countryside turf", vertices = vertices, colors = colors, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        private static Mesh Farmyard()
        {
            var g = new CountrySideMeshes.Geometry(7);
            // Stepping-stone courtyard, split stone boundary, vegetable beds and a timber work shed.
            g.Box(new Vector3(0, -0.012f, -4.6f), new Vector3(11.8f, 0.05f, 4.2f), 5);
            for (int i = 0; i < 7; i++)
                g.Box(new Vector3((i % 2) * 0.16f, 0.045f, -4.4f - i * 0.57f), new Vector3(1.15f, 0.09f, 0.43f), 5, Quaternion.Euler(0, i % 3 * 5 - 5, 0));
            foreach (float side in new[] { -1f, 1f })
                for (int block = 0; block < 14; block++)
                {
                    float z = -6.5f + block * 0.72f;
                    g.Box(new Vector3(side * 6.1f, 0.23f, z), new Vector3(0.38f, 0.42f, 0.67f), 5);
                    g.Box(new Vector3(side * 6.1f, 0.49f, z), new Vector3(0.48f, 0.11f, 0.70f), 5);
                }
            // Open-sided farm shed with roof, slats, stacked firewood and diagonal braces.
            foreach (float x in new[] { -5.3f, -2.7f }) foreach (float z in new[] { 3.4f, 6.0f })
                g.Box(new Vector3(x, 1.2f, z), new Vector3(0.16f, 2.4f, 0.16f), 1);
            for (int i = 0; i < 12; i++)
                g.Box(new Vector3(-5.3f + i * 0.235f, 2.54f - i * 0.025f, 4.7f), new Vector3(0.26f, 0.13f, 3.4f), 3);
            for (int i = 0; i < 14; i++)
                g.Box(new Vector3(-5.3f + i * 0.2f, 1.2f, 6), new Vector3(0.15f, 2.35f, 0.09f), 2);
            g.Beam(new Vector3(-5.3f, 0.6f, 3.4f), new Vector3(-5.3f, 2.2f, 4.7f), 0.13f, 2);
            for (int row = 0; row < 4; row++) for (int log = 0; log < 5 - row / 2; log++)
                g.Tube(new[] { new Vector3(-4.8f + log * 0.37f + (row % 2) * 0.1f, 0.22f + row * 0.25f, 4.6f),
                    new Vector3(-4.8f + log * 0.37f + (row % 2) * 0.1f, 0.22f + row * 0.25f, 5.8f) }, 0.13f, 2, 6);
            // Small roofed stone well, safely within the farmyard.
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI / 6;
                g.Box(new Vector3(-4.3f + Mathf.Cos(angle) * 0.65f, 0.48f, -4.7f + Mathf.Sin(angle) * 0.65f),
                    new Vector3(0.33f, 0.9f, 0.34f), 5, Quaternion.Euler(0, -i * 30, 0));
            }
            foreach (float x in new[] { -5.15f, -3.45f }) g.Box(new Vector3(x, 1.2f, -4.7f), new Vector3(0.13f, 2.4f, 0.13f), 2);
            foreach (float side in new[] { -1f, 1f })
                g.Quad(new Vector3(-5.5f, 2.35f, -4.7f + side), new Vector3(-3.1f, 2.35f, -4.7f + side),
                    new Vector3(-3.1f, 2.85f, -4.7f), new Vector3(-5.5f, 2.85f, -4.7f), 3, Vector3.up);
            g.Beam(new Vector3(-5.15f, 1.9f, -4.7f), new Vector3(-3.45f, 1.9f, -4.7f), 0.09f, 1);
            for (int bed = 0; bed < 3; bed++)
            {
                float z = -3.5f + bed * 1.4f;
                g.Box(new Vector3(4.9f, 0.09f, z), new Vector3(1.2f, 0.16f, 1.05f), 2);
                g.Box(new Vector3(4.9f, 0.19f, z), new Vector3(1.06f, 0.05f, 0.91f), 1);
                for (int plant = 0; plant < 6; plant++)
                {
                    Vector3 p = new Vector3(4.55f + plant % 2 * 0.65f, 0.22f, z - 0.3f + plant / 2 * 0.3f);
                    foreach (float side in new[] { -1f, 1f })
                        g.Quad(p + new Vector3(-0.15f, 0, 0), p + new Vector3(0, 0.23f, side * 0.13f),
                            p + new Vector3(0.15f, 0, 0), p + new Vector3(0, 0.07f, -side * 0.13f), 6, Vector3.up);
                }
            }
            return g.Finish("Farmyard - well, stonework, shed, firewood and kitchen garden");
        }

        private static Mesh GroundDetails()
        {
            var g = new CountrySideMeshes.Geometry(4); var random = new System.Random(743);
            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i < 142; i++)
                {
                    float z = -35 + i * 2;
                    // Uneven, overlapping shoulder patches soften the ruler-straight grass edge.
                    float width = Range(random, 0.6f, 1.6f);
                    g.Box(new Vector3(side * (4.45f + width * 0.5f), -0.003f, z), new Vector3(width, 0.025f, 2.3f), 2);
                    if (i % 3 == 0)
                        g.Box(new Vector3(side * Range(random, 5.4f, 6.2f), 0.07f, z), new Vector3(Range(random, 0.2f, 0.45f), 0.16f, Range(random, 0.3f, 0.65f)), 0,
                            Quaternion.Euler(0, Range(random, 0, 180), 0));
                }
            // Masonry courses articulate the existing canal rather than adding another water plane.
            for (int i = 0; i < 131; i++) foreach (float x in new[] { 6.43f, 9.17f })
            {
                float z = -22 + i * 2;
                g.Box(new Vector3(x, 0.29f, z), new Vector3(0.42f, 0.12f, 1.92f), 0);
                g.Box(new Vector3(x, 0.11f, z), new Vector3(0.33f, 0.23f, 1.86f), 0);
            }
            return g.Finish("Rural shoulders, stones and capped irrigation masonry");
        }

        private static void PlantVerge(Transform parent)
        {
            Transform verge = Group("Layered wild verges and farm gardens", parent);
            var random = new System.Random(1784);
            for (int i = 0; i < 220; i++)
            {
                float side = i % 2 == 0 ? -1 : 1, z = Range(random, -32, 244);
                float x = side < 0 ? -Range(random, 6.0f, 8.4f) : Range(random, 9.6f, 11.5f);
                Plant("Grass_0" + (1 + i % 3), verge, new Vector3(x, 0, z), Range(random, 0.28f, 0.68f), Range(random, 0, 360));
                if (i % 5 == 0) Plant(i % 2 == 0 ? "Flower_White" : "Flower_Yellow", verge,
                    new Vector3(x + 0.22f, 0, z + 0.4f), Range(random, 0.22f, 0.42f), Range(random, 0, 360));
                if (i % 11 == 0) Plant("Bush_0" + (1 + i % 3) + "_01", verge,
                    new Vector3(side * Range(random, 12, 14), 0, z), Range(random, 0.65f, 1.2f), Range(random, 0, 360));
            }
            for (int i = 0; i < 28; i++)
                Plant("Reeds_01", verge, new Vector3(9.5f, 0, -22 + i * 9.4f), Range(random, 0.55f, 1), i * 137.5f);
            foreach (var house in parent.Cast<Transform>().Where(child => child.name.StartsWith("Farmyard -")).ToArray())
            {
                Transform garden = Group("Garden planting - " + house.name, verge);
                garden.SetLocalPositionAndRotation(house.localPosition, house.localRotation); garden.localScale = house.localScale;
                foreach (Vector3 p in new[] { new Vector3(-5.3f, 0, -6.4f), new Vector3(5.3f, 0, -5.9f), new Vector3(5.4f, 0, 2.8f) })
                    Plant("Bush_02_01", garden, p, Range(random, 0.7f, 1.1f), Range(random, 0, 360));
            }
        }

        private static void Plant(string name, Transform parent, Vector3 position, float height, float yaw)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(NaturePath + name + ".prefab");
            Bounds bounds = MainMenuAssetStoreTrees.BoundsOf(source); float scale = height / bounds.size.y;
            var plant = (GameObject)PrefabUtility.InstantiatePrefab(source, parent.gameObject.scene);
            plant.transform.SetParent(parent, false); plant.transform.localScale = Vector3.one * scale;
            plant.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            plant.transform.localPosition = position - Vector3.up * bounds.min.y * scale;
            Transform trigger = plant.transform.Find("LeafTrigger"); if (trigger != null) Object.DestroyImmediate(trigger.gameObject);
            foreach (Collider collider in plant.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (Transform child in plant.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
        }

        private static Material M(string name) => AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/" + name + ".mat");
        private static Material[] Palette() => new[] { M("Warm Plaster"), M("Dark Timber"), M("Cedar Timber"), M("Kawara Roof Tiles"), M("Shoji Paper"), M("Weathered Stone"), M("Rice Shade") };
        private static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        private static Transform Group(string name, Transform parent)
        {
            var root = new GameObject(name); root.transform.SetParent(parent, false); return root.transform;
        }
        private static MeshRenderer Part(string name, Transform parent, Mesh mesh, Material[] materials)
        {
            Transform item = Group(name, parent); item.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials; return renderer;
        }
        private static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path = Art + "Meshes/" + name + ".asset";
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved != null) { Object.DestroyImmediate(mesh); return saved; }
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static void Array(SerializedProperty property, Object[] objects)
        {
            property.arraySize = objects.Length;
            for (int i = 0; i < objects.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
        }
    }
}
