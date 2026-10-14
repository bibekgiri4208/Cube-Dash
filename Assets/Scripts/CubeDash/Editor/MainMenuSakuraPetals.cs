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
        public const string GroundName = "Pavement Landing Plane";
        public const string SettledName = "Settled Sakura Petals";
        public const string RestingName = "Resting Sakura Petals";
        public const string RestingMeshPath = MainMenuSakuraBlossoms.ArtPath + "Menu Resting Petals.asset";

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
            Transform existing = garden.Find(RootName);
            if (existing != null)
            {
                AddGroundLanding(existing);
                Debug.Log("Menu petals are present. Added missing ground landing while preserving existing effect edits."); return;
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
            AddGroundLanding(effect);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Added falling, wind-drifted sakura petals around the two foreground trees. Trees, lighting, UI and gameplay were preserved.");
        }

        private static void AddGroundLanding(Transform effect)
        {
            AddRestingPetals(effect);
            // Only the two canopy systems: settled sub-emitters must not themselves emit more petals.
            ParticleSystem[] falling = effect.Cast<Transform>().Select(child => child.GetComponent<ParticleSystem>()).Where(item => item != null).ToArray();
            foreach (ParticleSystem emitter in falling) RepairTransparentSettledPetals(emitter);
            if (falling.All(item => item.transform.Find(SettledName) != null)) return;
            Transform plane = effect.Find(GroundName);
            if (plane == null)
            {
                plane = new GameObject(GroundName).transform;
                plane.SetParent(effect, false);
                plane.SetPositionAndRotation(new Vector3(0, 0.025f, 0), Quaternion.identity);
                Undo.RegisterCreatedObjectUndo(plane.gameObject, "Add sakura pavement landing plane");
            }
            foreach (ParticleSystem emitter in falling)
            {
                if (emitter.transform.Find(SettledName) != null) continue;
                Undo.RecordObject(emitter, "Let sakura petals reach the pavement");
                var main = emitter.main;
                bool playOnAwake = main.playOnAwake;
                // Keep shorter-lived floating petals, but give a portion enough time to reach the pavement.
                main.startLifetime = new ParticleSystem.MinMaxCurve(main.startLifetime.constantMin, Mathf.Max(30, main.startLifetime.constantMax));
                main.duration = Mathf.Max(32, main.duration);
                var collision = emitter.collision;
                collision.enabled = true; collision.type = ParticleSystemCollisionType.Planes;
                collision.mode = ParticleSystemCollisionMode.Collision3D; collision.SetPlane(0, plane);
                collision.dampen = 1; collision.bounce = 0; collision.lifetimeLoss = 1;
                collision.radiusScale = 0; collision.sendCollisionMessages = false;
                ParticleSystem settled = CreateSettledEmitter(emitter);
                var children = emitter.subEmitters; children.enabled = true;
                children.AddSubEmitter(settled, ParticleSystemSubEmitterType.Collision,
                    ParticleSystemSubEmitterProperties.InheritSize);
                // Unity shares this flag across a particle hierarchy; sub-emitters are event-driven regardless.
                main.playOnAwake = playOnAwake;
            }
            EditorSceneManager.MarkSceneDirty(effect.gameObject.scene);
        }

        private static void AddRestingPetals(Transform effect)
        {
            if (effect.Find(RestingName) != null) return;
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RestingMeshPath);
            if (mesh == null)
            {
                // A small permanent scatter keeps the pavement visibly petal-strewn even before the next landing.
                const int count = 96;
                var vertices = new Vector3[count * 4]; var uv = new Vector2[count * 4];
                var colors = new Color[count * 4]; var triangles = new int[count * 6];
                var random = new System.Random(7289);
                float Range(float low, float high) => Mathf.Lerp(low, high, (float)random.NextDouble());
                for (int i = 0; i < count; i++)
                {
                    bool left = i < count / 2;
                    Vector3 center = new Vector3((left ? -2.7f : 6.8f) + Range(-2.2f, 2.2f), 0.035f, Range(-3.8f, 4.5f));
                    if (center.x > 1.7f && center.x < 4.7f && center.z > -2.5f && center.z < 1) center.x += 2;
                    float angle = Range(0, Mathf.PI * 2), length = Range(0.17f, 0.29f);
                    Vector3 across = new Vector3(Mathf.Cos(angle), 0, -Mathf.Sin(angle)) * (length * 0.32f);
                    Vector3 along = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * (length * 0.5f);
                    int v = i * 4, t = i * 6;
                    vertices[v] = center - across - along; vertices[v + 1] = center + across - along;
                    vertices[v + 2] = center + across + along; vertices[v + 3] = center - across + along;
                    uv[v] = Vector2.zero; uv[v + 1] = Vector2.right; uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
                    Color tint = Color.Lerp(new Color(1, 0.4f, 0.62f), new Color(1, 0.72f, 0.82f), Range(0, 1));
                    for (int corner = 0; corner < 4; corner++) colors[v + corner] = tint;
                    triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                    triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
                }
                mesh = new Mesh { name = "Menu Resting Sakura Petals", vertices = vertices, uv = uv, colors = colors, triangles = triangles };
                mesh.RecalculateBounds(); mesh.RecalculateNormals();
                AssetDatabase.CreateAsset(mesh, RestingMeshPath);
            }
            Transform resting = new GameObject(RestingName).transform;
            resting.SetParent(effect, false); resting.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(resting.gameObject, "Add visible resting sakura petals");
            resting.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = resting.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            EditorSceneManager.MarkSceneDirty(effect.gameObject.scene);
        }

        private static void RepairTransparentSettledPetals(ParticleSystem falling)
        {
            var children = falling.subEmitters;
            for (int i = 0; i < children.subEmittersCount; i++)
            {
                ParticleSystem settled = children.GetSubEmitterSystem(i);
                if (settled == null || settled.name != SettledName) continue;
                if (settled.transform.position != Vector3.zero)
                {
                    Undo.RecordObject(settled.transform, "Anchor settled petals to the ground");
                    settled.transform.position = Vector3.zero;
                    EditorSceneManager.MarkSceneDirty(falling.gameObject.scene);
                }
                var renderer = settled.GetComponent<ParticleSystemRenderer>();
                if (renderer.renderMode == ParticleSystemRenderMode.HorizontalBillboard)
                {
                    Undo.RecordObject(settled, "Keep grounded petals flat in world space");
                    Undo.RecordObject(renderer, "Render grounded sakura petal meshes");
                    UseFlatGroundMesh(settled);
                    EditorSceneManager.MarkSceneDirty(falling.gameObject.scene);
                }
                var main = settled.main;
                // Upgrade only the original small, short-lived preset; retain custom authored ground settings.
                if (main.startSizeX.constant == 1 && main.startSizeY.constant == 1
                    && main.startLifetime.constantMin == 8 && main.startLifetime.constantMax == 14)
                {
                    Undo.RecordObject(settled, "Make grounded sakura petals easier to see");
                    main.duration = 18; main.startLifetime = new ParticleSystem.MinMaxCurve(12, 18); main.maxParticles = 96;
                    main.startSizeX = 1.4f; main.startSizeY = 1.4f;
                    EditorSceneManager.MarkSceneDirty(falling.gameObject.scene);
                }
                var properties = children.GetSubEmitterProperties(i);
                if ((properties & ParticleSystemSubEmitterProperties.InheritColor) == 0) continue;
                Undo.RecordObject(falling, "Fix invisible settled sakura petals");
                Undo.RecordObject(settled, "Restore settled sakura petal colors");
                children.SetSubEmitterProperties(i, properties & ~ParticleSystemSubEmitterProperties.InheritColor);
                main.startColor = falling.main.startColor;
                EditorSceneManager.MarkSceneDirty(falling.gameObject.scene);
            }
        }

        private static ParticleSystem CreateSettledEmitter(ParticleSystem falling)
        {
            var settled = new GameObject(SettledName).AddComponent<ParticleSystem>();
            settled.transform.SetParent(falling.transform, false);
            settled.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(settled.gameObject, "Add settled sakura petals");
            settled.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            settled.useAutoRandomSeed = false; settled.randomSeed = falling.randomSeed + 1;
            var main = settled.main;
            main.duration = 18; main.loop = false; main.prewarm = false; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.useUnscaledTime = true;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate; main.maxParticles = 96;
            main.startLifetime = new ParticleSystem.MinMaxCurve(12, 18); main.startSpeed = 0; main.gravityModifier = 0;
            // Collision kills the falling particle, whose lifetime fade is then zero. Never inherit that alpha.
            main.startColor = falling.main.startColor; main.startSize3D = true;
            main.startSizeX = 1.4f; main.startSizeY = 1.4f; main.startSizeZ = 1;
            var shape = settled.shape; shape.enabled = false;
            var emission = settled.emission; emission.enabled = true; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
            var color = settled.colorOverLifetime; color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.65f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            var renderer = settled.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = falling.GetComponent<ParticleSystemRenderer>().sharedMaterial;
            UseFlatGroundMesh(settled);
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return settled;
        }

        private static void UseFlatGroundMesh(ParticleSystem settled)
        {
            var main = settled.main;
            main.startRotation3D = true; main.startRotationX = Mathf.PI * 0.5f;
            main.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); main.startRotationZ = 0;
            var renderer = settled.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            renderer.enableGPUInstancing = false;
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
