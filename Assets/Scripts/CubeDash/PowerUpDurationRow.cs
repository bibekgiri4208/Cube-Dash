using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>A saved, non-interactive power-up card. Its meter only reflects gameplay state.</summary>
    public sealed class PowerUpDurationRow : MonoBehaviour
    {
        public enum Effect { Shield, DoublePoints, Flight, Truck, Magnet, TimedShield, Supercar }
        [SerializeField] private Effect effect;
        [SerializeField] private Text remainingLabel;
        [SerializeField] private Slider durationMeter;
        private int shownSeconds = -1;
        private string shownCharge;
        public Effect Kind => effect;
        public Text RemainingLabel => remainingLabel;
        public Slider DurationMeter => durationMeter;
        public RectTransform Rect => (RectTransform)transform;

        public bool Refresh(CubeDashGame game, bool visible)
        {
            bool active = false;
            float remaining = 0, total = 0;
            string charge = null;
            switch (effect)
            {
                case Effect.Shield: active = game.Shields > 0; charge = "1 HIT"; break;
                case Effect.DoublePoints: remaining = game.DoublePointsRemaining; total = game.DoublePointsDuration; break;
                case Effect.Flight: remaining = game.FlightRemaining; total = game.FlightDuration; break;
                case Effect.Truck: remaining = game.TruckRemaining; total = game.TruckDuration; break;
                case Effect.Magnet: remaining = game.MagnetRemaining; total = game.MagnetDuration; break;
                case Effect.TimedShield: remaining = game.LandingShieldRemaining; total = game.TimedShieldDuration; break;
                // Retain the saved enum ID, but cosmetic skins are not power-ups or HUD indicators.
                case Effect.Supercar: break;
            }
            active = visible && (active || remaining > 0);
            if (gameObject.activeSelf != active) gameObject.SetActive(active);
            if (!active) return false;
            int seconds = Mathf.CeilToInt(remaining);
            if (remainingLabel != null && (shownSeconds != seconds || shownCharge != charge))
            {
                remainingLabel.text = charge ?? seconds + "s";
                shownSeconds = seconds;
                shownCharge = charge;
            }
            if (durationMeter != null) durationMeter.SetValueWithoutNotify(charge != null ? 1 : Mathf.Clamp01(remaining / Mathf.Max(0.001f, total)));
            return true;
        }
    }
}
