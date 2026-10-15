using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Landing-screen navigation and audio settings. Environment, camera and UI are authored in the scene.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameplayScene = "Level";
        [SerializeField] private Button startButton = null;
        [SerializeField] private GameObject shopPanel = null;
        [SerializeField] private GameObject optionsPanel = null;
        [SerializeField] private Button shopClose = null;
        [SerializeField] private Button optionsClose = null;
        [Header("Map selection")]
        [SerializeField] private GameObject mapPanel = null;
        [SerializeField] private Button firstMap = null;
        [Header("Exit confirmation")]
        [SerializeField] private GameObject quitPanel = null;
        [SerializeField] private Button quitCancel = null;
        [SerializeField] private Slider volume = null;
        [SerializeField] private Text volumeReadout = null;
        [SerializeField] private AudioSource music = null;
        [SerializeField] private Slider musicVolume = null;
        [SerializeField] private Text musicVolumeReadout = null;
        [SerializeField] private Toggle fullscreen = null;
        [SerializeField] private Renderer hero = null;
        private MaterialPropertyBlock appearance;
        private Button panelReturnButton;
        private GameObject currentPanel;
        private bool loading;
        public bool Loading => loading;
        public GameObject CurrentPanel => currentPanel;
        public string GameplayScene => gameplayScene;
        public GameObject ShopPanel => shopPanel;
        public GameObject OptionsPanel => optionsPanel;
        public GameObject MapPanel => mapPanel;
        public GameObject QuitPanel => quitPanel;
        public Renderer Hero => hero;
        public AudioSource Music => music;
        public const string VolumePreference = "CubeDash.MasterVolume";
        public const string MusicVolumePreference = "CubeDash.MusicVolume";

        private void Awake()
        {
            Time.timeScale = 1;
            RoadCurve.Clear();
            appearance = new MaterialPropertyBlock();
            if (shopPanel != null) shopPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            if (mapPanel != null) mapPanel.SetActive(false);
            if (quitPanel != null) quitPanel.SetActive(false);
            float savedVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreference, 1));
            AudioListener.volume = savedVolume;
            if (volume != null) volume.SetValueWithoutNotify(savedVolume);
            RefreshVolumeReadout(savedVolume);
            ApplyMusicVolume(Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePreference, 1)));
            if (fullscreen != null) fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);
        }

        private void Start()
        {
            GameAudioSettings.Apply();
            if (music != null && music.clip != null) music.Play();
            if (EventSystem.current != null && startButton != null)
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ClosePanels();
            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame) ClosePanels();
        }

        public void StartGame()
        {
            if (mapPanel != null) OpenPanel(mapPanel, firstMap, nameof(StartGame));
            else StartMap(gameplayScene); // Older, not-yet-upgraded menu scenes remain usable.
        }

        public void StartMap(string sceneName)
        {
            if (loading || string.IsNullOrWhiteSpace(sceneName)) return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning("This map is not yet available: " + sceneName);
                return;
            }
            GetComponentInChildren<MainMenuEntrance>()?.Complete();
            loading = true;
            if (startButton != null) startButton.interactable = false;
            if (mapPanel != null) mapPanel.GetComponent<CanvasGroup>().interactable = false;
            if (music != null) music.Stop();
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        public void OpenShop()
        {
            OpenPanel(shopPanel, shopClose, nameof(OpenShop));
        }

        public void OpenOptions()
        {
            OpenPanel(optionsPanel, optionsClose, nameof(OpenOptions));
        }

        private void OpenPanel(GameObject panel, Button close, string action)
        {
            if (panel == null || loading) return;
            GetComponentInChildren<MainMenuEntrance>()?.Complete();
            if (currentPanel == panel) { Focus(close); return; }
            // Pointer clicks, submit, and direct calls all return to the button for this panel.
            Button selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                ? EventSystem.current.currentSelectedGameObject.GetComponent<Button>() : null;
            panelReturnButton = OpensPanel(selected, action) ? selected : null;
            if (panelReturnButton == null)
                foreach (Button button in GetComponentsInChildren<Button>(true))
                    if (OpensPanel(button, action)) { panelReturnButton = button; break; }
            HidePanels(panel);
            currentPanel = panel;
            if (panel.TryGetComponent(out MenuPanelMotion motion)) motion.Open();
            else panel.SetActive(true);
            Focus(close);
        }

        private bool OpensPanel(Button button, string action)
        {
            if (button == null || !button.isActiveAndEnabled || !button.IsInteractable()) return false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                if (button.onClick.GetPersistentTarget(i) == this && button.onClick.GetPersistentMethodName(i) == action) return true;
            return false;
        }

        public void ClosePanels()
        {
            if (currentPanel == null) return;
            GameObject closing = currentPanel; currentPanel = null;
            ClosePanel(closing);
            Focus(panelReturnButton != null && panelReturnButton.isActiveAndEnabled && panelReturnButton.IsInteractable()
                ? panelReturnButton : startButton);
            panelReturnButton = null;
        }

        private void HidePanels(GameObject except = null)
        {
            if (shopPanel != except) ClosePanel(shopPanel);
            if (optionsPanel != except) ClosePanel(optionsPanel);
            if (mapPanel != except) ClosePanel(mapPanel);
            if (quitPanel != except) ClosePanel(quitPanel);
        }

        private static void ClosePanel(GameObject panel)
        {
            if (panel == null || !panel.activeSelf) return;
            if (panel.TryGetComponent(out MenuPanelMotion motion)) motion.Close();
            else panel.SetActive(false);
        }

        private static void Focus(Button button)
        {
            if (button != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        public void SetVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            if (volume != null) volume.SetValueWithoutNotify(AudioListener.volume);
            PlayerPrefs.SetFloat(VolumePreference, AudioListener.volume);
            RefreshVolumeReadout(AudioListener.volume);
        }

        private void RefreshVolumeReadout(float value)
        {
            if (volumeReadout != null) volumeReadout.text = Mathf.RoundToInt(value * 100) + "%";
        }

        public void SetMusicVolume(float value)
        {
            float clamped = Mathf.Clamp01(value);
            ApplyMusicVolume(clamped);
            GameAudioSettings.SetMusicVolume(clamped);
        }

        private void ApplyMusicVolume(float value)
        {
            if (music != null) music.volume = value;
            if (musicVolume != null) musicVolume.SetValueWithoutNotify(value);
            if (musicVolumeReadout != null) musicVolumeReadout.text = Mathf.RoundToInt(value * 100) + "%";
        }

        public void SetFullscreen(bool value) => Screen.fullScreen = value;
        public void PreviewRed() => Preview(hero != null ? hero.sharedMaterial.GetColor("_BaseColor") : new Color(0.95f, 0.012f, 0.025f));
        public void PreviewBlue() => Preview(new Color(0.08f, 0.35f, 0.95f));
        public void PreviewGreen() => Preview(new Color(0.12f, 0.8f, 0.3f));
        private void Preview(Color color)
        {
            if (hero == null) return;
            hero.GetPropertyBlock(appearance);
            appearance.SetColor("_BaseColor", color);
            hero.SetPropertyBlock(appearance);
        }

        public void Quit()
        {
            OpenPanel(quitPanel, quitCancel, nameof(Quit));
        }

        public void ConfirmQuit()
        {
            // A stale submit/callback outside the open confirmation must never exit the game.
            if (quitPanel == null || currentPanel != quitPanel || !quitPanel.activeInHierarchy || loading) return;
            loading = true;
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private void OnApplicationQuit() => PlayerPrefs.Save();
    }
}
