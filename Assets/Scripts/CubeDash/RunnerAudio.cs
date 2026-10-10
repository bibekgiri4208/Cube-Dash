using UnityEngine;

namespace CubeDash
{
    /// <summary>Saved pickup cues and vehicle loops; gameplay owns their lifecycle, pause and pitch.</summary>
    public sealed class RunnerAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource pickupSource = null;
        [SerializeField] private AudioSource flightSource = null;
        [SerializeField] private AudioSource truckSource = null;
        [Tooltip("Shield, Double Points, Fighter Plane, Truck, Magnet.")]
        [SerializeField] private AudioClip[] pickupClips = new AudioClip[5];
        [SerializeField, Range(0, 1)] private float pickupVolume = 0.55f;
        [SerializeField, Range(0, 1)] private float flightVolume = 0.24f;
        [SerializeField, Range(0, 1)] private float truckVolume = 0.3f;
        private bool flightActive, truckActive, paused;

        public AudioSource PickupSource => pickupSource;
        public AudioSource FlightSource => flightSource;
        public AudioSource TruckSource => truckSource;
        public bool FlightActive => flightActive;
        public bool TruckActive => truckActive;
        public AudioClip PickupClip(PowerUpType type) => (int)type < pickupClips.Length ? pickupClips[(int)type] : null;

        public void PlayPickup(PowerUpType type)
        {
            AudioClip clip = PickupClip(type);
            if (pickupSource == null || clip == null || paused) return;
            pickupSource.pitch = 1;
            pickupSource.PlayOneShot(clip, pickupVolume);
        }

        public void Tick(float dt, CubeDashGame.RunState state, bool flying, bool trucking, float speed, float steering)
        {
            SetPaused(state == CubeDashGame.RunState.Paused);
            if (paused) return;
            if (state != CubeDashGame.RunState.Running) { ResetSounds(); return; }
            SetEngine(flightSource, flying && !trucking, ref flightActive);
            SetEngine(truckSource, trucking, ref truckActive);
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
        }

        private static void SetEngine(AudioSource source, bool wanted, ref bool active)
        {
            if (active == wanted) return;
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
        }

        private static void PauseSource(AudioSource source, bool value)
        {
            if (source == null) return;
            if (value) source.Pause(); else source.UnPause();
        }

        public void ResetSounds()
        {
            flightActive = truckActive = paused = false;
            if (pickupSource != null) pickupSource.Stop();
            if (flightSource != null) { flightSource.Stop(); flightSource.volume = 0; flightSource.pitch = 1; }
            if (truckSource != null) { truckSource.Stop(); truckSource.volume = 0; truckSource.pitch = 1; }
        }

        private void OnDisable() => ResetSounds();
    }
}
