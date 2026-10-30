using System.Collections.Generic;
using UnityEngine;

namespace CubeDash.Editor
{
    /// <summary>Reference-inspired shark costume on the exact existing bevelled cube, not a larger collision body.</summary>
    internal static class SharkCubeMesh
    {
        public static Mesh Build(Mesh cube)
        {
            var g = new Builder();
            Vector3[] points = cube.vertices; int[] triangles = cube.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = points[triangles[i]], b = points[triangles[i + 1]], c = points[triangles[i + 2]];
                g.Triangle(a, b, c, 0);
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                // The white wrap follows the original lower bevels. The flat front gets its own jagged belly line.
                if (normal.z < -0.99f) continue;
                var white = Clip(new[] { a, b, c }, Vector3.up, -0.29f, true);
                for (int v = 0; v < white.Count; v++)
                {
                    Vector3 p = white[v] + normal * 0.0007f;
                    p.y = Mathf.Max(cube.bounds.min.y, p.y); white[v] = p;
                }
                g.Polygon(white, 1);
            }
            float[] x = { -0.445f, -0.34f, -0.29f, -0.20f, -0.07f, 0.05f, 0.15f, 0.27f, 0.33f, 0.445f };
            float[] y = { -0.29f, -0.17f, -0.23f, -0.18f, -0.135f, -0.15f, -0.20f, -0.27f, -0.20f, -0.29f };
            for (int i = 0; i < x.Length - 1; i++)
                g.Face(new[] { new Vector3(x[i], -0.445f, -0.501f), new Vector3(x[i + 1], -0.445f, -0.501f),
                    new Vector3(x[i + 1], y[i + 1], -0.501f), new Vector3(x[i], y[i], -0.501f) }, Vector3.back, 1);

            // Slightly swept, chamfered dorsal fin; its narrow cap gives it real thickness from every camera angle.
            g.Dorsal(new[] { new Vector2(-0.18f, 0.495f), new Vector2(-0.075f, 0.65f), new Vector2(0.10f, 0.80f),
                new Vector2(0.19f, 0.825f), new Vector2(0.19f, 0.67f), new Vector2(0.245f, 0.495f) });
            for (int side = -1; side <= 1; side += 2)
            {
                g.SideFin(side);
                for (int row = 0; row < 2; row++)
                {
                    float inner = side * 0.32f, outer = side * 0.43f, top = -0.015f - row * 0.085f;
                    g.Face(new[] { new Vector3(inner, top, -0.502f), new Vector3(outer, top - 0.045f, -0.502f),
                        new Vector3(outer, top - 0.078f, -0.502f), new Vector3(inner, top - 0.033f, -0.502f) }, Vector3.back, 2);
                }
            }
            return g.Finish();
        }

