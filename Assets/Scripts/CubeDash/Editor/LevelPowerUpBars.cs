using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CubeDash.Editor
{
    /// <summary>Replaces only Level's power-up banner with fixed, saved left-hand duration cards.</summary>
    public static class LevelPowerUpBars
    {
        public const string StackName = "Power-Up Bars";

        [MenuItem("Tools/Cube Dash/Add Left Power-Up Duration Bars")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            var scene = SceneManager.GetSceneByPath(LevelMenuTheme.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(LevelMenuTheme.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the duration bars.");
        }

        public static void ApplyToScene(Scene scene)
        {
            if (scene.path != LevelMenuTheme.ScenePath) throw new InvalidOperationException("This tool only upgrades Level.");
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RunnerHud>(true)).Single();
            if (!hud.RibbonTheme) throw new InvalidOperationException("Apply the Main Menu Level theme before adding duration bars.");
            if (hud.PowerUpBars != null) { Debug.Log("Power-up duration bars already exist; authored edits were preserved."); return; }
            Transform safe = hud.transform.Find("Safe Area");
            if (safe.Find(StackName) != null || hud.GetComponent<RunnerPowerUpBars>() != null)
                throw new InvalidOperationException("Existing duration-bar objects need inspection before replacing them.");
            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MainMenu/Fonts/Anton-Regular.ttf");
            if (font == null) throw new InvalidOperationException("Main Menu Anton font is required.");
            var stack = Rect(StackName, safe, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -164), new Vector2(216, 440));
            stack.SetAsFirstSibling();
            var rows = new PowerUpDurationRow[7];
            string[] names = { "SHIELD", "DOUBLE POINTS", "FLIGHT", "TRUCK", "MAGNET", "TIMED SHIELD", "SUPERCAR" };
            for (int i = 0; i < rows.Length; i++)
            {
                var card = Rect(names[i], stack, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * 64), new Vector2(216, 56));
                Panel(card, LevelMenuTheme.Paper, 3, 8);
                Label("Name", card, font, names[i], new Vector2(12, -20), new Vector2(140, 32), TextAnchor.MiddleLeft);
                Text time = Label("Remaining", card, font, "10s", new Vector2(156, -20), new Vector2(48, 32), TextAnchor.MiddleRight);
                var rail = Rect("Duration meter", card, new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 8), new Vector2(192, 10));
                Panel(rail, LevelMenuTheme.Cream, 1, 0);
                var area = Rect("Fill area", rail, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = Vector2.one; area.offsetMax = -Vector2.one;
                var fill = Rect("Fill", area, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
                Panel(fill, LevelMenuTheme.Accent, 0, 0);
                var slider = Undo.AddComponent<Slider>(rail.gameObject);
                slider.minValue = 0; slider.maxValue = 1; slider.fillRect = fill; slider.direction = Slider.Direction.LeftToRight;
                slider.interactable = false; slider.transition = Selectable.Transition.None;
                slider.navigation = new Navigation { mode = Navigation.Mode.None }; slider.SetValueWithoutNotify(1);
                var row = Undo.AddComponent<PowerUpDurationRow>(card.gameObject);
                var data = new SerializedObject(row);
                data.FindProperty("effect").enumValueIndex = i;
                data.FindProperty("remainingLabel").objectReferenceValue = time;
                data.FindProperty("durationMeter").objectReferenceValue = slider;
                data.ApplyModifiedPropertiesWithoutUndo(); rows[i] = row;
                card.gameObject.SetActive(false);
            }
            var controller = Undo.AddComponent<RunnerPowerUpBars>(hud.gameObject);
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("game").objectReferenceValue = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CubeDashGame>(true)).Single();
            serialized.FindProperty("safeRoot").objectReferenceValue = safe;
            serialized.FindProperty("stack").objectReferenceValue = stack;
            var array = serialized.FindProperty("rows"); array.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var settings = new SerializedObject(hud); settings.FindProperty("powerUpBars").objectReferenceValue = controller;
            settings.ApplyModifiedPropertiesWithoutUndo();
            foreach (string name in new[] { "Power-Up Ribbon", "Power-Up Status" })
            {
                Transform old = safe.Find(name);
                if (old != null) { Undo.RecordObject(old.gameObject, "Hide previous power-up banner"); old.gameObject.SetActive(false); }
            }
            stack.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
            Undo.RegisterCreatedObjectUndo(rect.gameObject, "Author compact power-up meter"); return rect;
        }

        private static void Panel(RectTransform rect, Color color, float border, float cut)
        {
            var graphic = Undo.AddComponent<MenuPanelGraphic>(rect.gameObject);
            graphic.color = color; graphic.CornerRadius = 0; graphic.CornerCut = cut;
            graphic.BorderWidth = border; graphic.BorderColor = LevelMenuTheme.Ink; graphic.raycastTarget = false;
        }

        private static Text Label(string name, Transform parent, Font font, string value, Vector2 position, Vector2 size, TextAnchor alignment)
        {
            var rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 0.5f), position, size);
            var text = Undo.AddComponent<MenuLabelText>(rect.gameObject);
            text.font = font; text.text = value; text.fontSize = 18; text.fontStyle = FontStyle.Normal;
            text.color = LevelMenuTheme.Ink; text.alignment = alignment; text.raycastTarget = false;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = 18;
            return text;
        }
    }
}
