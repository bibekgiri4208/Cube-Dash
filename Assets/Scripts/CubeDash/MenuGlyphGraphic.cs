using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuGlyphGraphic : MaskableGraphic
    {
        private const int CurveSegments = 64;
        public enum Glyph { Play, Cart, Gear, Exit, Collection, Check }
        [SerializeField] private Glyph symbol;
        public Glyph Symbol { get => symbol; set { symbol = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (symbol == Glyph.Play) Polygon(vh, Rounded(new[] { new Vector2(0.2f, 0.08f), new Vector2(0.2f, 0.92f), new Vector2(0.92f, 0.5f) }, 0.025f));
            else if (symbol == Glyph.Cart)
            {
                Stroke(vh, new Vector2(0.07f, 0.88f), new Vector2(0.23f, 0.88f), 0.075f);
                Stroke(vh, new Vector2(0.23f, 0.88f), new Vector2(0.36f, 0.29f), 0.065f);
                Ring(vh, new[] { new Vector2(0.29f, 0.76f), new Vector2(0.94f, 0.76f), new Vector2(0.81f, 0.40f), new Vector2(0.37f, 0.40f) },
                    new[] { new Vector2(0.39f, 0.67f), new Vector2(0.81f, 0.67f), new Vector2(0.75f, 0.49f), new Vector2(0.41f, 0.49f) });
                Stroke(vh, new Vector2(0.50f, 0.68f), new Vector2(0.52f, 0.47f), 0.035f);
                Stroke(vh, new Vector2(0.68f, 0.68f), new Vector2(0.66f, 0.47f), 0.035f);
                Stroke(vh, new Vector2(0.36f, 0.29f), new Vector2(0.81f, 0.29f), 0.065f);
                Disc(vh, new Vector2(0.43f, 0.12f), 0.082f); Disc(vh, new Vector2(0.77f, 0.12f), 0.082f);
            }
            else if (symbol == Glyph.Gear)
            {
                var outer = new Vector2[CurveSegments * 2]; var inner = new Vector2[outer.Length];
                for (int i = 0; i < outer.Length; i++)
                {
                    float angle = i * Mathf.PI * 2 / outer.Length;
                    float radius = Mathf.Lerp(0.36f, 0.47f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.4f, 0.5f, Mathf.Cos(angle * 8))));
                    outer[i] = Point(angle, radius); inner[i] = Point(angle, 0.18f);
                }
                Ring(vh, outer, inner);
            }
            else if (symbol == Glyph.Exit)
            {
                Ring(vh, new[] { new Vector2(0.10f, 0.01f), new Vector2(0.10f, 0.97f), new Vector2(0.95f, 0.97f), new Vector2(0.95f, 0.01f) },
                    new[] { new Vector2(0.24f, 0.15f), new Vector2(0.24f, 0.83f), new Vector2(0.81f, 0.83f), new Vector2(0.81f, 0.15f) });
                Stroke(vh, new Vector2(0.3f, 0.5f), new Vector2(0.62f, 0.5f), 0.13f);
                Polygon(vh, Rounded(new[] { new Vector2(0.54f, 0.23f), new Vector2(0.80f, 0.5f), new Vector2(0.54f, 0.77f) }, 0.018f));
            }
            else if (symbol == Glyph.Collection)
            {
                Stroke(vh, new Vector2(0.15f, 0.32f), new Vector2(0.15f, 0.87f), 0.055f);
                Stroke(vh, new Vector2(0.15f, 0.87f), new Vector2(0.70f, 0.87f), 0.055f);
                Ring(vh, new[] { new Vector2(0.27f, 0.10f), new Vector2(0.27f, 0.73f), new Vector2(0.86f, 0.73f), new Vector2(0.86f, 0.10f) },
                    new[] { new Vector2(0.34f, 0.17f), new Vector2(0.34f, 0.66f), new Vector2(0.79f, 0.66f), new Vector2(0.79f, 0.17f) });
                Polygon(vh, new[] { new Vector2(0.56f, 0.26f), new Vector2(0.43f, 0.41f), new Vector2(0.56f, 0.56f), new Vector2(0.69f, 0.41f) });
            }
            else if (symbol == Glyph.Check)
            {
                Stroke(vh, new Vector2(0.16f, 0.49f), new Vector2(0.40f, 0.26f), 0.105f);
                Stroke(vh, new Vector2(0.40f, 0.26f), new Vector2(0.87f, 0.80f), 0.105f);
            }
        }
        private static Vector2 Point(float angle, float radius) => new Vector2(0.5f + Mathf.Cos(angle) * radius, 0.5f + Mathf.Sin(angle) * radius);
        private void Disc(VertexHelper vh, Vector2 center, float radius)
        {
            var contour = new Vector2[CurveSegments];
            for (int i = 0; i < contour.Length; i++)
                contour[i] = center + new Vector2(Mathf.Cos(i * Mathf.PI * 2 / contour.Length), Mathf.Sin(i * Mathf.PI * 2 / contour.Length)) * radius;
            Polygon(vh, contour);
        }
        private void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width)
        {
            Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
            Quad(vh, a - n, a + n, b + n, b - n);
            Disc(vh, a, width * 0.5f); Disc(vh, b, width * 0.5f);
        }
        private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d) => Polygon(vh, new[] { a, b, c, d });
        private void Polygon(VertexHelper vh, Vector2[] points)
        {
            MenuVectorGeometry.Polygon(vh, Map(points), color, MenuVectorGeometry.FringeWidth(this));
        }
        private void Ring(VertexHelper vh, Vector2[] outer, Vector2[] inner) =>
            MenuVectorGeometry.Ring(vh, Map(outer), Map(inner), color, MenuVectorGeometry.FringeWidth(this));
        private Vector2[] Map(Vector2[] points)
        {
            Rect r = rectTransform.rect; var mapped = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++) mapped[i] = new Vector2(r.xMin + points[i].x * r.width, r.yMin + points[i].y * r.height);
            return mapped;
        }
        private static Vector2[] Rounded(Vector2[] points, float radius)
        {
            const int steps = 4;
            var rounded = new Vector2[points.Length * (steps + 1)];
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 p = points[i], entry = p + (points[(i + points.Length - 1) % points.Length] - p).normalized * radius;
                Vector2 exit = p + (points[(i + 1) % points.Length] - p).normalized * radius;
                for (int step = 0; step <= steps; step++)
                {
                    float t = step / (float)steps;
                    rounded[i * (steps + 1) + step] = (1 - t) * (1 - t) * entry + 2 * (1 - t) * t * p + t * t * exit;
                }
            }
            return rounded;
        }
    }
}
