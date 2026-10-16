using System;
using UnityEngine;
using G = CubeDash.Editor.CountrySideMeshes.Geometry;

namespace CubeDash.Editor
{
    /// <summary>Original faceted sandstone and solid desert plants, baked only in the Editor.</summary>
    internal static class DesertSceneryModels
    {
        internal const int Slots = 9;
        internal static readonly string[] Names = { "Layered Mesa", "Eroded Butte", "Sandstone Spire", "Red Boulder", "Split Boulder", "Sandstone Chips", "Saguaro", "Young Saguaro", "Flowering Prickly Pear", "Barrel Cactus", "Desert Bunchgrass", "Yucca" };
        internal static Mesh Build(string name)
        {
            var g = new G(Slots);
            switch (name)
            {
                case "Layered Mesa": Cliff(g, 21, 24, 15, 17); break;
                case "Eroded Butte": Cliff(g, 14, 30, 12, 53); break;
                case "Sandstone Spire": Cliff(g, 4.4f, 23, 4.7f, 91); break;
                case "Red Boulder": Rock(g, Vector3.zero, new Vector3(2.2f, 1.7f, 1.6f), 23); break;
                case "Split Boulder":
                    Rock(g, new Vector3(-0.7f, 0, 0), new Vector3(1.6f, 2.1f, 1.4f), 67);
                    Rock(g, new Vector3(1.2f, -0.1f, 0.3f), new Vector3(0.85f, 1.2f, 1.1f), 29); break;
                case "Sandstone Chips":
                    for (int i = 0; i < 4; i++) Rock(g, new Vector3((i % 2 - 0.5f) * 1.4f, 0, (i / 2 - 0.5f) * 0.95f), new Vector3(0.45f + i * 0.08f, 0.32f, 0.37f), 31 + i * 17); break;
                case "Saguaro": Saguaro(g, false); break;
                case "Young Saguaro": Saguaro(g, true); break;
                case "Flowering Prickly Pear": Pear(g); break;
                case "Barrel Cactus": Barrel(g); break;
                case "Desert Bunchgrass": Grass(g, false); break;
                case "Yucca": Grass(g, true); break;
                default: throw new ArgumentOutOfRangeException(nameof(name));
            }
            return g.Finish(name);
        }

        private static void Cliff(G g, float width, float height, float depth, int seed)
        {
            const int sides = 24;
            // Broad sloping talus, steep eroded faces, recessed seams and projecting caprock.
            float[] levels = { -0.15f, 0.03f, 0.14f, 0.16f, 0.18f, 0.31f, 0.33f, 0.35f, 0.47f, 0.49f, 0.51f, 0.64f, 0.66f, 0.68f, 0.80f, 0.82f, 0.84f, 0.97f, 0.985f, 1 };
            float[] radius = { 1.12f, 1.04f, 0.96f, 0.975f, 0.89f, 0.85f, 0.865f, 0.80f, 0.77f, 0.785f, 0.70f, 0.67f, 0.685f, 0.60f, 0.57f, 0.585f, 0.53f, 0.49f, 0.505f, 0.48f };
            Vector3 Point(int ring, int side)
            {
                float a = side * Mathf.PI * 2 / sides;
                float ripple = 1 + 0.085f * Mathf.Sin(a * 7 + seed) + 0.045f * Mathf.Cos(a * 11 + seed * 0.3f)
                    + 0.032f * Mathf.Sin(a * 5 + ring / 3 * 1.7f + seed);
                float x = Mathf.Sign(Mathf.Cos(a)) * Mathf.Pow(Mathf.Abs(Mathf.Cos(a)), 0.65f);
                float z = Mathf.Sign(Mathf.Sin(a)) * Mathf.Pow(Mathf.Abs(Mathf.Sin(a)), 0.65f);
                float drift = levels[ring] * 0.8f;
                return new Vector3(x * width * radius[ring] * ripple + drift,
                    height * levels[ring] + Mathf.Sin(a * 5 + seed) * 0.40f + Mathf.Sin(a * 9 + ring / 3) * 0.12f, z * depth * radius[ring] * ripple);
            }
            for (int ring = 0; ring < levels.Length - 1; ring++) for (int side = 0; side < sides; side++)
            {
                int slot = ring >= 2 && ring % 3 == 2 ? 2 : ring >= 3 && ring % 3 == 0 ? 3 : 1;
                float shade = 0.76f + 0.24f * Mathf.PerlinNoise(side * 0.9f + seed, ring * 0.9f);
                // Alternating split faces create broad facets and vertical erosion, not stacked cylinders.
                Vector3 a = Point(ring, side), b = Point(ring, side + 1), c = Point(ring + 1, side + 1), d = Point(ring + 1, side);
                Vector3 outward = new Vector3(a.x, 0, a.z);
                Color tint = new Color(shade, shade * (0.96f + 0.04f * Mathf.Sin(side + ring)), shade * 0.96f);
                g.Triangle(a, b, (ring + side) % 2 == 0 ? d : c, slot, outward, tint);
                if ((ring + side) % 2 == 0) g.Triangle(b, c, d, slot, outward, tint * 0.97f);
                else g.Triangle(a, c, d, slot, outward, tint * 0.97f);
            }
            for (int side = 0; side < sides; side++)
            {
                g.Triangle(new Vector3(0.8f, height, 0), Point(levels.Length - 1, side), Point(levels.Length - 1, side + 1), 2, Vector3.up);
                g.Triangle(new Vector3(0, -height * 0.15f, 0), Point(0, side), Point(0, side + 1), 3, Vector3.down);
            }
        }

