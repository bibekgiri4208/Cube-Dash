using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Resolution-independent, embossed star coins matching the collectible's gold palette.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuCoinGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Color ink = new Color(0.15f, 0.075f, 0.025f);
            Disc(mesh, new Vector2(0.43f, 0.58f), 0.36f, ink);
            Disc(mesh, new Vector2(0.43f, 0.60f), 0.32f, new Color(0.81f, 0.39f, 0.045f));
            Disc(mesh, new Vector2(0.43f, 0.63f), 0.29f, new Color(1, 0.83f, 0.27f));
            var center = new Vector2(0.57f, 0.43f);
            Disc(mesh, center + Vector2.down * 0.025f, 0.37f, ink);
            Disc(mesh, center, 0.34f, new Color(0.91f, 0.49f, 0.055f));
            Disc(mesh, center + Vector2.up * 0.015f, 0.305f, new Color(1, 0.91f, 0.48f));
            Disc(mesh, center, 0.26f, new Color(0.84f, 0.44f, 0.035f));
            Disc(mesh, center + Vector2.up * 0.006f, 0.235f, new Color(1, 0.74f, 0.13f));
            Star(mesh, center + Vector2.down * 0.006f, 0.185f, new Color(0.74f, 0.32f, 0.02f));
            Star(mesh, center + Vector2.up * 0.013f, 0.15f, new Color(1, 0.98f, 0.77f));
        }

        private void Disc(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            var points = new Vector2[48];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / points.Length;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            Polygon(mesh, points, tint);
        }
        private void Star(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            var points = new Vector2[10];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = (90 + i * 36) * Mathf.Deg2Rad;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * (i % 2 == 0 ? 1 : 0.46f);
            }
            var middle = new Vector2[5];
            for (int i = 0; i < 5; i++)
            {
                middle[i] = points[i * 2 + 1];
                Polygon(mesh, new[] { points[(i * 2 + 9) % 10], points[i * 2], points[i * 2 + 1] }, tint);
            }
            Polygon(mesh, middle, tint);
        }
        private void Polygon(VertexHelper mesh, Vector2[] points, Color tint)
        {
            Rect rect = rectTransform.rect;
            for (int i = 0; i < points.Length; i++)
                points[i] = new Vector2(rect.xMin + points[i].x * rect.width, rect.yMin + points[i].y * rect.height);
            MenuVectorGeometry.Polygon(mesh, points, tint * color, MenuVectorGeometry.FringeWidth(this));
        }
    }
}
