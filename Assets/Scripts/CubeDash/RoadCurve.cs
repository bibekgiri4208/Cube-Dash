using UnityEngine;

namespace CubeDash
{
    /// <summary>Arcade render-space turns. The nearest six metres and contact space stay unbent.</summary>
    public static class RoadCurve
    {
        public const float MaximumLookAhead = 180;
        public static float Phase(int seed) => (uint)seed % 6283 * 0.001f;
        private static void Sample(float distance, float phase, out float position, out float slope)
        {
            float t = Mathf.Clamp01((distance - 60) / 180);
            float envelope = t * t * (3 - 2 * t);
            float a = distance * 0.012f + phase, b = distance * 0.021f + phase * 1.7f;
            float path = 22 * Mathf.Sin(a) + 10 * Mathf.Sin(b);
            position = envelope * path;
            slope = envelope * (0.264f * Mathf.Cos(a) + 0.21f * Mathf.Cos(b))
                + t * (1 - t) / 30 * path;
        }
        public static float Offset(float z, float distance, float phase, float strength = 1)
        {
            float ahead = Mathf.Max(0, z - 6);
            if (ahead == 0) return 0;
            float sample = Mathf.Min(MaximumLookAhead, ahead);
            Sample(distance, phase, out float origin, out float slope);
            Sample(distance + sample, phase, out float path, out float endSlope);
            return (path - origin - slope * sample + (endSlope - slope) * (ahead - sample)) * strength;
        }
        public static void Apply(float distance, float phase, float strength, float originZ)
        {
            Sample(distance, phase, out float path, out float slope);
            Shader.SetGlobalVector("_CubeDashRoadCurve", new Vector4(distance, phase, strength, originZ));
            Shader.SetGlobalVector("_CubeDashRoadOrigin", new Vector4(path, slope, 0, 0));
        }
        public static void Clear()
        {
            Shader.SetGlobalVector("_CubeDashRoadCurve", Vector4.zero);
            Shader.SetGlobalVector("_CubeDashRoadOrigin", Vector4.zero);
        }
    }
}
