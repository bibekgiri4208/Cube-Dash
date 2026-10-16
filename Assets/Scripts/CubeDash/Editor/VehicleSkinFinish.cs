using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CubeDash.Editor
{
    /// <summary>Authors compact gameplay vehicles separately from menu previews, with model-specific rear-light ribbons.</summary>
    public static class VehicleSkinFinish
    {
        public const float MenuModelScale = 0.55f;
        // Imported vehicles are over six units long. A raw 0.55 mesh scale still makes them
        // roughly four metres long in maps. Reduce that previous gameplay size to 55%,
        // without shrinking the shared player root/collider, power-ups or menu previews.
        public const float GameplaySizeMultiplier = 0.55f;
        public const float GameplayModelScale = MenuModelScale * GameplaySizeMultiplier;
        public const string MaterialFolder = "Assets/Material/Vehicle Skins";
        public const string MaterialPath = MaterialFolder + "/Rear Lamp Light Trail.mat";

        [MenuItem("Tools/Cube Dash/Set Vehicle Skins Scale and Rear Light Trails")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }
        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            ApplyToPlayer();
            string original = File.ReadAllText(MainMenuSceneBuilder.ScenePath);
            Scene scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToMenu(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save vehicle preview sizes.");
            MainMenuSkinsRevamp.PreserveUnchangedDocuments(MainMenuSceneBuilder.ScenePath, original);
            AssetDatabase.SaveAssets();
        }

        public static void ApplyToMenu(Scene scene)
        {
            var shop = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MenuSkinShop>(true)).Single();
            foreach (var card in shop.Cards.Where(card => card.Skin == PlayerSkin.MonsterTruck || card.Skin == PlayerSkin.MilitaryVan))
            {
                Transform model = card.Preview.transform;
                float floor = Bounds(model).min.y;
                model.localScale = Vector3.one * MenuModelScale;
                model.position += Vector3.up * (floor - Bounds(model).min.y);
                PrefabUtility.RecordPrefabInstancePropertyModifications(model);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void ApplyToPlayer()
        {
            string path = MainMenuSkinsRevamp.PlayerPath, original = File.ReadAllText(path);
            GameObject player = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var models = player.GetComponent<PlayerSupercar>().AdditionalSkins;
                if (models.Length != 2 || models.Any(model => model.root == null))
                    throw new InvalidOperationException("The two authored vehicle skins are required.");
                if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Material", "Vehicle Skins");
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("CubeDash/Supercar Light Trail"));
                    // Warm red-orange lens color sampled from the vehicles' original emissive atlas.
                    Color lens = new Color(0.992f, 0.322f, 0.106f).linear * 3;
                    lens.a = 0.8f; material.SetColor("_Tint", lens);
                    AssetDatabase.CreateAsset(material, MaterialPath);
                }
                foreach (var model in models)
                {
                    if (model.skin != PlayerSkin.MonsterTruck && model.skin != PlayerSkin.MilitaryVan)
                        throw new InvalidOperationException("Unknown vehicle skin; refusing to overwrite it.");
                    Transform root = model.root;
                    var bounds = MainMenuSkinsRevamp.LocalBounds(root, root);
                    root.localScale = Vector3.one * GameplayModelScale;
                    root.localPosition = new Vector3(-bounds.center.x * GameplayModelScale, -0.575f - bounds.min.y * GameplayModelScale, -bounds.center.z * GameplayModelScale);
                    Transform effect = root.Find("Rear Light Trails");
                    if (effect == null)
                    {
                        effect = new GameObject("Rear Light Trails", typeof(SupercarLightTrail)).transform;
                        effect.SetParent(root, false);
                    }
                    var trail = effect.GetComponent<SupercarLightTrail>();
                    if (trail == null) throw new InvalidOperationException("Unrecognized rear-light object.");
                    var renderer = effect.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.enabled = false;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    var settings = new SerializedObject(trail);
                    settings.FindProperty("car").objectReferenceValue = root;
                    settings.FindProperty("leftLamp").vector3Value = Lamp(model.skin, false);
                    settings.FindProperty("rightLamp").vector3Value = Lamp(model.skin, true);
                    settings.FindProperty("width").floatValue = (model.skin == PlayerSkin.MonsterTruck ? 0.12f : 0.34f) * GameplaySizeMultiplier;
                    settings.FindProperty("minimumLength").floatValue = 2.4f * GameplaySizeMultiplier;
                    settings.FindProperty("maximumLength").floatValue = 4.0f * GameplaySizeMultiplier;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(root);
                }
                PrefabUtility.SaveAsPrefabAsset(player, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            MainMenuSkinsRevamp.PreserveUnchangedDocuments(path, original);
        }

        // Model-local centers of the rear-facing red lenses, measured from the original imported geometry.
        public static Vector3 Lamp(PlayerSkin skin, bool right)
        {
            return skin == PlayerSkin.MonsterTruck
                ? new Vector3(right ? 1.00584f : -1.00486f, 1.96364f, -3.125f)
                : new Vector3(right ? 0.98946f : -0.98946f, 0.74832f, -3.114f);
        }

        private static Bounds Bounds(Transform model)
        {
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
