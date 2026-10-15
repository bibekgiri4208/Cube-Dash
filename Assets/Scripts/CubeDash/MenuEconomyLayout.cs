using UnityEngine;

namespace CubeDash
{
    /// <summary>Safe-area wallet and an independently fitted Shop, without fighting panel motion.</summary>
    public sealed class MenuEconomyLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private RectTransform canvas;
        [SerializeField] private MenuPanelMotion shopMotion;
        private Rect lastArea;
        private Vector2 lastSize;
        private void OnEnable() { lastSize = Vector2.zero; Refresh(); }
        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            Rect area = Screen.safeArea; Vector2 size = canvas.rect.size;
            if (Screen.width <= 0 || Screen.height <= 0 || size.x <= 0 || size.y <= 0 || (area == lastArea && size == lastSize)) return;
            lastArea = area; lastSize = size;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            shopMotion.SetLayoutScale(Mathf.Min(1, Mathf.Min((size.x - 48) / 1160, (size.y - 48) / 650)));
        }
    }
}
