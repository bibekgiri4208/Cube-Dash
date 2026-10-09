using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class GamepadLaneInputTests
    {
        [Test]
        public void DriftAndThresholdChatterDoNotChangeLanes()
        {
            var input = new GamepadLaneInput();
            Assert.That(input.Read(0.2f), Is.Zero);
            Assert.That(input.Read(-0.3f), Is.Zero);
            Assert.That(input.Read(0.6f), Is.EqualTo(1));
            Assert.That(input.Read(0.5f), Is.Zero);
            Assert.That(input.Read(0.7f), Is.Zero);
        }

        [Test]
        public void HoldingTiltsOnceAndNeutralOrReversalRearmsTheStick()
        {
            var input = new GamepadLaneInput();
            Assert.That(input.Read(1), Is.EqualTo(1));
            for (int frame = 0; frame < 120; frame++) Assert.That(input.Read(1), Is.Zero);
            Assert.That(input.Read(0), Is.Zero);
            Assert.That(input.Read(1), Is.EqualTo(1));
            Assert.That(input.Read(-1), Is.EqualTo(-1));
            Assert.That(input.Read(-1), Is.Zero);
        }

        [Test]
        public void ResetDoesNotCarryAHeldMenuTiltIntoTheRun()
        {
            var input = new GamepadLaneInput();
            input.Reset(0.9f);
            Assert.That(input.Read(0.9f), Is.Zero);
            input.Read(0);
            Assert.That(input.Read(0.9f), Is.EqualTo(1));
        }
    }

    public sealed class DesktopInputSceneTests
    {
        // Resolve after each Play Mode reload instead of retaining NUnit fixture references.
        private Gamepad pad => InputSystem.GetDevice("DesktopTestGamepad") as Gamepad;
        private CubeDashGame game => Object.FindAnyObjectByType<CubeDashGame>();
        private RunnerHud hud => Object.FindAnyObjectByType<RunnerHud>();

        [Test]
        public void SavedSceneIsDesktopFocusedAndUiHasOneConfirmOwner()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                RunnerHud sceneHud = null;
                InputSystemUIInputModule module = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.GetComponent<RunnerHud>() != null) sceneHud = root.GetComponent<RunnerHud>();
                    if (root.GetComponent<InputSystemUIInputModule>() != null) module = root.GetComponent<InputSystemUIInputModule>();
                }
                Transform safe = sceneHud.transform.Find("Safe Area");
                Assert.That(safe.Find("Lane Controls").gameObject.activeSelf, Is.False);
                Assert.That(safe.Find("Desktop Controls Hint").gameObject.activeSelf, Is.False);
                Assert.That(safe.Find("Menu Overlay/Menu Card/Primary Action").GetComponentInChildren<Text>().text, Is.EqualTo("START RUN"));
                Assert.That(safe.Find("Menu Overlay/Menu Card/Description").GetComponent<Text>().text, Does.Not.Contain("swipe"));
                Assert.That(module.move, Is.Not.Null);
                Assert.That(module.submit, Is.Null);
                Assert.That(module.cancel, Is.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private void SendPad(GamepadState state) => InputSystem.QueueStateEvent(pad, state);

        private void ClearTrack()
        {
            foreach (TrackSegment segment in game.Track.Segments)
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
        }

        private void PlaceCube(CubeColor color)
        {
            ClearTrack();
            RunnerCube target = game.Track.Segments[1].Cubes[0];
            target.Configure(color, game.Track.ColorMaterial(color));
            target.transform.position = new Vector3(game.Player.position.x, 0.575f, 0.5f);
            target.gameObject.SetActive(true);
        }

        [UnityTest]
        public IEnumerator GamepadSteersOneLanePerTiltOrDpadPressWithoutTouchUi()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            InputSystem.AddDevice<Gamepad>("DesktopTestGamepad");
            yield return null;
            SendPad(new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            Assert.That(hud.UsingGamepad, Is.True);
            Assert.That(hud.transform.Find("Safe Area/Lane Controls").gameObject.activeSelf, Is.False);
            Assert.That(hud.transform.Find("Safe Area/Desktop Controls Hint").gameObject.activeSelf, Is.False);
            ClearTrack();
            SendPad(new GamepadState().WithButton(GamepadButton.DpadLeft));
            yield return null;
            for (float elapsed = 0; elapsed < 0.35f; elapsed += Time.deltaTime) yield return null;
            Assert.That(game.Player.position.x, Is.LessThan(-2.5f));
            // Stick + D-pad in the same frame must not skip the centre lane.
            SendPad(new GamepadState { leftStick = new Vector2(0.9f, 0) }.WithButton(GamepadButton.DpadRight));
            yield return null;
            for (float elapsed = 0; elapsed < 0.35f; elapsed += Time.deltaTime) yield return null;
            Assert.That(Mathf.Abs(game.Player.position.x), Is.LessThan(0.15f));
            SendPad(new GamepadState { leftStick = new Vector2(0.9f, 0) });
            yield return null;
            for (float elapsed = 0; elapsed < 0.2f; elapsed += Time.deltaTime) yield return null;
            Assert.That(Mathf.Abs(game.Player.position.x), Is.LessThan(0.15f));
            SendPad(new GamepadState());
            yield return null;
            SendPad(new GamepadState { leftStick = new Vector2(0.9f, 0) });
            yield return null;
            for (float elapsed = 0; elapsed < 0.35f; elapsed += Time.deltaTime) yield return null;
            Assert.That(game.Player.position.x, Is.GreaterThan(2.5f));
            SendPad(new GamepadState { leftStick = new Vector2(-0.9f, 0) });
            yield return null;
            for (float elapsed = 0; elapsed < 0.35f; elapsed += Time.deltaTime) yield return null;
            Assert.That(Mathf.Abs(game.Player.position.x), Is.LessThan(0.15f));
        }

        [UnityTest]
        public IEnumerator GamepadNavigatesPauseMenuConfirmsOnceAndRetriesAfterCrash()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            InputSystem.AddDevice<Gamepad>("DesktopTestGamepad");
            yield return null;
            SendPad(new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            PlaceCube(CubeColor.Red);
            yield return null;
            Assert.That(game.Score, Is.EqualTo(1));
            SendPad(new GamepadState().WithButton(GamepadButton.Start));
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Paused));
            float frozen = game.Distance;
            SendPad(new GamepadState().WithButton(GamepadButton.Start));
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Paused));
            Assert.That(game.Distance, Is.EqualTo(frozen));
            SendPad(new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running), "Confirm must not resume and immediately pause again.");
            Assert.That(game.Score, Is.EqualTo(1));
            SendPad(new GamepadState().WithButton(GamepadButton.Start));
            yield return null;
            SendPad(new GamepadState().WithButton(GamepadButton.DpadDown));
            yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Restart"));
            SendPad(new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            Assert.That(game.Score, Is.Zero, "Confirming the focused Restart button should restart, not resume.");
            ClearTrack();
            SendPad(new GamepadState().WithButton(GamepadButton.Start));
            yield return null;
            SendPad(new GamepadState().WithButton(GamepadButton.East));
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            PlaceCube(CubeColor.Blue);
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.GameOver));
            SendPad(new GamepadState().WithButton(GamepadButton.West));
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            PlaceCube(CubeColor.Blue);
            yield return null;
            SendPad(new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
        }

        [UnityTest]
        public IEnumerator DisconnectPausesControllerPlayAndDesktopMenuCanResumeAfterReconnect()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            InputSystem.AddDevice<Gamepad>("DesktopTestGamepad");
            yield return null;
            SendPad(new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            ClearTrack();
            InputSystem.RemoveDevice(pad);
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Paused));
            Assert.That(hud.UsingGamepad, Is.False);
            InputSystem.AddDevice<Gamepad>("DesktopTestGamepad");
            yield return null;
            SendPad(new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            // Exercise the shared desktop fallback actions directly: batch mode does not
            // have a focused Game view for synthetic keyboard events.
            hud.SetInputMode(false);
            game.TogglePause();
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Paused));
            Assert.That(hud.UsingGamepad, Is.False);
            hud.ConfirmSelection();
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            InputSystem.RemoveDevice(pad);
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running), "An unused controller disconnect must not interrupt keyboard play.");
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
