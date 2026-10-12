using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuGlyphGraphic : MaskableGraphic
    {
        public enum Glyph { Play, Cart, Gear, Exit }
        [SerializeField] private Glyph symbol;
        public Glyph Symbol { get => symbol; set { symbol = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (symbol == Glyph.Play) Polygon(vh, new[] { new Vector2(0.2f, 0.08f), new Vector2(0.2f, 0.92f), new Vector2(0.92f, 0.5f) });
            else if (symbol == Glyph.Cart)
            {
                Stroke(vh, new Vector2(0.03f, 0.91f), new Vector2(0.24f, 0.91f), 0.09f);
                Stroke(vh, new Vector2(0.22f, 0.92f), new Vector2(0.36f, 0.25f), 0.085f);
                Polygon(vh, new[] { new Vector2(0.28f, 0.78f), new Vector2(0.97f, 0.78f), new Vector2(0.81f, 0.37f), new Vector2(0.37f, 0.37f) });
                Stroke(vh, new Vector2(0.36f, 0.25f), new Vector2(0.84f, 0.25f), 0.08f);
                Disc(vh, new Vector2(0.42f, 0.10f), 0.105f); Disc(vh, new Vector2(0.79f, 0.10f), 0.105f);
            }
            else if (symbol == Glyph.Gear)
            {
                for (int i = 0; i < 32; i++)
                {
                    float a = i * Mathf.PI / 16, b = (i + 1) * Mathf.PI / 16;
                    float r = i % 4 == 0 || i % 4 == 1 ? 0.48f : 0.36f;
                    Quad(vh, Point(a, 0.18f), Point(a, r), Point(b, r), Point(b, 0.18f));
                }
            }
            else
            {
                Stroke(vh, new Vector2(0.17f, 0.08f), new Vector2(0.17f, 0.9f), 0.14f);
                Stroke(vh, new Vector2(0.17f, 0.9f), new Vector2(0.88f, 0.9f), 0.14f);
                Stroke(vh, new Vector2(0.88f, 0.9f), new Vector2(0.88f, 0.08f), 0.14f);
                Stroke(vh, new Vector2(0.17f, 0.08f), new Vector2(0.88f, 0.08f), 0.14f);
                Stroke(vh, new Vector2(0.3f, 0.5f), new Vector2(0.62f, 0.5f), 0.13f);
                Polygon(vh, new[] { new Vector2(0.54f, 0.23f), new Vector2(0.80f, 0.5f), new Vector2(0.54f, 0.77f) });
            }
        }
        private static Vector2 Point(float angle, float radius) => new Vector2(0.5f + Mathf.Cos(angle) * radius, 0.5f + Mathf.Sin(angle) * radius);
        private void Disc(VertexHelper vh, Vector2 center, float radius)
        {
            for (int i = 0; i < 20; i++)
                Polygon(vh, new[] { center, center + new Vector2(Mathf.Cos(i * Mathf.PI / 10), Mathf.Sin(i * Mathf.PI / 10)) * radius,
                    center + new Vector2(Mathf.Cos((i + 1) * Mathf.PI / 10), Mathf.Sin((i + 1) * Mathf.PI / 10)) * radius });
        }
        private void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width)
        {
            Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
            Quad(vh, a - n, a + n, b + n, b - n);
        }
        private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d) => Polygon(vh, new[] { a, b, c, d });
        private void Polygon(VertexHelper vh, Vector2[] points)
        {
            Rect r = rectTransform.rect; int start = vh.currentVertCount;
            foreach (Vector2 p in points) vh.AddVert(new Vector3(r.xMin + p.x * r.width, r.yMin + p.y * r.height), color, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
