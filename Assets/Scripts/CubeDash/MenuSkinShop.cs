using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Updates saved wallets, skin cards and the equipped menu model. Creates no runtime UI.</summary>
    public sealed class MenuSkinShop : MonoBehaviour
    {
        [SerializeField] private MainMenuController menu;
        [SerializeField] private Text menuCoins;
        [SerializeField] private Text shopCoins;
        [SerializeField] private Button action;
        [SerializeField] private Text actionLabel;
        [SerializeField] private Text status;
        [SerializeField] private GameObject cubePreview;
        [SerializeField] private GameObject carPreview;
        [SerializeField] private MenuSkinCard[] cards = new MenuSkinCard[0];
        public MenuSkinCard[] Cards => cards;
        public Text MenuCoins => menuCoins;
        public Text ShopCoins => shopCoins;
        public Button Action => action;
        public GameObject CarPreview => carPreview;
        public GameObject CubePreview => cubePreview;

        private void OnEnable() { GameEconomy.Changed += Refresh; Refresh(); }
        private void OnDisable() => GameEconomy.Changed -= Refresh;

        public void Refresh()
        {
            string balance = GameEconomy.Coins.ToString("N0");
            menuCoins.text = shopCoins.text = balance;
            if (cards.Length > 0)
            {
                cubePreview.SetActive(GameEconomy.EquippedSkin == PlayerSkin.Cube);
                foreach (var card in cards) if (card != null) card.Refresh();
                status.text = GameEconomy.EquippedSkin == PlayerSkin.Cube ? "" : GameEconomy.SkinName(GameEconomy.EquippedSkin) + " EQUIPPED";
                return;
            }
            bool owned = GameEconomy.OwnsSupercar, equipped = GameEconomy.SupercarEquipped;
            actionLabel.text = !owned ? "BUY" : equipped ? "UNEQUIP" : "EQUIP";
            action.interactable = owned || GameEconomy.Coins >= GameEconomy.SupercarPrice;
            status.text = equipped ? "SUPERCAR EQUIPPED" : owned ? "OWNED - READY TO EQUIP"
                : GameEconomy.Coins < GameEconomy.SupercarPrice ? "NEED " + (GameEconomy.SupercarPrice - GameEconomy.Coins) + " MORE COINS" : "NEON SUPERCAR - 100 COINS";
            cubePreview.SetActive(!equipped);
            carPreview.SetActive(equipped);
        }

        public void PurchaseOrToggle()
            => PurchaseOrToggle(PlayerSkin.Supercar);

        public void PurchaseOrToggle(PlayerSkin skin)
        {
            // Hidden/closing panels cannot spend coins through stale submits or callbacks.
            if (SceneLoader.IsLoading || menu.CurrentPanel != menu.ShopPanel || !menu.ShopPanel.activeInHierarchy) return;
            if (menu.ShopPanel.TryGetComponent(out CanvasGroup group) && !group.interactable) return;
            if (!GameEconomy.Owns(skin)) GameEconomy.TryBuy(skin);
            else GameEconomy.Equip(GameEconomy.EquippedSkin == skin ? PlayerSkin.Cube : skin);
            Refresh();
        }
    }
}
