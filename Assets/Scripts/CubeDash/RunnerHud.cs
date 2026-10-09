using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Updates existing Canvas objects. Layout and button events are saved in the scene.</summary>
    public sealed class RunnerHud : MonoBehaviour
    {
        [Header("Scene UI references")]
        [SerializeField] private CubeDashGame game = null;
        [SerializeField] private RectTransform safeRoot = null;
        [SerializeField] private RectTransform card = null;
        [SerializeField] private GameObject overlay = null;
        [SerializeField] private GameObject controls = null;
        [SerializeField] private GameObject pauseButton = null;
        [SerializeField] private GameObject secondaryButton = null;
        [SerializeField] private Text distance = null;
        [SerializeField] private Text best = null;
        [SerializeField] private Text speed = null;
        [SerializeField] private Text heading = null;
        [SerializeField] private Text description = null;
        [SerializeField] private Text actionLabel = null;
        [Header("Minimal game-over UI")]
        [SerializeField] private GameObject gameOverOverlay = null;
        [SerializeField] private Text gameOverScore = null;
        [SerializeField] private Text collectionFeedback = null;
        public GameObject GameOverOverlay => gameOverOverlay;
        public Text GameOverScore => gameOverScore;
        private Rect lastSafeArea;
        private Vector2 lastScreen;
        private int shownScore = -1;
        private int shownBest = -1;
        private int shownMetres = -1;
        private float scorePulse;
        private int feedbackCount;
        private Vector2 feedbackOrigin;
        private float entrance = 1;
        private Color scoreColor;
        private CanvasGroup menuGroup;
        private CanvasGroup endGroup;
        private RectTransform endComposition;
        private GameObject scorePanel;
        private GameObject objectivePanel;
        private GameObject brand;
        private Text desktopHint;
        private Text menuHint;
        private Text retryHint;
        private Button primaryButton;
        private Button retryButton;
        public bool UsingGamepad { get; private set; }

        public void Initialize(CubeDashGame controller)
        {
            game = controller;
            speed.color = game.Track.ColorMaterial(game.PlayerCubeColor).color;
            scoreColor = distance.color;
            if (collectionFeedback != null) feedbackOrigin = collectionFeedback.rectTransform.anchoredPosition;
            menuGroup = overlay.GetComponent<CanvasGroup>();
            scorePanel = safeRoot.Find("Score Panel")?.gameObject;
            objectivePanel = safeRoot.Find("Color Objective Panel")?.gameObject;
            brand = safeRoot.Find("Brand")?.gameObject;
            desktopHint = safeRoot.Find("Desktop Controls Hint")?.GetComponent<Text>();
            menuHint = card.Find("Keyboard Hint")?.GetComponent<Text>();
            primaryButton = card.Find("Primary Action")?.GetComponent<Button>();
            controls.SetActive(false);
            InputSystemUIInputModule module = EventSystem.current != null ? EventSystem.current.GetComponent<InputSystemUIInputModule>() : null;
            if (module != null)
            {
                // Gameplay owns confirm/cancel. Keep the module's pointer and navigation actions.
                module.submit = null;
                module.cancel = null;
            }
            if (gameOverOverlay != null)
            {
                endGroup = gameOverOverlay.GetComponent<CanvasGroup>();
                endComposition = gameOverOverlay.transform.Find("Abstract Composition") as RectTransform;
                retryButton = endComposition != null ? endComposition.Find("Retry")?.GetComponent<Button>() : null;
                retryHint = endComposition != null ? endComposition.Find("Tap Hint")?.GetComponent<Text>() : null;
            }
            UpdateStats();
        }

        public void NotifyCollection(int count)
        {
            if (count <= 0) return;
            feedbackCount = scorePulse > 0 ? feedbackCount + count : count;
            scorePulse = 1;
            if (collectionFeedback != null)
            {
                collectionFeedback.text = "+" + feedbackCount;
                Color color = collectionFeedback.color;
                color.a = 1;
                collectionFeedback.color = color;
                collectionFeedback.rectTransform.anchoredPosition = feedbackOrigin;
                collectionFeedback.gameObject.SetActive(true);
            }
        }

        public void PrimaryAction()
        {
            if (game.State == CubeDashGame.RunState.Paused) game.TogglePause();
            else game.StartRun();
        }

        public void MoveLeft() => game.ChangeLane(-1);
        public void MoveRight() => game.ChangeLane(1);

        public void SetInputMode(bool gamepad)
        {
            if (UsingGamepad == gamepad) return;
            UsingGamepad = gamepad;
            RefreshInputPrompts();
            if (gamepad && game.State != CubeDashGame.RunState.Running && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == null) FocusPrimary();
        }

        public void ConfirmSelection()
        {
            if (game.State == CubeDashGame.RunState.Running) return;
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            Button button = selected != null ? selected.GetComponent<Button>() : null;
            if (button != null && button.gameObject.activeInHierarchy && button.IsInteractable() &&
                (button == primaryButton || button == retryButton || button.gameObject == secondaryButton)) button.onClick.Invoke();
            else PrimaryAction();
        }

        public void CycleMenuSelection()
        {
            if (EventSystem.current == null || game.State == CubeDashGame.RunState.Running) return;
            if (game.State != CubeDashGame.RunState.Paused) { FocusPrimary(); return; }
            GameObject current = EventSystem.current.currentSelectedGameObject;
            EventSystem.current.SetSelectedGameObject(current == secondaryButton ? primaryButton.gameObject : secondaryButton);
        }

        private void FocusPrimary()
        {
            if (EventSystem.current == null) return;
            Button button = game.State == CubeDashGame.RunState.GameOver ? retryButton : primaryButton;
            EventSystem.current.SetSelectedGameObject(button != null ? button.gameObject : null);
        }

        private void RefreshInputPrompts()
        {
            if (game.State == CubeDashGame.RunState.Ready)
            {
                description.text = "Collect " + game.PlayerCubeColor.ToString().ToUpperInvariant() + ". Dodge the rest.\n" +
                    (UsingGamepad ? "Left stick or D-pad to change lanes." : "A / D or arrow keys to change lanes.");
                actionLabel.text = "START RUN";
            }
            if (menuHint != null) menuHint.text = UsingGamepad ? "A / Cross confirm   ·   D-pad navigate   ·   Menu pause" :
                "Enter / Space confirm   ·   Tab / arrows select   ·   Esc pause";
            if (desktopHint != null) desktopHint.text = UsingGamepad ? "LEFT STICK / D-PAD   ·   MENU pause" :
                "A / D or LEFT / RIGHT   ·   P / ESC pause   ·   R restart";
            if (retryHint != null) retryHint.text = UsingGamepad ? "A / Cross or X / Square to retry" : "Enter / Space or R to retry";
        }

        public void Show(CubeDashGame.RunState state)
        {
            bool running = state == CubeDashGame.RunState.Running;
            bool ended = state == CubeDashGame.RunState.GameOver;
            entrance = 0;
            if (!running && menuGroup != null) menuGroup.alpha = 0;
            if (ended && endGroup != null) endGroup.alpha = 0;
            if (state == CubeDashGame.RunState.Running && game.Score == 0)
            {
                scorePulse = 0;
                feedbackCount = 0;
                shownScore = shownMetres = -1;
                distance.rectTransform.localScale = Vector3.one;
                distance.color = scoreColor;
                if (collectionFeedback != null)
                {
                    collectionFeedback.rectTransform.anchoredPosition = feedbackOrigin;
                    collectionFeedback.gameObject.SetActive(false);
                }
            }
            overlay.SetActive(!running && !(ended && gameOverOverlay != null));
            if (gameOverOverlay != null) gameOverOverlay.SetActive(ended);
            if (gameOverScore != null) gameOverScore.text = game.Score.ToString();
            distance.gameObject.SetActive(!ended);
            best.gameObject.SetActive(!ended);
            speed.gameObject.SetActive(!ended);
            if (scorePanel != null) scorePanel.SetActive(!ended);
            if (objectivePanel != null) objectivePanel.SetActive(!ended);
            if (brand != null) brand.SetActive(!ended);
            if (ended && collectionFeedback != null) collectionFeedback.gameObject.SetActive(false);
            controls.SetActive(false);
            if (desktopHint != null) desktopHint.gameObject.SetActive(running);
            pauseButton.SetActive(running);
            secondaryButton.SetActive(state == CubeDashGame.RunState.Paused);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            switch (state)
            {
                case CubeDashGame.RunState.Ready:
                    heading.text = "CUBE DASH";
                    actionLabel.text = "START RUN";
                    break;
                case CubeDashGame.RunState.Paused:
                    heading.text = "PAUSED";
                    description.text = "Take a breath. Keep your streak.";
                    actionLabel.text = "RESUME RUN";
                    break;
                case CubeDashGame.RunState.GameOver:
                    heading.text = "GAME OVER";
                    description.text = game.Score + " POINTS";
                    actionLabel.text = "TRY AGAIN";
                    break;
            }
            RefreshInputPrompts();
            if (!running) FocusPrimary();
        }

        public void UpdateStats()
        {
            if (shownScore != game.Score) { shownScore = game.Score; distance.text = shownScore + " POINTS"; }
            if (shownBest != game.Best) { shownBest = game.Best; best.text = "BEST  " + shownBest; }
            int metres = Mathf.FloorToInt(game.Distance);
            if (shownMetres != metres)
            {
                shownMetres = metres;
                speed.text = "COLLECT " + game.PlayerCubeColor.ToString().ToUpperInvariant() + "  ·  " + metres + " m";
            }
            AnimateFeedback();
            Rect area = Screen.safeArea;
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            if (screen.x <= 0 || screen.y <= 0 || (area == lastSafeArea && screen == lastScreen)) return;
            lastSafeArea = area;
            lastScreen = screen;
            safeRoot.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            safeRoot.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Min(540, Mathf.Max(240, safeRoot.rect.width - 32));
            card.sizeDelta = new Vector2(width, 360);
            heading.rectTransform.sizeDelta = new Vector2(width - 30, 86);
            description.rectTransform.sizeDelta = new Vector2(width - 36, 100);
            float objectiveY = safeRoot.rect.width < 780 ? -174 : -42;
            speed.rectTransform.anchoredPosition = new Vector2(0, objectiveY);
            if (objectivePanel != null) ((RectTransform)objectivePanel.transform).anchoredPosition = new Vector2(0, objectiveY);
            Transform controlsHint = controls.transform.Find("Controls Hint");
            if (controlsHint != null) controlsHint.gameObject.SetActive(safeRoot.rect.width >= 580);
        }

        private void AnimateFeedback()
        {
            float dt = game.State == CubeDashGame.RunState.Paused ? 0 : Time.unscaledDeltaTime;
            scorePulse = Mathf.MoveTowards(scorePulse, 0, dt * 2.7f);
            distance.rectTransform.localScale = Vector3.one * (1 + Mathf.Sin(scorePulse * Mathf.PI) * 0.16f);
            distance.color = Color.Lerp(scoreColor, new Color(1f, 0.75f, 0.42f), scorePulse);
            if (collectionFeedback != null && collectionFeedback.gameObject.activeSelf)
            {
                Color color = collectionFeedback.color;
                color.a = scorePulse;
                collectionFeedback.color = color;
                collectionFeedback.rectTransform.anchoredPosition = feedbackOrigin + Vector2.up * ((1 - scorePulse) * 22);
                if (scorePulse <= 0) collectionFeedback.gameObject.SetActive(false);
            }
            entrance = Mathf.Min(1, entrance + Time.unscaledDeltaTime * 4.5f);
            float eased = 1 - Mathf.Pow(1 - entrance, 3);
            if (menuGroup != null && overlay.activeSelf)
            {
                menuGroup.alpha = eased;
                card.localScale = Vector3.one * Mathf.Lerp(0.94f, 1, eased);
            }
            if (endGroup != null && gameOverOverlay.activeSelf)
            {
                endGroup.alpha = eased;
                if (endComposition != null) endComposition.localScale = Vector3.one * Mathf.Lerp(0.9f, 1, eased);
            }
        }
    }
}
