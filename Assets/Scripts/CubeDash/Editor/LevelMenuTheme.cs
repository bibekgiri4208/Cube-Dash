using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Scene-only ribbon UI upgrade. Never rebuilds gameplay, scenery or shared prefabs.</summary>
    public static class LevelMenuTheme
    {
        public const string ScenePath = "Assets/Scenes/Level.unity";
        public const string CountdownName = "Run Countdown";
        public static readonly Color Ink = new Color(0.018f, 0.014f, 0.025f);
        public static readonly Color Paper = new Color(1, 0.82f, 0.84f);
        public static readonly Color Cream = new Color(1, 0.99f, 0.94f);
        public static readonly Color Accent = new Color(0.95f, 0.02f, 0.11f);

        [MenuItem("Tools/Cube Dash/Match Level UI to Main Menu")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the Level UI.");
        }

        public static void ApplyToScene(Scene scene)
        {
            if (scene.path != ScenePath) throw new InvalidOperationException("This tool only upgrades Level.");
            RunnerHud hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RunnerHud>(true)).Single();
            if (hud.RibbonTheme)
            {
                LevelPowerUpBars.ApplyToScene(scene);
                Debug.Log("Level already uses the Main Menu theme; existing UI edits were preserved."); return;
            }
            var game = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CubeDashGame>(true)).Single();
            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MainMenu/Fonts/Anton-Regular.ttf");
            if (font == null) throw new InvalidOperationException("Main Menu Anton font is required.");
            Transform safe = hud.transform.Find("Safe Area");
            Transform card = safe.Find("Menu Overlay/Menu Card");
            Transform end = safe.Find("Game Over Overlay/Abstract Composition");
            foreach (Text original in hud.GetComponentsInChildren<Text>(true))
            {
                Text text = UpgradeLabel(original, hud);
                Undo.RecordObject(text, "Match Level menu typography");
                text.font = font; text.fontStyle = FontStyle.Normal; text.color = Ink;
                text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
                text.raycastTarget = false;
                foreach (Shadow effect in text.GetComponents<Shadow>()) Undo.DestroyObjectImmediate(effect);
            }
            foreach (string name in new[] { "Lane Controls", "Desktop Controls Hint" }) Hide(safe.Find(name));

            Ribbon(safe.Find("Score Panel"), false);
            Place(safe.Find("Score Panel"), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(260, 132));
            Label(safe.Find("Brand"), "CUBE / DASH", 18, Ink, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-46, -45), new Vector2(220, 32), TextAnchor.MiddleRight);
            Label(safe.Find("Distance"), "0 POINTS", 36, Ink, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-44, -82), new Vector2(220, 62), TextAnchor.MiddleRight);
            Fit(safe.Find("Distance").GetComponent<Text>(), 16, 36);
            Label(safe.Find("Best"), "BEST  0", 18, Ink, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-46, -125), new Vector2(220, 32), TextAnchor.MiddleRight);
            Label(safe.Find("Pickup Feedback"), "+1", 28, Accent, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-286, -82), new Vector2(64, 50), TextAnchor.MiddleRight);

            Ribbon(safe.Find("Color Objective Panel"), false);
            Place(safe.Find("Color Objective Panel"), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(180, 72));
            Label(safe.Find("Speed"), "0 m", 26, Ink, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(146, 56));

            Transform pause = safe.Find("Pause"); Ribbon(pause, true);
            Place(pause, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(90, 72));
            Hide(pause.Find("Label"));
            foreach (Image bar in pause.Find("Pause Icon").GetComponentsInChildren<Image>(true))
            { Undo.RecordObject(bar, "Match pause icon"); bar.color = Cream; bar.raycastTarget = false; }
            Place(pause.Find("Pause Icon"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-3, 3), new Vector2(30, 26));

            Dim(safe.Find("Menu Overlay")); Dim(safe.Find("Game Over Overlay"));
            Panel(card); Panel(end);
            Place(card, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(560, 440));
            Place(end, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, new Vector2(520, 440));
            Header(card, new Vector2(0, -70), new Vector2(520, 98));
            Header(end, new Vector2(0, -64), new Vector2(480, 92));
            Label(card.Find("Heading"), "PAUSED", 48, Ink, new Vector2(0.5f, 1), Vector2.one * 0.5f, new Vector2(-4, -68), new Vector2(500, 90));
            foreach (string name in new[] { "Accent", "Eyebrow", "Description", "Keyboard Hint", "Pause Motif" }) Hide(card.Find(name));
            StyleButton(card.Find("Primary Action"), "RESUME RUN", true, new Vector2(0, 30), new Vector2(420, 68));
            StyleButton(card.Find("Restart"), "RESTART", false, new Vector2(0, -62), new Vector2(420, 68));
            StyleButton(card.Find("Quit"), "QUIT", false, new Vector2(0, -154), new Vector2(420, 68));
            BindRun(card.Find("Restart").GetComponent<Button>(), game);
            card.Find("Restart").gameObject.SetActive(false); card.Find("Quit").gameObject.SetActive(false);

            foreach (string name in new[] { "Small Blue Square", "Small Green Square", "Main Coral Square", "Tap Hint" }) Hide(end.Find(name));
            Label(end.Find("Heading"), "GAME OVER", 46, Ink, new Vector2(0.5f, 1), Vector2.one * 0.5f, new Vector2(-4, -62), new Vector2(460, 86));
            Label(end.Find("Score"), "0", 76, Ink, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, 54), new Vector2(420, 108));
            end.Find("Score").GetComponent<Text>().verticalOverflow = VerticalWrapMode.Overflow;
            Fit(end.Find("Score").GetComponent<Text>(), 30, 76);
            Label(end.Find("Score Caption"), "POINTS", 22, Ink, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, -14), new Vector2(420, 44));
            StyleButton(end.Find("Retry"), "TRY AGAIN", true, new Vector2(0, -88), new Vector2(380, 68));
            StyleButton(end.Find("Quit"), "QUIT", false, new Vector2(0, -172), new Vector2(380, 68));
            BindRun(end.Find("Retry").GetComponent<Button>(), game);

            var powerRibbon = StatusRibbon("Power-Up Ribbon", safe, true);
            var cheatRibbon = StatusRibbon("Cheat Ribbon", safe, false);
            if (safe.Find("Power-Up Status") != null)
            {
                Text text = safe.Find("Power-Up Status").GetComponent<Text>(); text.fontSize = 20; text.color = Cream;
                text.alignment = TextAnchor.MiddleCenter;
                text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = 20;
            }
            if (safe.Find("Cheat Status") != null)
            {
                Text text = safe.Find("Cheat Status").GetComponent<Text>(); text.fontSize = 18; text.color = Ink;
                text.alignment = TextAnchor.MiddleCenter;
                text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = 18;
            }
            if (safe.Find(CountdownName) != null) throw new InvalidOperationException("An existing countdown requires inspection before replacing it.");
            RectTransform countdown = NewRect(CountdownName, safe); Stretch(countdown);
            RectTransform badge = NewRect("Countdown ribbon", countdown);
            Place(badge, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(0, 16), new Vector2(250, 284));
            Ribbon(badge, true).raycastTarget = false;
            Text number = NewLabel("Number", badge, font);
            Label(number.transform, "3", 148, Cream, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(-5, 30), new Vector2(210, 212));
            number.verticalOverflow = VerticalWrapMode.Overflow;
            Text caption = NewLabel("Caption", badge, font);
            Label(caption.transform, "GET READY", 26, Cream, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(-5, -94), new Vector2(210, 54));
            countdown.gameObject.SetActive(false);
            safe.Find("Menu Overlay").gameObject.SetActive(false); safe.Find("Game Over Overlay").gameObject.SetActive(false);
            var settings = new SerializedObject(hud);
            settings.FindProperty("ribbonTheme").boolValue = true;
            settings.FindProperty("countdownOverlay").objectReferenceValue = countdown.gameObject;
            settings.FindProperty("countdownNumber").objectReferenceValue = number;
            settings.FindProperty("powerUpRibbon").objectReferenceValue = powerRibbon;
            settings.FindProperty("cheatRibbon").objectReferenceValue = cheatRibbon;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var gameSettings = new SerializedObject(game); gameSettings.FindProperty("useStartCountdown").boolValue = true;
            gameSettings.ApplyModifiedPropertiesWithoutUndo();
            LevelPowerUpBars.ApplyToScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Level now uses Main Menu ribbons, typography and colors, with automatic 3/2/1 entry and countdown retries. Gameplay/world/prefabs preserved.");
        }

        private static Text UpgradeLabel(Text original, RunnerHud hud)
        {
            if (original is MenuLabelText) return original;
            var settings = new SerializedObject(hud);
            var iterator = settings.GetIterator(); var references = new System.Collections.Generic.List<string>();
            while (iterator.NextVisible(true))
                if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == original)
                    references.Add(iterator.propertyPath);
            GameObject go = original.gameObject; string value = original.text;
            int size = original.fontSize, min = original.resizeTextMinSize, max = original.resizeTextMaxSize;
            var alignment = original.alignment; bool fit = original.resizeTextForBestFit, active = original.enabled;
            float spacing = original.lineSpacing;
            Undo.DestroyObjectImmediate(original);
            var label = Undo.AddComponent<MenuLabelText>(go);
            label.text = value; label.fontSize = size; label.alignment = alignment; label.lineSpacing = spacing;
            label.resizeTextMinSize = min; label.resizeTextMaxSize = max; label.resizeTextForBestFit = fit; label.enabled = active;
            settings.Update();
            foreach (string path in references) settings.FindProperty(path).objectReferenceValue = label;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return label;
        }

        private static void BindRun(Button button, CubeDashGame game)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentTarget(i) == game && button.onClick.GetPersistentMethodName(i) == nameof(CubeDashGame.StartRun))
                    UnityEventTools.RegisterPersistentListener(button.onClick, i, game.RequestRun);
        }

        private static void StyleButton(Transform transform, string label, bool primary, Vector2 position, Vector2 size)
        {
            Ribbon(transform, primary); Place(transform, Vector2.one * 0.5f, Vector2.one * 0.5f, position, size);
            Text text = transform.GetComponentInChildren<Text>(true);
            Label(text.transform, label, 30, primary ? Cream : Ink, Vector2.one * 0.5f, Vector2.one * 0.5f, new Vector2(-4, 2), size - new Vector2(24, 4));
            Fit(text, 16, 30);
        }

        private static MenuRibbonGraphic Ribbon(Transform transform, bool primary)
        {
            RemoveOldGraphic(transform);
            var ribbon = transform.GetComponent<MenuRibbonGraphic>();
            if (ribbon == null) ribbon = Undo.AddComponent<MenuRibbonGraphic>(transform.gameObject);
            ribbon.Primary = primary; ribbon.raycastTarget = transform.GetComponent<Button>() != null;
            Button button = transform.GetComponent<Button>();
            if (button != null)
            {
                Undo.RecordObject(button, "Match ribbon interactions"); button.targetGraphic = ribbon; button.transition = Selectable.Transition.None;
                if (button.GetComponent<ArcadeButton>() == null) Undo.AddComponent<ArcadeButton>(button.gameObject);
                Outline outline = button.GetComponent<Outline>();
                if (outline == null) outline = Undo.AddComponent<Outline>(button.gameObject);
                outline.effectColor = Cream; outline.effectDistance = new Vector2(3, -3); outline.enabled = false;
            }
            return ribbon;
        }

        private static void Panel(Transform transform)
        {
            RemoveOldGraphic(transform);
            var panel = transform.GetComponent<MenuPanelGraphic>();
            if (panel == null) panel = Undo.AddComponent<MenuPanelGraphic>(transform.gameObject);
            panel.color = Paper; panel.CornerRadius = 0; panel.CornerCut = 22; panel.BorderColor = Ink; panel.BorderWidth = 4;
        }

        private static void RemoveOldGraphic(Transform transform)
        {
            Image image = transform.GetComponent<Image>();
            if (image != null) Undo.DestroyObjectImmediate(image);
        }

        private static void Header(Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform header = NewRect("Ribbon header", parent); header.SetAsFirstSibling();
            Place(header, new Vector2(0.5f, 1), Vector2.one * 0.5f, position, size); Ribbon(header, false);
        }

        private static RectTransform StatusRibbon(string name, Transform safe, bool primary)
        {
            RectTransform rect = NewRect(name, safe); rect.SetAsFirstSibling();
            Place(rect, new Vector2(0.5f, 1), Vector2.one * 0.5f, Vector2.zero, new Vector2(640, 60));
            Ribbon(rect, primary); rect.gameObject.SetActive(false); return rect;
        }

        private static void Dim(Transform transform)
        {
            Image image = transform.GetComponent<Image>(); Undo.RecordObject(image, "Match menu backdrop");
            image.color = new Color(0.025f, 0.02f, 0.045f, 0.56f);
        }

        private static void Label(Transform transform, string value, int size, Color color, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 bounds, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Text text = transform.GetComponent<Text>(); Undo.RecordObject(text, "Match ribbon label");
            text.text = value; text.fontSize = size; text.fontStyle = FontStyle.Normal; text.color = color; text.alignment = alignment;
            text.raycastTarget = false; Place(transform, anchor, pivot, position, bounds);
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(rect.gameObject, "Author Level ribbon UI"); return rect;
        }
        private static Text NewLabel(string name, Transform parent, Font font)
        {
            var label = Undo.AddComponent<MenuLabelText>(NewRect(name, parent).gameObject); label.font = font; return label;
        }
        private static void Hide(Transform transform) { if (transform != null) { Undo.RecordObject(transform.gameObject, "Hide previous UI detail"); transform.gameObject.SetActive(false); } }
        private static void Fit(Text label, int min, int max) { label.resizeTextForBestFit = true; label.resizeTextMinSize = min; label.resizeTextMaxSize = max; }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Place(Transform transform, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)transform; Undo.RecordObject(rect, "Lay out Level ribbons");
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
        }
    }
}
