using UnityEngine;

namespace CubeDash
{
    // Persistent IDs: append new styles, never reorder existing ones.
    public enum PlayerTrail { None = 0, NeonAurora = 1, SolarFlare = 2, FrostWave = 3, RainbowRush = 4 }

    public static class TrailStyles
    {
        public static bool Valid(PlayerTrail trail) => trail >= PlayerTrail.NeonAurora && trail <= PlayerTrail.RainbowRush;
        public static string Name(PlayerTrail trail) => trail == PlayerTrail.NeonAurora ? "NEON AURORA"
            : trail == PlayerTrail.SolarFlare ? "SOLAR FLARE" : trail == PlayerTrail.FrostWave ? "FROST WAVE"
            : trail == PlayerTrail.RainbowRush ? "RAINBOW RUSH" : "DEFAULT TRAIL";
        public static Color Primary(PlayerTrail trail) => trail == PlayerTrail.SolarFlare ? new Color(1, 0.38f, 0.035f)
            : trail == PlayerTrail.FrostWave ? new Color(0.18f, 0.64f, 1) : trail == PlayerTrail.RainbowRush
            ? new Color(1, 0.15f, 0.58f) : new Color(0.04f, 1, 0.82f);
        public static Color Secondary(PlayerTrail trail) => trail == PlayerTrail.SolarFlare ? new Color(1, 0.88f, 0.16f)
            : trail == PlayerTrail.FrostWave ? new Color(0.82f, 0.98f, 1) : trail == PlayerTrail.RainbowRush
            ? new Color(0.35f, 0.3f, 1) : new Color(0.62f, 0.2f, 1);
    }
}
