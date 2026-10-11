using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CubeDash.Editor
{
    /// <summary>Targeted graphics pass; leaves models, gameplay, audio and user-created volumes intact.</summary>
    public static class CubeDashGraphics
    {
        [MenuItem("Tools/Cube Dash/Improve Lighting and Post Processing")]
        public static void ApplyFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) ApplyFromCommandLine();
        }

        public static void ApplyFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level.unity");
            var game = Object.FindAnyObjectByType<CubeDashGame>();
            if (game == null) throw new InvalidOperationException("Cube Dash game is missing.");
            AssetDatabase.ImportAsset("Assets/Shaders/RunnerMotionBlur.shader", ImportAssetOptions.ForceUpdate);
            ConfigureProfile();
            ConfigureRenderer("PC", 0.035f);
            ConfigureRenderer("Mobile", 0.022f);
            var camera = game.GameCamera;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            camera.allowHDR = true;
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.dithering = true;
            cameraData.stopNaN = true;
            var graphics = camera.GetComponent<RunnerGraphics>() ?? camera.gameObject.AddComponent<RunnerGraphics>();
            Set(graphics, "game", game);
            var gameSettings = new SerializedObject(game);
            var graphicsSettings = new SerializedObject(graphics);
            float maximumSpeed = gameSettings.FindProperty("maximumSpeed").floatValue;
            graphicsSettings.FindProperty("blurStartSpeed").floatValue = Mathf.Min(maximumSpeed - 1,
                gameSettings.FindProperty("startSpeed").floatValue + 2);
            graphicsSettings.FindProperty("blurFullSpeed").floatValue = maximumSpeed;
            graphicsSettings.ApplyModifiedPropertiesWithoutUndo();
            var settings = new SerializedObject(game.Track.Environment);
            Light sun = settings.FindProperty("sunlight").objectReferenceValue as Light;
            Light fill = settings.FindProperty("skyFill").objectReferenceValue as Light;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(48, -32, 0);
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.92f;
                sun.shadowBias = 0.035f; sun.shadowNormalBias = 0.25f;
            }
            if (fill != null) { fill.intensity = 0.16f; fill.shadows = LightShadows.None; }
            // Keep each biome's established sky/ground palette, but reduce the flat ambient wash
            // and give the directional key light stronger separation from the shaded faces.
            float[] intensities = { 2.05f, 1.85f, 2.15f, 2.25f, 2.1f };
            var atmospheres = settings.FindProperty("atmospheres");
            for (int i = 0; i < atmospheres.arraySize; i++)
            {
                var value = atmospheres.GetArrayElementAtIndex(i);
                Color horizon = value.FindPropertyRelative("Horizon").colorValue;
                value.FindPropertyRelative("AmbientSky").colorValue = Color.Lerp(horizon, new Color(0.45f, 0.61f, 0.79f), 0.55f) * 0.76f;
                value.FindPropertyRelative("AmbientEquator").colorValue = Color.Lerp(horizon, new Color(0.51f, 0.55f, 0.61f), 0.55f) * 0.62f;
                value.FindPropertyRelative("AmbientGround").colorValue = new Color(0.24f, 0.25f, 0.28f);
                value.FindPropertyRelative("SunIntensity").floatValue = intensities[i];
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            game.Track.Environment.ApplyDistance(0, 0);
            Material sky = RenderSettings.skybox;
            if (sky != null && sun != null)
            {
                var city = settings.FindProperty("atmospheres").GetArrayElementAtIndex(0);
                sky.SetColor("_HorizonColor", city.FindPropertyRelative("Horizon").colorValue);
                sky.SetColor("_ZenithColor", city.FindPropertyRelative("Zenith").colorValue);
                sky.SetColor("_GroundColor", city.FindPropertyRelative("Ground").colorValue);
                sky.SetVector("_SunDirection", -sun.transform.forward);
                sky.SetFloat("_SunSize", 0.65f);
                sky.SetColor("_SunColor", sun.color * 1.5f);
                EditorUtility.SetDirty(sky);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Graphics saved: directional daylight, softer sky fill, contact shading, surface relief, selective bloom, neutral grading, SMAA/dithering and speed-responsive peripheral blur. No DOF, film grain or color-fringing; gameplay and HUD preserved.");
        }

        private static void ConfigureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/CubeDashVisuals.asset");
            if (profile == null) throw new InvalidOperationException("Missing CubeDashVisuals profile.");
            T Effect<T>() where T : VolumeComponent
            {
                if (profile.TryGet(out T existing)) return existing;
                T effect = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(effect, profile); return effect;
            }
            Bloom bloom = Effect<Bloom>(); bloom.active = true;
            bloom.threshold.Override(1.12f); bloom.intensity.Override(0.30f); bloom.scatter.Override(0.62f);
            bloom.clamp.Override(8); bloom.highQualityFiltering.Override(true);
            ColorAdjustments grade = Effect<ColorAdjustments>(); grade.active = true;
            grade.postExposure.Override(0.12f); grade.contrast.Override(12); grade.saturation.Override(4);
            grade.hueShift.Override(0); grade.colorFilter.Override(Color.white);
            Tonemapping tone = Effect<Tonemapping>(); tone.active = true; tone.mode.Override(TonemappingMode.Neutral);
            Vignette vignette = Effect<Vignette>(); vignette.active = true;
            vignette.intensity.Override(0.16f); vignette.smoothness.Override(0.65f);
            // Keep the lane colors and fast-moving pickups clean, instead of photographic artifacts.
            if (profile.TryGet(out DepthOfField dof)) dof.active = false;
            if (profile.TryGet(out MotionBlur motion)) motion.active = false;
            if (profile.TryGet(out ChromaticAberration fringe)) fringe.active = false;
            if (profile.TryGet(out FilmGrain grain)) grain.active = false;
            foreach (var component in profile.components) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
        }

        private static void ConfigureRenderer(string tier, float blurRadius)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/" + tier + "_Renderer.asset");
            RunnerMotionBlurFeature blur = null;
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature is RunnerMotionBlurFeature existing) blur = existing;
                if (tier == "PC" && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                {
                    var ao = new SerializedObject(feature);
                    ao.FindProperty("m_Settings.Intensity").floatValue = 0.75f;
                    ao.FindProperty("m_Settings.Radius").floatValue = 0.45f;
                    ao.FindProperty("m_Settings.DirectLightingStrength").floatValue = 0.18f;
                    ao.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            if (blur == null)
            {
                blur = ScriptableObject.CreateInstance<RunnerMotionBlurFeature>();
                blur.name = "Cube Dash Peripheral Motion Blur";
                AssetDatabase.AddObjectToAsset(blur, renderer);
                renderer.rendererFeatures.Add(blur);
            }
            var serialized = new SerializedObject(blur);
            serialized.FindProperty("blurShader").objectReferenceValue = Shader.Find("Hidden/CubeDash/Peripheral Motion Blur");
            serialized.FindProperty("maximumRadius").floatValue = blurRadius;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // Keep URP's recovery map in sync, so feature references survive reload/builds.
            var rendererSettings = new SerializedObject(renderer);
            var map = rendererSettings.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < map.arraySize; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string guid, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            rendererSettings.ApplyModifiedPropertiesWithoutUndo();
            blur.SetActive(true); blur.Create(); renderer.SetDirty(); EditorUtility.SetDirty(renderer);
        }

        private static void Set(Object target, string field, Object value)
        {
            var settings = new SerializedObject(target);
            settings.FindProperty(field).objectReferenceValue = value;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
