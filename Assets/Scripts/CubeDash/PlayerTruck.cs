using UnityEngine;

namespace CubeDash
{
    /// <summary>Presentation of the saved truck model. Its wheels and suspension freeze with gameplay.</summary>
    public sealed class PlayerTruck : MonoBehaviour
    {
        [SerializeField] private Transform truck = null;
        [SerializeField] private Transform[] wheels = new Transform[0];
        [SerializeField] private Vector2 contactHalfSize = new Vector2(0.85f, 1.85f);
        private Vector3 home;
        private float age;
        private bool driving;
        public Transform Truck => truck;
        public Transform[] Wheels => wheels;
        public Vector2 ContactHalfSize => contactHalfSize;

        private void Awake() { if (truck != null) home = truck.localPosition; }

        public void SetDriving(bool value)
        {
            driving = value;
            age = 0;
            if (truck != null) truck.gameObject.SetActive(value);
        }

        public void Tick(float dt, float speed, float laneVelocity)
        {
            if (!driving || truck == null) return;
            age += dt;
            truck.localPosition = home + Vector3.up * (Mathf.Sin(age * 19) * 0.012f);
            truck.localRotation = Quaternion.Slerp(truck.localRotation,
                Quaternion.Euler(0, laneVelocity * 0.35f, Mathf.Clamp(-laneVelocity * 0.3f, -5, 5)),
                1 - Mathf.Exp(-10 * dt));
            foreach (Transform wheel in wheels)
                if (wheel != null) wheel.Rotate(Vector3.right, speed * dt / 0.34f * Mathf.Rad2Deg, Space.Self);
        }

        public void ResetPresentation()
        {
            driving = false;
            age = 0;
            if (truck != null)
            {
                truck.gameObject.SetActive(false);
                truck.localPosition = home;
                truck.localRotation = Quaternion.identity;
            }
            foreach (Transform wheel in wheels) if (wheel != null) wheel.localRotation = Quaternion.identity;
        }

        private void OnDisable() => ResetPresentation();
    }
}
