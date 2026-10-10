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
            GameObject prefab = BuildReferenceTruck();
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
            Debug.Log("Truck power-up saved: blue cab-over tractor, six rotating wheel assemblies, obstacle fragments, 10-second rampage and 3-second exit shield.");
        }

        [MenuItem("Tools/Cube Dash/Refine Truck Presentation")]
        public static void RefinePresentationFromCommandLine()
        {
            GameObject truck = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Truck.prefab");
            if (truck == null) throw new InvalidOperationException("Add the Truck power-up before refining its presentation.");
            GameObject prefab = BuildTruck(truck.GetComponent<MeshFilter>().sharedMesh,
                truck.transform.Find("Left Wheel 1").GetComponent<MeshFilter>().sharedMesh,
                truck.transform.Find("Left Wheel 2").GetComponent<MeshFilter>().sharedMesh,
                truck.GetComponent<MeshRenderer>().sharedMaterials,
                truck.transform.Find("Left Wheel 1").GetComponent<MeshRenderer>().sharedMaterials, SmokeMaterial());
            UpdatePlayer(prefab);
            DisablePickupSmoke();
            AssetDatabase.SaveAssets();
            Debug.Log("Truck presentation refined: smaller player model, sprung motion, front steering and twin stack smoke. Level and gameplay tuning preserved.");
        }

        [MenuItem("Tools/Cube Dash/Rebuild Reference Truck Model")]
        public static void RebuildReferenceModelFromCommandLine()
        {
            UpdatePlayer(BuildReferenceTruck());
            DisablePickupSmoke();
            AssetDatabase.SaveAssets();
            Debug.Log("Reference truck saved: boxy blue sleeper, navy band, squared hollow stacks, ladder frame, faceted tanks, fifth wheel, ten tires and curved rear mudguards. Existing gameplay and level preserved.");
        }

        [MenuItem("Tools/Cube Dash/Add Truck Tire Smoke")]
        public static void AddTireSmokeFromCommandLine()
        {
            string path = Prefabs + "Truck.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                throw new InvalidOperationException("Add the Truck power-up before adding tire smoke.");
            Material smoke = TireSmokeMaterial();
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                AddTireEmitters(root.transform, smoke);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            UpdatePlayer(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            DisablePickupSmoke();
            AssetDatabase.SaveAssets();
            Debug.Log("Truck tire smoke saved: six bounded road-contact emitters, stronger haze while steering, gameplay-only simulation and clean resets. Existing exhaust, models, level and gameplay preserved.");
        }

        private static GameObject BuildReferenceTruck()
        {
            if (!AssetDatabase.IsValidFolder(Materials)) AssetDatabase.CreateFolder("Assets/Material", "Truck");
            Mesh body = SaveMesh(TruckMesh.BuildBody(), "Assets/3D Models/CubeDashTruck.asset");
            Mesh wheel = SaveMesh(TruckMesh.BuildWheel(), "Assets/3D Models/CubeDashTruckWheel.asset");
            Mesh rearWheel = SaveMesh(TruckMesh.BuildWheel(true), "Assets/3D Models/CubeDashTruckRearWheel.asset");
            Material blue = Lit("Blue Cab", new Color(0.24f, 0.31f, 0.77f), 0.05f, 0.23f);
            Material navy = Lit("Navy Band", new Color(0.12f, 0.16f, 0.39f), 0.03f, 0.2f);
            Material frame = Lit("Chassis", new Color(0.065f, 0.078f, 0.088f), 0.12f, 0.22f);
            Material chrome = Lit("Chrome", new Color(0.59f, 0.66f, 0.69f), 0.3f, 0.32f);
            Material glass = Lit("Windows", new Color(0.17f, 0.22f, 0.23f), 0.25f, 0.65f);
            Material amber = Lit("Amber Lights", new Color(1, 0.55f, 0.06f), 0, 0.2f, 0.3f);
            Material white = Lit("Headlights", new Color(0.9f, 0.96f, 1), 0, 0.25f, 0.3f);
            Material red = Lit("Tail Lights", new Color(0.88f, 0.045f, 0.04f), 0, 0.2f, 0.3f);
            Material tire = Lit("Tires", new Color(0.055f, 0.058f, 0.063f), 0, 0.16f);
            Material tank = Lit("Tanks and Mudguards", new Color(0.29f, 0.32f, 0.35f), 0.15f, 0.26f);
            Material roof = Lit("Cab Roof", new Color(0.4f, 0.51f, 0.88f), 0.04f, 0.22f);
            return BuildTruck(body, wheel, rearWheel,
                new[] { blue, navy, frame, chrome, glass, amber, white, red, tank, roof }, new[] { tire, chrome, frame }, SmokeMaterial());
        }

        private static void DisablePickupSmoke()
        {
            // Only the driven truck emits smoke, not its miniature pickup icon.
            string path = Prefabs + "PowerUp.prefab";
            GameObject pickup = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform icon = pickup.transform.Find("Truck Icon");
                if (icon != null)
                    foreach (ParticleSystem system in icon.GetComponentsInChildren<ParticleSystem>(true))
                        system.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(pickup, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(pickup); }
        }

        private static GameObject BuildTruck(Mesh body, Mesh wheel, Mesh rearWheel, Material[] bodyMaterials, Material[] wheelMaterials, Material smoke)
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
                        child.localPosition = new Vector3(side * (axle == 0 ? 1.15f : 1.0f), 0.58f, axle == 0 ? 1.65f : axle == 1 ? -1.72f : -2.87f);
                        Render(child.gameObject, axle == 0 ? wheel : rearWheel, wheelMaterials);
                    }
                StackSmoke(root.transform, -1, smoke);
                StackSmoke(root.transform, 1, smoke);
                AddTireEmitters(root.transform, TireSmokeMaterial());
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
                visual.localScale = Vector3.one * 0.48f;
                visual.gameObject.SetActive(false);
                PlayerTruck presentation = root.GetComponent<PlayerTruck>();
                if (presentation == null) presentation = root.AddComponent<PlayerTruck>();
                var settings = new SerializedObject(presentation);
                settings.FindProperty("truck").objectReferenceValue = visual;
                var wheels = settings.FindProperty("wheels"); wheels.arraySize = 6;
                for (int i = 0; i < 6; i++) wheels.GetArrayElementAtIndex(i).objectReferenceValue =
                    visual.Find((i < 3 ? "Left" : "Right") + " Wheel " + (i % 3 + 1));
                var exhaust = settings.FindProperty("exhaust"); exhaust.arraySize = 2;
                exhaust.GetArrayElementAtIndex(0).objectReferenceValue = visual.Find("Left Stack Smoke").GetComponent<ParticleSystem>();
                exhaust.GetArrayElementAtIndex(1).objectReferenceValue = visual.Find("Right Stack Smoke").GetComponent<ParticleSystem>();
                var tires = settings.FindProperty("tireSmoke"); tires.arraySize = 6;
                for (int i = 0; i < 6; i++) tires.GetArrayElementAtIndex(i).objectReferenceValue =
                    visual.Find((i < 3 ? "Left" : "Right") + " Tire Smoke " + (i % 3 + 1)).GetComponent<ParticleSystem>();
                settings.FindProperty("contactHalfSize").vector2Value = new Vector2(0.74f, 1.62f);
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

        private static Material SmokeMaterial()
        {
            string path = Materials + "/Stack Smoke.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("CubeDash/Jet Particle")) { name = "Stack Smoke" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_Tint", new Color(0.36f, 0.38f, 0.42f, 0.72f));
            material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }

        private static Material TireSmokeMaterial()
        {
            string path = Materials + "/Tire Smoke.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("CubeDash/Jet Particle")) { name = "Tire Smoke" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_Tint", new Color(0.78f, 0.81f, 0.83f, 0.52f));
            material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }

        private static void AddTireEmitters(Transform parent, Material material)
        {
            foreach (int side in new[] { -1, 1 })
                for (int axle = 0; axle < 3; axle++)
                {
                    string name = (side < 0 ? "Left" : "Right") + " Tire Smoke " + (axle + 1);
                    Transform child = parent.Find(name);
                    if (child == null) { child = new GameObject(name, typeof(ParticleSystem)).transform; child.SetParent(parent, false); }
                    // Siblings of the wheels: emitters never rotate with the rolling tire mesh.
                    Transform wheel = parent.Find((side < 0 ? "Left" : "Right") + " Wheel " + (axle + 1));
                    child.localPosition = new Vector3(side * 1.2f, 0.1f, wheel.localPosition.z - 0.22f);
                    child.localRotation = Quaternion.Euler(-90, 0, 0);
                    ParticleSystem system = child.GetComponent<ParticleSystem>();
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    system.useAutoRandomSeed = false; system.randomSeed = (uint)(827 + axle * 137 + (side > 0 ? 593 : 0));
                    var main = system.main;
                    main.loop = true; main.playOnAwake = false; main.useUnscaledTime = false;
                    main.duration = 1; main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.maxParticles = 48;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
                    main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                    main.startColor = Color.white;
                    var emission = system.emission; emission.enabled = true; emission.rateOverTime = 0;
                    var shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 22; shape.radius = 0.09f;
                    var velocity = system.velocityOverLifetime; velocity.enabled = true;
                    velocity.space = ParticleSystemSimulationSpace.World; velocity.x = 0; velocity.y = 0.45f; velocity.z = -5.4f;
                    var noise = system.noise; noise.enabled = true; noise.strength = 0.06f;
                    noise.frequency = 1.2f; noise.scrollSpeed = 0.6f; noise.quality = ParticleSystemNoiseQuality.Low;
                    var size = system.sizeOverLifetime; size.enabled = true;
                    size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.7f, 1, 3.5f));
                    var color = system.colorOverLifetime; color.enabled = true;
                    Gradient fade = new Gradient();
                    fade.SetKeys(new[] { new GradientColorKey(new Color(0.8f, 0.83f, 0.86f), 0), new GradientColorKey(Color.white, 1) },
                        new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.65f, 0.12f),
                            new GradientAlphaKey(0.35f, 0.5f), new GradientAlphaKey(0, 1) });
                    color.color = fade;
                    ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
                    renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
        }

        private static void StackSmoke(Transform parent, int side, Material material)
        {
            string name = (side < 0 ? "Left" : "Right") + " Stack Smoke";
            Transform child = parent.Find(name);
            if (child == null) { child = new GameObject(name, typeof(ParticleSystem)).transform; child.SetParent(parent, false); }
            child.localPosition = new Vector3(side * 0.96f, 4.28f, 0.14f);
            child.localRotation = Quaternion.Euler(-90, 0, 0);
            ParticleSystem system = child.GetComponent<ParticleSystem>();
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed = false; system.randomSeed = side < 0 ? 347u : 593u;
            var main = system.main;
            main.loop = true; main.playOnAwake = false; main.useUnscaledTime = false;
            main.duration = 2; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.maxParticles = 80;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.26f, 0.38f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = Color.white;
            var emission = system.emission; emission.enabled = true; emission.rateOverTime = 24;
            var shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12; shape.radius = 0.065f;
            var velocity = system.velocityOverLifetime; velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World; velocity.x = 0; velocity.y = 0.35f; velocity.z = -3.8f;
            var noise = system.noise; noise.enabled = true; noise.strength = 0.18f;
            noise.frequency = 0.7f; noise.scrollSpeed = 0.45f; noise.quality = ParticleSystemNoiseQuality.Low;
            var size = system.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.65f, 1, 3.2f));
            var color = system.colorOverLifetime; color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(new Color(0.65f, 0.67f, 0.7f), 0),
                    new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.85f, 0.1f),
                    new GradientAlphaKey(0.45f, 0.55f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
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
