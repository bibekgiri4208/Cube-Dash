using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Live, resolution-independent swatches matching the four gameplay trail palettes and patterns.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MenuTrailGraphic : MaskableGraphic
    {
        [SerializeField] private PlayerTrail style = PlayerTrail.NeonAurora;
        private float age;
        private float refreshAge;
        public PlayerTrail Style => style;
        public void Sample(float seconds) { age = seconds; SetVerticesDirty(); }
        private void Update()
        {
            if (!Application.isPlaying) return;
            age += Time.unscaledDeltaTime; refreshAge += Time.unscaledDeltaTime;
            if (refreshAge < 0.05f) return;
            refreshAge = 0; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect r = rectTransform.rect;
            Poly(mesh, new[] { P(0.02f, 0.08f), P(0.92f, 0.08f), P(0.98f, 0.2f), P(0.98f, 0.92f), P(0.08f, 0.92f), P(0.02f, 0.80f) }, new Color(0.035f, 0.025f, 0.09f));
            // A receding floor and floating particles make the height of the effect readable in the thumbnail.
            Poly(mesh, new[] { P(0.08f, 0.13f), P(0.91f, 0.13f), P(0.78f, 0.33f), P(0.19f, 0.33f) }, new Color(0.09f, 0.10f, 0.18f, 0.8f));
            for (int i = 0; i < 7; i++)
            {
                float x = 0.09f + i * 0.13f;
                Poly(mesh, new[] { P(x, 0.14f), P(x + 0.004f, 0.14f), P(0.2f + (x - 0.09f) * 0.72f, 0.32f) }, new Color(0.3f, 0.4f, 0.6f, 0.14f));
            }
            int strands = style == PlayerTrail.RainbowRush ? 4 : style == PlayerTrail.FrostWave ? 2 : 3;
            for (int layer = 0; layer < 3; layer++)
                for (int strand = 0; strand < strands; strand++)
                    for (int i = 0; i < 48; i++)
                    {
                        float t = i / 48f, next = (i + 1) / 48f;
                        Vector2 a = Path(t, strand, strands), b = Path(next, strand, strands);
                        float width = (layer == 0 ? 0.095f : layer == 1 ? 0.028f : 0.008f) * (0.22f + t * 0.78f);
                        Vector2 half = Vector2.up * r.height * width;
                        Color tint = style == PlayerTrail.RainbowRush ? Color.HSVToRGB(Mathf.Repeat(t * 0.75f + strand * 0.16f - age * 0.12f, 1), 0.8f, 1)
                            : Color.Lerp(TrailStyles.Primary(style), TrailStyles.Secondary(style), strand / (float)Mathf.Max(1, strands - 1));
                        if (layer == 2) tint = Color.Lerp(tint, Color.white, 0.75f);
                        tint.a = (layer == 0 ? 0.12f : layer == 1 ? 0.75f : 1) * Mathf.Pow(t, 0.65f);
                        Poly(mesh, new[] { a - half, b - half, b + half, a + half }, tint);
                    }
            for (int i = 0; i < 18; i++)
            {
                float t = Mathf.Repeat(i * 0.087f + age * 0.10f, 0.92f);
                Vector2 point = Path(t, i % strands, strands) + Vector2.up * (0.08f + Mathf.Sin(i * 9.1f + age) * 0.08f) * r.height;
                point.y = Mathf.Min(point.y, P(0, 0.86f).y);
                float radius = (style == PlayerTrail.FrostWave ? 0.044f : style == PlayerTrail.RainbowRush ? 0.027f : 0.024f) * r.height * Mathf.Sin(t * Mathf.PI);
                Color tint = Color.Lerp(TrailStyles.Secondary(style), Color.white, 0.5f); tint.a = Mathf.Sin(t * Mathf.PI);
                if (style == PlayerTrail.FrostWave || style == PlayerTrail.RainbowRush)
                {
                    if (style == PlayerTrail.RainbowRush) tint = Color.HSVToRGB(Mathf.Repeat(t + i * 0.1f - age * 0.1f, 1), 0.6f, 1);
                    Vector2 top = point + Vector2.up * radius * 1.6f, bottom = point - Vector2.up * radius * 1.6f;
                    Vector2 left = point - Vector2.right * radius * 0.7f, right = point + Vector2.right * radius * 0.7f;
                    Poly(mesh, new[] { top, left, point }, Color.Lerp(tint, Color.white, 0.5f));
                    Poly(mesh, new[] { top, point, right }, tint);
                    Poly(mesh, new[] { bottom, point, left }, tint * new Color(0.6f, 0.7f, 0.8f, 1));
                    Poly(mesh, new[] { bottom, right, point }, tint * new Color(0.35f, 0.5f, 0.7f, 1));
                }
                else
                {
                    Color glow = style == PlayerTrail.SolarFlare ? TrailStyles.Primary(style) : tint;
                    glow.a = tint.a * 0.15f; Disc(mesh, point, radius * 2.6f, glow);
                    glow.a = tint.a * 0.55f; Disc(mesh, point, radius, glow);
                    Disc(mesh, point, radius * 0.38f, Color.Lerp(tint, Color.white, 0.7f));
                }
            }
        }

        private Vector2 Path(float t, int strand, int count)
        {
            float y = 0.50f + (strand - (count - 1) * 0.5f) * (count == 4 ? 0.12f : 0.23f);
            float bend = Mathf.Sin(t * 9 - age * 1.8f + strand * 1.7f) * 0.11f + t * 0.03f;
            if (style == PlayerTrail.SolarFlare) bend += Mathf.Sin(t * 27 - age * 5) * 0.035f;
            if (style == PlayerTrail.FrostWave) bend = Mathf.Sin(t * 8 - age + strand) * 0.07f;
            return P(0.09f + t * 0.82f, y + bend);
        }
        private Vector2 P(float x, float y)
        { Rect r = rectTransform.rect; return new Vector2(r.xMin + r.width * x, r.yMin + r.height * y); }
        private void Poly(VertexHelper mesh, Vector2[] points, Color tint)
            => MenuVectorGeometry.Polygon(mesh, points, tint * color, MenuVectorGeometry.FringeWidth(this));
        private void Disc(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            var points = new Vector2[16];
            for (int i = 0; i < points.Length; i++) { float angle = i * Mathf.PI * 2 / points.Length; points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius; }
            Poly(mesh, points, tint);
        }
    }
}
