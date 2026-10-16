using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Bakes real map scenes into static menu artwork; no gameplay map is saved or rebuilt.</summary>
    public static class MainMenuMapIllustrations
    {
        public const string Folder = "Assets/MainMenu/Map Previews";
        public const int Width = 1200, Height = 450;
        public static string ImagePath(string name) => Folder + "/" + name + ".png";

        public static void RenderBeachFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            Render("Beach"); AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Cube Dash/Render Actual Map Selection Previews")]
        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/MainMenu", "Map Previews");
            foreach (string name in MainMenuMapSelection.MapNames) Render(name);
            Scene menu = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(menu);
            if (!EditorSceneManager.SaveScene(menu)) throw new InvalidOperationException("Could not save actual map artwork.");
            AssetDatabase.SaveAssets();
        }

        public static void ApplyToScene(Scene scene)
        {
            var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
            foreach (string name in MainMenuMapSelection.MapNames)
            {
                var preview = menu.MapPanel.transform.Find("Card/" + name + "/Map illustration").GetComponent<MapPreviewGraphic>();
                var image = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath(name));
                if (image == null) throw new InvalidOperationException("Render the actual map preview first: " + name);
                preview.Illustration = image; preview.color = Color.white; preview.raycastTarget = false;
                EditorUtility.SetDirty(preview);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void Render(string name)
        {
            string path = "Assets/Scenes/" + MainMenuMapSelection.SceneNameFor(name) + ".unity";
            Scene scene = EditorSceneManager.OpenScene(path);
            var roots = scene.GetRootGameObjects();
            var game = roots.SelectMany(root => root.GetComponentsInChildren<CubeDashGame>(true)).Single();
            Camera camera = game.GameCamera;
            foreach (Canvas canvas in roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true))) canvas.gameObject.SetActive(false);
            game.Player.gameObject.SetActive(false);
            foreach (CubeWake wake in roots.SelectMany(root => root.GetComponentsInChildren<CubeWake>(true))) wake.gameObject.SetActive(false);
            foreach (SceneryRise rise in roots.SelectMany(root => root.GetComponentsInChildren<SceneryRise>(true))) rise.Settle();
            RoadCurve.Clear();

            var profiles = roots.SelectMany(root => root.GetComponentsInChildren<Volume>(true)).Where(volume => volume.sharedProfile != null)
                .Select(volume =>
                {
                    var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    profile.components = volume.sharedProfile.components.Select(component => Object.Instantiate(component)).ToList();
                    volume.profile = profile;
                    if (profile.TryGet(out DepthOfField focus)) focus.active = false;
                    if (profile.TryGet(out MotionBlur blur)) blur.active = false;
                    return profile;
                }).ToArray();
            Vector3 origin = game.Player.position;
            Vector3 position = new Vector3(5, 6, -10), look = new Vector3(0, 1, 45);
            if (name == "Jungle") { position = new Vector3(-4, 5, -10); look = new Vector3(1, 1, 39); }
            if (name == "Beach") { position = new Vector3(-5, 6, -11); look = new Vector3(-8, 1, 43); }
            if (name == "Country Side") { position = new Vector3(-8, 7, -13); look = new Vector3(-5, 1, 43); }
            if (name == "Dynamic") { position = new Vector3(12, 14, 56); look = new Vector3(-2, 1, 175); }
            camera.transform.position = new Vector3(origin.x, 0, origin.z) + position;
            camera.transform.LookAt(new Vector3(origin.x, 0, origin.z) + look);
            camera.fieldOfView = 48;
            var target = new RenderTexture(Width, Height, 24); var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target;
                for (int i = 0; i < 6; i++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply();
                File.WriteAllBytes(ImagePath(name), image.EncodeToPNG());
                AssetDatabase.ImportAsset(ImagePath(name), ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(ImagePath(name));
                importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous; ShaderUtil.allowAsyncCompilation = async;
                foreach (VolumeProfile profile in profiles)
                {
                    foreach (VolumeComponent component in profile.components) Object.DestroyImmediate(component);
                    Object.DestroyImmediate(profile);
                }
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
            // Drop only in-memory capture changes; never save the gameplay scene.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
