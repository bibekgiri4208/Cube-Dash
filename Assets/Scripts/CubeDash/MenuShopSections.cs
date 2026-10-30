using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Independent skin/trail categories. Hiding a category disables all of its purchase callbacks.</summary>
    public sealed class MenuShopSections : MonoBehaviour
    {
        [SerializeField] private MainMenuController menu;
        [SerializeField] private RectTransform skinsViewport;
        [SerializeField] private RectTransform trailsViewport;
        [SerializeField] private RectTransform tabs;
        [SerializeField] private Button skinsTab, trailsTab;
        [SerializeField] private MenuTrailCard[] cards;
        [SerializeField] private GridLayoutGroup trailGrid;
        [SerializeField] private RectTransform[] trailContents;
        [SerializeField] private Button[] trailActions;
        public bool TrailsSelected { get; private set; }
        public RectTransform Tabs => tabs;
        public RectTransform TrailsViewport => trailsViewport;
        public GridLayoutGroup TrailGrid => trailGrid;
        public RectTransform[] TrailContents => trailContents;
        public Button[] TrailActions => trailActions;
        public MenuTrailCard[] Cards => cards;
        public Button ActiveTab => TrailsSelected ? trailsTab : skinsTab;
        public bool CanPurchase => isActiveAndEnabled && TrailsSelected && menu.CurrentPanel == gameObject &&
            !menu.Loading && !SceneLoader.IsLoading && GetComponent<CanvasGroup>().interactable;

        private void OnEnable() => Activate(TrailsSelected, false);
        public void ShowSkins() { if (CanSwitch()) Activate(false, true); }
        public void ShowTrails() { if (CanSwitch()) Activate(true, true); }

        private bool CanSwitch() => isActiveAndEnabled && menu.CurrentPanel == gameObject &&
            !SceneLoader.IsLoading && !menu.Loading && GetComponent<CanvasGroup>().interactable;

        private void Activate(bool trails, bool focus)
        {
            TrailsSelected = trails;
            skinsViewport.gameObject.SetActive(!trails); trailsViewport.gameObject.SetActive(trails);
            StyleTab(skinsTab, !trails); StyleTab(trailsTab, trails);
            GetComponent<MenuSkinsLayout>().RefreshProducts();
            if (focus && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(ActiveTab.gameObject);
        }

        private static void StyleTab(Button button, bool selected)
        {
            button.GetComponent<MenuRibbonGraphic>().Primary = selected;
            button.GetComponentInChildren<Text>(true).color = selected ? new Color(1, 0.99f, 0.94f) : new Color(0.018f, 0.014f, 0.025f);
        }

        public void WireTabNavigation(Button first, Button back)
        {
            skinsTab.navigation = Nav(trailsTab, first, back);
            trailsTab.navigation = Nav(skinsTab, first, back);
        }

        private static Navigation Nav(Button other, Button first, Button back) => new Navigation
        { mode = Navigation.Mode.Explicit, selectOnLeft = other, selectOnRight = other, selectOnDown = first, selectOnUp = back };
    }
}
