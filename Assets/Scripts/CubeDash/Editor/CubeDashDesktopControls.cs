using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CubeDash.Editor
{
    /// <summary>Only updates input/UI presentation; preserves the authored city and visual assets.</summary>
    public static class CubeDashDesktopControls
    {
        [MenuItem("Tools/Cube Dash/Apply Desktop and Gamepad Controls")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            ApplyToScene(Object.FindAnyObjectByType<RunnerHud>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Desktop controls saved: touch UI hidden, keyboard/gamepad prompts, focused menu navigation and single-owner confirmation.");
        }

        public static void ApplyToScene(RunnerHud hud)
        {
            Transform safe = hud.transform.Find("Safe Area");
            safe.Find("Lane Controls").gameObject.SetActive(false);
            Transform existing = safe.Find("Desktop Controls Hint");
            Text hint;
            if (existing == null)
            {
                RectTransform rect = new GameObject("Desktop Controls Hint", typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
                rect.SetParent(safe, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
                rect.pivot = new Vector2(0.5f, 0);
                rect.anchoredPosition = new Vector2(0, 26);
                rect.sizeDelta = new Vector2(600, 36);
                hint = rect.GetComponent<Text>();
            }
            else hint = existing.GetComponent<Text>();
            hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hint.fontSize = 14;
            hint.alignment = TextAnchor.MiddleCenter;
            hint.color = new Color(0.12f, 0.24f, 0.30f);
            hint.raycastTarget = false;
            hint.text = "A / D or LEFT / RIGHT   ·   P / ESC pause   ·   R restart";

            Transform card = safe.Find("Menu Overlay/Menu Card");
            card.Find("Description").GetComponent<Text>().text = "Collect " + Object.FindAnyObjectByType<CubeDashGame>().PlayerCubeColor.ToString().ToUpperInvariant() +
                ". Dodge the rest.\nA / D or arrow keys to change lanes.";
            Button primary = card.Find("Primary Action").GetComponent<Button>();
            primary.GetComponentInChildren<Text>().text = "START RUN";
            Button restart = card.Find("Restart").GetComponent<Button>();
            primary.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = restart, selectOnUp = restart };
            restart.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = primary, selectOnUp = primary };
            card.Find("Keyboard Hint").GetComponent<Text>().text = "Enter / Space confirm   ·   Tab / arrows select   ·   Esc pause";
            Transform composition = safe.Find("Game Over Overlay/Abstract Composition");
            composition.Find("Tap Hint").GetComponent<Text>().text = "Enter / Space or R to retry";
            composition.Find("Retry").GetComponent<Button>().navigation = new Navigation { mode = Navigation.Mode.None };
            foreach (Button button in hud.GetComponentsInChildren<Button>(true))
            {
                Outline outline = button.GetComponent<Outline>();
                if (outline == null) outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.65f, 0.35f, 0.95f);
                outline.effectDistance = new Vector2(2, -2);
                outline.enabled = false;
            }
            EventSystem events = Object.FindAnyObjectByType<EventSystem>();
            InputSystemUIInputModule module = events != null ? events.GetComponent<InputSystemUIInputModule>() : null;
            if (module != null)
            {
                module.submit = null;
                module.cancel = null;
                module.deselectOnBackgroundClick = false;
                EditorUtility.SetDirty(module);
            }
        }
    }
}
