using UnityEngine;

namespace CubeDash
{
    /// <summary>Saved map cards use three columns on desktop and two on narrower canvases.</summary>
    public sealed class MenuMapLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform card;
        [SerializeField] private RectTransform shadow;
        [SerializeField] private RectTransform header;
        [SerializeField] private RectTransform heading;
        [SerializeField] private RectTransform back;
        [SerializeField] private RectTransform[] maps = new RectTransform[0];
        private Vector2 lastSize;
        private void OnEnable() { lastSize = Vector2.zero; Refresh(); }
        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            Vector2 size = ((RectTransform)transform).rect.size;
            if (size == lastSize || size.x <= 0 || size.y <= 0) return;
            lastSize = size;
            float width = Mathf.Min(1160, Mathf.Max(280, size.x - 48));
            int columns = width >= 1000 ? 3 : 2;
            float height = columns == 3 ? 740 : 980;
            float fit = Mathf.Min(1, Mathf.Max(0.1f, (size.y - 48) / height));
            // Keep pose/scale owned by MenuPanelMotion; shrinking authored dimensions avoids competing tweens.
            width *= fit; height *= fit;
            card.sizeDelta = shadow.sizeDelta = new Vector2(width, height);
            header.sizeDelta = new Vector2(width - 48 * fit, 104 * fit);
            header.anchoredPosition = heading.anchoredPosition = new Vector2(0, height * 0.5f - 76 * fit);
            heading.sizeDelta = new Vector2(width - 80 * fit, 90 * fit);
            float cellWidth = (width - 80 * fit - (columns - 1) * 24 * fit) / columns;
            for (int i = 0; i < maps.Length; i++)
            {
                maps[i].sizeDelta = new Vector2(cellWidth, 212 * fit);
                maps[i].anchoredPosition = new Vector2((i % columns - (columns - 1) * 0.5f) * (cellWidth + 24 * fit),
                    height * 0.5f - (266 + i / columns * 236) * fit);
            }
            back.anchoredPosition = new Vector2(width * 0.5f - 140 * fit, -height * 0.5f + 70 * fit);
            back.sizeDelta = new Vector2(200 * fit, 58 * fit);
        }
    }
}
