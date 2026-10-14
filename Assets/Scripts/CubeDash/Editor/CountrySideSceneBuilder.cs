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
    /// <summary>Authors the separate countryside environment; never rebuilds Level or changes navigation.</summary>
    public static class CountrySideSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Country Side.unity";
        public const string ArtPath = "Assets/CountrySide/";
        public const string RootName = "Japanese Countryside - Spring Valley";
        public const string EnvironmentPath = ArtPath + "Prefabs/Japanese Countryside.prefab";
        private const string NaturePath = "Assets/Idyllic Fantasy Nature/Prefabs/";
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, GameObject> plants = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, Bounds> plantBounds = new Dictionary<string, Bounds>();
        private static readonly Vector4[] Hills = {
            new Vector4(-115, 80, 75, 28), new Vector4(112, 112, 85, 33), new Vector4(-112, 212, 100, 44), new Vector4(124, 247, 108, 40),
            new Vector4(-160, 338, 122, 69), new Vector4(167, 342, 126, 65), new Vector4(-82, 383, 102, 51), new Vector4(91, 409, 119, 62),
            new Vector4(-170, 472, 160, 82), new Vector4(174, 495, 150, 93), new Vector4(0, 526, 148, 83) };

        [MenuItem("Tools/Cube Dash/Design Japanese Country Side Environment")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before authoring scenery.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your scene edits first.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath);
            SceneManager.SetActiveScene(scene);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Country Side.");
        }

        public static void ApplyToScene(Scene scene)
        {
            if (scene.path != ScenePath) throw new InvalidOperationException("This tool only authors the Country Side scene.");
            GameObject existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == RootName);
            if (existing != null)
            {
                var runner = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CountrySideRunnerScenery>(true)).SingleOrDefault();
                bool playable = runner != null && runner.Tiles.Length == 2 && runner.Tiles.All(tile => tile != null && tile.IsChildOf(existing.transform));
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existing) != EnvironmentPath && !playable)
                    throw new InvalidOperationException("A user object uses the environment root name; it was not replaced.");
                Debug.Log("Country Side is already designed. Existing scene and scenery edits were preserved."); return;
            }
            Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera"));
            Light sun = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true)).FirstOrDefault(light => light.type == LightType.Directional);
            if (sun == null) throw new InvalidOperationException("The existing directional light is required.");
            PrepareAssets();
            CreateEnvironmentPrefab();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath), scene);
            instance.name = RootName;
            Undo.RegisterCreatedObjectUndo(instance, "Design Japanese countryside");
            Undo.RecordObjects(new Object[] { camera, camera.transform, sun, sun.transform }, "Light and frame countryside");
            camera.transform.position = new Vector3(0, 5.3f, -16);
            camera.transform.LookAt(new Vector3(0, 3.2f, 65));
            camera.fieldOfView = 58; camera.nearClipPlane = 0.15f; camera.farClipPlane = 850;
            camera.clearFlags = CameraClearFlags.Skybox; camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High; data.dithering = true;
            sun.transform.rotation = Quaternion.Euler(42, -38, 0);
            sun.color = new Color(1, 0.93f, 0.80f); sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.82f; sun.shadowNormalBias = 0.30f;
            RenderSettings.sun = sun; RenderSettings.skybox = materials["Spring Sky"];
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.78f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.64f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.29f, 0.22f);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.71f, 0.83f, 0.84f);
            RenderSettings.fogStartDistance = 140; RenderSettings.fogEndDistance = 680;
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
            Debug.Log("Authored a separate Japanese spring countryside: linked nature-pack flowers/grass/trees, original minka/kura architecture, rice paddies, irrigation bridges and layered hills. Existing Level and menu navigation are unchanged.");
        }

        private static void PrepareAssets()
        {
            Folder(ArtPath.TrimEnd('/')); Folder(ArtPath + "Materials"); Folder(ArtPath + "Meshes"); Folder(ArtPath + "Prefabs");
            materials.Clear(); plants.Clear(); plantBounds.Clear();
            Lit("Warm Plaster", "EFE1C4"); Lit("Dark Timber", "42362F"); Lit("Cedar Timber", "A07850");
            Lit("Kawara Roof Tiles", "4A5961", 0.28f, true); Lit("Shoji Paper", "FFF0D0");
            Lit("Weathered Stone", "929381"); Lit("Torii Vermilion", "B74332");
            Lit("Country Asphalt", "737A71"); Lit("Road Edge Paint", "E4DBBF"); Lit("Farm Path", "BEA47A");
            Lit("Field Soil", "7C7950"); Lit("Paddy Embankment", "82934B"); Lit("Canal Water", "80B7B9", 0.83f);
            Lit("Rice Shade", "5F8E3E"); Lit("Rice Tips", "A1B853"); Lit("Cloud White", "F7F3DF");
            Ground("Valley Grass", Color.white);
            Ground("Near Hills", new Color(0.85f, 0.97f, 0.84f));
            Ground("Middle Hills", new Color(0.68f, 0.86f, 0.84f));
            Ground("Distant Hills", new Color(0.64f, 0.77f, 0.87f));
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "Materials/Spring Sky.mat");
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Spring Sky" };
                sky.SetFloat("_SunSize", 0.025f); sky.SetFloat("_AtmosphereThickness", 0.80f);
                sky.SetColor("_SkyTint", new Color(0.54f, 0.69f, 0.78f)); sky.SetColor("_GroundColor", new Color(0.62f, 0.70f, 0.55f));
                sky.SetFloat("_Exposure", 1.15f); AssetDatabase.CreateAsset(sky, ArtPath + "Materials/Spring Sky.mat");
            }
            materials["Spring Sky"] = sky;
            MeshAsset("Valley", CountrySideMeshes.Valley);
            MeshAsset("Hill", CountrySideMeshes.Hill);
            Model("Minka Farmhouse", () => CountrySideMeshes.Farmhouse(false), ArchitectureMaterials());
            Model("Kura Storehouse", () => CountrySideMeshes.Farmhouse(true), ArchitectureMaterials());
            Model("Village Torii", CountrySideMeshes.Torii, ArchitectureMaterials());
            Model("Stone Lantern", CountrySideMeshes.Lantern, ArchitectureMaterials());
            Model("Canal Footbridge", CountrySideMeshes.Bridge, ArchitectureMaterials());
            Material paddyWater = AssetDatabase.LoadAssetAtPath<Material>(CountrySideWaterBuilder.PaddyMaterialPath);
            Model("Rice Paddy", CountrySideMeshes.Paddy, new[] { M("Paddy Embankment"), paddyWater != null ? paddyWater : M("Canal Water"), M("Rice Shade"), M("Rice Tips") });
            foreach (string name in new[] { "BlossomTree_01", "BlossomTree_03", "BlossomTree_05", "BroadleafTree_01_Green", "BroadleafTree_03_Green", "Fir_02", "Fir_04",
                "Bush_01_01", "Bush_02_01", "Bush_03_01", "Grass_01", "Grass_02", "Grass_03", "FlowerMeadow_Pink", "FlowerMeadow_White", "FlowerMeadow_PurpleRedPink",
                "FlowerMeadow_OrangePinkRedPurpleBlue", "FlowerMeadow_BluePurple", "Flower_Pink", "Flower_White", "Flower_Yellow", "Rock_Medium_01", "Rock_Small_02", "Reeds_01" })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NaturePath + name + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Missing nature-pack prefab: " + name);
                foreach (MonoBehaviour script in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                    if (script == null || script.GetType().FullName != "IdyllicFantasyNature.BushLeafTrigger" || !script.transform.IsChildOf(prefab.transform.Find("LeafTrigger")))
                        throw new InvalidOperationException("Unexpected demo script on nature prefab: " + name);
                Bounds bounds = MainMenuAssetStoreTrees.BoundsOf(prefab);
                if (bounds.size.y <= 0 || bounds.size.x <= 0) throw new InvalidOperationException("Invalid nature mesh bounds: " + name);
                plants[name] = prefab; plantBounds[name] = bounds;
            }
            CreateLightingProfile(); AssetDatabase.SaveAssets();
        }

        private static Material[] ArchitectureMaterials() => new[] { M("Warm Plaster"), M("Dark Timber"), M("Cedar Timber"), M("Kawara Roof Tiles"), M("Shoji Paper"), M("Weathered Stone"), M("Torii Vermilion") };
        private static Material M(string name) => materials[name];

        private static void CreateEnvironmentPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath) != null) return;
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(RootName); SceneManager.MoveGameObjectToScene(root, scene);
                Transform landscape = Group("Landscape - green valley and layered hills", root.transform);
                Part("Rolling valley ground", landscape, MeshAsset("Valley", CountrySideMeshes.Valley), new[] { M("Valley Grass") }, Vector3.zero, Vector3.one);
                for (int i = 0; i < Hills.Length; i++)
                {
                    Vector4 hill = Hills[i]; string mat = i < 4 ? "Near Hills" : i < 8 ? "Middle Hills" : "Distant Hills";
                    var part = Part(mat + " " + (i + 1), landscape, MeshAsset("Hill", CountrySideMeshes.Hill), new[] { M(mat) },
                        new Vector3(hill.x, -0.4f, hill.y), new Vector3(hill.z, hill.w, hill.z * 0.83f));
                    part.transform.localRotation = Quaternion.Euler(0, i * 43, 0);
                }
                Roads(root.transform);
                Transform fields = Group("Flower fields - linked Idyllic meadows", root.transform);
                Field(fields, "Pink cosmos foreground", new Vector3(-20, 0, -4), 22, 32, "FlowerMeadow_Pink", 2.8f, 11);
                Field(fields, "White blossom foreground", new Vector3(20, 0, 12), 22, 32, "FlowerMeadow_White", 2.8f, 27);
                Field(fields, "Rose and purple flower field", new Vector3(-22, 0, 65), 24, 34, "FlowerMeadow_PurpleRedPink", 3.0f, 33);
                Field(fields, "Mixed spring flower field", new Vector3(22, 0, 94), 24, 36, "FlowerMeadow_OrangePinkRedPurpleBlue", 3.0f, 45);
                Field(fields, "Blue wildflower meadow", new Vector3(-25, 0, 184), 28, 36, "FlowerMeadow_BluePurple", 4.1f, 67);
                Field(fields, "Distant pink flower meadow", new Vector3(25, 0, 205), 28, 36, "FlowerMeadow_Pink", 4.1f, 81);
                Village(root.transform);
                Vegetation(root.transform);
                Clouds(root.transform);
                var lighting = Group("Country Side Lighting", root.transform);
                Volume volume = lighting.gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 1;
                volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ArtPath + "Spring Lighting.asset");
                if (PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPath) == null) throw new InvalidOperationException("Could not save countryside environment prefab.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void Roads(Transform parent)
        {
            Transform road = Group("Runner road - clear 8.4 metre corridor", parent);
            var g = new CountrySideMeshes.Geometry(2);
            g.Box(new Vector3(0, 0.0f, 106), new Vector3(8.4f, 0.08f, 284), 0);
            foreach (float x in new[] { -3.95f, 3.95f }) g.Box(new Vector3(x, 0.048f, 106), new Vector3(0.10f, 0.012f, 284), 1);
            for (int dash = 0; dash < 48; dash++) g.Box(new Vector3(0, 0.05f, -31 + dash * 5.9f), new Vector3(0.11f, 0.012f, 2.6f), 1);
            Part("Asphalt and rural road paint", road, MeshAsset("Country Road", () => g.Finish("Country road - asphalt and painted edges")), new[] { M("Country Asphalt"), M("Road Edge Paint") }, Vector3.zero, Vector3.one);
            Transform details = Group("Rural details - canals fences and farm paths", parent);
            var props = new CountrySideMeshes.Geometry(4); // stone, wood, path, water
            props.WaterSurface(new Vector3(7.8f, 0.005f, 106), new Vector2(2.35f, 284), 3);
            foreach (float x in new[] { 6.46f, 9.14f }) props.Box(new Vector3(x, 0.12f, 106), new Vector3(0.30f, 0.34f, 284), 0);
            foreach (float z in new[] { 24f, 45f, 118f, 146f })
            {
                float side = z == 24 || z == 118 || z == 146 ? -1 : 1;
                props.Box(new Vector3(side * 12.3f, 0.015f, z), new Vector3(15.8f, 0.06f, 1.9f), 2);
            }
            foreach (float side in new[] { -1f, 1f })
                for (int post = 0; post < 57; post++)
                {
                    float z = -28 + post * 4.8f;
                    if (new[] { 24f, 45f, 82f, 118f, 146f }.Any(entry => Mathf.Abs(z - entry) < 3)) continue;
                    props.Box(new Vector3(side * 5.45f, 0.39f, z), new Vector3(0.12f, 0.78f, 0.12f), 1);
                    foreach (float y in new[] { 0.25f, 0.61f }) props.Box(new Vector3(side * 5.45f, y, z + 2.2f), new Vector3(0.075f, 0.08f, 4.4f), 1);
                }
            Part("Irrigation banks and cedar field fencing", details, MeshAsset("Rural Infrastructure", () => props.Finish("Countryside - irrigation canal, paths and cedar fencing")),
                new[] { M("Weathered Stone"), M("Cedar Timber"), M("Farm Path"), M("Canal Water") }, Vector3.zero, Vector3.one);
            foreach (float z in new[] { 45f, 94f, 148f }) PlaceModel("Canal Footbridge", details, new Vector3(7.8f, 0.19f, z), 1, 0);
        }

        private static void Field(Transform parent, string name, Vector3 center, float width, float depth, string prefab, float spacing, int seed)
        {
            Transform field = Group(name, parent); field.localPosition = center;
            var bed = new CountrySideMeshes.Geometry(1);
            bed.Box(new Vector3(0, -0.012f, 0), new Vector3(width, 0.025f, depth), 0);
            Part("Soft planted field bed", field, MeshAsset(name + " Bed", () => bed.Finish(name + " soil bed")), new[] { M("Field Soil") }, Vector3.zero, Vector3.one);
            var random = new System.Random(seed);
            int columns = Mathf.FloorToInt(width / spacing), rows = Mathf.FloorToInt(depth / spacing);
            for (int row = 0; row < rows; row++) for (int column = 0; column < columns; column++)
            {
                Vector3 p = new Vector3((column - (columns - 1) * 0.5f) * spacing + Range(random, -0.28f, 0.28f), 0.005f,
                    (row - (rows - 1) * 0.5f) * spacing + Range(random, -0.28f, 0.28f));
                Nature(prefab, field, p, spacing * Range(random, 0.94f, 1.15f), false, Range(random, 0, 360));
            }
        }

        private static void Village(Transform parent)
        {
            Transform village = Group("Village - original Japanese minka and kura", parent);
            PlaceModel("Minka Farmhouse", village, new Vector3(-18, 0, 24), 1.12f, -45);
            PlaceModel("Minka Farmhouse", village, new Vector3(20, 0, 45), 1.0f, 50);
            PlaceModel("Kura Storehouse", village, new Vector3(31, 0, 67), 1.10f, 45);
            PlaceModel("Minka Farmhouse", village, new Vector3(-22, 0, 118), 1.08f, -55);
            PlaceModel("Kura Storehouse", village, new Vector3(-32, 0, 145), 0.97f, -45);
            PlaceModel("Minka Farmhouse", village, new Vector3(29, 0, 171), 0.96f, 45);
            Transform shrine = Group("Roadside shrine and stone lanterns", parent);
            PlaceModel("Village Torii", shrine, new Vector3(-13, 0, 91), 0.92f, -55);
            PlaceModel("Kura Storehouse", shrine, new Vector3(-24, 0, 96), 0.48f, -55);
            foreach (Vector3 position in new[] { new Vector3(-10.2f, 0, 86), new Vector3(-10.2f, 0, 96), new Vector3(-14, 0, 20), new Vector3(14, 0, 40) })
                PlaceModel("Stone Lantern", shrine, position, 1, 0);
            Transform paddies = Group("Rice paddies - flooded seedling terraces", parent);
            foreach (Vector3 position in new[] { new Vector3(-25, 0, 145), new Vector3(-39, 0, 143), new Vector3(22, 0, 141), new Vector3(37, 0, 145) })
                PlaceModel("Rice Paddy", paddies, position, 1, 0);
        }

        private static void Vegetation(Transform parent)
        {
            Transform trees = Group("Trees - sakura avenue and green woodland", parent);
            for (int i = 0; i < 10; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                Nature(new[] { "BlossomTree_01", "BlossomTree_03", "BlossomTree_05" }[i % 3], trees, new Vector3(side * (11.2f + i % 3), 0, -3 + i * 23), 7.0f + i % 3 * 0.9f, true, i * 137.5f);
            }
            var random = new System.Random(920);
            string[] green = { "BroadleafTree_01_Green", "BroadleafTree_03_Green", "Fir_02", "Fir_04" };
            for (int i = 0; i < 72; i++)
            {
                float side = i % 2 == 0 ? -1 : 1, x = side * Range(random, 39, 96), z = Range(random, -18, 284);
                Nature(green[i % 4], trees, new Vector3(x, LandscapeHeight(x, z) - 0.12f, z), Range(random, 7, 13), true, Range(random, 0, 360));
            }
            Transform plantsRoot = Group("Roadside grass bushes rocks and flowers", parent);
            for (int i = 0; i < 170; i++)
            {
                float side = i % 2 == 0 ? -1 : 1, x = side * Range(random, 5.8f, 6.25f), z = Range(random, -28, 239);
                Nature("Grass_0" + (1 + i % 3), plantsRoot, new Vector3(x, 0, z), Range(random, 0.26f, 0.52f), true, Range(random, 0, 360));
                if (i < 50)
                    Nature(new[] { "Flower_Pink", "Flower_White", "Flower_Yellow" }[i % 3], plantsRoot, new Vector3(x + side * 0.14f, 0, z + 0.4f), Range(random, 0.36f, 0.57f), true, Range(random, 0, 360));
            }
            for (int i = 0; i < 28; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                Nature(new[] { "Bush_01_01", "Bush_02_01", "Bush_03_01" }[i % 3], plantsRoot, new Vector3(side * Range(random, 32, 38), 0, i * 8.2f), Range(random, 1.1f, 1.8f), true, i * 73);
                if (i < 14) Nature(i % 2 == 0 ? "Rock_Medium_01" : "Rock_Small_02", plantsRoot, new Vector3(side * Range(random, 10, 12), 0, i * 16 + 8), Range(random, 0.45f, 1.0f), true, i * 91);
                if (i < 20) Nature("Reeds_01", plantsRoot, new Vector3(9.4f, 0, i * 11 + 4), Range(random, 0.60f, 1.05f), true, i * 54);
            }
        }

        private static void Clouds(Transform parent)
        {
            Transform clouds = Group("Sky - soft distant cloud banks", parent);
            Mesh sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            for (int bank = 0; bank < 5; bank++)
                for (int lobe = 0; lobe < 4; lobe++)
                    Part("Cloud bank " + (bank + 1), clouds, sphere, new[] { M("Cloud White") }, new Vector3(-190 + bank * 95 + lobe * 16, 88 + bank % 2 * 22 + lobe % 2 * 5, 325 + bank * 36),
                        new Vector3(46, 17 + lobe % 2 * 9, 24));
        }

        private static Transform Nature(string name, Transform parent, Vector3 position, float fit, bool height, float yaw)
        {
            Bounds bounds = plantBounds[name]; float scale = fit / (height ? bounds.size.y : Mathf.Max(bounds.size.x, bounds.size.z));
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(plants[name], parent.gameObject.scene);
            instance.transform.SetParent(parent, false); instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localPosition = position - Vector3.up * (bounds.min.y * scale);
            Transform trigger = instance.transform.Find("LeafTrigger"); if (trigger != null) Object.DestroyImmediate(trigger.gameObject);
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
            return instance.transform;
        }

        private static Transform PlaceModel(string name, Transform parent, Vector3 position, float scale, float yaw)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ArtPath + "Prefabs/" + name + ".prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent.gameObject.scene);
            instance.transform.SetParent(parent, false); instance.transform.localPosition = position;
            instance.transform.localScale = Vector3.one * scale; instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return instance.transform;
        }

        private static MeshRenderer Part(string name, Transform parent, Mesh mesh, Material[] palette, Vector3 position, Vector3 scale)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh; var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterials = palette;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic); return renderer;
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }

        private static void Model(string name, Func<Mesh> geometry, Material[] palette)
        {
            string path = ArtPath + "Prefabs/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            Mesh mesh = MeshAsset(name, geometry); Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(name); SceneManager.MoveGameObjectToScene(root, preview);
                Part("Original modeled " + name, root.transform, mesh, palette, Vector3.zero, Vector3.one);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidOperationException("Could not save countryside model: " + name);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static Mesh MeshAsset(string name, Func<Mesh> create)
        {
            string path = ArtPath + "Meshes/" + name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = create(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        private static void Lit(string name, string hex, float smoothness = 0.13f, bool doubleSided = false)
        {
            string path = ArtPath + "Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
                ColorUtility.TryParseHtmlString("#" + hex, out Color color); material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness); material.SetFloat("_Metallic", 0);
                if (doubleSided) material.SetFloat("_Cull", (float)CullMode.Off);
                AssetDatabase.CreateAsset(material, path);
            }
            materials[name] = material;
        }

        private static void Ground(string name, Color tint)
        {
            string path = ArtPath + "Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("CubeDash/Countryside Ground");
                if (shader == null) throw new InvalidOperationException("The countryside ground shader must be imported first.");
                material = new Material(shader) { name = name, enableInstancing = true }; material.SetColor("_BaseColor", tint);
                AssetDatabase.CreateAsset(material, path);
            }
            materials[name] = material;
        }

        private static void CreateLightingProfile()
        {
            string path = ArtPath + "Spring Lighting.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) return;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "Country Side Spring Lighting";
            AssetDatabase.CreateAsset(profile, path);
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true); color.postExposure.Override(0.12f); color.contrast.Override(5); color.saturation.Override(5);
            var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(1.2f); bloom.intensity.Override(0.15f); bloom.scatter.Override(0.45f);
            foreach (VolumeComponent component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
        }

        private static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        internal static float LandscapeHeight(float x, float z)
        {
            float height = CountrySideMeshes.GroundHeight(x, z);
            for (int i = 0; i < Hills.Length; i++)
            {
                Vector4 hill = Hills[i];
                Vector3 local = Quaternion.Euler(0, -i * 43, 0) * new Vector3(x - hill.x, 0, z - hill.y);
                float nx = local.x / hill.z, nz = local.z / (hill.z * 0.83f), radius = Mathf.Sqrt(nx * nx + nz * nz);
                if (radius >= 1) continue;
                float angle = Mathf.Atan2(nz, nx);
                float ripple = 1 + Mathf.SmoothStep(0, 1, radius * 3) * (0.055f * Mathf.Sin(angle * 3) + 0.035f * Mathf.Sin(angle * 5 + radius * 3));
                height = Mathf.Max(height, -0.4f + hill.w * Mathf.Pow(1 - radius * radius, 1.65f) * ripple);
            }
            return height;
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/'); Folder(path.Substring(0, split)); AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }

        public static void ApplyAndCaptureFromCommandLine()
        {
            ApplyFromCommandLine(); CaptureFromCommandLine();
        }

        public static void CaptureFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).Single(c => c.CompareTag("MainCamera"));
            string prefix = Environment.GetEnvironmentVariable("CUBE_DASH_COUNTRY_SIDE_PREVIEW") ?? Path.Combine(Path.GetDirectoryName(Application.dataPath), "country-side");
            var target = new RenderTexture(1672, 940, 24); var image = new Texture2D(1672, 940, TextureFormat.RGB24, false);
            RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            bool oldAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target;
                Save("-runner");
                camera.transform.position = new Vector3(38, 22, -29); camera.transform.LookAt(new Vector3(0, 1, 67)); Save("-overview");
                camera.transform.position = new Vector3(-8, 4.4f, 8); camera.transform.LookAt(new Vector3(-18, 2.7f, 24)); Save("-farmhouse");
                void Save(string suffix)
                {
                    for (int i = 0; i < 6; i++) camera.Render();
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                    File.WriteAllBytes(prefix + suffix + ".png", image.EncodeToPNG()); Debug.Log("Countryside preview: " + prefix + suffix + ".png");
                }
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive; camera.transform.SetPositionAndRotation(position, rotation);
                ShaderUtil.allowAsyncCompilation = oldAsync; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
