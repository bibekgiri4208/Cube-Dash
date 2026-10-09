using System.Collections.Generic;
using UnityEngine;

namespace CubeDash.Editor
{
    /// <summary>Low-poly blue cab-over tractor: split windshield, sleeper, twin stacks and tandem rear axles.</summary>
    internal static class TruckMesh
    {
        public static Mesh BuildBody()
        {
            var g = new Builder(8);
            // Materials: blue paint, navy band, chassis, chrome, glass, amber, white, red.
            g.Box(new Vector3(0, 0.67f, -0.45f), new Vector3(1.85f, 0.3f, 5.9f), 2);
            foreach (int side in new[] { -1, 1 })
            {
                g.Box(new Vector3(side * 0.65f, 0.92f, -1.3f), new Vector3(0.18f, 0.25f, 3.8f), 2);
                g.Box(new Vector3(side * 1.03f, 0.79f, -0.6f), new Vector3(0.5f, 0.7f, 1.2f), 3);
                g.Box(new Vector3(side * 1.03f, 1.16f, -0.6f), new Vector3(0.18f, 0.07f, 0.25f), 2);
                g.Box(new Vector3(side * 1.18f, 0.77f, 0.55f), new Vector3(0.16f, 0.75f, 0.48f), 2);
                for (int step = 0; step < 3; step++)
                    g.Box(new Vector3(side * 1.28f, 0.48f + step * 0.24f, 0.55f), new Vector3(0.25f, 0.06f, 0.44f), 3);
                g.Box(new Vector3(side * 1.11f, 1.12f, -2.35f), new Vector3(0.56f, 0.18f, 2.3f), 3);
                g.Box(new Vector3(side * 1.11f, 0.6f, -3.42f), new Vector3(0.59f, 0.85f, 0.08f), 2);
            }
            g.Box(new Vector3(0, 0.94f, -1.85f), new Vector3(1.2f, 0.13f, 0.7f), 3);
            g.Box(new Vector3(0, 1.02f, -1.85f), new Vector3(0.25f, 0.06f, 0.5f), 2);
            g.Prism(new[]
            {
                new Vector3(-1.15f, 1.1f, 0.35f), new Vector3(-1.15f, 1.1f, 2.5f),
                new Vector3(-1.15f, 2.4f, 2.5f), new Vector3(-1.15f, 3.4f, 2.22f),
                new Vector3(-1.15f, 3.4f, 0.35f)
            }, Vector3.right * 2.3f, 0);
            g.Box(new Vector3(0, 2.1f, 0.33f), new Vector3(2.3f, 0.72f, 0.035f), 1);
            g.Box(new Vector3(0, 2.92f, 0.3f), new Vector3(0.8f, 0.25f, 0.05f), 4);
            // Sloped split front glass; chrome rain visor above it.
            for (int side = -1; side <= 1; side += 2)
            {
                float left = side < 0 ? -1.03f : 0.055f, right = side < 0 ? -0.055f : 1.03f;
                g.Quad(new Vector3(left, 2.52f, 2.477f), new Vector3(right, 2.52f, 2.477f),
                    new Vector3(right, 3.23f, 2.279f), new Vector3(left, 3.23f, 2.279f), 4);
                g.Box(new Vector3(side * 0.79f, 2.35f, 2.515f), new Vector3(0.36f, 0.035f, 0.045f), 3);
                g.Box(new Vector3(side * 0.91f, 1.49f, 2.53f), new Vector3(0.36f, 0.24f, 0.075f), 3);
                for (int lamp = -1; lamp <= 1; lamp += 2)
                    g.Box(new Vector3(side * 0.91f + lamp * 0.08f, 1.49f, 2.575f), new Vector3(0.135f, 0.17f, 0.025f), 6);
                g.Box(new Vector3(side * 0.94f, 0.91f, 2.675f), new Vector3(0.13f, 0.15f, 0.025f), 5);
                g.Box(new Vector3(side * 1.171f, 2.1f, 1.45f), new Vector3(0.045f, 0.72f, 1.85f), 1);
                // Door outline, glass, handle and projecting rectangular mirrors.
                g.Box(new Vector3(side * 1.2f, 2.26f, 1.73f), new Vector3(0.03f, 1.58f, 0.92f), 2);
                g.Box(new Vector3(side * 1.225f, 2.22f, 1.73f), new Vector3(0.025f, 1.46f, 0.85f), 0);
                g.Box(new Vector3(side * 1.245f, 2.83f, 1.73f), new Vector3(0.03f, 0.66f, 0.75f), 4);
                g.Box(new Vector3(side * 1.25f, 1.94f, 1.43f), new Vector3(0.05f, 0.08f, 0.16f), 3);
                g.Box(new Vector3(side * 1.29f, 2.12f, 1.19f), new Vector3(0.045f, 1.43f, 0.045f), 3);
                g.Box(new Vector3(side * 1.3f, 2.81f, 2.18f), new Vector3(0.3f, 0.05f, 0.05f), 2);
                g.Box(new Vector3(side * 1.47f, 2.66f, 2.18f), new Vector3(0.075f, 0.47f, 0.22f), 2);
                g.Box(new Vector3(side * 1.515f, 2.66f, 2.18f), new Vector3(0.015f, 0.38f, 0.16f), 4);
                g.Box(new Vector3(side * 1.18f, 2.92f, 0.73f), new Vector3(0.06f, 0.3f, 0.2f), 4);
                g.Box(new Vector3(side * 1.19f, 1.23f, 0.45f), new Vector3(0.07f, 0.08f, 0.14f), 5);
                g.Box(new Vector3(side * 1.19f, 3.25f, 0.45f), new Vector3(0.07f, 0.07f, 0.14f), 5);
                // Tall chrome exhaust pipes, with visibly dark hollow mouths.
                Vector3 stack = new Vector3(side * 0.96f, 2.76f, 0.14f);
                g.Cylinder(stack, 0.11f, 2.85f, false, 3, false);
                g.Rim(new Vector3(stack.x, 4.185f, stack.z), 0.11f, 0.083f, 3);
                g.Cylinder(new Vector3(stack.x, 4.14f, stack.z), 0.083f, 0.02f, false, 2);
                g.Box(new Vector3(stack.x, 1.34f, 0.23f), new Vector3(0.25f, 0.24f, 0.3f), 3);
                // Two air horns on the roof.
                g.Box(new Vector3(side * 0.73f, 3.47f, 1.04f), new Vector3(0.1f, 0.1f, 0.55f), 3);
                g.Box(new Vector3(side * 0.73f, 3.5f, 1.34f), new Vector3(0.22f, 0.15f, 0.08f), 3);
                g.Box(new Vector3(side * 0.73f, 3.5f, 1.39f), new Vector3(0.16f, 0.1f, 0.015f), 2);
            }
            g.Box(new Vector3(0, 3.35f, 2.28f), new Vector3(2.43f, 0.17f, 0.22f), 3);
            for (int lamp = -2; lamp <= 2; lamp++)
            {
                g.Box(new Vector3(lamp * 0.43f, 3.48f, 2.07f), new Vector3(0.19f, 0.12f, 0.22f), 0);
                g.Box(new Vector3(lamp * 0.43f, 3.5f, 2.19f), new Vector3(0.13f, 0.09f, 0.045f), 5);
            }
            g.Box(new Vector3(0, 0.98f, 2.58f), new Vector3(2.4f, 0.41f, 0.18f), 3);
            g.Box(new Vector3(0, 1.83f, 2.525f), new Vector3(0.92f, 1.16f, 0.07f), 3);
            g.Box(new Vector3(0, 1.83f, 2.57f), new Vector3(0.81f, 1.04f, 0.03f), 2);
            for (int bar = 0; bar < 12; bar++)
                g.Box(new Vector3(0, 1.34f + bar * 0.088f, 2.6f), new Vector3(0.79f, 0.026f, 0.024f), 3);
            foreach (float x in new[] { -0.4f, 0, 0.4f })
                g.Box(new Vector3(x, 1.83f, 2.615f), new Vector3(0.035f, 1.04f, 0.03f), 3);
            g.Box(new Vector3(0, 0.86f, -3.46f), new Vector3(1.6f, 0.29f, 0.08f), 2);
            foreach (float x in new[] { -0.55f, 0.55f })
                g.Box(new Vector3(x, 0.89f, -3.51f), new Vector3(0.23f, 0.17f, 0.025f), 7);
            g.Box(new Vector3(0, 0.89f, -3.51f), new Vector3(0.2f, 0.14f, 0.025f), 6);
            return g.Finish("CubeDashTruck");
        }

