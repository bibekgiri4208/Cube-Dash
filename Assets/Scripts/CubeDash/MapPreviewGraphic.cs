using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Actual scene artwork, center-cropped to fill responsive map cards without stretching.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MapPreviewGraphic : MaskableGraphic
    {
        public enum Map { City, Jungle, Dynamic, Desert, Beach, CountrySide }
        [SerializeField] private Map map;
        [SerializeField] private Texture2D illustration;
        public Map Kind => map;
        public Texture2D Illustration
        {
            get => illustration;
            set { illustration = value; SetVerticesDirty(); SetMaterialDirty(); }
        }
        public override Texture mainTexture => illustration != null ? illustration : Texture2D.whiteTexture;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); Rect rect = rectTransform.rect;
            if (illustration == null || rect.width <= 0 || rect.height <= 0) return;
            float sourceAspect = illustration.width / (float)illustration.height;
            float targetAspect = rect.width / rect.height;
            Vector2 crop = sourceAspect > targetAspect ? new Vector2(targetAspect / sourceAspect, 1)
                : new Vector2(1, sourceAspect / targetAspect);
            Vector2 min = (Vector2.one - crop) * 0.5f, max = min + crop;
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), color, min);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), color, new Vector2(min.x, max.y));
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), color, max);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), color, new Vector2(max.x, min.y));
            mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(2, 3, 0);
        }
    }
}
