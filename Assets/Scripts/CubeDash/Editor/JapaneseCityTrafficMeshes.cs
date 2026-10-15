using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CubeDash.Editor
{
    public static partial class HighwayHazardArtBuilder
    {
        /// <summary>Original low-poly 1980s Japanese liftback: wedge nose, black lower trim, sloped hatch and pop-up lamps.</summary>
        public static Mesh BuildCityJdmCoupe(bool raisedLights)
        {
            var g = new Geometry();
            const float front = -2.20f, rear = 2.20f, halfWidth = 0.75f;
            float[] z = { front, -1.98f, -0.80f, -0.36f, 0.52f, 1.65f, rear };
            float[] top = { 0.59f, 0.71f, 0.79f, 1.29f, 1.33f, 0.83f, 0.73f };
            float Top(float distance) => Sample(z, top, distance);
            float Width(float distance) => distance < -1.98f ? Mathf.Lerp(0.68f, halfWidth, Mathf.InverseLerp(front, -1.98f, distance)) :
                distance > 1.95f ? Mathf.Lerp(halfWidth, 0.70f, Mathf.InverseLerp(1.95f, rear, distance)) : halfWidth;
            float Low(float distance)
            {
                foreach (float axle in new[] { -1.42f, 1.32f })
                    if (Mathf.Abs(distance - axle) < 0.325f) return 0.30f + Mathf.Sqrt(Mathf.Max(0, 0.325f * 0.325f - (distance - axle) * (distance - axle)));
                return 0.30f;
            }
            Vector3[] Ring(float distance)
            {
                float w = Width(distance), height = Top(distance), belt = Mathf.Min(height - 0.045f, 0.77f);
                return new[] { new Vector3(-w, Low(distance), distance), new Vector3(-w, belt, distance),
                    new Vector3(-w * 0.82f, height, distance), new Vector3(w * 0.82f, height, distance),
                    new Vector3(w, belt, distance), new Vector3(w, Low(distance), distance) };
            }
            var spaced = new List<float>();
            foreach (float row in z.Concat(Enumerable.Range(0, 57).Select(i => Mathf.Lerp(front, rear, i / 56f))).OrderBy(v => v))
                if (spaced.Count == 0 || row - spaced[spaced.Count - 1] > 0.001f) spaced.Add(row);
            float[] rows = spaced.ToArray();
            for (int row = 0; row < rows.Length - 1; row++)
            {
                Vector3[] a = Ring(rows[row]), b = Ring(rows[row + 1]);
                for (int face = 0; face < 6; face++)
                {
                    int next = (face + 1) % 6;
                    Vector3 hint = (a[face] + a[next]) * 0.5f - new Vector3(0, 0.62f, rows[row]);
                    g.Quad(a[face], b[face], b[next], a[next], face == 5 ? Trim : Paint, hint);
                }
            }
            g.Cap(Ring(front), Paint, Vector3.back); g.Cap(Ring(rear), Paint, Vector3.forward);
            g.Box(new Vector3(0, 0.235f, 0), new Vector3(1.20f, 0.13f, 4.1f), Trim, 0.025f);
            void RoofGlass(float from, float to, float width, Vector3 hint)
            {
                Vector3 A(float x, float distance, float lift) => new Vector3(x, Top(distance) + lift, distance);
                g.Quad(A(-width, from, 0.008f), A(width, from, 0.008f), A(width * 0.96f, to, 0.008f), A(-width * 0.96f, to, 0.008f), Trim, hint);
                g.Quad(A(-width + 0.035f, from + 0.025f, 0.014f), A(width - 0.035f, from + 0.025f, 0.014f),
                    A(width * 0.96f - 0.035f, to - 0.025f, 0.014f), A(-width * 0.96f + 0.035f, to - 0.025f, 0.014f), Glass, hint);
            }
            RoofGlass(-0.755f, -0.385f, 0.59f, new Vector3(0, 1, -1));
            RoofGlass(0.61f, 1.62f, 0.58f, new Vector3(0, 1, 1));
            g.Beam(new Vector3(-0.45f, 0.845f, -0.72f), new Vector3(0.04f, 0.89f, -0.68f), 0.012f, Trim);
            g.Beam(new Vector3(-0.11f, 1.14f, 0.99f), new Vector3(0.22f, 1.04f, 1.22f), 0.012f, Trim);
            for (int side = -1; side <= 1; side += 2)
            {
                // Black panda waist and sill, with the colored body still prominent for match-color gameplay.
                g.Box(new Vector3(side * 0.752f, 0.43f, 0.01f), new Vector3(0.018f, 0.105f, 1.94f), Trim, 0.008f);
                g.Box(new Vector3(side * 0.752f, 0.70f, 0.01f), new Vector3(0.018f, 0.045f, 3.50f), Trim, 0.008f);
                Vector3 Side(float y, float distance, float lift = 0)
                {
                    float roof = Top(distance), fraction = Mathf.Clamp01((y - 0.77f) / Mathf.Max(0.01f, roof - 0.77f));
                    return new Vector3(side * (Mathf.Lerp(0.75f, 0.615f, fraction) + lift), y, distance);
                }
                void Pane(Vector3[] points)
                {
                    g.Quad(points[0], points[1], points[2], points[3], Trim, Vector3.right * side);
                    Vector3 center = points.Aggregate(Vector3.zero, (sum, v) => sum + v) / 4;
                    Vector3[] glass = points.Select(v => Vector3.Lerp(center, v, 0.88f) + Vector3.right * side * 0.005f).ToArray();
                    g.Quad(glass[0], glass[1], glass[2], glass[3], Glass, Vector3.right * side);
                }
                Pane(new[] { Side(0.80f, -0.68f, 0.008f), Side(0.80f, 0.37f, 0.008f), Side(1.265f, 0.37f, 0.008f), Side(1.245f, -0.34f, 0.008f) });
                Pane(new[] { Side(0.80f, 0.47f, 0.008f), Side(0.80f, 1.50f, 0.008f), Side(1.22f, 0.70f, 0.008f), Side(1.26f, 0.47f, 0.008f) });
                g.Beam(new Vector3(side * 0.76f, 0.50f, 0.40f), new Vector3(side * 0.76f, 0.77f, 0.40f), 0.009f, Trim);
                g.Box(new Vector3(side * 0.766f, 0.735f, 0.22f), new Vector3(0.025f, 0.027f, 0.16f), Trim, 0.005f);
                g.Beam(Side(0.86f, -0.53f, 0.01f), new Vector3(side * 0.79f, 0.88f, -0.58f), 0.028f, Trim);
                g.Box(new Vector3(side * 0.795f, 0.90f, -0.59f), new Vector3(0.095f, 0.10f, 0.17f), Paint, 0.02f);
                g.Box(new Vector3(side * 0.795f, 0.90f, -0.50f), new Vector3(0.065f, 0.065f, 0.012f), Glass, 0.004f);
                g.Box(new Vector3(side * 0.59f, 0.52f, front - 0.015f), new Vector3(0.15f, 0.08f, 0.027f), Amber, 0.005f);
                g.Box(new Vector3(side * 0.52f, 0.64f, rear + 0.008f), new Vector3(0.34f, 0.12f, 0.025f), Tail, 0.006f);
                g.Box(new Vector3(side * 0.70f, 0.64f, rear + 0.022f), new Vector3(0.06f, 0.10f, 0.022f), Amber, 0.004f);
                if (raisedLights)
                {
                    g.Box(new Vector3(side * 0.46f, 0.80f, -1.83f), new Vector3(0.37f, 0.21f, 0.25f), Paint, 0.012f, Quaternion.Euler(-12, 0, 0));
                    g.Box(new Vector3(side * 0.46f, 0.795f, -1.965f), new Vector3(0.30f, 0.115f, 0.016f), Lamp, 0.006f);
                }
                else g.Box(new Vector3(side * 0.46f, Top(-1.83f) + 0.008f, -1.83f), new Vector3(0.36f, 0.013f, 0.30f), Paint, 0.007f);
                CityWheel(g, side, -1.42f, false); CityWheel(g, side, 1.32f, false);
            }
            g.Box(new Vector3(0, 0.42f, front - 0.02f), new Vector3(1.48f, 0.14f, 0.09f), Trim, 0.012f);
            g.Box(new Vector3(0, 0.58f, front - 0.008f), new Vector3(0.72f, 0.12f, 0.018f), Trim, 0.007f);
            for (int bar = 0; bar < 3; bar++) g.Box(new Vector3(0, 0.545f + bar * 0.033f, front - 0.02f), new Vector3(0.67f, 0.009f, 0.014f), Steel, 0);
            g.Box(new Vector3(0, 0.42f, rear + 0.015f), new Vector3(1.46f, 0.13f, 0.07f), Trim, 0.012f);
            // A short black ducktail, separate exhaust and small Japanese-style plate give readable rear detail.
            g.Box(new Vector3(0, 0.79f, 1.98f), new Vector3(1.42f, 0.055f, 0.22f), Trim, 0.012f);
            g.Cylinder(new Vector3(-0.49f, 0.245f, 2.15f), 0.045f, 0.18f, Steel, Quaternion.Euler(0, 90, 0), 12);
            CityPlate(g, new Vector3(0, 0.59f, rear + 0.03f), false);
            CityPlate(g, new Vector3(0, 0.43f, front - 0.072f), false, true);
            return g.Finish(raisedLights ? "Hachiroku Pop-up Coupe" : "Hachiroku Street Coupe");
        }

        /// <summary>Compact cab-over Japanese pickup, with real open cargo bed and small steel wheels.</summary>
        public static Mesh BuildCityKeiTruck(int style)
        {
            if (style < 0 || style > 2) throw new ArgumentOutOfRangeException(nameof(style));
            var g = new Geometry(); const float width = 0.68f;
            g.Box(new Vector3(0, 0.32f, 0), new Vector3(1.12f, 0.13f, 3.05f), Trim, 0.025f);
            g.Box(new Vector3(0, 0.64f, -0.94f), new Vector3(width * 2, 0.62f, 1.16f), Paint, 0.055f);
            // Windscreen and painted cab roof are separate from the open bed, not a shortened panel van.
            Vector3[] cab = { new Vector3(-width, 0.93f, -1.51f), new Vector3(width, 0.93f, -1.51f),
                new Vector3(width * 0.91f, 1.72f, -1.26f), new Vector3(-width * 0.91f, 1.72f, -1.26f) };
            g.Quad(cab[0], cab[1], cab[2], cab[3], Paint, new Vector3(0, 1, -1));
            Vector3 screenCenter = cab.Aggregate(Vector3.zero, (sum, v) => sum + v) / 4;
            Vector3[] seal = cab.Select(v => Vector3.Lerp(screenCenter, v, 0.87f) + new Vector3(0, 0.003f, -0.007f)).ToArray();
            g.Quad(seal[0], seal[1], seal[2], seal[3], Trim, new Vector3(0, 1, -1));
            Vector3[] screen = seal.Select(v => Vector3.Lerp(screenCenter, v, 0.90f) + Vector3.back * 0.008f).ToArray();
            g.Quad(screen[0], screen[1], screen[2], screen[3], Glass, new Vector3(0, 1, -1));
            g.Box(new Vector3(0, 1.735f, -0.89f), new Vector3(1.29f, 0.07f, 0.85f), Paint, 0.025f);
            g.Box(new Vector3(0, 1.28f, -0.46f), new Vector3(1.27f, 0.88f, 0.07f), Paint, 0.025f);
            g.Box(new Vector3(0, 1.37f, -0.413f), new Vector3(0.95f, 0.43f, 0.025f), Trim, 0.015f);
            g.Box(new Vector3(0, 1.37f, -0.394f), new Vector3(0.85f, 0.34f, 0.012f), Glass, 0.008f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * width;
                Vector3[] pane = { new Vector3(x, 0.96f, -1.45f), new Vector3(x, 0.96f, -0.50f),
                    new Vector3(side * 0.615f, 1.66f, -0.50f), new Vector3(side * 0.615f, 1.66f, -1.23f) };
                g.Quad(pane[0], pane[1], pane[2], pane[3], Paint, Vector3.right * side);
                Vector3 center = pane.Aggregate(Vector3.zero, (sum, p) => sum + p) / 4;
                Vector3[] glass = pane.Select(p => Vector3.Lerp(center, p, 0.81f) + Vector3.right * side * 0.008f).ToArray();
                g.Quad(glass[0], glass[1], glass[2], glass[3], Glass, Vector3.right * side);
                g.Box(new Vector3(side * 0.693f, 0.96f, -0.63f), new Vector3(0.022f, 0.034f, 0.15f), Trim, 0.008f);
                g.Beam(new Vector3(x, 1.11f, -1.37f), new Vector3(side * 0.74f, 1.15f, -1.43f), 0.024f, Trim);
                g.Box(new Vector3(side * 0.744f, 1.20f, -1.42f), new Vector3(0.07f, 0.18f, 0.12f), Trim, 0.017f);
                g.Box(new Vector3(side * 0.744f, 1.20f, -1.351f), new Vector3(0.045f, 0.14f, 0.009f), Glass, 0.004f);
                g.Box(new Vector3(side * 0.49f, 0.80f, -1.535f), new Vector3(0.28f, 0.17f, 0.035f), Lamp, 0.009f);
                g.Box(new Vector3(side * 0.63f, 0.77f, -1.55f), new Vector3(0.065f, 0.11f, 0.028f), Amber, 0.005f);
                // Ribbed drop-side bed, inner floor and actual empty cargo volume.
                g.Box(new Vector3(side * 0.655f, 0.83f, 0.64f), new Vector3(0.065f, 0.43f, 1.91f), Paint, 0.015f);
                for (int rib = 0; rib < 3; rib++) g.Box(new Vector3(side * 0.691f, 0.69f + rib * 0.12f, 0.64f), new Vector3(0.017f, 0.018f, 1.81f), Paint, 0.004f);
                foreach (float latch in new[] { -0.10f, 1.35f }) g.Box(new Vector3(side * 0.704f, 0.94f, latch), new Vector3(0.025f, 0.075f, 0.045f), Steel, 0.004f);
                g.Box(new Vector3(side * 0.49f, 0.49f, 1.61f), new Vector3(0.24f, 0.105f, 0.035f), Tail, 0.005f);
                g.Box(new Vector3(side * 0.65f, 0.49f, 1.61f), new Vector3(0.07f, 0.10f, 0.036f), Amber, 0.005f);
                foreach (float axle in new[] { -1.04f, 1.02f }) CityWheel(g, side, axle, true);
            }
            g.Box(new Vector3(0, 0.62f, 0.64f), new Vector3(1.27f, 0.065f, 1.94f), Paint, 0.014f);
            for (int rib = -4; rib <= 4; rib++) g.Box(new Vector3(rib * 0.135f, 0.659f, 0.64f), new Vector3(0.025f, 0.012f, 1.81f), Paint, 0.003f);
            g.Box(new Vector3(0, 0.83f, 1.59f), new Vector3(1.36f, 0.43f, 0.065f), Paint, 0.012f);
            g.Box(new Vector3(0, 0.45f, -1.54f), new Vector3(1.39f, 0.14f, 0.11f), Trim, 0.016f);
            g.Box(new Vector3(0, 0.39f, 1.60f), new Vector3(1.35f, 0.09f, 0.06f), Trim, 0.012f);
            g.Box(new Vector3(0, 0.77f, -1.547f), new Vector3(0.46f, 0.12f, 0.02f), Trim, 0.007f);
            for (int slat = 0; slat < 3; slat++) g.Box(new Vector3(0, 0.735f + slat * 0.033f, -1.563f), new Vector3(0.41f, 0.012f, 0.015f), Steel, 0);
            CityPlate(g, new Vector3(0, 0.48f, 1.64f), true);
            CityPlate(g, new Vector3(0, 0.46f, -1.604f), true, true);
            g.Beam(new Vector3(-0.43f, 1.00f, -1.50f), new Vector3(-0.08f, 1.07f, -1.48f), 0.012f, Trim);
            if (style == 1)
            {
                // Recessed stacked produce crates, kept below the cab so this remains recognisably a kei pickup.
                for (int row = 0; row < 2; row++)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 c = new Vector3(side * 0.30f, 0.84f, 0.10f + row * 0.72f);
                        g.Box(c, new Vector3(0.48f, 0.34f, 0.61f), Trim, 0.018f);
                        foreach (float h in new[] { -0.10f, 0.02f, 0.14f }) g.Box(c + new Vector3(0, h, -0.315f), new Vector3(0.48f, 0.035f, 0.025f), Paint, 0.003f);
                    }
            }
            if (style == 2)
            {
                foreach (float x in new[] { -0.55f, 0.55f })
                    g.Beam(new Vector3(x, 0.70f, -0.31f), new Vector3(x, 1.59f, -0.31f), 0.045f, Steel);
                for (int bar = 0; bar < 3; bar++) g.Box(new Vector3(0, 1.24f + bar * 0.16f, -0.31f), new Vector3(1.13f, 0.035f, 0.04f), Steel, 0.006f);
            }
            return g.Finish(style == 1 ? "Kei Produce Flatbed" : style == 2 ? "Kei Utility Flatbed" : "Kei Street Flatbed");
        }

        private static void CityWheel(Geometry g, int side, float axle, bool steelWheel)
        {
            // Existing 0.30 m tread geometry keeps all four tires grounded and reusable in the smash pool.
            g.Tire(new Vector3(side * 0.65f, 0.30f, axle));
            g.Cylinder(new Vector3(side * 0.755f, 0.30f, axle), steelWheel ? 0.16f : 0.19f, 0.025f, Steel, Quaternion.identity, 16);
            g.Cylinder(new Vector3(side * 0.773f, 0.30f, axle), 0.13f, 0.012f, Trim, Quaternion.identity, 16);
            Vector3 hub = new Vector3(side * 0.782f, 0.30f, axle);
            int spokes = steelWheel ? 6 : 8;
            for (int i = 0; i < spokes; i++)
            {
                float angle = i * Mathf.PI * 2 / spokes;
                if (steelWheel) g.Cylinder(hub + new Vector3(0, Mathf.Cos(angle), Mathf.Sin(angle)) * 0.10f, 0.025f, 0.008f, Steel, Quaternion.identity, 8);
                else g.Beam(hub, hub + new Vector3(0, Mathf.Cos(angle), Mathf.Sin(angle)) * 0.165f, 0.025f, Steel);
            }
            g.Cylinder(hub, 0.045f, 0.017f, Steel, Quaternion.identity, 12);
        }

        private static void CityPlate(Geometry g, Vector3 center, bool kei, bool front = false)
        {
            g.Box(center, new Vector3(0.28f, 0.105f, 0.012f), kei ? Amber : Lamp, 0.008f);
            float direction = front ? -1 : 1;
            for (int mark = 0; mark < 4; mark++)
                g.Box(center + new Vector3(-0.09f + mark * 0.06f, -0.012f, direction * 0.008f), new Vector3(0.022f, 0.043f, 0.004f), Trim, 0);
            g.Box(center + new Vector3(0, 0.028f, direction * 0.008f), new Vector3(0.13f, 0.009f, 0.003f), Trim, 0);
        }
    }
}
