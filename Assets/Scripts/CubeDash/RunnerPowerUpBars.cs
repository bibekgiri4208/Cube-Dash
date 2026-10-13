using UnityEngine;

namespace CubeDash
{
    /// <summary>Stacks active, saved power-up cards down the left safe-area edge, without runtime UI creation.</summary>
    [DisallowMultipleComponent]
    public sealed class RunnerPowerUpBars : MonoBehaviour
    {
        [SerializeField] private CubeDashGame game;
        [SerializeField] private RectTransform safeRoot;
        [SerializeField] private RectTransform stack;
        [SerializeField] private PowerUpDurationRow[] rows = new PowerUpDurationRow[0];
        public RectTransform Stack => stack;
        public PowerUpDurationRow[] Rows => rows;
        public void Initialize(CubeDashGame controller) { game = controller; Refresh(); }
        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (game == null || stack == null || safeRoot == null) return;
            bool visible = game.State == CubeDashGame.RunState.Running || (game.State == CubeDashGame.RunState.Paused && !game.CountdownActive);
            int count = 0;
            foreach (PowerUpDurationRow row in rows)
                if (row != null && row.Refresh(game, visible))
                {
                    row.Rect.anchoredPosition = new Vector2(0, -count * 64);
                    count++;
                }
            float top = safeRoot.rect.width < 780 ? 242 : 164;
            stack.anchoredPosition = new Vector2(24, -top);
            stack.sizeDelta = new Vector2(216, count > 0 ? count * 64 - 8 : 0);
            float available = Mathf.Max(0, safeRoot.rect.height - top - 24);
            float scale = count > 0 ? Mathf.Min(1, available / Mathf.Max(1, stack.sizeDelta.y)) : 1;
            stack.localScale = Vector3.one * scale;
            if (stack.gameObject.activeSelf != (count > 0)) stack.gameObject.SetActive(count > 0);
        }
    }
}
