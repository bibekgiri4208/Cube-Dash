using UnityEngine;

namespace CubeDash
{
    /// <summary>One-shot, unscaled slide/fade stagger; never changes button scale or saved layout.</summary>
    public sealed class MainMenuEntrance : MonoBehaviour
    {
        [SerializeField] private RectTransform[] ribbons = new RectTransform[0];
        [SerializeField] private CanvasGroup[] fades = new CanvasGroup[0];
        [SerializeField, Min(0.01f)] private float duration = 0.5f;
        [SerializeField, Min(0)] private float stagger = 0.18f;
        [SerializeField, Min(0)] private float startupDelay = 0.12f;
        [SerializeField, Min(0)] private float slideDistance = 130f;
        private Vector2[] positions;
        private float age;
        private int warmupFrames;
        public bool Playing { get; private set; }

        private void OnEnable()
        {
            if (positions == null)
            {
                positions = new Vector2[ribbons.Length];
                for (int i = 0; i < ribbons.Length; i++) positions[i] = ribbons[i].anchoredPosition;
            }
            age = 0; warmupFrames = 2; Playing = true; Apply();
        }
        private void Update() => Tick(Time.unscaledDeltaTime);
        private void Tick(float dt)
        {
            if (!Playing) return;
            // Scene loading/shader warmup can give the first visible frame a huge delta.
            // Keep two initial frames hidden and bound subsequent steps so the stagger cannot skip.
            if (warmupFrames > 0) { warmupFrames--; return; }
            age += Mathf.Clamp(dt, 0, 0.05f);
            Apply();
            if (age >= startupDelay + duration + Mathf.Max(0, ribbons.Length - 1) * stagger) Complete();
        }
        private void Apply()
        {
            for (int i = 0; i < ribbons.Length; i++)
            {
                float t = Mathf.Clamp01((age - startupDelay - i * stagger) / Mathf.Max(0.01f, duration));
                float ease = 1 - Mathf.Pow(1 - t, 3);
                ribbons[i].anchoredPosition = positions[i] + new Vector2(-slideDistance * (1 - ease), 0);
                fades[i].alpha = ease;
                fades[i].blocksRaycasts = ease > 0.15f;
            }
        }
        public void Complete()
        {
            Playing = false;
            if (positions == null) return;
            for (int i = 0; i < ribbons.Length; i++)
            {
                ribbons[i].anchoredPosition = positions[i];
                fades[i].alpha = 1; fades[i].blocksRaycasts = true;
            }
        }
        private void OnDisable() => Complete();
    }
}
