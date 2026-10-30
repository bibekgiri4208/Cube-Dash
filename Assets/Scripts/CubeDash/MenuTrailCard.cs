using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CubeDash
{
    public sealed class MenuTrailCard : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private MenuShopSections sections;
        [SerializeField] private PlayerTrail style;
        [SerializeField] private Button action;
        [SerializeField] private Text actionLabel, status;
        public PlayerTrail Style => style;
        public Button Action => action;
        private void OnEnable() { GameEconomy.Changed += Refresh; Refresh(); }
        private void OnDisable() => GameEconomy.Changed -= Refresh;

        public void Refresh()
        {
            bool owned = GameEconomy.OwnsTrail(style), equipped = GameEconomy.EquippedTrail == style;
            actionLabel.text = !owned ? "BUY" : equipped ? "UNEQUIP" : "EQUIP";
            status.text = equipped ? "EQUIPPED" : owned ? "OWNED" : GameEconomy.Coins < GameEconomy.TrailPrice
                ? "NEED " + (GameEconomy.TrailPrice - GameEconomy.Coins) + " MORE COINS" : "250 COINS";
            action.interactable = true;
        }

        public void PurchaseOrToggle()
        {
            if (!isActiveAndEnabled || !sections.CanPurchase) return;
            if (!GameEconomy.OwnsTrail(style)) GameEconomy.TryBuyTrail(style);
            else GameEconomy.EquipTrail(GameEconomy.EquippedTrail == style ? PlayerTrail.None : style);
            Refresh();
        }

        public void OnSelect(BaseEventData data)
        {
            var scroll = GetComponentInParent<ScrollRect>(); if (scroll == null) return;
            Canvas.ForceUpdateCanvases();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, transform.parent.parent);
            float shift = bounds.max.y > scroll.viewport.rect.yMax ? scroll.viewport.rect.yMax - bounds.max.y
                : bounds.min.y < scroll.viewport.rect.yMin ? scroll.viewport.rect.yMin - bounds.min.y : 0;
            if (shift != 0) { scroll.StopMovement(); scroll.content.anchoredPosition += Vector2.up * shift; }
        }
    }
}
