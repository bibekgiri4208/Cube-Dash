using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>One responsive skin grid: three cards on desktop, scrollable rows on narrow screens.</summary>
    public sealed class MenuSkinsLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform card, shadow, header, heading, icon, divider, viewport, wallet, status, back;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private RectTransform[] contents = new RectTransform[0];
        [SerializeField] private Button[] actions = new Button[0];
        private Vector2 lastSize;
        private void OnEnable() { lastSize = Vector2.zero; Refresh(); }
        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            Vector2 size = ((RectTransform)transform).rect.size;
            if (size == lastSize || size.x <= 0 || size.y <= 0) return;
            lastSize = size;
            float width = Mathf.Min(1160, Mathf.Max(320, size.x - 48));
            int columns = width >= 1000 ? 3 : width >= 720 ? 2 : 1;
            float height = columns == 3 ? 650 : columns == 2 ? 900 : 1040;
            float fit = Mathf.Min(1, Mathf.Max(0.1f, (size.y - 48) / height));
            width *= fit; height *= fit;
            card.sizeDelta = shadow.sizeDelta = new Vector2(width, height);
            header.sizeDelta = new Vector2(width - 48 * fit, 104 * fit);
            header.anchoredPosition = new Vector2(0, height * 0.5f - 78 * fit);
            heading.anchoredPosition = new Vector2(46 * fit, height * 0.5f - 78 * fit);
            heading.sizeDelta = new Vector2(width - 170 * fit, 88 * fit);
            icon.anchoredPosition = new Vector2(-width * 0.5f + 85 * fit, height * 0.5f - 78 * fit);
            icon.localScale = Vector3.one * fit;
            divider.anchoredPosition = new Vector2(0, height * 0.5f - 172 * fit);
            divider.sizeDelta = new Vector2(width - 96 * fit, 2 * fit);
            viewport.anchoredPosition = new Vector2(0, -35 * fit);
            viewport.sizeDelta = new Vector2(width - 128 * fit, height - 300 * fit);
            float cell = (viewport.sizeDelta.x - (columns - 1) * 16 * fit) / columns;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(cell, cell * 336 / 328);
            grid.spacing = Vector2.one * 16 * fit;
            foreach (var content in contents) content.localScale = Vector3.one * (cell / 328);
            wallet.anchoredPosition = new Vector2(-width * 0.5f + 174 * fit, -height * 0.5f + 58 * fit);
            wallet.localScale = Vector3.one * fit;
            back.anchoredPosition = new Vector2(width * 0.5f - 143 * fit, -height * 0.5f + 58 * fit);
            back.localScale = Vector3.one * fit;
            status.anchoredPosition = new Vector2(0, -height * 0.5f + 99 * fit);
            status.sizeDelta = new Vector2(width - 96 * fit, 26 * fit);
            var backButton = back.GetComponent<Button>();
            for (int i = 0; i < actions.Length; i++)
                actions[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? actions[i - 1] : backButton,
                    selectOnRight = i + 1 < actions.Length ? actions[i + 1] : backButton,
                    selectOnUp = i >= columns ? actions[i - columns] : backButton,
                    selectOnDown = i + columns < actions.Length ? actions[i + columns] : backButton };
            backButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = actions[actions.Length - 1],
                selectOnDown = actions[0], selectOnLeft = actions[actions.Length - 1], selectOnRight = actions[0] };
        }
    }
}
