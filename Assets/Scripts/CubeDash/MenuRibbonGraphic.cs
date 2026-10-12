using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Resolution-independent, outlined cut-corner ribbons, matching the reference silhouette.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuRibbonGraphic : MaskableGraphic
    {
        [SerializeField] private bool primary;
        public bool Primary { get => primary; set { primary = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Color border = primary ? new Color(0.27f, 0, 0.055f) : new Color(0.018f, 0.014f, 0.025f);
            Polygon(vh, r, 0, -5, r.width, r.height, border, border);
            Polygon(vh, r, 5, 3, r.width - 12, r.height - 10,
                primary ? new Color(1, 0.065f, 0.15f) : new Color(1, 0.69f, 0.73f),
                primary ? new Color(0.95f, 0.02f, 0.11f) : new Color(1, 0.82f, 0.84f));
            if (!primary) return;
            Color stripe = new Color(1, 0.33f, 0.4f, 0.15f);
            for (int i = 0; i < 5; i++)
            {
                float x = r.xMin + r.width * (0.58f + i * 0.055f);
                Quad(vh, new Vector2(x, r.yMin + 8), new Vector2(x + 12, r.yMin + 8),
                    new Vector2(x + 82, r.yMax - 9), new Vector2(x + 70, r.yMax - 9), stripe);
            }
        }

        private static void Polygon(VertexHelper vh, Rect rect, float x, float y, float w, float h, Color left, Color right)
        {
            Vector2[] p = { new Vector2(0, 0), new Vector2(0.025f, 1), new Vector2(0.94f, 1),
                new Vector2(1, 0.74f), new Vector2(0.996f, 0.45f), new Vector2(0.967f, 0.22f), new Vector2(0.91f, 0) };
            int first = vh.currentVertCount;
            for (int i = 0; i < p.Length; i++) vh.AddVert(new Vector3(rect.xMin + x + p[i].x * w, rect.yMin + y + p[i].y * h),
                Color.Lerp(left, right, p[i].x), Vector2.zero);
            for (int i = 1; i < p.Length - 1; i++) vh.AddTriangle(first, first + i, first + i + 1);
        }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
