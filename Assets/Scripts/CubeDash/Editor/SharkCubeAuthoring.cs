using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Adds the reference-inspired shark cube without rebuilding existing shop cards, menu art or gameplay maps.</summary>
    public static class SharkCubeAuthoring
    {
        public const string MeshPath = "Assets/3D Models/Shark Cube/Shark Cube.asset";
        public const string MaterialFolder = "Assets/Material/Shark Cube";
        public const string PrefabPath = "Assets/Prefab/CubeDash/Shark Cube.prefab";
        public const string IllustrationPath = "Assets/MainMenu/Shop Skins/Shark Cube.png";

        [MenuItem("Tools/Cube Dash/Add Shark Cube Skin")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            PrepareAssets();
            string original = File.ReadAllText(MainMenuSceneBuilder.ScenePath);
            Scene scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            BakeIllustration();
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the Shark Cube shop card.");
            MainMenuSkinsRevamp.PreserveUnchangedDocuments(MainMenuSceneBuilder.ScenePath, original);
            AssetDatabase.SaveAssets();
            Debug.Log("Added Shark Cube: blue bevelled cube, white jagged belly, dorsal/side fins and gills; 100-coin saved buy/equip, actual-model illustration and menu preview. Existing collisions, maps, audio and vehicle skins preserved.");
        }

        public static void PrepareAssets()
        {
            Folder("Assets/3D Models", "Shark Cube"); Folder("Assets/Material", "Shark Cube");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                var cube = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/CubeDashBeveledCube.asset");
                if (cube == null) throw new InvalidOperationException("The original bevelled cube mesh is required.");
                mesh = SharkCubeMesh.Build(cube); AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            Material[] materials = {
                Material("Shark Blue", new Color(0.20f, 0.34f, 0.52f)),
                Material("Shark Belly Ivory", new Color(0.89f, 0.90f, 0.88f)),
                Material("Shark Gills Navy", new Color(0.10f, 0.23f, 0.37f)) };
            string original = File.ReadAllText(PlayerCubePresentationBuilder.PlayerPath);
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerCubePresentationBuilder.PlayerPath);
            try
            {
                var visual = player.transform.Find("Cube Visual").GetComponent<PlayerCubeVisual>();
                if (visual == null) throw new InvalidOperationException("The existing cube presentation is required.");
                Set(visual, "sharkMesh", mesh); SetArray(visual, "sharkMaterials", materials);
                if (PrefabUtility.SaveAsPrefabAsset(player, PlayerCubePresentationBuilder.PlayerPath) == null)
                    throw new InvalidOperationException("Could not save the player skin references.");
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            MainMenuSkinsRevamp.PreserveUnchangedDocuments(PlayerCubePresentationBuilder.PlayerPath, original);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                var preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerCubePresentationBuilder.PreviewPath);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                    model.name = "Shark Cube";
                    var visual = model.GetComponentInChildren<PlayerCubeVisual>(true);
                    // Explicit references also support an older source preview with local overrides.
                    Set(visual, "sharkMesh", mesh); SetArray(visual, "sharkMaterials", materials);
                    visual.ApplySkin(PlayerSkin.SharkCube);
                    if (PrefabUtility.SaveAsPrefabAsset(model, PrefabPath) == null) throw new InvalidOperationException("Could not save the visual-only shark prefab.");
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            AssetDatabase.SaveAssets();
        }

        public static void ApplyToScene(Scene scene)
        {
            var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
            var shop = menu.GetComponent<MenuSkinShop>();
            if (shop == null || shop.Cards.Length == 0) throw new InvalidOperationException("The existing unified skin shop is required.");
            if (shop.Cards.Any(card => card.Skin == PlayerSkin.SharkCube))
            { menu.ShopPanel.GetComponent<MenuSkinsLayout>().RefreshProducts(); return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IllustrationPath);
            if (prefab == null || sprite == null) throw new InvalidOperationException("Prepare the Shark Cube assets and illustration first.");
            Transform slots = shop.Cards[0].transform.parent.parent.parent;
            if (slots.Find("SHARK CUBE") != null) throw new InvalidOperationException("Inspect the partial Shark Cube card before authoring.");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            model.name = "Equipped SHARK CUBE Preview";
            Transform cube = shop.CubePreview.transform;
            model.transform.SetParent(cube.parent, false);
            model.transform.localPosition = cube.localPosition;
            model.transform.localRotation = cube.localRotation;
            model.transform.localScale = cube.localScale;
            model.SetActive(false);

            var product = Object.Instantiate(shop.Cards[0].transform.parent.parent.gameObject, slots);
            product.name = "SHARK CUBE";
            Transform contents = product.transform.Find("Contents");
            contents.Find("Name").GetComponent<Text>().text = "SHARK CUBE";
            contents.Find("Price").GetComponent<Text>().text = "100 COINS";
            contents.Find("Status").GetComponent<Text>().text = "NEED 100 MORE COINS";
            contents.Find("Model Illustration").GetComponent<Image>().sprite = sprite;
            var card = product.GetComponentInChildren<MenuSkinCard>(true);
            Set(card, "shop", shop); Set(card, "preview", model);
            var data = new SerializedObject(card); data.FindProperty("skin").intValue = (int)PlayerSkin.SharkCube;
            data.ApplyModifiedPropertiesWithoutUndo();
            card.Action.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(card.Action.onClick, card.PurchaseOrToggle);
            card.Action.GetComponentInChildren<Text>(true).text = "BUY";
            MenuSkinCard[] cards = shop.Cards.Concat(new[] { card }).ToArray();
            SetArray(shop, "cards", cards);
            var layout = menu.ShopPanel.GetComponent<MenuSkinsLayout>();
            SetArray(layout, "contents", cards.Select(item => item.transform.parent.GetComponent<RectTransform>()).ToArray());
            SetArray(layout, "actions", cards.Select(item => item.Action).ToArray());
            layout.RefreshProducts();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("Tools/Cube Dash/Rebuild Shark Cube Mesh and Illustration")]
        public static void RebuildArtFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            PrepareAssets();
            var source = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/CubeDashBeveledCube.asset");
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath); var rebuilt = SharkCubeMesh.Build(source);
            EditorUtility.CopySerialized(rebuilt, saved); Object.DestroyImmediate(rebuilt); EditorUtility.SetDirty(saved);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            BakeIllustration(true);
        }

        private static void BakeIllustration(bool force = false)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<Sprite>(IllustrationPath) != null) return;
            Folder("Assets/MainMenu", "Shop Skins");
            Scene previous = SceneManager.GetActiveScene();
            Scene preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(preview);
            RenderTexture target = null; Texture2D image = null; RenderTexture active = RenderTexture.active;
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), preview);
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); model.transform.localScale = Vector3.one;
                foreach (Transform child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                var renderers = model.GetComponentsInChildren<Renderer>(true); Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                var camera = new GameObject("Shark Illustration Camera").AddComponent<Camera>();
                camera.enabled = false; camera.cullingMask = 1 << 31; camera.orthographic = true;
                camera.transform.position = bounds.center + new Vector3(0.65f, 0.64f, -1.9f).normalized * 4;
                camera.transform.LookAt(bounds.center); camera.orthographicSize = bounds.size.y * 0.66f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(1, 0.91f, 0.92f); camera.allowHDR = false;
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.64f, 0.67f, 0.73f);
                var sun = new GameObject("Shark Illustration Sun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.3f; sun.cullingMask = 1 << 31;
                sun.transform.rotation = Quaternion.Euler(43, -32, 0); RenderSettings.sun = sun;
                target = new RenderTexture(900, 520, 24); camera.targetTexture = target;
                for (int i = 0; i < 6; i++) camera.Render();
                RenderTexture.active = target; image = new Texture2D(900, 520, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 900, 520), 0, 0); image.Apply();
                File.WriteAllBytes(IllustrationPath, image.EncodeToPNG());
                AssetDatabase.ImportAsset(IllustrationPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(IllustrationPath);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            }
            finally
            {
                RenderTexture.active = active; SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(preview, true);
                if (target != null) Object.DestroyImmediate(target); if (image != null) Object.DestroyImmediate(image);
                ShaderUtil.allowAsyncCompilation = async;
            }
        }

        private static Material Material(string name, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", 0.16f); material.SetFloat("_Metallic", 0);
            material.SetColor("_EmissionColor", Color.black); material.DisableKeyword("_EMISSION");
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private static void Folder(string parent, string name)
        { if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name); }

        private static void Set(Object target, string field, Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray<T>(Object target, string field, T[] values) where T : Object
        {
            var data = new SerializedObject(target); var array = data.FindProperty(field); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
