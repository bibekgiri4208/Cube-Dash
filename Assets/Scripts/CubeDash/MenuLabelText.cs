using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Higher-resolution dynamic-font sampling without changing the label's authored size or layout.</summary>
    public sealed class MenuLabelText : Text
    {
        private readonly UIVertex[] quad = new UIVertex[4];

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            if (font == null) { mesh.Clear(); return; }
            if (!font.dynamic) { base.OnPopulateMesh(mesh); return; }
            const int sampling = 2;
            m_DisableFontTextureRebuiltCallback = true;
            try
            {
                TextGenerationSettings settings = GetGenerationSettings(rectTransform.rect.size * sampling);
                settings.fontSize *= sampling; settings.resizeTextMinSize *= sampling; settings.resizeTextMaxSize *= sampling;
                cachedTextGenerator.PopulateWithErrors(text, settings, gameObject);
                var vertices = cachedTextGenerator.verts;
                mesh.Clear();
                if (vertices.Count == 0) return;
                float unitsPerPixel = 1 / (pixelsPerUnit * sampling);
                Vector2 first = (Vector2)vertices[0].position * unitsPerPixel;
                Vector2 rounding = PixelAdjustPoint(first) - first;
                for (int index = 0; index < vertices.Count; index++)
                {
                    int corner = index & 3;
                    quad[corner] = vertices[index]; quad[corner].position *= unitsPerPixel;
                    quad[corner].position += (Vector3)rounding;
                    if (corner == 3) mesh.AddUIVertexQuad(quad);
                }
            }
            finally { m_DisableFontTextureRebuiltCallback = false; }
        }
    }
}
