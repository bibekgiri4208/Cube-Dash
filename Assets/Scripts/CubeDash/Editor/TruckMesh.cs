using System.Collections.Generic;
using UnityEngine;

namespace CubeDash.Editor
{
    /// <summary>Reference-style cab-over tractor, with a sleeper, hollow squared stacks and dual rear tires.</summary>
    internal static class TruckMesh
    {
        public static Mesh BuildBody()
        {
            // Blue, navy, chassis, alloy, glass, amber, white, red, tank/fender grey, roof blue.
            var g = new Builder(10);
            // An open ladder chassis, not a solid slab behind the cab.
            foreach (float x in new[] { -0.56f, 0.56f })
                g.Box(new Vector3(x, 0.82f, -0.48f), new Vector3(0.17f, 0.3f, 5.98f), 2);
            foreach (float z in new[] { -3.32f, -2.3f, -1.1f, 0.05f, 1.5f })
                g.Box(new Vector3(0, 0.8f, z), new Vector3(1.12f, 0.17f, 0.16f), 2);
            foreach (float z in new[] { 1.65f, -1.72f, -2.87f })
            {
                g.Cylinder(new Vector3(0, 0.58f, z), 0.09f, 2.08f, true, 2);
                g.BevelBox(new Vector3(0, 0.55f, z), new Vector3(0.4f, 0.3f, 0.4f), 0.08f, 2);
            }
            g.Box(new Vector3(0, 0.52f, -1.35f), new Vector3(0.1f, 0.1f, 3.6f), 2);

            // Fifth-wheel saddle: a broad faceted plate with an open kingpin slot at the rear.
            g.Box(new Vector3(0, 1.015f, -1.45f), new Vector3(0.85f, 0.18f, 0.55f), 2);
            foreach (int side in new[] { -1, 1 })
                g.Prism(new[] {
                    new Vector3(side * 0.1f, 1.12f, -1.94f), new Vector3(side * 0.48f, 1.12f, -2.04f),
                    new Vector3(side * 0.64f, 1.12f, -1.72f), new Vector3(side * 0.56f, 1.12f, -1.32f),
                    new Vector3(side * 0.12f, 1.12f, -1.32f) }, Vector3.up * 0.08f, 8);
            g.Box(new Vector3(0, 1.16f, -1.32f), new Vector3(0.87f, 0.08f, 0.2f), 8);

            // Tall, square sleeper cab with the shallow sloped windshield of the reference.
            g.Prism(new[] {
                new Vector3(-1.15f, 1.4f, 0.18f), new Vector3(-1.15f, 1.4f, 2.52f),
                new Vector3(-1.15f, 2.4f, 2.52f), new Vector3(-1.15f, 3.4f, 2.29f),
                new Vector3(-1.15f, 3.4f, 0.18f) }, Vector3.right * 2.3f, 0);
            g.Box(new Vector3(0, 3.408f, 1.235f), new Vector3(2.3f, 0.016f, 2.11f), 9);
            g.Box(new Vector3(0, 2.04f, 0.166f), new Vector3(2.3f, 0.86f, 0.022f), 1);
            // Dark-framed rear sleeper window, shallow reflections and two white rear markers.
            g.Box(new Vector3(0, 2.93f, 0.148f), new Vector3(0.94f, 0.3f, 0.022f), 2);
            g.Box(new Vector3(0, 2.93f, 0.132f), new Vector3(0.87f, 0.245f, 0.016f), 4);
            g.Box(new Vector3(-0.18f, 2.96f, 0.119f), new Vector3(0.42f, 0.045f, 0.008f), 8);
            g.Box(new Vector3(0.22f, 2.84f, 0.119f), new Vector3(0.38f, 0.035f, 0.008f), 8);
            foreach (int side in new[] { -1, 1 })
            {
                g.Box(new Vector3(side * 0.8f, 3.005f, 0.142f), new Vector3(0.15f, 0.14f, 0.025f), 6);
                // The lower rear cab is open around the frame, flanked by blue corner pillars.
                g.Box(new Vector3(side * 0.94f, 1.17f, 0.38f), new Vector3(0.42f, 0.46f, 0.4f), 0);
                SideDetails(g, side);
                Stack(g, side);
                Horn(g, side * 0.73f);
            }

            // Split front glass with blue borders, thin dark seals, wipers and a restrained visor.
            for (int side = -1; side <= 1; side += 2)
            {
                float left = side < 0 ? -1.055f : 0.035f, right = side < 0 ? -0.035f : 1.055f;
                g.Quad(new Vector3(left, 2.51f, 2.503f), new Vector3(right, 2.51f, 2.503f),
                    new Vector3(right, 3.255f, 2.332f), new Vector3(left, 3.255f, 2.332f), 2);
                g.Quad(new Vector3(left + 0.025f, 2.54f, 2.499f), new Vector3(right - 0.025f, 2.54f, 2.499f),
                    new Vector3(right - 0.025f, 3.22f, 2.343f), new Vector3(left + 0.025f, 3.22f, 2.343f), 4);
                g.Box(new Vector3(side * 0.68f, 2.49f, 2.51f), new Vector3(0.53f, 0.025f, 0.025f), 2);
                g.Box(new Vector3(side * 0.88f, 1.55f, 2.545f), new Vector3(0.42f, 0.25f, 0.045f), 3);
                for (int lamp = -1; lamp <= 1; lamp += 2)
                    g.Box(new Vector3(side * 0.88f + lamp * 0.093f, 1.55f, 2.575f), new Vector3(0.15f, 0.16f, 0.022f), 6);
                g.Box(new Vector3(side * 0.95f, 0.98f, 2.59f), new Vector3(0.12f, 0.14f, 0.024f), 5);
            }
            g.Box(new Vector3(0, 3.315f, 2.335f), new Vector3(2.4f, 0.12f, 0.19f), 8);
            for (int lamp = -2; lamp <= 2; lamp++)
            {
                float x = lamp * 0.43f;
                g.Prism(new[] { new Vector3(x - 0.085f, 3.416f, 1.98f), new Vector3(x - 0.085f, 3.416f, 2.22f),
                    new Vector3(x - 0.085f, 3.505f, 2.2f), new Vector3(x - 0.085f, 3.54f, 2.05f) }, Vector3.right * 0.17f, 0);
                g.Box(new Vector3(x, 3.46f, 2.225f), new Vector3(0.115f, 0.062f, 0.019f), 5);
            }
            g.Box(new Vector3(0, 0.99f, 2.535f), new Vector3(2.3f, 0.42f, 0.12f), 3);
            g.Box(new Vector3(0, 1.31f, 2.46f), new Vector3(2.3f, 0.22f, 0.12f), 0);
            foreach (float x in new[] { -0.46f, 0.46f })
                g.Box(new Vector3(x, 0.88f, 2.604f), new Vector3(0.28f, 0.027f, 0.008f), 2);
            g.Box(new Vector3(0, 1.94f, 2.54f), new Vector3(0.96f, 1.19f, 0.06f), 3);
            g.Box(new Vector3(0, 1.94f, 2.579f), new Vector3(0.83f, 1.09f, 0.026f), 2);
            for (int bar = -5; bar <= 5; bar++)
                g.Box(new Vector3(bar * 0.067f, 1.94f, 2.601f), new Vector3(0.024f, 1.045f, 0.018f), 3);
            g.Box(new Vector3(0, 1.94f, 2.616f), new Vector3(0.045f, 1.12f, 0.02f), 3);
            foreach (float y in new[] { 1.57f, 1.9f, 2.24f })
                g.Box(new Vector3(0, y, 2.606f), new Vector3(0.8f, 0.019f, 0.008f), 8);

            // Separate rear mudflaps, amber corner markers and the central red/white lamp cluster.
            g.Box(new Vector3(0, 0.83f, -3.5f), new Vector3(1.19f, 0.55f, 0.11f), 2);
            foreach (float x in new[] { -0.33f, 0.33f })
                g.Box(new Vector3(x, 0.97f, -3.566f), new Vector3(0.19f, 0.18f, 0.025f), 7);
            g.Box(new Vector3(0, 0.97f, -3.566f), new Vector3(0.16f, 0.16f, 0.025f), 6);
            return g.Finish("CubeDashTruck");
        }

