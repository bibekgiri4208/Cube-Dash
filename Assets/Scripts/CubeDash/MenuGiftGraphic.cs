using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Illustrated, dimensional present and a deterministic celebration, drawn as one antialiased UI mesh.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuGiftGraphic : MaskableGraphic
    {
        private static readonly Color Ink = new Color(0.25f, 0.065f, 0.12f);
        private static readonly Color Gold = new Color(1, 0.74f, 0.18f);
        private static readonly Color Cream = new Color(1, 0.96f, 0.74f);
        private float frame;
        private Vector2 origin, offset;
        private Vector2 shapeScale = Vector2.one;
        private float fit, rotation, opacity = 1;

        public float Frame
        {
            get => frame;
            set { if (frame == value) return; frame = Mathf.Max(0, value); SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            fit = Mathf.Min(rect.width / 560, rect.height / 300);
            origin = rect.center + Vector2.down * (12 * fit);
            offset = Vector2.zero; shapeScale = Vector2.one; rotation = 0; opacity = 1;
            float anticipation = Phase(frame, 0.12f, 0.64f);
            float pop = Phase(frame, 0.64f, 1.18f);
            float jump = Mathf.Sin(pop * Mathf.PI) * 17;
            float light = Ease(Phase(frame, 0.61f, 0.93f));

            // Soft lighting and a grounded contact shadow, rather than a flat rectangle floating in space.
            Glow(mesh, new Vector2(0, 12), new Vector2(172, 135), new Color(1, 0.76f, 0.23f, 0.09f + light * 0.23f));
            Glow(mesh, new Vector2(0, -104), new Vector2(107 - jump, 13), new Color(0.20f, 0.035f, 0.08f, 0.19f));
            Rays(mesh, light);
            Celebration(mesh);

            float squeeze = Mathf.Sin(anticipation * Mathf.PI) * (1 - pop);
            shapeScale = new Vector2(1 + 0.07f * squeeze, 1 - 0.09f * squeeze);
            rotation = Mathf.Sin(anticipation * Mathf.PI * 5) * 3.5f * squeeze;
            offset = new Vector2(0, jump - 5 * squeeze);
            Body(mesh, light);
            RestingCoins(mesh, light);

            Vector2 bodyOffset = offset;
            float lid = Ease(Phase(frame, 0.64f, 1.27f));
            offset += new Vector2(92 * lid, 72 * lid + Mathf.Sin(lid * Mathf.PI) * 26);
            rotation -= 32 * lid;
            shapeScale *= Mathf.Lerp(1, 0.84f, lid);
            opacity = 1 - Ease(Phase(frame, 1.15f, 1.65f));
            Lid(mesh);
            offset = bodyOffset; shapeScale = Vector2.one; rotation = 0; opacity = 1;
        }

        private void Body(VertexHelper mesh, float light)
        {
            // Three distinct planes, satin highlights, inset seams and a real dark opening under the lid.
            Panel(mesh, new[] { V(-88, 11), V(53, -5), V(96, 23), V(-43, 40) }, Ink);
            Gradient(mesh, new[] { V(-82, 11), V(53, -2), V(88, 23), V(-43, 34) },
                new Color(0.43f, 0.06f, 0.15f), new Color(0.15f, 0.025f, 0.07f));
            Glow(mesh, V(0, 18), V(67, 23), new Color(1, 0.81f, 0.27f, light * 0.8f));
            Panel(mesh, new[] { V(52, -97), V(98, -69), V(98, 26), V(52, -2) }, Ink);
            Gradient(mesh, new[] { V(57, -89), V(92, -66), V(92, 17), V(57, -6) },
                new Color(0.71f, 0.025f, 0.18f), new Color(0.46f, 0.02f, 0.12f));
            Panel(mesh, new[] { V(-89, -81), V(56, -97), V(56, -2), V(-89, 14) }, Ink);
            Gradient(mesh, new[] { V(-83, -76), V(50, -90), V(50, -7), V(-83, 8) },
                new Color(1, 0.24f, 0.39f), new Color(0.83f, 0.055f, 0.22f));
            Panel(mesh, new[] { V(-79, 3), V(46, -10), V(46, -15), V(-79, -2) }, new Color(1, 0.52f, 0.58f));
            Panel(mesh, new[] { V(-30, -84), V(-2, -87), V(-2, 2), V(-30, 5) }, new Color(0.64f, 0.30f, 0.04f));
            Gradient(mesh, new[] { V(-25, -82), V(-6, -84), V(-6, 1), V(-25, 3) }, Cream, Gold);
            Panel(mesh, new[] { V(-22, -82), V(-18, -82), V(-18, 2), V(-22, 3) }, new Color(1, 0.98f, 0.76f));
            Panel(mesh, new[] { V(-83, -46), V(50, -61), V(50, -40), V(-83, -25) }, new Color(0.81f, 0.41f, 0.065f));
            Gradient(mesh, new[] { V(-83, -41), V(50, -56), V(50, -42), V(-83, -27) }, Cream, Gold);
            Panel(mesh, new[] { V(57, -56), V(92, -33), V(92, -17), V(57, -40) }, new Color(0.95f, 0.60f, 0.09f));
            // A small embossed star seal finishes the front instead of adding another text label.
            Disc(mesh, V(-15, -39), V(18, 18), Ink);
            Disc(mesh, V(-15, -38), V(14, 14), Gold);
            Star(mesh, V(-15, -38), 9, Cream);
        }

        private void Lid(VertexHelper mesh)
        {
            Panel(mesh, new[] { V(-97, 22), V(54, 5), V(107, 39), V(-42, 57) }, Ink);
            Gradient(mesh, new[] { V(-87, 23), V(53, 10), V(96, 38), V(-42, 51) },
                new Color(1, 0.47f, 0.55f), new Color(0.96f, 0.18f, 0.34f));
            Panel(mesh, new[] { V(-97, 22), V(54, 5), V(54, -15), V(-97, 2) }, Ink);
            Gradient(mesh, new[] { V(-91, 17), V(49, 2), V(49, -9), V(-91, 7) },
                new Color(1, 0.38f, 0.48f), new Color(0.82f, 0.07f, 0.23f));
            Panel(mesh, new[] { V(54, -15), V(107, 20), V(107, 39), V(54, 5) }, Ink);
            Panel(mesh, new[] { V(59, -5), V(101, 23), V(101, 31), V(59, 3) }, new Color(0.79f, 0.055f, 0.21f));
            Gradient(mesh, new[] { V(-30, 14), V(-3, 11), V(48, 44), V(22, 48) }, Cream, Gold);
            Panel(mesh, new[] { V(-29, 14), V(-5, 11), V(-5, -9), V(-29, -6) }, Gold);
            // Curved, thick satin loops with dark edging and a cream specular seam.
            Bow(mesh, V(-10, 46), V(-94, 132), V(-91, 41), V(-10, 46));
            Bow(mesh, V(-5, 47), V(51, 121), V(83, 58), V(-5, 47));
            Panel(mesh, new[] { V(-15, 44), V(-41, 20), V(-18, 24), V(-8, 12), V(3, 47) }, new Color(0.96f, 0.65f, 0.11f));
            Panel(mesh, new[] { V(-2, 47), V(18, 17), V(22, 30), V(41, 25), V(8, 49) }, Gold);
            Disc(mesh, V(-7, 48), V(18, 13), Ink);
            Disc(mesh, V(-7, 50), V(14, 10), Gold);
            Disc(mesh, V(-10, 53), V(8, 3), Cream);
        }

        private void Bow(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            Curve(mesh, a, b, c, d, 15, Ink);
            Curve(mesh, a, b, c, d, 10, Gold);
            Curve(mesh, a + V(0, 3), b + V(0, 3), c + V(0, 3), d + V(0, 3), 2, Cream);
        }

        private void RestingCoins(VertexHelper mesh, float light)
        {
            if (light <= 0) return;
            float grow = Ease(Phase(frame, 0.76f, 1.16f));
            Coin(mesh, V(-40, 23 + 12 * grow), 18 * grow, 0.84f, -14);
            Coin(mesh, V(30, 12 + 17 * grow), 21 * grow, 0.92f, 12);
            Coin(mesh, V(-4, 23 + 24 * grow), 24 * grow, 1, -5);
        }

        private void Rays(VertexHelper mesh, float light)
        {
            float burst = Mathf.Sin(Phase(frame, 0.64f, 1.65f) * Mathf.PI);
            if (burst <= 0) return;
            for (int i = 0; i < 10; i++)
            {
                float angle = (18 + i * 16) * Mathf.Deg2Rad;
                Vector2 direction = V(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 side = V(-direction.y, direction.x);
                Panel(mesh, new[] { V(0, 14) + direction * 43, V(0, 14) + direction * (128 + burst * 27) + side * 7,
                    V(0, 14) + direction * (128 + burst * 27) - side * 7 }, new Color(1, 0.73f, 0.16f, burst * light * 0.13f));
            }
        }

        private void Celebration(VertexHelper mesh)
        {
            for (int i = 0; i < 14; i++)
            {
                float t = Phase(frame, 0.68f + i * 0.024f, 1.73f + i * 0.024f);
                if (t <= 0 || t >= 1) continue;
                float spread = (i % 7 - 3) / 3f;
                Vector2 point = Bezier(V(-8, 24), V(spread * 170, 205 + (i % 3) * 13), V(spread * 48, -142), t);
                float size = (17 + i % 3 * 3) * Ease(Mathf.Clamp01(t * 6)) * (1 - Ease(Phase(t, 0.77f, 1)));
                float spin = Mathf.Max(0.2f, Mathf.Abs(Mathf.Cos(t * Mathf.PI * 3 + i)));
                Coin(mesh, point, size, spin, spread * 24 + t * 70);
            }
            for (int i = 0; i < 20; i++)
            {
                float t = Phase(frame, 0.65f + i % 4 * 0.025f, 1.80f + i % 4 * 0.07f);
                if (t <= 0 || t >= 1) continue;
                float angle = (19 + i * 137.5f) * Mathf.Deg2Rad;
                Vector2 point = V(Mathf.Cos(angle) * 165 * t, 32 + Mathf.Sin(angle) * 105 * t + 128 * t - 150 * t * t);
                float size = (i % 3 == 0 ? 6 : 3.5f) * Mathf.Sin(t * Mathf.PI);
                Color tint = i % 3 == 0 ? Gold : i % 3 == 1 ? new Color(1, 0.23f, 0.42f) : Cream;
                tint.a = 1 - Ease(Phase(t, 0.68f, 1));
                Spark(mesh, point, size, tint, angle + t * 7);
            }
            float shine = Ease(Phase(frame, 1.1f, 1.6f));
            for (int i = 0; i < 5; i++)
            {
                float angle = (25 + i * 32) * Mathf.Deg2Rad;
                Spark(mesh, V(Mathf.Cos(angle) * 117, 15 + Mathf.Sin(angle) * 96), (i % 2 == 0 ? 7 : 4) * shine,
                    new Color(1, 0.76f, 0.16f, shine * 0.9f), 0);
            }
        }

        private void Coin(VertexHelper mesh, Vector2 center, float size, float spin, float tilt)
        {
            if (size < 0.1f) return;
            Vector2 previousOffset = offset, previousScale = shapeScale; float previousRotation = rotation;
            offset += center; shapeScale = V(spin, 1); rotation = tilt;
            Disc(mesh, V(3, -2), V(size, size), new Color(0.49f, 0.22f, 0.025f));
            Disc(mesh, Vector2.zero, V(size, size), new Color(0.75f, 0.40f, 0.035f));
            Disc(mesh, V(-1, 2), V(size * 0.86f, size * 0.86f), Cream);
            Disc(mesh, Vector2.zero, V(size * 0.71f, size * 0.71f), Gold);
            Star(mesh, V(0, -1), size * 0.52f, new Color(0.80f, 0.42f, 0.045f));
            Star(mesh, V(-1, 1), size * 0.43f, Cream);
            offset = previousOffset; shapeScale = previousScale; rotation = previousRotation;
        }

        private Vector2 Map(Vector2 point)
        {
            point = Vector2.Scale(point, shapeScale);
            float angle = rotation * Mathf.Deg2Rad;
            point = V(point.x * Mathf.Cos(angle) - point.y * Mathf.Sin(angle), point.x * Mathf.Sin(angle) + point.y * Mathf.Cos(angle));
            return origin + (point + offset) * fit;
        }

        private void Panel(VertexHelper mesh, Vector2[] points, Color tint)
        {
            if (opacity <= 0) return;
            for (int i = 0; i < points.Length; i++) points[i] = Map(points[i]);
            tint.a *= opacity;
            MenuVectorGeometry.Polygon(mesh, points, tint * color, MenuVectorGeometry.FringeWidth(this));
        }

        private void Gradient(VertexHelper mesh, Vector2[] points, Color top, Color bottom)
        {
            var colors = new Color[points.Length];
            float min = float.MaxValue, max = float.MinValue;
            foreach (Vector2 point in points) { min = Mathf.Min(min, point.y); max = Mathf.Max(max, point.y); }
            for (int i = 0; i < points.Length; i++)
            {
                colors[i] = Color.Lerp(bottom, top, Mathf.InverseLerp(min, max, points[i].y)) * color;
                colors[i].a *= opacity; points[i] = Map(points[i]);
            }
            MenuVectorGeometry.Polygon(mesh, points, colors, MenuVectorGeometry.FringeWidth(this));
        }

        private void Disc(VertexHelper mesh, Vector2 center, Vector2 size, Color tint)
        {
            var points = new Vector2[32];
            for (int i = 0; i < points.Length; i++)
            { float a = i * Mathf.PI * 2 / points.Length; points[i] = center + Vector2.Scale(V(Mathf.Cos(a), Mathf.Sin(a)), size); }
            Panel(mesh, points, tint);
        }

        private void Glow(VertexHelper mesh, Vector2 center, Vector2 size, Color tint)
        {
            int first = mesh.currentVertCount;
            tint.a *= opacity; mesh.AddVert(Map(center), tint * color, Vector2.zero);
            for (int i = 0; i <= 48; i++)
            {
                float a = i * Mathf.PI * 2 / 48;
                Color edge = tint; edge.a = 0;
                mesh.AddVert(Map(center + Vector2.Scale(V(Mathf.Cos(a), Mathf.Sin(a)), size)), edge, Vector2.zero);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        private void Star(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            // A center fan handles the concave star without crossed triangles.
            int first = mesh.currentVertCount; tint.a *= opacity;
            mesh.AddVert(Map(center), tint * color, Vector2.zero);
            for (int i = 0; i <= 10; i++)
            {
                float a = (90 + i * 36) * Mathf.Deg2Rad;
                mesh.AddVert(Map(center + V(Mathf.Cos(a), Mathf.Sin(a)) * radius * (i % 2 == 0 ? 1 : 0.44f)), tint * color, Vector2.zero);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        private void Spark(VertexHelper mesh, Vector2 center, float radius, Color tint, float angle)
        {
            if (radius <= 0.01f) return;
            int first = mesh.currentVertCount; tint.a *= opacity;
            mesh.AddVert(Map(center), tint * color, Vector2.zero);
            for (int i = 0; i <= 8; i++)
            {
                float a = angle + i * Mathf.PI / 4;
                float length = radius * (i % 2 == 0 ? 1 : 0.23f);
                Color edge = tint; if (i % 2 == 0) edge.a *= 0.4f;
                mesh.AddVert(Map(center + V(Mathf.Cos(a), Mathf.Sin(a)) * length), edge * color, Vector2.zero);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        private void Curve(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, float width, Color tint)
        {
            Vector2 previous = a;
            for (int i = 1; i <= 24; i++)
            {
                float t = i / 24f, u = 1 - t;
                Vector2 next = a * (u * u * u) + b * (3 * u * u * t) + c * (3 * u * t * t) + d * (t * t * t);
                Vector2 edge = (next - previous).normalized; edge = V(-edge.y, edge.x) * width * 0.5f;
                Panel(mesh, new[] { previous - edge, next - edge, next + edge, previous + edge }, tint);
                previous = next;
            }
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) => a * ((1 - t) * (1 - t)) + b * (2 * (1 - t) * t) + c * (t * t);
        private static Vector2 V(float x, float y) => new Vector2(x, y);
        private static float Phase(float time, float start, float end) => Mathf.Clamp01((time - start) / (end - start));
        private static float Ease(float t) => 1 - Mathf.Pow(1 - t, 3);
    }
}
