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
        public GameObject GameOverOverlay => gameOverOverlay;
        public Text GameOverScore => gameOverScore;
        private Rect lastSafeArea;
        private Vector2 lastScreen;

        public void Initialize(CubeDashGame controller)
        {
            game = controller;
            speed.color = game.Track.ColorMaterial(game.PlayerCubeColor).color;
            UpdateStats();
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
            overlay.SetActive(!running && !(ended && gameOverOverlay != null));
            if (gameOverOverlay != null) gameOverOverlay.SetActive(ended);
            if (gameOverScore != null) gameOverScore.text = game.Score.ToString();
            distance.gameObject.SetActive(!ended);
            best.gameObject.SetActive(!ended);
            speed.gameObject.SetActive(!ended);
            controls.SetActive(running);
            pauseButton.SetActive(running);
            secondaryButton.SetActive(state == CubeDashGame.RunState.Paused);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            switch (state)
            {
                case CubeDashGame.RunState.Ready:
                    heading.text = "CUBE DASH";
                    description.text = "Collect " + game.PlayerCubeColor.ToString().ToUpperInvariant() + " cubes. Avoid every other color.\nA / D, arrows, or swipe to change lanes.";
                    actionLabel.text = "TAP TO START";
                    break;
                case CubeDashGame.RunState.Paused:
                    heading.text = "TAKE A BREATH";
                    description.text = "Your run is paused.\nThe road will be here when you're ready.";
                    actionLabel.text = "KEEP DASHING";
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
            distance.text = game.Score + " POINTS";
            best.text = "BEST  " + game.Best;
            speed.text = "COLLECT " + game.PlayerCubeColor.ToString().ToUpperInvariant() + "  ·  " + Mathf.FloorToInt(game.Distance) + " m";
            speed.color = game.Track.ColorMaterial(game.PlayerCubeColor).color;
            Rect area = Screen.safeArea;
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            if (screen.x <= 0 || screen.y <= 0 || (area == lastSafeArea && screen == lastScreen)) return;
            lastSafeArea = area;
            lastScreen = screen;
            safeRoot.anchorMin = new Vector2(area.xMin / screen.x, area.yMin / screen.y);
            safeRoot.anchorMax = new Vector2(area.xMax / screen.x, area.yMax / screen.y);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            float width = Mathf.Min(600, safeRoot.rect.width - 32);
            card.sizeDelta = new Vector2(width, 320);
            heading.rectTransform.sizeDelta = new Vector2(width - 30, 86);
            description.rectTransform.sizeDelta = new Vector2(width - 36, 100);
        }
    }
}
