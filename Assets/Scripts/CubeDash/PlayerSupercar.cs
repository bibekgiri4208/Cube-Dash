using UnityEngine;

namespace CubeDash
{
    /// <summary>Saved cosmetic Supercar, with rolling wheels, front steering and restrained chassis motion.</summary>
    public sealed class PlayerSupercar : MonoBehaviour
    {
        [SerializeField] private Transform car = null;
        [Tooltip("Left front, right front, left rear, right rear.")]
        [SerializeField] private Transform[] wheels = new Transform[0];
        private Vector3 home, scale;
        private Quaternion rotation;
        private Quaternion[] wheelHomes;
        private float spin, steering, roadPhase, age;
        private bool driving;
        private bool initialized;
        public Transform Car => car;
        public Transform[] Wheels => wheels;
        public bool Driving => driving;

        private void Awake() => EnsureHomes();

        private void EnsureHomes()
        {
            if (initialized) return;
            initialized = true;
            if (car != null) { home = car.localPosition; scale = car.localScale; rotation = car.localRotation; }
            wheelHomes = new Quaternion[wheels.Length];
            for (int i = 0; i < wheels.Length; i++) wheelHomes[i] = wheels[i] != null ? wheels[i].localRotation : Quaternion.identity;
        }

        public void SetDriving(bool value)
        {
            EnsureHomes();
            if (driving == value) return;
            if (!value) { ResetPresentation(); return; }
            driving = true; age = 0;
            if (car != null) car.gameObject.SetActive(true);
        }

        public void Tick(float dt, float speed, float laneVelocity)
        {
            if (!driving || car == null || dt <= 0) return;
            age += dt;
            float response = 1 - Mathf.Exp(-12 * dt);
            roadPhase = Mathf.Repeat(roadPhase + speed * dt * 1.8f, Mathf.PI * 2);
            car.localPosition = home + Vector3.up * (0.006f + Mathf.Sin(roadPhase) * 0.006f);
            car.localRotation = Quaternion.Slerp(car.localRotation, rotation * Quaternion.Euler(
                Mathf.Sin(roadPhase) * 0.35f, Mathf.Clamp(laneVelocity * 0.65f, -10, 10),
                Mathf.Clamp(-laneVelocity * 0.25f, -3, 3)), response);
            float pop = Mathf.Sin(Mathf.Clamp01(age / 0.35f) * Mathf.PI) * 0.035f;
            car.localScale = scale * (1 + pop);
            steering = Mathf.Lerp(steering, Mathf.Clamp(laneVelocity * 2, -22, 22), response);
            spin = Mathf.Repeat(spin + speed * dt / Mathf.Max(0.01f, 0.4f * scale.y) * Mathf.Rad2Deg, 360);
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] != null) wheels[i].localRotation = wheelHomes[i]
                    * Quaternion.Euler(0, i < 2 ? steering : 0, 0) * Quaternion.AngleAxis(spin, Vector3.right);
        }

        public void ResetPresentation()
        {
            EnsureHomes();
            driving = false; spin = steering = roadPhase = age = 0;
            if (car != null)
            {
                car.gameObject.SetActive(false); car.localPosition = home;
                car.localRotation = rotation; car.localScale = scale;
            }
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] != null) wheels[i].localRotation = wheelHomes != null ? wheelHomes[i] : Quaternion.identity;
        }

        private void OnDisable() => ResetPresentation();
    }
}
