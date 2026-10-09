using System.Collections.Generic;
using UnityEngine;

namespace CubeDash.Editor
{
    /// <summary>Solid, readable pickup silhouettes: a horseshoe magnet, pointed shield and extruded 2x.</summary>
    internal static class PowerUpIconMeshes
    {
        public static Mesh Magnet()
        {
            var g = new Builder();
            const int steps = 16;
            for (int i = 0; i < steps; i++)
            {
                float a = Mathf.PI + i * Mathf.PI / steps, b = Mathf.PI + (i + 1) * Mathf.PI / steps;
                Vector2 Point(float angle, float radius) => new Vector2(Mathf.Cos(angle) * radius, 0.09f + Mathf.Sin(angle) * radius);
                g.Prism(new[] { Point(a, 0.38f), Point(b, 0.38f), Point(b, 0.2f), Point(a, 0.2f) }, -0.12f, 0.12f, 0);
            }
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 0.29f;
                g.Prism(new[] { new Vector2(x - 0.09f, 0.09f), new Vector2(x + 0.09f, 0.09f),
                    new Vector2(x + 0.09f, 0.45f), new Vector2(x - 0.09f, 0.45f) }, -0.12f, 0.12f, 0);
                g.Bar(new Vector2(side * 0.29f, 0.53f), new Vector2(0.18f, 0.16f), 0, -0.125f, 0.125f, 2);
                g.Bar(new Vector2(side * 0.29f, 0.453f), new Vector2(0.184f, 0.027f), 0, -0.128f, 0.128f, 1);
            }
            return g.Finish("CubeDashMagnet");
        }

        public static Mesh Shield()
        {
            var g = new Builder();
            Vector2[] outline = { new Vector2(-0.43f, 0.48f), new Vector2(0, 0.55f), new Vector2(0.43f, 0.48f),
                new Vector2(0.38f, -0.05f), new Vector2(0, -0.52f), new Vector2(-0.38f, -0.05f) };
            g.Prism(outline, -0.105f, 0.105f, 1);
            Vector2[] inner = new Vector2[outline.Length];
            for (int i = 0; i < inner.Length; i++) inner[i] = outline[i] * 0.84f;
            g.Prism(inner, 0.106f, 0.135f, 0);
            g.Bar(new Vector2(0, 0.09f), new Vector2(0.095f, 0.5f), 0, 0.136f, 0.178f, 2);
            g.Bar(new Vector2(0, 0.14f), new Vector2(0.4f, 0.095f), 0, 0.179f, 0.192f, 2);
            // A raised emblem on both sides keeps the model recognisable while it sways.
            g.Prism(inner, -0.135f, -0.106f, 0);
            g.Bar(new Vector2(0, 0.09f), new Vector2(0.095f, 0.5f), 0, -0.178f, -0.136f, 2);
            g.Bar(new Vector2(0, 0.14f), new Vector2(0.4f, 0.095f), 0, -0.192f, -0.179f, 2);
            return g.Finish("CubeDashShieldPickup");
        }

        public static Mesh Multiplier()
        {
            var g = new Builder();
            void Stroke(Vector2 position, Vector2 size, float rotation = 0)
            {
                g.Bar(position, size, rotation, -0.1f, 0.1f, 1);
                g.Bar(position, size * 0.84f, rotation, 0.101f, 0.132f, 0);
                g.Bar(position, size * 0.84f, rotation, -0.132f, -0.101f, 0);
            }
            Stroke(new Vector2(-0.25f, 0.35f), new Vector2(0.38f, 0.11f));
            Stroke(new Vector2(-0.115f, 0.19f), new Vector2(0.11f, 0.34f));
            Stroke(new Vector2(-0.25f, 0.035f), new Vector2(0.38f, 0.11f));
            Stroke(new Vector2(-0.385f, -0.125f), new Vector2(0.11f, 0.34f));
            Stroke(new Vector2(-0.25f, -0.285f), new Vector2(0.38f, 0.11f));
            Stroke(new Vector2(0.28f, -0.03f), new Vector2(0.12f, 0.56f), 45);
            g.Bar(new Vector2(0.28f, -0.03f), new Vector2(0.12f, 0.56f), -45, -0.105f, 0.14f, 0);
            return g.Finish("CubeDashDoublePointsPickup");
        }

        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>() };
            private void Triangle(Vector3 a, Vector3 b, Vector3 c, int material, bool reverse = false)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(reverse ? c : b); vertices.Add(reverse ? b : c);
                triangles[material].Add(start); triangles[material].Add(start + 1); triangles[material].Add(start + 2);
            }
            public void Prism(Vector2[] outline, float back, float front, int material)
            {
                Vector2 center = Vector2.zero;
                float area = 0;
                for (int i = 0; i < outline.Length; i++)
                {
                    center += outline[i];
                    Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                    area += a.x * b.y - b.x * a.y;
                }
                center /= outline.Length;
                bool reverse = area < 0;
                for (int i = 0; i < outline.Length; i++)
                {
                    Vector2 a = outline[i], b = outline[(i + 1) % outline.Length];
                    Vector3 a0 = new Vector3(a.x, a.y, back), b0 = new Vector3(b.x, b.y, back);
                    Vector3 a1 = new Vector3(a.x, a.y, front), b1 = new Vector3(b.x, b.y, front);
                    Triangle(new Vector3(center.x, center.y, back), a0, b0, material, !reverse);
                    Triangle(new Vector3(center.x, center.y, front), a1, b1, material, reverse);
                    Triangle(a0, b0, b1, material, reverse); Triangle(a0, b1, a1, material, reverse);
                }
            }
            public void Bar(Vector2 center, Vector2 size, float rotation, float back, float front, int material)
            {
                Vector2 half = size * 0.5f;
                float bevel = Mathf.Min(size.x, size.y) * 0.12f;
                Vector2[] outline = { new Vector2(-half.x + bevel, -half.y), new Vector2(half.x - bevel, -half.y),
                    new Vector2(half.x, -half.y + bevel), new Vector2(half.x, half.y - bevel), new Vector2(half.x - bevel, half.y),
                    new Vector2(-half.x + bevel, half.y), new Vector2(-half.x, half.y - bevel), new Vector2(-half.x, -half.y + bevel) };
                Quaternion turn = Quaternion.Euler(0, 0, rotation);
                for (int i = 0; i < outline.Length; i++) outline[i] = center + (Vector2)(turn * outline[i]);
                Prism(outline, back, front, material);
            }
            public Mesh Finish(string name)
            {
                Mesh mesh = new Mesh { name = name };
                mesh.SetVertices(vertices); mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
                mesh.uv = new Vector2[vertices.Count]; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                Vector3 center = mesh.bounds.center;
                for (int i = 0; i < vertices.Count; i++) vertices[i] -= center;
                mesh.SetVertices(vertices); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
