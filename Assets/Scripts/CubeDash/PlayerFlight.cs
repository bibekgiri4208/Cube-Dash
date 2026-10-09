using UnityEngine;

namespace CubeDash
{
    /// <summary>Animates the saved fighter and bounded exhaust systems; never creates a model during a run.</summary>
    public sealed class PlayerFlight : MonoBehaviour
    {
        [SerializeField] private Transform fighter = null;
        [SerializeField] private ParticleSystem[] exhaust = new ParticleSystem[0];
        [SerializeField] private Transform shield = null;
        private bool flying;
        private float age;
        public Transform Fighter => fighter;
        public ParticleSystem[] Exhaust => exhaust;
        public bool ShieldVisible => shield != null && shield.gameObject.activeSelf;

        public void SetFlying(bool value)
        {
            if (flying == value) return;
            flying = value;
            age = 0;
            if (fighter != null) fighter.gameObject.SetActive(value);
            foreach (ParticleSystem system in exhaust)
            {
                if (system == null) continue;
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (value) { system.Play(false); system.Pause(false); }
            }
            if (shield != null) shield.gameObject.SetActive(false);
        }

        public void Tick(float dt, float laneVelocity, bool protectedLanding)
        {
            age += dt;
            if (flying && fighter != null)
            {
                fighter.localRotation = Quaternion.Slerp(fighter.localRotation,
                    Quaternion.Euler(-Mathf.Exp(-age * 4) * 10, laneVelocity * 0.2f,
                        Mathf.Clamp(-laneVelocity * 1.1f, -22, 22)), 1 - Mathf.Exp(-12 * dt));
                foreach (ParticleSystem system in exhaust)
                    if (system != null) system.Simulate(dt, false, false, false);
            }
            if (shield != null)
            {
                shield.gameObject.SetActive(!flying && protectedLanding);
                shield.localScale = Vector3.one * (1.65f + Mathf.Sin(age * 6) * 0.035f);
            }
        }

        public void ResetPresentation()
        {
            flying = false;
            age = 0;
            if (fighter != null) { fighter.gameObject.SetActive(false); fighter.localRotation = Quaternion.identity; }
            foreach (ParticleSystem system in exhaust)
                if (system != null) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (shield != null) shield.gameObject.SetActive(false);
        }

        private void OnDisable() => ResetPresentation();
    }
}
