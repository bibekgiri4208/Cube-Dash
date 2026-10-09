using UnityEngine;

namespace CubeDash
{
    public enum EnvironmentBiome { City, Jungle, Mountains, Desert, Beach }

    /// <summary>Distance-based scenery rules, independent of the obstacle random stream.</summary>
    public static class BiomeRules
    {
        public const int Count = 5;

        public static float Length(int segmentsPerBiome) => RunnerRules.SegmentLength * Mathf.Max(2, segmentsPerBiome);

        public static EnvironmentBiome AtDistance(float distance, int segmentsPerBiome)
        {
            return (EnvironmentBiome)(Mathf.FloorToInt(Mathf.Max(0, distance) / Length(segmentsPerBiome)) % Count);
        }

        public static int Variation(int sectionIndex, int count)
        {
            if (count <= 0) return 0;
            // Stable, varied scenery without consuming any gameplay randomness.
            uint hash = unchecked((uint)sectionIndex * 747796405u + 2891336453u);
            hash = ((hash >> (int)((hash >> 28) + 4)) ^ hash) * 277803737u;
            return (int)(((hash >> 22) ^ hash) % (uint)count);
        }
    }
}
