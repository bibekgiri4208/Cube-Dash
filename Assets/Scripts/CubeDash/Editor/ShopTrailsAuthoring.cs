using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    public static class ShopTrailsAuthoring
    {
        public const string MaterialPath = "Assets/Material/Trails/Cosmetic Trail.mat";
        [MenuItem("Tools/Cube Dash/Add Shop Trails Section")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            PreparePlayer();
            string original = File.ReadAllText(MainMenuSceneBuilder.ScenePath);
            var scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene); EditorSceneManager.SaveScene(scene);
            MainMenuSkinsRevamp.PreserveUnchangedDocuments(MainMenuSceneBuilder.ScenePath, original);
            AssetDatabase.SaveAssets();
        }

        public static void PreparePlayer()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Material/Trails")) AssetDatabase.CreateFolder("Assets/Material", "Trails");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("CubeDash/Shop Cosmetic Trail");
                if (shader == null) throw new InvalidOperationException("Missing cosmetic trail shader.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath);
            }
            string path = PlayerCubePresentationBuilder.PlayerPath, original = File.ReadAllText(path);
            var player = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (player.GetComponentInChildren<PlayerTrailEffect>(true) == null)
                {
                    if (player.transform.Find("Cosmetic Trail") != null) throw new InvalidOperationException("Inspect the custom Cosmetic Trail object first.");
                    var effect = new GameObject("Cosmetic Trail", typeof(PlayerTrailEffect)); effect.transform.SetParent(player.transform, false);
                    var renderer = effect.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.enabled = false;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    PrefabUtility.SaveAsPrefabAsset(player, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            MainMenuSkinsRevamp.PreserveUnchangedDocuments(path, original);
            // The menu-only cube (and its Shark variant) must not inherit a gameplay ribbon or extra scripts.
            path = PlayerCubePresentationBuilder.PreviewPath; original = File.ReadAllText(path);
            var preview = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var trail = preview.GetComponentInChildren<PlayerTrailEffect>(true);
                if (trail != null)
                {
                    Object.DestroyImmediate(trail.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(preview, path);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(preview); }
            MainMenuSkinsRevamp.PreserveUnchangedDocuments(path, original);
        }

        public static void ApplyToScene(Scene scene)
        {
            var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
            if (menu.ShopPanel.GetComponent<MenuShopSections>() != null) return;
            var shop = menu.GetComponent<MenuSkinShop>();
            if (shop == null || shop.Cards.Length == 0) throw new InvalidOperationException("The unified skin shop is required.");
            Transform card = menu.ShopPanel.transform.Find("Card");
            if (card.Find("Trails") != null || card.Find("Shop categories") != null) throw new InvalidOperationException("Inspect the partial shop categories before authoring.");
            var sections = menu.ShopPanel.AddComponent<MenuShopSections>();
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MainMenu/Fonts/Anton-Regular.ttf");
            var tabs = Rect("Shop categories", card, Vector2.zero, new Vector2(420, 48));
            Button skins = Tab(tabs, "SKINS", font, sections.ShowSkins, new Vector2(0, 0), new Vector2(0.48f, 1));
            Button trails = Tab(tabs, "TRAILS", font, sections.ShowTrails, new Vector2(0.52f, 0), Vector2.one);
            var viewport = Rect("Trails", card, new Vector2(0, -58), new Vector2(1032, 292)); viewport.gameObject.SetActive(false);
            viewport.gameObject.AddComponent<RectMask2D>(); viewport.gameObject.AddComponent<Image>().color = Color.clear;
            var slots = Rect("Trail slots", viewport, Vector2.zero, Vector2.zero);
            slots.anchorMin = new Vector2(0, 1); slots.anchorMax = Vector2.one; slots.pivot = new Vector2(0.5f, 1);
            var grid = slots.gameObject.AddComponent<GridLayoutGroup>(); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4; grid.cellSize = new Vector2(246, 252); grid.spacing = Vector2.one * 16;
            grid.childAlignment = TextAnchor.UpperCenter;
            slots.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = slots;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            var cards = new MenuTrailCard[4]; var contents = new RectTransform[4]; var actions = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var style = (PlayerTrail)(i + 1);
                var product = Object.Instantiate(shop.Cards[0].transform.parent.parent.gameObject, slots);
                product.name = TrailStyles.Name(style);
                Transform content = product.transform.Find("Contents"); contents[i] = content.GetComponent<RectTransform>();
                content.Find("Name").GetComponent<Text>().text = TrailStyles.Name(style);
                content.Find("Price").GetComponent<Text>().text = "250 COINS";
                Text status = content.Find("Status").GetComponent<Text>(); status.text = "NEED 250 MORE COINS";
                Transform image = content.Find("Model Illustration"); Object.DestroyImmediate(image.GetComponent<Image>());
                image.name = "Live Trail Preview";
                var graphic = image.gameObject.AddComponent<MenuTrailGraphic>(); graphic.raycastTarget = false;
                var data = new SerializedObject(graphic); data.FindProperty("style").intValue = (int)style; data.ApplyModifiedPropertiesWithoutUndo();
                var old = product.GetComponentInChildren<MenuSkinCard>(true); Button button = old.Action;
                Object.DestroyImmediate(old); button.onClick = new Button.ButtonClickedEvent();
                var item = cards[i] = button.gameObject.AddComponent<MenuTrailCard>(); actions[i] = button;
                Set(item, "sections", sections); Set(item, "action", button); Set(item, "status", status);
                Text label = button.GetComponentInChildren<Text>(true); label.text = "BUY"; Set(item, "actionLabel", label);
                data = new SerializedObject(item); data.FindProperty("style").intValue = (int)style; data.ApplyModifiedPropertiesWithoutUndo();
                UnityEventTools.AddPersistentListener(button.onClick, item.PurchaseOrToggle);
            }
            Set(sections, "menu", menu); Set(sections, "skinsViewport", card.Find("Skins")); Set(sections, "trailsViewport", viewport);
            Set(sections, "tabs", tabs); Set(sections, "skinsTab", skins); Set(sections, "trailsTab", trails); Set(sections, "trailGrid", grid);
            SetArray(sections, "cards", cards); SetArray(sections, "trailContents", contents); SetArray(sections, "trailActions", actions);
            card.Find("Heading").GetComponent<Text>().text = "SHOP";
            menu.ShopPanel.GetComponent<MenuSkinsLayout>().RefreshProducts();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static Button Tab(Transform parent, string name, Font font, UnityAction callback, Vector2 min, Vector2 max)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var ribbon = rect.gameObject.AddComponent<MenuRibbonGraphic>(); ribbon.Primary = name == "SKINS";
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = ribbon; button.transition = Selectable.Transition.None;
            var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(1, 0.99f, 0.94f); outline.effectDistance = new Vector2(3, -3); outline.enabled = false;
            var feedback = rect.gameObject.AddComponent<ArcadeButton>(); var data = new SerializedObject(feedback);
            data.FindProperty("livelyFeedback").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo();
            var label = Rect("Label", rect, Vector2.zero, Vector2.zero); label.anchorMin = Vector2.zero; label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(12, 0); label.offsetMax = new Vector2(-12, 0);
            var text = label.gameObject.AddComponent<MenuLabelText>(); text.font = font; text.text = name; text.fontSize = 27;
            text.color = name == "SKINS" ? new Color(1, 0.99f, 0.94f) : new Color(0.018f, 0.014f, 0.025f);
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            UnityEventTools.AddPersistentListener(button.onClick, callback); return button;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f; rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        private static void Set(Object target, string field, Object value)
        { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetArray<T>(Object target, string field, T[] values) where T : Object
        {
            var data = new SerializedObject(target); var array = data.FindProperty(field); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