        private static void SideDetails(Builder g, int side)
        {
            float x = side * 1.15f;
            g.Box(new Vector3(x + side * 0.013f, 2.04f, 1.35f), new Vector3(0.025f, 0.86f, 2.32f), 1);
            // Side skirts stop around the front tire instead of intersecting it.
            g.Box(new Vector3(x, 1.145f, 0.52f), new Vector3(0.025f, 0.51f, 0.68f), 0);
            g.Box(new Vector3(x, 1.145f, 2.46f), new Vector3(0.025f, 0.51f, 0.12f), 0);
            g.ArcCover(new Vector3(side * 1.14f, 0.58f, 1.65f), 0.68f, 0.035f, 0.08f, 2);
            // Door seam, navy lower panel and large black-framed side window.
            g.Box(new Vector3(x + side * 0.03f, 2.36f, 1.79f), new Vector3(0.027f, 1.62f, 0.94f), 2);
            g.Box(new Vector3(x + side * 0.049f, 2.36f, 1.79f), new Vector3(0.015f, 1.55f, 0.88f), 1);
            g.Box(new Vector3(x + side * 0.062f, 2.84f, 1.8f), new Vector3(0.018f, 0.64f, 0.76f), 2);
            g.Box(new Vector3(x + side * 0.077f, 2.84f, 1.8f), new Vector3(0.015f, 0.59f, 0.7f), 4);
            g.Box(new Vector3(x + side * 0.081f, 2.84f, 1.97f), new Vector3(0.017f, 0.6f, 0.025f), 2);
            g.Box(new Vector3(x + side * 0.071f, 1.99f, 1.44f), new Vector3(0.03f, 0.085f, 0.12f), 3);
            // Sleeper service hatch, latch, small upper window and lower vents.
            g.Box(new Vector3(x + side * 0.037f, 1.94f, 0.65f), new Vector3(0.02f, 0.53f, 0.59f), 0);
            g.Box(new Vector3(x + side * 0.052f, 1.94f, 0.65f), new Vector3(0.014f, 0.5f, 0.56f), 1);
            g.Box(new Vector3(x + side * 0.065f, 1.93f, 0.44f), new Vector3(0.02f, 0.14f, 0.021f), 3);
            g.Box(new Vector3(x + side * 0.02f, 2.84f, 0.71f), new Vector3(0.023f, 0.3f, 0.17f), 2);
            g.Box(new Vector3(x + side * 0.039f, 2.84f, 0.71f), new Vector3(0.015f, 0.25f, 0.125f), 4);
            foreach (float z in new[] { 0.63f, 1.2f })
                g.Box(new Vector3(x + side * 0.02f, 1.25f, z), new Vector3(0.025f, 0.14f, 0.27f), 2);
            foreach (float y in new[] { 1.12f, 3.13f })
                g.Box(new Vector3(x + side * 0.025f, y, 0.31f), new Vector3(0.03f, 0.055f, 0.075f), 5);

            // Rectangular mirror with a thin, continuous vertical support, as in the reference.
            g.Box(new Vector3(side * 1.31f, 2.92f, 2.14f), new Vector3(0.33f, 0.038f, 0.04f), 2);
            g.Box(new Vector3(side * 1.31f, 1.74f, 1.37f), new Vector3(0.33f, 0.035f, 0.04f), 2);
            g.Box(new Vector3(side * 1.465f, 2.33f, 1.37f), new Vector3(0.035f, 1.2f, 0.035f), 3);
            g.Box(new Vector3(side * 1.465f, 2.94f, 1.76f), new Vector3(0.035f, 0.035f, 0.81f), 2);
            g.Box(new Vector3(side * 1.465f, 2.68f, 2.15f), new Vector3(0.055f, 0.51f, 0.28f), 2);
            g.Box(new Vector3(side * 1.503f, 2.68f, 2.15f), new Vector3(0.015f, 0.44f, 0.225f), 4);

            // Dark faceted fuel tank with metal straps and a filler cap; hollow two-rung cab steps.
            g.BevelBox(new Vector3(side * 1.04f, 0.81f, -0.68f), new Vector3(0.66f, 0.81f, 1.37f), 0.15f, 8);
            foreach (float z in new[] { -1.2f, -0.16f })
                g.BevelBox(new Vector3(side * 1.04f, 0.81f, z), new Vector3(0.682f, 0.832f, 0.055f), 0.15f, 2);
            g.Cylinder(new Vector3(side * 1.09f, 1.225f, -0.56f), 0.085f, 0.035f, false, 2);
            foreach (float z in new[] { 0.43f, 0.92f })
                g.Box(new Vector3(side * 1.13f, 0.64f, z), new Vector3(0.15f, 0.88f, 0.065f), 2);
            foreach (float y in new[] { 0.27f, 0.58f, 0.94f })
                g.Box(new Vector3(side * 1.18f, y, 0.675f), new Vector3(0.27f, 0.065f, 0.55f), 8);
            foreach (float z in new[] { -1.72f, -2.87f })
                g.ArcCover(new Vector3(side * 1.0f, 0.58f, z), 0.67f, 0.035f, 0.81f, 8);
            g.Box(new Vector3(side * 1.02f, 0.63f, -3.5f), new Vector3(0.8f, 0.87f, 0.055f), 2);
            g.Box(new Vector3(side * 1.02f, 1.09f, -3.53f), new Vector3(0.85f, 0.13f, 0.09f), 8);
            g.Box(new Vector3(side * 1.38f, 1.095f, -3.586f), new Vector3(0.075f, 0.115f, 0.015f), 5);
        }

