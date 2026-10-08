using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Authoring tool only. Saves normal scene objects, prefab assets, and materials.</summary>
    public static class CubeDashSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Level.unity";
        private const string PrefabFolder = "Assets/Prefab/CubeDash";
        private static readonly Color Mint = new Color(0.3f, 1f, 0.72f);
        private static readonly Color Cyan = new Color(0.12f, 0.92f, 1f);
        private static readonly Color Muted = new Color(0.65f, 0.74f, 0.86f);
        private static Font font;

        [MenuItem("Tools/Cube Dash/Set Up Level Scene")]
        public static void SetUpLevel()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildFromCommandLine();
        }

        public static void BuildFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (Object.FindAnyObjectByType<CubeDashGame>() != null)
                throw new InvalidOperationException("Level already contains Cube Dash. Existing authored objects will not be overwritten.");
            Camera camera = GameObject.Find("Main Camera")?.GetComponent<Camera>();
            GameObject plane = GameObject.Find("Plane");
            if (camera == null || plane == null)
                throw new InvalidOperationException("Level must contain its original Main Camera and Plane.");
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Prefab", "CubeDash");

            Material road = MaterialAsset("Road", new Color(0.065f, 0.085f, 0.14f), 0f);
            Material line = MaterialAsset("LaneLight", Cyan, 0.65f);
            Material obstacle = MaterialAsset("Obstacle", new Color(1f, 0.19f, 0.29f), 0.25f);
            Material scenery = MaterialAsset("Skyline", new Color(0.06f, 0.09f, 0.19f), 0f);
            Material playerMaterial = MaterialAsset("Player", Mint, 0.55f);

            GameObject playerPrefab = PlayerPrefab(playerMaterial);
            GameObject obstaclePrefab = ObstaclePrefab(obstacle, line);
            GameObject segmentPrefab = SegmentPrefab(road, line, scenery, obstaclePrefab);
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.name = "Player Cube";
            player.transform.position = new Vector3(0, 0.5f, 0);

            EndlessTrack track = new GameObject("Track").AddComponent<EndlessTrack>();
            TrackSegment[] segments = new TrackSegment[8];
            for (int i = 0; i < segments.Length; i++)
            {
                GameObject segment = (GameObject)PrefabUtility.InstantiatePrefab(segmentPrefab);
                segment.name = "Segment " + i.ToString("00");
                segment.transform.SetParent(track.transform, false);
                segment.transform.localPosition = new Vector3(0, 0, (i - 1) * RunnerRules.SegmentLength);
                segments[i] = segment.GetComponent<TrackSegment>();
            }
            SetArray(track, "segments", segments);

            // The user's existing Plane becomes the starting section, not a discarded placeholder.
            Object.DestroyImmediate(segments[1].transform.Find("Road").gameObject);
            plane.transform.SetParent(segments[1].transform, false);
            plane.transform.localPosition = new Vector3(0, 0, 21);
            plane.transform.localScale = new Vector3(0.84f, 1, 4.2f);
            plane.GetComponent<Renderer>().sharedMaterial = road;
            track.Reset(42);

            // Configure the existing camera in edit mode. Runtime scripts never create one.
            camera.transform.position = new Vector3(0, 6.7f, -11.5f);
            camera.transform.LookAt(new Vector3(0, 0.4f, 12));
            camera.fieldOfView = 65;
            camera.farClipPlane = 220;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.095f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.4f, 0.55f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogStartDistance = 65;
            RenderSettings.fogEndDistance = 165;

            CubeDashGame game = new GameObject("Game Manager").AddComponent<CubeDashGame>();
            RunnerHud hud = CreateHud(game);
            Set(game, "player", player.transform);
            Set(game, "playerCollider", player.GetComponent<BoxCollider>());
            Set(game, "gameCamera", camera);
            Set(game, "track", track);
            Set(game, "hud", hud);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Level.");
            Debug.Log("Cube Dash authored in Level: original Main Camera/Plane, Player Cube, eight Track segments, Canvas, and prefab/material assets.");
        }

        private static Material MaterialAsset(string name, Color color, float glow)
        {
            string path = "Assets/Material/CubeDash" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CubeDash" + name, color = color };
            material.enableInstancing = true;
            material.SetFloat("_Smoothness", 0.35f);
            if (glow > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * glow);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject PlayerPrefab(Material material)
        {
            string path = PrefabFolder + "/PlayerCube.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            GameObject cube = Box("Player Cube", null, Vector3.zero, Vector3.one * RunnerRules.PlayerSize, material);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(cube, path);
            Object.DestroyImmediate(cube);
            return prefab;
        }

        private static GameObject ObstaclePrefab(Material material, Material line)
        {
            string path = PrefabFolder + "/ObstacleCube.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            GameObject cube = Box("Obstacle Cube", null, Vector3.zero,
                new Vector3(RunnerRules.ObstacleWidth, 1.9f, RunnerRules.ObstacleDepth), material);
            GameObject stripe = Box("Warning Stripe", cube.transform, new Vector3(0, 0.2f, -0.506f),
                new Vector3(0.9f, 0.09f, 0.018f), line);
            Object.DestroyImmediate(stripe.GetComponent<BoxCollider>());
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(cube, path);
            Object.DestroyImmediate(cube);
            return prefab;
        }

        private static GameObject SegmentPrefab(Material road, Material line, Material scenery, GameObject obstaclePrefab)
        {
            string path = PrefabFolder + "/TrackSegment.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            GameObject root = new GameObject("Track Segment");
            TrackSegment segment = root.AddComponent<TrackSegment>();
            Box("Road", root.transform, new Vector3(0, -0.35f, 21), new Vector3(8.4f, 0.7f, 42), road);
            for (int side = -1; side <= 1; side += 2)
            {
                Decoration("Edge Light", root.transform, new Vector3(side * 4.02f, 0.02f, 21), new Vector3(0.12f, 0.08f, 42), line);
                for (int building = 0; building < 3; building++)
                {
                    float height = 3f + building * 3f;
                    Decoration("Skyline Cube", root.transform, new Vector3(side * (9f + building % 2 * 4), height * 0.5f - 3, building * 14 + 5),
                        new Vector3(2.4f, height, 3.5f), scenery);
                }
                for (int dash = 0; dash < 7; dash++)
                    Decoration("Lane Marker", root.transform, new Vector3(side * 1.3f, 0.015f, dash * 6 + 3), new Vector3(0.045f, 0.025f, 2.4f), line);
            }
            BoxCollider[] obstacles = new BoxCollider[9];
            string[] lanes = { "Left", "Centre", "Right" };
            for (int row = 0; row < 3; row++)
                for (int lane = 0; lane < 3; lane++)
                {
                    GameObject cube = (GameObject)PrefabUtility.InstantiatePrefab(obstaclePrefab);
                    cube.name = "Row " + (row + 1) + " - " + lanes[lane];
                    cube.transform.SetParent(root.transform, false);
                    cube.transform.localPosition = new Vector3((lane - 1) * 2.6f, 0.95f, 7 + row * 14);
                    obstacles[row * 3 + lane] = cube.GetComponent<BoxCollider>();
                }
            SetArray(segment, "obstacles", obstacles);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        private static void Decoration(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            Object.DestroyImmediate(Box(name, parent, position, scale, material).GetComponent<BoxCollider>());
        }

        private static RunnerHud CreateHud(CubeDashGame game)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RectTransform canvas = Rect("Canvas", null);
            canvas.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            RunnerHud hud = canvas.gameObject.AddComponent<RunnerHud>();
            Set(hud, "game", game);
            RectTransform safe = Rect("Safe Area", canvas);
            Stretch(safe);
            Set(hud, "safeRoot", safe);

            Text brand = Label("Brand", safe, "CUBE DASH", 20, Mint, TextAnchor.MiddleLeft);
            Place(brand.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(28, -35), new Vector2(200, 32));
            Text distance = Label("Distance", safe, "0 m", 44, Color.white, TextAnchor.MiddleLeft);
            Place(distance.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(28, -85), new Vector2(250, 60));
            Set(hud, "distance", distance);
            Text best = Label("Best", safe, "BEST  0 m", 17, Muted, TextAnchor.MiddleLeft);
            Place(best.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(30, -129), new Vector2(250, 30));
            Set(hud, "best", best);
            Text speed = Label("Speed", safe, "12 m/s", 18, Mint, TextAnchor.MiddleRight);
            Place(speed.rectTransform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-28, -103), new Vector2(180, 30));
            Set(hud, "speed", speed);
            GameObject pause = Button("Pause", safe, "PAUSE", new Color(0.12f, 0.18f, 0.26f), Color.white, game.TogglePause, out _);
            Place((RectTransform)pause.transform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-28, -42), new Vector2(116, 42));
            Set(hud, "pauseButton", pause);
            RectTransform controls = Rect("Lane Controls", safe);
            Stretch(controls);
            Set(hud, "controls", controls.gameObject);
            GameObject left = Button("Left", controls, "<", new Color(0.08f, 0.15f, 0.21f, 0.9f), Mint, hud.MoveLeft, out _);
            Place((RectTransform)left.transform, Vector2.zero, Vector2.zero, new Vector2(28, 26), new Vector2(88, 64));
            GameObject right = Button("Right", controls, ">", new Color(0.08f, 0.15f, 0.21f, 0.9f), Mint, hud.MoveRight, out _);
            Place((RectTransform)right.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 26), new Vector2(88, 64));
            Text hint = Label("Controls Hint", controls, "A / D  ·  ARROWS  ·  SWIPE", 15, Muted, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(320, 40));

            RectTransform overlay = Rect("Menu Overlay", safe);
            Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.02f, 0.045f, 0.78f);
            Set(hud, "overlay", overlay.gameObject);
            RectTransform card = Rect("Menu Card", overlay);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(600, 440));
            card.gameObject.AddComponent<Image>().color = new Color(0.045f, 0.075f, 0.13f, 0.98f);
            Set(hud, "card", card);
            RectTransform accent = Rect("Accent", card);
            Place(accent, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(90, 4));
            accent.gameObject.AddComponent<Image>().color = Mint;
            Text eyebrow = Label("Eyebrow", card, "ENDLESS / PROCEDURAL / CUBE", 15, Mint, TextAnchor.MiddleCenter);
            Place(eyebrow.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(360, 30));
            Text heading = Label("Heading", card, "CUBE DASH", 58, Color.white, TextAnchor.MiddleCenter);
            Place(heading.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -104), new Vector2(550, 86));
            heading.resizeTextForBestFit = true;
            heading.resizeTextMinSize = 30;
            heading.resizeTextMaxSize = 58;
            Set(hud, "heading", heading);
            Text description = Label("Description", card, "One cube. Infinite road.", 21, Muted, TextAnchor.MiddleCenter);
            Place(description.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -196), new Vector2(530, 100));
            Set(hud, "description", description);
            GameObject primary = Button("Primary Action", card, "LET'S DASH", Mint, new Color(0.025f, 0.045f, 0.09f), hud.PrimaryAction, out Text actionLabel);
            Place((RectTransform)primary.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 101), new Vector2(290, 60));
            Set(hud, "actionLabel", actionLabel);
            GameObject restart = Button("Restart", card, "RESTART RUN", new Color(0.1f, 0.15f, 0.22f), Muted, game.StartRun, out _);
            Place((RectTransform)restart.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 51), new Vector2(290, 38));
            Set(hud, "secondaryButton", restart);
            Text footer = Label("Keyboard Hint", card, "SPACE / ENTER to go   ·   P / ESC to pause", 15, Muted, TextAnchor.MiddleCenter);
            Place(footer.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 15), new Vector2(510, 30));
            overlay.gameObject.SetActive(false); // Editor Game view shows the real cube/track, not the menu.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule))
                .GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            return hud;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor alignment)
        {
            Text label = Rect(name, parent).gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.supportRichText = false;
            return label;
        }

        private static GameObject Button(string name, Transform parent, string caption, Color background,
            Color foreground, UnityAction callback, out Text label)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = background;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            UnityEventTools.AddPersistentListener(button.onClick, callback);
            label = Label("Label", rect, caption, 20, foreground, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            return rect.gameObject;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Set(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray<T>(Object target, string field, T[] values) where T : Object
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
