using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class InvincibilityTests
    {
        private Keyboard keyboard;
        private static void Clear(CubeDashGame game)
        {
            foreach (TrackSegment segment in game.Track.Segments)
            {
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
        }
        private static void Hazard(CubeDashGame game)
        {
            RunnerCube cube = game.Track.Segments[1].Cubes[0];
            cube.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue));
            cube.transform.position = new Vector3(game.Player.position.x, 0.575f, 0.5f);
            cube.gameObject.SetActive(true);
        }
        private static void Apply(CubeDashGame game, PowerUpType type)
            => typeof(CubeDashGame).GetMethod("ApplyPowerUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { type });

        [Test]
        public void SavedIndicatorIsAssignedHiddenByDefaultAndDoesNotBlockPointerInput()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                RunnerHud hud = null;
                foreach (GameObject root in scene.GetRootGameObjects()) if ((hud = root.GetComponentInChildren<RunnerHud>(true)) != null) break;
                Assert.That(hud, Is.Not.Null);
                Assert.That(hud.CheatStatus, Is.Not.Null);
                Assert.That(hud.CheatStatus.text, Does.Contain("INVINCIBILITY ACTIVE"));
                Assert.That(hud.CheatStatus.gameObject.activeSelf, Is.False);
                Assert.That(hud.CheatStatus.raycastTarget, Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator IKeyTogglesOncePerPressAndCheatSurvivesPauseAndRetryWithoutExtraObjects()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            RunnerHud hud = Object.FindAnyObjectByType<RunnerHud>();
            game.enabled = false;
            var background = InputSystem.settings.backgroundBehavior;
            var focus = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("InvincibilityTestKeyboard"); keyboard.MakeCurrent();
            MethodInfo read = typeof(CubeDashGame).GetMethod("ReadInput", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                int objects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                Assert.That(game.InvincibilityEnabled, Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.I)); InputSystem.Update(); read.Invoke(game, null);
                Assert.That(game.InvincibilityEnabled, Is.True);
                Assert.That(hud.CheatStatus.gameObject.activeInHierarchy, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.I)); InputSystem.Update(); read.Invoke(game, null);
                Assert.That(game.InvincibilityEnabled, Is.True, "Holding I must not repeatedly toggle.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                game.StartRun(); Clear(game);
                Assert.That(game.InvincibilityEnabled, Is.True);
                game.TogglePause(); yield return null;
                Assert.That(hud.CheatStatus.gameObject.activeInHierarchy, Is.True);
                game.TogglePause(); game.StartRun(); Clear(game);
                Assert.That(game.InvincibilityEnabled, Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.I)); InputSystem.Update(); read.Invoke(game, null);
                Assert.That(game.InvincibilityEnabled, Is.False);
                Assert.That(hud.CheatStatus.gameObject.activeSelf, Is.False);
                Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(objects));
            }
            finally
            {
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = focus;
            }
        }

        [UnityTest]
        public IEnumerator CheatPreventsRealHazardCrashesPreservesShieldsAndCoinsAndNormalRulesReturnWhenOff()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); Clear(game);
            Apply(game, PowerUpType.Shield); game.ToggleInvincibility();
            BoxCollider collider = game.Player.GetComponent<BoxCollider>(); Vector3 size = collider.size;
            Hazard(game);
            RunnerCube coin = game.Track.Segments[1].Cubes[1];
            coin.Configure(CubeColor.Red, game.Track.ColorMaterial(CubeColor.Red));
            coin.transform.position = new Vector3(game.Player.position.x, 0.575f, 0.5f); coin.gameObject.SetActive(true);
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            Assert.That(game.Shields, Is.EqualTo(1)); Assert.That(game.Score, Is.EqualTo(1));
            Assert.That(game.Track.LastTruckSmashes, Is.Zero, "Cheat is not a Truck smash.");
            Assert.That(collider.enabled, Is.True); Assert.That(collider.size, Is.EqualTo(size));
            Apply(game, PowerUpType.Magnet); Apply(game, PowerUpType.DoublePoints);
            Assert.That(game.MagnetActive && game.DoublePointsActive, Is.True);
            game.ToggleInvincibility(); Clear(game); Hazard(game); yield return null;
            Assert.That(game.Shields, Is.Zero); Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            Clear(game); Hazard(game); yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.GameOver));
        }

        [UnityTest]
        public IEnumerator CheatedRunsCannotOverwriteNormalBestEvenAfterDisablingCheatAndCleanupClearsIndicator()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); Clear(game);
            RunnerHud hud = Object.FindAnyObjectByType<RunnerHud>(); int best = game.Best;
            game.ToggleInvincibility();
            typeof(CubeDashGame).GetField("<Score>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, best + 100);
            typeof(CubeDashGame).GetMethod("Crash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            game.ToggleInvincibility(); Hazard(game); yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.GameOver)); Assert.That(game.Best, Is.EqualTo(best));
            Assert.That(PlayerPrefs.GetInt("CubeDash.BestColorScore", 0), Is.EqualTo(best));
            game.StartRun(); Clear(game); game.ToggleInvincibility(); game.enabled = false;
            Assert.That(game.InvincibilityEnabled, Is.False); Assert.That(hud.CheatStatus.gameObject.activeSelf, Is.False);
            typeof(CubeDashGame).GetField("<Score>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, best + 100);
            game.enabled = true; Hazard(game); yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.GameOver));
            Assert.That(game.Best, Is.EqualTo(best), "Disabling the component must not launder a cheated run into a normal best.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
