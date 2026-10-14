using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CubeDash.Editor
{
    /// <summary>Adjusts only the four landing-screen ribbons, preserving control identities and all artwork.</summary>
    public static class MainMenuRibbonPolish
    {
        [MenuItem("Tools/Cube Dash/Improve Main Menu Ribbon Hover and Spacing")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            Scene scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save Main Menu ribbon feedback.");
        }

        public static void ApplyToScene(Scene scene)
        {
            var menu = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MainMenuController>(true)).Single();
            Transform parent = menu.GetComponentInChildren<Canvas>(true).transform.Find("Left menu ribbons");
            if (parent == null) throw new InvalidOperationException("Expected the saved main-menu ribbons.");
            string[] names = { "START", "SHOP", "OPTIONS", "QUIT" };
            var buttons = names.Select(n => parent.Find(n)?.GetComponent<ArcadeButton>()).ToArray();
            if (buttons.Any(b => b == null || b.GetComponent<MenuRibbonGraphic>() == null)) throw new InvalidOperationException("Expected all four ribbon controls.");
            if (buttons.All(b => b.RibbonFeedback)) return; // Keep later manual placement/timing edits.
            if (buttons.Any(b => b.RibbonFeedback)) throw new InvalidOperationException("Inspect partially configured ribbon feedback first.");
            float[] tops = { 248, 408, 552, 696 };
            for (int i = 0; i < buttons.Length; i++)
            {
                var rect = buttons[i].GetComponent<RectTransform>();
                Undo.RecordObject(rect, "Space main menu ribbons");
                rect.anchoredPosition = new Vector2(68, -tops[i]);
                var data = new SerializedObject(buttons[i]); data.FindProperty("ribbonFeedback").boolValue = true;
                data.ApplyModifiedProperties();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Main Menu ribbons saved: aligned placement, safe spacing for bounded smooth hover/press feedback, and a shape-matched white focus ring. Navigation, callbacks, panels, world artwork and gameplay preserved.");
        }
    }
}