        private static void Rock(G g, Vector3 center, Vector3 size, int seed)
        {
            const int sides = 9;
            Vector3 P(int ring, int i)
            {
                float a = i * Mathf.PI * 2 / sides + (ring == 2 ? 0.19f : 0);
                float r = ring == 0 ? 0.82f : ring == 1 ? 1 : 0.56f;
                float rough = 0.76f + 0.40f * Mathf.PerlinNoise(Mathf.Cos(a) * 3 + seed + ring * 0.7f, Mathf.Sin(a) * 3 + seed);
                float y = ring == 0 ? -0.16f : ring == 1 ? 0.30f + 0.19f * Mathf.Sin(a * 2 + seed) + 0.09f * Mathf.Cos(a * 3 + seed * 0.7f) : 0.82f + 0.16f * Mathf.Sin(a * 3 + seed);
                return center + Vector3.Scale(size, new Vector3(Mathf.Cos(a) * r * rough + ring * 0.07f, y, Mathf.Sin(a) * r * rough));
            }
            for (int i = 0; i < sides; i++)
            {
                for (int ring = 0; ring < 2; ring++)
                {
                    float value = 0.82f + 0.17f * Mathf.PerlinNoise(i + seed, ring * 2);
                    var tint = new Color(value, value, value);
                    Vector3 normal = P(ring, i) - center; normal.y = 0;
                    g.Triangle(P(ring, i), P(ring, i + 1), P(ring + 1, i), 1, normal, tint);
                    g.Triangle(P(ring, i + 1), P(ring + 1, i + 1), P(ring + 1, i), 1, normal, tint * 1.04f);
                }
                g.Triangle(center + Vector3.Scale(size, new Vector3(-0.10f, 0.98f, 0.13f)), P(2, i), P(2, i + 1), 1, Vector3.up, new Color(1, 0.94f, 0.87f));
                g.Triangle(center - Vector3.up * size.y * 0.16f, P(0, i), P(0, i + 1), 1, Vector3.down);
            }
        }

        private static void Saguaro(G g, bool young)
        {
            float height = young ? 4.6f : 7.8f, r = young ? 0.43f : 0.63f;
            Stem(g, new[] { new Vector3(0, -0.25f, 0), new Vector3(0.06f, height * 0.45f, 0), new Vector3(0.12f, height - r, 0) }, r);
            if (!young) Stem(g, new[] { new Vector3(-0.22f, 2.8f, 0), new Vector3(-1.30f, 2.8f, 0), new Vector3(-1.85f, 3.05f, 0), new Vector3(-2.02f, 3.6f, 0), new Vector3(-2.02f, 5.15f, 0) }, 0.39f);
            Stem(g, new[] { new Vector3(0.22f, height * 0.37f, 0.05f), new Vector3(1.20f, height * 0.37f, 0.05f), new Vector3(1.65f, height * 0.40f, 0.05f), new Vector3(1.80f, height * 0.49f, 0.05f), new Vector3(1.80f, height * 0.78f, 0.05f) }, r * 0.62f);
        }

