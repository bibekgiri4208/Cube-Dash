using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Upgrades only the skin shop/player cosmetics; user vehicle prefabs and gameplay maps are never saved.</summary>
    public static class MainMenuSkinsRevamp
    {
        public const string MonsterPath = "Assets/Prefab/CubeDash/Monster Truck.prefab";
        public const string MilitaryPath = "Assets/Prefab/CubeDash/Military Van.prefab";
        public const string PlayerPath = "Assets/Prefab/CubeDash/PlayerCube.prefab";
        public const string ArtFolder = "Assets/MainMenu/Shop Skins";
        private static readonly Color Ink = new Color(0.018f, 0.014f, 0.025f);
        private static readonly Color Cream = new Color(1, 0.99f, 0.94f);
        private static readonly PlayerSkin[] Skins = { PlayerSkin.Supercar, PlayerSkin.MonsterTruck, PlayerSkin.MilitaryVan };
        public static string IllustrationPath(PlayerSkin skin) => skin == PlayerSkin.Supercar ? MainMenuEconomyAuthoring.IllustrationPath
            : ArtFolder + (skin == PlayerSkin.MonsterTruck ? "/Monster Truck.png" : "/Military Van.png");
        private static string PrefabPath(PlayerSkin skin) => skin == PlayerSkin.Supercar ? "Assets/Prefab/CubeDash/Supercar.prefab"
            : skin == PlayerSkin.MonsterTruck ? MonsterPath : MilitaryPath;

        [MenuItem("Tools/Cube Dash/Revamp Shop with Vehicle Skins")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }
        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            ValidateSources();
            AuthorPlayer();
            VehicleSkinFinish.ApplyToPlayer();
            string originalScene = File.ReadAllText(MainMenuSceneBuilder.ScenePath);
            Scene scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            VehicleSkinFinish.ApplyToMenu(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the skin shop.");
            PreserveUnchangedDocuments(MainMenuSceneBuilder.ScenePath, originalScene);
            AssetDatabase.SaveAssets();
        }

        private static void ValidateSources()
        {
            foreach (var skin in Skins)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(skin)) == null)
                    throw new InvalidOperationException("Missing skin prefab: " + PrefabPath(skin));
        }

        public static void ApplyToScene(Scene scene)
        {
            var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
            var shop = menu.GetComponent<MenuSkinShop>();
            if (shop == null) throw new InvalidOperationException("The saved wallet/shop is required first.");
            if (menu.ShopPanel.GetComponent<MenuSkinsLayout>() != null) return; // Preserve manual edits on reapplication.
            ValidateSources();
            if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/MainMenu", "Shop Skins");
            foreach (var skin in Skins.Skip(1)) BakeIllustration(skin);
            var card = menu.ShopPanel.transform.Find("Card");
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MainMenu/Fonts/Anton-Regular.ttf");
            // The old rarity containers are superseded, not retained as three inactive categories.
            foreach (string name in new[] { "Common Skins", "Rare Skins", "Epic Skins" })
            {
                Transform old = card.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var viewport = Rect("Skins", card, new Vector2(0, -35), new Vector2(1064, 320));
            viewport.gameObject.AddComponent<RectMask2D>();
            // Transparent raycast surface allows scrolling by dragging empty grid space as well as cards.
            var surface = viewport.gameObject.AddComponent<Image>(); surface.color = Color.clear;
            var slots = Rect("Skin slots", viewport, Vector2.zero, new Vector2(0, 0));
            slots.anchorMin = new Vector2(0, 1); slots.anchorMax = Vector2.one; slots.pivot = new Vector2(0.5f, 1);
            var grid = slots.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(344, 352); grid.spacing = Vector2.one * 16;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;
            slots.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = slots;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            var cards = new MenuSkinCard[3]; var contents = new RectTransform[3];
            for (int i = 0; i < Skins.Length; i++)
            {
                PlayerSkin skin = Skins[i];
                var product = Rect(GameEconomy.SkinName(skin), slots, Vector2.zero, new Vector2(328, 336));
                var panel = product.gameObject.AddComponent<MenuPanelGraphic>();
                panel.color = new Color(1, 0.91f, 0.92f); panel.BorderColor = Ink; panel.BorderWidth = 3; panel.CornerCut = 12;
                var content = contents[i] = Rect("Contents", product, Vector2.zero, new Vector2(328, 336));
                Label("Name", content, font, GameEconomy.SkinName(skin), 28, new Vector2(0, 132), new Vector2(304, 44));
                var image = Rect("Model Illustration", content, new Vector2(0, 28), new Vector2(302, 162)).gameObject.AddComponent<Image>();
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IllustrationPath(skin)); image.preserveAspect = true; image.raycastTarget = false;
                Label("Price", content, font, "100 COINS", 23, new Vector2(0, -65), new Vector2(294, 32));
                var state = Label("Status", content, font, "NEED 100 MORE COINS", 17, new Vector2(0, -95), new Vector2(302, 26));
                var buy = Rect("Purchase Equip", content, new Vector2(0, -135), new Vector2(216, 52));
                var ribbon = buy.gameObject.AddComponent<MenuRibbonGraphic>(); ribbon.Primary = true;
                var action = buy.gameObject.AddComponent<Button>(); action.targetGraphic = ribbon; action.transition = Selectable.Transition.None;
                buy.gameObject.AddComponent<ArcadeButton>();
                var label = Label("Label", buy, font, "BUY", 28, new Vector2(-4, 1), new Vector2(200, 48)); label.color = Cream;
                var item = cards[i] = buy.gameObject.AddComponent<MenuSkinCard>();
                Set(item, "shop", shop); Set(item, "action", action); Set(item, "actionLabel", label); Set(item, "status", state);
                var data = new SerializedObject(item); data.FindProperty("skin").enumValueIndex = (int)skin; data.ApplyModifiedPropertiesWithoutUndo();
                GameObject preview = skin == PlayerSkin.Supercar ? shop.CarPreview : MenuPreview(menu, skin, scene);
                Set(item, "preview", preview);
                UnityEventTools.AddPersistentListener(action.onClick, item.PurchaseOrToggle);
            }
            SetArray(shop, "cards", cards); Set(shop, "action", cards[0].Action);
            Set(shop, "actionLabel", cards[0].Action.GetComponentInChildren<Text>());
            var layout = menu.ShopPanel.AddComponent<MenuSkinsLayout>();
            foreach (var entry in new[] { ("card", card), ("shadow", menu.ShopPanel.transform.Find("Soft card shadow")),
                ("header", card.Find("Ribbon header")), ("heading", card.Find("Heading")), ("icon", card.Find("Section icon")),
                ("divider", card.Find("Header divider")), ("viewport", (Transform)viewport), ("wallet", shop.ShopCoins.transform.parent),
                ("status", card.Find("Shop Economy Status")), ("back", card.Find("BACK")) }) Set(layout, entry.Item1, entry.Item2.GetComponent<RectTransform>());
            Set(layout, "grid", grid); SetArray(layout, "contents", contents); SetArray(layout, "actions", cards.Select(item => item.Action).ToArray());
            var headingText = card.Find("Heading").GetComponent<Text>();
            headingText.text = "SKINS"; headingText.resizeTextForBestFit = true;
            headingText.resizeTextMinSize = 12; headingText.resizeTextMaxSize = 50;
            headingText.alignment = TextAnchor.MiddleLeft;
            // Never bake the developer's real wallet/ownership into the shared scene.
            card.Find("Shop Economy Status").GetComponent<Text>().text = "";
            // Author a useful desktop layout while leaving modal visibility/animation unchanged.
            layout.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static GameObject MenuPreview(MainMenuController menu, PlayerSkin skin, Scene scene)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(skin)), scene);
            instance.name = "Equipped " + GameEconomy.SkinName(skin) + " Preview";
            Transform cube = menu.Hero.transform.parent;
            instance.transform.SetParent(cube.parent, false);
            instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = Quaternion.identity; instance.transform.localScale = Vector3.one;
            float scale = VehicleSkinFinish.ModelScale;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.rotation = Quaternion.Euler(0, -145, 0);
            instance.transform.position = menu.Hero.bounds.center - Vector3.up * menu.Hero.bounds.extents.y;
            Bounds world = RendererBounds(instance);
            instance.transform.position += Vector3.up * (menu.Hero.bounds.min.y - world.min.y);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
            instance.SetActive(false);
            return instance;
        }

        public static void AuthorPlayer()
        {
            string originalPlayer = File.ReadAllText(PlayerPath);
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var presentation = player.GetComponent<PlayerSupercar>();
                if (presentation.AdditionalSkins.Length == 2) return;
                if (presentation.AdditionalSkins.Length != 0 || player.transform.Find("Monster Truck Skin") != null || player.transform.Find("Military Van Skin") != null)
                    throw new InvalidOperationException("Partial/custom player skins require inspection.");
                var data = new SerializedObject(presentation); var models = data.FindProperty("additionalSkins"); models.arraySize = 2;
                for (int i = 0; i < 2; i++)
                {
                    PlayerSkin skin = Skins[i + 1];
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(skin)), player.scene);
                    instance.name = skin == PlayerSkin.MonsterTruck ? "Monster Truck Skin" : "Military Van Skin";
                    instance.transform.SetParent(player.transform, false);
                    instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = Quaternion.identity; instance.transform.localScale = Vector3.one;
                    Bounds bounds = LocalBounds(instance.transform, player.transform);
                    float scale = VehicleSkinFinish.ModelScale;
                    instance.transform.localScale = Vector3.one * scale;
                    instance.transform.localPosition = new Vector3(-bounds.center.x * scale, -0.575f - bounds.min.y * scale, -bounds.center.z * scale);
                    foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                    foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
                    Transform[] wheels = new[] { "FL Tire", "FR Tire", "BL Tire", "BR Tire" }.Select(suffix =>
                        instance.GetComponentsInChildren<Transform>(true).Single(t => t.name.EndsWith(suffix, StringComparison.Ordinal))).ToArray();
                    var model = models.GetArrayElementAtIndex(i); model.FindPropertyRelative("skin").enumValueIndex = (int)skin;
                    model.FindPropertyRelative("root").objectReferenceValue = instance.transform;
                    var wheelData = model.FindPropertyRelative("wheels"); wheelData.arraySize = wheels.Length;
                    for (int w = 0; w < wheels.Length; w++) wheelData.GetArrayElementAtIndex(w).objectReferenceValue = wheels[w];
                    model.FindPropertyRelative("wheelRadius").floatValue = wheels[0].GetComponent<MeshFilter>().sharedMesh.bounds.extents.y;
                    instance.SetActive(false);
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            PreserveUnchangedDocuments(PlayerPath, originalPlayer);
            // The menu-only cube variant must not inherit gameplay vehicle meshes, even inactive ones.
            const string previewPath = "Assets/Prefab/CubeDash/PlayerCubeMenuPreview.prefab";
            var preview = PrefabUtility.LoadPrefabContents(previewPath);
            try
            {
                foreach (string name in new[] { "Monster Truck Skin", "Military Van Skin" })
                { var child = preview.transform.Find(name); if (child != null) Object.DestroyImmediate(child.gameObject); }
                PrefabUtility.SaveAsPrefabAsset(preview, previewPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(preview); }
        }

        public static Bounds LocalBounds(Transform model, Transform relativeTo)
        {
            bool first = true; Bounds bounds = default;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 matrix = relativeTo.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                }
            }
            if (first) throw new InvalidOperationException("Skin has no mesh geometry.");
            return bounds;
        }
        private static Bounds RendererBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true); Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void BakeIllustration(PlayerSkin skin)
        {
            string path = IllustrationPath(skin);
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null) return;
            Scene previous = SceneManager.GetActiveScene();
            Scene preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(preview);
            RenderTexture target = null; Texture2D texture = null; RenderTexture active = RenderTexture.active;
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(skin)));
                model.transform.position = Vector3.zero; model.transform.rotation = Quaternion.identity; model.transform.localScale = Vector3.one;
                foreach (Transform child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                Bounds bounds = RendererBounds(model);
                var camera = new GameObject("Skin Illustration Camera").AddComponent<Camera>();
                camera.enabled = false; camera.cullingMask = 1 << 31; camera.orthographic = true;
                camera.transform.position = bounds.center + new Vector3(1.1f, 0.72f, 1.4f).normalized * bounds.size.magnitude * 2;
                camera.transform.LookAt(bounds.center);
                camera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.z * 0.62f) * 0.78f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(1, 0.91f, 0.92f); camera.allowHDR = false;
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.65f, 0.67f, 0.72f);
                var light = new GameObject("Skin Illustration Sun").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.5f; light.cullingMask = 1 << 31;
                light.transform.rotation = Quaternion.Euler(40, -35, 0); RenderSettings.sun = light;
                target = new RenderTexture(900, 520, 24); camera.targetTexture = target;
                for (int frame = 0; frame < 6; frame++) camera.Render();
                RenderTexture.active = target; texture = new Texture2D(900, 520, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 900, 520), 0, 0); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            }
            finally
            {
                RenderTexture.active = active; SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(preview, true);
                if (target != null) Object.DestroyImmediate(target); if (texture != null) Object.DestroyImmediate(texture);
                ShaderUtil.allowAsyncCompilation = async;
            }
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private static Text Label(string name, Transform parent, Font font, string text, int size, Vector2 position, Vector2 dimensions)
        {
            var label = Rect(name, parent, position, dimensions).gameObject.AddComponent<MenuLabelText>();
            label.font = font; label.text = text; label.fontSize = size; label.color = Ink; label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = size; label.raycastTarget = false; return label;
        }
        private static void Set(Object target, string field, Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetArray<T>(Object target, string field, T[] values) where T : Object
        {
            var data = new SerializedObject(target); var array = data.FindProperty(field); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void PreserveUnchangedDocuments(string path, string original)
        {
            const string pattern = @"(?ms)^--- !u!\d+ &(-?\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)";
            var documents = Regex.Matches(original, pattern).Cast<Match>().ToDictionary(match => match.Groups[1].Value, match => match.Value);
            string Normalize(string document) => Regex.Replace(document, @"[ \t]+(?=\r?$)", "", RegexOptions.Multiline).Replace("\r\n", "\n");
            string saved = File.ReadAllText(path);
            string retained = Regex.Replace(saved, pattern, match => documents.TryGetValue(match.Groups[1].Value, out string old) &&
                Normalize(old) == Normalize(match.Value) ? old : match.Value);
            if (retained == saved) return;
            File.WriteAllText(path, retained);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
