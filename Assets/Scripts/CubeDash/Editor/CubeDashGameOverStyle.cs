using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CubeDash.Editor
{
    /// <summary>Authors only the abstract game-over overlay; start/pause screens stay intact.</summary>
    public static class CubeDashGameOverStyle
    {
        private static readonly Color Ink = new Color(0.12f, 0.24f, 0.27f);
        private static readonly Color Muted = new Color(0.3f, 0.44f, 0.46f);
        private static readonly Color Coral = new Color(0.88f, 0.25f, 0.22f);

        [MenuItem("Tools/Cube Dash/Apply Abstract Game-Over UI")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            RunnerHud hud = UnityEngine.Object.FindAnyObjectByType<RunnerHud>();
            CubeDashGame game = UnityEngine.Object.FindAnyObjectByType<CubeDashGame>();
            Transform safe = hud.transform.Find("Safe Area");
            if (safe.Find("Game Over Overlay") != null)
                throw new InvalidOperationException("Game-over overlay already exists; existing UI will not be overwritten.");

            RectTransform overlay = Rect("Game Over Overlay", safe);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            overlay.gameObject.AddComponent<Image>().color = new Color(0.93f, 0.97f, 0.94f, 0.48f);
            RectTransform composition = Rect("Abstract Composition", overlay);
            composition.anchorMin = composition.anchorMax = new Vector2(0.5f, 0.62f);
            composition.sizeDelta = new Vector2(320, 320);

            Square("Small Blue Square", composition, new Vector2(-37, 107), 22, new Color(0.18f, 0.43f, 0.82f), -12);
            Square("Small Green Square", composition, new Vector2(36, 127), 17, new Color(0.22f, 0.68f, 0.47f), 10);
            Square("Main Coral Square", composition, new Vector2(0, 119), 39, Coral, 7);
            Label("Heading", composition, "GAME OVER", 17, Ink, new Vector2(0, 68), new Vector2(300, 30));
            Text score = Label("Score", composition, "0", 68, Ink, new Vector2(0, 7), new Vector2(300, 88));
            score.fontStyle = FontStyle.Bold;
            Label("Score Caption", composition, "POINTS", 13, Muted, new Vector2(0, -42), new Vector2(300, 25));

            RectTransform retry = Rect("Retry", composition);
            retry.anchoredPosition = new Vector2(0, -103);
            retry.sizeDelta = new Vector2(204, 46);
            Image background = retry.gameObject.AddComponent<Image>();
            background.color = Coral;
            Button button = retry.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            UnityEventTools.AddPersistentListener(button.onClick, game.StartRun);
            Label("Label", retry, "TRY AGAIN", 17, Color.white, Vector2.zero, new Vector2(200, 42));
            Label("Tap Hint", composition, "Tap anywhere to retry", 13, Muted, new Vector2(0, -147), new Vector2(300, 26));
            Set(hud, "gameOverOverlay", overlay.gameObject);
            Set(hud, "gameOverScore", score);
            overlay.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Saved abstract game-over UI: cube motif, score, and retry; start/pause screens unchanged.");
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void Square(string name, Transform parent, Vector2 position, float size, Color color, float angle)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchoredPosition = position;
            rect.sizeDelta = Vector2.one * size;
            rect.localRotation = Quaternion.Euler(0, 0, angle);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static Text Label(string name, Transform parent, string value, int size, Color color, Vector2 position, Vector2 bounds)
        {
            RectTransform rect = Rect(name, parent);
            rect.anchoredPosition = position;
            rect.sizeDelta = bounds;
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
