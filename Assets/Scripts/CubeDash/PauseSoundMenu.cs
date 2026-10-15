using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>Authored pause submenu. Never resumes or restarts a run while adjusting audio.</summary>
    public sealed class PauseSoundMenu : MonoBehaviour
    {
        [SerializeField] private GameObject panel = null;
        [SerializeField] private GameObject pauseCard = null;
        [SerializeField] private Button soundButton = null;
        [SerializeField] private Button backButton = null;
        [SerializeField] private Slider musicSlider = null;
        [SerializeField] private Slider sfxSlider = null;
        [SerializeField] private Text musicReadout = null;
        [SerializeField] private Text sfxReadout = null;
        private CubeDashGame game;
        public bool IsOpen => panel != null && panel.activeSelf;
        public Button SoundButton => soundButton;
        public Slider MusicSlider => musicSlider;
        public Slider SfxSlider => sfxSlider;
        public GameObject Panel => panel;

        public void Initialize(CubeDashGame controller) { game = controller; Hide(); }

        public void Open()
        {
            if (game == null || game.State != CubeDashGame.RunState.Paused) return;
            Refresh();
            pauseCard.SetActive(false);
            panel.SetActive(true);
            Focus(musicSlider.gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            Hide();
            PlayerPrefs.Save();
            if (game != null && game.State == CubeDashGame.RunState.Paused) Focus(soundButton.gameObject);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
            if (pauseCard != null) pauseCard.SetActive(true);
        }

        public void SetMusicVolume(float value) { GameAudioSettings.SetMusicVolume(value); Refresh(); }
        public void SetSfxVolume(float value) { GameAudioSettings.SetSfxVolume(value); Refresh(); }

        private void Refresh()
        {
            musicSlider.SetValueWithoutNotify(GameAudioSettings.MusicVolume);
            sfxSlider.SetValueWithoutNotify(GameAudioSettings.SfxVolume);
            musicReadout.text = Mathf.RoundToInt(GameAudioSettings.MusicVolume * 100) + "%";
            sfxReadout.text = Mathf.RoundToInt(GameAudioSettings.SfxVolume * 100) + "%";
        }

        public void Confirm()
        {
            // Confirm on a slider does not fall through to Resume Run.
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == backButton.gameObject) Close();
        }

        public void CycleSelection()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            Selectable control = selected != null ? selected.GetComponent<Selectable>() : null;
            Selectable next = control != null ? control.navigation.selectOnDown : null;
            Focus(next != null ? next.gameObject : musicSlider.gameObject);
        }

        public void FocusMusic() => Focus(musicSlider.gameObject);
        public void Layout(float width)
        {
            ((RectTransform)panel.transform).sizeDelta = new Vector2(width, 440);
            panel.transform.Find("Ribbon header").GetComponent<RectTransform>().sizeDelta = new Vector2(width - 40, 98);
            panel.transform.Find("Heading").GetComponent<RectTransform>().sizeDelta = new Vector2(width - 56, 86);
            foreach (Slider slider in new[] { musicSlider, sfxSlider })
                ((RectTransform)slider.transform).sizeDelta = new Vector2(width - 100, 32);
            foreach (string name in new[] { "Music", "SFX" })
            {
                var label = panel.transform.Find(name + " label").GetComponent<RectTransform>();
                label.sizeDelta = new Vector2(width - 180, 40);
                var value = panel.transform.Find(name + " value").GetComponent<RectTransform>();
                value.anchoredPosition = new Vector2((width - 120) * 0.5f, value.anchoredPosition.y);
            }
            ((RectTransform)backButton.transform).sizeDelta = new Vector2(width - 64, 68);
            backButton.GetComponentInChildren<Text>(true).rectTransform.sizeDelta = new Vector2(width - 88, 64);
        }

        private static void Focus(GameObject target)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target);
        }
    }
}
