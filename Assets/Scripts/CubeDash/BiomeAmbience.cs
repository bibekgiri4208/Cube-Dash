using UnityEngine;

namespace CubeDash
{
    /// <summary>Saved stereo ambience beds, crossfaded using the scenery's distance blend.</summary>
    public sealed class BiomeAmbience : MonoBehaviour
    {
        [SerializeField] private EnvironmentDirector environment = null;
        [Tooltip("City (silent), Jungle, Mountains, Desert, Beach. Preserve stereo for an enveloping background.")]
        [SerializeField] private AudioSource[] sources = new AudioSource[BiomeRules.Count];
        [SerializeField, Range(0, 1)] private float volume = 0.32f;
        [SerializeField, Min(0.05f)] private float fadeInSeconds = 0.8f;
        [SerializeField, Min(0.05f)] private float fadeOutSeconds = 1.2f;
        private readonly bool[] active = new bool[BiomeRules.Count];
        private float runGain;
        private bool paused;

        public float RunGain => runGain;
        public bool Paused => paused;
        public AudioSource Source(EnvironmentBiome biome) => sources != null && (int)biome < sources.Length ? sources[(int)biome] : null;
        public bool Active(EnvironmentBiome biome) => active[(int)biome];

        public static float Weight(EnvironmentBiome biome, EnvironmentBiome previous, EnvironmentBiome next, float blend)
        {
            if (previous == next) return biome == next ? 1 : 0;
            float angle = Mathf.Clamp01(blend) * Mathf.PI * 0.5f;
            // Equal-power mixing avoids the audible volume dip of linear crossfades.
            if (biome == previous) return blend >= 1 ? 0 : Mathf.Cos(angle);
            if (biome == next) return blend <= 0 ? 0 : Mathf.Sin(angle);
            return 0;
        }

        public void Tick(float dt, CubeDashGame.RunState state, float distance)
        {
            if (!isActiveAndEnabled) return;
            SetPaused(state == CubeDashGame.RunState.Paused);
            if (paused || environment == null) return;
            bool running = state == CubeDashGame.RunState.Running;
            runGain = Mathf.MoveTowards(runGain, running ? 1 : 0,
                Mathf.Max(0, dt) / Mathf.Max(0.05f, running ? fadeInSeconds : fadeOutSeconds));
            float envelope = Mathf.SmoothStep(0, 1, runGain);
            BiomeRules.BlendAtDistance(distance, environment.SegmentsPerBiome, environment.TransitionDistance,
                out EnvironmentBiome previous, out EnvironmentBiome next, out float blend);
            for (int i = 0; i < BiomeRules.Count; i++)
            {
                AudioSource source = Source((EnvironmentBiome)i);
                if (source == null) continue;
                float gain = Weight((EnvironmentBiome)i, previous, next, blend) * envelope * volume;
                if (gain > 0 && source.clip != null)
                {
                    source.volume = gain;
                    if (!active[i]) { source.Play(); active[i] = true; }
                }
                else
                {
                    source.volume = 0;
                    if (active[i]) { source.Stop(); active[i] = false; }
                }
            }
        }

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            for (int i = 0; i < BiomeRules.Count; i++)
            {
                AudioSource source = Source((EnvironmentBiome)i);
                if (source == null || !active[i]) continue;
                if (value) source.Pause(); else source.UnPause();
            }
        }

        public void ResetSounds()
        {
            runGain = 0; paused = false;
            for (int i = 0; i < BiomeRules.Count; i++)
            {
                active[i] = false;
                AudioSource source = Source((EnvironmentBiome)i);
                if (source == null) continue;
                source.Stop(); source.volume = 0;
            }
        }

        private void OnDisable() => ResetSounds();
    }
}
