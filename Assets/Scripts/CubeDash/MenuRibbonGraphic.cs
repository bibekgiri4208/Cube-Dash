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
            if (r.width <= 14 || r.height <= 12) return;
            float fringe = MenuVectorGeometry.FringeWidth(this);
            Color border = primary ? new Color(0.27f, 0, 0.055f) : new Color(0.018f, 0.014f, 0.025f);
            Vector2[] outline = Contour(r, 0, -5, r.width, r.height);
            Vector2[] face = Contour(r, 5, 3, r.width - 12, r.height - 10);
            MenuVectorGeometry.Polygon(vh, outline, border, fringe);
            Color left = primary ? new Color(1, 0.065f, 0.15f) : new Color(1, 0.69f, 0.73f);
            Color right = primary ? new Color(0.95f, 0.02f, 0.11f) : new Color(1, 0.82f, 0.84f);
            var colors = new Color[face.Length];
            for (int i = 0; i < face.Length; i++) colors[i] = Color.Lerp(left, right, (face[i].x - r.xMin - 5) / (r.width - 12));
            MenuVectorGeometry.Polygon(vh, face, colors, fringe);
            if (!primary) return;
            Color stripe = new Color(1, 0.33f, 0.4f, 0.15f);
            Vector2[] stripeBoundary = MenuVectorGeometry.OffsetContour(face, -fringe * 2);
            for (int i = 0; i < 5; i++)
            {
                float x = r.xMin + r.width * (0.58f + i * 0.055f);
                float width = r.width * (12f / 455), lean = r.height * (70f / 122);
                Vector2[] band = { new Vector2(x, r.yMin + 8), new Vector2(x + width, r.yMin + 8),
                    new Vector2(x + lean + width, r.yMax - 9), new Vector2(x + lean, r.yMax - 9) };
                MenuVectorGeometry.Polygon(vh, MenuVectorGeometry.ClipConvex(band, stripeBoundary), stripe, fringe);
            }
        }

        private static Vector2[] Contour(Rect rect, float x, float y, float w, float h)
        {
            Vector2[] p = { new Vector2(0, 0), new Vector2(0.025f, 1), new Vector2(0.94f, 1),
                new Vector2(1, 0.74f), new Vector2(0.996f, 0.45f), new Vector2(0.967f, 0.22f), new Vector2(0.91f, 0) };
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(rect.xMin + x + p[i].x * w, rect.yMin + y + p[i].y * h);
            return p;
        }
    }
}
