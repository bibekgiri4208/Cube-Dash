using UnityEngine;

namespace CubeDash
{
    /// <summary>Saved cosmetic vehicles, with rolling wheels, steering and restrained chassis motion.</summary>
    public sealed class PlayerSupercar : MonoBehaviour
    {
        [SerializeField] private Transform car = null;
        [Tooltip("Left front, right front, left rear, right rear.")]
        [SerializeField] private Transform[] wheels = new Transform[0];
        [System.Serializable]
        public sealed class VehicleModel
        {
            public PlayerSkin skin;
            public Transform root;
            public Transform[] wheels = new Transform[0];
            public float wheelRadius = 0.6f;
        }
        [SerializeField] private VehicleModel[] additionalSkins = new VehicleModel[0];
        private Transform activeCar;
        private Transform[] activeWheels;
        private PlayerSkin selectedSkin = PlayerSkin.Supercar;
        private float wheelRadius = 0.4f;
        private Vector3 home, scale;
        private Quaternion rotation;
        private Quaternion[] wheelHomes;
        private float spin, steering, roadPhase, age;
        private bool driving;
        private bool initialized;
        private SupercarLightTrail lightTrail;
        public Transform Car => activeCar != null ? activeCar : car;
        public Transform[] Wheels => activeWheels ?? wheels;
        public VehicleModel[] AdditionalSkins => additionalSkins;
        public bool Driving => driving;
        public SupercarLightTrail LightTrail { get { EnsureHomes(); return lightTrail; } }

        private void Awake() => EnsureHomes();

        private void EnsureHomes()
        {
            if (initialized) return;
            initialized = true;
            if (Car != null) { home = Car.localPosition; scale = Car.localScale; rotation = Car.localRotation; }
            lightTrail = Car != null ? Car.GetComponentInChildren<SupercarLightTrail>(true) : null;
            wheelHomes = new Quaternion[Wheels.Length];
            for (int i = 0; i < Wheels.Length; i++) wheelHomes[i] = Wheels[i] != null ? Wheels[i].localRotation : Quaternion.identity;
        }

        public void SelectSkin(PlayerSkin skin)
        {
            if (skin == PlayerSkin.Cube) skin = PlayerSkin.Supercar;
            if (selectedSkin == skin) return;
            ResetPresentation();
            selectedSkin = skin; activeCar = car; activeWheels = wheels; wheelRadius = 0.4f;
            foreach (var model in additionalSkins)
                if (model.skin == skin && model.root != null)
                { activeCar = model.root; activeWheels = model.wheels; wheelRadius = model.wheelRadius; break; }
            initialized = false;
            EnsureHomes();
        }

        public void SetDriving(bool value)
        {
            EnsureHomes();
            if (driving == value) return;
            if (!value) { ResetPresentation(); return; }
            driving = true; age = 0;
            if (Car != null) Car.gameObject.SetActive(true);
        }

        public void Tick(float dt, float speed, float laneVelocity)
        {
            if (!driving || Car == null || dt <= 0) return;
            age += dt;
            float response = 1 - Mathf.Exp(-12 * dt);
            roadPhase = Mathf.Repeat(roadPhase + speed * dt * 1.8f, Mathf.PI * 2);
            Car.localPosition = home + Vector3.up * (0.006f + Mathf.Sin(roadPhase) * 0.006f);
            Car.localRotation = Quaternion.Slerp(Car.localRotation, rotation * Quaternion.Euler(
                Mathf.Sin(roadPhase) * 0.35f, Mathf.Clamp(laneVelocity * 0.65f, -10, 10),
                Mathf.Clamp(-laneVelocity * 0.25f, -3, 3)), response);
            float pop = Mathf.Sin(Mathf.Clamp01(age / 0.35f) * Mathf.PI) * 0.035f;
            Car.localScale = scale * (1 + pop);
            steering = Mathf.Lerp(steering, Mathf.Clamp(laneVelocity * 2, -22, 22), response);
            spin = Mathf.Repeat(spin + speed * dt / Mathf.Max(0.01f, wheelRadius * scale.y) * Mathf.Rad2Deg, 360);
            for (int i = 0; i < Wheels.Length; i++)
                if (Wheels[i] != null) Wheels[i].localRotation = wheelHomes[i]
                    * Quaternion.Euler(0, i < 2 ? steering : 0, 0) * Quaternion.AngleAxis(spin, Vector3.right);
        }

        public void ResetPresentation()
        {
            EnsureHomes();
            driving = false; spin = steering = roadPhase = age = 0;
            if (lightTrail != null) lightTrail.ResetTrail();
            if (Car != null)
            {
                Car.gameObject.SetActive(false); Car.localPosition = home;
                Car.localRotation = rotation; Car.localScale = scale;
            }
            for (int i = 0; i < Wheels.Length; i++)
                if (Wheels[i] != null) Wheels[i].localRotation = wheelHomes != null ? wheelHomes[i] : Quaternion.identity;
        }

        private void OnDisable() => ResetPresentation();
    }
}
