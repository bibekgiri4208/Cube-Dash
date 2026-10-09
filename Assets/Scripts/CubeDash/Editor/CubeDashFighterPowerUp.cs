using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Bakes the fighter, exhaust and shield into saved prefabs and connects the existing level.</summary>
    public static class CubeDashFighterPowerUp
    {
        private const string Prefabs = "Assets/Prefab/CubeDash/";
        private const string Materials = "Assets/Material/FighterPlane";
        private const string ModelPath = "Assets/3D Models/CubeDashFighter.asset";

        [MenuItem("Tools/Cube Dash/Add Fighter Plane Power-Up")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder("Assets/Material", "FighterPlane");
            Mesh generated = FighterPlaneMesh.Build();
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ModelPath);
            if (mesh == null) { mesh = generated; AssetDatabase.CreateAsset(mesh, ModelPath); }
            else { EditorUtility.CopySerialized(generated, mesh); Object.DestroyImmediate(generated); EditorUtility.SetDirty(mesh); }
            Material shell = Lit("Airframe", new Color(0.64f, 0.67f, 0.70f), 0.22f, 0.30f);
            Material canopy = Lit("Cockpit and Intakes", new Color(0.045f, 0.085f, 0.13f), 0.35f, 0.72f);
            Material engine = Lit("Engine Alloy", new Color(0.23f, 0.26f, 0.30f), 0.5f, 0.4f);
            Material fire = Lit("Afterburner Core", new Color(1, 0.35f, 0.075f), 0, 0.2f);
            fire.EnableKeyword("_EMISSION"); fire.SetColor("_EmissionColor", new Color(2.8f, 0.72f, 0.12f));
            Material smoke = Custom("Booster Smoke", "CubeDash/Jet Particle", new Color(0.33f, 0.36f, 0.40f, 0.65f));
            Material flame = Custom("Booster Flame", "CubeDash/Jet Particle", new Color(3.2f, 0.85f, 0.15f, 0.85f));
            Material shield = Custom("Landing Shield", "CubeDash/Flight Shield", new Color(0.25f, 1.1f, 1.5f, 0.4f));
            GameObject fighterPrefab = BuildFighter(mesh, new[] { shell, canopy, engine, fire }, smoke, flame);
            UpdatePlayer(fighterPrefab, shield);
            Material[] pickups = CubeDashPowerUps.MakePickupMaterials();
            CubeDashPowerUps.UpdatePowerUpPrefab(pickups);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            if (game == null) throw new InvalidOperationException("The authored Cube Dash level is required.");
            Set(game, "flightPresentation", game.Player.GetComponent<PlayerFlight>());
            SetFloat(game, "flightDuration", 10); SetFloat(game, "landingShieldDuration", 3); SetFloat(game, "flightAltitude", 3.2f);
            var track = new SerializedObject(game.Track);
            SerializedProperty materials = track.FindProperty("powerUpMaterials");
            materials.arraySize = pickups.Length;
            for (int i = 0; i < pickups.Length; i++) materials.GetArrayElementAtIndex(i).objectReferenceValue = pickups[i];
            track.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("Fighter power-up authored: faceted twin-engine model, saved boosters, 10-second flight and 3-second landing shield. Existing difficulty, scenery and pickup frequency preserved.");
        }

        private static GameObject BuildFighter(Mesh mesh, Material[] materials, Material smoke, Material flame)
        {
            string path = Prefabs + "FighterPlane.prefab";
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject("Fighter Plane");
            try
            {
                root.name = "Fighter Plane";
                MeshFilter filter = root.GetComponent<MeshFilter>();
                if (filter == null) filter = root.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer renderer = root.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                for (int side = -1; side <= 1; side += 2)
                {
                    string engine = side < 0 ? "Left" : "Right";
                    Exhaust(root.transform, engine + " Booster Smoke", side, smoke, false);
                    Exhaust(root.transform, engine + " Booster Flame", side, flame, true);
                }
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { if (exists) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        }

        private static void Exhaust(Transform parent, string name, int side, Material material, bool flame)
        {
            Transform child = parent.Find(name);
            if (child == null) { child = new GameObject(name, typeof(ParticleSystem)).transform; child.SetParent(parent, false); }
            child.localPosition = new Vector3(side * 0.4f, -0.025f, -2.46f);
            child.localRotation = Quaternion.Euler(0, 180, 0);
            ParticleSystem system = child.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed = false; system.randomSeed = (uint)(side < 0 ? 131 : 257) + (flame ? 11u : 0u);
            var main = system.main;
            main.loop = true; main.playOnAwake = false; main.useUnscaledTime = false;
            main.duration = 2; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = flame ? 32 : 96;
            main.startLifetime = new ParticleSystem.MinMaxCurve(flame ? 0.12f : 1.2f, flame ? 0.23f : 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(flame ? 2.5f : 3.5f, flame ? 3.5f : 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(flame ? 0.09f : 0.26f, flame ? 0.15f : 0.36f);
            main.startColor = Color.white;
            var emission = system.emission; emission.enabled = true; emission.rateOverTime = flame ? 40 : 44;
            var shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = flame ? 3 : 8; shape.radius = 0.055f;
            var size = system.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, flame ? 0.6f : 1f, 1, flame ? 0.1f : 3f));
            var color = system.colorOverLifetime; color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.9f, 0.12f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        private static void UpdatePlayer(GameObject fighterPrefab, Material shieldMaterial)
        {
            string path = Prefabs + "PlayerCube.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform fighter = root.transform.Find("Fighter Visual");
                if (fighter == null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fighterPrefab, root.scene);
                    instance.name = "Fighter Visual"; fighter = instance.transform; fighter.SetParent(root.transform, false);
                }
                fighter.localScale = Vector3.one * 0.62f;
                fighter.gameObject.SetActive(false);
                Transform shield = root.transform.Find("Landing Shield");
                if (shield == null)
                {
                    shield = new GameObject("Landing Shield", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                    shield.SetParent(root.transform, false);
                    GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    shield.GetComponent<MeshFilter>().sharedMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
                    Object.DestroyImmediate(sphere);
                }
                shield.localScale = Vector3.one * 1.65f;
                shield.GetComponent<MeshRenderer>().sharedMaterial = shieldMaterial;
                shield.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                shield.gameObject.SetActive(false);
                PlayerFlight presentation = root.GetComponent<PlayerFlight>();
                if (presentation == null) presentation = root.AddComponent<PlayerFlight>();
                Set(presentation, "fighter", fighter); Set(presentation, "shield", shield);
                var data = new SerializedObject(presentation);
                ParticleSystem[] systems = fighter.GetComponentsInChildren<ParticleSystem>(true);
                var list = data.FindProperty("exhaust"); list.arraySize = systems.Length;
                for (int i = 0; i < systems.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = systems[i];
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Material Lit(string name, Color color, float metallic, float smoothness)
        {
            Material material = GetMaterial(name, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
            return material;
        }
        private static Material Custom(string name, string shader, Color tint)
        {
            Material material = GetMaterial(name, shader); material.SetColor("_Tint", tint); return material;
        }
        private static Material GetMaterial(string name, string shader)
        {
            string path = Materials + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find(shader)) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }
        private static void Set(Object target, string name, Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(name).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetFloat(Object target, string name, float value)
        {
            var data = new SerializedObject(target); data.FindProperty(name).floatValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
