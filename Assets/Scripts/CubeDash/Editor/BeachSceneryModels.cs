using System;
using UnityEngine;
using G = CubeDash.Editor.CountrySideMeshes.Geometry;

namespace CubeDash.Editor
{
    /// <summary>Original, closed coastal models. Slots retain the saved Beach palette's meaning.</summary>
    internal static class BeachSceneryModels
    {
        internal static readonly string[] Names = { "Coastal Palm", "Shaded Loungers", "Weatherboard Hut", "Lighthouse", "Timber Jetty", "Sailing Dinghy" };
        internal static Mesh Build(string name)
        {
            var g = new G(15);
            switch (name)
            {
                case "Coastal Palm": Palm(g); break;
                case "Shaded Loungers": Loungers(g); break;
                case "Weatherboard Hut": Hut(g); break;
                case "Lighthouse": Lighthouse(g); break;
                case "Timber Jetty": Jetty(g); break;
                case "Sailing Dinghy": Boat(g); break;
                default: throw new ArgumentOutOfRangeException(nameof(name));
            }
            return g.Finish(name);
        }

        // Closed tapered cylinders, unlike uncapped primitive beams or disconnected roof sheets.
        private static void Round(G g, Vector3 p, float bottom, float top, float height, int slot, int sides = 16)
        {
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 u = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), v = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                Vector3 up = Vector3.up * height;
                g.Quad(p + u * bottom, p + v * bottom, p + up + v * top, p + up + u * top, slot, u + v);
                g.Triangle(p, p + v * bottom, p + u * bottom, slot, Vector3.down);
                if (top > 0) g.Triangle(p + up, p + up + u * top, p + up + v * top, slot, Vector3.up);
            }
        }

        private static void Palm(G g)
        {
            Vector3 Stem(float t) => new Vector3(1.5f * t * t, 14 * t, 0.45f * Mathf.Sin(t * 2));
            for (int ring = 0; ring < 24; ring++)
            {
                float t = ring / 24f;
                Vector3 a = Stem(t), b = Stem((ring + 1) / 24f);
                // A continuous curved, tapered trunk with actual bark collars.
                float radius = Mathf.Lerp(0.47f, 0.19f, t);
                g.Tube(new[] { a, b }, radius, 0, 10);
                if (ring % 2 == 0) Round(g, a, radius * 1.09f, radius * 1.04f, 0.09f, 9, 10);
            }
            Round(g, Stem(0), 0.50f, 0.44f, 0.16f, 0, 10);
            Vector3 crown = Stem(1);
            Round(g, crown - Vector3.up * 0.25f, 0.30f, 0.12f, 0.65f, 1, 10);
            for (int frond = 0; frond < 11; frond++)
            {
                float angle = frond * Mathf.PI * 2 / 11;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 side = Vector3.Cross(Vector3.up, direction);
                float length = 4.7f + frond % 3 * 0.45f;
                Vector3 Spine(float t) => crown + direction * (length * t) + Vector3.up * (2.8f * t - 4.4f * t * t);
                var spine = new Vector3[9];
                for (int i = 0; i < spine.Length; i++) spine[i] = Spine(i / 8f);
                g.Tube(spine, 0.038f, 2, 5);
                for (int leaflet = 1; leaflet <= 15; leaflet++)
                {
                    float t = leaflet / 17f;
                    float width = Mathf.Sin(t * Mathf.PI) * 0.84f;
                    foreach (float sign in new[] { -1f, 1f })
                    {
                        Vector3 root = Spine(t), tip = root + side * (sign * width) - direction * 0.28f - Vector3.up * (0.16f + t * 0.22f);
                        Vector3 a = root - direction * 0.095f, b = root + direction * 0.12f;
                        int slot = (leaflet + frond) % 4 == 0 ? 2 : 1;
                        // Individually modeled leaflets, visible from above and below.
                        g.Triangle(a, b, tip, slot, Vector3.up);
                        g.Triangle(a, b, tip, slot, Vector3.down);
                    }
                }
            }
            for (int fruit = 0; fruit < 3; fruit++)
            {
                float angle = fruit * Mathf.PI * 2 / 3;
                Round(g, crown + new Vector3(Mathf.Cos(angle) * 0.25f, -0.45f, Mathf.Sin(angle) * 0.25f), 0.19f, 0.13f, 0.32f, 9, 8);
            }
        }

        private static void Loungers(G g)
        {
            foreach (float x in new[] { -1.25f, 1.25f })
            {
                for (int slat = 0; slat < 11; slat++)
                    g.Box(new Vector3(x, 0.57f, -0.8f + slat * 0.145f), new Vector3(1.02f, 0.075f, 0.125f), 9);
                g.Box(new Vector3(x, 0.635f, -0.12f), new Vector3(0.91f, 0.07f, 1.64f), 4);
                Vector3 back = new Vector3(x, 1.12f, 1.02f);
                Quaternion tilt = Quaternion.Euler(28, 0, 0);
                g.Box(back, new Vector3(1.02f, 1.34f, 0.10f), 9, tilt);
                g.Box(back + new Vector3(0, 0, -0.075f), new Vector3(0.91f, 1.24f, 0.07f), 4, tilt);
                // Canvas stripes are geometry on the cushions, not a floating or skewed back panel.
                for (int stripe = -2; stripe <= 2; stripe++)
                {
                    g.Box(new Vector3(x + stripe * 0.18f, 0.676f, -0.12f), new Vector3(0.074f, 0.006f, 1.60f), 7);
                    g.Box(back + tilt * new Vector3(stripe * 0.18f, 0, -0.09f), new Vector3(0.074f, 1.20f, 0.008f), 7, tilt);
                }
                foreach (float side in new[] { -1f, 1f })
                {
                    g.Beam(new Vector3(x + side * 0.47f, 0.04f, -0.82f), new Vector3(x + side * 0.47f, 0.57f, 0.42f), 0.09f, 9);
                    g.Beam(new Vector3(x + side * 0.47f, 0.04f, 0.83f), new Vector3(x + side * 0.47f, 0.57f, -0.59f), 0.09f, 9);
                    g.Beam(new Vector3(x + side * 0.48f, 0.52f, 0.25f), back + tilt * new Vector3(side * 0.48f, 0.52f, 0.02f), 0.08f, 9);
                }
            }
            Round(g, new Vector3(0, 0, -0.4f), 0.39f, 0.31f, 0.07f, 9);
            Round(g, new Vector3(0, 0.05f, -0.4f), 0.07f, 0.07f, 0.63f, 9);
            Round(g, new Vector3(0, 0.65f, -0.4f), 0.48f, 0.48f, 0.075f, 9, 24);
            Round(g, new Vector3(0, 0.72f, -0.4f), 0.10f, 0.09f, 0.15f, 12, 12);
            // Parasol is behind the table/chairs; pole goes all the way into the hub.
            Vector3 hub = new Vector3(0, 3.6f, 1.65f);
            Round(g, new Vector3(0, -0.08f, 1.65f), 0.055f, 0.045f, 3.76f, 9, 12);
            for (int panel = 0; panel < 16; panel++)
            {
                float a = panel * Mathf.PI / 8, b = (panel + 1) * Mathf.PI / 8;
                Vector3 Rim(float angle, float r) => hub + new Vector3(Mathf.Cos(angle) * r, -0.78f * Mathf.Pow(r / 2.6f, 1.3f), Mathf.Sin(angle) * r);
                for (int ring = 0; ring < 4; ring++)
                {
                    float r0 = ring * 0.65f, r1 = (ring + 1) * 0.65f;
                    int slot = panel % 2 == 0 ? 4 : 6;
                    g.Quad(Rim(a, r0), Rim(a, r1), Rim(b, r1), Rim(b, r0), slot, Vector3.up);
                    g.Quad(Rim(a, r0), Rim(a, r1), Rim(b, r1), Rim(b, r0), slot, Vector3.down);
                }
                g.Beam(hub - Vector3.up * 0.03f, Rim(a, 2.58f) - Vector3.up * 0.025f, 0.026f, 9);
                g.Quad(Rim(a, 2.6f), Rim(b, 2.6f), Rim(b, 2.6f) - Vector3.up * 0.13f, Rim(a, 2.6f) - Vector3.up * 0.13f, panel % 2 == 0 ? 4 : 6,
                    new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
            }
            Round(g, hub, 0.10f, 0.025f, 0.19f, 9, 12);
        }

        private static void Hut(G g)
        {
            foreach (float x in new[] { -2.7f, 2.7f }) foreach (float z in new[] { -2.25f, 2.25f })
            {
                g.Box(new Vector3(x, 0.52f, z), new Vector3(0.22f, 1.16f, 0.22f), 9);
                g.Beam(new Vector3(x, 0.1f, z), new Vector3(x, 1.15f, -z), 0.11f, 9);
            }
            for (int plank = 0; plank < 24; plank++)
                g.Box(new Vector3(-3 + plank * 0.26f, 1.08f, -0.5f), new Vector3(0.245f, 0.16f, 6.6f), 9);
            g.Box(new Vector3(0, 2.75f, 0.45f), new Vector3(5.6f, 3.2f, 4.7f), 7);
            for (int board = 0; board < 17; board++)
            {
                float y = 1.22f + board * 0.185f;
                foreach (float side in new[] { -1f, 1f })
                {
                    g.Box(new Vector3(side * 2.84f, y, 0.45f), new Vector3(0.07f, 0.15f, 4.72f), 7);
                    g.Box(new Vector3(0, y, 0.45f + side * 2.38f), new Vector3(5.65f, 0.15f, 0.07f), 7);
                }
            }
            foreach (float x in new[] { -2.8f, 2.8f }) foreach (float z in new[] { -1.92f, 2.82f })
                g.Box(new Vector3(x, 2.8f, z), new Vector3(0.18f, 3.4f, 0.18f), 12);
            g.Box(new Vector3(0, 2.33f, -1.99f), new Vector3(1.06f, 2.35f, 0.12f), 9);
            Round(g, new Vector3(0.35f, 2.25f, -2.09f), 0.045f, 0.045f, 0.09f, 13, 8);
            foreach (float x in new[] { -1.8f, 1.8f })
            {
                g.Box(new Vector3(x, 3.0f, -2.01f), new Vector3(1.15f, 1.15f, 0.10f), 11);
                foreach (float side in new[] { -1f, 1f })
                {
                    g.Box(new Vector3(x + side * 0.62f, 3, -2.08f), new Vector3(0.11f, 1.35f, 0.16f), 12);
                    g.Box(new Vector3(x, 3 + side * 0.61f, -2.08f), new Vector3(1.33f, 0.11f, 0.16f), 12);
                }
                g.Box(new Vector3(x, 3, -2.10f), new Vector3(0.06f, 1.15f, 0.10f), 12);
                g.Box(new Vector3(x, 3, -2.10f), new Vector3(1.15f, 0.06f, 0.10f), 12);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                g.Triangle(new Vector3(side * 2.8f, 4.35f, -1.9f), new Vector3(side * 2.8f, 6, 0.45f), new Vector3(side * 2.8f, 4.35f, 2.8f), 7, Vector3.right * side);
                g.Quad(new Vector3(-3.25f, 4.28f, 0.45f + side * 2.9f), new Vector3(3.25f, 4.28f, 0.45f + side * 2.9f),
                    new Vector3(3.25f, 6.1f, 0.45f), new Vector3(-3.25f, 6.1f, 0.45f), 10, new Vector3(0, 1, side));
                for (int seam = 0; seam < 19; seam++)
                    g.Beam(new Vector3(-3.15f + seam * 0.35f, 4.31f, 0.45f + side * 2.9f), new Vector3(-3.15f + seam * 0.35f, 6.14f, 0.45f), 0.045f, 12);
            }
            g.Box(new Vector3(0, 4.29f, 0.45f), new Vector3(6.5f, 0.12f, 5.8f), 9);
            g.Box(new Vector3(0, 6.12f, 0.45f), new Vector3(6.65f, 0.16f, 0.22f), 12);
            for (int step = 0; step < 5; step++)
                g.Box(new Vector3(0, (step + 1) * 0.108f, -5.15f + step * 0.31f), new Vector3(1.65f, (step + 1) * 0.216f, 0.32f), 9);
            foreach (float x in new[] { -2.8f, -1.2f, 1.2f, 2.8f })
                g.Box(new Vector3(x, 1.66f, -3.67f), new Vector3(0.10f, 1.1f, 0.10f), 12);
            foreach (float side in new[] { -1f, 1f })
                g.Box(new Vector3(side * 2, 2.18f, -3.67f), new Vector3(1.75f, 0.10f, 0.13f), 12);
        }

        private static void Lighthouse(G g)
        {
            Round(g, Vector3.zero, 2.3f, 2.3f, 0.45f, 3, 24);
            for (int course = 0; course < 12; course++)
                Round(g, new Vector3(0, 0.45f + course, 0), 1.6f - course * 0.035f, 1.565f - course * 0.035f, 1,
                    course == 2 || course == 6 || course == 10 ? 6 : 12, 24);
            g.Box(new Vector3(0, 1.7f, -1.64f), new Vector3(0.98f, 2.5f, 0.12f), 9);
            foreach (float y in new[] { 5.2f, 8.6f })
            {
                g.Box(new Vector3(0, y, -1.5f + y * 0.025f), new Vector3(0.6f, 0.92f, 0.12f), 11);
                g.Box(new Vector3(0, y - 0.5f, -1.56f + y * 0.025f), new Vector3(0.79f, 0.11f, 0.24f), 9);
            }
            Round(g, new Vector3(0, 12.45f, 0), 2.15f, 2.15f, 0.24f, 12, 32);
            for (int post = 0; post < 20; post++)
            {
                float a = post * Mathf.PI / 10, b = (post + 1) * Mathf.PI / 10;
                Vector3 p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.95f;
                g.Beam(p + Vector3.up * 12.65f, p + Vector3.up * 13.55f, 0.055f, 13);
                g.Beam(p + Vector3.up * 13.55f, new Vector3(Mathf.Cos(b) * 1.95f, 13.55f, Mathf.Sin(b) * 1.95f), 0.055f, 13);
            }
            Round(g, new Vector3(0, 12.7f, 0), 1.15f, 1.15f, 1.8f, 11, 16);
            for (int post = 0; post < 8; post++)
            {
                float a = post * Mathf.PI / 4;
                g.Box(new Vector3(Mathf.Cos(a) * 1.17f, 13.62f, Mathf.Sin(a) * 1.17f), new Vector3(0.11f, 1.95f, 0.11f), 12);
            }
            Round(g, new Vector3(0, 14.47f, 0), 1.55f, 0, 1.5f, 10, 24);
            Round(g, new Vector3(0, 15.9f, 0), 0.055f, 0.035f, 0.65f, 13, 8);
            for (int step = 0; step < 3; step++)
                g.Box(new Vector3(0, 0.075f + step * 0.075f, -2.75f + step * 0.34f), new Vector3(1.6f, 0.15f + step * 0.15f, 0.36f), 3);
        }

        private static void Jetty(G g)
        {
            const float deck = -9.1f;
            for (int plank = 0; plank < 48; plank++)
                g.Box(new Vector3(5.8f + plank * 0.49f, deck, 0), new Vector3(0.46f, 0.18f, 3.25f), 9);
            foreach (float z in new[] { -1.16f, 1.16f })
            {
                g.Box(new Vector3(17.3f, deck - 0.28f, z), new Vector3(24.1f, 0.30f, 0.22f), 0);
                for (int post = 0; post < 7; post++)
                {
                    float x = 6.6f + post * 3.65f, bottom = EnvironmentModelMeshes.BeachFloorHeight(x) - 0.35f;
                    Round(g, new Vector3(x, bottom, z), 0.16f, 0.14f, deck + 0.9f - bottom, 9, 12);
                    Round(g, new Vector3(x, deck + 0.7f, z), 0.17f, 0.17f, 0.10f, 8, 12);
                    if (post < 6)
                        g.Beam(new Vector3(x, deck + 0.72f, z), new Vector3(x + 3.65f, deck + 0.72f, z), 0.055f, 8);
                }
            }
            // Boarding ladder is joined to the deck and reaches the low sea level.
            foreach (float x in new[] { 26.5f, 27.35f })
                g.Beam(new Vector3(x, deck + 0.45f, -1.7f), new Vector3(x, -13.3f, -1.7f), 0.09f, 13);
            for (int rung = 0; rung < 8; rung++)
                g.Beam(new Vector3(26.5f, deck - rung * 0.46f, -1.7f), new Vector3(27.35f, deck - rung * 0.46f, -1.7f), 0.075f, 13);
        }

        private static void Boat(G g)
        {
            // Two nested curved hull skins plus sealed rim/transom: no missing end faces.
            const int sections = 14;
            Vector3 Hull(int i, float side, bool inner, bool upper)
            {
                float t = i / (float)sections, z = Mathf.Lerp(-3.5f, 3.8f, t);
                float width = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), 0.55f) * 1.45f + 0.09f;
                float rim = 0.8f + Mathf.Pow(Mathf.Abs(t - 0.48f) * 2, 2) * 0.35f;
                return new Vector3(side * Mathf.Max(0.025f, width - (inner ? 0.12f : 0)) * (upper ? 1 : 0.55f), upper ? rim : inner ? 0.01f : -0.20f, z);
            }
            for (int i = 0; i < sections; i++)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    g.Quad(Hull(i, side, false, false), Hull(i + 1, side, false, false), Hull(i + 1, side, false, true), Hull(i, side, false, true), 7, Vector3.right * side);
                    g.Quad(Hull(i, side, true, false), Hull(i + 1, side, true, false), Hull(i + 1, side, true, true), Hull(i, side, true, true), 9, -Vector3.right * side);
                    g.Quad(Hull(i, side, false, true), Hull(i + 1, side, false, true), Hull(i + 1, side, true, true), Hull(i, side, true, true), 12, Vector3.up);
                }
                g.Quad(Hull(i, -1, false, false), Hull(i + 1, -1, false, false), Hull(i + 1, 1, false, false), Hull(i, 1, false, false), 9, Vector3.down);
                g.Quad(Hull(i, -1, true, false), Hull(i + 1, -1, true, false), Hull(i + 1, 1, true, false), Hull(i, 1, true, false), 9, Vector3.up);
            }
            foreach (int i in new[] { 0, sections })
                g.Quad(Hull(i, -1, false, false), Hull(i, -1, false, true), Hull(i, 1, false, true), Hull(i, 1, false, false), 9, i == 0 ? Vector3.back : Vector3.forward);
            foreach (float z in new[] { -1.8f, 0.2f, 1.9f }) g.Box(new Vector3(0, 0.49f, z), new Vector3(2.15f, 0.12f, 0.37f), 9);
            Round(g, new Vector3(0, 0.1f, 0.25f), 0.065f, 0.035f, 6.3f, 9, 12);
            Vector3 a = new Vector3(0.04f, 1.15f, 0.25f), b = new Vector3(0.04f, 6.15f, 0.25f), c = new Vector3(0.04f, 1.3f, 3.1f);
            g.Triangle(a, b, c, 4, Vector3.right); g.Triangle(a, b, c, 4, Vector3.left);
            g.Beam(new Vector3(0, 1.12f, 0.2f), new Vector3(0, 1.25f, 3.15f), 0.07f, 9);
            g.Beam(new Vector3(0, 6.1f, 0.25f), new Vector3(0, 0.9f, -3.3f), 0.025f, 8);
            g.Beam(new Vector3(0, 6.1f, 0.25f), new Vector3(0, 0.9f, 3.6f), 0.025f, 8);
            g.Box(new Vector3(0, -0.05f, -3.6f), new Vector3(0.12f, 0.9f, 0.7f), 9);
        }
    }
}
