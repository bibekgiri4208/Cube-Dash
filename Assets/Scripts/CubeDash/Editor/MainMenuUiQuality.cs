using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CubeDash.Editor
{
    /// <summary>Preserves UI component identities, layout, style and button events while improving text sampling.</summary>
    public static class MainMenuUiQuality
    {
        [MenuItem("Tools/Cube Dash/Improve Main Menu UI Edge Quality")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before improving UI quality.");
            Scene scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/CubeDash/MenuLabelText.cs");
            if (script == null) throw new InvalidOperationException("The menu label sampling script was not found.");
            var menu = scene.GetRootGameObjects().Select(root => root.GetComponent<MainMenuController>()).FirstOrDefault(item => item != null);
            if (menu == null) throw new InvalidOperationException("The authored Main Menu controller is required.");
            int labels = 0;
            foreach (MenuRibbonGraphic ribbon in menu.GetComponentsInChildren<MenuRibbonGraphic>(true))
            {
                Text label = ribbon.GetComponentInChildren<Text>(true);
                if (label == null || label is MenuLabelText) continue;
                if (label.GetType() != typeof(Text)) continue; // Preserve any unrelated custom label component.
                Undo.RecordObject(label, "Improve menu label sampling");
                var serialized = new SerializedObject(label);
                serialized.FindProperty("m_Script").objectReferenceValue = script;
                serialized.ApplyModifiedProperties(); labels++;
            }
            if (labels != 0) EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the menu UI quality.");
            Debug.Log($"Upgraded {labels} menu labels to higher-resolution font sampling. Font, text, layout, component identities, button events, environment, camera and lighting were preserved.");
        }

        public static void ApplyAndCaptureFromCommandLine()
        {
            ApplyFromCommandLine(); MainMenuSceneBuilder.CaptureFromCommandLine();
        }
    }
}
