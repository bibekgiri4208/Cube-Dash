using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
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
        [SerializeField] private Text powerUpStatus = null;
        [SerializeField] private Text cheatStatus = null;
        [Header("Main-menu ribbon presentation (scene opt-in)")]
        [SerializeField] private bool ribbonTheme = false;
        [SerializeField] private GameObject countdownOverlay = null;
        [SerializeField] private Text countdownNumber = null;
        [SerializeField] private RectTransform powerUpRibbon = null;
        [SerializeField] private RectTransform cheatRibbon = null;
        [SerializeField] private RunnerPowerUpBars powerUpBars = null;
        public RunnerPowerUpBars PowerUpBars => powerUpBars;
        public bool RibbonTheme => ribbonTheme;
        public GameObject CountdownOverlay => countdownOverlay;
        public Text CountdownNumber => countdownNumber;
        public Text CheatStatus => cheatStatus;
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
        private GameObject menuAccent;
        private GameObject pauseMotif;
        private GameObject pauseQuit;
        private Button pauseQuitButton;
        private Text menuHint;
        private Text retryHint;
        private string powerUpMessage;
        private float powerUpMessageTimer;
        private Button primaryButton;
        private Button retryButton;
        private Button quitButton;
        private bool returningToMenu;
        public bool UsingGamepad { get; private set; }

        public void Initialize(CubeDashGame controller)
        {
            game = controller;
            if (powerUpBars != null) powerUpBars.Initialize(controller);
            if (!ribbonTheme) speed.color = new Color(1f, 0.78f, 0.25f);
            scoreColor = distance.color;
            if (collectionFeedback != null) feedbackOrigin = collectionFeedback.rectTransform.anchoredPosition;
            menuGroup = overlay.GetComponent<CanvasGroup>();
            scorePanel = safeRoot.Find("Score Panel")?.gameObject;
            objectivePanel = safeRoot.Find("Color Objective Panel")?.gameObject;
            brand = safeRoot.Find("Brand")?.gameObject;
            menuAccent = card.Find("Accent")?.gameObject;
            pauseMotif = card.Find("Pause Motif")?.gameObject;
            pauseQuit = card.Find("Quit")?.gameObject;
            pauseQuitButton = card.Find("Quit")?.GetComponent<Button>();
            menuHint = card.Find("Keyboard Hint")?.GetComponent<Text>();
            if (powerUpStatus == null) powerUpStatus = safeRoot.Find("Power-Up Status")?.GetComponent<Text>();
            if (cheatStatus == null) cheatStatus = safeRoot.Find("Cheat Status")?.GetComponent<Text>();
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
                quitButton = endComposition != null ? endComposition.Find("Quit")?.GetComponent<Button>() : null;
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

        public void NotifyPowerUp(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Shield: powerUpMessage = "SHIELD UP"; break;
                case PowerUpType.DoublePoints: powerUpMessage = "DOUBLE POINTS"; break;
                case PowerUpType.FighterPlane: powerUpMessage = "FIGHTER FLIGHT"; break;
                case PowerUpType.Truck: powerUpMessage = "TRUCK RAMPAGE"; break;
                case PowerUpType.Magnet: powerUpMessage = "COIN MAGNET"; break;
            }
            powerUpMessageTimer = 1.5f;
        }

        public void NotifyLandingShield()
        {
            powerUpMessage = "SAFE LANDING";
            powerUpMessageTimer = 1f;
        }

        public void NotifyTruckShield()
        {
            powerUpMessage = "EXIT SHIELD";
            powerUpMessageTimer = 1f;
        }

        public void NotifySupercar(bool enabled)
        {
            powerUpMessage = enabled ? "SUPERCAR  \xB7  SAME COLOR RULES" : "CUBE MODE";
            powerUpMessageTimer = 1.5f;
        }

        public void PrimaryAction()
        {
            if (game.State == CubeDashGame.RunState.Paused) game.TogglePause();
            else game.RequestRun();
        }

        /// <summary>Pause Quit returns to the landing scene; game-over Quit retains its exit behavior.</summary>
        public void QuitGame()
        {
            if (returningToMenu) return;
            if (game != null && game.State == CubeDashGame.RunState.Paused)
            {
                returningToMenu = true;
                SceneManager.LoadSceneAsync("Main Menu", LoadSceneMode.Single);
                return;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
            if (game.CountdownActive && game.State == CubeDashGame.RunState.Ready) return;
            if (game.State == CubeDashGame.RunState.Running) return;
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            Button button = selected != null ? selected.GetComponent<Button>() : null;
            if (button != null && button.gameObject.activeInHierarchy && button.IsInteractable() &&
                (button == primaryButton || button == retryButton || button == quitButton ||
                 button == pauseQuitButton || button.gameObject == secondaryButton)) button.onClick.Invoke();
            else PrimaryAction();
        }

        public void CycleMenuSelection()
        {
            if (EventSystem.current == null || game.State == CubeDashGame.RunState.Running) return;
            if (game.State != CubeDashGame.RunState.Paused) { FocusPrimary(); return; }
            GameObject current = EventSystem.current.currentSelectedGameObject;
            GameObject next;
            if (current == primaryButton.gameObject) next = secondaryButton;
            else if (current == secondaryButton) next = pauseQuit != null ? pauseQuit : primaryButton.gameObject;
            else next = primaryButton.gameObject;
            EventSystem.current.SetSelectedGameObject(next);
        }

        private void FocusPrimary()
        {
            if (EventSystem.current == null) return;
            if (ribbonTheme && game.State == CubeDashGame.RunState.Ready)
            {
                EventSystem.current.SetSelectedGameObject(null); return;
            }
            Button button = game.State == CubeDashGame.RunState.GameOver ? retryButton : primaryButton;
            EventSystem.current.SetSelectedGameObject(button != null ? button.gameObject : null);
        }

        private void RefreshInputPrompts()
        {
            if (game.State == CubeDashGame.RunState.Ready)
                description.text = "Collect coins. Dodge obstacles.";
            if (menuHint != null)
            {
                if (game.State == CubeDashGame.RunState.Ready)
                    menuHint.text = UsingGamepad ? "LEFT STICK / D-PAD move   \xB7   A / CROSS start" :
                        "A/D or ARROWS move  \xB7  ENTER/SPACE start  \xB7  ENTER x2 car";
                else
                    menuHint.text = UsingGamepad ? "A / Cross confirm   \xB7   D-pad navigate   \xB7   Menu pause" :
                        "Enter / Space confirm   \xB7   Tab / arrows select   \xB7   Esc pause";
            }
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
            overlay.SetActive(!running && !(ended && gameOverOverlay != null) && !(ribbonTheme && state == CubeDashGame.RunState.Ready));
            if (countdownOverlay != null) countdownOverlay.SetActive(state == CubeDashGame.RunState.Ready && game.CountdownActive);
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
            pauseButton.SetActive(running || (ribbonTheme && state == CubeDashGame.RunState.Ready && game.CountdownActive));
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
            if (pauseMotif != null) pauseMotif.SetActive(!ribbonTheme && state == CubeDashGame.RunState.Paused);
            if (menuAccent != null) menuAccent.SetActive(!ribbonTheme && state == CubeDashGame.RunState.Ready);
            if (pauseQuit != null) pauseQuit.SetActive(state == CubeDashGame.RunState.Paused);
            if (menuHint != null) menuHint.gameObject.SetActive(!ribbonTheme && state == CubeDashGame.RunState.Ready);
            RefreshInputPrompts();
            RefreshCheatStatus();
            if (powerUpBars != null) powerUpBars.Refresh();
            if (!running) FocusPrimary();
        }

        public void UpdateStats()
        {
            if (countdownNumber != null && game.CountdownActive)
                countdownNumber.text = Mathf.CeilToInt(game.CountdownRemaining).ToString();
            if (shownScore != game.Score) { shownScore = game.Score; distance.text = shownScore + " POINTS"; }
            if (shownBest != game.Best) { shownBest = game.Best; best.text = "BEST  " + shownBest; }
            int metres = Mathf.FloorToInt(game.Distance);
            if (shownMetres != metres)
            {
                shownMetres = metres;
                speed.text = ribbonTheme ? metres + " m" : "STAGE " + game.Stage + "  ·  " + metres + " m";
            }
            AnimateFeedback();
            UpdatePowerUpStatus();
            RefreshCheatStatus();
            Rect area = Screen.safeArea;
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            if (screen.x <= 0 || screen.y <= 0 || (area == lastSafeArea && screen == lastScreen)) return;
            lastSafeArea = area;
            lastScreen = screen;
            safeRoot.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            safeRoot.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Min(ribbonTheme ? 560 : 540, Mathf.Max(240, safeRoot.rect.width - 32));
            card.sizeDelta = new Vector2(width, ribbonTheme ? 440 : 360);
            heading.rectTransform.sizeDelta = new Vector2(width - 30, 86);
            description.rectTransform.sizeDelta = new Vector2(width - 36, 40);
            float objectiveY = safeRoot.rect.width < 780 ? -184 : ribbonTheme ? -60 : -42;
            speed.rectTransform.anchoredPosition = new Vector2(0, objectiveY);
            if (objectivePanel != null) ((RectTransform)objectivePanel.transform).anchoredPosition = new Vector2(0, objectiveY);
            Transform controlsHint = controls.transform.Find("Controls Hint");
            if (controlsHint != null) controlsHint.gameObject.SetActive(safeRoot.rect.width >= 580);
            if (ribbonTheme) LayoutRibbonMenus(width);
        }

        private void LayoutRibbonMenus(float width)
        {
            foreach (Button button in card.GetComponentsInChildren<Button>(true))
            {
                button.GetComponent<RectTransform>().sizeDelta = new Vector2(Mathf.Min(420, width - 64), 68);
                button.GetComponentInChildren<Text>(true).rectTransform.sizeDelta = new Vector2(Mathf.Min(420, width - 64) - 24, 64);
            }
            Transform header = card.Find("Ribbon header");
            if (header != null) header.GetComponent<RectTransform>().sizeDelta = new Vector2(width - 40, 98);
            if (endComposition != null)
            {
                float endWidth = Mathf.Min(520, Mathf.Max(240, safeRoot.rect.width - 32));
                endComposition.sizeDelta = new Vector2(endWidth, 440);
                foreach (Button button in endComposition.GetComponentsInChildren<Button>(true))
                {
                    button.GetComponent<RectTransform>().sizeDelta = new Vector2(Mathf.Min(380, endWidth - 64), 68);
                    button.GetComponentInChildren<Text>(true).rectTransform.sizeDelta = new Vector2(Mathf.Min(380, endWidth - 64) - 24, 64);
                }
                foreach (Text label in endComposition.GetComponentsInChildren<Text>(true))
                    if (label.transform.parent == endComposition) label.rectTransform.sizeDelta = new Vector2(endWidth - 48, label.rectTransform.sizeDelta.y);
                Transform endHeader = endComposition.Find("Ribbon header");
                if (endHeader != null) endHeader.GetComponent<RectTransform>().sizeDelta = new Vector2(endWidth - 40, 92);
            }
        }

        private void UpdatePowerUpStatus()
        {
            if (powerUpBars != null)
            {
                if (powerUpStatus != null) powerUpStatus.gameObject.SetActive(false);
                if (powerUpRibbon != null) powerUpRibbon.gameObject.SetActive(false);
                powerUpBars.Refresh();
                return;
            }
            if (powerUpStatus == null) return;
            if (powerUpMessageTimer > 0) powerUpMessageTimer = Mathf.Max(0, powerUpMessageTimer - Time.unscaledDeltaTime);
            if (game.State != CubeDashGame.RunState.Running)
            {
                powerUpStatus.gameObject.SetActive(false);
                if (powerUpRibbon != null) powerUpRibbon.gameObject.SetActive(false);
                return;
            }
            string status;
            if (powerUpMessageTimer > 0) status = powerUpMessage;
            else
            {
                status = "";
                if (game.Flying) status = "FLIGHT " + Mathf.CeilToInt(game.FlightRemaining) + "s";
                if (game.Trucking) status = "TRUCK " + Mathf.CeilToInt(game.TruckRemaining) + "s";
                if (game.SupercarDriving) status = "SUPERCAR  \xB7  ENTER x2: CUBE";
                if (game.LandingShieldRemaining > 0)
                    status = AppendStatus(status, "TIMED SHIELD " + Mathf.CeilToInt(game.LandingShieldRemaining) + "s");
                if (game.Shields > 0) status = AppendStatus(status, "SHIELD");
                if (game.DoublePointsActive) status = AppendStatus(status, "DOUBLE POINTS " + Mathf.CeilToInt(game.DoublePointsRemaining) + "s");
                if (game.MagnetActive) status = AppendStatus(status, "MAGNET " + Mathf.CeilToInt(game.MagnetRemaining) + "s");
            }
            powerUpStatus.text = status;
            powerUpStatus.gameObject.SetActive(status.Length > 0);
            powerUpStatus.rectTransform.anchoredPosition = new Vector2(0, safeRoot.rect.width < 780 ? (ribbonTheme ? -250 : -190) : (ribbonTheme ? -176 : -92));
            if (ribbonTheme)
            {
                powerUpStatus.rectTransform.sizeDelta = new Vector2(Mathf.Max(200, Mathf.Min(720, safeRoot.rect.width - 48)), 48);
                SyncStatusRibbon(powerUpRibbon, powerUpStatus);
            }
        }

        public void RefreshCheatStatus()
        {
            if (cheatStatus == null || game == null) return;
            cheatStatus.text = "CHEAT: INVINCIBILITY ACTIVE  |  I TO DISABLE";
            cheatStatus.gameObject.SetActive(game.InvincibilityEnabled);
            cheatStatus.rectTransform.anchoredPosition = new Vector2(0, safeRoot.rect.width < 780 ? (ribbonTheme ? -314 : -226) : (ribbonTheme ? -244 : -130));
            cheatStatus.rectTransform.sizeDelta = new Vector2(Mathf.Max(200, Mathf.Min(620, safeRoot.rect.width - 32)), 48);
            if (ribbonTheme) SyncStatusRibbon(cheatRibbon, cheatStatus);
        }

        private static void SyncStatusRibbon(RectTransform ribbon, Text label)
        {
            if (ribbon == null) return;
            ribbon.gameObject.SetActive(label.gameObject.activeSelf);
            ribbon.anchorMin = label.rectTransform.anchorMin;
            ribbon.anchorMax = label.rectTransform.anchorMax;
            ribbon.pivot = label.rectTransform.pivot;
            ribbon.anchoredPosition = label.rectTransform.anchoredPosition + Vector2.Scale(ribbon.pivot - Vector2.one * 0.5f, new Vector2(16, 12));
            ribbon.sizeDelta = label.rectTransform.sizeDelta + new Vector2(16, 12);
        }

        private static string AppendStatus(string current, string value)
        {
            return current.Length == 0 ? value : current + "   \xB7   " + value;
        }

        private void AnimateFeedback()
        {
            float dt = game.State == CubeDashGame.RunState.Paused ? 0 : Time.unscaledDeltaTime;
            scorePulse = Mathf.MoveTowards(scorePulse, 0, dt * 2.7f);
            distance.rectTransform.localScale = Vector3.one * (1 + Mathf.Sin(scorePulse * Mathf.PI) * 0.16f);
            distance.color = Color.Lerp(scoreColor, ribbonTheme ? new Color(0.95f, 0.02f, 0.11f) : new Color(1f, 0.75f, 0.42f), scorePulse);
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
