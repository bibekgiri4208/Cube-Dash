using UnityEngine;

namespace CubeDash
{
    /// <summary>Fits a small anchored dialog to the canvas without competing with its fade/lift animation.</summary>
    public sealed class MenuDialogLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform card;
        [SerializeField] private RectTransform shadow;
        [SerializeField] private Vector2 maximumSize = new Vector2(760, 320);
        private Vector2 lastSize;

        private void OnEnable() { lastSize = Vector2.zero; Refresh(); }
        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            Vector2 size = ((RectTransform)transform).rect.size;
            if (size == lastSize || size.x <= 0 || size.y <= 0) return;
            lastSize = size;
            Vector2 fit = new Vector2(Mathf.Min(maximumSize.x, Mathf.Max(1, size.x - 48)),
                Mathf.Min(maximumSize.y, Mathf.Max(1, size.y - 48)));
            card.sizeDelta = shadow.sizeDelta = fit;
        }
    }
}
