using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Editor-only art pass. All results are saved as normal editable assets/objects.</summary>
    public static class CubeDashVisualUpgrade
    {
        private const string Prefabs = "Assets/Prefab/CubeDash/";
        private static Mesh bevel;
        private static Material metal, amber, coolTrim;

        [MenuItem("Tools/Cube Dash/Enhance City Visuals")]
        public static void EnhanceFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) CubeDashColorMatchStyle.ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            if (game == null) throw new InvalidOperationException("Open an authored Cube Dash Level first.");
            bevel = BeveledCube();
            metal = Lit("BuildingMetal", new Color(0.10f, 0.15f, 0.22f), 0.65f, 0.55f);
            amber = Lit("AmberLight", new Color(1f, 0.48f, 0.13f), 0.15f, 0.55f, 2f);
            coolTrim = Lit("CoolTrim", new Color(0.27f, 0.90f, 0.89f), 0.15f, 0.55f, 1f);
            Lit("Road", new Color(0.13f, 0.17f, 0.25f), 0.24f, 0.52f);
            Lit("LaneLight", new Color(0.2f, 0.76f, 0.85f), 0f, 0.4f, 0.7f);
            UpdatePlayer();
            UpdateObstacles();

            GameObject[] towers = new GameObject[3];
            for (int i = 0; i < towers.Length; i++) towers[i] = Tower(i);
            UpdateTrack(towers);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Material sky = MaterialAsset("Horizon", "CubeDash/Horizon Skybox");
            sky.SetColor("_ZenithColor", new Color(0.10f, 0.17f, 0.36f));
            sky.SetColor("_HorizonColor", new Color(0.75f, 0.51f, 0.67f));
            sky.SetColor("_GroundColor", new Color(0.20f, 0.21f, 0.36f));
            sky.SetFloat("_GradientPower", 0.55f);
            sky.SetColor("_SunColor", new Color(1.5f, 0.8f, 0.5f));
            sky.SetVector("_SunDirection", new Vector4(0.15f, 0.07f, 1, 0));
            sky.SetFloat("_SunSize", 1.8f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.46f, 0.65f);
            RenderSettings.ambientEquatorColor = new Color(0.27f, 0.31f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.15f, 0.23f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.47f, 0.39f, 0.54f);
            RenderSettings.fogStartDistance = 85;
            RenderSettings.fogEndDistance = 260;
            Camera camera = game.GameCamera;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = 360;
            camera.allowHDR = true;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            Light light = GameObject.Find("Directional Light")?.GetComponent<Light>();
            if (light != null)
            {
                light.color = new Color(1f, 0.80f, 0.68f);
                light.intensity = 1.5f;
                light.transform.rotation = Quaternion.Euler(35, -25, 0);
            }
            AddPostProcessing();
            AddWake(game);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Saved dusk horizon sky, glass skyscrapers, beveled amber barricades, bloom, and the cube's editable ribbon trail.");
        }

        private static Material MaterialAsset(string name, string shaderName)
        {
            string path = "Assets/Material/CubeDash" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing shader: " + shaderName);
            if (material == null)
            {
                material = new Material(shader) { name = "CubeDash" + name };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material Lit(string name, Color color, float metallic, float smoothness, float glow = 0)
        {
            Material material = MaterialAsset(name, "Universal Render Pipeline/Lit");
            material.color = color;
            material.enableInstancing = true;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetColor("_EmissionColor", color * glow);
            if (glow > 0) material.EnableKeyword("_EMISSION");
            else material.DisableKeyword("_EMISSION");
            return material;
        }

        private static void UpdatePlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Prefabs + "PlayerCube.prefab");
            try
            {
                root.GetComponent<MeshFilter>().sharedMesh = bevel;
                root.GetComponent<Renderer>().sharedMaterial = Lit("Player", new Color(0.26f, 0.95f, 0.73f), 0.3f, 0.65f, 0.3f);
                Transform details = ManagedGroup(root.transform, "Cube Face Details");
                Part("Core Inset", details, new Vector3(0, 0, -0.501f), new Vector3(0.56f, 0.56f, 0.018f), metal);
                Part("Core Light", details, new Vector3(0, 0, -0.517f), new Vector3(0.24f, 0.24f, 0.02f), coolTrim);
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "PlayerCube.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void UpdateObstacles()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Prefabs + "ObstacleCube.prefab");
            try
            {
                root.GetComponent<MeshFilter>().sharedMesh = bevel;
                root.GetComponent<Renderer>().sharedMaterial = Lit("Obstacle", new Color(0.18f, 0.22f, 0.30f), 0.55f, 0.6f);
                Transform oldStripe = root.transform.Find("Warning Stripe");
                if (oldStripe != null) Object.DestroyImmediate(oldStripe.gameObject);
                Transform details = ManagedGroup(root.transform, "Barrier Details");
                Part("Front Inset", details, new Vector3(0, 0, -0.504f), new Vector3(0.73f, 0.73f, 0.03f), metal);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Amber Vertical Frame", details, new Vector3(side * 0.385f, 0, -0.523f), new Vector3(0.035f, 0.80f, 0.02f), amber);
                    Part("Amber Horizontal Frame", details, new Vector3(0, side * 0.385f, -0.523f), new Vector3(0.80f, 0.035f, 0.02f), amber);
                    Part("Side Armour", details, new Vector3(side * 0.502f, 0, 0), new Vector3(0.02f, 0.65f, 0.70f), metal);
                }
                for (int slash = -1; slash <= 1; slash++)
                {
                    GameObject stripe = Part("Hazard Slash", details, new Vector3(slash * 0.18f, 0, -0.53f),
                        new Vector3(0.065f, 0.36f, 0.025f), amber);
                    stripe.transform.localRotation = Quaternion.Euler(0, 0, -28);
                }
                Part("Top Light", details, new Vector3(0, 0.503f, 0), new Vector3(0.63f, 0.025f, 0.13f), amber);
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "ObstacleCube.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static GameObject Tower(int variant)
        {
            Material glass = MaterialAsset("Glass" + variant, "CubeDash/City Glass");
            Color[] tints = { new Color(0.20f, 0.28f, 0.39f), new Color(0.27f, 0.23f, 0.34f), new Color(0.24f, 0.32f, 0.41f) };
            glass.SetColor("_BaseColor", tints[variant]);
            glass.SetColor("_CoolWindows", new Color(0.28f, 0.63f, 0.82f));
            glass.SetColor("_WarmWindows", new Color(1.05f, 0.70f, 0.39f));
            glass.SetVector("_WindowSpacing", new Vector4(0.70f, 1.45f, 0, 0));
            glass.SetFloat("_LitWindows", 0.35f + variant * 0.07f);
            glass.SetFloat("_Seed", 3 + variant * 19);
            glass.enableInstancing = true;

            float[] heights = { 25f, 34f, 20f };
            float[] widths = { 3.8f, 4.4f, 5f };
            float height = heights[variant];
            float width = widths[variant];
            string path = Prefabs + "Skyscraper" + (variant + 1) + ".prefab";
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject("Skyscraper " + (variant + 1));
            try
            {
                Transform geometry = ManagedGroup(root.transform, "Tower Architecture");
                Part("Glass Facade", geometry, new Vector3(0, height * 0.5f, 0), new Vector3(width, height, 5), glass, false);
                Part("Setback Crown", geometry, new Vector3(0, height + 1.2f, 0), new Vector3(width * 0.7f, 2.4f, 3.5f), glass, false);
                Part("Roof Cap", geometry, new Vector3(0, height + 2.45f, 0), new Vector3(width * 0.76f, 0.16f, 3.7f), metal);
                Part("Antenna", geometry, new Vector3(0.5f, height + 3.9f, 0.4f), new Vector3(0.075f, 2.8f, 0.075f), metal);
                Part("Aircraft Beacon", geometry, new Vector3(0.5f, height + 5.35f, 0.4f), Vector3.one * 0.12f, amber);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Facade Mullion", geometry, new Vector3(side * width * 0.5f, height * 0.5f, -2.51f),
                        new Vector3(0.075f, height, 0.065f), metal, false);
                    Part("Crown Light", geometry, new Vector3(0, height + 2.4f, side * 1.84f),
                        new Vector3(width * 0.72f, 0.045f, 0.045f), coolTrim, false);
                }
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        private static void UpdateTrack(GameObject[] towers)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Prefabs + "TrackSegment.prefab");
            try
            {
                List<GameObject> oldBuildings = new List<GameObject>();
                foreach (Transform child in root.transform)
                    if (child.name == "Skyline Cube") oldBuildings.Add(child.gameObject);
                foreach (GameObject old in oldBuildings) Object.DestroyImmediate(old);
                Transform city = ManagedGroup(root.transform, "City Surroundings");
                for (int side = -1; side <= 1; side += 2)
                    for (int row = 0; row < 4; row++)
                    {
                        int variant = (row + (side == 1 ? 1 : 0)) % 3;
                        GameObject tower = (GameObject)PrefabUtility.InstantiatePrefab(towers[variant], root.scene);
                        tower.name = (side < 0 ? "West" : "East") + " Skyscraper " + (row + 1);
                        tower.transform.SetParent(city, false);
                        tower.transform.localPosition = new Vector3(side * (row == 3 ? 29f : 14.5f + row % 2 * 3.5f), -9f,
                            row == 3 ? 26f : 4f + row * 14f);
                        tower.transform.localScale = new Vector3(1, row == 3 ? 1.3f : 1, 1);
                    }
                Transform rails = ManagedGroup(root.transform, "Bridge Rails");
                Material plaza = Lit("CityPlaza", new Color(0.12f, 0.15f, 0.23f), 0.1f, 0.35f);
                Part("City Ground", rails, new Vector3(0, -9.3f, 21), new Vector3(68, 0.6f, 42), plaza, false);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Rail Base", rails, new Vector3(side * 4.12f, 0.1f, 21), new Vector3(0.15f, 0.2f, 42), metal, false);
                    for (int support = 0; support < 2; support++)
                        Part("Bridge Support", rails, new Vector3(side * 3f, -4.7f, 9f + support * 22f),
                            new Vector3(0.55f, 9f, 0.8f), metal, false);
                }
                foreach (Transform child in root.transform)
                {
                    if (child.name != "Edge Light") continue;
                    Vector3 position = child.localPosition;
                    position.y = 0.22f;
                    child.localPosition = position;
                    child.localScale = new Vector3(0.055f, 0.045f, 42);
                }
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "TrackSegment.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void AddWake(CubeDashGame game)
        {
            Material trailMaterial = MaterialAsset("Wake", "CubeDash/Wake Glow");
            trailMaterial.SetColor("_Tint", new Color(0.16f, 1.8f, 1.2f, 0.7f));
            string path = Prefabs + "PlayerTrail.prefab";
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject("Player Trail");
            GameObject prefab;
            try
            {
                CubeWake wake = root.GetComponent<CubeWake>();
                if (wake == null) wake = root.AddComponent<CubeWake>();
                Transform ribbon = ManagedGroup(root.transform, "Ribbon Pieces");
                Transform[] pieces = new Transform[12];
                for (int i = 0; i < pieces.Length; i++)
                {
                    float taper = 1f - i / 13f;
                    GameObject piece = Part("Wake " + i.ToString("00"), ribbon,
                        new Vector3(0, 0.018f, -0.6f - i * 0.34f), new Vector3(0.7f * taper, 0.018f, 0.37f), trailMaterial, false);
                    pieces[i] = piece.transform;
                }
                SetArray(wake, "pieces", pieces);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
            CubeWake instance = Object.FindAnyObjectByType<CubeWake>();
            if (instance == null) instance = ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponent<CubeWake>();
            Set(instance, "player", game.Player);
            Set(instance, "game", game);
            Set(game, "wake", instance);
        }

        private static void AddPostProcessing()
        {
            string path = "Assets/Settings/CubeDashVisuals.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "CubeDashVisuals";
                AssetDatabase.CreateAsset(profile, path);
            }
            Bloom bloom = Component<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.3f);
            bloom.scatter.Override(0.6f);
            Tonemapping tone = Component<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.ACES);
            ColorAdjustments color = Component<ColorAdjustments>(profile);
            color.postExposure.Override(0.15f);
            color.contrast.Override(7);
            color.saturation.Override(4);
            Vignette vignette = Component<Vignette>(profile);
            vignette.intensity.Override(0.12f);
            vignette.smoothness.Override(0.4f);
            EditorUtility.SetDirty(profile);
            GameObject volumeObject = GameObject.Find("City Atmosphere");
            if (volumeObject == null) volumeObject = new GameObject("City Atmosphere");
            Volume volume = volumeObject.GetComponent<Volume>();
            if (volume == null) volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 5;
            volume.sharedProfile = profile;
        }

        private static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static Transform ManagedGroup(Transform parent, string name)
        {
            Transform old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static GameObject Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool rounded = true)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            if (rounded) cube.GetComponent<MeshFilter>().sharedMesh = bevel;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
            return cube;
        }

        private static Mesh BeveledCube()
        {
            string path = "Assets/3D Models/CubeDashBeveledCube.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Vector2> uv = new List<Vector2>();
            const float outer = 0.5f;
            const float inner = 0.445f;
            Action<Vector3[]> face = points =>
            {
                Vector3 centre = Vector3.zero;
                foreach (Vector3 point in points) centre += point;
                if (Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), centre) < 0) Array.Reverse(points);
                int start = vertices.Count;
                foreach (Vector3 point in points) { vertices.Add(point); uv.Add(new Vector2(point.x + 0.5f, point.y + 0.5f)); }
                for (int i = 1; i < points.Length - 1; i++) { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
            };
            for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int u = (axis + 1) % 3, v = (axis + 2) % 3;
                    Vector3[] points = new Vector3[4];
                    int[] a = { -1, 1, 1, -1 }, b = { -1, -1, 1, 1 };
                    for (int i = 0; i < 4; i++) { points[i][axis] = sign * outer; points[i][u] = a[i] * inner; points[i][v] = b[i] * inner; }
                    face(points);
                }
            for (int a = 0; a < 3; a++)
                for (int b = a + 1; b < 3; b++)
                    for (int sa = -1; sa <= 1; sa += 2)
                        for (int sb = -1; sb <= 1; sb += 2)
                        {
                            int c = 3 - a - b;
                            Vector3[] points = new Vector3[4];
                            for (int i = 0; i < 4; i++)
                            {
                                bool first = i == 0 || i == 3;
                                points[i][a] = sa * (first ? outer : inner);
                                points[i][b] = sb * (first ? inner : outer);
                                points[i][c] = (i < 2 ? -1 : 1) * inner;
                            }
                            face(points);
                        }
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        face(new[] { new Vector3(x * outer, y * inner, z * inner), new Vector3(x * inner, y * outer, z * inner), new Vector3(x * inner, y * inner, z * outer) });
            Mesh mesh = new Mesh { name = "CubeDash Beveled Cube" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uv);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static void CapturePreview(Camera camera, string path)
        {
            RenderTexture target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create();
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }

        private static void Set(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Transform[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
