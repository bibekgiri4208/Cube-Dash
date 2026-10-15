using System;
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
    /// <summary>Adds only a saved, themed exit dialog; existing menu art, controls and listeners stay intact.</summary>
    public static class MainMenuQuitConfirmation
    {
        public const string PanelName = "Quit - Confirmation";

        [MenuItem("Tools/Cube Dash/Add Main Menu Quit Confirmation")]
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
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save quit confirmation.");
        }

        public static void ApplyToScene(Scene scene)
        {
            var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
            var canvas = menu.GetComponentInChildren<Canvas>(true).transform;
            var settings = new SerializedObject(menu);
            if (menu.QuitPanel != null)
            {
                if (menu.QuitPanel.transform.parent != canvas || settings.FindProperty("quitCancel").objectReferenceValue == null)
                    throw new InvalidOperationException("Inspect partial/custom quit confirmation before authoring.");
                Debug.Log("Quit confirmation already saved; manual UI edits preserved."); return;
            }
            if (canvas.Find(PanelName) != null) throw new InvalidOperationException("Inspect the unassigned quit confirmation before authoring.");
            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MainMenu/Fonts/Anton-Regular.ttf");
            if (font == null) throw new InvalidOperationException("Main Menu Anton font is required.");

            RectTransform overlay = Rect(PanelName, canvas, Vector2.zero, Vector2.zero);
            overlay.gameObject.SetActive(false);
            Stretch(overlay, Vector2.zero, Vector2.one);
            overlay.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.02f, 0.045f, 0.56f);
            overlay.gameObject.AddComponent<CanvasGroup>();
            RectTransform shadow = Rect("Soft card shadow", overlay, new Vector2(8, -9), new Vector2(760, 320));
            Face(shadow, LevelMenuTheme.Ink, 0).raycastTarget = false;
            RectTransform card = Rect("Card", overlay, Vector2.zero, new Vector2(760, 320));
            Face(card, LevelMenuTheme.Paper, 4);
            RectTransform header = Rect("Ribbon header", card, Vector2.zero, Vector2.zero);
            Stretch(header, new Vector2(0.04f, 0.58f), new Vector2(0.96f, 0.91f));
            header.gameObject.AddComponent<MenuRibbonGraphic>().raycastTarget = false;
            Text heading = Label("Heading", card, "Confirm exit?", font, 52, LevelMenuTheme.Ink);
            Stretch(heading.rectTransform, new Vector2(0.08f, 0.59f), new Vector2(0.92f, 0.90f));
            Button yes = Action(card, "YES", true, font, menu.ConfirmQuit);
            Stretch(yes.GetComponent<RectTransform>(), new Vector2(0.10f, 0.16f), new Vector2(0.46f, 0.41f));
            Button no = Action(card, "NO", false, font, menu.ClosePanels);
            Stretch(no.GetComponent<RectTransform>(), new Vector2(0.54f, 0.16f), new Vector2(0.90f, 0.41f));
            yes.navigation = NavigationTo(no); no.navigation = NavigationTo(yes);

            var layout = overlay.gameObject.AddComponent<MenuDialogLayout>(); var layoutData = new SerializedObject(layout);
            layoutData.FindProperty("card").objectReferenceValue = card;
            layoutData.FindProperty("shadow").objectReferenceValue = shadow; layoutData.ApplyModifiedPropertiesWithoutUndo();
            var motion = overlay.gameObject.AddComponent<MenuPanelMotion>(); var motionData = new SerializedObject(motion);
            motionData.FindProperty("card").objectReferenceValue = card;
            motionData.FindProperty("shadow").objectReferenceValue = shadow; motionData.ApplyModifiedPropertiesWithoutUndo();
            settings.FindProperty("quitPanel").objectReferenceValue = overlay.gameObject;
            settings.FindProperty("quitCancel").objectReferenceValue = no; settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Saved themed Confirm exit? dialog: Yes exits, No/Escape/controller Back cancel, safe default focus on No, existing Quit callback and all menu artwork preserved.");
        }

        private static Navigation NavigationTo(Button other) => new Navigation { mode = Navigation.Mode.Explicit,
            selectOnUp = other, selectOnDown = other, selectOnLeft = other, selectOnRight = other };

        private static Button Action(Transform parent, string name, bool primary, Font font, UnityAction callback)
        {
            RectTransform rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            var ribbon = rect.gameObject.AddComponent<MenuRibbonGraphic>(); ribbon.Primary = primary;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = ribbon; button.transition = Selectable.Transition.None;
            var outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = LevelMenuTheme.Cream;
            outline.effectDistance = new Vector2(3, -3); outline.enabled = false;
            var feedback = rect.gameObject.AddComponent<ArcadeButton>(); var data = new SerializedObject(feedback);
            data.FindProperty("livelyFeedback").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo();
            Text text = Label("Label", rect, name, font, 38, primary ? LevelMenuTheme.Cream : LevelMenuTheme.Ink);
            Stretch(text.rectTransform, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.95f));
            UnityEventTools.AddPersistentListener(button.onClick, callback); return button;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            Undo.RegisterCreatedObjectUndo(rect.gameObject, "Add quit confirmation"); return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        private static MenuPanelGraphic Face(RectTransform rect, Color color, float border)
        {
            var graphic = rect.gameObject.AddComponent<MenuPanelGraphic>(); graphic.color = color;
            graphic.CornerRadius = 0; graphic.CornerCut = 22; graphic.BorderWidth = border; graphic.BorderColor = LevelMenuTheme.Ink;
            return graphic;
        }

        private static Text Label(string name, Transform parent, string value, Font font, int size, Color color)
        {
            var text = Rect(name, parent, Vector2.zero, Vector2.zero).gameObject.AddComponent<MenuLabelText>();
            text.text = value; text.font = font; text.fontSize = size; text.color = color;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 8; text.resizeTextMaxSize = size;
            return text;
        }
    }
}
