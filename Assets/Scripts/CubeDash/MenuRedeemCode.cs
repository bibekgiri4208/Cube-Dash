using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Saved Options sub-dialog. Currency is banked before its cosmetic reveal begins.</summary>
    public sealed class MenuRedeemCode : MonoBehaviour
    {
        [SerializeField] private MainMenuController menu;
        [SerializeField] private InputField codeInput;
        [SerializeField] private GameObject form;
        [SerializeField] private Text status;
        [SerializeField] private Button redeem;
        [SerializeField] private Button again;
        [SerializeField] private Button back;
        [SerializeField] private MenuGiftReveal gift;
        private bool showingGift;

        public InputField CodeInput => codeInput;
        public MenuGiftReveal Gift => gift;
        public bool ShowingGift => showingGift;

        private bool CanInteract => isActiveAndEnabled && menu.CurrentPanel == gameObject &&
            !menu.Loading && !SceneLoader.IsLoading && GetComponent<CanvasGroup>().interactable;

        private void Update()
        {
            if (!CanInteract) return;
            if (showingGift && !gift.Playing && !again.interactable)
            {
                again.interactable = true;
                Select(again.gameObject);
            }
        }

        public void ResetForm()
        {
            if (!CanInteract) return;
            showingGift = false;
            gift.gameObject.SetActive(false);
            again.gameObject.SetActive(false);
            form.SetActive(true);
            codeInput.SetTextWithoutNotify(string.Empty);
            status.text = string.Empty;
            back.navigation = Nav(redeem, codeInput);
            Select(codeInput.gameObject);
        }

        public void Redeem()
        {
            // Prevent double submits during the reveal and stale callbacks from hidden/closing panels.
            if (!CanInteract || showingGift) return;
            if (!GameEconomy.TryRedeemCode(codeInput.text))
            {
                status.text = string.IsNullOrWhiteSpace(codeInput.text) ? "Enter a redeem code." : "Invalid code. Please try again.";
                Select(codeInput.gameObject);
                return;
            }
            showingGift = true;
            codeInput.DeactivateInputField();
            form.SetActive(false);
            gift.gameObject.SetActive(true);
            gift.Play();
            again.gameObject.SetActive(true);
            again.interactable = false;
            var navigation = Nav(again, again);
            navigation.selectOnLeft = navigation.selectOnRight = again;
            back.navigation = navigation;
            Select(back.gameObject);
        }

        public void SubmitCode(string value) => Redeem();

        public void RedeemAgain()
        {
            if (CanInteract && showingGift && !gift.Playing) ResetForm();
        }

        private static Navigation Nav(Selectable up, Selectable down) => new Navigation
        { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };

        private static void Select(GameObject target)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