        private static void Stack(Builder g, int side)
        {
            // Bevelled square tube with a slant-cut lip and actual inner walls/open mouth.
            Vector3 center = new Vector3(side * 0.96f, 0, 0.14f);
            Vector2[] outline = { new Vector2(-0.075f, -0.11f), new Vector2(0.075f, -0.11f),
                new Vector2(0.11f, -0.075f), new Vector2(0.11f, 0.075f), new Vector2(0.075f, 0.11f),
                new Vector2(-0.075f, 0.11f), new Vector2(-0.11f, 0.075f), new Vector2(-0.11f, -0.075f) };
            Vector3 Point(int i, bool inner, bool top)
            {
                Vector2 p = outline[i % outline.Length] * (inner ? 0.72f : 1);
                return center + new Vector3(p.x, top ? 4.2f + p.y * 0.55f : 1.13f, p.y);
            }
            for (int i = 0; i < outline.Length; i++)
            {
                g.Quad(Point(i, false, false), Point(i, false, true), Point(i + 1, false, true), Point(i + 1, false, false), 3);
                g.Quad(Point(i, false, true), Point(i, true, true), Point(i + 1, true, true), Point(i + 1, false, true), 3);
                Vector3 a = Point(i, true, true), b = Point(i + 1, true, true);
                g.Quad(a, a - Vector3.up * 0.4f, b - Vector3.up * 0.4f, b, 2);
            }
            g.Box(center + new Vector3(0, 3.78f, 0), new Vector3(0.145f, 0.015f, 0.145f), 2);
            g.BevelBox(center + new Vector3(0, 1.155f, 0.075f), new Vector3(0.29f, 0.25f, 0.33f), 0.07f, 3);
        }

