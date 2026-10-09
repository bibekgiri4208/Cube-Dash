using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class RunnerSceneTests
    {
        [Test]
        public void LevelUsesGentlerDifficultyTuning()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                CubeDashGame game = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.GetComponent<CubeDashGame>() != null) game = root.GetComponent<CubeDashGame>();
                Assert.That(game, Is.Not.Null);
                var settings = new UnityEditor.SerializedObject(game);
                Assert.That(settings.FindProperty("startSpeed").floatValue, Is.EqualTo(12f));
                Assert.That(settings.FindProperty("maximumSpeed").floatValue, Is.EqualTo(30f));
                Assert.That(settings.FindProperty("acceleration").floatValue, Is.EqualTo(0.8f));
                Assert.That(settings.FindProperty("scoreForMaximumDifficulty").intValue, Is.EqualTo(180));
                Assert.That(RunnerRules.RowSpacing / settings.FindProperty("startSpeed").floatValue,
                    Is.GreaterThan(1f), "The opening gives more than a second between rows.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void LevelHasRealObjectsAndReferencesBeforePlayMode()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                CubeDashGame game = null;
                int cameras = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.GetComponent<CubeDashGame>() != null) game = root.GetComponent<CubeDashGame>();
                    cameras += root.GetComponentsInChildren<Camera>(true).Length;
                }
                Assert.That(game, Is.Not.Null);
                Assert.That(cameras, Is.EqualTo(1));
                Assert.That(game.GameCamera.name, Is.EqualTo("Main Camera"));
                Assert.That(game.Player.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
                Assert.That(game.Player.GetComponent<BoxCollider>(), Is.Not.Null);
                Assert.That(game.GameCamera.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
                Assert.That(game.Track.Segments.Length, Is.EqualTo(8));
                foreach (TrackSegment segment in game.Track.Segments)
                {
                    Assert.That(segment.Obstacles.Length, Is.EqualTo(9));
                    foreach (BoxCollider obstacle in segment.Obstacles) Assert.That(obstacle, Is.Not.Null);
                    Assert.That(segment.transform.Find("City Surroundings"), Is.Not.Null);
                    foreach (RunnerCube cube in segment.Cubes) Assert.That(cube, Is.Not.Null);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void HudKeepsScoreTopRightPauseTopLeftAndQuitOnGameOver()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                RunnerHud hud = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.GetComponent<RunnerHud>() != null) hud = root.GetComponent<RunnerHud>();
                Assert.That(hud, Is.Not.Null);
                Transform safe = hud.transform.Find("Safe Area");

                RectTransform score = (RectTransform)safe.Find("Score Panel");
                Assert.That(score.anchorMin.x, Is.EqualTo(1f));
                Assert.That(score.anchorMin.y, Is.EqualTo(1f));
                Assert.That(score.anchoredPosition.x, Is.LessThan(0f));
                Assert.That(safe.Find("Distance").GetComponent<Text>().alignment, Is.EqualTo(TextAnchor.MiddleRight));

                RectTransform pause = (RectTransform)safe.Find("Pause");
                Assert.That(pause.anchorMin.x, Is.EqualTo(0f));
                Assert.That(pause.anchorMin.y, Is.EqualTo(1f));
                Assert.That(pause.anchoredPosition.x, Is.GreaterThan(0f));
                Assert.That(pause.Find("Label").gameObject.activeSelf, Is.False, "The pause button uses an icon, not text.");
                Assert.That(pause.Find("Pause Icon"), Is.Not.Null);
                Assert.That(pause.Find("Pause Icon").childCount, Is.EqualTo(2));

                Assert.That(safe.Find("Desktop Controls Hint").gameObject.activeSelf, Is.False);
                Assert.That(safe.Find("Menu Overlay/Menu Card/Eyebrow").gameObject.activeSelf, Is.False);
                Assert.That(safe.Find("Menu Overlay/Menu Card/Accent").gameObject.activeSelf, Is.True);
                Assert.That(safe.Find("Menu Overlay/Menu Card/Pause Motif"), Is.Not.Null);
                Assert.That(safe.Find("Menu Overlay/Menu Card/Pause Motif").gameObject.activeSelf, Is.False,
                    "The abstract pause motif only shows while paused.");

                RectTransform primary = (RectTransform)safe.Find("Menu Overlay/Menu Card/Primary Action");
                RectTransform restart = (RectTransform)safe.Find("Menu Overlay/Menu Card/Restart");
                Assert.That(primary.sizeDelta, Is.EqualTo(restart.sizeDelta), "Resume and Restart share one size.");

                Transform pauseQuit = safe.Find("Menu Overlay/Menu Card/Quit");
                Assert.That(pauseQuit, Is.Not.Null, "The pause menu offers a Quit action.");
                Assert.That(pauseQuit.gameObject.activeSelf, Is.False, "Quit is hidden until the run is paused.");
                Assert.That(pauseQuit.GetComponent<ArcadeButton>(), Is.Not.Null);
                Button pauseQuitButton = pauseQuit.GetComponent<Button>();
                Assert.That(pauseQuitButton.onClick.GetPersistentEventCount(), Is.EqualTo(1));
                Assert.That(pauseQuitButton.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(RunnerHud.QuitGame)));
                Assert.That(pauseQuitButton.colors.highlightedColor, Is.EqualTo(primary.GetComponent<Button>().colors.highlightedColor),
                    "Quit shares the same hover highlight as the other menu buttons.");
                Assert.That(pauseQuit.GetComponent<Outline>(), Is.Not.Null, "Quit has the same focus outline as the other buttons.");

                Transform composition = safe.Find("Game Over Overlay/Abstract Composition");
                Button quit = composition.Find("Quit").GetComponent<Button>();
                Assert.That(quit, Is.Not.Null);
                Assert.That(quit.onClick.GetPersistentEventCount(), Is.EqualTo(1));
                Assert.That(quit.onClick.GetPersistentMethodName(0), Is.EqualTo(nameof(RunnerHud.QuitGame)));
                Assert.That(composition.Find("Quit").GetComponent<ArcadeButton>(), Is.Not.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator PauseMenuShowsAbstractMotifOnlyWhilePaused()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            RunnerHud hud = Object.FindAnyObjectByType<RunnerHud>();
            Transform card = hud.transform.Find("Safe Area/Menu Overlay/Menu Card");
            Assert.That(card.Find("Pause Motif").gameObject.activeSelf, Is.False);
            Assert.That(card.Find("Accent").gameObject.activeSelf, Is.True);
            Assert.That(card.Find("Keyboard Hint").gameObject.activeSelf, Is.True);
            Assert.That(card.Find("Quit").gameObject.activeSelf, Is.False);

            game.StartRun();
            yield return null;
            game.TogglePause();
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Paused));
            Assert.That(card.Find("Pause Motif").gameObject.activeSelf, Is.True);
            Assert.That(card.Find("Accent").gameObject.activeSelf, Is.False, "The start-menu accent yields to the pause motif.");
            Assert.That(card.Find("Keyboard Hint").gameObject.activeSelf, Is.False, "The pause menu drops the instruction line.");
            Assert.That(card.Find("Quit").gameObject.activeSelf, Is.True, "The pause menu offers Quit.");
        }

        [UnityTest]
        public IEnumerator SceneBuildsRunsPausesAndRestartsWithoutGrowingThePool()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            Assert.That(game, Is.Not.Null);
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Ready));
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(game.GameCamera, Is.SameAs(Camera.main));
            Assert.That(game.GameCamera.name, Is.EqualTo("Main Camera"));
            Assert.That(Object.FindObjectsByType<Camera>().Length, Is.EqualTo(1));
            Transform pool = game.Track.transform;
            Assert.That(pool.childCount, Is.EqualTo(8));

            game.StartRun();
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            Assert.That(game.Distance, Is.GreaterThan(0));
            Assert.That(game.Score, Is.Zero);
            game.ChangeLane(1);
            yield return null;
            yield return null;
            Assert.That(game.Player.position.x, Is.GreaterThan(0));
            CubeWake wake = Object.FindAnyObjectByType<CubeWake>();
            Assert.That(wake, Is.Not.Null);
            Assert.That(wake.Pieces.Length, Is.EqualTo(12));
            Assert.That(wake.Pieces[0].position.x, Is.GreaterThan(0));

            game.TogglePause();
            float pausedDistance = game.Distance;
            yield return null;
            yield return null;
            Assert.That(game.Distance, Is.EqualTo(pausedDistance));
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Paused));

            game.TogglePause();
            yield return null;
            Assert.That(game.Distance, Is.GreaterThan(pausedDistance));
            game.StartRun();
            Assert.That(game.Distance, Is.Zero);
            Assert.That(game.Score, Is.Zero);
            Assert.That(game.Player.position.x, Is.Zero);
            Assert.That(wake.Pieces[0].position.x, Is.Zero);
            Assert.That(pool.childCount, Is.EqualTo(8));
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator MatchingCubeIsConsumedButWrongColorIsFatal()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            foreach (TrackSegment segment in game.Track.Segments)
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
            RunnerCube target = game.Track.Segments[1].Cubes[0];
            target.Configure(game.PlayerCubeColor, game.Track.ColorMaterial(game.PlayerCubeColor));
            target.transform.position = new Vector3(0, 0.575f, 2);
            target.gameObject.SetActive(true);
            bool fatal = game.Track.Advance(5, 0, 0, 0, Vector2.one * 0.575f, 0, out int collected);
            Assert.That(fatal, Is.False);
            Assert.That(collected, Is.EqualTo(1));
            Assert.That(target.gameObject.activeSelf, Is.False);
            game.Track.Advance(0, 0, 0, 0, Vector2.one * 0.575f, 0, out collected);
            Assert.That(collected, Is.Zero, "A consumed cube must not score again.");

            target.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue));
            target.transform.position = new Vector3(0, 0.575f, 2);
            target.gameObject.SetActive(true);
            fatal = game.Track.Advance(5, 0, 0, 0, Vector2.one * 0.575f, 0, out collected);
            Assert.That(fatal, Is.True);
            Assert.That(collected, Is.Zero);
        }

        [UnityTest]
        public IEnumerator NoPointsAreAwardedAfterAnEarlierWrongColorHit()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            foreach (TrackSegment segment in game.Track.Segments)
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
            RunnerCube laterMatch = game.Track.Segments[1].Cubes[0];
            RunnerCube earlierWrong = game.Track.Segments[1].Cubes[1];
            laterMatch.Configure(CubeColor.Red, game.Track.ColorMaterial(CubeColor.Red));
            earlierWrong.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue));
            laterMatch.transform.position = new Vector3(0, 0.575f, 5);
            earlierWrong.transform.position = new Vector3(0, 0.575f, 2);
            laterMatch.gameObject.SetActive(true);
            earlierWrong.gameObject.SetActive(true);
            Assert.That(game.Track.Advance(10, 0, 0, 0, Vector2.one * 0.575f, 0, out int collected), Is.True);
            Assert.That(collected, Is.Zero);
            Assert.That(laterMatch.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator CollectingACubeInGameAwardsPointsAndRestartResetsThem()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun();
            foreach (TrackSegment segment in game.Track.Segments)
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
            RunnerCube target = game.Track.Segments[1].Cubes[0];
            target.Configure(game.PlayerCubeColor, game.Track.ColorMaterial(game.PlayerCubeColor));
            target.transform.position = new Vector3(0, 0.575f, 0.5f);
            target.gameObject.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            Assert.That(game.Score, Is.EqualTo(1));
            Assert.That(target.gameObject.activeSelf, Is.False);
            game.StartRun();
            Assert.That(game.Score, Is.Zero);
        }

        [UnityTest]
        public IEnumerator WrongColorEndsTheGameAndRestartRestoresRunning()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun();
            foreach (TrackSegment segment in game.Track.Segments)
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
            RunnerCube target = game.Track.Segments[1].Cubes[0];
            target.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue));
            target.transform.position = new Vector3(0, 0.575f, 0.5f);
            target.gameObject.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.GameOver));
            Assert.That(game.Score, Is.Zero);
            RunnerHud hud = Object.FindAnyObjectByType<RunnerHud>();
            Assert.That(hud.GameOverOverlay.activeSelf, Is.True);
            Assert.That(hud.GameOverScore.text, Is.EqualTo("0"));
            hud.GameOverOverlay.transform.Find("Abstract Composition/Retry").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            Assert.That(game.Score, Is.Zero);
            Assert.That(hud.GameOverOverlay.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator TrackRecyclesForFiftyKilometresWithAFixedObjectCount()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            EndlessTrack track = Object.FindAnyObjectByType<EndlessTrack>();
            GameObject root = track.gameObject;
            track.Reset(42);
            yield return null;
            int objectCount = root.GetComponentsInChildren<Transform>(true).Length;
            for (int step = 0; step < 10000; step++)
                track.Advance(5f, 100f, 100f, 1f, Vector2.one * 0.45f);

            Assert.That(root.transform.childCount, Is.EqualTo(8));
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount));
            float[] starts = new float[8];
            for (int i = 0; i < starts.Length; i++) starts[i] = root.transform.GetChild(i).position.z;
            System.Array.Sort(starts);
            Assert.That(starts[0], Is.InRange(-60f, -18f));
            for (int i = 1; i < starts.Length; i++)
                Assert.That(starts[i] - starts[i - 1], Is.EqualTo(RunnerRules.SegmentLength));
        }
    }
}
