using UnityEngine;

namespace CubeDash
{
    /// <summary>Reversible modal fade/lift. A closing panel loses interaction immediately.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class MenuPanelMotion : MonoBehaviour
    {
        [SerializeField] private RectTransform card;
        [SerializeField] private RectTransform shadow;
        [SerializeField, Min(0.01f)] private float openDuration = 0.28f;
        [SerializeField, Min(0.01f)] private float closeDuration = 0.18f;
        private CanvasGroup group;
        private Vector2 cardPosition, shadowPosition;
        private Vector3 cardScale, shadowScale;
        private float progress, from, target, age;
        private bool initialized;
        public bool Closing => Playing && target == 0;
        public bool Playing { get; private set; }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true; group = GetComponent<CanvasGroup>();
            cardPosition = card.anchoredPosition; cardScale = card.localScale;
            if (shadow != null) { shadowPosition = shadow.anchoredPosition; shadowScale = shadow.localScale; }
        }
        private void OnEnable() { Initialize(); progress = 0; Begin(1); Apply(); }
        public void Open()
        {
            if (!gameObject.activeSelf) { gameObject.SetActive(true); return; }
            Initialize(); Begin(1);
        }
        public void Close()
        {
            if (!gameObject.activeSelf || Closing) return;
            Initialize(); Begin(0);
        }
        private void Begin(float value)
        {
            from = progress; target = value; age = 0; Playing = true;
            group.interactable = value == 1; group.blocksRaycasts = value == 1;
        }
        private void Update() => Tick(Time.unscaledDeltaTime);
        private void Tick(float dt)
        {
            if (!Playing) return;
            age += Mathf.Max(0, dt);
            float t = Mathf.Clamp01(age / Mathf.Max(0.01f, target == 1 ? openDuration : closeDuration));
            float ease = target == 1 ? 1 - Mathf.Pow(1 - t, 3) : t * t * (3 - 2 * t);
            progress = Mathf.Lerp(from, target, ease); Apply();
            if (t < 1) return;
            Playing = false;
            if (target == 0) gameObject.SetActive(false);
        }
        private void Apply()
        {
            group.alpha = progress;
            Vector2 offset = new Vector2(0, -28 * (1 - progress));
            float scale = Mathf.Lerp(0.95f, 1, progress);
            card.anchoredPosition = cardPosition + offset; card.localScale = cardScale * scale;
            if (shadow != null) { shadow.anchoredPosition = shadowPosition + offset; shadow.localScale = shadowScale * scale; }
        }
        private void OnDisable()
        {
            Playing = false;
            if (!initialized) return;
            card.anchoredPosition = cardPosition; card.localScale = cardScale;
            if (shadow != null) { shadow.anchoredPosition = shadowPosition; shadow.localScale = shadowScale; }
            group.alpha = 1; group.interactable = group.blocksRaycasts = true;
        }
    }
}
