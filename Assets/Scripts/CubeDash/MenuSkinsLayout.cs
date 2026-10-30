using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>One responsive skin grid with scrollable rows and keyboard/gamepad navigation.</summary>
    public sealed class MenuSkinsLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform card, shadow, header, heading, icon, divider, viewport, wallet, status, back;
        [SerializeField] private GridLayoutGroup grid;
        [SerializeField] private RectTransform[] contents = new RectTransform[0];
        [SerializeField] private Button[] actions = new Button[0];
        private Vector2 lastSize;
        private void OnEnable() { lastSize = Vector2.zero; Refresh(); }
        private void LateUpdate() => Refresh();

        public void RefreshProducts() { lastSize = Vector2.zero; Refresh(); }

        public void Refresh()
        {
            Vector2 size = ((RectTransform)transform).rect.size;
            if (size == lastSize || size.x <= 0 || size.y <= 0) return;
            lastSize = size;
            var sections = GetComponent<MenuShopSections>();
            float width = Mathf.Min(1160, Mathf.Max(320, size.x - 48));
            int columns = width >= 1120 && actions.Length >= 4 ? 4 : width >= 1000 ? 3 : width >= 720 ? 2 : 1;
            float height = columns >= 3 ? 650 : columns == 2 ? 900 : 1040;
            float fit = Mathf.Min(1, Mathf.Max(0.1f, (size.y - 48) / height));
            width *= fit; height *= fit;
            card.sizeDelta = shadow.sizeDelta = new Vector2(width, height);
            header.sizeDelta = new Vector2(width - 48 * fit, 104 * fit);
            header.anchoredPosition = new Vector2(0, height * 0.5f - 78 * fit);
            heading.anchoredPosition = new Vector2(46 * fit, height * 0.5f - 78 * fit);
            heading.sizeDelta = new Vector2(width - 170 * fit, 88 * fit);
            icon.anchoredPosition = new Vector2(-width * 0.5f + 85 * fit, height * 0.5f - 78 * fit);
            icon.localScale = Vector3.one * fit;
            divider.anchoredPosition = new Vector2(0, height * 0.5f - (sections != null ? 206 : 172) * fit);
            divider.sizeDelta = new Vector2(width - 96 * fit, 2 * fit);
            viewport.anchoredPosition = new Vector2(0, (sections != null ? -58 : -35) * fit);
            viewport.sizeDelta = new Vector2(width - 128 * fit, height - (sections != null ? 358 : 300) * fit);
            float cell = (viewport.sizeDelta.x - (columns - 1) * 16 * fit) / columns;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(cell, cell * 336 / 328);
            grid.spacing = Vector2.one * 16 * fit;
            foreach (var content in contents) content.localScale = Vector3.one * (cell / 328);
            if (sections != null)
            {
                sections.Tabs.anchoredPosition = new Vector2(0, height * 0.5f - 166 * fit);
                sections.Tabs.sizeDelta = new Vector2(420, 48) * fit;
                sections.TrailsViewport.anchoredPosition = viewport.anchoredPosition;
                sections.TrailsViewport.sizeDelta = viewport.sizeDelta;
                sections.TrailGrid.constraintCount = columns; sections.TrailGrid.cellSize = grid.cellSize; sections.TrailGrid.spacing = grid.spacing;
                foreach (var content in sections.TrailContents) content.localScale = Vector3.one * (cell / 328);
            }
            wallet.anchoredPosition = new Vector2(-width * 0.5f + 174 * fit, -height * 0.5f + 58 * fit);
            wallet.localScale = Vector3.one * fit;
            back.anchoredPosition = new Vector2(width * 0.5f - 143 * fit, -height * 0.5f + 58 * fit);
            back.localScale = Vector3.one * fit;
            status.anchoredPosition = new Vector2(0, -height * 0.5f + 99 * fit);
            status.sizeDelta = new Vector2(width - 96 * fit, 26 * fit);
            var backButton = back.GetComponent<Button>();
            Button[] active = sections != null && sections.TrailsSelected ? sections.TrailActions : actions;
            for (int i = 0; i < active.Length; i++)
                active[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnLeft = i > 0 ? active[i - 1] : backButton,
                    selectOnRight = i + 1 < active.Length ? active[i + 1] : backButton,
                    selectOnUp = i >= columns ? active[i - columns] : sections != null ? sections.ActiveTab : backButton,
                    selectOnDown = i + columns < active.Length ? active[i + columns] : backButton };
            backButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = active[active.Length - 1],
                selectOnDown = active[0], selectOnLeft = sections != null ? sections.ActiveTab : active[active.Length - 1], selectOnRight = active[0] };
            if (sections != null) sections.WireTabNavigation(active[0], backButton);
        }
    }
}
