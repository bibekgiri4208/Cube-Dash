using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CubeDash.Tests
{
    public sealed class GraphicsTests
    {
        [Test]
        public void PostProcessingPreservesGameplayColorsAndAvoidsDistractingArtifacts()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/CubeDashVisuals.asset");
            Assert.That(profile.TryGet(out Bloom bloom), Is.True);
            Assert.That(bloom.threshold.value, Is.GreaterThan(1));
            Assert.That(bloom.intensity.value, Is.InRange(0.2f, 0.4f));
            Assert.That(profile.TryGet(out Tonemapping tone), Is.True);
            Assert.That(tone.mode.value, Is.EqualTo(TonemappingMode.Neutral));
            Assert.That(profile.TryGet(out ColorAdjustments grade), Is.True);
            Assert.That(grade.contrast.value, Is.InRange(8, 16));
            Assert.That(grade.hueShift.value, Is.Zero);
            Assert.That(grade.colorFilter.value, Is.EqualTo(Color.white));
            Assert.That(!profile.TryGet(out DepthOfField dof) || !dof.active, Is.True);
            Assert.That(!profile.TryGet(out ChromaticAberration fringe) || !fringe.active, Is.True);
            Assert.That(!profile.TryGet(out MotionBlur motion) || !motion.active, Is.True);
        }

        [TestCase("PC", 0.035f)]
        [TestCase("Mobile", 0.022f)]
        public void RenderersHaveExactlyOneSavedSpeedBlurFeatureWithShaderReference(string tier, float radius)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/" + tier + "_Renderer.asset");
            var features = renderer.rendererFeatures.OfType<RunnerMotionBlurFeature>().ToArray();
            Assert.That(features.Length, Is.EqualTo(1)); Assert.That(features[0].isActive, Is.True);
            Assert.That(new SerializedObject(renderer).FindProperty("m_RendererFeatureMap").arraySize,
                Is.EqualTo(renderer.rendererFeatures.Count));
            var settings = new SerializedObject(features[0]);
            Shader shader = settings.FindProperty("blurShader").objectReferenceValue as Shader;
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            Assert.That(settings.FindProperty("maximumRadius").floatValue, Is.EqualTo(radius));
            Assert.That(ShaderUtil.ShaderHasError(Shader.Find("CubeDash/Environment Surface")), Is.False);
        }

        [Test]
        public void SavedLightingHasDirectionalSeparationAndBiomeBlendedAmbientColors()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                var director = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EnvironmentDirector>(true)).First();
                var settings = new SerializedObject(director);
                var sun = (Light)settings.FindProperty("sunlight").objectReferenceValue;
                var fill = (Light)settings.FindProperty("skyFill").objectReferenceValue;
                Assert.That(sun.shadows, Is.EqualTo(LightShadows.Soft));
                Assert.That(sun.shadowStrength, Is.InRange(0.85f, 1));
                Assert.That(fill.shadows, Is.EqualTo(LightShadows.None));
                Assert.That(fill.intensity, Is.InRange(0.1f, 0.2f));
                var looks = settings.FindProperty("atmospheres");
                Assert.That(looks.arraySize, Is.EqualTo(BiomeRules.Count));
                for (int i = 0; i < looks.arraySize; i++)
                {
                    var look = looks.GetArrayElementAtIndex(i);
                    Assert.That(look.FindPropertyRelative("SunIntensity").floatValue, Is.InRange(1.7f, 2.4f));
                    Assert.That(look.FindPropertyRelative("AmbientGround").colorValue.maxColorComponent, Is.LessThan(0.35f));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void SpeedBlurResponseIsBoundedAndScalesWithSpeed()
        {
            Assert.That(RunnerGraphics.SpeedResponse(0, 18, 48), Is.Zero);
            Assert.That(RunnerGraphics.SpeedResponse(18, 18, 48), Is.Zero);
            Assert.That(RunnerGraphics.SpeedResponse(48, 18, 48), Is.EqualTo(1));
            Assert.That(RunnerGraphics.SpeedResponse(1000, 18, 48), Is.EqualTo(1));
            float previous = 0;
            for (int speed = 0; speed < 100; speed++)
            {
                float response = RunnerGraphics.SpeedResponse(speed, 18, 48);
                Assert.That(response, Is.InRange(previous, 1)); previous = response;
            }
        }

        [UnityTest]
        public IEnumerator SavedCameraBlurRespondsToSpeedAndStopsImmediatelyOnPauseRestartAndDisable()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>();
            var graphics = game.GameCamera.GetComponent<RunnerGraphics>();
            Assert.That(graphics, Is.Not.Null); Assert.That(graphics.BlurAmount, Is.Zero);
            Assert.That(new SerializedObject(graphics).FindProperty("blurFullSpeed").floatValue,
                Is.EqualTo(new SerializedObject(game).FindProperty("maximumSpeed").floatValue));
            var cameraData = game.GameCamera.GetUniversalAdditionalCameraData();
            Assert.That(cameraData.renderPostProcessing, Is.True);
            Assert.That(cameraData.antialiasing, Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
            Assert.That(cameraData.dithering, Is.True);
            game.StartRun(); graphics.Tick(2, game.State, 12);
            Assert.That(graphics.BlurAmount, Is.Zero);
            graphics.Tick(2, game.State, 48);
            Assert.That(graphics.BlurAmount, Is.GreaterThan(0.5f));
            game.TogglePause(); Assert.That(graphics.BlurAmount, Is.Zero);
            graphics.Tick(1, game.State, 48); Assert.That(graphics.BlurAmount, Is.Zero);
            game.TogglePause(); graphics.Tick(2, game.State, 48);
            Assert.That(graphics.BlurAmount, Is.GreaterThan(0.5f));
            game.StartRun(); Assert.That(graphics.BlurAmount, Is.Zero);
            graphics.Tick(2, game.State, 48); graphics.enabled = false;
            Assert.That(graphics.BlurAmount, Is.Zero);
            graphics.enabled = true; Assert.That(graphics.BlurAmount, Is.Zero);
            graphics.Tick(2, game.State, 48); game.enabled = false;
            Assert.That(graphics.BlurAmount, Is.Zero);
            game.enabled = true; Assert.That(graphics.BlurAmount, Is.Zero);
            graphics.Tick(2, CubeDashGame.RunState.GameOver, 48); Assert.That(graphics.BlurAmount, Is.Zero);
            graphics.Tick(2, game.State, 48);
            typeof(CubeDashGame).GetMethod("Crash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            Assert.That(graphics.BlurAmount, Is.Zero);
            game.StartRun();
            var settings = new SerializedObject(graphics);
            settings.FindProperty("motionBlurStrength").floatValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            graphics.Tick(2, game.State, 48); Assert.That(graphics.BlurAmount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SpeedBlurChangesPeripheralPixelsButPreservesTheCentralRoad()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("GPU rendering required.");
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun();
            foreach (var segment in game.Track.Segments) segment.GetComponent<SceneryRise>().Settle();
            var graphics = game.GameCamera.GetComponent<RunnerGraphics>();
            var target = new RenderTexture(640, 360, 24);
            var image = new Texture2D(640, 360, TextureFormat.RGBA32, false);
            var oldTarget = game.GameCamera.targetTexture; var oldActive = RenderTexture.active;
            int oldQuality = QualitySettings.GetQualityLevel();
            try
            {
                Color32[] Capture()
                {
                    game.GameCamera.targetTexture = target; game.GameCamera.Render();
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); image.Apply();
                    return image.GetPixels32();
                }
                foreach (int quality in new[] { 1, 0 })
                {
                    QualitySettings.SetQualityLevel(quality, true);
                    graphics.Tick(0, CubeDashGame.RunState.Ready, 0);
                    Capture(); var sharp = Capture();
                    graphics.Tick(2, CubeDashGame.RunState.Running, 48);
                    var blurred = Capture();
                    long centerError = 0, edgeError = 0;
                    for (int y = 20; y < 210; y++) for (int x = 0; x < 640; x++)
                    {
                        int i = y * 640 + x;
                        int error = Mathf.Abs(sharp[i].r - blurred[i].r) + Mathf.Abs(sharp[i].g - blurred[i].g) + Mathf.Abs(sharp[i].b - blurred[i].b);
                        if (x > 245 && x < 395) centerError += error;
                        if (x < 75 || x > 565) edgeError += error;
                    }
                    Assert.That(edgeError, Is.GreaterThan(1000), "The RenderGraph effect must actually execute, quality " + quality);
                    Assert.That(centerError, Is.LessThan(edgeError * 0.05 + 100), "The central lanes must remain readable, quality " + quality);
                }
            }
            finally
            {
                game.GameCamera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                QualitySettings.SetQualityLevel(oldQuality, true);
                Object.DestroyImmediate(image); Object.DestroyImmediate(target);
            }
        }
    }
}
