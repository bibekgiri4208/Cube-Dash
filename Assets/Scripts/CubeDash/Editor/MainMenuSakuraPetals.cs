using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CubeDash.Editor
{
    /// <summary>Authors two lightweight, editable petal emitters without changing the menu's trees or lighting.</summary>
    public static class MainMenuSakuraPetals
    {
        public const string RootName = "Floating Sakura Petals";
        public const string TexturePath = "Assets/2D Images/leaf.png";
        public const string MaterialPath = MainMenuSakuraBlossoms.ArtPath + "Menu Sakura Petals.mat";
        public const string ShaderPath = MainMenuSakuraBlossoms.ArtPath + "Menu Sakura Petals.shader";

        [MenuItem("Tools/Cube Dash/Add Floating Main Menu Sakura Petals")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before adding petals.");
            Scene scene = SceneManager.GetSceneByPath(MainMenuSceneBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainMenuSceneBuilder.ScenePath);
            ApplyToScene(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the menu petals.");
        }

        public static void ApplyToScene(Scene scene)
        {
            MainMenuController menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).SingleOrDefault();
            Transform garden = menu != null ? menu.transform.Find("Parking Lot Diorama/" + MainMenuJapaneseGarden.RootName) : null;
            if (garden == null) throw new InvalidOperationException("The existing Main Menu garden is required.");
            if (garden.Find(RootName) != null)
            {
                Debug.Log("Floating menu petals are already present. Existing effect edits were preserved."); return;
            }
            Camera camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single(item => item.CompareTag("MainCamera"));
            // Include manually placed prefab roots, not just trees under the generated garden.
            Transform[] trees = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>())
                .Where(tree => tree.gameObject.activeInHierarchy && PrefabUtility.IsAnyPrefabInstanceRoot(tree.gameObject)
                    && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tree.gameObject).StartsWith(MainMenuAssetStoreTrees.PrefabPath + "BlossomTree_", StringComparison.Ordinal))
                .OrderBy(tree => Vector3.Dot(tree.position - camera.transform.position, camera.transform.forward)).Take(2).ToArray();
            if (trees.Length != 2) throw new InvalidOperationException("Two foreground sakura prefab instances are required.");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (texture == null || shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("The petal texture and shader are required.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Menu Sakura Petals" };
                material.mainTexture = texture;
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            var effect = new GameObject(RootName).transform;
            effect.SetParent(garden, false);
            Undo.RegisterCreatedObjectUndo(effect.gameObject, "Add floating sakura petals");
            foreach (Transform tree in trees) CreateEmitter(effect, tree, material, texture.width / (float)texture.height, camera);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Added falling, wind-drifted sakura petals around the two foreground trees. Trees, lighting, UI and gameplay were preserved.");
        }

        private static void CreateEmitter(Transform parent, Transform tree, Material material, float aspect, Camera camera)
        {
            LODGroup lod = tree.GetComponent<LODGroup>();
            Renderer[] renderers = lod != null ? lod.GetLODs()[0].renderers : tree.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            bool left = camera.transform.InverseTransformPoint(tree.position).x < 0;
            var particles = new GameObject(left ? "Left Sakura - Falling Petals" : "Right Sakura - Falling Petals").AddComponent<ParticleSystem>();
            particles.transform.SetParent(parent, false);
            // Spread emission through the outer canopy rather than pouring from a single point above the trunk.
            particles.transform.position = new Vector3(bounds.center.x, Mathf.Lerp(bounds.min.y, bounds.max.y, 0.78f), bounds.center.z - bounds.extents.z * 0.35f);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.useAutoRandomSeed = false; particles.randomSeed = left ? 173u : 419u;
            var main = particles.main;
            main.duration = 22; main.loop = true; main.prewarm = true; main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.useUnscaledTime = true;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate; main.maxParticles = 180;
            main.startLifetime = new ParticleSystem.MinMaxCurve(14, 20);
            main.startSpeed = 0; main.gravityModifier = 0;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.12f * aspect, 0.23f * aspect);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.12f, 0.23f);
            main.startSizeZ = 1;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 0.39f, 0.62f), new Color(1, 0.73f, 0.82f));
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(-0.65f, 0.65f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);

            var emission = particles.emission; emission.rateOverTime = 6;
            var shape = particles.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(bounds.size.x * 0.72f, bounds.size.y * 0.16f, bounds.size.z * 0.55f);
            var velocity = particles.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = left ? new ParticleSystem.MinMaxCurve(0.16f, 0.48f) : new ParticleSystem.MinMaxCurve(-0.48f, -0.16f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.48f, -0.30f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.28f, -0.10f);
            var noise = particles.noise; noise.enabled = true; noise.separateAxes = true;
            noise.strengthX = 0.42f; noise.strengthY = 0.16f; noise.strengthZ = 0.32f;
            noise.frequency = 0.22f; noise.scrollSpeed = 0.12f; noise.damping = true; noise.quality = ParticleSystemNoiseQuality.Low;
            var rotation = particles.rotationOverLifetime; rotation.enabled = true; rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            rotation.y = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            var color = particles.colorOverLifetime; color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.9f, 0.08f), new GradientAlphaKey(0.85f, 0.72f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 0.8f, 1, 1));
            var rendererModule = particles.GetComponent<ParticleSystemRenderer>();
            rendererModule.sharedMaterial = material; rendererModule.renderMode = ParticleSystemRenderMode.Billboard;
            rendererModule.alignment = ParticleSystemRenderSpace.View; rendererModule.sortMode = ParticleSystemSortMode.Distance;
            rendererModule.shadowCastingMode = ShadowCastingMode.Off; rendererModule.receiveShadows = false;
            rendererModule.lightProbeUsage = LightProbeUsage.Off; rendererModule.reflectionProbeUsage = ReflectionProbeUsage.Off;
            rendererModule.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }
    }
}
