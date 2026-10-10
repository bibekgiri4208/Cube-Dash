using UnityEngine;

namespace CubeDash
{
    /// <summary>Animates the saved truck, suspension, steering and bounded stack/tire smoke with gameplay.</summary>
    public sealed class PlayerTruck : MonoBehaviour
    {
        [SerializeField] private Transform truck = null;
        [SerializeField] private Transform[] wheels = new Transform[0];
        [SerializeField] private ParticleSystem[] exhaust = new ParticleSystem[0];
        [SerializeField] private ParticleSystem[] tireSmoke = new ParticleSystem[0];
        [SerializeField] private Vector2 contactHalfSize = new Vector2(0.74f, 1.62f);
        private Vector3 home;
        private Quaternion homeRotation = Quaternion.identity;
        private Quaternion[] wheelRotations;
        private float roadPhase;
        private float wheelSpin;
        private float steering;
        private float previousSpeed;
        private bool hasSpeed;
        private bool driving;
        public Transform Truck => truck;
        public Transform[] Wheels => wheels;
        public ParticleSystem[] Exhaust => exhaust;
        public ParticleSystem[] TireSmoke => tireSmoke;
        public Vector2 ContactHalfSize => contactHalfSize;

        private void Awake()
        {
            if (truck != null) { home = truck.localPosition; homeRotation = truck.localRotation; }
            wheelRotations = new Quaternion[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
                wheelRotations[i] = wheels[i] != null ? wheels[i].localRotation : Quaternion.identity;
        }

        public void SetDriving(bool value)
        {
            if (driving == value) return;
            if (!value) { ResetPresentation(); return; }
            driving = true;
            if (truck != null) truck.gameObject.SetActive(true);
            StartSmoke(exhaust);
            StartSmoke(tireSmoke);
        }

        private static void StartSmoke(ParticleSystem[] systems)
        {
            foreach (ParticleSystem system in systems)
            {
                if (system == null) continue;
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Play(false); system.Pause(false);
            }
        }

        public void Tick(float dt, float speed, float laneVelocity)
        {
            if (!driving || truck == null || dt <= 0) return;
            float response = 1 - Mathf.Exp(-8 * dt);
            float moving = Mathf.Clamp01(speed / 12f);
            roadPhase = Mathf.Repeat(roadPhase + speed * dt * 1.7f, Mathf.PI * 2);
            float bounce = (Mathf.Sin(roadPhase) * 0.026f + Mathf.Sin(roadPhase * 2) * 0.008f) * moving;
            float acceleration = hasSpeed ? Mathf.Clamp((speed - previousSpeed) / dt, -4, 4) : 0;
            previousSpeed = speed; hasSpeed = true;
            truck.localPosition = Vector3.Lerp(truck.localPosition, home + Vector3.up * bounce, response);
            truck.localRotation = Quaternion.Slerp(truck.localRotation,
                homeRotation * Quaternion.Euler(Mathf.Sin(roadPhase) * 1.2f * moving - acceleration * 0.6f,
                    Mathf.Clamp(laneVelocity * 0.5f, -8, 8),
                    Mathf.Clamp(-laneVelocity * 0.5f, -6, 6) + Mathf.Sin(roadPhase + 0.7f) * 0.65f * moving), response);
            steering = Mathf.Lerp(steering, Mathf.Clamp(laneVelocity * 2.2f, -24, 24), response);
            float radius = 0.58f * truck.localScale.y;
            wheelSpin = Mathf.Repeat(wheelSpin + speed * dt / Mathf.Max(0.01f, radius) * Mathf.Rad2Deg, 360);
            for (int i = 0; i < wheels.Length; i++)
            {
                Transform wheel = wheels[i];
                if (wheel == null) continue;
                // The front axle steers independently of tire spin; rear tandem wheels remain straight.
                wheel.localRotation = wheelRotations[i] * Quaternion.Euler(0, wheel.localPosition.z > 0 ? steering : 0, 0)
                    * Quaternion.AngleAxis(wheelSpin, Vector3.right);
            }
            foreach (ParticleSystem system in exhaust)
            {
                if (system == null) continue;
                var emission = system.emission; emission.rateOverTime = Mathf.Lerp(18, 32, Mathf.Clamp01(speed / 30));
                var velocity = system.velocityOverLifetime; velocity.z = -Mathf.Max(1.5f, speed * 0.32f);
                system.Simulate(dt, false, false, false);
            }
            foreach (ParticleSystem system in tireSmoke)
            {
                if (system == null) continue;
                float skid = Mathf.Clamp01(Mathf.Abs(laneVelocity) / 12f);
                var emission = system.emission;
                emission.rateOverTime = (Mathf.Lerp(10, 24, Mathf.Clamp01(speed / 30)) + skid * 14) * moving;
                var velocity = system.velocityOverLifetime;
                velocity.x = -laneVelocity * 0.08f;
                velocity.z = -Mathf.Max(1, speed * 0.45f);
                system.Simulate(dt, false, false, false);
            }
        }

        public void ResetPresentation()
        {
            driving = false;
            roadPhase = wheelSpin = steering = previousSpeed = 0;
            hasSpeed = false;
            if (truck != null)
            {
                truck.gameObject.SetActive(false);
                truck.localPosition = home;
                truck.localRotation = homeRotation;
            }
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] != null) wheels[i].localRotation = wheelRotations != null ? wheelRotations[i] : Quaternion.identity;
            foreach (ParticleSystem system in exhaust)
                if (system != null) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (ParticleSystem system in tireSmoke)
                if (system != null) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable() => ResetPresentation();
    }
}
