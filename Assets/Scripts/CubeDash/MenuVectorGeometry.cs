using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Small alpha fringes smooth overlay UI independently of the world's camera anti-aliasing.</summary>
    internal static class MenuVectorGeometry
    {
        public static float FringeWidth(Graphic graphic)
        {
            Canvas canvas = graphic.canvas;
            float scale = canvas != null ? canvas.scaleFactor : 1;
            if (canvas != null)
                scale *= canvas.transform.InverseTransformVector(graphic.rectTransform.TransformVector(Vector3.right)).magnitude;
            return 1.1f / Mathf.Max(0.01f, scale);
        }

        public static void Polygon(VertexHelper mesh, Vector2[] points, Color[] colors, float fringe)
        {
            if (points.Length < 3) return;
            int first = mesh.currentVertCount;
            for (int i = 0; i < points.Length; i++) mesh.AddVert(points[i], colors[i], Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(first, first + i, first + i + 1);
            ContourFringe(mesh, points, colors, fringe);
        }

        public static void Polygon(VertexHelper mesh, Vector2[] points, Color color, float fringe)
        {
            var colors = new Color[points.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            Polygon(mesh, points, colors, fringe);
        }

        public static void Ring(VertexHelper mesh, Vector2[] outer, Vector2[] inner, Color color, float fringe)
        {
            int first = mesh.currentVertCount, count = outer.Length;
            for (int i = 0; i < count; i++)
            {
                mesh.AddVert(outer[i], color, Vector2.zero); mesh.AddVert(inner[i], color, Vector2.zero);
            }
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                mesh.AddTriangle(first + i * 2, first + next * 2, first + next * 2 + 1);
                mesh.AddTriangle(first + i * 2, first + next * 2 + 1, first + i * 2 + 1);
            }
            var colors = new Color[count];
            for (int i = 0; i < count; i++) colors[i] = color;
            ContourFringe(mesh, outer, colors, fringe);
            ContourFringe(mesh, inner, colors, -fringe); // Smooth the hole, not every internal tessellation edge.
        }

        private static void ContourFringe(VertexHelper mesh, Vector2[] points, Color[] colors, float width)
        {
            Vector2[] outside = OffsetContour(points, width);
            int first = mesh.currentVertCount;
            for (int i = 0; i < points.Length; i++)
            {
                Color transparent = colors[i]; transparent.a = 0;
                mesh.AddVert(points[i], colors[i], Vector2.zero); mesh.AddVert(outside[i], transparent, Vector2.zero);
            }
            for (int i = 0; i < points.Length; i++)
            {
                int next = (i + 1) % points.Length;
                mesh.AddTriangle(first + i * 2, first + i * 2 + 1, first + next * 2 + 1);
                mesh.AddTriangle(first + i * 2, first + next * 2 + 1, first + next * 2);
            }
        }

        public static Vector2[] OffsetContour(Vector2[] points, float distance)
        {
            var result = new Vector2[points.Length];
            float winding = Mathf.Sign(SignedArea(points));
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 before = (points[i] - points[(i + points.Length - 1) % points.Length]).normalized;
                Vector2 after = (points[(i + 1) % points.Length] - points[i]).normalized;
                Vector2 a = new Vector2(before.y, -before.x) * winding, b = new Vector2(after.y, -after.x) * winding;
                Vector2 miter = (a + b).normalized;
                result[i] = points[i] + miter * (distance / Mathf.Max(0.25f, Vector2.Dot(miter, b)));
            }
            return result;
        }

        public static Vector2[] ClipConvex(Vector2[] subject, Vector2[] boundary)
        {
            var output = new List<Vector2>(subject);
            float winding = Mathf.Sign(SignedArea(boundary));
            for (int edge = 0; edge < boundary.Length && output.Count != 0; edge++)
            {
                Vector2 a = boundary[edge], b = boundary[(edge + 1) % boundary.Length];
                var input = output; output = new List<Vector2>(input.Count + 1);
                Vector2 previous = input[input.Count - 1]; float previousDistance = Cross(b - a, previous - a) * winding;
                foreach (Vector2 current in input)
                {
                    float distance = Cross(b - a, current - a) * winding;
                    if ((distance >= 0) != (previousDistance >= 0))
                        output.Add(Vector2.LerpUnclamped(previous, current, previousDistance / (previousDistance - distance)));
                    if (distance >= 0) output.Add(current);
                    previous = current; previousDistance = distance;
                }
            }
            // Clipping through an existing corner can produce duplicate points, which have undefined edge normals.
            for (int i = output.Count - 1; i >= 0 && output.Count > 1; i--)
                if ((output[i] - output[(i + 1) % output.Count]).sqrMagnitude < 0.000001f) output.RemoveAt(i);
            return output.ToArray();
        }

        private static float SignedArea(Vector2[] points)
        {
            float area = 0;
            for (int i = 0; i < points.Length; i++) area += Cross(points[i], points[(i + 1) % points.Length]);
            return area * 0.5f;
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
