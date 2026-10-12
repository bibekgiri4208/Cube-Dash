using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Landing-screen navigation only. Environment, camera and UI are authored in the scene.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameplayScene = "Level";
        [SerializeField] private Button startButton = null;
        [SerializeField] private GameObject shopPanel = null;
        [SerializeField] private GameObject optionsPanel = null;
        [SerializeField] private Button shopClose = null;
        [SerializeField] private Button optionsClose = null;
        [SerializeField] private Slider volume = null;
        [SerializeField] private Toggle fullscreen = null;
        [SerializeField] private Renderer hero = null;
        private MaterialPropertyBlock appearance;
        private bool loading;
        public bool Loading => loading;
        public string GameplayScene => gameplayScene;
        public GameObject ShopPanel => shopPanel;
        public GameObject OptionsPanel => optionsPanel;
        public Renderer Hero => hero;
        public const string VolumePreference = "CubeDash.MasterVolume";

        private void Awake()
        {
            Time.timeScale = 1;
            RoadCurve.Clear();
            appearance = new MaterialPropertyBlock();
            if (shopPanel != null) shopPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            float savedVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreference, 1));
            AudioListener.volume = savedVolume;
            if (volume != null) volume.SetValueWithoutNotify(savedVolume);
            if (fullscreen != null) fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);
        }

        private void Start()
        {
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
            if (loading) return;
            loading = true;
            if (startButton != null) startButton.interactable = false;
            SceneManager.LoadSceneAsync(gameplayScene);
        }

        public void OpenShop()
        {
            ClosePanels();
            if (shopPanel != null) shopPanel.SetActive(true);
            Focus(shopClose);
        }

        public void OpenOptions()
        {
            ClosePanels();
            if (optionsPanel != null) optionsPanel.SetActive(true);
            Focus(optionsClose);
        }

        public void ClosePanels()
        {
            if (shopPanel != null) shopPanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            Focus(startButton);
        }

        private static void Focus(Button button)
        {
            if (button != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        public void SetVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumePreference, AudioListener.volume);
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
