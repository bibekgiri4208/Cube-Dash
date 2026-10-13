using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CubeDash.Editor
{
    /// <summary>Adds selection feedback only; existing volume controls, geometry and callbacks stay intact.</summary>
    public static class MainMenuSliderFocus
    {
        public const string FrameName = "Slider focus frame";

        [MenuItem("Tools/Cube Dash/Add Options Slider Focus Feedback")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
            Scene scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save slider feedback.");
        }

        public static void ApplyToScene(Scene scene)
        {
            var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
            if (menu.OptionsPanel == null) return;
            foreach (Slider slider in menu.OptionsPanel.GetComponentsInChildren<Slider>(true))
            {
                Transform existing = slider.transform.Find(FrameName);
                Graphic frame = existing != null ? existing.GetComponent<Graphic>() : null;
                if (existing != null && frame == null) throw new InvalidOperationException("A user object uses the slider focus-frame name.");
                if (frame == null)
                {
                    var go = new GameObject(FrameName, typeof(RectTransform));
                    go.transform.SetParent(slider.transform, false); go.transform.SetAsFirstSibling();
                    Undo.RegisterCreatedObjectUndo(go, "Add slider focus feedback");
                    var rect = go.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.offsetMin = new Vector2(-8, -6); rect.offsetMax = new Vector2(8, 6);
                    var graphic = Undo.AddComponent<MenuPanelGraphic>(go);
                    graphic.color = new Color(1, 0.99f, 0.94f);
                    graphic.CornerRadius = 0; graphic.CornerCut = 8;
                    graphic.BorderWidth = 3; graphic.BorderColor = new Color(0.95f, 0.02f, 0.11f);
                    graphic.raycastTarget = false; graphic.enabled = false; frame = graphic;
                }
                var feedback = slider.GetComponent<MenuSliderFocus>();
                if (feedback == null) feedback = Undo.AddComponent<MenuSliderFocus>(slider.gameObject);
                var serialized = new SerializedObject(feedback);
                if (serialized.FindProperty("focusFrame").objectReferenceValue == null)
                {
                    Undo.RecordObject(feedback, "Wire slider focus frame");
                    serialized.FindProperty("focusFrame").objectReferenceValue = frame;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
