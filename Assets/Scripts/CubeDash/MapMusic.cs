using UnityEngine;

namespace CubeDash
{
    /// <summary>One scene-owned music loop; independent of paused gameplay and SFX envelopes.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class MapMusic : MonoBehaviour
    {
        private AudioSource source;
        public AudioSource Source => source != null ? source : GetComponent<AudioSource>();

        private void OnEnable()
        {
            source = GetComponent<AudioSource>();
            source.volume = GameAudioSettings.MusicVolume;
            GameAudioSettings.MusicChanged += SetVolume;
        }

        private void Start()
        {
            // Mixer parameters must be applied after Awake/OnEnable.
            GameAudioSettings.Apply();
            if (source.clip != null) source.Play();
        }

        private void SetVolume(float value) => source.volume = value;
        private void OnDisable()
        {
            GameAudioSettings.MusicChanged -= SetVolume;
            if (source != null) source.Stop();
        }
    }
}
