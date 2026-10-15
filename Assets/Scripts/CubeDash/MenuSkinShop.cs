using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Updates authored wallet readouts, the Epic product and the equipped menu model.</summary>
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
            bool owned = GameEconomy.OwnsSupercar, equipped = GameEconomy.SupercarEquipped;
            actionLabel.text = !owned ? "BUY" : equipped ? "UNEQUIP" : "EQUIP";
            action.interactable = owned || GameEconomy.Coins >= GameEconomy.SupercarPrice;
            status.text = equipped ? "SUPERCAR EQUIPPED" : owned ? "OWNED - READY TO EQUIP"
                : GameEconomy.Coins < GameEconomy.SupercarPrice ? "NEED " + (GameEconomy.SupercarPrice - GameEconomy.Coins) + " MORE COINS" : "EPIC SUPERCAR - 100 COINS";
            cubePreview.SetActive(!equipped);
            carPreview.SetActive(equipped);
        }

        public void PurchaseOrToggle()
        {
            // Hidden/closing panels cannot spend coins through stale submits or callbacks.
            if (SceneLoader.IsLoading || menu.CurrentPanel != menu.ShopPanel || !menu.ShopPanel.activeInHierarchy) return;
            if (menu.ShopPanel.TryGetComponent(out CanvasGroup group) && !group.interactable) return;
            if (!GameEconomy.OwnsSupercar) GameEconomy.TryBuySupercar();
            else GameEconomy.EquipSupercar(!GameEconomy.SupercarEquipped);
            Refresh();
        }
    }
}
