using System;
using UnityEngine;

namespace CubeDash
{
    /// <summary>Blends the atmosphere by travelled distance; never changes gameplay colors.</summary>
    public sealed class EnvironmentDirector : MonoBehaviour
    {
        [Serializable]
        public struct Atmosphere
        {
            public Color Horizon, Zenith, Ground, Sun, AmbientSky, AmbientEquator, AmbientGround;
            public float SunIntensity;
        }

        [Tooltip("Each biome lasts this many 42-metre sections. Default: 336 metres.")]
        [SerializeField, Min(2)] private int segmentsPerBiome = 8;
        [SerializeField, Range(10f, 84f)] private float transitionDistance = 42f;
        [SerializeField] private Light sunlight = null;
        [SerializeField] private Light skyFill = null;
        [SerializeField] private Atmosphere[] atmospheres = new Atmosphere[BiomeRules.Count];
        private Material originalSky, runtimeSky;
        private Color originalFog, originalAmbientSky, originalEquator, originalGround;
        private float animationTime;
        public int SegmentsPerBiome => segmentsPerBiome;
        public float BiomeLength => BiomeRules.Length(segmentsPerBiome);
        public EnvironmentBiome CurrentBiome { get; private set; }

        private void EnsureSky()
        {
            if (!Application.isPlaying || runtimeSky != null) return;
            originalSky = RenderSettings.skybox;
            originalFog = RenderSettings.fogColor;
            originalAmbientSky = RenderSettings.ambientSkyColor;
            originalEquator = RenderSettings.ambientEquatorColor;
            originalGround = RenderSettings.ambientGroundColor;
            if (originalSky != null)
            {
                runtimeSky = new Material(originalSky) { name = "Cube Dash runtime atmosphere" };
                RenderSettings.skybox = runtimeSky;
            }
        }

        public void ResetRun()
        {
            animationTime = 0;
            ApplyDistance(0, 0);
        }

        public void ApplyDistance(float distance, float deltaTime)
        {
            EnsureSky();
            animationTime += Mathf.Max(0, deltaTime);
            Shader.SetGlobalFloat("_CubeDashEnvironmentTime", animationTime);
            CurrentBiome = BiomeRules.AtDistance(distance, segmentsPerBiome);
            if (atmospheres == null || atmospheres.Length != BiomeRules.Count) return;

            // Blend across a boundary, rather than suddenly replacing the whole background.
            float width = Mathf.Clamp(transitionDistance, 1f, BiomeLength);
            float shifted = Mathf.Max(0, distance) + width * 0.5f;
            int next = (int)BiomeRules.AtDistance(shifted, segmentsPerBiome);
            int previous = (next + BiomeRules.Count - 1) % BiomeRules.Count;
            float phase = shifted % BiomeLength;
            float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(phase / width));
            if (shifted < BiomeLength) { previous = 0; next = 0; }
            Atmosphere a = atmospheres[previous], b = atmospheres[next];
            Color horizon = Color.Lerp(a.Horizon, b.Horizon, blend);
            RenderSettings.fogColor = horizon;
            RenderSettings.ambientSkyColor = Color.Lerp(a.AmbientSky, b.AmbientSky, blend);
            RenderSettings.ambientEquatorColor = Color.Lerp(a.AmbientEquator, b.AmbientEquator, blend);
            RenderSettings.ambientGroundColor = Color.Lerp(a.AmbientGround, b.AmbientGround, blend);
            if (runtimeSky != null)
            {
                runtimeSky.SetColor("_HorizonColor", horizon);
                runtimeSky.SetColor("_ZenithColor", Color.Lerp(a.Zenith, b.Zenith, blend));
                runtimeSky.SetColor("_GroundColor", Color.Lerp(a.Ground, b.Ground, blend));
                runtimeSky.SetColor("_SunColor", Color.Lerp(a.Sun, b.Sun, blend) * 1.2f);
            }
            if (sunlight != null)
            {
                sunlight.color = Color.Lerp(a.Sun, b.Sun, blend);
                sunlight.intensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, blend);
            }
            if (skyFill != null) skyFill.color = Color.Lerp(a.AmbientSky, b.AmbientSky, blend);
        }

        private void OnDestroy()
        {
            if (runtimeSky == null) return;
            if (RenderSettings.skybox == runtimeSky)
            {
                RenderSettings.skybox = originalSky;
                RenderSettings.fogColor = originalFog;
                RenderSettings.ambientSkyColor = originalAmbientSky;
                RenderSettings.ambientEquatorColor = originalEquator;
                RenderSettings.ambientGroundColor = originalGround;
            }
            if (Application.isPlaying) Destroy(runtimeSky);
            else DestroyImmediate(runtimeSky);
        }
    }
}
