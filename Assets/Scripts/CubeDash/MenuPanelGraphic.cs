using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Rounded menu cards and controls with the same overlay-safe edge smoothing as the ribbons.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuPanelGraphic : MaskableGraphic
    {
        [SerializeField, Min(0)] private float cornerRadius = 16;
        [SerializeField, Min(0)] private float borderWidth = 2;
        [SerializeField] private Color borderColor = new Color(0.22f, 0.13f, 0.18f);
        public float CornerRadius { get => cornerRadius; set { cornerRadius = Mathf.Max(0, value); SetVerticesDirty(); } }
        public float BorderWidth { get => borderWidth; set { borderWidth = Mathf.Max(0, value); SetVerticesDirty(); } }
        public Color BorderColor { get => borderColor; set { borderColor = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect rect = rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0) return;
            float fringe = MenuVectorGeometry.FringeWidth(this);
            float width = Mathf.Min(borderWidth, Mathf.Min(rect.width, rect.height) * 0.5f);
            if (width > 0)
            {
                MenuVectorGeometry.Polygon(mesh, Contour(rect, cornerRadius), borderColor, fringe);
                rect = new Rect(rect.xMin + width, rect.yMin + width, rect.width - width * 2, rect.height - width * 2);
                if (rect.width <= 0 || rect.height <= 0) return;
            }
            MenuVectorGeometry.Polygon(mesh, Contour(rect, Mathf.Max(0, cornerRadius - width)), color, fringe);
        }

        private static Vector2[] Contour(Rect rect, float radius)
        {
            radius = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            if (radius <= 0.001f) return new[] { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax) };
            const int steps = 8;
            var points = new Vector2[4 * (steps + 1)];
            Vector2[] centers = { new Vector2(rect.xMin + radius, rect.yMin + radius), new Vector2(rect.xMax - radius, rect.yMin + radius),
                new Vector2(rect.xMax - radius, rect.yMax - radius), new Vector2(rect.xMin + radius, rect.yMax - radius) };
            for (int corner = 0; corner < 4; corner++)
                for (int step = 0; step <= steps; step++)
                {
                    float angle = (180 + corner * 90 + step * 90f / steps) * Mathf.Deg2Rad;
                    points[corner * (steps + 1) + step] = centers[corner] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
            return points;
        }
    }
}
