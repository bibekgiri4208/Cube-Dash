using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Foliage-only upgrade. Leaves the authored menu, camera, vehicles, skybox and lighting intact.</summary>
    public static class MainMenuJapaneseGarden
    {
        public const string ArtPath = "Assets/MainMenu/JapaneseGarden/";
        public const string RootName = "Japanese Garden Foliage";

        [MenuItem("Tools/Cube Dash/Improve Main Menu Japanese Foliage")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before updating foliage.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            Scene scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the menu foliage.");
        }

        public static void ApplyAndCaptureFromCommandLine()
        {
            ApplyFromCommandLine();
            MainMenuSceneBuilder.CaptureFromCommandLine();
        }

        public static void ApplyToScene(Scene scene)
        {
            MainMenuController menu = null;
            foreach (GameObject candidate in scene.GetRootGameObjects())
                if (candidate.GetComponent<MainMenuController>() != null) menu = candidate.GetComponent<MainMenuController>();
            if (menu == null) throw new InvalidOperationException("The Main Menu controller is required.");
            Transform world = menu.transform.Find("Parking Lot Diorama");
            if (world == null) throw new InvalidOperationException("The authored Parking Lot Diorama was not found.");
            // Never wipe an existing upgraded garden: its transforms/materials may contain user edits.
            if (world.Find(RootName) != null)
            {
                Debug.Log("Japanese foliage is already authored. Existing garden edits were preserved.");
                return;
            }
            PrepareAssets();
            var originalPlants = new List<Transform>();
            foreach (Transform child in world.GetComponentsInChildren<Transform>(true))
                if (child.name == "Layered low-poly evergreen" || child.name == "Faceted leafy shrub" || child.name == "Asphalt crack weed")
                    originalPlants.Add(child);
            Transform garden = new GameObject(RootName).transform;
            garden.SetParent(world, false); Undo.RegisterCreatedObjectUndo(garden.gameObject, "Improve Japanese foliage");
            Transform pines = Group("Layered Japanese black pines", garden);
            Transform shrubs = Group("Rounded azalea shrubs", garden);
            Transform grasses = Group("Susuki grass and fine roadside tufts", garden);
            int shrubNumber = 0;
            Mesh pine = AssetDatabase.LoadAssetAtPath<Mesh>(ArtPath + "Meshes/Japanese Pine.asset");
            foreach (Transform original in originalPlants)
            {
                if (!original.gameObject.activeSelf) continue;
                Vector3 position = world.InverseTransformPoint(original.position);
                Quaternion rotation = Quaternion.Inverse(world.rotation) * original.rotation;
                if (original.name == "Layered low-poly evergreen")
                {
                    Transform trunk = original.Find("Pine trunk");
                    float height = trunk != null ? trunk.localScale.y / 0.64f : 9;
                    Place("Japanese Pine", pines, position, Vector3.one * (height * 0.86f / pine.bounds.size.y), rotation);
                }
                else if (original.name == "Faceted leafy shrub")
                {
                    Transform clump = original.Find("Angular foliage clump");
                    float size = clump != null ? clump.localScale.x / 0.75f : 0.75f;
                    Place("Azalea Shrub", shrubs, position, Vector3.one * size, rotation * Quaternion.Euler(0, shrubNumber * 137.5f, 0));
                    if (size < 1)
                    {
                        Place("Susuki Grass", grasses, position + new Vector3(0.55f, -0.04f, 0.22f),
                            Vector3.one * (0.35f + shrubNumber % 3 * 0.06f), Quaternion.Euler(0, shrubNumber * 73, 0));
                        shrubNumber++;
                    }
                }
                else
                {
                    Place("Roadside Grass", grasses, position, Vector3.one * 0.23f, rotation);
                }
                // Preserve the original models and all their serialized edits as inactive backups.
                Undo.RecordObject(original.gameObject, "Hide original foliage"); original.gameObject.SetActive(false);
            }
            Transform sakura = Group("Sakura trees and fallen petals", garden);
            Sakura(sakura, new Vector3(10.8f, 0.03f, 6.8f), 1, 17, true);
            Sakura(sakura, new Vector3(-5.6f, 0.03f, 13), 1.10f, 115, false);
            Sakura(sakura, new Vector3(5.6f, 0.03f, 20), 1.02f, 245, false);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Japanese menu foliage authored: branched sakura with five-petal flowers, layered black pines, leafy azaleas and susuki grass. Original foliage retained inactive; layout, vehicles, UI, player, camera and lighting preserved.");
        }

        private static Transform Group(string name, Transform parent)
        {
            Transform group = new GameObject(name).transform; group.SetParent(parent, false); return group;
        }

        private static Transform Place(string name, Transform parent, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtPath + "Prefabs/" + name + ".prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            instance.transform.SetParent(parent, false); instance.transform.localPosition = position;
            instance.transform.localScale = scale; instance.transform.localRotation = rotation; return instance.transform;
        }

        private static void Sakura(Transform parent, Vector3 position, float scale, float rotation, bool planter)
        {
            Transform tree = Place("Sakura Tree", parent, position, Vector3.one * scale, Quaternion.Euler(0, rotation, 0));
            if (planter)
            {
                Material stone = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "Materials/Weathered stone.mat");
                Material soil = AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "Materials/Planting soil.mat");
                Box("Sakura planting soil", tree, new Vector3(0, 0.05f, 0), new Vector3(1.55f, 0.08f, 1.55f), soil);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Low stone planter rim", tree, new Vector3(side * 0.84f, 0.10f, 0), new Vector3(0.13f, 0.20f, 1.8f), stone);
                    Box("Low stone planter rim", tree, new Vector3(0, 0.10f, side * 0.84f), new Vector3(1.8f, 0.20f, 0.13f), stone);
                }
                for (int i = 0; i < 5; i++)
                    Place("Roadside Grass", tree, Direction(i * 137.5f) * 0.57f + Vector3.up * 0.09f, Vector3.one * 0.5f, Quaternion.Euler(0, i * 80, 0));
            }
            Place("Fallen Sakura Petals", parent, position + Vector3.up * (planter ? 0.01f : 0), Vector3.one * scale, Quaternion.Euler(0, rotation, 0));
        }

        private static Vector3 Direction(float degrees) => new Vector3(Mathf.Cos(degrees * Mathf.Deg2Rad), 0, Mathf.Sin(degrees * Mathf.Deg2Rad));
        private static void Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            box.transform.SetParent(parent, false); box.transform.localPosition = position; box.transform.localScale = scale;
            box.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void PrepareAssets()
        {
            Folder(ArtPath.TrimEnd('/')); Folder(ArtPath + "Materials"); Folder(ArtPath + "Meshes"); Folder(ArtPath + "Prefabs");
            Material bark = Material("Sakura bark", "70534E"), markings = Material("Bark lenticels", "B39385");
            Material pink = Material("Sakura blush", "F39ABB"), pale = Material("Sakura pale petals", "FFD7E3"), buds = Material("Sakura rose buds", "D86C98");
            Material centers = Material("Sakura golden stamens", "DBA755"), young = Material("Sakura young leaves", "7B9952");
            Prefab("Sakura Tree", JapaneseGardenMeshes.Sakura, new[] { bark, markings, young, pink, pale, buds, centers });
            Prefab("Japanese Pine", JapaneseGardenMeshes.Pine, new[] { Material("Pine bark", "63564A"), Material("Pine needle shade", "174C38"),
                Material("Pine green needles", "28714C"), Material("Pine fresh tips", "559567") });
            Prefab("Azalea Shrub", JapaneseGardenMeshes.Shrub, new[] { bark, Material("Azalea leaf shade", "245338"), Material("Azalea leaves", "3D7752"),
                Material("Azalea young growth", "7AA35D"), Material("Azalea flowers", "DF7DA0"), centers });
            Material[] grass = { Material("Grass blade shade", "397344"), Material("Grass fresh blades", "70A464"),
                Material("Grass warm tips", "9FB06D"), Material("Susuki silver seed heads", "E1D0AF") };
            Prefab("Susuki Grass", () => JapaneseGardenMeshes.Grass(true), grass);
            Prefab("Roadside Grass", () => JapaneseGardenMeshes.Grass(false), grass);
            Prefab("Fallen Sakura Petals", JapaneseGardenMeshes.FallenPetals, new[] { pink, pale });
            Material("Weathered stone", "888C83"); Material("Planting soil", "514A3B");
            AssetDatabase.SaveAssets();
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/'); Folder(path.Substring(0, split)); AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }

        private static Material Material(string name, string hex)
        {
            string path = ArtPath + "Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            ColorUtility.TryParseHtmlString("#" + hex, out Color color); material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.18f); material.SetFloat("_Metallic", 0);
            material.SetFloat("_SpecularHighlights", 0); material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0); material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.SetFloat("_Cull", (float)CullMode.Off); material.SetColor("_EmissionColor", Color.black);
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private static void Prefab(string name, Func<Mesh> createMesh, Material[] materials)
        {
            string path = ArtPath + "Prefabs/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            string meshPath = ArtPath + "Meshes/" + name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null) { mesh = createMesh(); AssetDatabase.CreateAsset(mesh, meshPath); }
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                SceneManager.MoveGameObjectToScene(root, preview);
                root.GetComponent<MeshFilter>().sharedMesh = mesh; root.GetComponent<MeshRenderer>().sharedMaterials = materials;
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidOperationException("Could not save botanical prefab: " + name);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
    }
}
