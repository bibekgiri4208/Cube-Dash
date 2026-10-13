using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CubeDash.Editor
{
    public static class MainMenuMapSelection
    {
        public const string PanelName = "Start - Map Selection";
        public static readonly string[] MapNames = { "City", "Jungle", "Mountain", "Desert", "Beach", "Country Side" };

        [MenuItem("Tools/Cube Dash/Add Main Menu Map Selection")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }
        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            var scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save map selection.");
        }
        public static void ApplyToScene(Scene scene)
        {
            var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
            if (menu.MapPanel != null) { UpdateMapTargets(menu); Debug.Log("Updated map targets; existing layout and manual edits preserved."); return; }
            var canvas = menu.GetComponentInChildren<Canvas>(true).transform;
            if (canvas.Find(PanelName) != null) throw new InvalidOperationException("An unassigned map panel requires inspection before replacing it.");
            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MainMenu/Fonts/Anton-Regular.ttf");
            if (font == null) throw new InvalidOperationException("Main Menu Anton font is required.");
            RectTransform panel = Rect(PanelName, canvas, Vector2.zero, Vector2.zero); Stretch(panel, Vector2.zero, Vector2.one);
            panel.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.02f, 0.045f, 0.56f);
            panel.gameObject.AddComponent<CanvasGroup>();
            RectTransform shadow = Rect("Soft card shadow", panel, new Vector2(8, -9), new Vector2(1160, 740));
            Face(shadow, LevelMenuTheme.Ink, 0, 22).raycastTarget = false;
            RectTransform card = Rect("Card", panel, Vector2.zero, new Vector2(1160, 740)); Face(card, LevelMenuTheme.Paper, 4, 22);
            RectTransform header = Rect("Ribbon header", card, new Vector2(0, 294), new Vector2(1112, 104));
            header.gameObject.AddComponent<MenuRibbonGraphic>().raycastTarget = false;
            Text heading = Label("Heading", card, font, "CHOOSE YOUR MAP", 50, LevelMenuTheme.Ink, new Vector2(0, 294), new Vector2(1080, 90));
            var maps = new Button[6]; var rects = new RectTransform[6];
            for (int i = 0; i < maps.Length; i++)
            {
                bool ready = i == 0 || i == 5;
                RectTransform tile = Rect(MapNames[i], card, new Vector2((i % 3 - 1) * 344, 104 - i / 3 * 236), new Vector2(336, 212));
                rects[i] = tile;
                var face = Face(tile, new Color(1, 0.91f, 0.92f), 3, 12);
                var button = tile.gameObject.AddComponent<Button>(); maps[i] = button;
                button.targetGraphic = face; button.transition = Selectable.Transition.None; button.interactable = ready;
                tile.gameObject.AddComponent<ArcadeButton>();
                var outline = tile.gameObject.AddComponent<Outline>(); outline.effectColor = LevelMenuTheme.Cream;
                outline.effectDistance = new Vector2(3, -3); outline.enabled = false;
                var illustration = Rect("Map illustration", tile, Vector2.zero, Vector2.zero);
                Stretch(illustration, new Vector2(0.055f, 0.41f), new Vector2(0.945f, 0.925f));
                var preview = illustration.gameObject.AddComponent<MapPreviewGraphic>(); preview.raycastTarget = false;
                var previewData = new SerializedObject(preview); previewData.FindProperty("map").enumValueIndex = i; previewData.ApplyModifiedPropertiesWithoutUndo();
                var title = Label("Map name", tile, font, MapNames[i].ToUpperInvariant(), 30, LevelMenuTheme.Ink, Vector2.zero, Vector2.zero);
                Stretch(title.rectTransform, new Vector2(0.04f, 0.19f), new Vector2(0.96f, 0.41f));
                var badge = Rect("Availability badge", tile, Vector2.zero, Vector2.zero);
                Stretch(badge, new Vector2(0.18f, 0.065f), new Vector2(0.82f, 0.205f));
                Face(badge, ready ? LevelMenuTheme.Accent : new Color(0.72f, 0.65f, 0.69f), 0, 5).raycastTarget = false;
                var status = Label("Availability", badge, font, ready ? "PLAY" : "COMING SOON", 18, ready ? LevelMenuTheme.Cream : LevelMenuTheme.Ink, Vector2.zero, Vector2.zero);
                Stretch(status.rectTransform, new Vector2(0.04f, 0), new Vector2(0.96f, 1));
                UnityEventTools.AddStringPersistentListener(button.onClick, menu.StartMap, i == 0 ? "Level" : i == 5 ? "Country Side" : "");
            }
            var backRect = Rect("BACK", card, new Vector2(440, -300), new Vector2(200, 58));
            var ribbon = backRect.gameObject.AddComponent<MenuRibbonGraphic>(); ribbon.Primary = true;
            var back = backRect.gameObject.AddComponent<Button>(); back.targetGraphic = ribbon; back.transition = Selectable.Transition.None;
            backRect.gameObject.AddComponent<ArcadeButton>();
            var backOutline = backRect.gameObject.AddComponent<Outline>(); backOutline.effectColor = LevelMenuTheme.Cream;
            backOutline.effectDistance = new Vector2(3, -3); backOutline.enabled = false;
            var backText = Label("Label", backRect, font, "BACK", 30, LevelMenuTheme.Cream, Vector2.zero, Vector2.zero); Stretch(backText.rectTransform, Vector2.zero, Vector2.one);
            UnityEventTools.AddPersistentListener(back.onClick, menu.ClosePanels);
            maps[0].navigation = Nav(back, maps[5], maps[5]); maps[5].navigation = Nav(maps[0], back, maps[0]); back.navigation = Nav(maps[5], maps[0], maps[0]);
            for (int i = 1; i < 5; i++) maps[i].navigation = new Navigation { mode = Navigation.Mode.None };
            var layout = panel.gameObject.AddComponent<MenuMapLayout>(); var layoutData = new SerializedObject(layout);
            foreach (var item in new[] { ("card", card), ("shadow", shadow), ("header", header), ("heading", heading.rectTransform), ("back", backRect) })
                layoutData.FindProperty(item.Item1).objectReferenceValue = item.Item2;
            var array = layoutData.FindProperty("maps"); array.arraySize = 6;
            for (int i = 0; i < 6; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = rects[i];
            layoutData.ApplyModifiedPropertiesWithoutUndo();
            var settings = new SerializedObject(menu); settings.FindProperty("mapPanel").objectReferenceValue = panel.gameObject;
            settings.FindProperty("firstMap").objectReferenceValue = maps[0]; settings.ApplyModifiedPropertiesWithoutUndo();
            UpdateMapTargets(menu);
            panel.gameObject.SetActive(false);
            MainMenuUiAnimations.ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        private static void UpdateMapTargets(MainMenuController menu)
        {
            var card = menu.MapPanel.transform.Find("Card");
            foreach (string name in MapNames)
            {
                var tile = card.Find(name); var button = tile.GetComponent<Button>();
                bool ready = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/" + name + ".unity") != null;
                string target = ready ? name : name == "City" ? "Level" : "";
                ready |= name == "City";
                button.interactable = ready;
                var data = new SerializedObject(button); var calls = data.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                // Update only this map's existing loading callback, retaining any extra user-authored listeners.
                for (int i = 0; i < calls.arraySize; i++)
                {
                    var call = calls.GetArrayElementAtIndex(i);
                    if (call.FindPropertyRelative("m_Target").objectReferenceValue == menu &&
                        call.FindPropertyRelative("m_MethodName").stringValue == nameof(MainMenuController.StartMap))
                        call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = target;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                var badge = tile.Find("Availability badge");
                badge.GetComponent<MenuPanelGraphic>().color = ready ? LevelMenuTheme.Accent : new Color(0.72f, 0.65f, 0.69f);
                var label = badge.Find("Availability").GetComponent<Text>();
                label.text = ready ? "PLAY" : "COMING SOON"; label.color = ready ? LevelMenuTheme.Cream : LevelMenuTheme.Ink;
            }
            menu.MapPanel.GetComponent<MenuMapLayout>().ConfigureNavigation(3);
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        }
        private static Navigation Nav(Selectable up, Selectable down, Selectable side) => new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = side, selectOnRight = side };
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f; rect.anchoredPosition = position; rect.sizeDelta = size;
            Undo.RegisterCreatedObjectUndo(rect.gameObject, "Author map-selection submenu"); return rect;
        }
        private static MenuPanelGraphic Face(RectTransform rect, Color color, float border, float cut)
        {
            var graphic = rect.gameObject.AddComponent<MenuPanelGraphic>(); graphic.color = color; graphic.BorderColor = LevelMenuTheme.Ink;
            graphic.BorderWidth = border; graphic.CornerRadius = 0; graphic.CornerCut = cut; return graphic;
        }
        private static Text Label(string name, Transform parent, Font font, string text, int size, Color color, Vector2 position, Vector2 bounds)
        {
            var label = Rect(name, parent, position, bounds).gameObject.AddComponent<MenuLabelText>();
            label.font = font; label.text = text; label.fontSize = size; label.fontStyle = FontStyle.Normal; label.color = color;
            label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 8; label.resizeTextMaxSize = size; return label;
        }
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
