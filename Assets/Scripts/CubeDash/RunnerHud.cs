using UnityEngine;
using UnityEngine.EventSystems;
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
        private float entrance = 1;
        private Color scoreColor;
        private CanvasGroup menuGroup;
        private CanvasGroup endGroup;
        private RectTransform endComposition;
        private GameObject scorePanel;
        private GameObject objectivePanel;
        private GameObject brand;

        public void Initialize(CubeDashGame controller)
        {
            game = controller;
            speed.color = game.Track.ColorMaterial(game.PlayerCubeColor).color;
            scoreColor = distance.color;
            menuGroup = overlay.GetComponent<CanvasGroup>();
            scorePanel = safeRoot.Find("Score Panel")?.gameObject;
            objectivePanel = safeRoot.Find("Color Objective Panel")?.gameObject;
            brand = safeRoot.Find("Brand")?.gameObject;
            if (gameOverOverlay != null)
            {
                endGroup = gameOverOverlay.GetComponent<CanvasGroup>();
                endComposition = gameOverOverlay.transform.Find("Abstract Composition") as RectTransform;
            }
            UpdateStats();
        }

        public void NotifyCollection(int count)
        {
            scorePulse = 1;
            if (collectionFeedback != null)
            {
                collectionFeedback.text = "+" + count;
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
                shownScore = shownMetres = -1;
                distance.rectTransform.localScale = Vector3.one;
                if (collectionFeedback != null) collectionFeedback.gameObject.SetActive(false);
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
            controls.SetActive(running);
            pauseButton.SetActive(running);
            secondaryButton.SetActive(state == CubeDashGame.RunState.Paused);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            switch (state)
            {
                case CubeDashGame.RunState.Ready:
                    heading.text = "CUBE DASH";
                    description.text = "Collect " + game.PlayerCubeColor.ToString().ToUpperInvariant() + ". Dodge the rest.\nArrows / A D / swipe to move.";
                    actionLabel.text = "TAP TO START";
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
                collectionFeedback.rectTransform.anchoredPosition = new Vector2(194, -70 + (1 - scorePulse) * 22);
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
