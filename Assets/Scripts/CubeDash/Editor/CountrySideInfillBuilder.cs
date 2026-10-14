using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Fills the whole route with saved hamlets, orchards and layered planting, not runtime spawns.</summary>
    public static class CountrySideInfillBuilder
    {
        public const string RootName = "Country Side - inhabited valley";
        public const string PrefabPath = CountrySideSceneBuilder.ArtPath + "Prefabs/Inhabited Countryside.prefab";
        private const string Art = CountrySideSceneBuilder.ArtPath;
        private const string Nature = "Assets/Idyllic Fantasy Nature/Prefabs/";
        private static readonly Vector4[] Homes = {
            new Vector4(-16, -26, 0.82f, -55), new Vector4(19, -15, 0.78f, 60),
            new Vector4(-38, 30, 0.85f, -40), new Vector4(38, 24, 0.74f, 50),
            new Vector4(38, 51, 0.86f, 65), new Vector4(-39, 78, 0.78f, -65),
            new Vector4(-39, 111, 0.84f, -45), new Vector4(39, 119, 0.80f, 55),
            new Vector4(-17, 216, 0.91f, -55), new Vector4(18, 231, 0.83f, 55),
            new Vector4(-33, 235, 0.80f, -35), new Vector4(36, 231, 0.72f, 70) };

        [MenuItem("Tools/Cube Dash/Fill Country Side Villages and Vegetation")]
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
            if (scene.path != CountrySideSceneBuilder.ScenePath) throw new InvalidOperationException("Only Country Side can be filled.");
            var scenery = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CountrySideRunnerScenery>(true)).Single();
            if (scenery.Tiles.Length != 2 || scenery.LandscapeTiles.Length != 3) throw new InvalidOperationException("Upgrade the travelling countryside first.");
            if (scenery.Tiles.All(tile => tile.Find(RootName) != null)) return;
            CreatePrefab();
            foreach (Transform tile in scenery.Tiles)
            {
                if (tile.Find(RootName) != null) continue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                instance.name = RootName; instance.transform.SetParent(tile, false);
                Undo.RegisterCreatedObjectUndo(instance, "Populate countryside villages and vegetation");
            }
            Transform environment = scene.GetRootGameObjects().Single(root => root.name == CountrySideSceneBuilder.RootName).transform;
            CountrySideDetailBuilder.RefreshCurveReferences(environment, scenery);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void CreatePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                Transform root = new GameObject(RootName).transform; SceneManager.MoveGameObjectToScene(root.gameObject, preview);
                var occupied = ReservedLand();
                Hamlets(root, occupied);
                CropPlots(root, occupied);
                Orchards(root, occupied);
                WildEdges(root, occupied);
                Hedges(root);
                Part("Continuous meadow undergrowth", root, "Valley Undergrowth", Grass(occupied), new[] { M("Valley Grass") });
                Part("Roadside poles signs and farm stalls", root, "Valley Village Props", VillageProps(),
                    new[] { M("Cedar Timber"), M("Dark Timber"), M("Weathered Stone"), M("Torii Vermilion"), M("Shoji Paper"), M("Rice Tips") });
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(item.gameObject, 0);
                if (PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath) == null) throw new InvalidOperationException("Could not save the inhabited countryside.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static void Hamlets(Transform root, List<Rect> occupied)
        {
            Transform hamlets = Group("Hamlets - roadside homes and outbuildings", root);
            Mesh yard = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "Meshes/Farmyard Details.asset");
            Material[] palette = { M("Warm Plaster"), M("Dark Timber"), M("Cedar Timber"), M("Kawara Roof Tiles"), M("Shoji Paper"), M("Weathered Stone"), M("Rice Shade") };
            for (int i = 0; i < Homes.Length; i++)
            {
                Vector4 home = Homes[i];
                Transform plot = Group("Homestead " + (i + 1).ToString("00"), hamlets);
                plot.SetLocalPositionAndRotation(new Vector3(home.x, 0, home.y), Quaternion.Euler(0, home.w, 0));
                plot.localScale = Vector3.one * home.z;
                Model(i % 4 == 2 ? "Kura Storehouse" : "Minka Farmhouse", plot, Vector3.zero, 1, 0);
                // A shared batched farmyard adds scale and context to every house, not isolated buildings.
                var item = new GameObject("Stone courtyard well and work shed", typeof(MeshFilter), typeof(MeshRenderer));
                item.transform.SetParent(plot, false); item.GetComponent<MeshFilter>().sharedMesh = yard;
                item.GetComponent<MeshRenderer>().sharedMaterials = palette;
                Plant("Bush_03_01", plot, new Vector3(-5.4f, 0, -5.8f), 1.15f, i * 43);
                Plant("FlowerMeadow_White", plot, new Vector3(4.5f, 0, -5.5f), 0.35f, i * 71);
                occupied.Add(Box(home.x, home.y, home.z * 10, home.z * 10));
            }
        }

        private static List<Rect> ReservedLand()
        {
            // Existing fields, yards, the shrine and flooded paddies are left intact.
            return new List<Rect> {
                Box(-20,-4,12,18), Box(20,12,12,18), Box(-22,65,13,19), Box(22,94,13,20), Box(-25,184,15,20), Box(25,205,15,20),
                Box(-18,24,12,12), Box(20,45,11,11), Box(31,67,7,7), Box(-22,118,12,12), Box(-32,145,7,7), Box(29,171,11,11),
                Box(-18,93,12,13), Box(-32,144,15,15), Box(29.5f,143,15.5f,17) };
        }

        private static void CropPlots(Transform root, List<Rect> occupied)
        {
            Transform plots = Group("Kitchen gardens and cultivated field strips", root);
            var ground = new CountrySideMeshes.Geometry(2); var crop = new CountrySideMeshes.Geometry(1);
            var random = new System.Random(8124); int count = 0;
            // Place useful-sized plots in the actual gaps between authored flower fields and
            // farmyards, rather than rejecting every candidate in a coarse occupancy grid.
            var sites = new[] { new Vector4(17,-30,7.2f,8), new Vector4(29,-30,7.2f,8), new Vector4(-15,41,7.2f,8),
                new Vector4(16,62,7.2f,8), new Vector4(15,121,6,8), new Vector4(-14.8f,160,3.6f,5),
                new Vector4(13,183,5,3), new Vector4(-8,161,3,6) };
            foreach (Vector4 site in sites)
                {
                    float x = site.x, z = site.y, width = site.z, depth = site.w;
                    Rect footprint = Box(x, z, width * 0.5f + 0.2f, depth * 0.5f + 0.2f);
                    occupied.Add(footprint); count++;
                    Transform marker = Group("Vegetable plot " + count.ToString("00"), plots); marker.localPosition = new Vector3(x, 0, z);
                    ground.Box(new Vector3(x, -0.005f, z), new Vector3(width, 0.04f, depth), 0);
                    foreach (float side in new[] { -1f, 1f })
                        ground.Box(new Vector3(x + side * width * 0.5f, 0.09f, z), new Vector3(0.14f, 0.18f, depth), 1);
                    int rows = Mathf.FloorToInt(width / 0.95f), plants = Mathf.FloorToInt(depth / 0.6f);
                    for (int row = 0; row < rows; row++)
                    {
                        float rowX = x + (row - (rows - 1) * 0.5f) * 0.95f;
                        ground.Box(new Vector3(rowX, 0.06f, z), new Vector3(0.55f, 0.10f, depth - 0.4f), 0);
                        for (int plant = 0; plant < plants; plant++)
                        {
                            Vector3 p = new Vector3(rowX + Range(random, -0.1f, 0.1f), 0.12f, z + (plant - (plants - 1) * 0.5f) * 0.6f);
                            float height = Range(random, 0.3f, 0.6f);
                            Color tint = Color.Lerp(new Color(0.28f, 0.46f, 0.10f), new Color(0.59f, 0.68f, 0.27f), Range(random, 0, 1));
                            for (int leaf = 0; leaf < 3; leaf++)
                            {
                                float angle = leaf * Mathf.PI / 3;
                                Vector3 axis = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.22f;
                                crop.Quad(p - axis, p + Vector3.up * height - axis * 0.35f, p + Vector3.up * height + axis * 0.35f, p + axis, 0, Vector3.up, tint);
                                crop.Quad(p + axis, p + Vector3.up * height + axis * 0.35f, p + Vector3.up * height - axis * 0.35f, p - axis, 0, Vector3.down, tint);
                            }
                        }
                    }
                }
            Part("Rich soil raised beds and timber borders", plots, "Village Crop Beds", ground.Finish("Kitchen garden beds"), new[] { M("Field Soil"), M("Cedar Timber") });
            Part("Leafy planted vegetable rows", plots, "Village Crops", crop.Finish("Batched leafy crop rows"), new[] { M("Valley Grass") });
        }

        private static void Orchards(Transform root, List<Rect> occupied)
        {
            Transform groves = Group("Orchards and farm windbreaks", root);
            var random = new System.Random(8714);
            // A continuous mid-distance layer connects isolated farms to the wooded valley sides.
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 28; i++)
                {
                    float x = side * Range(random, 35, 41), z = -29 + i * 10;
                    if (Blocked(occupied, x, z, 2.3f)) continue;
                    Plant(i % 3 == 0 ? "BroadleafTree_02_Green" : "BroadleafTree_04_Green", groves,
                        new Vector3(x, 0, z), Range(random, 4.8f, 7.8f), Range(random, 0, 360));
                }
            // Closer, irregular groups provide passing landmarks through the otherwise empty gaps.
            for (int i = 0; i < 20; i++)
            {
                float x = i % 2 == 0 ? -10 : 13.5f, z = -30 + i * 14;
                if (Blocked(occupied, x, z, 2)) continue;
                Plant("BroadleafTree_01_Green", groves, new Vector3(x, 0, z), Range(random, 3.9f, 5.8f), i * 137.5f);
            }
        }

        private static void WildEdges(Transform root, List<Rect> occupied)
        {
            Transform wild = Group("Dense irregular meadow margins and hedgerows", root);
            var random = new System.Random(4207);
            for (int i = 0; i < 250; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float x = side < 0 ? -Range(random, 6.5f, 13) : Range(random, 10, 14);
                float z = Range(random, -34, 246);
                if (IsEntrance(z) || Blocked(occupied, x, z, 0.65f)) continue;
                Plant(i % 5 == 0 ? "FlowerMeadow_BluePurple" : "Grass_0" + (1 + i % 3), wild,
                    new Vector3(x, 0, z), Range(random, 0.30f, 0.70f), Range(random, 0, 360));
            }
            for (int i = 0; i < 68; i++)
            {
                float side = i % 2 == 0 ? -1 : 1, x = side * Range(random, 12, 16), z = -32 + i * 4.1f;
                if (IsEntrance(z) || Blocked(occupied, x, z, 1)) continue;
                Plant(i % 3 == 0 ? "Bush_01_01" : "Bush_03_01", wild, new Vector3(x, 0, z), Range(random, 0.9f, 1.5f), i * 91);
            }
        }

        private static Mesh Grass(List<Rect> occupied)
        {
            var g = new CountrySideMeshes.Geometry(1); var random = new System.Random(7134);
            for (int i = 0; i < 10000; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float x = side < 0 ? -Range(random, 5.9f, 18) : Range(random, 9.6f, 18);
                float z = Range(random, -35.5f, 247.5f);
                if (IsEntrance(z) || Blocked(occupied, x, z, 0.25f)) continue;
                Vector3 p = new Vector3(x, 0.015f, z);
                float height = Range(random, 0.13f, 0.42f), angle = Range(random, 0, Mathf.PI * 2);
                Color tint = Color.Lerp(new Color(0.19f, 0.32f, 0.08f), new Color(0.51f, 0.60f, 0.21f), Range(random, 0, 1));
                for (int leaf = 0; leaf < 3; leaf++)
                {
                    Vector3 axis = new Vector3(Mathf.Cos(angle + leaf * 2.1f), 0, Mathf.Sin(angle + leaf * 2.1f));
                    Vector3 tip = p + Vector3.up * height + axis * height * 0.35f;
                    g.Triangle(p - axis * 0.045f, tip, p + axis * 0.045f, 0, Vector3.up, tint);
                    g.Triangle(p + axis * 0.045f, tip, p - axis * 0.045f, 0, Vector3.down, tint);
                }
            }
            return g.Finish("Dense batched roadside meadow grass");
        }

        private static void Hedges(Transform root)
        {
            Transform hedges = Group("Passing hedgerows and lush canal banks", root);
            var random = new System.Random(7138);
            for (int i = 0; i < 86; i++)
            {
                float z = -33 + i * 3.25f;
                if (IsEntrance(z)) continue;
                float x = i % 2 == 0 ? -Range(random, 6.1f, 7.2f) : Range(random, 9.8f, 10.4f);
                Plant(i % 3 == 0 ? "Bush_01_01" : "Bush_03_01", hedges, new Vector3(x, 0, z), Range(random, 0.65f, 1.05f), i * 137.5f);
                Plant("Grass_0" + (1 + i % 3), hedges, new Vector3(x + 0.3f, 0, z + 1.1f), Range(random, 0.45f, 0.75f), i * 91);
            }
        }

        private static Mesh VillageProps()
        {
            var g = new CountrySideMeshes.Geometry(6);
            for (int i = 0; i < 8; i++)
            {
                float z = -34 + i * 35.5f;
                g.Box(new Vector3(-8.8f, 2.6f, z), new Vector3(0.17f, 5.2f, 0.17f), 0);
                g.Box(new Vector3(-8.8f, 4.9f, z), new Vector3(1.65f, 0.12f, 0.13f), 1);
                foreach (float offset in new[] { -0.58f, 0.58f })
                {
                    g.Box(new Vector3(-8.8f + offset, 5.02f, z), new Vector3(0.09f, 0.20f, 0.09f), 4);
                    var points = new Vector3[9];
                    for (int p = 0; p < points.Length; p++)
                    {
                        float t = p / 8f;
                        points[p] = new Vector3(-8.8f + offset, 5.1f - Mathf.Sin(t * Mathf.PI) * 0.65f, z + 35.5f * t);
                    }
                    g.Tube(points, 0.018f, 1, 4);
                }
            }
            foreach (float z in new[] { 10f, 74f, 158f, 222f })
            {
                g.Box(new Vector3(-6.8f, 0.95f, z), new Vector3(0.12f, 1.9f, 0.12f), 0);
                g.Box(new Vector3(-6.8f, 1.68f, z), new Vector3(1.1f, 0.5f, 0.14f), 0);
                for (int line = 0; line < 3; line++)
                    g.Box(new Vector3(-7.13f + line * 0.30f, 1.68f, z - 0.08f), new Vector3(0.12f, 0.23f, 0.025f), 4);
            }
            foreach (Vector3 p in new[] { new Vector3(-11, 0, 105), new Vector3(12.5f, 0, 35), new Vector3(-11, 0, 241) })
            {
                foreach (float x in new[] { -1.3f, 1.3f }) foreach (float z in new[] { -0.75f, 0.75f })
                    g.Box(p + new Vector3(x, 1.1f, z), new Vector3(0.10f, 2.2f, 0.10f), 0);
                g.Box(p + Vector3.up * 0.88f, new Vector3(2.8f, 0.16f, 1.7f), 0);
                for (int stripe = 0; stripe < 10; stripe++)
                    g.Box(p + new Vector3(-1.44f + stripe * 0.32f, 2.28f, 0), new Vector3(0.32f, 0.08f, 2.25f), stripe % 2 == 0 ? 3 : 4,
                        Quaternion.Euler(8, 0, 0));
                foreach (float x in new[] { -0.85f, 0, 0.85f })
                {
                    g.Box(p + new Vector3(x, 1.07f, 0), new Vector3(0.65f, 0.25f, 0.72f), 0);
                    for (int fruit = 0; fruit < 6; fruit++)
                        g.Box(p + new Vector3(x - 0.2f + fruit % 3 * 0.2f, 1.24f, -0.18f + fruit / 3 * 0.36f), Vector3.one * 0.15f, 5,
                            Quaternion.Euler(0, fruit * 37, 20));
                }
                g.Box(p + new Vector3(0, 1.57f, 0.80f), new Vector3(1.4f, 0.27f, 0.06f), 4);
            }
            return g.Finish("Rural utility lines, wayfinding and striped produce stalls");
        }

        private static void Model(string name, Transform parent, Vector3 position, float scale, float yaw)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Prefabs/" + name + ".prefab");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, parent.gameObject.scene);
            model.transform.SetParent(parent, false); model.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            model.transform.localScale = Vector3.one * scale;
        }
        private static void Plant(string name, Transform parent, Vector3 position, float height, float yaw)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Nature + name + ".prefab");
            if (source == null) throw new InvalidOperationException("Missing native countryside plant: " + name);
            Bounds bounds = MainMenuAssetStoreTrees.BoundsOf(source); float scale = height / bounds.size.y;
            var plant = (GameObject)PrefabUtility.InstantiatePrefab(source, parent.gameObject.scene);
            plant.transform.SetParent(parent, false); plant.transform.localScale = Vector3.one * scale;
            plant.transform.SetLocalPositionAndRotation(position - Vector3.up * bounds.min.y * scale, Quaternion.Euler(0, yaw, 0));
            Transform trigger = plant.transform.Find("LeafTrigger"); if (trigger != null) Object.DestroyImmediate(trigger.gameObject);
            foreach (Collider collider in plant.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        }
        private static void Part(string name, Transform parent, string assetName, Mesh mesh, Material[] materials)
        {
            string path = Art + "Meshes/" + assetName + ".asset";
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { saved = mesh; AssetDatabase.CreateAsset(mesh, path); }
            else Object.DestroyImmediate(mesh);
            Transform item = Group(name, parent); item.gameObject.AddComponent<MeshFilter>().sharedMesh = saved;
            item.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
        }
        private static Transform Group(string name, Transform parent)
        {
            var item = new GameObject(name); item.transform.SetParent(parent, false); return item.transform;
        }
        private static Material M(string name) => AssetDatabase.LoadAssetAtPath<Material>(Art + "Materials/" + name + ".mat");
        private static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        private static Rect Box(float x, float z, float halfWidth, float halfDepth) => Rect.MinMaxRect(x - halfWidth, z - halfDepth, x + halfWidth, z + halfDepth);
        private static bool Blocked(List<Rect> occupied, float x, float z, float radius) => occupied.Any(item => item.Overlaps(Box(x, z, radius, radius)));
        private static bool IsEntrance(float z) => new[] { 24f, 45f, 82f, 118f, 146f }.Any(entry => Mathf.Abs(z - entry) < 2);
    }
}
