using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>One authored skin product, with persistent buy/equip wiring and scroll-into-view navigation.</summary>
    public sealed class MenuSkinCard : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private MenuSkinShop shop;
        [SerializeField] private PlayerSkin skin;
        [SerializeField] private Button action;
        [SerializeField] private Text actionLabel;
        [SerializeField] private Text status;
        [SerializeField] private GameObject preview;
        public PlayerSkin Skin => skin;
        public Button Action => action;
        public GameObject Preview => preview;

        public void Refresh()
        {
            bool owned = GameEconomy.Owns(skin), equipped = GameEconomy.EquippedSkin == skin;
            actionLabel.text = !owned ? "BUY" : equipped ? "UNEQUIP" : "EQUIP";
            // Keep unaffordable products navigable so controller users can read every card.
            action.interactable = true;
            status.text = equipped ? "EQUIPPED" : owned ? "OWNED" : GameEconomy.Coins < GameEconomy.SkinPrice
                ? "NEED " + (GameEconomy.SkinPrice - GameEconomy.Coins) + " MORE COINS" : "100 COINS";
            if (preview != null) preview.SetActive(equipped);
        }

        public void PurchaseOrToggle() => shop.PurchaseOrToggle(skin);

        public void OnSelect(BaseEventData eventData)
        {
            var scroll = GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.content == null) return;
            Canvas.ForceUpdateCanvases();
            var card = transform.parent.parent.GetComponent<RectTransform>();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, card);
            Rect view = scroll.viewport.rect;
            float shift = bounds.max.y > view.yMax ? view.yMax - bounds.max.y
                : bounds.min.y < view.yMin ? view.yMin - bounds.min.y : 0;
            if (shift != 0) { scroll.StopMovement(); scroll.content.anchoredPosition += Vector2.up * shift; }
        }
    }
}
