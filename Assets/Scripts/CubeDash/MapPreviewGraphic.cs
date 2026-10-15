using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Original lightweight map illustrations; no external textures or runtime objects.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MapPreviewGraphic : MaskableGraphic
    {
        public enum Map { City, Jungle, Dynamic, Desert, Beach, CountrySide }
        [SerializeField] private Map map;
        public Map Kind => map;
        private VertexHelper mesh;
        private float fringe;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); mesh = vh; fringe = MenuVectorGeometry.FringeWidth(this);
            if (rectTransform.rect.width <= 0 || rectTransform.rect.height <= 0) return;
            Color sky = new Color(0.74f, 0.88f, 0.87f), green = new Color(0.28f, 0.50f, 0.36f);
            Box(0, 0, 1, 1, sky); Box(0, 0, 1, 0.24f, new Color(0.63f, 0.76f, 0.57f));
            Sun(0.82f, 0.78f, 0.09f);
            switch (map)
            {
                case Map.City:
                    for (int i = 0; i < 6; i++)
                    {
                        float x = 0.08f + i * 0.145f, h = 0.34f + i % 3 * 0.16f;
                        Box(x, 0.18f, 0.115f, h, i % 2 == 0 ? new Color(0.46f, 0.50f, 0.59f) : new Color(0.73f, 0.51f, 0.49f));
                        for (int row = 0; row < 3; row++) for (int col = 0; col < 2; col++)
                            Box(x + 0.019f + col * 0.046f, 0.24f + row * 0.095f, 0.022f, 0.035f, new Color(1, 0.88f, 0.59f));
                    }
                    Polygon(new Color(0.27f, 0.29f, 0.35f), new Vector2(0.3f, 0), new Vector2(0.7f, 0), new Vector2(0.57f, 0.2f), new Vector2(0.43f, 0.2f));
                    Box(0.49f, 0.03f, 0.02f, 0.12f, Color.white); break;
                case Map.Jungle:
                    for (int i = 0; i < 7; i++)
                    {
                        float x = 0.08f + i * 0.135f;
                        Box(x - 0.012f, 0.12f, 0.024f, 0.48f, new Color(0.36f, 0.25f, 0.23f));
                        Polygon(i % 2 == 0 ? green : new Color(0.35f, 0.63f, 0.44f), new Vector2(x - 0.12f, 0.3f), new Vector2(x + 0.12f, 0.3f), new Vector2(x + 0.08f, 0.73f), new Vector2(x - 0.05f, 0.89f), new Vector2(x - 0.12f, 0.57f));
                    }
                    break;
                case Map.Dynamic:
                    // A mixed landscape and continuous road distinguish the cycling level from a fixed biome.
                    Polygon(new Color(0.48f, 0.54f, 0.63f), new Vector2(0.26f, 0.22f), new Vector2(0.74f, 0.22f), new Vector2(0.51f, 0.90f));
                    Polygon(new Color(1, 0.97f, 0.9f), new Vector2(0.44f, 0.70f), new Vector2(0.58f, 0.70f), new Vector2(0.51f, 0.90f));
                    for (int i = 0; i < 3; i++)
                    {
                        float x = 0.06f + i * 0.12f;
                        Box(x, 0.20f, 0.09f, 0.30f + i % 2 * 0.16f, new Color(0.57f, 0.52f, 0.59f));
                        Box(x + 0.025f, 0.30f, 0.035f, 0.06f, new Color(1, 0.88f, 0.59f));
                    }
                    Box(0.72f, 0.18f, 0.025f, 0.37f, new Color(0.36f, 0.25f, 0.23f));
                    Polygon(green, new Vector2(0.60f, 0.39f), new Vector2(0.87f, 0.39f), new Vector2(0.74f, 0.78f));
                    Polygon(new Color(1, 0.85f, 0.61f), new Vector2(0.55f, 0), new Vector2(1, 0), new Vector2(1, 0.30f), new Vector2(0.78f, 0.23f));
                    Polygon(new Color(0.3f, 0.69f, 0.76f), new Vector2(0.80f, 0), new Vector2(1, 0), new Vector2(1, 0.24f), new Vector2(0.91f, 0.18f));
                    Polygon(new Color(0.27f, 0.29f, 0.35f), new Vector2(0.26f, 0), new Vector2(0.74f, 0), new Vector2(0.54f, 0.44f), new Vector2(0.46f, 0.44f));
                    Box(0.49f, 0.04f, 0.02f, 0.11f, Color.white); Box(0.495f, 0.23f, 0.01f, 0.08f, Color.white);
                    break;
                case Map.Desert:
                    Box(0, 0, 1, 0.72f, new Color(0.94f, 0.75f, 0.5f));
                    Polygon(new Color(0.81f, 0.52f, 0.38f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 0.35f), new Vector2(0.7f, 0.54f), new Vector2(0.37f, 0.27f), new Vector2(0.08f, 0.47f), new Vector2(0, 0.4f));
                    Box(0.69f, 0.17f, 0.03f, 0.4f, green); Box(0.6f, 0.34f, 0.12f, 0.04f, green); Box(0.6f, 0.34f, 0.03f, 0.13f, green); break;
                case Map.Beach:
                    Box(0, 0.1f, 1, 0.46f, new Color(0.3f, 0.69f, 0.76f));
                    Polygon(new Color(1, 0.85f, 0.61f), Vector2.zero, new Vector2(1, 0), new Vector2(1, 0.12f), new Vector2(0.75f, 0.17f), new Vector2(0.4f, 0.32f), new Vector2(0, 0.45f));
                    Box(0.15f, 0.22f, 0.025f, 0.47f, new Color(0.51f, 0.34f, 0.29f));
                    Polygon(green, new Vector2(0.03f, 0.52f), new Vector2(0.17f, 0.72f), new Vector2(0.35f, 0.56f), new Vector2(0.17f, 0.64f)); break;
                case Map.CountrySide:
                    Polygon(green, new Vector2(0, 0.2f), new Vector2(1, 0.2f), new Vector2(1, 0.42f), new Vector2(0.75f, 0.65f), new Vector2(0.45f, 0.35f), new Vector2(0.2f, 0.63f), new Vector2(0, 0.48f));
                    Box(0, 0, 1, 0.22f, new Color(0.76f, 0.78f, 0.47f));
                    Box(0.35f, 0.14f, 0.27f, 0.26f, new Color(1, 0.87f, 0.74f));
                    Polygon(new Color(0.46f, 0.32f, 0.34f), new Vector2(0.3f, 0.4f), new Vector2(0.68f, 0.4f), new Vector2(0.49f, 0.62f));
                    Box(0.46f, 0.14f, 0.06f, 0.16f, new Color(0.44f, 0.32f, 0.29f)); break;
            }
        }
        private void Box(float x, float y, float w, float h, Color color) => Polygon(color, new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h));
        private void Polygon(Color color, params Vector2[] points)
        {
            Rect r = rectTransform.rect;
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(r.xMin + Mathf.Clamp01(points[i].x) * r.width, r.yMin + Mathf.Clamp01(points[i].y) * r.height);
            MenuVectorGeometry.Polygon(mesh, points, color, fringe);
        }
        private void Sun(float x, float y, float radius)
        {
            var points = new Vector2[16];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / points.Length;
                points[i] = new Vector2(x + Mathf.Cos(angle) * radius * 0.4f, y + Mathf.Sin(angle) * radius);
            }
            Polygon(new Color(1, 0.87f, 0.59f), points);
        }
    }
}
