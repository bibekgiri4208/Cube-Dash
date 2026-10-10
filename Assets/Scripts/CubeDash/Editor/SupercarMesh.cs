using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CubeDash.Editor
{
    /// <summary>Reference-derived low-poly hypercar: sculpted skin, open wheel arches and modeled lights/trim.</summary>
    internal static class SupercarMesh
    {
        // Charcoal, cyan, intake black, glass, graphite, blue trim, alloy, white LED, red LED, rubber.
        public const int MaterialCount = 10;
        public const float FrontAxle = 1.30f, RearAxle = -1.37f, WheelRadius = 0.4f;
        private static readonly float[] Stations = { -2.28f, -2.05f, -1.37f, -0.85f, -0.35f, 0.4f, 0.85f, 1.30f, 1.85f, 2.34f };
        private static readonly float[] Widths = { 1.03f, 1.08f, 1.10f, 1.03f, 0.96f, 0.98f, 1.03f, 1.08f, 1.01f, 0.91f };
        private static readonly float[] Shoulders = { 0.81f, 0.87f, 0.96f, 0.90f, 0.81f, 0.83f, 0.91f, 0.94f, 0.77f, 0.55f };
        private static readonly float[] Centers = { 0.70f, 0.75f, 0.83f, 0.86f, 0.82f, 0.77f, 0.72f, 0.68f, 0.61f, 0.48f };

        private static float Sample(float[] values, float z)
        {
            for (int i = 1; i < Stations.Length; i++)
                if (z <= Stations[i]) return Mathf.Lerp(values[i - 1], values[i], Mathf.InverseLerp(Stations[i - 1], Stations[i], z));
            return values[values.Length - 1];
        }

        private static float LowerEdge(float z)
        {
            foreach (float axle in new[] { FrontAxle, RearAxle })
            {
                float offset = z - axle;
                if (Mathf.Abs(offset) <= 0.48f) return WheelRadius + Mathf.Sqrt(Mathf.Max(0, 0.48f * 0.48f - offset * offset));
            }
            return 0.17f;
        }

        private static Vector3 Top(float fraction, float z, int side)
        {
            float center = Sample(Centers, z), outer = Sample(Shoulders, z);
            float t = Mathf.Abs(fraction);
            float y = t < 0.52f ? center + t * 0.06f : Mathf.Lerp(center + 0.0312f, outer, (t - 0.52f) / 0.48f);
            return new Vector3(side * fraction * Sample(Widths, z), y, z);
        }

        public static Mesh BuildBody()
        {
            var g = new Builder(MaterialCount);
            var sections = new SortedSet<float>(Stations);
            foreach (float axle in new[] { FrontAxle, RearAxle })
            {
                sections.Add(axle - 0.482f); sections.Add(axle + 0.482f);
                for (int step = 0; step <= 12; step++) sections.Add(axle + Mathf.Sin((-90 + step * 15) * Mathf.Deg2Rad) * 0.48f);
            }
            float[] zRows = new float[sections.Count]; sections.CopyTo(zRows);
            g.Box(new Vector3(0, 0.24f, 0), new Vector3(1.35f, 0.16f, 4.55f), 2);
            foreach (int side in new[] { -1, 1 })
            {
                for (int row = 0; row < zRows.Length - 1; row++)
                {
                    float za = zRows[row], zb = zRows[row + 1];
                    Vector3 Edge(float z, float height)
                    {
                        float low = LowerEdge(z), high = Sample(Shoulders, z);
                        return new Vector3(side * Sample(Widths, z) * (height < 0.01f ? 0.97f : 1), Mathf.Lerp(low, high, height), z);
                    }
                    g.Quad(Edge(za, 0), Edge(zb, 0), Edge(zb, 0.38f), Edge(za, 0.38f), 0, Vector3.right * side);
                    g.Quad(Edge(za, 0.38f), Edge(zb, 0.38f), Edge(zb, 1), Edge(za, 1), 0, Vector3.right * side);
                }
                // The bonnet uses broad deliberate facets; wheel-arch subdivisions stay on the sides.
                for (int row = 0; row < Stations.Length - 1; row++)
                {
                    float za = Stations[row], zb = Stations[row + 1];
                    float HoodWidth(float z) => z < 0.65f ? 0.52f : Mathf.Lerp(0.75f, 0.15f, Mathf.InverseLerp(0.65f, 2.34f, z));
                    float[] first = { 0, HoodWidth(za), 0.80f, 1 }, next = { 0, HoodWidth(zb), 0.80f, 1 };
                    for (int col = 0; col < 3; col++)
                    {
                        float middle = (za + zb) * 0.5f;
                        bool paint = col > 0 && (middle > 0.65f || middle < -0.75f) || col == 2 && middle > -1.95f;
                        g.Quad(Top(first[col], za, side), Top(next[col], zb, side),
                            Top(next[col + 1], zb, side), Top(first[col + 1], za, side), paint ? 1 : 0, Vector3.up);
                    }
                }
                WheelArch(g, side, FrontAxle); WheelArch(g, side, RearAxle);
                SideDetails(g, side); Headlight(g, side);
            }
            Front(g); Canopy(g); Rear(g);
            Mesh body = g.Finish("CubeDashSupercarBody");
            // Low roof/wing silhouette of the references, without shrinking the road-contact wheels.
            Vector3[] vertices = body.vertices;
            Vector3[] normals = body.normals;
            for (int i = 0; i < vertices.Length; i++)
                if (vertices[i].y > 0.8f)
                {
                    vertices[i].y = 0.8f + (vertices[i].y - 0.8f) * 0.77f;
                    normals[i] = new Vector3(normals[i].x, normals[i].y / 0.77f, normals[i].z).normalized;
                }
            body.vertices = vertices; body.normals = normals; body.RecalculateBounds(); return body;
        }

        private static void WheelArch(Builder g, int side, float axle)
        {
            for (int step = 0; step < 12; step++)
            {
                Vector3 Point(int index, float radius, float inset = 0)
                {
                    float angle = (-90 + index * 15) * Mathf.Deg2Rad, z = axle + Mathf.Sin(angle) * radius;
                    return new Vector3(side * (Sample(Widths, z) + 0.012f - inset), WheelRadius + Mathf.Cos(angle) * radius, z);
                }
                g.Quad(Point(step, 0.48f), Point(step + 1, 0.48f), Point(step + 1, 0.515f), Point(step, 0.515f), 4, Vector3.right * side);
                Vector3 normal = new Vector3(0, Mathf.Cos((-82.5f + step * 15) * Mathf.Deg2Rad), Mathf.Sin((-82.5f + step * 15) * Mathf.Deg2Rad));
                g.Quad(Point(step, 0.515f), Point(step + 1, 0.515f), Point(step + 1, 0.515f, 0.09f), Point(step, 0.515f, 0.09f), 0, normal);
            }
        }

        private static void SideDetails(Builder g, int side)
        {
            Vector3 Side(float y, float z, float offset = 0.02f) => new Vector3(side * (Sample(Widths, z) + offset), y, z);
            Vector3[] inlet = { Side(0.68f, -0.61f), Side(0.84f, -0.74f), Side(0.91f, -1.10f), Side(0.73f, -1.02f) };
            g.Polygon(inlet, 5, Vector3.right * side);
            Vector3 center = (inlet[0] + inlet[1] + inlet[2] + inlet[3]) * 0.25f;
            var inside = new Vector3[inlet.Length];
            for (int i = 0; i < inside.Length; i++) inside[i] = Vector3.Lerp(center, inlet[i], 0.83f) + Vector3.right * side * 0.004f;
            g.Polygon(inside, 2, Vector3.right * side);
            g.Box(Side(0.71f, -0.25f, 0.031f), new Vector3(0.018f, 0.036f, 0.22f), 2);
            g.Polygon(new[] { Side(0.18f, -0.79f), Side(0.23f, -0.12f), Side(0.41f, 0.66f), Side(0.23f, 0.30f) }, 2, Vector3.right * side);
            g.Ribbon(Side(0.18f, -0.78f, 0.032f), Side(0.19f, 0.70f, 0.032f), 0.018f, Vector3.right * side, 5);
            g.Ribbon(Side(0.24f, 0.59f), Side(0.73f, 0.55f), 0.008f, Vector3.right * side, 4);
            g.Ribbon(Side(0.20f, -0.48f), Side(0.70f, -0.55f), 0.008f, Vector3.right * side, 4);
            g.Box(new Vector3(side * 0.96f, 0.97f, 0.50f), new Vector3(0.32f, 0.034f, 0.05f), 0);
            g.Box(new Vector3(side * 1.13f, 0.99f, 0.49f), new Vector3(0.24f, 0.065f, 0.15f), 0);
            g.Box(new Vector3(side * 1.13f, 0.99f, 0.409f), new Vector3(0.20f, 0.041f, 0.013f), 6);
            // Angular dark bonnet vents lie on the faceted top skin, not in front of the lights.
            Vector3 OnTop(float x, float z) => Top(x, z, side) + Vector3.up * 0.008f;
            g.Polygon(new[] { OnTop(0.32f, 1.08f), OnTop(0.64f, 1.24f), OnTop(0.70f, 1.55f), OnTop(0.42f, 1.49f) }, 2, Vector3.up);
        }

        private static void Headlight(Builder g, int side)
        {
            Vector3 a = new Vector3(side * 0.67f, 0.635f, 2.045f), b = new Vector3(side * 0.93f, 0.70f, 2.02f);
            Vector3 c = new Vector3(side * 0.95f, 0.83f, 1.71f), d = new Vector3(side * 0.76f, 0.76f, 1.76f);
            Vector3 normal = new Vector3(side * 0.15f, 0.8f, 0.6f).normalized;
            g.Quad(a, b, c, d, 1, normal);
            Vector3 middle = (a + b + c + d) * 0.25f;
            Vector3 Inset(Vector3 p) => Vector3.Lerp(middle, p, 0.80f) + normal * 0.008f;
            g.Ribbon(Inset(a), Inset(c), 0.029f, normal, 7);
            g.Ribbon(Inset(b), Inset(d), 0.029f, normal, 7);
        }

        private static void Front(Builder g)
        {
            g.Polygon(new[] { new Vector3(-0.89f, 0.17f, 2.34f), new Vector3(0.89f, 0.17f, 2.34f),
                new Vector3(0.91f, 0.55f, 2.34f), new Vector3(-0.91f, 0.55f, 2.34f) }, 0, Vector3.forward);
            var grille = new[] { new Vector3(-0.25f, 0.155f, 2.368f), new Vector3(0.25f, 0.155f, 2.368f),
                new Vector3(0.285f, 0.31f, 2.368f), new Vector3(0.24f, 0.46f, 2.368f), new Vector3(0.13f, 0.52f, 2.368f),
                new Vector3(-0.13f, 0.52f, 2.368f), new Vector3(-0.24f, 0.46f, 2.368f), new Vector3(-0.285f, 0.31f, 2.368f) };
            g.Polygon(grille, 4, Vector3.forward);
            Vector3 center = new Vector3(0, 0.33f, 2.375f);
            var mouth = new Vector3[grille.Length];
            for (int i = 0; i < mouth.Length; i++) mouth[i] = Vector3.Lerp(center, grille[i], 0.87f) + Vector3.forward * 0.01f;
            g.Polygon(mouth, 2, Vector3.forward);
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 P(float x, float y, float z = 2.348f) => new Vector3(side * x, y, z);
                g.Polygon(new[] { P(0.34f, 0.19f), P(0.94f, 0.20f), P(0.91f, 0.43f), P(0.35f, 0.39f) }, 2, Vector3.forward);
                g.Ribbon(P(0.36f, 0.30f, 2.356f), P(0.90f, 0.34f, 2.356f), 0.017f, Vector3.forward, 5);
                g.Ribbon(P(0.36f, 0.245f, 2.358f), P(0.90f, 0.27f, 2.358f), 0.026f, Vector3.forward, 4);
            }
            g.Prism(new[] { new Vector3(-1.10f, 0.12f, 2.16f), new Vector3(-0.92f, 0.12f, 2.44f),
                new Vector3(0.92f, 0.12f, 2.44f), new Vector3(1.10f, 0.12f, 2.16f), new Vector3(0.89f, 0.12f, 2.08f),
                new Vector3(-0.89f, 0.12f, 2.08f) }, Vector3.up * 0.06f, 4);
        }

        private static void Canopy(Builder g)
        {
            float[] columns = { -1, -0.90f, -0.60f, 0, 0.60f, 0.90f, 1 };
            Vector3 Bottom(float t) => new Vector3(t * 0.78f, 0.845f + t * t * 0.055f, 0.79f - t * t * 0.28f);
            Vector3 TopEdge(float t) => new Vector3(t * 0.61f, 1.28f - Mathf.Pow(Mathf.Abs(t), 3) * 0.08f, 0.13f - t * t * 0.09f);
            Vector3 RoofRear(float t) => new Vector3(t * 0.61f, 1.27f - Mathf.Abs(t) * 0.07f, -0.63f + Mathf.Abs(t) * 0.04f);
            for (int col = 0; col < columns.Length - 1; col++)
            {
                float a = columns[col], b = columns[col + 1];
                Vector3 Glass(float t, float v) => Vector3.Lerp(Bottom(t), TopEdge(t), v)
                    + Vector3.forward * (Mathf.Sin(v * Mathf.PI) * (1 - t * t) * 0.045f);
                for (int row = 0; row < 3; row++)
                    g.Quad(Glass(a, row / 3f), Glass(b, row / 3f), Glass(b, (row + 1) / 3f), Glass(a, (row + 1) / 3f), 3, new Vector3(0, 1, 1));
                g.Quad(TopEdge(a), TopEdge(b), RoofRear(b), RoofRear(a), 0, Vector3.up);
            }
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 front = new Vector3(side * 0.80f, 0.88f, 0.51f), back = new Vector3(side * 0.85f, 0.91f, -1.04f);
                Vector3 upperFront = TopEdge(side), upperBack = RoofRear(side);
                g.Quad(front, upperFront, upperBack, back, 0, Vector3.right * side);
                g.Polygon(new[] { new Vector3(side * 0.796f, 0.934f, 0.50f), new Vector3(side * 0.636f, 1.153f, 0.035f),
                    new Vector3(side * 0.649f, 1.151f, -0.49f), new Vector3(side * 0.831f, 0.943f, -0.89f) }, 3, Vector3.right * side);
                g.Ribbon(front + Vector3.right * side * 0.01f, back + Vector3.right * side * 0.01f, 0.016f, Vector3.right * side, 1);
                Vector3 a = new Vector3(side * 0.03f, 1.267f, -0.64f), b = new Vector3(side * 0.60f, 1.201f, -0.60f);
                Vector3 c = new Vector3(side * 0.73f, 0.941f, -1.67f), d = new Vector3(side * 0.03f, 1.007f, -1.72f);
                g.Quad(a, b, c, d, 3, new Vector3(0, 1, -0.3f));
            }
            // Tall triangular longitudinal fin with the reference's thin cyan crown.
            g.Prism(new[] { new Vector3(-0.014f, 1.27f, -0.52f), new Vector3(-0.014f, 1.43f, -1.96f),
                new Vector3(-0.014f, 0.89f, -1.96f) }, Vector3.right * 0.028f, 0);
            g.Ribbon(new Vector3(0.016f, 1.28f, -0.53f), new Vector3(0.016f, 1.439f, -1.96f), 0.016f, Vector3.right, 1);
            g.Ribbon(new Vector3(-0.016f, 1.28f, -0.53f), new Vector3(-0.016f, 1.439f, -1.96f), 0.016f, Vector3.left, 1);
        }

        private static void Rear(Builder g)
        {
            g.Polygon(new[] { new Vector3(-0.89f, 0.18f, -2.29f), new Vector3(0.89f, 0.18f, -2.29f),
                new Vector3(1.03f, 0.44f, -2.29f), new Vector3(1.03f, 0.81f, -2.29f),
                new Vector3(0.30f, 0.70f, -2.29f), new Vector3(-0.30f, 0.70f, -2.29f),
                new Vector3(-1.03f, 0.81f, -2.29f), new Vector3(-1.03f, 0.44f, -2.29f) }, 2, Vector3.back);
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 P(float x, float y, float z = -2.30f) => new Vector3(side * x, y, z);
                for (int bar = 0; bar < 3; bar++)
                    g.Ribbon(P(0.43f, 0.45f + bar * 0.062f, -2.317f), P(0.93f, 0.53f + bar * 0.065f, -2.317f), 0.027f, Vector3.back, 8);
                g.Ribbon(P(0.36f, 0.66f), P(0.92f, 0.81f, -2.22f), 0.045f, Vector3.back, 1);
                g.Ribbon(P(0.33f, 0.28f, -2.335f), P(0.83f, 0.19f, -2.335f), 0.035f, Vector3.back, 5);
                g.Ribbon(P(0.83f, 0.19f, -2.335f), P(1.00f, 0.07f, -2.40f), 0.035f, Vector3.back, 5);
                g.Prism(new[] { new Vector3(side * 0.85f - 0.025f, 0.77f, -2.12f), new Vector3(side * 0.85f - 0.025f, 1.39f, -2.08f),
                    new Vector3(side * 0.85f - 0.025f, 1.40f, -1.96f), new Vector3(side * 0.85f - 0.025f, 0.80f, -1.94f) }, Vector3.right * 0.05f, 0);
                g.Prism(new[] { new Vector3(side * 1.09f - 0.025f, 0.86f, -2.23f), new Vector3(side * 1.09f - 0.025f, 1.45f, -2.24f),
                    new Vector3(side * 1.09f - 0.025f, 1.46f, -1.92f), new Vector3(side * 1.09f - 0.025f, 1.28f, -1.86f) }, Vector3.right * 0.05f, 0);
                g.Exhaust(new Vector3(side * 0.105f, 0.405f, -2.34f));
            }
            float[] xs = { -1.09f, -0.72f, -0.28f, 0.28f, 0.72f, 1.09f };
            float WingY(float x) => Mathf.Abs(x) > 0.7f ? 1.44f : 1.35f;
            for (int i = 0; i < xs.Length - 1; i++)
            {
                float a = xs[i], b = xs[i + 1];
                g.Quad(new Vector3(a, WingY(a), -2.25f), new Vector3(b, WingY(b), -2.25f),
                    new Vector3(b, WingY(b) + 0.016f, -1.95f), new Vector3(a, WingY(a) + 0.016f, -1.95f), 0, Vector3.up);
                g.Quad(new Vector3(a, WingY(a) - 0.065f, -2.25f), new Vector3(b, WingY(b) - 0.065f, -2.25f),
                    new Vector3(b, WingY(b), -2.25f), new Vector3(a, WingY(a), -2.25f), 4, Vector3.back);
            }
            foreach (float x in new[] { -0.66f, -0.33f, 0, 0.33f, 0.66f })
                g.Prism(new[] { new Vector3(x - 0.016f, 0.06f, -2.45f), new Vector3(x - 0.016f, 0.26f, -2.24f),
                    new Vector3(x - 0.016f, 0.18f, -1.98f), new Vector3(x - 0.016f, 0.06f, -2.03f) }, Vector3.right * 0.032f, 4);
        }

        public static Mesh BuildWheel()
        {
            var g = new Builder(MaterialCount);
            float[] xs = { -0.14f, -0.105f, 0.105f, 0.14f }, radii = { 0.367f, 0.4f, 0.4f, 0.367f };
            for (int ring = 0; ring < 3; ring++) g.AxialBand(xs[ring], radii[ring], xs[ring + 1], radii[ring + 1], 9);
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 0.14f;
                g.AxialRing(x, 0.367f, 0.29f, 9, side);
                g.AxialRing(x + side * 0.003f, 0.29f, 0.275f, 5, side);
                g.AxialRing(x + side * 0.002f, 0.275f, 0, 2, side);
                for (int spoke = 0; spoke < 5; spoke++)
                {
                    float angle = spoke * Mathf.PI * 2 / 5;
                    Vector3 radial = new Vector3(0, Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector3 tangent = new Vector3(0, -Mathf.Sin(angle), Mathf.Cos(angle));
                    Vector3 center = Vector3.right * (x + side * 0.007f);
                    g.Polygon(new[] { center + radial * 0.065f - tangent * 0.035f, center + radial * 0.065f + tangent * 0.035f,
                        center + radial * 0.266f + tangent * 0.045f, center + radial * 0.266f - tangent * 0.018f }, 4, Vector3.right * side);
                    g.Ribbon(center + radial * 0.085f, center + radial * 0.247f - tangent * 0.014f, 0.012f, Vector3.right * side, 0);
                }
                g.AxialRing(x + side * 0.018f, 0.070f, 0.047f, 4, side);
                g.AxialRing(x + side * 0.020f, 0.047f, 0, 0, side);
            }
            return g.Finish("CubeDashSupercarWheel");
        }

        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<int>[] indices;
            public Builder(int count) { indices = new List<int>[count]; for (int i = 0; i < count; i++) indices[i] = new List<int>(); }
            private void Triangle(Vector3 a, Vector3 b, Vector3 c, int material, Vector3 outward, Vector3 shade = default)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f) return;
                bool reverse = Vector3.Dot(normal, outward) < 0;
                int first = vertices.Count; vertices.Add(a); vertices.Add(reverse ? c : b); vertices.Add(reverse ? b : c);
                Vector3 lighting = shade.sqrMagnitude > 0 ? shade.normalized : normal.normalized;
                if (Vector3.Dot(lighting, outward) < 0) lighting = -lighting;
                normals.Add(lighting); normals.Add(lighting); normals.Add(lighting);
                indices[material].Add(first); indices[material].Add(first + 1); indices[material].Add(first + 2);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int material, Vector3 outward)
            {
                Vector3 shade = Vector3.Cross(b - a, c - a) + Vector3.Cross(c - a, d - a);
                Triangle(a, b, c, material, outward, shade); Triangle(a, c, d, material, outward, shade);
            }
            public void Polygon(Vector3[] points, int material, Vector3 outward)
            { for (int i = 1; i < points.Length - 1; i++) Triangle(points[0], points[i], points[i + 1], material, outward); }
            public void Ribbon(Vector3 a, Vector3 b, float width, Vector3 normal, int material)
            {
                Vector3 side = Vector3.Cross(normal, b - a).normalized * width * 0.5f;
                Quad(a - side, b - side, b + side, a + side, material, normal);
            }
            public void Prism(Vector3[] outline, Vector3 extrusion, int material)
            {
                Polygon(outline, material, -extrusion); var end = new Vector3[outline.Length];
                Vector3 center = Vector3.zero;
                for (int i = 0; i < outline.Length; i++) { end[i] = outline[i] + extrusion; center += outline[i]; }
                center = center / outline.Length + extrusion * 0.5f;
                Polygon(end, material, extrusion);
                for (int i = 0; i < outline.Length; i++)
                {
                    int next = (i + 1) % outline.Length;
                    Vector3 normal = (outline[i] + outline[next] + end[i] + end[next]) * 0.25f - center;
                    Quad(outline[i], outline[next], end[next], end[i], material, normal);
                }
            }
            public void Box(Vector3 center, Vector3 size, int material)
            {
                Vector3 h = size * 0.5f;
                Prism(new[] { center + new Vector3(-h.x, -h.y, -h.z), center + new Vector3(h.x, -h.y, -h.z),
                    center + new Vector3(h.x, -h.y, h.z), center + new Vector3(-h.x, -h.y, h.z) }, Vector3.up * size.y, material);
            }
            private static Vector3 Radial(int index)
            { float a = index * Mathf.PI * 2 / 16; return new Vector3(0, Mathf.Cos(a), Mathf.Sin(a)); }
            public void AxialBand(float x1, float r1, float x2, float r2, int material)
            {
                for (int i = 0; i < 16; i++)
                {
                    Vector3 a = Radial(i), b = Radial(i + 1);
                    Quad(Vector3.right * x1 + a * r1, Vector3.right * x1 + b * r1,
                        Vector3.right * x2 + b * r2, Vector3.right * x2 + a * r2, material, (a + b) * 0.5f);
                }
            }
            public void AxialRing(float x, float outer, float inner, int material, int side)
            {
                for (int i = 0; i < 16; i++)
                {
                    Vector3 a = Radial(i), b = Radial(i + 1), center = Vector3.right * x;
                    Quad(center + a * outer, center + b * outer, center + b * inner, center + a * inner, material, Vector3.right * side);
                }
            }
            public void Exhaust(Vector3 center)
            {
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2 / 16, b = (i + 1) * Mathf.PI * 2 / 16;
                    Vector3 ra = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0), rb = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0);
                    Vector3 rear = center + Vector3.back * 0.08f, front = center + Vector3.forward * 0.08f;
                    Quad(rear + ra * 0.087f, rear + rb * 0.087f, rear + rb * 0.060f, rear + ra * 0.060f, 6, Vector3.back);
                    Quad(front + ra * 0.087f, rear + ra * 0.087f, rear + rb * 0.087f, front + rb * 0.087f, 4, (ra + rb) * 0.5f);
                    Quad(rear + ra * 0.060f, front + ra * 0.060f, front + rb * 0.060f, rear + rb * 0.060f, 2, -(ra + rb) * 0.5f);
                    Triangle(front, front + ra * 0.06f, front + rb * 0.06f, 2, Vector3.back);
                }
            }
            public Mesh Finish(string name)
            {
                Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.subMeshCount = indices.Length;
                for (int i = 0; i < indices.Length; i++) mesh.SetTriangles(indices[i], i);
                // Smooth only the wraparound glazing, not the sculpted low-poly painted panels.
                var glassNormals = new Dictionary<Vector3, Vector3>();
                foreach (int index in indices[3])
                {
                    glassNormals.TryGetValue(vertices[index], out Vector3 sum);
                    glassNormals[vertices[index]] = sum + normals[index];
                }
                foreach (int index in indices[3]) normals[index] = glassNormals[vertices[index]].normalized;
                mesh.uv = new Vector2[vertices.Count]; mesh.SetNormals(normals); mesh.RecalculateBounds(); return mesh;
            }
        }
    }
}
