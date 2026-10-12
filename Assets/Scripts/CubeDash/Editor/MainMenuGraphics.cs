using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CubeDash.Editor
{
    /// <summary>Menu-only grading. Does not regenerate geometry or alter gameplay's graphics profile.</summary>
    public static class MainMenuGraphics
    {
        public const string ProfilePath = "Assets/MainMenu/Menu Lighting.asset";

        [MenuItem("Tools/Cube Dash/Tune Main Menu Post Processing")]
        public static void ApplyFromMenu()
        {
            ApplyFromCommandLine();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public static void ApplyFromCommandLine()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null) throw new InvalidOperationException("Missing menu lighting profile. Build the menu first.");
            ConfigureProfile(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            Debug.Log("Bright menu post-processing tuned: neutral tonemapping, lifted exposure, soft contrast, vibrant colors and broad bloom; no dark vignette. Scene layout and Level graphics are unchanged.");
        }

        internal static void ConfigureProfile(VolumeProfile profile)
        {
            T Effect<T>() where T : VolumeComponent
            {
                if (profile.TryGet(out T existing)) { existing.active = true; return existing; }
                T effect = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(effect, profile);
                return effect;
            }

            // Bright, airy arcade daylight rather than a dark cinematic grade.
            Effect<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            ColorAdjustments grade = Effect<ColorAdjustments>();
            grade.postExposure.Override(0.45f); grade.contrast.Override(-8); grade.saturation.Override(16);
            grade.hueShift.Override(0); grade.colorFilter.Override(Color.white);
            WhiteBalance balance = Effect<WhiteBalance>();
            balance.temperature.Override(3); balance.tint.Override(1);
            ShadowsMidtonesHighlights separation = Effect<ShadowsMidtonesHighlights>();
            separation.shadows.Override(new Vector4(1, 1, 1, 0.04f));
            separation.midtones.Override(new Vector4(1, 1, 1, 0));
            separation.highlights.Override(new Vector4(1.015f, 1.005f, 0.985f, 0));
            separation.shadowsStart.Override(0); separation.shadowsEnd.Override(0.35f);
            separation.highlightsStart.Override(0.55f); separation.highlightsEnd.Override(1);
            Bloom bloom = Effect<Bloom>();
            bloom.intensity.Override(1.1f); bloom.threshold.Override(0.7f); bloom.scatter.Override(0.8f);
            bloom.clamp.Override(8); bloom.highQualityFiltering.Override(true);
            Vignette vignette = Effect<Vignette>();
            vignette.intensity.Override(0); vignette.smoothness.Override(0.75f);
            // The mascot (~9m) and graffiti wall (~18m) stay sharp; only distant scenery softens.
            DepthOfField depth = Effect<DepthOfField>();
            depth.mode.Override(DepthOfFieldMode.Gaussian);
            depth.gaussianStart.Override(24); depth.gaussianEnd.Override(55);
            depth.gaussianMaxRadius.Override(0.7f); depth.highQualitySampling.Override(true);
            if (profile.TryGet(out MotionBlur motion)) motion.active = false;
            if (profile.TryGet(out ChromaticAberration fringe)) fringe.active = false;
            if (profile.TryGet(out FilmGrain grain)) grain.active = false;
            foreach (VolumeComponent component in profile.components) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
        }

        public static void ApplyAndCaptureFromCommandLine()
        {
            ApplyFromCommandLine();
            MainMenuSceneBuilder.CaptureFromCommandLine();
        }
    }
}