        private static void Stem(G g, Vector3[] path, float radius)
        {
            const int sides = 32;
            Vector3 Ring(int ring, int side)
            {
                Vector3 tangent = ring == 0 ? path[1] - path[0] : ring == path.Length - 1 ? Vector3.up : path[ring + 1] - path[ring - 1];
                Quaternion q = Quaternion.FromToRotation(Vector3.up, tangent.normalized);
                float a = side * Mathf.PI * 2 / sides;
                float rib = 0.95f + 0.05f * Mathf.Cos(a * 8);
                float taper = Mathf.Lerp(1, 0.86f, ring / (float)(path.Length - 1));
                return path[ring] + q * new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (radius * rib * taper);
            }
            for (int ring = 0; ring < path.Length - 1; ring++) for (int side = 0; side < sides; side++)
            {
                float brightness = side % 4 == 0 ? 1.03f : side % 4 == 2 ? 0.90f : 0.97f;
                g.Quad(Ring(ring, side), Ring(ring, side + 1), Ring(ring + 1, side + 1), Ring(ring + 1, side), side % 4 == 0 ? 5 : 4,
                    Ring(ring, side) - path[ring], new Color(brightness, brightness, brightness));
                if (ring == 0) g.Triangle(path[0], Ring(0, side), Ring(0, side + 1), 4, -Vector3.up);
            }
            // A short closed dome follows the ribs instead of an uncapped cylinder end.
            Vector3 top = path[path.Length - 1];
            for (int side = 0; side < sides; side++)
            {
                Vector3 a = Ring(path.Length - 1, side), b = Ring(path.Length - 1, side + 1);
                Vector3 c = top + (b - top) * 0.65f + Vector3.up * radius * 0.63f;
                Vector3 d = top + (a - top) * 0.65f + Vector3.up * radius * 0.63f;
                g.Quad(a, b, c, d, side % 4 == 0 ? 5 : 4, a - top + Vector3.up * radius);
                g.Triangle(d, c, top + Vector3.up * radius * 0.88f, 5, Vector3.up);
            }
        }

        private static void Ellipsoid(G g, Vector3 p, Vector3 size, Quaternion q, int slot, bool ribbed = false)
        {
            const int sides = 16, rings = 8;
            Vector3 P(int r, int s)
            {
                float a = s * Mathf.PI * 2 / sides, t = -Mathf.PI * 0.5f + r * Mathf.PI / rings;
                float ribs = ribbed ? 0.91f + 0.09f * Mathf.Cos(a * 8) : 1;
                return p + q * Vector3.Scale(size, new Vector3(Mathf.Cos(a) * Mathf.Cos(t) * ribs, Mathf.Sin(t), Mathf.Sin(a) * Mathf.Cos(t) * ribs));
            }
            for (int r = 0; r < rings; r++) for (int s = 0; s < sides; s++)
            {
                Vector3 normal = q * new Vector3(Mathf.Cos(s * Mathf.PI * 2 / sides), (r - 3.5f) * 0.24f, Mathf.Sin(s * Mathf.PI * 2 / sides));
                Color tint = Color.Lerp(new Color(0.73f, 0.79f, 0.65f), Color.white, r / 7f);
                if (r == 0) g.Triangle(P(r, s), P(r + 1, s), P(r + 1, s + 1), slot, normal, tint);
                else if (r == rings - 1) g.Triangle(P(r, s), P(r + 1, s), P(r, s + 1), slot, normal, tint);
                else g.Quad(P(r, s), P(r + 1, s), P(r + 1, s + 1), P(r, s + 1), slot, normal, tint);
            }
        }

