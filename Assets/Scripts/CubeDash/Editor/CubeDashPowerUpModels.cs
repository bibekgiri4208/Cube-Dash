using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Authors separate power-up slots, three pickup models and Magnet settings without replacing track objects.</summary>
    public static class CubeDashPowerUpModels
    {
        [MenuItem("Tools/Cube Dash/Add Magnet and 3D Power-Up Models")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Material/PowerUpModels")) AssetDatabase.CreateFolder("Assets/Material", "PowerUpModels");
            Material[] materials = CubeDashPowerUps.MakePickupMaterials();
            Material edge = Material("Alloy Edge", new Color(0.16f, 0.3f, 0.39f), 0.45f);
            Material white = Material("Silver Emblem", new Color(0.88f, 0.96f, 1f), 0.55f);
            Build("ShieldPickup", PowerUpIconMeshes.Shield(), new[] { materials[0], edge, white });
            Build("DoublePointsPickup", PowerUpIconMeshes.Multiplier(), new[] { materials[1], edge, white });
            Build("Magnet", PowerUpIconMeshes.Magnet(), new[] { materials[4], edge, white });
            CubeDashPowerUps.UpdatePowerUpPrefab(materials);
            AssetDatabase.SaveAssets();

            string path = "Assets/Prefab/CubeDash/TrackSegment.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Reposition(root.GetComponent<TrackSegment>(), materials);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            var settings = new SerializedObject(game.Track);
            var list = settings.FindProperty("powerUpMaterials"); list.arraySize = materials.Length;
            for (int i = 0; i < materials.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            foreach (TrackSegment segment in game.Track.Segments) Reposition(segment, materials);
            // New defaults are serialized with the level; existing duration and speed settings remain untouched.
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("Power-ups saved between coin rows: 3D shield, 2x and horseshoe magnet. Magnet pulls only coins across lanes for 10 seconds; existing track objects and gameplay tuning preserved.");
        }

        private static void Reposition(TrackSegment segment, Material[] materials)
        {
            for (int row = 0; row < segment.PowerUps.Length; row++)
            {
                PowerUpPickup pickup = segment.PowerUps[row];
                Vector3 position = pickup.transform.localPosition;
                position.z = segment.Cubes[row * 3].transform.localPosition.z - RunnerRules.PowerUpRowOffset;
                pickup.transform.localPosition = position;
                pickup.Configure(pickup.Type, materials[(int)pickup.Type]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(pickup.transform);
                foreach (Transform child in pickup.transform)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                }
            }
        }

        private static void Build(string name, Mesh generated, Material[] materials)
        {
            string modelPath = "Assets/3D Models/CubeDash" + name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(modelPath);
            if (mesh == null) { mesh = generated; AssetDatabase.CreateAsset(mesh, modelPath); }
            else { EditorUtility.CopySerialized(generated, mesh); Object.DestroyImmediate(generated); EditorUtility.SetDirty(mesh); }
            string path = "Assets/Prefab/CubeDash/" + name + ".prefab";
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            try
            {
                root.GetComponent<MeshFilter>().sharedMesh = mesh;
                root.GetComponent<MeshRenderer>().sharedMaterials = materials;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        }

        private static Material Material(string name, Color color, float metallic)
        {
            string path = "Assets/Material/PowerUpModels/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", 0.4f);
            material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }
    }
}
