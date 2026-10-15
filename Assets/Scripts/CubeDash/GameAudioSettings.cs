using UnityEngine;
using UnityEngine.Audio;

namespace CubeDash
{
    /// <summary>Shared, saved channel levels. Source envelopes remain owned by gameplay.</summary>
    public static class GameAudioSettings
    {
        public const string SfxPreference = "CubeDash.SfxVolume";
        public const string SfxParameter = "SfxVolume";
        public static event System.Action<float> MusicChanged;
        private static AudioMixer mixer;
        public static float MusicVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(MainMenuController.MusicVolumePreference, 1));
        public static float SfxVolume => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxPreference, 1));

        public static void Apply()
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MainMenuController.VolumePreference, 1));
            ApplySfx(SfxVolume);
        }

        public static void SetMusicVolume(float value)
        {
            value = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MainMenuController.MusicVolumePreference, value);
            MusicChanged?.Invoke(value);
        }

        public static void SetSfxVolume(float value)
        {
            value = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxPreference, value);
            ApplySfx(value);
        }

        public static float Decibels(float value) => value <= 0 ? -80 : Mathf.Max(-80, 20 * Mathf.Log10(Mathf.Clamp01(value)));

        private static void ApplySfx(float value)
        {
            if (mixer == null) mixer = Resources.Load<AudioMixer>("Game Audio");
            if (mixer != null) mixer.SetFloat(SfxParameter, Decibels(value));
        }
    }
}
