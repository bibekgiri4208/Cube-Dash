using UnityEngine;

namespace CubeDash
{
    /// <summary>Saved pickup/impact cues and vehicle loops; gameplay owns their lifecycle, pause and pitch.</summary>
    public sealed class RunnerAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource pickupSource = null;
        [SerializeField] private AudioSource flightSource = null;
        [SerializeField] private AudioSource truckSource = null;
        [SerializeField] private AudioSource supercarSource = null;
        [SerializeField] private AudioSource[] truckImpactSources = new AudioSource[0];
        [SerializeField] private AudioClip[] truckImpactClips = new AudioClip[3];
        [Tooltip("Shield, Double Points, Fighter Plane, Truck, Magnet.")]
        [SerializeField] private AudioClip[] pickupClips = new AudioClip[5];
        [SerializeField, Range(0, 1)] private float pickupVolume = 0.55f;
        [SerializeField, Range(0, 1)] private float flightVolume = 0.24f;
        [SerializeField, Range(0, 1)] private float truckVolume = 0.3f;
        [SerializeField, Range(0, 1)] private float supercarVolume = 0.55f;
        [SerializeField, Range(0, 1)] private float truckImpactVolume = 0.6f;
        private bool flightActive, truckActive, supercarActive, paused;
        private int nextTruckImpact;

        public AudioSource PickupSource => pickupSource;
        public AudioSource FlightSource => flightSource;
        public AudioSource TruckSource => truckSource;
        public AudioSource SupercarSource => supercarSource;
        public AudioSource[] TruckImpactSources => truckImpactSources;
        public AudioClip TruckImpactClip(int index) => index >= 0 && index < truckImpactClips.Length ? truckImpactClips[index] : null;
        public bool FlightActive => flightActive;
        public bool TruckActive => truckActive;
        public bool SupercarActive => supercarActive;
        public AudioClip PickupClip(PowerUpType type) => (int)type < pickupClips.Length ? pickupClips[(int)type] : null;

        public void PlayPickup(PowerUpType type)
        {
            AudioClip clip = PickupClip(type);
            if (pickupSource == null || clip == null || paused) return;
            pickupSource.pitch = 1;
            pickupSource.PlayOneShot(clip, pickupVolume);
        }

        public void PlayTruckImpacts(int count)
        {
            if (count <= 0 || paused || truckImpactSources.Length == 0 || truckImpactClips.Length == 0) return;
            // Fixed saved voices bound overlapping crashes, even during a long frame. Variation
            // is deterministic and never consumes the obstacle generator's random sequence.
            int voices = Mathf.Min(count, truckImpactSources.Length);
            float volume = truckImpactVolume / Mathf.Sqrt(voices);
            for (int i = 0; i < voices; i++)
            {
                int sequence = nextTruckImpact++;
                AudioSource source = truckImpactSources[sequence % truckImpactSources.Length];
                AudioClip clip = truckImpactClips[sequence % truckImpactClips.Length];
                if (source == null || clip == null) continue;
                source.Stop();
                source.clip = clip;
                source.volume = volume;
                source.pitch = 0.96f + (sequence % 3) * 0.04f;
                source.Play();
            }
        }

        public void Tick(float dt, CubeDashGame.RunState state, bool flying, bool trucking, float speed, float steering, bool drivingCar = false)
        {
            SetPaused(state == CubeDashGame.RunState.Paused);
            if (paused) return;
            if (state != CubeDashGame.RunState.Running) { ResetSounds(); return; }
            SetEngine(flightSource, flying && !trucking, ref flightActive);
            SetEngine(truckSource, trucking, ref truckActive);
            SetEngine(supercarSource, drivingCar && !flying && !trucking, ref supercarActive);
            float pace = Mathf.Clamp01(speed / 30);
            float response = 1 - Mathf.Exp(-6 * Mathf.Max(0, dt));
            if (flightActive && flightSource != null)
            {
                flightSource.volume = Mathf.Lerp(flightSource.volume, flightVolume * Mathf.Lerp(0.85f, 1, pace), response);
                flightSource.pitch = Mathf.Lerp(flightSource.pitch, Mathf.Lerp(0.94f, 1.06f, pace), response);
            }
            if (truckActive && truckSource != null)
            {
                truckSource.volume = Mathf.Lerp(truckSource.volume, truckVolume * Mathf.Lerp(0.8f, 1, pace), response);
                float rev = Mathf.Lerp(0.86f, 1.12f, pace) + Mathf.Clamp01(Mathf.Abs(steering) / 18) * 0.03f;
                truckSource.pitch = Mathf.Lerp(truckSource.pitch, rev, response);
            }
            if (supercarActive && supercarSource != null)
            {
                supercarSource.volume = Mathf.Lerp(supercarSource.volume, supercarVolume * Mathf.Lerp(0.8f, 1, pace), response);
                float rev = Mathf.Lerp(0.85f, 1.35f, pace) + Mathf.Clamp01(Mathf.Abs(steering) / 18) * 0.06f;
                supercarSource.pitch = Mathf.Lerp(supercarSource.pitch, rev, response);
            }
        }

        private static void SetEngine(AudioSource source, bool wanted, ref bool active)
        {
            // A selected engine must recover if playback was interrupted by an audio-device/editor reset.
            // Paused sources never reach this path: Tick returns above while gameplay is paused.
            if (active == wanted && (!wanted || source == null || source.isPlaying)) return;
            active = wanted;
            if (source == null) return;
            source.volume = 0;
            if (wanted) { source.pitch = 1; source.Play(); }
            else source.Stop();
        }

        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            PauseSource(pickupSource, value);
            PauseSource(flightSource, value);
            PauseSource(truckSource, value);
            PauseSource(supercarSource, value);
            foreach (AudioSource source in truckImpactSources) PauseSource(source, value);
        }

        private static void PauseSource(AudioSource source, bool value)
        {
            if (source == null) return;
            if (value) source.Pause(); else source.UnPause();
        }

        public void ResetSounds()
        {
            flightActive = truckActive = supercarActive = paused = false;
            nextTruckImpact = 0;
            if (pickupSource != null) pickupSource.Stop();
            if (flightSource != null) { flightSource.Stop(); flightSource.volume = 0; flightSource.pitch = 1; }
            if (truckSource != null) { truckSource.Stop(); truckSource.volume = 0; truckSource.pitch = 1; }
            if (supercarSource != null) { supercarSource.Stop(); supercarSource.volume = 0; supercarSource.pitch = 1; }
            foreach (AudioSource source in truckImpactSources)
                if (source != null) { source.Stop(); source.volume = 0; source.pitch = 1; source.clip = null; }
        }

        private void OnDisable() => ResetSounds();
    }
}
