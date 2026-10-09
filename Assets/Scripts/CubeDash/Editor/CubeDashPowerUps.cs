using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>
    /// Authoring tool only. Adds Shield / Double Points / Fighter / Truck / Magnet pickups as real prefab
    /// and scene objects: a shared PowerUp prefab, three separate pickups inside each TrackSegment,
    /// pick-up materials on the track, and a "Power-Up Status" line in the HUD. Safe to re-run.
    /// </summary>
    public static class CubeDashPowerUps
    {
        private const string Prefabs = "Assets/Prefab/CubeDash/";
        private const string Materials = "Assets/Material/";
        private static readonly Color Shield = new Color(0.45f, 0.85f, 0.95f);
        private static readonly Color Points = new Color(1f, 0.78f, 0.3f);
        private static Mesh bevel;

        [MenuItem("Tools/Cube Dash/Add Power-Ups")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            if (game == null) throw new InvalidOperationException("The authored Cube Dash level is required.");
            bevel = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/CubeDashBeveledCube.asset");
            if (bevel == null) throw new InvalidOperationException("CubeDashBeveledCube mesh asset is required.");

            Material[] pickups = MakePickupMaterials();
            GameObject pickupPrefab = UpdatePowerUpPrefab(pickups);
            UpdateTrackSegment(pickupPrefab);

            SetArray(game.Track, "powerUpMaterials", pickups);
            SetFloat(game.Track, "powerUpChance", 0.18f);
            StylePowerUpHud(game);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Power-ups added: authored gem prefab, 3 pickups per track segment, " +
                "pick-up materials on the track, and a Power-Up Status line in the HUD.");
        }

        internal static Material[] MakePickupMaterials()
        {
            Color[] colors = { Shield, Points, new Color(0.65f, 0.55f, 1f), new Color(0.3f, 0.46f, 1f), new Color(0.96f, 0.18f, 0.13f) };
            string[] names = { "PickupShield", "PickupPoints", "PickupFlight", "PickupTruck", "PickupMagnet" };
            Material[] materials = new Material[5];
            for (int i = 0; i < materials.Length; i++)
            {
                string path = Materials + "CubeDash" + names[i] + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CubeDash" + names[i] };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.color = colors[i];
                material.enableInstancing = true;
                material.SetFloat("_Metallic", 0);
                material.SetFloat("_Smoothness", 0.35f);
                material.SetFloat("_SpecularHighlights", 0);
                material.SetFloat("_EnvironmentReflections", 0);
                material.SetColor("_EmissionColor", colors[i] * 0.3f);
                material.EnableKeyword("_EMISSION");
                EditorUtility.SetDirty(material);
                materials[i] = material;
            }
            return materials;
        }

        internal static GameObject UpdatePowerUpPrefab(Material[] pickups)
        {
            string path = Prefabs + "PowerUp.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                GameObject empty = new GameObject("Power Up");
                PrefabUtility.SaveAsPrefabAsset(empty, path);
                Object.DestroyImmediate(empty);
            }
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                contents.name = "Power Up";
                BoxCollider box = contents.GetComponent<BoxCollider>();
                if (box == null) box = contents.AddComponent<BoxCollider>();
                box.size = new Vector3(0.72f, 0.72f, 0.72f);
                box.center = Vector3.zero;

                Transform gem = contents.transform.Find("Gem");
                if (gem == null) gem = new GameObject("Gem").transform;
                gem.SetParent(contents.transform, false);
                gem.localPosition = Vector3.zero;
                gem.localScale = Vector3.one * 0.55f;
                gem.localRotation = Quaternion.Euler(0, 45, 45);
                MeshFilter meshFilter = gem.GetComponent<MeshFilter>();
                if (meshFilter == null) meshFilter = gem.gameObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = bevel != null ? bevel : AssetDatabase.LoadAssetAtPath<Mesh>("Assets/3D Models/CubeDashBeveledCube.asset");
                MeshRenderer renderer = gem.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = gem.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pickups[0];

                PowerUpPickup pickup = contents.GetComponent<PowerUpPickup>();
                if (pickup == null) pickup = contents.AddComponent<PowerUpPickup>();
                Set(pickup, "visual", gem);
                Set(pickup, "visualRenderer", renderer);
                AttachModel(contents, pickup, "ShieldPickup.prefab", "Shield Icon", "shieldVisual");
                AttachModel(contents, pickup, "DoublePointsPickup.prefab", "2x Icon", "pointsVisual");
                AttachModel(contents, pickup, "Magnet.prefab", "Magnet Icon", "magnetVisual");
                GameObject fighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "FighterPlane.prefab");
                if (fighterPrefab != null)
                {
                    Transform icon = contents.transform.Find("Fighter Icon");
                    if (icon == null)
                    {
                        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fighterPrefab, contents.scene);
                        instance.name = "Fighter Icon";
                        icon = instance.transform;
                        icon.SetParent(contents.transform, false);
                    }
                    icon.localScale = Vector3.one * 0.14f;
                    foreach (ParticleSystem system in icon.GetComponentsInChildren<ParticleSystem>(true)) system.gameObject.SetActive(false);
                    icon.gameObject.SetActive(false);
                    Set(pickup, "fighterVisual", icon);
                }
                GameObject truckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Truck.prefab");
                if (truckPrefab != null)
                {
                    Transform icon = contents.transform.Find("Truck Icon");
                    if (icon == null)
                    {
                        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(truckPrefab, contents.scene);
                        instance.name = "Truck Icon";
                        icon = instance.transform;
                        icon.SetParent(contents.transform, false);
                    }
                    icon.localScale = Vector3.one * 0.13f;
                    foreach (ParticleSystem system in icon.GetComponentsInChildren<ParticleSystem>(true)) system.gameObject.SetActive(false);
                    icon.gameObject.SetActive(false);
                    Set(pickup, "truckVisual", icon);
                }

                pickup.Configure(PowerUpType.Shield, pickups[0]);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void AttachModel(GameObject root, PowerUpPickup pickup, string prefabName, string name, string property)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefabName);
            if (prefab == null) return;
            Transform icon = root.transform.Find(name);
            if (icon == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.scene);
                icon = instance.transform; icon.SetParent(root.transform, false); icon.name = name;
            }
            icon.localPosition = Vector3.zero; icon.localScale = Vector3.one * 0.8f;
            icon.gameObject.SetActive(false);
            Set(pickup, property, icon);
        }

        private static void UpdateTrackSegment(GameObject pickupPrefab)
        {
            string path = Prefabs + "TrackSegment.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                TrackSegment segment = root.GetComponent<TrackSegment>();
                PowerUpPickup[] pickups = new PowerUpPickup[3];
                for (int row = 0; row < 3; row++)
                {
                    string name = "Power Up " + (row + 1);
                    Transform stale = root.transform.Find(name);
                    if (stale != null) Object.DestroyImmediate(stale.gameObject);
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(pickupPrefab, root.scene);
                    instance.name = name;
                    instance.transform.SetParent(root.transform, false);
                    instance.transform.localPosition = new Vector3(0, 1.35f, row * RunnerRules.RowSpacing);
                    instance.SetActive(false);
                    pickups[row] = instance.GetComponent<PowerUpPickup>();
                }
                SetArray(segment, "powerUps", pickups);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void StylePowerUpHud(CubeDashGame game)
        {
            RunnerHud hud = Object.FindAnyObjectByType<RunnerHud>();
            if (hud == null) throw new InvalidOperationException("The Cube Dash HUD is required.");
            Transform safe = hud.transform.Find("Safe Area");
            RectTransform status = safe.Find("Power-Up Status") as RectTransform;
            Text statusText = status != null ? status.GetComponent<Text>() : null;
            if (statusText == null)
            {
                if (status != null) Object.DestroyImmediate(status.gameObject);
                GameObject created = new GameObject("Power-Up Status", typeof(RectTransform));
                status = (RectTransform)created.transform;
                status.SetParent(safe, false);
                statusText = created.AddComponent<Text>();
                statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                statusText.alignment = TextAnchor.MiddleCenter;
                statusText.raycastTarget = false;
                statusText.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            Place(status, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(420, 28));
            statusText.fontSize = 16;
            statusText.fontStyle = FontStyle.Bold;
            statusText.color = new Color(1f, 0.8f, 0.35f);
            statusText.text = string.Empty;
            status.gameObject.SetActive(false);
            Set(hud, "powerUpStatus", statusText);
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Set(Object target, string name, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray<T>(Object target, string name, T[] values) where T : Object
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(name);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string name, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(name).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
