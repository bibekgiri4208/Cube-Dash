using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CubeDash.Editor
{
    /// <summary>Adds saved animation components without rebuilding artwork, controls or navigation.</summary>
    public static class MainMenuUiAnimations
    {
        [MenuItem("Tools/Cube Dash/Animate Main Menu UI")]
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
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save menu UI animation.");
        }
        public static void ApplyToScene(Scene scene)
        {
            var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
            var canvas = menu.GetComponentInChildren<Canvas>(true);
            var ribbons = canvas.transform.Find("Left menu ribbons");
            if (ribbons == null) throw new InvalidOperationException("Main menu ribbons are required.");
            if (ribbons.GetComponent<MainMenuEntrance>() == null)
            {
                string[] names = { "START", "SHOP", "OPTIONS", "QUIT" };
                var entrance = Undo.AddComponent<MainMenuEntrance>(ribbons.gameObject);
                var data = new SerializedObject(entrance);
                var entries = data.FindProperty("ribbons"); var fades = data.FindProperty("fades");
                entries.arraySize = fades.arraySize = names.Length;
                for (int i = 0; i < names.Length; i++)
                {
                    var rect = ribbons.Find(names[i]).GetComponent<RectTransform>();
                    var group = rect.GetComponent<CanvasGroup>();
                    if (group == null) group = Undo.AddComponent<CanvasGroup>(rect.gameObject);
                    entries.GetArrayElementAtIndex(i).objectReferenceValue = rect;
                    fades.GetArrayElementAtIndex(i).objectReferenceValue = group;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (GameObject panel in new[] { menu.ShopPanel, menu.OptionsPanel })
            {
                if (panel.GetComponent<MenuPanelMotion>() == null)
                {
                    if (panel.GetComponent<CanvasGroup>() == null) Undo.AddComponent<CanvasGroup>(panel);
                    var motion = Undo.AddComponent<MenuPanelMotion>(panel);
                    var data = new SerializedObject(motion);
                    data.FindProperty("card").objectReferenceValue = panel.transform.Find("Card");
                    data.FindProperty("shadow").objectReferenceValue = panel.transform.Find("Soft card shadow");
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (ArcadeButton feedback in panel.GetComponentsInChildren<ArcadeButton>(true)) EnableFeedback(feedback);
            }
            foreach (ArcadeButton feedback in ribbons.GetComponentsInChildren<ArcadeButton>(true)) EnableFeedback(feedback);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        private static void EnableFeedback(ArcadeButton feedback)
        {
            if (feedback.LivelyFeedback) return;
            var data = new SerializedObject(feedback); data.FindProperty("livelyFeedback").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
