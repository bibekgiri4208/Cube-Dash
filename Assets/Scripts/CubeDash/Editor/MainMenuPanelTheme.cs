using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CubeDash.Editor
{
    /// <summary>Restyles existing panel graphics without replacing controls, collections or audio wiring.</summary>
    public static class MainMenuPanelTheme
    {
        private static readonly Color Ink = new Color(0.018f, 0.014f, 0.025f);
        private static readonly Color Paper = new Color(1, 0.82f, 0.84f);
        private static readonly Color Surface = new Color(1, 0.91f, 0.92f);
        private static readonly Color Cream = new Color(1, 0.99f, 0.94f);
        private static readonly Color Accent = new Color(0.95f, 0.02f, 0.11f);

        [MenuItem("Tools/Cube Dash/Match Shop and Options to Menu Ribbons")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before restyling panels.");
            Scene scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save menu panel theme.");
        }

        public static void ApplyToScene(Scene scene)
        {
            var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
            ThemePanel(menu.ShopPanel);
            ThemePanel(menu.OptionsPanel);
            MainMenuSliderFocus.ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ThemePanel(GameObject panel)
        {
            var card = panel.transform.Find("Card").GetComponent<RectTransform>();
            Style(card.GetComponent<MenuPanelGraphic>(), Paper, 4, Ink, 22);
            var shadow = panel.transform.Find("Soft card shadow").GetComponent<MenuPanelGraphic>();
            Style(shadow, Ink, 0, Ink, 22);
            Undo.RecordObject(shadow.rectTransform, "Match ribbon drop shadow");
            shadow.rectTransform.anchoredPosition = new Vector2(8, -9);
            shadow.rectTransform.sizeDelta = card.sizeDelta;

            Transform header = card.Find("Ribbon header");
            if (header == null)
            {
                var go = new GameObject("Ribbon header", typeof(RectTransform));
                go.transform.SetParent(card, false); go.transform.SetAsFirstSibling();
                Undo.RegisterCreatedObjectUndo(go, "Add ribbon panel header");
                var rect = go.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(0, 247); rect.sizeDelta = new Vector2(card.sizeDelta.x - 48, 104);
                var ribbon = Undo.AddComponent<MenuRibbonGraphic>(go); ribbon.raycastTarget = false;
            }
            foreach (Text text in card.GetComponentsInChildren<Text>(true))
            {
                Undo.RecordObject(text, "Match ribbon typography colors");
                text.color = Ink;
            }
            var icon = card.Find("Section icon").GetComponent<MenuGlyphGraphic>();
            Undo.RecordObject(icon, "Match ribbon icon"); icon.color = Ink;
            Move(card.Find("Description"), new Vector2(-card.sizeDelta.x * 0.5f + 470, 176));
            var divider = card.Find("Header divider").GetComponent<MenuPanelGraphic>();
            Style(divider, Ink, 0, Ink, 0);
            Move(divider.transform, new Vector2(0, 153));

            foreach (string name in new[] { "Common Skins", "Rare Skins", "Epic Skins", "Audio section", "Display section" })
            {
                Transform section = card.Find(name);
                if (section == null) continue;
                Style(section.GetComponent<MenuPanelGraphic>(), Surface, 3, Ink, 12);
                Transform rarity = section.Find("Rarity heading");
                if (rarity != null)
                {
                    Ribbon(rarity.gameObject, false);
                    Move(rarity, new Vector2(0, 123));
                    Undo.RecordObject(rarity.GetComponent<RectTransform>(), "Size rarity ribbon");
                    rarity.GetComponent<RectTransform>().sizeDelta = new Vector2(294, 58);
                }
            }
            foreach (Slider slider in card.GetComponentsInChildren<Slider>(true)) ThemeSlider(slider);
            foreach (string name in new[] { "Volume value badge", "Music volume value badge" })
            {
                Transform badge = card.Find("Audio section/" + name);
                if (badge != null)
                {
                    Style(badge.GetComponent<MenuPanelGraphic>(), Ink, 0, Ink, 6);
                    Text value = badge.GetComponentInChildren<Text>(true);
                    Undo.RecordObject(value, "Contrast volume readout"); value.color = Cream;
                }
            }
            var check = card.Find("Display section/Fullscreen checkbox");
            if (check != null) Style(check.GetComponent<MenuPanelGraphic>(), Paper, 3, Ink, 5);
            foreach (Button button in card.GetComponentsInChildren<Button>(true))
            {
                Ribbon(button.gameObject, true);
                Undo.RecordObject(button, "Match menu ribbon interaction");
                button.targetGraphic = button.GetComponent<MenuRibbonGraphic>(); button.transition = Selectable.Transition.None;
                foreach (Text label in button.GetComponentsInChildren<Text>(true))
                {
                    Undo.RecordObject(label, "Match primary ribbon label"); label.color = Cream;
                    Undo.RecordObject(label.rectTransform, "Center label on ribbon face");
                    label.rectTransform.anchoredPosition = new Vector2(-5, 2);
                }
            }
        }

        private static void ThemeSlider(Slider slider)
        {
            var track = slider.transform.Find("Track").GetComponent<MenuPanelGraphic>();
            Style(track, Paper, 2, Ink, 0);
            Undo.RecordObject(track.rectTransform, "Outline slider rail"); track.rectTransform.sizeDelta = new Vector2(792, 18);
            var area = slider.transform.Find("Fill area").GetComponent<RectTransform>();
            Undo.RecordObject(area, "Align fill with inner rail edges");
            area.sizeDelta = new Vector2(-4, 14); area.anchoredPosition = Vector2.zero;
            Style(slider.fillRect.GetComponent<MenuPanelGraphic>(), Accent, 0, Ink, 0);
            Style(slider.handleRect.GetComponent<MenuPanelGraphic>(), Cream, 3, Ink, 4);
        }

        private static void Ribbon(GameObject go, bool primary)
        {
            var old = go.GetComponent<MenuPanelGraphic>();
            if (old != null) Undo.DestroyObjectImmediate(old);
            var ribbon = go.GetComponent<MenuRibbonGraphic>();
            if (ribbon == null) ribbon = Undo.AddComponent<MenuRibbonGraphic>(go);
            Undo.RecordObject(ribbon, "Match menu ribbon face"); ribbon.Primary = primary;
            ribbon.raycastTarget = go.GetComponent<Button>() != null;
        }

        private static void Style(MenuPanelGraphic graphic, Color color, float border, Color edge, float cut)
        {
            Undo.RecordObject(graphic, "Match menu panel theme");
            graphic.color = color; graphic.CornerRadius = 0; graphic.CornerCut = cut;
            graphic.BorderWidth = border; graphic.BorderColor = edge;
        }

        private static void Move(Transform transform, Vector2 position)
        {
            var rect = transform.GetComponent<RectTransform>();
            Undo.RecordObject(rect, "Position panel decoration"); rect.anchoredPosition = position;
        }
    }
}
