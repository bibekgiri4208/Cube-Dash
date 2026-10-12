using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CubeDash.Editor
{
    /// <summary>Adds only the menu camera's sway component, preserving its pose and the rest of the scene.</summary>
    public static class MainMenuCameraMotion
    {
        [MenuItem("Tools/Cube Dash/Add Gentle Main Menu Camera Sway")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before adding camera sway.");
            Scene scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the menu camera sway.");
        }

        public static void ApplyToScene(Scene scene)
        {
            if (!scene.GetRootGameObjects().Any(root => root.GetComponent<MainMenuController>() != null))
                throw new InvalidOperationException("The Main Menu controller is required.");
            Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).SingleOrDefault(item => item.CompareTag("MainCamera"));
            if (camera == null) throw new InvalidOperationException("The authored Main Menu camera was not found.");
            if (camera.GetComponent<MainMenuCameraSway>() != null)
            {
                Debug.Log("Menu camera sway is already present. Existing motion settings were preserved."); return;
            }
            Undo.AddComponent<MainMenuCameraSway>(camera.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Added subtle Main Menu camera sway. Camera pose, lens, lighting, environment and gameplay were preserved.");
        }
    }
}