        private static void Pear(G g)
        {
            Vector3[] pads = { new Vector3(0, 0.62f, 0), new Vector3(0.55f, 1.06f, 0.04f), new Vector3(-0.55f, 1.02f, 0.08f),
                new Vector3(1.03f, 1.77f, 0.09f), new Vector3(-1.04f, 1.68f, 0.10f), new Vector3(0.14f, 1.99f, -0.08f), new Vector3(-1.40f, 2.43f, 0.06f) };
            for (int pad = 0; pad < 7; pad++)
            {
                Vector3 p = pads[pad];
                Quaternion q = Quaternion.Euler(0, (pad % 3 - 1) * 16, -p.x * 22);
                Ellipsoid(g, p, new Vector3(0.55f, 0.80f, 0.22f), q, pad % 3 == 0 ? 5 : 4);
                // Small areoles on both pad faces; no transparent cards or floating spikes.
                foreach (float sign in new[] { -1f, 1f }) for (int dot = 0; dot < 7; dot++)
                {
                    float a = dot * 2.39996f, rr = dot == 0 ? 0 : 0.66f;
                    Vector3 local = new Vector3(Mathf.Cos(a) * 0.55f * rr, Mathf.Sin(a) * 0.8f * rr, sign * (dot == 0 ? 0.224f : 0.168f));
                    Vector3 at = p + q * local;
                    g.Triangle(at + q * Vector3.up * 0.035f, at + q * new Vector3(-0.025f, -0.02f, 0), at + q * new Vector3(0.025f, -0.02f, 0), 8, q * Vector3.forward * sign);
                }
                if (pad > 2)
                {
                    Vector3 flower = p + q * new Vector3(0, 0.77f, 0);
                    Ellipsoid(g, flower, new Vector3(0.10f, 0.19f, 0.10f), q, 7);
                    for (int petal = 0; petal < 5; petal++)
                    {
                        float a = petal * Mathf.PI * 2 / 5;
                        g.Triangle(flower + Vector3.up * 0.17f, flower + new Vector3(Mathf.Cos(a), 0.7f, Mathf.Sin(a)) * 0.22f,
                            flower + new Vector3(Mathf.Cos(a + 0.8f), 0.7f, Mathf.Sin(a + 0.8f)) * 0.22f, 7, Vector3.up);
                    }
                }
            }
        }

        private static void Barrel(G g)
        {
            Vector3 center = new Vector3(0, 0.65f, 0);
            Ellipsoid(g, center, new Vector3(0.68f, 0.95f, 0.68f), Quaternion.identity, 4, true);
            for (int row = 0; row < 4; row++) for (int rib = 0; rib < 8; rib++)
            {
                float a = rib * Mathf.PI / 4, t = -0.55f + row * 0.40f;
                Vector3 normal = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 side = Vector3.Cross(Vector3.up, normal);
                Vector3 p = center + normal * (Mathf.Cos(t) * 0.685f) + Vector3.up * (Mathf.Sin(t) * 0.95f);
                g.Triangle(p - side * 0.025f, p + side * 0.025f, p + normal * 0.045f + Vector3.up * 0.06f, 8, normal);
            }
            Ellipsoid(g, new Vector3(0, 1.58f, 0), new Vector3(0.13f, 0.08f, 0.13f), Quaternion.identity, 8);
        }

        private static void Grass(G g, bool yucca)
        {
            var random = new System.Random(yucca ? 71 : 38);
            int count = yucca ? 22 : 34;
            for (int blade = 0; blade < count; blade++)
            {
                float a = blade * 2.39996f;
                Vector3 direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), side = Vector3.Cross(Vector3.up, direction);
                float length = (yucca ? 1.7f : 1.45f) * (0.50f + (float)random.NextDouble() * 0.5f);
                float spread = (yucca ? 0.95f : 0.8f) * (0.3f + (float)random.NextDouble() * 0.7f);
                Vector3 root = direction * (yucca ? 0.04f : 0.16f), mid = root + direction * spread * 0.43f + Vector3.up * length * 0.64f;
                Vector3 tip = root + direction * spread + Vector3.up * length * (yucca ? 0.65f : 0.80f);
                float w = yucca ? 0.11f : 0.045f;
                int slot = yucca ? 5 : 6;
                Color bottom = new Color(0.55f, 0.49f, 0.28f), top = Color.white;
                // Folded triangular blades, with modeled undersides for real shadows.
                foreach (float sign in new[] { -1f, 1f })
                {
                    Vector3 outward = direction * sign;
                    g.Quad(root - side * w, root + side * w, mid + side * w * 0.55f, mid - direction * 0.035f, slot, outward, bottom);
                    g.Triangle(mid - direction * 0.035f, mid + side * w * 0.55f, tip, slot, outward, top);
                    g.Triangle(root - side * w, mid - direction * 0.035f, mid - side * w * 0.55f, slot, outward, bottom);
                    g.Triangle(mid - side * w * 0.55f, mid - direction * 0.035f, tip, slot, outward, top);
                }
            }
        }
    }
}
