using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CubeDash
{
    /// <summary>One persistent, input-blocking screen for asynchronous map/menu transitions.</summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        public const string ResourceName = "Loading Screen";
        [SerializeField] private Image artwork = null;
        [SerializeField] private RectTransform safeArea = null;
        [SerializeField] private RectTransform card = null;
        [SerializeField] private Slider progressBar = null;
        [SerializeField] private Text loadingText = null;
        [SerializeField] private Text destinationText = null;
        [SerializeField] private Text percentage = null;
        [SerializeField] private CanvasGroup visibility = null;
        [SerializeField, Min(0)] private float minimumDisplaySeconds = 0.7f;
        [SerializeField, Min(0)] private float fadeOutSeconds = 0.18f;
        private static SceneLoader current;
        private AsyncOperation operation;
        private EventSystem blockedEvents;
        private bool previousNavigation;
        private Rect lastSafeArea;
        private Vector2 lastSize;
        private float age;
        private int dots = -1;

        public static bool IsLoading => current != null;
        public static SceneLoader Current => current;
        public float Progress => progressBar.value;
        public Slider ProgressBar => progressBar;
        public Image Artwork => artwork;

        public static bool Load(string sceneName)
        {
            if (IsLoading || string.IsNullOrWhiteSpace(sceneName)) return false;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning("This scene is not available: " + sceneName);
                return false;
            }
            var prefab = Resources.Load<SceneLoader>(ResourceName);
            if (prefab == null)
            {
                Debug.LogError("The authored Loading Screen prefab is missing from Resources.");
                return false;
            }
            current = Instantiate(prefab);
            current.name = ResourceName;
            DontDestroyOnLoad(current.gameObject);
            current.Begin(sceneName);
            return true;
        }

        private void Begin(string sceneName)
        {
            visibility.alpha = 1;
            destinationText.text = sceneName == "Main Menu" ? "RETURNING TO MAIN MENU" : "ENTERING " + (sceneName == "Level" ? "DYNAMIC MAP" : sceneName.ToUpperInvariant());
            SetProgress(0);
            BlockNavigation();
            SceneManager.sceneLoaded += SceneLoaded;
            StartCoroutine(LoadScene(sceneName));
        }

        private IEnumerator LoadScene(string sceneName)
        {
            // Give the overlay a rendered frame before native scene deserialization can stall.
            yield return null;
            yield return null;
            try { operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); }
            catch (System.Exception exception) { Debug.LogException(exception); }
            if (operation == null) { Finish(); yield break; }
            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f || age < minimumDisplaySeconds || Progress < 0.95f)
            {
                // Unity reserves the final 10% for activation. Never report 100% before it finishes.
                float target = Mathf.Clamp01(operation.progress / 0.9f) * 0.95f;
                SetProgress(Mathf.MoveTowards(Progress, target, Time.unscaledDeltaTime * 2));
                yield return null;
            }
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;
            SetProgress(1);
            yield return null;
            yield return null;
            float elapsed = 0;
            while (elapsed < fadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                visibility.alpha = 1 - Mathf.Clamp01(elapsed / fadeOutSeconds);
                yield return null;
            }
            Finish();
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            int count = (int)(age / 0.35f) % 3 + 1;
            if (count != dots) { dots = count; loadingText.text = "LOADING" + new string('.', count); }
            BlockNavigation();
            Layout();
        }

        private void Layout()
        {
            Rect area = Screen.safeArea;
            Vector2 size = new Vector2(Screen.width, Screen.height);
            if (size.x <= 0 || size.y <= 0 || (area == lastSafeArea && size == lastSize)) return;
            lastSafeArea = area; lastSize = size;
            safeArea.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
            safeArea.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            card.sizeDelta = new Vector2(Mathf.Min(980, Mathf.Max(240, safeArea.rect.width - 64)), 180);
        }

        private void SetProgress(float value)
        {
            progressBar.SetValueWithoutNotify(Mathf.Clamp01(value));
            percentage.text = Mathf.FloorToInt(progressBar.value * 100) + "%";
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode) => BlockNavigation();
        private void BlockNavigation()
        {
            EventSystem events = EventSystem.current;
            if (events == blockedEvents) return;
            RestoreNavigation();
            blockedEvents = events;
            if (blockedEvents == null) return;
            previousNavigation = blockedEvents.sendNavigationEvents;
            blockedEvents.sendNavigationEvents = false;
        }

        private void RestoreNavigation()
        {
            if (blockedEvents != null) blockedEvents.sendNavigationEvents = previousNavigation;
            blockedEvents = null;
        }

        private void Finish()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            RestoreNavigation();
            visibility.blocksRaycasts = false;
            visibility.alpha = 0;
            if (current == this) current = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            RestoreNavigation();
            // An externally interrupted transition must not leave Unity's async queue blocked.
            if (operation != null && !operation.isDone) operation.allowSceneActivation = true;
            if (current == this) current = null;
        }
    }
}
