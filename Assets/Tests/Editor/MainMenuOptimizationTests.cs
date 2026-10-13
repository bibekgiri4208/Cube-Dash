using System.Collections;
using System.Linq;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class MainMenuOptimizationTests
    {
        private const string MenuScene = "Assets/Scenes/Main Menu.unity";

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void BatchesRigidSceneryWithoutChangingContentAndIsIdempotent()
        {
            Scene scene = OpenUnoptimizedPreview();
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                Transform[] objects = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                Vector3[] positions = objects.Select(item => item.localPosition).ToArray();
                Quaternion[] rotations = objects.Select(item => item.localRotation).ToArray();
                Vector3[] scales = objects.Select(item => item.localScale).ToArray();
                bool[] active = objects.Select(item => item.gameObject.activeSelf).ToArray();
                var renderers = roots.SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
                string[] appearance = renderers.Select(renderer => EditorJsonUtility.ToJson(renderer)).ToArray();
                var lights = roots.SelectMany(root => root.GetComponentsInChildren<Light>(true)).ToArray();
                string[] lighting = lights.Select(light => EditorJsonUtility.ToJson(light)).ToArray();
                var volume = roots.SelectMany(root => root.GetComponentsInChildren<Volume>(true)).Single();
                string profile = EditorJsonUtility.ToJson(volume.sharedProfile);
                int changed = MainMenuOptimization.ApplyToScene(scene);
                Assert.That(changed, Is.GreaterThan(300), "The stationary diorama should actually be optimized.");
                Assert.That(MainMenuOptimization.ApplyToScene(scene), Is.Zero);
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Count(), Is.EqualTo(objects.Length));
                for (int i = 0; i < objects.Length; i++)
                {
                    Assert.That(objects[i].localPosition, Is.EqualTo(positions[i]));
                    Assert.That(objects[i].localRotation, Is.EqualTo(rotations[i]));
                    Assert.That(objects[i].localScale, Is.EqualTo(scales[i]));
                    Assert.That(objects[i].gameObject.activeSelf, Is.EqualTo(active[i]));
                }
                for (int i = 0; i < renderers.Length; i++) Assert.That(EditorJsonUtility.ToJson(renderers[i]), Is.EqualTo(appearance[i]));
                for (int i = 0; i < lights.Length; i++) Assert.That(EditorJsonUtility.ToJson(lights[i]), Is.EqualTo(lighting[i]));
                Assert.That(EditorJsonUtility.ToJson(volume.sharedProfile), Is.EqualTo(profile));
                var menu = roots.SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                foreach (Renderer renderer in menu.Hero.GetComponentsInChildren<Renderer>(true))
                    Assert.That((int)(GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic), Is.Zero);
                foreach (LODGroup group in roots.SelectMany(root => root.GetComponentsInChildren<LODGroup>(true)))
                    foreach (Renderer renderer in group.GetComponentsInChildren<Renderer>(true))
                        Assert.That((int)(GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic), Is.Zero);
                foreach (MeshRenderer renderer in renderers.Where(item => item.sharedMaterials.Any(material =>
                    material != null && AssetDatabase.GetAssetPath(material).StartsWith("Assets/Idyllic Fantasy Nature/"))))
                    Assert.That((int)(GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic), Is.Zero);
                Debug.Log($"Menu optimization validated: {changed} new stationary renderer batching flags.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void SkipsAnimatedAncestorsPropertyBlocksAndPreservesExistingStaticFlags()
        {
            Scene scene = OpenUnoptimizedPreview();
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MainMenuController>(true)).Single();
                Transform world = menu.transform.Find("Parking Lot Diorama");
                MeshRenderer candidate = world.GetComponentsInChildren<MeshRenderer>().First(renderer => renderer.enabled
                    && renderer.sharedMaterials.All(material => material.shader.name == "Universal Render Pipeline/Lit")
                    && renderer.GetComponentInParent<LODGroup>() == null && renderer.GetComponentInParent<PlayerCubeVisual>() == null);
                GameObject animated = new GameObject("Animated parent", typeof(Animator));
                SceneManager.MoveGameObjectToScene(animated, scene); animated.transform.SetParent(world, false);
                GameObject child = Object.Instantiate(candidate.gameObject, animated.transform);
                var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", Color.blue);
                candidate.SetPropertyBlock(block);
                GameObjectUtility.SetStaticEditorFlags(candidate.gameObject, StaticEditorFlags.OccluderStatic);
                MainMenuOptimization.ApplyToScene(scene);
                Assert.That(GameObjectUtility.GetStaticEditorFlags(candidate.gameObject), Is.EqualTo(StaticEditorFlags.OccluderStatic));
                Assert.That(GameObjectUtility.GetStaticEditorFlags(child), Is.EqualTo((StaticEditorFlags)0));
                candidate.SetPropertyBlock(null);
                MainMenuOptimization.ApplyToScene(scene);
                Assert.That(GameObjectUtility.GetStaticEditorFlags(candidate.gameObject),
                    Is.EqualTo(StaticEditorFlags.OccluderStatic | StaticEditorFlags.BatchingStatic));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void DoesNotOptimizeGameplayOrChangeItsCamera()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                GameObject[] objects = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(item => item.gameObject).ToArray();
                var flags = objects.Select(GameObjectUtility.GetStaticEditorFlags).ToArray();
                var camera = objects.Select(item => item.GetComponent<UniversalAdditionalCameraData>()).First(item => item != null);
                string before = EditorJsonUtility.ToJson(camera);
                Assert.That(MainMenuOptimization.ApplyToScene(scene), Is.Zero);
                CollectionAssert.AreEqual(flags, objects.Select(GameObjectUtility.GetStaticEditorFlags).ToArray());
                Assert.That(EditorJsonUtility.ToJson(camera), Is.EqualTo(before));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void EditorApplicationPreservesCameraSettingsAndTransientOptimizationRespectsExplicitOverrides()
        {
            Scene scene = OpenUnoptimizedPreview();
            try
            {
                var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
                var data = camera.GetUniversalAdditionalCameraData();
                string before = EditorJsonUtility.ToJson(data);
                Assert.That(MainMenuOptimization.ApplyToScene(scene, true), Is.GreaterThan(300));
                Assert.That(EditorJsonUtility.ToJson(data), Is.EqualTo(before), "Saving batching flags must not permanently disable the opaque buffer.");
                data.requiresColorOption = CameraOverrideOption.On;
                MainMenuOptimization.ApplyToScene(scene);
                Assert.That(data.requiresColorOption, Is.EqualTo(CameraOverrideOption.On));
                data.requiresColorOption = CameraOverrideOption.Off;
                MainMenuOptimization.ApplyToScene(scene);
                Assert.That(data.requiresColorOption, Is.EqualTo(CameraOverrideOption.Off));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void KeepsOpaqueCopyForSceneColorShadersAndAdditionalCameras()
        {
            Scene scene = OpenUnoptimizedPreview();
            Material water = null;
            try
            {
                var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
                var data = camera.GetUniversalAdditionalCameraData();
                data.requiresColorOption = CameraOverrideOption.UsePipelineSettings;
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Idyllic Fantasy Nature/Shader/Water.shadergraph");
                Assert.That(shader, Is.Not.Null);
                water = new Material(shader);
                var mesh = new GameObject("Scene-color dependent water", typeof(MeshRenderer), typeof(MeshFilter));
                SceneManager.MoveGameObjectToScene(mesh, scene);
                mesh.GetComponent<MeshRenderer>().sharedMaterial = water;
                MainMenuOptimization.ApplyToScene(scene);
                Assert.That(data.requiresColorOption, Is.EqualTo(CameraOverrideOption.UsePipelineSettings));
                Object.DestroyImmediate(mesh);
                var second = new GameObject("Unrecognized extra camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(second, scene);
                MainMenuOptimization.ApplyToScene(scene);
                Assert.That(data.requiresColorOption, Is.EqualTo(CameraOverrideOption.UsePipelineSettings));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (water != null) Object.DestroyImmediate(water);
            }
        }

        [UnityTest]
        public IEnumerator ButtonSettlesWithoutTransformWritesAndStillRespondsToInputAndInteractability()
        {
            yield return new EnterPlayMode();
            var go = new GameObject("Button feedback test", typeof(RectTransform), typeof(Button), typeof(ArcadeButton));
            try
            {
                var feedback = go.GetComponent<ArcadeButton>();
                var button = go.GetComponent<Button>();
                feedback.OnSelect(null);
                yield return WaitForScale(go.transform, 1.05f);
                for (int i = 0; i < 5; i++)
                {
                    go.transform.hasChanged = false;
                    yield return null;
                    Assert.That(go.transform.hasChanged, Is.False, "Idle feedback must not dirty the UI transform.");
                }
                button.interactable = false;
                yield return WaitForScale(go.transform, 1);
                button.interactable = true;
                yield return WaitForScale(go.transform, 1.05f);
                feedback.OnDeselect(null); feedback.OnPointerEnter(null);
                yield return WaitForScale(go.transform, 1.035f);
                feedback.OnPointerDown(new PointerEventData(null) { button = PointerEventData.InputButton.Left });
                yield return WaitForScale(go.transform, 0.95f);
                feedback.OnPointerUp(null);
                yield return WaitForScale(go.transform, 1.035f);
                feedback.OnPointerExit(null);
                yield return WaitForScale(go.transform, 1);
                go.SetActive(false);
                Assert.That(go.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally { Object.Destroy(go); }
        }

        private static IEnumerator WaitForScale(Transform transform, float scale)
        {
            Vector3 expected = Vector3.one * scale;
            float deadline = Time.realtimeSinceStartup + 3;
            while (!transform.localScale.Equals(expected) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(transform.localScale, Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator StaticBatchingAndOpaqueCopyRemovalPreserveRenderedWorld()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("GPU rendering required.");
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MenuScene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Camera camera = Camera.main;
            camera.GetComponent<MainMenuCameraSway>().enabled = false;
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            menu.Hero.GetComponent<PlayerCubeVisual>().enabled = false;
            var canvas = menu.GetComponentInChildren<Canvas>();
            var data = camera.GetUniversalAdditionalCameraData();
            Random.State oldRandom = Random.state;
            bool oldCanvas = canvas.enabled, oldCompilation = ShaderUtil.allowAsyncCompilation;
            var oldColor = data.requiresColorOption;
            RenderTexture oldTarget = camera.targetTexture, oldActive = RenderTexture.active;
            var target = new RenderTexture(960, 540, 24);
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                canvas.enabled = false; ShaderUtil.allowAsyncCompilation = false; camera.targetTexture = target;
                camera.Render(); // Initialize renderer passes before taking deterministic comparisons.
                Color32[] Capture()
                {
                    Random.InitState(7306);
                    ResetRenderingNoise(data);
                    for (int i = 0; i < 3; i++) camera.Render();
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                    return image.GetPixels32();
                }
                Color32[] before = Capture();
                MainMenuOptimization.ApplyToScene(camera.gameObject.scene);
                var rigid = menu.GetComponentsInChildren<MeshRenderer>().Where(renderer =>
                    (GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic) != 0).ToArray();
                Assert.That(rigid.Length, Is.GreaterThan(300));
                // The Editor may have already batched a source scene optimized with the menu command.
                var unbatched = rigid.Where(renderer => !renderer.isPartOfStaticBatch).Select(renderer => renderer.gameObject).ToArray();
                if (unbatched.Length > 0) StaticBatchingUtility.Combine(unbatched, menu.gameObject);
                Assert.That(rigid.Count(renderer => renderer.isPartOfStaticBatch), Is.GreaterThan(300));
                Color32[] after = Capture();
                double difference = 0; int maximum = 0;
                for (int i = 0; i < before.Length; i++)
                {
                    int r = System.Math.Abs(before[i].r - after[i].r), g = System.Math.Abs(before[i].g - after[i].g), b = System.Math.Abs(before[i].b - after[i].b);
                    difference += r + g + b; maximum = System.Math.Max(maximum, System.Math.Max(r, System.Math.Max(g, b)));
                }
                double mean = difference / (before.Length * 3 * 255);
                Debug.Log($"Menu optimized render comparison: mean RGB difference {mean:F6}, max channel difference {maximum}/255; opaque copy {data.requiresColorOption}.");
                Assert.That(mean, Is.LessThan(0.001), "Batching must preserve the rendered world, including foliage, shadows and post-processing.");
            }
            finally
            {
                canvas.enabled = oldCanvas; data.requiresColorOption = oldColor;
                Random.state = oldRandom;
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive; ShaderUtil.allowAsyncCompilation = oldCompilation;
                Object.Destroy(target); Object.Destroy(image);
            }
        }

        private static void ResetRenderingNoise(UniversalAdditionalCameraData data)
        {
            // URP advances its blue-noise texture per camera.Render, even within the same frame.
            // Reset only test sampling so screenshot differences represent the optimization, not noise.
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                object pass = feature.GetType().GetField("m_SSAOPass", flags).GetValue(feature);
                if (pass != null) pass.GetType().GetField("m_BlueNoiseTextureIndex", flags).SetValue(pass, 0);
            }
            var activeRenderer = data.scriptableRenderer;
            object postProcessing = activeRenderer.GetType().GetField("m_PostProcess", flags).GetValue(activeRenderer);
            postProcessing.GetType().GetField("m_DitheringTextureIndex", flags).SetValue(postProcessing, 0);
        }

        private static Scene OpenUnoptimizedPreview()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(MenuScene);
            // Exercise the upgrade even after the user has saved its batching flags into the source.
            // Only temporary preview objects are modified; all other authored static flags survive.
            foreach (Transform item in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
            {
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(item.gameObject);
                GameObjectUtility.SetStaticEditorFlags(item.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
            }
            return scene;
        }
    }
}
