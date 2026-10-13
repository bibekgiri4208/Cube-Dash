using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CubeDash.Editor
{
    /// <summary>Upgrades only menu panels. Empty rarity collections are UI containers, not invented skins or purchases.</summary>
    public static class MainMenuSections
    {
        public const string ShopName = "Shop - Skin Collections";
        public const string OptionsName = "Options - Audio and Display";
        private static readonly Color Ink = new Color(0.19f, 0.10f, 0.15f);
        private static readonly Color Muted = new Color(0.42f, 0.33f, 0.38f);
        private static readonly Color Accent = new Color(0.94f, 0.035f, 0.14f);
        private static readonly Color Paper = new Color(1, 0.96f, 0.97f);

        [MenuItem("Tools/Cube Dash/Improve Main Menu Shop and Options")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before upgrading menu sections.");
            Scene scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the menu sections.");
        }

        public static void ApplyToScene(Scene scene)
        {
            var menu = scene.GetRootGameObjects().Select(root => root.GetComponent<MainMenuController>()).FirstOrDefault(item => item != null);
            if (menu == null) throw new InvalidOperationException("The authored menu controller is required.");
            Canvas canvas = menu.GetComponentInChildren<Canvas>(true);
            if (canvas == null) throw new InvalidOperationException("The authored menu canvas is required.");
            Font heading = AssetDatabase.LoadAssetAtPath<Font>("Assets/MainMenu/Fonts/Anton-Regular.ttf");
            Font body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (heading == null || body == null) throw new InvalidOperationException("Menu fonts were not found.");
            bool changed = false;
            if (canvas.transform.Find(ShopName) == null)
            {
                Backup(menu.ShopPanel, "Previous Shop Preview (inactive backup)");
                RectTransform shop = Shell(ShopName, canvas.transform, new Vector2(1160, 650), "SKINS", MenuGlyphGraphic.Glyph.Cart,
                    "Browse by rarity. New skins will appear here.", heading, body, out RectTransform card);
                Collection(card, "Common", -350, new Color(0.48f, 0.51f, 0.56f), heading, body);
                Collection(card, "Rare", 0, new Color(0.18f, 0.46f, 0.82f), heading, body);
                Collection(card, "Epic", 350, new Color(0.61f, 0.27f, 0.78f), heading, body);
                Label("Availability note", card, "No skins available yet. Nothing to buy or equip.", body, 23, Muted,
                    new Vector2(-165, -270), new Vector2(670, 54), TextAnchor.MiddleLeft);
                Button back = Back(card, new Vector2(420, -270), menu.ClosePanels, heading);
                back.navigation = new Navigation { mode = Navigation.Mode.None };
                Set(menu, "shopPanel", shop.gameObject); Set(menu, "shopClose", back);
                shop.gameObject.SetActive(false); changed = true;
            }
            if (canvas.transform.Find(OptionsName) == null)
            {
                Backup(menu.OptionsPanel, "Previous Options (inactive backup)");
                RectTransform options = Shell(OptionsName, canvas.transform, new Vector2(1000, 650), "OPTIONS", MenuGlyphGraphic.Glyph.Gear,
                    "A few settings to make the game feel right for you.", heading, body, out RectTransform card);
                RectTransform audio = Box("Audio section", card, new Vector2(0, 64), new Vector2(880, 170), Color.white, 16, 1, new Color(0.88f, 0.79f, 0.83f));
                Label("Volume heading", audio, "MASTER VOLUME", heading, 28, Ink, new Vector2(-100, 45), new Vector2(620, 42), TextAnchor.MiddleLeft);
                RectTransform badge = Box("Volume value badge", audio, new Vector2(362, 45), new Vector2(108, 40), new Color(1, 0.88f, 0.91f), 12, 0, Ink);
                Text value = Label("Volume value", badge, "100%", body, 24, Ink, Vector2.zero, new Vector2(104, 36));
                Slider slider = VolumeSlider(audio, menu);
                RectTransform display = Box("Display section", card, new Vector2(0, -126), new Vector2(880, 154), Color.white, 16, 1, new Color(0.88f, 0.79f, 0.83f));
                Label("Fullscreen heading", display, "FULLSCREEN", heading, 28, Ink, new Vector2(-95, 22), new Vector2(630, 42), TextAnchor.MiddleLeft);
                Label("Fullscreen explanation", display, "Switch between windowed and fullscreen display.", body, 23, Muted,
                    new Vector2(-70, -25), new Vector2(680, 40), TextAnchor.MiddleLeft);
                Toggle toggle = display.gameObject.AddComponent<Toggle>();
                RectTransform check = Box("Fullscreen checkbox", display, new Vector2(362, 0), new Vector2(56, 56), Paper, 12, 2, Ink);
                RectTransform tick = Rect("Checked", check, Vector2.zero, new Vector2(42, 42));
                var tickGraphic = tick.gameObject.AddComponent<MenuGlyphGraphic>(); tickGraphic.Symbol = MenuGlyphGraphic.Glyph.Check;
                tickGraphic.color = Accent; tickGraphic.raycastTarget = false;
                toggle.targetGraphic = check.GetComponent<MenuPanelGraphic>(); toggle.graphic = tickGraphic;
                UnityEventTools.AddPersistentListener(toggle.onValueChanged, menu.SetFullscreen);
                Label("Settings note", card, "Volume is saved automatically.", body, 23, Muted, new Vector2(-155, -270), new Vector2(570, 54), TextAnchor.MiddleLeft);
                Button back = Back(card, new Vector2(340, -270), menu.ClosePanels, heading);
                slider.navigation = Nav(back, toggle);
                toggle.navigation = Nav(slider, back);
                back.navigation = Nav(toggle, slider);
                Set(menu, "optionsPanel", options.gameObject); Set(menu, "optionsClose", back);
                Set(menu, "volume", slider); Set(menu, "volumeReadout", value); Set(menu, "fullscreen", toggle);
                options.gameObject.SetActive(false); changed = true;
            }
            if (changed) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(changed ? "Authored empty Common, Rare and Epic skin collections and polished Audio/Display options. Previous panels retained inactive; world, camera, main ribbons and gameplay preserved."
                : "Menu sections are already authored. Existing panel and collection edits were preserved.");
        }

        public static void ApplyAndCaptureFromCommandLine()
        {
            ApplyFromCommandLine();
            Scene scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            MainMenuController menu = scene.GetRootGameObjects().Select(root => root.GetComponent<MainMenuController>()).First(item => item != null);
            Canvas canvas = menu.GetComponentInChildren<Canvas>(true);
            Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).Single(item => item.CompareTag("MainCamera"));
            string prefix = Environment.GetEnvironmentVariable("CUBE_DASH_MENU_SECTIONS_PREVIEW") ?? Path.Combine(Path.GetDirectoryName(Application.dataPath), "main-menu-sections");
            var target = new RenderTexture(1672, 940, 24); var image = new Texture2D(1672, 940, TextureFormat.RGB24, false);
            RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            RenderMode oldMode = canvas.renderMode; Camera oldCamera = canvas.worldCamera;
            float oldPlane = canvas.planeDistance; bool oldAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                camera.targetTexture = target; ShaderUtil.allowAsyncCompilation = false;
                menu.OpenShop(); Capture(camera, target, image, prefix + "-shop.png");
                menu.OpenOptions(); Capture(camera, target, image, prefix + "-options.png");
            }
            finally
            {
                menu.ClosePanels(); camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldPlane;
                ShaderUtil.allowAsyncCompilation = oldAsync;
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static void Capture(Camera camera, RenderTexture target, Texture2D image, string path)
        {
            Canvas.ForceUpdateCanvases();
            for (int frame = 0; frame < 4; frame++) camera.Render();
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG()); Debug.Log("Menu section preview saved to " + path);
        }

        private static RectTransform Shell(string name, Transform parent, Vector2 size, string title, MenuGlyphGraphic.Glyph glyph,
            string subtitle, Font heading, Font body, out RectTransform card)
        {
            RectTransform overlay = Rect(name, parent, Vector2.zero, Vector2.zero); Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.02f, 0.045f, 0.56f);
            Box("Soft card shadow", overlay, new Vector2(8, -10), size + new Vector2(10, 10), new Color(0.035f, 0.012f, 0.025f, 0.28f), 24, 0, Ink).GetComponent<MenuPanelGraphic>().raycastTarget = false;
            card = Box("Card", overlay, Vector2.zero, size, Paper, 22, 3, Ink);
            float left = -size.x * 0.5f + 60;
            RectTransform icon = Rect("Section icon", card, new Vector2(left + 25, 247), new Vector2(52, 52));
            var graphic = icon.gameObject.AddComponent<MenuGlyphGraphic>(); graphic.Symbol = glyph; graphic.color = Ink; graphic.raycastTarget = false;
            Label("Heading", card, title, heading, 50, Ink, new Vector2(left + 245, 247), new Vector2(350, 88), TextAnchor.MiddleLeft);
            Label("Description", card, subtitle, body, 24, Muted, new Vector2(left + 410, 195), new Vector2(820, 40), TextAnchor.MiddleLeft);
            Box("Header divider", card, new Vector2(0, 163), new Vector2(size.x - 120, 2), new Color(0.86f, 0.73f, 0.78f), 0, 0, Ink).GetComponent<MenuPanelGraphic>().raycastTarget = false;
            return overlay;
        }

        private static void Collection(RectTransform parent, string rarity, float x, Color accent, Font heading, Font body)
        {
            RectTransform section = Box(rarity + " Skins", parent, new Vector2(x, -20), new Vector2(320, 324), Color.white, 16, 2, accent);
            RectTransform badge = Box("Rarity heading", section, new Vector2(0, 126), new Vector2(288, 48), accent, 10, 0, accent);
            Label("Rarity label", badge, rarity.ToUpperInvariant(), heading, 29, Color.white, Vector2.zero, new Vector2(280, 44));
            RectTransform viewport = Rect("Skin viewport", section, new Vector2(0, -24), new Vector2(284, 206));
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("Skin slots", viewport, Vector2.zero, new Vector2(284, 0));
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 1); content.pivot = new Vector2(0.5f, 1);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(132, 94); grid.spacing = new Vector2(12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2; grid.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 18;
            RectTransform empty = Rect("Empty state", section, new Vector2(0, -20), new Vector2(280, 200));
            RectTransform icon = Rect("Collection emblem", empty, new Vector2(0, 34), new Vector2(54, 54));
            var graphic = icon.gameObject.AddComponent<MenuGlyphGraphic>(); graphic.Symbol = MenuGlyphGraphic.Glyph.Collection; graphic.color = accent; graphic.raycastTarget = false;
            Label("Empty title", empty, "NO SKINS YET", heading, 25, Ink, new Vector2(0, -20), new Vector2(260, 42));
            Label("Empty explanation", empty, "This collection is empty.", body, 21, Muted, new Vector2(0, -58), new Vector2(276, 36));
        }

        private static Slider VolumeSlider(RectTransform parent, MainMenuController menu)
        {
            RectTransform root = Rect("Volume slider", parent, new Vector2(0, -30), new Vector2(792, 46));
            root.gameObject.AddComponent<Image>().color = Color.clear; // Keep the entire control easy to click, not only the narrow track.
            var slider = root.gameObject.AddComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1;
            RectTransform track = Box("Track", root, Vector2.zero, new Vector2(792, 14), new Color(0.87f, 0.80f, 0.83f), 7, 0, Ink);
            track.GetComponent<MenuPanelGraphic>().raycastTarget = true;
            RectTransform fillArea = Rect("Fill area", root, Vector2.zero, new Vector2(-24, 14));
            fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f);
            RectTransform fill = Box("Fill", fillArea, Vector2.zero, Vector2.zero, Accent, 7, 0, Ink);
            Stretch(fill);
            fill.GetComponent<MenuPanelGraphic>().raycastTarget = false;
            RectTransform handles = Rect("Handle area", root, Vector2.zero, new Vector2(-24, 42));
            handles.anchorMin = new Vector2(0, 0.5f); handles.anchorMax = new Vector2(1, 0.5f);
            RectTransform handle = Box("Handle", handles, Vector2.zero, new Vector2(28, 0), Color.white, 10, 2, Accent);
            handle.anchorMin = handle.anchorMax = new Vector2(0, 0.5f);
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<MenuPanelGraphic>(); slider.value = 1;
            UnityEventTools.AddPersistentListener(slider.onValueChanged, menu.SetVolume); return slider;
        }

        private static Button Back(RectTransform parent, Vector2 position, UnityAction action, Font font)
        {
            RectTransform root = Box("BACK", parent, position, new Vector2(190, 58), Accent, 12, 2, new Color(0.39f, 0.025f, 0.10f));
            var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = root.GetComponent<MenuPanelGraphic>();
            root.gameObject.AddComponent<ArcadeButton>(); UnityEventTools.AddPersistentListener(button.onClick, action);
            Label("Label", root, "BACK", font, 30, Color.white, Vector2.zero, new Vector2(174, 52)); return button;
        }

        private static Navigation Nav(Selectable up, Selectable down) => new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };
        private static void Backup(GameObject panel, string name)
        {
            if (panel == null) return;
            Undo.RecordObject(panel, "Preserve previous menu panel"); panel.name = name; panel.SetActive(false);
        }
        private static void Set(MainMenuController menu, string property, UnityEngine.Object value)
        {
            Undo.RecordObject(menu, "Wire menu section"); var serialized = new SerializedObject(menu);
            serialized.FindProperty(property).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
            Undo.RegisterCreatedObjectUndo(rect.gameObject, "Create menu section"); return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static RectTransform Box(string name, Transform parent, Vector2 position, Vector2 size, Color color, float radius, float border, Color borderColor)
        {
            RectTransform rect = Rect(name, parent, position, size);
            var graphic = rect.gameObject.AddComponent<MenuPanelGraphic>(); graphic.color = color; graphic.CornerRadius = radius;
            graphic.BorderWidth = border; graphic.BorderColor = borderColor; return rect;
        }
        private static Text Label(string name, Transform parent, string text, Font font, int size, Color color, Vector2 position, Vector2 dimensions, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var label = Rect(name, parent, position, dimensions).gameObject.AddComponent<MenuLabelText>(); label.text = text; label.font = font;
            label.fontSize = size; label.color = color; label.alignment = alignment; label.raycastTarget = false;
            // Retain editable helper copy, but keep Shop/Options free of instruction paragraphs.
            if (name == "Description" || name == "Availability note" || name == "Settings note" ||
                name == "Fullscreen explanation" || name == "Empty explanation") label.gameObject.SetActive(false);
            return label;
        }
    }
}