        private static void Horn(Builder g, float x)
        {
            g.Box(new Vector3(x, 3.44f, 1.1f), new Vector3(0.14f, 0.06f, 0.32f), 2);
            Vector3 a = new Vector3(x, 3.52f, 1.03f), b = new Vector3(x, 3.56f, 1.65f);
            Vector3[] near = { new Vector3(-0.035f, -0.035f, 0), new Vector3(0.035f, -0.035f, 0),
                new Vector3(0.035f, 0.035f, 0), new Vector3(-0.035f, 0.035f, 0) };
            Vector3[] far = { new Vector3(-0.145f, -0.07f, 0), new Vector3(0.145f, -0.07f, 0),
                new Vector3(0.145f, 0.07f, 0), new Vector3(-0.145f, 0.07f, 0) };
            for (int i = 0; i < 4; i++)
                g.Quad(a + near[i], a + near[(i + 1) % 4], b + far[(i + 1) % 4], b + far[i], 3);
            g.Box(b - Vector3.forward * 0.025f, new Vector3(0.25f, 0.105f, 0.009f), 2);
        }

        public static Mesh BuildWheel(bool dual = false)
        {
            var g = new Builder(3);
            foreach (float offset in dual ? new[] { -0.2f, 0.2f } : new[] { 0f })
            {
                // Chamfered tire shoulders and inset alloy rims, rather than stacked flat cylinders.
                float[] x = { -0.185f, -0.14f, 0.14f, 0.185f };
                float[] radii = { 0.525f, 0.58f, 0.58f, 0.525f };
                for (int ring = 0; ring < 3; ring++)
                    g.AxialBand(offset + x[ring], radii[ring], offset + x[ring + 1], radii[ring + 1], 0);
                foreach (int side in new[] { -1, 1 })
                {
                    float face = offset + side * 0.185f;
                    g.AxialRing(face, 0.525f, 0.395f, 0, side);
                    g.AxialRing(face + side * 0.005f, 0.395f, 0.305f, 1, side);
                    g.Cylinder(new Vector3(offset + side * 0.168f, 0, 0), 0.305f, 0.018f, true, 2);
                    g.Cylinder(new Vector3(offset + side * 0.181f, 0, 0), 0.252f, 0.022f, true, 1);
                    g.Cylinder(new Vector3(offset + side * 0.204f, 0, 0), 0.105f, 0.045f, true, 2);
                    for (int lug = 0; lug < 8; lug++)
                    {
                        float angle = lug * Mathf.PI / 4;
                        g.Box(new Vector3(offset + side * 0.2f, Mathf.Cos(angle) * 0.208f, Mathf.Sin(angle) * 0.208f),
                            Vector3.one * 0.027f, 1);
                    }
                }
            }
            return g.Finish(dual ? "CubeDashTruckRearWheel" : "CubeDashTruckWheel");
        }

        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int>[] indices;
            public Builder(int materials)
            {
                indices = new List<int>[materials];
                for (int i = 0; i < indices.Length; i++) indices[i] = new List<int>();
            }
            private void Triangle(Vector3 a, Vector3 b, Vector3 c, int material, bool reverse = false)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(reverse ? c : b); vertices.Add(reverse ? b : c);
                indices[material].Add(start); indices[material].Add(start + 1); indices[material].Add(start + 2);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int material, bool reverse = false)
            { Triangle(a, b, c, material, reverse); Triangle(a, c, d, material, reverse); }
            public void Box(Vector3 center, Vector3 size, int material)
            {
                Vector3 h = size * 0.5f;
                Prism(new[] { center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(h.x, -h.y, -h.z),
                    center + new Vector3(h.x, -h.y, h.z), center + new Vector3(-h.x, -h.y, h.z) }, Vector3.up * size.y, material);
            }
            public void BevelBox(Vector3 center, Vector3 size, float bevel, int material)
            {
                Vector3 h = size * 0.5f;
                Prism(new[] { center + new Vector3(-h.x + bevel, -h.y, -h.z), center + new Vector3(h.x - bevel, -h.y, -h.z),
                    center + new Vector3(h.x, -h.y + bevel, -h.z), center + new Vector3(h.x, h.y - bevel, -h.z),
                    center + new Vector3(h.x - bevel, h.y, -h.z), center + new Vector3(-h.x + bevel, h.y, -h.z),
                    center + new Vector3(-h.x, h.y - bevel, -h.z), center + new Vector3(-h.x, -h.y + bevel, -h.z) },
                    Vector3.forward * size.z, material);
            }
            public void Prism(Vector3[] outline, Vector3 extrusion, int material)
            {
                bool reverse = Vector3.Dot(Vector3.Cross(outline[1] - outline[0], outline[2] - outline[0]), extrusion) > 0;
                for (int i = 1; i < outline.Length - 1; i++)
                {
                    Triangle(outline[0], outline[i], outline[i + 1], material, reverse);
                    Triangle(outline[0] + extrusion, outline[i] + extrusion, outline[i + 1] + extrusion, material, !reverse);
                }
                for (int i = 0; i < outline.Length; i++)
                {
                    Vector3 a = outline[i], b = outline[(i + 1) % outline.Length];
                    Quad(a, a + extrusion, b + extrusion, b, material, reverse);
                }
            }
            public void ArcCover(Vector3 center, float radius, float thickness, float width, int material)
            {
                const int steps = 10;
                for (int i = 0; i < steps; i++)
                {
                    float a = Mathf.Lerp(-75, 75, i / (float)steps) * Mathf.Deg2Rad;
                    float b = Mathf.Lerp(-75, 75, (i + 1) / (float)steps) * Mathf.Deg2Rad;
                    Vector3 first = new Vector3(0, Mathf.Cos(a), Mathf.Sin(a)), next = new Vector3(0, Mathf.Cos(b), Mathf.Sin(b));
                    Vector3 left = center - Vector3.right * width * 0.5f, right = center + Vector3.right * width * 0.5f;
                    Quad(left + first * radius, right + first * radius, right + next * radius, left + next * radius, material);
                    Quad(left + first * (radius + thickness), left + next * (radius + thickness),
                        right + next * (radius + thickness), right + first * (radius + thickness), material);
                    foreach (int side in new[] { -1, 1 })
                    {
                        Vector3 edge = center + Vector3.right * side * width * 0.5f;
                        Quad(edge + first * radius, edge + next * radius, edge + next * (radius + thickness),
                            edge + first * (radius + thickness), material, side < 0);
                    }
                }
            }
            public void AxialBand(float x1, float r1, float x2, float r2, int material)
            {
                for (int i = 0; i < 16; i++)
                {
                    Vector3 a = Radial(i, 16), b = Radial(i + 1, 16);
                    Quad(Vector3.right * x1 + a * r1, Vector3.right * x1 + b * r1,
                        Vector3.right * x2 + b * r2, Vector3.right * x2 + a * r2, material);
                }
            }
            public void AxialRing(float x, float outer, float inner, int material, int side)
            {
                for (int i = 0; i < 16; i++)
                {
                    Vector3 a = Radial(i, 16), b = Radial(i + 1, 16), center = Vector3.right * x;
                    Quad(center + a * outer, center + b * outer, center + b * inner, center + a * inner, material, side < 0);
                }
            }
            private static Vector3 Radial(int i, int sides)
            {
                float angle = i * Mathf.PI * 2 / sides;
                return new Vector3(0, Mathf.Cos(angle), Mathf.Sin(angle));
            }
            public void Cylinder(Vector3 center, float radius, float length, bool horizontal, int material)
            {
                const int sides = 16;
                Vector3 axis = horizontal ? Vector3.right : Vector3.up;
                Vector3 Point(int i)
                {
                    float angle = i * Mathf.PI * 2 / sides;
                    return horizontal ? Radial(i, sides) * radius : new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
                }
                Vector3 start = center - axis * length * 0.5f, end = center + axis * length * 0.5f;
                for (int i = 0; i < sides; i++)
                {
                    Vector3 a = Point(i), b = Point(i + 1);
                    Quad(start + a, start + b, end + b, end + a, material);
                    Triangle(start, start + b, start + a, material);
                    Triangle(end, end + a, end + b, material);
                }
            }
            public Mesh Finish(string name)
            {
                Mesh mesh = new Mesh { name = name };
                mesh.SetVertices(vertices); mesh.subMeshCount = indices.Length;
                for (int i = 0; i < indices.Length; i++) mesh.SetTriangles(indices[i], i);
                mesh.uv = new Vector2[vertices.Count];
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
