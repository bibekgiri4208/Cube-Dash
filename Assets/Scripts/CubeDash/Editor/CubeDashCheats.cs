using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    public static class CubeDashCheats
    {
        [MenuItem("Tools/Cube Dash/Add Testing Cheat Indicator")]
        public static void AddIndicatorFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) AddIndicatorFromCommandLine();
        }

        public static void AddIndicatorFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before authoring the cheat indicator.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save scene changes before authoring the cheat indicator.");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            RunnerHud hud = Object.FindAnyObjectByType<RunnerHud>();
            if (hud == null) throw new InvalidOperationException("Level needs its saved Runner HUD.");
            ConfigureHud(hud);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Saved testing cheat indicator; press I to toggle session-only invincibility. Normal best scores exclude cheated runs.");
        }

        internal static void ConfigureHud(RunnerHud hud)
        {
            Transform safe = hud.transform.Find("Safe Area");
            if (safe == null) throw new InvalidOperationException("HUD needs its saved Safe Area.");
            Text text = safe.Find("Cheat Status")?.GetComponent<Text>();
            if (text == null)
            {
                var created = new GameObject("Cheat Status", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                created.transform.SetParent(safe, false);
                text = created.GetComponent<Text>();
            }
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18; text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1, 0.8f, 0.22f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = "CHEAT: INVINCIBILITY ACTIVE  |  I TO DISABLE";
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -130);
            rect.sizeDelta = new Vector2(620, 48);
            rect.SetAsLastSibling();
            text.gameObject.SetActive(false);
            var serialized = new SerializedObject(hud);
            serialized.FindProperty("cheatStatus").objectReferenceValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
