using UnityEngine;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Silent anticipation, dimensional gift opening and a staggered coin-to-wallet celebration.</summary>
    public sealed class MenuGiftReveal : MonoBehaviour
    {
        [SerializeField] private MenuGiftGraphic artwork;
        [SerializeField] private CanvasGroup reward;
        [SerializeField] private Text amount;
        private float age;
        public bool Playing { get; private set; }
        public Text Amount => amount;
        public MenuGiftGraphic Artwork => artwork;
        public const float Duration = 2.4f;

        public void Play()
        {
            age = 0;
            Playing = true;
            Apply(0);
        }

        private void Update()
        {
            if (!Playing) return;
            age += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            Apply(age);
            if (age >= Duration) Playing = false;
        }

        private void Apply(float time)
        {
            if (artwork != null) artwork.Frame = time;
            float reveal = Mathf.Clamp01((time - 1.05f) / 0.52f);
            float eased = 1 - Mathf.Pow(1 - reveal, 3);
            reward.alpha = eased;
            var rewardRect = (RectTransform)reward.transform;
            rewardRect.anchoredPosition = new Vector2(0, -18 * (1 - eased));
            float pop = 1 + Mathf.Sin(reveal * Mathf.PI) * 0.09f;
            rewardRect.localScale = Vector3.one * (Mathf.Lerp(0.85f, 1, eased) * pop);
            float count = Mathf.Clamp01((time - 1.12f) / 0.82f);
            int coins = Mathf.RoundToInt(GameEconomy.RedeemGiftCoins * (1 - Mathf.Pow(1 - count, 3)));
            amount.text = "+" + coins.ToString("N0") + " COINS";
        }

        private void OnDisable() => Playing = false;
    }
}
