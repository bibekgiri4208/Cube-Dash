using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Bakes the truck and a bounded debris pool, preserving the existing level and power-ups.</summary>
    public static class CubeDashTruckPowerUp
    {
        private const string Prefabs = "Assets/Prefab/CubeDash/";
        private const string Materials = "Assets/Material/Truck";

        [MenuItem("Tools/Cube Dash/Add Truck Power-Up")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder("Assets/Material", "Truck");
            Mesh body = SaveMesh(TruckMesh.BuildBody(), "Assets/3D Models/CubeDashTruck.asset");
            Mesh wheel = SaveMesh(TruckMesh.BuildWheel(), "Assets/3D Models/CubeDashTruckWheel.asset");
            Material blue = Lit("Blue Cab", new Color(0.16f, 0.22f, 0.68f), 0.15f, 0.35f);
            Material navy = Lit("Navy Band", new Color(0.075f, 0.1f, 0.32f), 0.1f, 0.3f);
            Material frame = Lit("Chassis", new Color(0.07f, 0.085f, 0.095f), 0.15f, 0.25f);
            Material chrome = Lit("Chrome", new Color(0.57f, 0.66f, 0.72f), 0.55f, 0.5f);
            Material glass = Lit("Windows", new Color(0.07f, 0.14f, 0.18f), 0.4f, 0.85f);
            Material amber = Lit("Amber Lights", new Color(1, 0.49f, 0.035f), 0, 0.25f, 0.7f);
            Material white = Lit("Headlights", new Color(0.85f, 0.96f, 1), 0, 0.35f, 0.65f);
            Material red = Lit("Tail Lights", new Color(0.85f, 0.045f, 0.04f), 0, 0.25f, 0.5f);
            Material tire = Lit("Tires", new Color(0.045f, 0.048f, 0.055f), 0, 0.15f);
            GameObject prefab = BuildTruck(body, wheel, new[] { blue, navy, frame, chrome, glass, amber, white, red },
                new[] { tire, chrome, frame });
            UpdatePlayer(prefab);
            Material[] pickups = CubeDashPowerUps.MakePickupMaterials();
            CubeDashPowerUps.UpdatePowerUpPrefab(pickups);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            if (game == null) throw new InvalidOperationException("The authored Cube Dash level is required.");
            var settings = new SerializedObject(game);
            settings.FindProperty("truckPresentation").objectReferenceValue = game.Player.GetComponent<PlayerTruck>();
            settings.FindProperty("truckDuration").floatValue = 10;
            settings.FindProperty("truckShieldDuration").floatValue = 3;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var track = new SerializedObject(game.Track);
            var list = track.FindProperty("powerUpMaterials"); list.arraySize = pickups.Length;
            for (int i = 0; i < pickups.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = pickups[i];
            track.FindProperty("debris").objectReferenceValue = BuildDebris(game.Track.transform);
            track.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("Truck power-up saved: blue cab-over tractor, six rotating wheels, obstacle fragments, 10-second rampage and 3-second exit shield.");
        }

        private static GameObject BuildTruck(Mesh body, Mesh wheel, Material[] bodyMaterials, Material[] wheelMaterials)
        {
            string path = Prefabs + "Truck.prefab";
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject("Truck");
            try
            {
                Render(root, body, bodyMaterials);
                foreach (int side in new[] { -1, 1 })
                    for (int axle = 0; axle < 3; axle++)
                    {
                        string name = (side < 0 ? "Left" : "Right") + " Wheel " + (axle + 1);
                        Transform child = root.transform.Find(name);
                        if (child == null) { child = new GameObject(name).transform; child.SetParent(root.transform, false); }
                        child.localPosition = new Vector3(side * 1.15f, 0.58f, axle == 0 ? 1.65f : axle == 1 ? -1.72f : -2.87f);
                        Render(child.gameObject, wheel, wheelMaterials);
                    }
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        }

        private static void UpdatePlayer(GameObject prefab)
        {
            string path = Prefabs + "PlayerCube.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform visual = root.transform.Find("Truck Visual");
                if (visual == null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.scene);
                    instance.name = "Truck Visual"; visual = instance.transform; visual.SetParent(root.transform, false);
                }
                visual.localPosition = Vector3.down * 0.575f;
                visual.localScale = Vector3.one * 0.55f;
                visual.gameObject.SetActive(false);
                PlayerTruck presentation = root.GetComponent<PlayerTruck>();
                if (presentation == null) presentation = root.AddComponent<PlayerTruck>();
                var settings = new SerializedObject(presentation);
                settings.FindProperty("truck").objectReferenceValue = visual;
                var wheels = settings.FindProperty("wheels"); wheels.arraySize = 6;
                for (int i = 0; i < 6; i++) wheels.GetArrayElementAtIndex(i).objectReferenceValue = visual.GetChild(i);
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static ObstacleDebris BuildDebris(Transform track)
        {
            Transform root = track.Find("Obstacle Fragments");
            if (root == null)
                foreach (GameObject candidate in track.gameObject.scene.GetRootGameObjects())
                    if (candidate.name == "Obstacle Fragments") { root = candidate.transform; break; }
            if (root == null) root = new GameObject("Obstacle Fragments").transform;
            // Track's immediate children remain exactly the eight recyclable sections.
            root.SetParent(null, true);
            ObstacleDebris debris = root.GetComponent<ObstacleDebris>();
            if (debris == null) debris = root.gameObject.AddComponent<ObstacleDebris>();
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/CubeDashBeveledCube.asset");
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/CubeDashRed.mat");
            var settings = new SerializedObject(debris);
            var pieces = settings.FindProperty("pieces"); pieces.arraySize = 128;
            var renderers = settings.FindProperty("renderers"); renderers.arraySize = 128;
            for (int i = 0; i < 128; i++)
            {
                Transform piece = root.Find("Fragment " + i.ToString("000"));
                if (piece == null) { piece = new GameObject("Fragment " + i.ToString("000")).transform; piece.SetParent(root, false); }
                Render(piece.gameObject, mesh, new[] { material });
                MeshRenderer renderer = piece.GetComponent<MeshRenderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off;
                piece.gameObject.SetActive(false);
                pieces.GetArrayElementAtIndex(i).objectReferenceValue = piece;
                renderers.GetArrayElementAtIndex(i).objectReferenceValue = renderer;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            return debris;
        }

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
            if (emission > 0) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * emission); }
            material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }
    }
}