        public static Mesh BuildWheel()
        {
            var g = new Builder(3);
            g.Cylinder(Vector3.zero, 0.58f, 0.36f, true, 0);
            foreach (int side in new[] { -1, 1 })
            {
                g.Cylinder(Vector3.right * side * 0.185f, 0.395f, 0.03f, true, 1);
                g.Cylinder(Vector3.right * side * 0.206f, 0.30f, 0.02f, true, 2);
                g.Cylinder(Vector3.right * side * 0.23f, 0.235f, 0.04f, true, 1);
                g.Cylinder(Vector3.right * side * 0.26f, 0.105f, 0.04f, true, 2);
                for (int lug = 0; lug < 8; lug++)
                {
                    float angle = lug * Mathf.PI / 4;
                    g.Box(new Vector3(side * 0.253f, Mathf.Cos(angle) * 0.27f, Mathf.Sin(angle) * 0.27f), Vector3.one * 0.04f, 1);
                }
            }
            return g.Finish("CubeDashTruckWheel");
        }

        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int>[] indices;
            public Builder(int materials)
            {
                indices = new List<int>[materials];
                for (int i = 0; i < materials; i++) indices[i] = new List<int>();
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
            public void Rim(Vector3 center, float outer, float inner, int material)
            {
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 6, b = (i + 1) * Mathf.PI / 6;
                    Vector3 first = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)), next = new Vector3(Mathf.Sin(b), 0, Mathf.Cos(b));
                    Quad(center + first * outer, center + next * outer, center + next * inner, center + first * inner, material);
                }
            }
            public void Cylinder(Vector3 center, float radius, float length, bool horizontal, int material, bool caps = true)
            {
                const int sides = 12;
                Vector3 axis = horizontal ? Vector3.right : Vector3.up;
                Vector3 Point(int i)
                {
                    float angle = i * Mathf.PI * 2 / sides;
                    return horizontal ? new Vector3(0, Mathf.Cos(angle), Mathf.Sin(angle)) * radius
                        : new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
                }
                Vector3 start = center - axis * length * 0.5f, end = center + axis * length * 0.5f;
                for (int i = 0; i < sides; i++)
                {
                    Vector3 a = Point(i), b = Point(i + 1);
                    Quad(start + a, start + b, end + b, end + a, material);
                    if (caps)
                    {
                        Triangle(start, start + b, start + a, material);
                        Triangle(end, end + a, end + b, material);
                    }
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
