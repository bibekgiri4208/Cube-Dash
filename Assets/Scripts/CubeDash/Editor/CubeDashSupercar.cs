using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Saves the reference-derived Supercar, player presentation and manual toggle connection.</summary>
    public static class CubeDashSupercar
    {
        private const string Materials = "Assets/Material/Supercar";

        [MenuItem("Tools/Cube Dash/Add Reference Supercar")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder("Assets/Material", "Supercar");
            Mesh body = SaveMesh(SupercarMesh.BuildBody(), "Assets/3D Models/CubeDashSupercarBody.asset");
            Mesh wheel = SaveMesh(SupercarMesh.BuildWheel(), "Assets/3D Models/CubeDashSupercarWheel.asset");
            Material[] materials = {
                Lit("Charcoal Body", new Color(0.055f, 0.048f, 0.095f), 0.08f, 0.26f),
                Lit("Cyan Panels", new Color(0.005f, 0.70f, 0.92f), 0.08f, 0.38f),
                Lit("Black Intakes", new Color(0.007f, 0.009f, 0.020f), 0, 0.12f),
                Lit("Tinted Glass", new Color(0.038f, 0.092f, 0.19f), 0.05f, 0.45f),
                Lit("Graphite Facets", new Color(0.078f, 0.084f, 0.13f), 0.12f, 0.26f),
                Lit("Blue Rim and Trim", new Color(0.005f, 0.42f, 1), 0.1f, 0.32f, 1),
                Lit("Exhaust Alloy", new Color(0.51f, 0.58f, 0.65f), 0.6f, 0.50f),
                Lit("White X Headlights", new Color(0.92f, 1, 1), 0, 0.25f, 5),
                Lit("Red Tail LEDs", new Color(0.90f, 0.008f, 0.025f), 0, 0.25f, 2.2f),
                Lit("Faceted Tires", new Color(0.035f, 0.037f, 0.054f), 0, 0.16f)
            };
            materials[3].shader = Shader.Find("CubeDash/Supercar Glass");
            materials[3].SetColor("_BaseColor", new Color(0.008f, 0.019f, 0.042f));
            materials[3].SetColor("_UpperColor", new Color(0.065f, 0.16f, 0.29f));
            EditorUtility.SetDirty(materials[3]);
            const string carPath = "Assets/Prefab/CubeDash/Supercar.prefab";
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(carPath) != null;
            GameObject car = exists ? PrefabUtility.LoadPrefabContents(carPath) : new GameObject("Supercar");
            GameObject prefab;
            try
            {
                Render(car, body, materials);
                for (int i = 0; i < 4; i++)
                {
                    string name = WheelName(i); Transform child = car.transform.Find(name);
                    if (child == null) { child = new GameObject(name).transform; child.SetParent(car.transform, false); }
                    child.localPosition = new Vector3((i % 2 == 0 ? -1 : 1) * 1.05f, SupercarMesh.WheelRadius,
                        i < 2 ? SupercarMesh.FrontAxle : SupercarMesh.RearAxle);
                    child.localRotation = Quaternion.identity; child.localScale = Vector3.one;
                    Render(child.gameObject, wheel, materials);
                }
                prefab = PrefabUtility.SaveAsPrefabAsset(car, carPath);
            }
            finally { if (exists) PrefabUtility.UnloadPrefabContents(car); else Object.DestroyImmediate(car); }

            const string playerPath = "Assets/Prefab/CubeDash/PlayerCube.prefab";
            GameObject player = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                Transform visual = player.transform.Find("Supercar Visual");
                if (visual == null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.scene);
                    instance.name = "Supercar Visual"; visual = instance.transform; visual.SetParent(player.transform, false);
                }
                visual.localPosition = Vector3.down * 0.575f;
                visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one * 0.72f;
                visual.gameObject.SetActive(false);
                PlayerSupercar presentation = player.GetComponent<PlayerSupercar>();
                if (presentation == null) presentation = player.AddComponent<PlayerSupercar>();
                var settings = new SerializedObject(presentation);
                settings.FindProperty("car").objectReferenceValue = visual;
                var wheels = settings.FindProperty("wheels"); wheels.arraySize = 4;
                for (int i = 0; i < 4; i++) wheels.GetArrayElementAtIndex(i).objectReferenceValue = visual.Find(WheelName(i));
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(player, playerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            if (game == null) throw new InvalidOperationException("The authored Cube Dash level is required.");
            var gameSettings = new SerializedObject(game);
            gameSettings.FindProperty("supercarPresentation").objectReferenceValue = game.Player.GetComponent<PlayerSupercar>();
            gameSettings.FindProperty("supercarDoublePressWindow").floatValue = 0.35f;
            gameSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Supercar saved: cyan/charcoal faceted body, real wheel openings, wraparound glass, X headlights, five-spoke blue rims, rear wing/fin, red LEDs, twin hollow exhausts and diffuser. Double-Enter manually toggles it; normal speed and cube collision rules are unchanged. Truck/Plane take temporary presentation priority.");
        }

        private static string WheelName(int index) => (index % 2 == 0 ? "Left" : "Right") + (index < 2 ? " Front Wheel" : " Rear Wheel");

        private static void Render(GameObject target, Mesh mesh, Material[] materials)
        {
            MeshFilter filter = target.GetComponent<MeshFilter>(); if (filter == null) filter = target.AddComponent<MeshFilter>();
            MeshRenderer renderer = target.GetComponent<MeshRenderer>(); if (renderer == null) renderer = target.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh; renderer.sharedMaterials = materials;
        }

        private static Mesh SaveMesh(Mesh generated, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(generated, path); return generated; }
            EditorUtility.CopySerialized(generated, existing); Object.DestroyImmediate(generated); EditorUtility.SetDirty(existing); return existing;
        }

        private static Material Lit(string name, Color color, float metallic, float smoothness, float emission = 0)
        {
            string path = Materials + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
            if (emission > 0)
            {
                material.SetColor("_EmissionColor", color * emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                material.EnableKeyword("_EMISSION");
            }
            material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }
    }
}
