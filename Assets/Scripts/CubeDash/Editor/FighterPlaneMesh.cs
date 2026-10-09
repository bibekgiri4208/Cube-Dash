using System.Collections.Generic;
using UnityEngine;

namespace CubeDash.Editor
{
    /// <summary>A faceted twin-engine, swept-wing fighter, with real hollow exhausts and canted fins.</summary>
    internal static class FighterPlaneMesh
    {
        public static Mesh Build()
        {
            var g = new Builder();
            g.Loft(new[] { -2.1f, -1.2f, 0.2f, 1.4f, 2.3f, 2.9f },
                new[] { 0.48f, 0.72f, 0.62f, 0.43f, 0.26f, 0.015f },
                new[] { 0.25f, 0.32f, 0.35f, 0.28f, 0.17f, 0.012f }, Vector3.up * 0.04f, 0, true);
            g.Loft(new[] { 0.65f, 1.05f, 1.6f, 2.05f },
                new[] { 0.20f, 0.27f, 0.21f, 0.04f }, new[] { 0.025f, 0.19f, 0.16f, 0.025f },
                Vector3.up * 0.35f, 1, true);
            for (int side = -1; side <= 1; side += 2)
            {
                g.Prism(new[]
                {
                    new Vector3(side * 0.42f, 0.08f, 1.25f), new Vector3(side * 2.65f, 0.12f, -0.55f),
                    new Vector3(side * 2.55f, 0.12f, -1.16f), new Vector3(side * 0.58f, 0.08f, -1.65f)
                }, Vector3.down * 0.09f, 0);
                g.Prism(new[]
                {
                    new Vector3(side * 0.24f, 0.14f, 2.0f), new Vector3(side * 0.86f, 0.09f, 0.35f),
                    new Vector3(side * 0.68f, 0.09f, -1.2f), new Vector3(side * 0.35f, 0.14f, 0.7f)
                }, Vector3.down * 0.12f, 0);
                g.Prism(new[]
                {
                    new Vector3(side * 0.42f, 0.15f, -1.3f), new Vector3(side * 1.65f, 0.18f, -1.7f),
                    new Vector3(side * 1.9f, 0.18f, -2.5f), new Vector3(side * 0.5f, 0.15f, -2.25f)
                }, Vector3.down * 0.07f, 0);
                g.Prism(new[]
                {
                    new Vector3(side * 0.47f, 0.28f, -1.25f), new Vector3(side * 0.9f, 1.38f, -1.6f),
                    new Vector3(side * 1.0f, 1.3f, -2.25f), new Vector3(side * 0.54f, 0.27f, -2.36f)
                }, Vector3.right * side * 0.05f, 0);
                Vector3 engine = new Vector3(side * 0.4f, -0.025f, 0);
                g.Loft(new[] { -2.45f, -2.28f, -1.15f }, new[] { 0.235f, 0.265f, 0.28f },
                    new[] { 0.235f, 0.265f, 0.28f }, engine, 2, false);
                g.Loft(new[] { -2.45f, -2.15f }, new[] { 0.18f, 0.16f },
                    new[] { 0.18f, 0.16f }, engine, 1, false, true);
                g.Rim(engine + Vector3.back * 2.45f, 0.235f, 0.18f, 2);
                g.Disc(engine + Vector3.back * 2.14f, 0.16f, 1, true);
                g.Disc(engine + Vector3.back * 2.17f, 0.105f, 3, true);
                // Angular intake fairings, with dark front-facing openings.
                g.Prism(new[]
                {
                    new Vector3(side * 0.47f, -0.02f, 1.03f), new Vector3(side * 0.77f, -0.04f, 0.9f),
                    new Vector3(side * 0.72f, -0.02f, -0.4f), new Vector3(side * 0.48f, -0.02f, -0.5f)
                }, Vector3.down * 0.25f, 0);
                g.Quad(new Vector3(side * 0.49f, -0.035f, 1.04f), new Vector3(side * 0.75f, -0.05f, 0.91f),
                    new Vector3(side * 0.75f, -0.24f, 0.91f), new Vector3(side * 0.49f, -0.24f, 1.04f), 1, side > 0);
            }
            return g.Finish();
        }

        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int>[] indices = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            private void Triangle(Vector3 a, Vector3 b, Vector3 c, int material, bool reverse = false)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(reverse ? c : b); vertices.Add(reverse ? b : c);
                indices[material].Add(start); indices[material].Add(start + 1); indices[material].Add(start + 2);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int material, bool reverse = false)
            { Triangle(a, b, c, material, reverse); Triangle(a, c, d, material, reverse); }
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
            public void Loft(float[] z, float[] widths, float[] heights, Vector3 offset, int material, bool caps, bool reverse = false)
            {
                const int sides = 8;
                Vector3 Point(int ring, int side)
                {
                    float angle = side * Mathf.PI * 2 / sides;
                    return offset + new Vector3(Mathf.Cos(angle) * widths[ring], Mathf.Sin(angle) * heights[ring], z[ring]);
                }
                for (int ring = 0; ring < z.Length - 1; ring++)
                    for (int side = 0; side < sides; side++)
                        Quad(Point(ring, side), Point(ring, side + 1), Point(ring + 1, side + 1), Point(ring + 1, side), material, reverse);
                if (!caps) return;
                for (int side = 0; side < sides; side++)
                {
                    Triangle(offset + Vector3.forward * z[0], Point(0, side + 1), Point(0, side), material);
                    int last = z.Length - 1;
                    Triangle(offset + Vector3.forward * z[last], Point(last, side), Point(last, side + 1), material);
                }
            }
            public void Rim(Vector3 center, float outer, float inner, int material)
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4, b = (i + 1) * Mathf.PI / 4;
                    Vector3 first = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0), next = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0);
                    Quad(center + first * outer, center + first * inner, center + next * inner, center + next * outer, material);
                }
            }
            public void Disc(Vector3 center, float radius, int material, bool rear)
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4, b = (i + 1) * Mathf.PI / 4;
                    Triangle(center, center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius,
                        center + new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0) * radius, material, rear);
                }
            }
            public Mesh Finish()
            {
                Mesh mesh = new Mesh { name = "CubeDashFighter" };
                mesh.SetVertices(vertices);
                mesh.subMeshCount = indices.Length;
                for (int i = 0; i < indices.Length; i++) mesh.SetTriangles(indices[i], i);
                mesh.uv = new Vector2[vertices.Count];
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
