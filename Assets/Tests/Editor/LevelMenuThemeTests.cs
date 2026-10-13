using System.Collections;
using System.Linq;
using System.Reflection;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class LevelMenuThemeTests
    {
        private static readonly MethodInfo Tick = typeof(CubeDashGame).GetMethod("TickStartCountdown", BindingFlags.NonPublic | BindingFlags.Instance);

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void SavedLevelUsesMainMenuGraphicsFontPaletteAndCountdownInsteadOfAStartScreen()
        {
            var scene = EditorSceneManager.OpenPreviewScene(LevelMenuTheme.ScenePath);
            try
            {
                var hud = Find<RunnerHud>(scene); var game = Find<CubeDashGame>(scene);
                Transform safe = hud.transform.Find("Safe Area");
                Assert.That(safe.Find("Speed").GetComponent<Text>().text, Is.EqualTo("0 m"));
                Assert.That(hud.RibbonTheme && game.UsesStartCountdown, Is.True);
                Assert.That(hud.CountdownOverlay, Is.Not.Null); Assert.That(hud.CountdownNumber.text, Is.EqualTo("3"));
                Assert.That(safe.Find("Menu Overlay").gameObject.activeSelf, Is.False);
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MainMenu/Fonts/Anton-Regular.ttf");
                Assert.That(hud.GetComponentsInChildren<Text>(true).All(label => label.font == font), Is.True);
                Assert.That(hud.GetComponentsInChildren<Text>(true).All(label => label is MenuLabelText), Is.True);
                foreach (string name in new[] { "Score Panel", "Color Objective Panel", "Pause", "Menu Overlay/Menu Card/Ribbon header", "Game Over Overlay/Abstract Composition/Ribbon header" })
                    Assert.That(safe.Find(name).GetComponent<MenuRibbonGraphic>(), Is.Not.Null, name);
                foreach (string name in new[] { "Menu Overlay/Menu Card", "Game Over Overlay/Abstract Composition" })
                {
                    var panel = safe.Find(name).GetComponent<MenuPanelGraphic>();
                    Assert.That(panel.color, Is.EqualTo(LevelMenuTheme.Paper)); Assert.That(panel.BorderColor, Is.EqualTo(LevelMenuTheme.Ink));
                    Assert.That(panel.CornerCut, Is.EqualTo(22));
                }
                foreach (string name in new[] { "Menu Overlay/Menu Card/Restart", "Game Over Overlay/Abstract Composition/Retry" })
                {
                    var button = safe.Find(name).GetComponent<Button>();
                    Assert.That(button.targetGraphic, Is.TypeOf<MenuRibbonGraphic>());
                    Assert.That(button.onClick.GetPersistentTarget(0), Is.SameAs(game));
                    Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(CubeDashGame.RequestRun)));
                }
                Assert.That(safe.Find("Pause/Pause Icon").GetComponentsInChildren<Image>().All(bar => bar.color == LevelMenuTheme.Cream), Is.True);
                Assert.That(safe.Find("Power-Up Ribbon").GetComponent<MenuRibbonGraphic>(), Is.Not.Null);
                Assert.That(safe.Find("Cheat Ribbon").GetComponent<MenuRibbonGraphic>(), Is.Not.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void ReapplyingThemePreservesManualUiEditsAndOtherScenesKeepTheirOriginalReadyFlow()
        {
            var level = EditorSceneManager.OpenPreviewScene(LevelMenuTheme.ScenePath);
            var country = EditorSceneManager.OpenPreviewScene(CountrySideSceneBuilder.ScenePath);
            try
            {
                var hud = Find<RunnerHud>(level); int count = hud.GetComponentsInChildren<Transform>(true).Length;
                var ribbon = hud.transform.Find("Safe Area/Score Panel").GetComponent<MenuRibbonGraphic>(); ribbon.Primary = true;
                var number = hud.CountdownNumber; number.fontSize = 130;
                LevelMenuTheme.ApplyToScene(level); LevelMenuTheme.ApplyToScene(level);
                CubeDashHudLayout.Apply(hud); CubeDashDesktopControls.ApplyToScene(hud);
                Assert.That(hud.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
                Assert.That(ribbon.Primary, Is.True); Assert.That(number.fontSize, Is.EqualTo(130));
                Assert.That(Find<RunnerHud>(country).RibbonTheme, Is.False);
                Assert.That(Find<CubeDashGame>(country).UsesStartCountdown, Is.False);
                Assert.Throws<System.InvalidOperationException>(() => LevelMenuTheme.ApplyToScene(country));
            }
            finally { EditorSceneManager.ClosePreviewScene(country); EditorSceneManager.ClosePreviewScene(level); }
        }

        [UnityTest]
        public IEnumerator EntryAutomaticallyCountsDownWithoutMovingTheTrackOrAcceptingStartConfirmation()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(LevelMenuTheme.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); var hud = Object.FindAnyObjectByType<RunnerHud>();
            Assert.That(game.CountdownActive, Is.True); Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Ready));
            Assert.That(game.Distance, Is.Zero); Assert.That(game.Track.Travelled, Is.Zero);
            Assert.That(hud.CountdownNumber.text, Is.EqualTo("3"));
            Assert.That(hud.transform.Find("Safe Area/Menu Overlay").gameObject.activeSelf, Is.False);
            hud.ConfirmSelection(); Assert.That(game.CountdownActive, Is.True);
            Vector3 position = game.Player.position; Bounds bounds = game.Player.GetComponent<BoxCollider>().bounds;
            float deadline = Time.realtimeSinceStartup + 20;
            while (game.CountdownRemaining > 2 && Time.realtimeSinceStartup < deadline) yield return null;
            hud.UpdateStats();
            Assert.That(game.CountdownRemaining, Is.LessThanOrEqualTo(2));
            Assert.That(hud.CountdownNumber.text, Is.EqualTo("2"));
            Assert.That(game.Distance, Is.Zero); Assert.That(game.Score, Is.Zero);
            Assert.That(game.Player.position, Is.EqualTo(position)); Assert.That(game.Player.GetComponent<BoxCollider>().bounds, Is.EqualTo(bounds));
            while (game.CountdownRemaining > 1 && Time.realtimeSinceStartup < deadline) yield return null;
            hud.UpdateStats();
            Assert.That(hud.CountdownNumber.text, Is.EqualTo("1"));
            while (game.CountdownActive && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running)); Assert.That(game.CountdownActive, Is.False);
            Assert.That(hud.CountdownOverlay.activeSelf, Is.False); Assert.That(game.Distance, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator CountdownPausesResumesAndRetryCallbacksStartAFreshFrozenRun()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(LevelMenuTheme.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); var hud = Object.FindAnyObjectByType<RunnerHud>();
            game.TogglePause(); float remaining = game.CountdownRemaining;
            for (int i = 0; i < 15; i++) yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Paused)); Assert.That(game.CountdownRemaining, Is.EqualTo(remaining));
            Assert.That(hud.CountdownOverlay.activeSelf, Is.False);
            hud.PrimaryAction(); Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Ready)); Assert.That(game.CountdownRemaining, Is.EqualTo(remaining));
            game.enabled = false; game.RequestRun();
            Step(game, 1.01f); hud.UpdateStats(); Assert.That(hud.CountdownNumber.text, Is.EqualTo("2"));
            Step(game, 1); hud.UpdateStats(); Assert.That(hud.CountdownNumber.text, Is.EqualTo("1"));
            Step(game, 1); Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            game.TogglePause();
            hud.transform.Find("Safe Area/Menu Overlay/Menu Card/Restart").GetComponent<Button>().onClick.Invoke();
            Assert.That(game.CountdownRemaining, Is.EqualTo(3)); Assert.That(game.Distance, Is.Zero);
            Step(game, 3); Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            typeof(CubeDashGame).GetMethod("Crash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            Assert.That(hud.GameOverOverlay.activeSelf, Is.True);
            hud.GameOverOverlay.transform.Find("Abstract Composition/Retry").GetComponent<Button>().onClick.Invoke();
            Assert.That(hud.GameOverOverlay.activeSelf, Is.False); Assert.That(game.CountdownRemaining, Is.EqualTo(3));
            Assert.That(game.Score, Is.Zero); Assert.That(game.Track.Travelled, Is.Zero);
        }

        [UnityTest]
        public IEnumerator GamepadCannotSkipCountdownAndPauseFocusUsesThemedButtons()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(LevelMenuTheme.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); var hud = Object.FindAnyObjectByType<RunnerHud>();
            var background = InputSystem.settings.backgroundBehavior; var focus = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var pad = InputSystem.AddDevice<Gamepad>("LevelRibbonGamepad");
            try
            {
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South)); yield return null; yield return null;
                Assert.That(game.CountdownActive, Is.True); Assert.That(game.Distance, Is.Zero);
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start)); yield return null; yield return null;
                Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Paused));
                Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Primary Action"));
                Assert.That(EventSystem.current.currentSelectedGameObject.GetComponent<MenuRibbonGraphic>().Primary, Is.True);
                Assert.That(EventSystem.current.currentSelectedGameObject.GetComponent<Outline>().enabled, Is.True);
                InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.East)); yield return null; yield return null;
                Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Ready)); Assert.That(game.CountdownActive, Is.True);
            }
            finally
            {
                InputSystem.RemoveDevice(pad); InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = focus;
            }
        }

        [UnityTest]
        public IEnumerator ThemedUiRendersCountdownHudPauseGameOverAndPortraitWithoutLosingStats()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(LevelMenuTheme.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); var hud = Object.FindAnyObjectByType<RunnerHud>();
            game.enabled = false; game.RequestRun(); hud.UpdateStats(); AssertGlyphs(hud.CountdownNumber); Capture(hud, "countdown-3");
            Step(game, 1.01f); hud.UpdateStats(); AssertGlyphs(hud.CountdownNumber); Capture(hud, "countdown-2");
            Step(game, 1); hud.UpdateStats(); AssertGlyphs(hud.CountdownNumber); Capture(hud, "countdown-1");
            Step(game, 1); hud.UpdateStats(); Capture(hud, "running");
            typeof(CubeDashGame).GetMethod("ApplyPowerUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { PowerUpType.Magnet });
            game.ToggleInvincibility(); hud.UpdateStats(); Capture(hud, "powerups-cheat");
            Assert.That(hud.transform.Find("Safe Area/Power-Up Ribbon").gameObject.activeSelf, Is.False);
            Assert.That(hud.PowerUpBars.Stack.gameObject.activeSelf, Is.True);
            Assert.That(hud.transform.Find("Safe Area/Cheat Ribbon").gameObject.activeSelf, Is.True);
            game.ToggleInvincibility();
            game.TogglePause();
            for (int i = 0; i < 40; i++) { yield return null; hud.UpdateStats(); }
            typeof(RunnerHud).GetField("entrance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, 1f); hud.UpdateStats();
            Capture(hud, "pause"); Capture(hud, "pause-portrait", 600, 1000);
            var labels = hud.transform.Find("Safe Area/Menu Overlay/Menu Card").GetComponentsInChildren<Text>();
            foreach (Text label in labels.Where(label => label.text.Length > 0))
            {
                label.cachedTextGenerator.Populate(label.text, label.GetGenerationSettings(label.rectTransform.rect.size));
                Assert.That(label.cachedTextGenerator.vertexCount, Is.GreaterThan(0), label.name + " must generate visible glyphs.");
            }
            game.TogglePause(); typeof(CubeDashGame).GetMethod("Crash", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
            for (int i = 0; i < 40; i++) { yield return null; hud.UpdateStats(); }
            typeof(RunnerHud).GetField("entrance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, 1f); hud.UpdateStats();
            Capture(hud, "game-over"); Capture(hud, "game-over-portrait", 600, 1000);
            Assert.That(hud.GameOverScore.text, Is.EqualTo(game.Score.ToString()));
            AssertGlyphs(hud.GameOverScore);
        }

        [UnityTest]
        public IEnumerator TopCounterShowsOnlyDistanceAcrossStageBoundariesPauseAndRestart()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(LevelMenuTheme.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); var hud = Object.FindAnyObjectByType<RunnerHud>();
            game.enabled = false; game.StartRun();
            Text counter = hud.transform.Find("Safe Area/Speed").GetComponent<Text>();
            foreach (float metres in new[] { 0f, 179.9f, 180f, 480f, 900f, 1500f })
            {
                typeof(CubeDashGame).GetProperty(nameof(CubeDashGame.Distance)).SetValue(game, metres);
                hud.UpdateStats();
                Assert.That(counter.text, Is.EqualTo(Mathf.FloorToInt(metres) + " m"));
                Assert.That(counter.text, Does.Not.Contain("STAGE"));
            }
            Assert.That(game.Stage, Is.EqualTo(5), "Only the label changes; distance-based gameplay progression is preserved.");
            game.TogglePause(); hud.UpdateStats(); Assert.That(counter.text, Is.EqualTo("1500 m"));
            game.RequestRun(); hud.UpdateStats(); Assert.That(counter.text, Is.EqualTo("0 m"));
            Assert.That(game.CountdownActive, Is.True);
        }

        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static void Step(CubeDashGame game, float dt) => Tick.Invoke(game, new object[] { dt });

        private static void AssertGlyphs(Text label)
        {
            using (var mesh = new VertexHelper())
            {
                typeof(MenuLabelText).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(label, new object[] { mesh });
                Assert.That(mesh.currentVertCount, Is.GreaterThanOrEqualTo(label.text.Length * 4), label.name + " must render every numeral.");
            }
        }

        private static void Capture(RunnerHud hud, string name, int width = 1672, int height = 940)
        {
            string prefix = System.Environment.GetEnvironmentVariable("CUBE_DASH_LEVEL_THEME_PREVIEW");
            if (string.IsNullOrEmpty(prefix)) return;
            Camera camera = Camera.main; var canvas = hud.GetComponent<Canvas>();
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float oldPlane = canvas.planeDistance;
            bool oldAsync = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                camera.targetTexture = target; ShaderUtil.allowAsyncCompilation = false; Canvas.ForceUpdateCanvases();
                typeof(RunnerHud).GetField("lastScreen", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hud, Vector2.zero);
                hud.UpdateStats(); Canvas.ForceUpdateCanvases();
                for (int i = 0; i < 4; i++) camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(prefix + "-" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldPlane;
                ShaderUtil.allowAsyncCompilation = oldAsync; Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                Canvas.ForceUpdateCanvases();
                typeof(RunnerHud).GetField("lastScreen", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(hud, Vector2.zero); hud.UpdateStats();
            }
        }
    }
}
