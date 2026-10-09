using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Authors the reference-inspired, non-neon color-matching scene and prefab assets.</summary>
    public static class CubeDashColorMatchStyle
    {
        private const string Prefabs = "Assets/Prefab/CubeDash/";
        private static readonly Color Ink = new Color(0.12f, 0.24f, 0.27f);
        private static readonly Color Muted = new Color(0.30f, 0.44f, 0.46f);
        private static readonly Color Red = new Color(0.88f, 0.25f, 0.22f);
        private static Mesh bevel;
        private static Material[] palette;

        [MenuItem("Tools/Cube Dash/Apply Clean Color-Match Style")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        [MenuItem("Tools/Cube Dash/Enhance Background City")]
        public static void ApplyBackgroundCityFromCommandLine()
        {
            UpdateBuildings();
            UpdateCityLayout();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Enhanced background city: detailed matte towers at double density (near, mid, and skyline rows).");
        }

        public static void ApplyFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            if (game == null) throw new InvalidOperationException("Level is not configured for Cube Dash.");
            bevel = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/CubeDashBeveledCube.asset");
            palette = new[] { Matte("Red", Red), Matte("Blue", new Color(0.18f, 0.43f, 0.82f)),
                Matte("Green", new Color(0.22f, 0.68f, 0.47f)) };
            Matte("Road", new Color(0.34f, 0.61f, 0.77f));
            Matte("LaneLight", new Color(0.66f, 0.81f, 0.84f));
            Matte("BuildingMetal", new Color(0.50f, 0.61f, 0.60f));
            Matte("CityPlaza", new Color(0.48f, 0.60f, 0.57f));
            UpdateCubePrefabs();
            UpdateBuildings();
            UpdateCityLayout();
            UpdateTrack();
            UpdateTrail();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SetArray(game.Track, "colorMaterials", palette);
            SetEnum(game, "playerCubeColor", (int)CubeColor.Red);
            SetFloat(game, "startSpeed", 14f);
            SetFloat(game, "maximumSpeed", 42f);
            SetFloat(game, "acceleration", 1.2f);
            game.Player.position = new Vector3(0, 0.595f, 0);
            game.Player.localScale = Vector3.one * 1.15f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(game.Player);
            game.Track.Reset(42, CubeColor.Red);
            foreach (TrackSegment segment in game.Track.Segments)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(segment.transform);
                foreach (RunnerCube cube in segment.Cubes)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(cube);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(cube.GetComponent<Renderer>());
                    PrefabUtility.RecordPrefabInstancePropertyModifications(cube.gameObject);
                }
            }

            Camera camera = game.GameCamera;
            camera.transform.position = new Vector3(0, 4f, -7.5f);
            camera.transform.LookAt(new Vector3(0, 0.75f, 15));
            camera.fieldOfView = 56;
            camera.farClipPlane = 600;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            Material sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/CubeDashHorizon.mat");
            sky.SetColor("_ZenithColor", new Color(0.47f, 0.72f, 0.84f));
            sky.SetColor("_HorizonColor", new Color(0.77f, 0.89f, 0.85f));
            sky.SetColor("_GroundColor", new Color(0.64f, 0.78f, 0.75f));
            sky.SetColor("_SunColor", Color.black);
            sky.SetFloat("_GradientPower", 0.7f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.fogColor = new Color(0.77f, 0.89f, 0.85f);
            RenderSettings.fogStartDistance = 45;
            RenderSettings.fogEndDistance = 260;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.70f, 0.82f, 0.84f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.66f, 0.67f);
            RenderSettings.ambientGroundColor = new Color(0.40f, 0.51f, 0.54f);
            Light key = GameObject.Find("Directional Light").GetComponent<Light>();
            key.color = new Color(1f, 0.96f, 0.89f);
            key.intensity = 2.1f;
            key.transform.rotation = Quaternion.Euler(48, -35, 0);
            key.shadows = LightShadows.Soft;
            UpdateAtmosphere();
            StyleCanvas();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Saved clean matte color-match city: collect your color, avoid others. Original Main Camera retained.");
        }

        private static Material Matte(string name, Color color)
        {
            string path = "Assets/Material/CubeDash" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (material == null) { material = new Material(shader) { name = "CubeDash" + name }; AssetDatabase.CreateAsset(material, path); }
            else material.shader = shader;
            material.color = color;
            material.enableInstancing = true;
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", 0.18f);
            material.SetFloat("_SpecularHighlights", 0);
            material.SetFloat("_EnvironmentReflections", 0);
            material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void UpdateCubePrefabs()
        {
            string[] names = { "PlayerCube.prefab", "ObstacleCube.prefab" };
            for (int i = 0; i < names.Length; i++)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(Prefabs + names[i]);
                try
                {
                    Remove(root.transform, "Cube Face Details");
                    Remove(root.transform, "Barrier Details");
                    Remove(root.transform, "Warning Stripe");
                    root.transform.localScale = Vector3.one * 1.15f;
                    root.GetComponent<MeshFilter>().sharedMesh = bevel;
                    root.GetComponent<Renderer>().sharedMaterial = palette[i == 0 ? 0 : 1];
                    root.GetComponent<BoxCollider>().size = Vector3.one;
                    if (i == 1)
                    {
                        root.name = "Color Cube";
                        RunnerCube cube = root.GetComponent<RunnerCube>();
                        if (cube == null) cube = root.AddComponent<RunnerCube>();
                        cube.Configure(CubeColor.Blue, palette[1]);
                        Set(cube, "cubeRenderer", root.GetComponent<Renderer>());
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, Prefabs + names[i]);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static void UpdateBuildings()
        {
            Color[] colors = { new Color(0.46f, 0.57f, 0.56f), new Color(0.57f, 0.65f, 0.61f), new Color(0.40f, 0.51f, 0.52f) };
            float[] heights = { 26, 34, 21 };
            float[] widths = { 5.2f, 5f, 6f };
            for (int variant = 0; variant < 3; variant++)
            {
                string path = Prefabs + "Skyscraper" + (variant + 1) + ".prefab";
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Remove(root.transform, "Tower Architecture");
                    Remove(root.transform, "Matte Architecture");
                    Transform architecture = new GameObject("Matte Architecture").transform;
                    architecture.SetParent(root.transform, false);
                    Material concrete = Matte("CityConcrete" + variant, colors[variant]);
                    Material ledge = Matte("CityLedge" + variant, Color.Lerp(colors[variant], Color.white, 0.12f));
                    float h = heights[variant], w = widths[variant];

                    // Street-level podium anchors the tower to the plaza slab.
                    Part("Street Podium", architecture, new Vector3(0, 1.7f, 0), new Vector3(w + 1.6f, 3.4f, 7.4f), ledge);
                    Part("Podium Cornice", architecture, new Vector3(0, 3.55f, 0), new Vector3(w + 1.9f, 0.3f, 7.7f), concrete);

                    // Shaft wrapped in horizontal floor bands for readable relief at distance.
                    Part("Tower Body", architecture, new Vector3(0, h * 0.5f, 0), new Vector3(w, h, 6), concrete);
                    for (int band = 0; band < 4; band++)
                        Part("Floor Band " + (band + 1), architecture, new Vector3(0, 5f + band * (h - 10f) / 3f, 0),
                            new Vector3(w + 0.3f, 0.28f, 6.3f), ledge);

                    // Stepped crown: setback, smaller crown, parapet, plant room, and mast.
                    float crownX = variant == 1 ? 0.5f : 0;
                    Part("Upper Setback", architecture, new Vector3(crownX, h + 1.8f, 0), new Vector3(w * 0.72f, 3.6f, 4.6f), ledge);
                    Part("Crown Setback", architecture, new Vector3(crownX, h + 4.9f, 0), new Vector3(w * 0.5f, 2.6f, 3.4f), concrete);
                    Part("Roof Edge", architecture, new Vector3(crownX, h + 6.35f, 0), new Vector3(w * 0.56f, 0.3f, 3.7f), ledge);
                    Part("Roof Unit", architecture, new Vector3(crownX + w * 0.1f, h + 7.15f, 0.4f), new Vector3(w * 0.24f, 1.3f, 1.6f), concrete);
                    Part("Roof Mast", architecture, new Vector3(crownX, h + 8.3f, -0.6f), new Vector3(0.16f, 4.2f, 0.16f), ledge);

                    if (variant == 2)
                    {
                        Part("Low Annex", architecture, new Vector3(w * 0.7f, 4.6f, 0.6f), new Vector3(w * 0.6f, 9.2f, 5.2f), ledge);
                        Part("Annex Roof", architecture, new Vector3(w * 0.7f, 9.35f, 0.6f), new Vector3(w * 0.68f, 0.3f, 5.6f), concrete);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static void UpdateCityLayout()
        {
            // Four depth rows per side: near street, inner street, mid blocks, and a taller
            // skyline row on the widened plaza — eight towers per side, sixteen per segment.
            float[] rowX = { 14.5f, 14.5f, 18.5f, 18.5f, 29f, 29f, 41f, 41f };
            float[] rowZ = { 4f, 26f, 15f, 37f, 10f, 31f, 5f, 27f };
            float[] rowScale = { 1f, 1.05f, 0.95f, 1.15f, 1.2f, 1f, 1.35f, 1.15f };
            GameObject[] towers = new GameObject[3];
            for (int i = 0; i < towers.Length; i++)
            {
                towers[i] = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Skyscraper" + (i + 1) + ".prefab");
                if (towers[i] == null) throw new InvalidOperationException("Missing Skyscraper" + (i + 1) + " prefab.");
            }
            string path = Prefabs + "TrackSegment.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform stale = root.transform.Find("City Surroundings");
                if (stale != null) Object.DestroyImmediate(stale.gameObject);
                Transform city = new GameObject("City Surroundings").transform;
                city.SetParent(root.transform, false);
                for (int side = -1; side <= 1; side += 2)
                    for (int slot = 0; slot < rowX.Length; slot++)
                    {
                        int variant = (slot + (side == 1 ? 1 : 0)) % 3;
                        GameObject tower = (GameObject)PrefabUtility.InstantiatePrefab(towers[variant], root.scene);
                        tower.name = (side < 0 ? "West" : "East") + " Skyscraper " + (slot + 1);
                        tower.transform.SetParent(city, false);
                        tower.transform.localPosition = new Vector3(side * rowX[slot], -9f, rowZ[slot]);
                        tower.transform.localScale = new Vector3(1, rowScale[slot], 1);
                    }
                Transform ground = root.transform.Find("Bridge Rails/City Ground");
                if (ground == null) throw new InvalidOperationException("TrackSegment lost its City Ground plaza slab.");
                ground.localScale = new Vector3(100, 0.6f, 42);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("City Surroundings rebuilt with " + (rowX.Length * 2) + " skyscrapers per segment.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void UpdateTrack()
        {
            string path = Prefabs + "TrackSegment.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                TrackSegment segment = root.GetComponent<TrackSegment>();
                RunnerCube[] cubes = new RunnerCube[9];
                for (int i = 0; i < cubes.Length; i++)
                {
                    GameObject tile = segment.Obstacles[i].gameObject;
                    cubes[i] = tile.GetComponent<RunnerCube>();
                    if (cubes[i] == null) throw new InvalidOperationException("Color cube component was not propagated into TrackSegment.");
                    tile.transform.localScale = Vector3.one * 1.15f;
                    Vector3 position = tile.transform.localPosition;
                    position.y = 0.575f;
                    tile.transform.localPosition = position;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(tile.transform);
                }
                SetArray(segment, "cubes", cubes);
                List<Transform> remove = new List<Transform>();
                foreach (Transform child in root.transform)
                    if (child.name == "Lane Marker" || child.name == "Edge Light") remove.Add(child);
                foreach (Transform child in remove) Object.DestroyImmediate(child.gameObject);
                Remove(root.transform, "Matte Road Edges");
                Transform trim = new GameObject("Matte Road Edges").transform;
                trim.SetParent(root.transform, false);
                Material edge = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/CubeDashLaneLight.mat");
                for (int side = -1; side <= 1; side += 2)
                    Part("Road Edge", trim, new Vector3(side * 4.02f, 0.015f, 21), new Vector3(0.06f, 0.03f, 42), edge);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void UpdateTrail()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/CubeDashWake.mat");
            material.SetColor("_Tint", new Color(Red.r, Red.g, Red.b, 0.12f));
            EditorUtility.SetDirty(material);
            string path = Prefabs + "PlayerTrail.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                CubeWake wake = root.GetComponent<CubeWake>();
                for (int i = 0; i < wake.Pieces.Length; i++)
                {
                    wake.Pieces[i].localPosition = new Vector3(0, 0.018f, -0.75f - i * 0.14f);
                    wake.Pieces[i].localScale = new Vector3(0.65f * (1 - i / 13f), 0.012f, 0.16f);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void UpdateAtmosphere()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/CubeDashVisuals.asset");
            if (profile.TryGet(out Bloom bloom)) { bloom.active = false; bloom.intensity.Override(0); EditorUtility.SetDirty(bloom); }
            if (profile.TryGet(out Tonemapping tone)) { tone.mode.Override(TonemappingMode.Neutral); EditorUtility.SetDirty(tone); }
            if (profile.TryGet(out ColorAdjustments color))
            {
                color.postExposure.Override(0);
                color.contrast.Override(3);
                color.saturation.Override(-2);
                EditorUtility.SetDirty(color);
            }
            if (profile.TryGet(out Vignette vignette)) { vignette.intensity.Override(0.04f); EditorUtility.SetDirty(vignette); }
            EditorUtility.SetDirty(profile);
        }

        private static void StyleCanvas()
        {
            Transform safe = GameObject.Find("Canvas").transform.Find("Safe Area");
            foreach (Text text in safe.GetComponentsInChildren<Text>(true)) text.color = Ink;
            foreach (Button button in safe.GetComponentsInChildren<Button>(true))
            {
                button.GetComponent<Image>().color = new Color(0.93f, 0.97f, 0.96f, 0.95f);
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.88f, 0.94f, 0.92f);
                colors.pressedColor = new Color(0.76f, 0.86f, 0.83f);
                button.colors = colors;
            }
            safe.Find("Brand").GetComponent<Text>().text = "CUBE DASH";
            safe.Find("Best").GetComponent<Text>().color = Muted;
            safe.Find("Speed").GetComponent<Text>().color = Red;
            RectTransform speed = (RectTransform)safe.Find("Speed");
            speed.sizeDelta = new Vector2(290, 30);
            Transform overlay = safe.Find("Menu Overlay");
            overlay.GetComponent<Image>().color = Color.clear;
            Transform card = overlay.Find("Menu Card");
            card.GetComponent<Image>().color = Color.clear;
            RectTransform cardRect = (RectTransform)card;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.64f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(600, 320);
            card.Find("Accent").gameObject.SetActive(false);
            Text eyebrow = card.Find("Eyebrow").GetComponent<Text>();
            eyebrow.text = "SAME COLOR. KEEP GOING.";
            eyebrow.color = Muted;
            Top(eyebrow.rectTransform, -18, 30);
            Text heading = card.Find("Heading").GetComponent<Text>();
            heading.fontStyle = FontStyle.Bold;
            heading.resizeTextMaxSize = 48;
            Top(heading.rectTransform, -64, 70);
            Text description = card.Find("Description").GetComponent<Text>();
            description.color = Ink;
            description.fontSize = 19;
            Top(description.rectTransform, -141, 75);
            RectTransform primary = (RectTransform)card.Find("Primary Action");
            primary.anchoredPosition = new Vector2(0, 70);
            primary.sizeDelta = new Vector2(260, 50);
            primary.GetComponent<Image>().color = Red;
            primary.GetComponentInChildren<Text>().color = Color.white;
            RectTransform restart = (RectTransform)card.Find("Restart");
            restart.anchoredPosition = new Vector2(0, 22);
            restart.sizeDelta = new Vector2(260, 34);
            RectTransform footer = (RectTransform)card.Find("Keyboard Hint");
            footer.anchoredPosition = new Vector2(0, -8);
            footer.GetComponent<Text>().fontSize = 13;
            footer.GetComponent<Text>().color = Muted;
        }

        private static void Top(RectTransform rect, float y, float height)
        {
            rect.anchoredPosition = new Vector2(0, y);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        private static void Remove(Transform root, string name)
        {
            Transform child = root.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        private static void Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
        }

        private static void Set(Object target, string name, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray<T>(Object target, string name, T[] values) where T : Object
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(name);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string name, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(name).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string name, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(name).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