        private static List<Vector3> Clip(IList<Vector3> input, Vector3 normal, float distance, bool below)
        {
            var output = new List<Vector3>();
            if (input.Count == 0) return output;
            Vector3 previous = input[input.Count - 1];
            float previousDistance = Vector3.Dot(previous, normal) - distance;
            foreach (Vector3 current in input)
            {
                float currentDistance = Vector3.Dot(current, normal) - distance;
                bool currentInside = below ? currentDistance <= 0 : currentDistance >= 0;
                bool previousInside = below ? previousDistance <= 0 : previousDistance >= 0;
                if (currentInside != previousInside)
                    output.Add(Vector3.LerpUnclamped(previous, current, previousDistance / (previousDistance - currentDistance)));
                if (currentInside) output.Add(current);
                previous = current; previousDistance = currentDistance;
            }
            return output;
        }

        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int>[] indices = { new List<int>(), new List<int>(), new List<int>() };

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, int material)
            {
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 0.0000000001f) return;
                int first = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
                indices[material].Add(first); indices[material].Add(first + 1); indices[material].Add(first + 2);
            }

            public void Polygon(IList<Vector3> points, int material)
            { for (int i = 1; i < points.Count - 1; i++) Triangle(points[0], points[i], points[i + 1], material); }

            public void Face(Vector3[] points, Vector3 normal, int material)
            {
                if (Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), normal) < 0)
                    System.Array.Reverse(points);
                Polygon(points, material);
            }

            public void Dorsal(Vector2[] profile)
            {
                Vector2 middle = Vector2.zero;
                foreach (Vector2 point in profile) middle += point;
                middle /= profile.Length;
                var left = new Vector3[profile.Length]; var right = new Vector3[profile.Length];
                for (int i = 0; i < profile.Length; i++)
                {
                    Vector2 inner = Vector2.Lerp(middle, profile[i], 0.91f);
                    left[i] = new Vector3(-0.044f, inner.y, inner.x);
                    right[i] = new Vector3(0.044f, inner.y, inner.x);
                }
                Face((Vector3[])left.Clone(), Vector3.left, 0); Face((Vector3[])right.Clone(), Vector3.right, 0);
                for (int i = 0; i < profile.Length; i++)
                {
                    int next = (i + 1) % profile.Length;
                    Vector3 a = new Vector3(-0.027f, profile[i].y, profile[i].x);
                    Vector3 b = new Vector3(-0.027f, profile[next].y, profile[next].x);
                    Vector3 c = new Vector3(0.027f, profile[next].y, profile[next].x);
                    Vector3 d = new Vector3(0.027f, profile[i].y, profile[i].x);
                    Vector3 normal = (a + b + c + d) * 0.25f - new Vector3(0, middle.y, middle.x);
                    Face(new[] { a, b, c, d }, normal, 0);
                    Face(new[] { left[i], left[next], b, a }, (left[i] + left[next] + b + a) * 0.25f - new Vector3(0, middle.y, middle.x), 0);
                    Face(new[] { d, c, right[next], right[i] }, (d + c + right[next] + right[i]) * 0.25f - new Vector3(0, middle.y, middle.x), 0);
                }
            }

            public void SideFin(int side)
            {
                var outline = new[] { new Vector3(side * 0.48f, -0.335f, -0.29f), new Vector3(side * 0.73f, -0.375f, -0.29f),
                    new Vector3(side * 0.765f, -0.35f, -0.29f), new Vector3(side * 0.70f, -0.255f, -0.29f),
                    new Vector3(side * 0.60f, -0.16f, -0.29f), new Vector3(side * 0.48f, -0.12f, -0.29f) };
                Vector3 extrusion = Vector3.forward * 0.11f;
                var back = new Vector3[outline.Length];
                for (int i = 0; i < back.Length; i++) back[i] = outline[i] + extrusion;
                FinFace((Vector3[])outline.Clone(), Vector3.back, side);
                FinFace((Vector3[])back.Clone(), Vector3.forward, side);
                Vector3 center = new Vector3(side * 0.60f, -0.25f, -0.235f);
                for (int i = 0; i < outline.Length; i++)
                {
                    int next = (i + 1) % outline.Length;
                    var face = new[] { outline[i], back[i], back[next], outline[next] };
                    FinFace(face, (face[0] + face[1] + face[2] + face[3]) * 0.25f - center, side);
                }
            }

            private void FinFace(Vector3[] points, Vector3 normal, int side)
            {
                if (Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), normal) < 0)
                    System.Array.Reverse(points);
                Vector3 cut = new Vector3(-side * 0.18f, 1, 0);
                for (int i = 1; i < points.Length - 1; i++)
                {
                    var triangle = new[] { points[0], points[i], points[i + 1] };
                    Polygon(Clip(triangle, cut, -0.3964f, false), 0);
                    Polygon(Clip(triangle, cut, -0.3964f, true), 1);
                }
            }

            public Mesh Finish()
            {
                var mesh = new Mesh { name = "Shark Cube - Blue body, white belly, fins and gills" };
                mesh.SetVertices(vertices); mesh.subMeshCount = 3;
                for (int i = 0; i < 3; i++) mesh.SetTriangles(indices[i], i);
                mesh.uv = new Vector2[vertices.Count]; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
