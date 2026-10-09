using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Bakes a reusable 3D coin into the existing slots without creating objects during a run.</summary>
    public static class CubeDashCoins
    {
        private const string Prefabs = "Assets/Prefab/CubeDash/";
        private const string Materials = "Assets/Material/Coins";

        [MenuItem("Tools/Cube Dash/Add 3D Coins")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder("Assets/Material", "Coins");
            Mesh generated = CoinMesh.Build();
            string modelPath = "Assets/3D Models/CubeDashCoin.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(modelPath);
            if (mesh == null) { mesh = generated; AssetDatabase.CreateAsset(mesh, modelPath); }
            else { EditorUtility.CopySerialized(generated, mesh); Object.DestroyImmediate(generated); EditorUtility.SetDirty(mesh); }
            Material gold = Gold("Coin Gold", new Color(1f, 0.62f, 0.065f), 0.55f, 0.46f);
            Material edge = Gold("Coin Edge", new Color(0.62f, 0.28f, 0.025f), 0.48f, 0.35f);
            Material relief = Gold("Coin Relief", new Color(1f, 0.83f, 0.28f), 0.5f, 0.5f);
            GameObject coin = BuildCoin(mesh, new[] { gold, edge, relief });
            UpdateSlot(coin);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            if (game == null) throw new InvalidOperationException("The authored Cube Dash level is required.");
            foreach (TrackSegment segment in game.Track.Segments)
                foreach (RunnerCube slot in segment.Cubes)
                {
                    // Preserve the saved layouts, colors, active slots and collision bounds; only change presentation.
                    slot.Configure(slot.Color, game.Track.ColorMaterial(slot.Color), slot.Color == game.PlayerCubeColor);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(slot.GetComponent<Renderer>());
                    PrefabUtility.RecordPrefabInstancePropertyModifications(slot.CoinVisual.gameObject);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(slot.CoinVisual);
                }
            RunnerHud hud = Object.FindAnyObjectByType<RunnerHud>();
            Transform description = hud.transform.Find("Safe Area/Menu Overlay/Menu Card/Description");
            description.GetComponent<Text>().text = "Collect coins. Dodge obstacles.";
            var settings = new SerializedObject(hud);
            Text label = (Text)settings.FindProperty("speed").objectReferenceValue;
            label.text = "COLLECT COINS  ·  0 m"; label.color = new Color(1f, 0.78f, 0.25f);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("3D coins saved: double-sided gold mesh, raised rim and star, pooled visuals and coin HUD. Obstacles, layouts, scoring and power-ups preserved.");
        }

        private static GameObject BuildCoin(Mesh mesh, Material[] materials)
        {
            string path = Prefabs + "Coin.prefab";
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject("Coin", typeof(MeshFilter), typeof(MeshRenderer));
            try
            {
                root.GetComponent<MeshFilter>().sharedMesh = mesh;
                root.GetComponent<MeshRenderer>().sharedMaterials = materials;
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        }

        private static void UpdateSlot(GameObject coin)
        {
            string path = Prefabs + "ObstacleCube.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform visual = root.transform.Find("Coin Visual");
                if (visual == null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(coin, root.scene);
                    visual = instance.transform; visual.SetParent(root.transform, false); visual.name = "Coin Visual";
                }
                visual.localPosition = Vector3.zero; visual.localScale = Vector3.one;
                visual.localRotation = Quaternion.Euler(0, 25, 0); visual.gameObject.SetActive(false);
                var settings = new SerializedObject(root.GetComponent<RunnerCube>());
                settings.FindProperty("coinVisual").objectReferenceValue = visual;
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Material Gold(string name, Color color, float metallic, float smoothness)
        {
            string path = Materials + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 0.12f);
            material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }
    }
}
