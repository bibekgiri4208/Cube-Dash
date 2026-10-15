using System.Collections;
using System.IO;
using System.Reflection;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class SceneLoaderTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void SavedLoadingScreenUsesUserArtworkAndThemedNonInteractiveProgress()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingScreenAuthoring.PrefabPath);
            Assert.That(root, Is.Not.Null);
            var loader = root.GetComponent<SceneLoader>();
            Assert.That(AssetDatabase.GetAssetPath(loader.Artwork.sprite), Is.EqualTo(LoadingScreenAuthoring.ImagePath));
            Assert.That(loader.Artwork.GetComponent<AspectRatioFitter>().aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
            Assert.That(root.GetComponent<Canvas>().sortingOrder, Is.GreaterThan(30000));
            Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            Assert.That(root.transform.Find("Opaque Input Blocker").GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(loader.ProgressBar.interactable, Is.False);
            Assert.That(loader.ProgressBar.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            Assert.That(loader.ProgressBar.handleRect, Is.Null);
            Assert.That(loader.ProgressBar.fillRect.GetComponent<MenuPanelGraphic>().color, Is.EqualTo(LevelMenuTheme.Accent));
            var progress = root.transform.Find("Safe Area/Loading Progress");
            Assert.That(progress.GetComponent<Graphic>(), Is.Null, "The loading bar must not be inside a visible menu card.");
            Assert.That(progress.Find("Destination"), Is.Null);
            Assert.That(progress.Find("Loading Text").GetComponent<Text>().text, Does.StartWith("LOADING"));
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                if (graphic.transform.name != "Opaque Input Blocker")
                    Assert.That(graphic.raycastTarget, Is.False, graphic.name);
        }

        [UnityTest]
        public IEnumerator EveryMapAndReturnUsesOneOverlayRealProgressAndAFrozenCountdown()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            foreach (string map in PauseSoundAuthoring.Maps)
            {
                var menu = Object.FindAnyObjectByType<MainMenuController>();
                Assert.That(menu, Is.Not.Null);
                menu.StartMap(map);
                Assert.That(menu.Loading, Is.True);
                Assert.That(SceneLoader.IsLoading, Is.True);
                var loader = SceneLoader.Current;
                Assert.That(loader.Progress, Is.Zero);
                Assert.That(EventSystem.current.sendNavigationEvents, Is.False);
                menu.StartMap(map);
                Assert.That(SceneLoader.Load("Main Menu"), Is.False, "Duplicate or competing requests must not queue another scene.");
                Assert.That(Object.FindObjectsByType<SceneLoader>().Length, Is.EqualTo(1));
                yield return WaitForLoad(map);
                Assert.That(loader == null, Is.True, "The persistent overlay is destroyed after completion.");
                Assert.That(EventSystem.current.sendNavigationEvents, Is.True);
                var game = Object.FindAnyObjectByType<CubeDashGame>();
                Assert.That(game, Is.Not.Null);
                Assert.That(game.CountdownActive, Is.True);
                Assert.That(game.CountdownRemaining, Is.GreaterThan(2.8f), "Loading must not consume the 3/2/1 countdown.");
                Assert.That(game.Distance, Is.Zero);
                Assert.That(Object.FindAnyObjectByType<MapMusic>().Source.isPlaying, Is.True);
                game.TogglePause();
                var hud = Object.FindAnyObjectByType<RunnerHud>();
                hud.QuitGame(); hud.QuitGame();
                Assert.That(SceneLoader.IsLoading, Is.True);
                yield return WaitForLoad("Main Menu");
                Assert.That(Object.FindAnyObjectByType<CubeDashGame>(), Is.Null);
                Assert.That(Object.FindAnyObjectByType<MainMenuController>().Music.isPlaying, Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("START"));
                Assert.That(EventSystem.current.sendNavigationEvents, Is.True);
            }
        }

        private static IEnumerator WaitForLoad(string destination)
        {
            float deadline = Time.realtimeSinceStartup + 35;
            float previous = 0;
            bool completed = false;
            while (SceneLoader.IsLoading && Time.realtimeSinceStartup < deadline)
            {
                var loader = SceneLoader.Current;
                Assert.That(loader.Progress, Is.GreaterThanOrEqualTo(previous));
                Assert.That(loader.Progress, Is.InRange(0, 1));
                previous = loader.Progress;
                completed |= previous == 1;
                if (SceneManager.GetActiveScene().name == destination)
                {
                    var game = Object.FindAnyObjectByType<CubeDashGame>();
                    if (game != null)
                    {
                        Assert.That(game.Distance, Is.Zero);
                        Assert.That(game.CountdownRemaining, Is.EqualTo(3).Within(0.001f));
                    }
                }
                yield return null;
            }
            Assert.That(SceneLoader.IsLoading, Is.False, "Scene transition timed out.");
            Assert.That(completed, Is.True, "The progress indicator must reach 100% after activation.");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(destination));
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnavailableSceneDoesNotShowAnOverlayOrLatchTheMenu()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Assert.That(SceneLoader.Load(null), Is.False);
            LogAssert.Expect(LogType.Warning, "This scene is not available: Missing Loading Test Map");
            Assert.That(SceneLoader.Load("Missing Loading Test Map"), Is.False);
            Assert.That(SceneLoader.IsLoading, Is.False);
            Assert.That(Object.FindAnyObjectByType<MainMenuController>().Loading, Is.False);
            Assert.That(EventSystem.current.sendNavigationEvents, Is.True);
        }

        [UnityTest]
        public IEnumerator LoadingArtworkAndProgressRenderInLandscapeAndPortrait()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) Assert.Ignore("GPU rendering required.");
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainMenuSceneBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var loader = Object.Instantiate(Resources.Load<SceneLoader>(SceneLoader.ResourceName));
            try
            {
                typeof(SceneLoader).GetMethod("SetProgress", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(loader, new object[] { 0.63f });
                Capture(loader, 1672, 940, "landscape");
                Capture(loader, 600, 1000, "portrait");
            }
            finally { Object.DestroyImmediate(loader.gameObject); }
        }

        private static void Capture(SceneLoader loader, int width, int height, string suffix)
        {
            string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_LOADING_PREVIEW");
            if (string.IsNullOrEmpty(prefix)) return;
            Camera camera = Camera.main; var canvas = loader.GetComponent<Canvas>();
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            bool oldAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                camera.targetTexture = target; ShaderUtil.allowAsyncCompilation = false;
                Canvas.ForceUpdateCanvases();
                typeof(SceneLoader).GetField("lastSize", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(loader, Vector2.zero);
                typeof(SceneLoader).GetMethod("Layout", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(loader, null);
                Canvas.ForceUpdateCanvases();
                var card = loader.transform.Find("Safe Area/Loading Progress").GetComponent<RectTransform>();
                Assert.That(loader.ProgressBar.GetComponent<RectTransform>().rect.width, Is.EqualTo(card.rect.width).Within(0.01f));
                for (int i = 0; i < 4; i++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(prefix + "-" + suffix + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive; ShaderUtil.allowAsyncCompilation = oldAsync;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
