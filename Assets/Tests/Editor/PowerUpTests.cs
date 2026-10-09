using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CubeDash.Tests
{
    public sealed class PowerUpTests
    {
        [Test]
        public void LevelHasPowerUpSlotsMaterialsAndHudStatus()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Level.unity");
            try
            {
                CubeDashGame game = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.GetComponent<CubeDashGame>() != null) game = root.GetComponent<CubeDashGame>();
                Assert.That(game, Is.Not.Null);
                foreach (TrackSegment segment in game.Track.Segments)
                {
                    Assert.That(segment.PowerUps.Length, Is.EqualTo(3));
                    for (int row = 0; row < segment.PowerUps.Length; row++)
                    {
                        PowerUpPickup pickup = segment.PowerUps[row];
                        Assert.That(pickup, Is.Not.Null);
                        Assert.That(pickup.GetComponent<BoxCollider>(), Is.Not.Null);
                        Transform gem = pickup.transform.Find("Gem");
                        Assert.That(gem, Is.Not.Null);
                        Assert.That(gem.GetComponent<Renderer>(), Is.Not.Null);
                        Assert.That(pickup.transform.localPosition.y, Is.EqualTo(1.35f));
                        Assert.That(pickup.transform.localPosition.z,
                            Is.EqualTo(segment.Cubes[row * 3].transform.localPosition.z - RunnerRules.PowerUpRowOffset));
                        Assert.That(gem.gameObject.activeSelf, Is.False, "Saved pickups show their 3D model before Play.");
                        string[] names = { "Shield Icon", "2x Icon", "Fighter Icon", "Truck Icon", "Magnet Icon" };
                        Assert.That(pickup.transform.Find(names[(int)pickup.Type]).gameObject.activeSelf, Is.True);
                    }
                }
                Assert.That(game.Track.PowerUpMaterial(PowerUpType.Shield), Is.Not.Null);
                Assert.That(game.Track.PowerUpMaterial(PowerUpType.DoublePoints), Is.Not.Null);
                Assert.That(System.Enum.GetValues(typeof(PowerUpType)),
                    Is.EquivalentTo(new[] { PowerUpType.Shield, PowerUpType.DoublePoints, PowerUpType.FighterPlane, PowerUpType.Truck, PowerUpType.Magnet }));
                var trackSettings = new UnityEditor.SerializedObject(game.Track);
                Assert.That(trackSettings.FindProperty("powerUpMaterials").arraySize, Is.EqualTo(5));
                Assert.That(game.Track.PowerUpMaterial(PowerUpType.FighterPlane), Is.Not.Null);
                Assert.That(game.Track.PowerUpMaterial(PowerUpType.Truck), Is.Not.Null);
                Assert.That(game.Track.PowerUpMaterial(PowerUpType.Magnet), Is.Not.Null);

                RunnerHud hud = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.GetComponent<RunnerHud>() != null) hud = root.GetComponent<RunnerHud>();
                Assert.That(hud, Is.Not.Null);
                Text status = hud.transform.Find("Safe Area/Power-Up Status").GetComponent<Text>();
                Assert.That(status, Is.Not.Null);
                Assert.That(status.fontSize, Is.EqualTo(16));
                Assert.That(status.gameObject.activeSelf, Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [UnityTest]
        public IEnumerator ShieldAbsorbsOneWrongColorHit()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            foreach (TrackSegment segment in game.Track.Segments)
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
            RunnerCube wrong = game.Track.Segments[1].Cubes[0];
            wrong.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue));
            wrong.transform.position = new Vector3(0, 0.575f, 2);
            wrong.gameObject.SetActive(true);

            bool fatal = game.Track.Advance(5, 0, 0, 0, Vector2.one * 0.575f, 0, true,
                out int collected, out bool shieldUsed);
            Assert.That(fatal, Is.False, "A shield absorbs the wrong-colour hit.");
            Assert.That(shieldUsed, Is.True);
            Assert.That(collected, Is.Zero);
            Assert.That(wrong.gameObject.activeSelf, Is.False, "The absorbed cube is consumed.");

            wrong.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue));
            wrong.transform.position = new Vector3(0, 0.575f, 2);
            wrong.gameObject.SetActive(true);
            fatal = game.Track.Advance(5, 0, 0, 0, Vector2.one * 0.575f, 0, false,
                out collected, out shieldUsed);
            Assert.That(fatal, Is.True, "Without a shield the same hit is fatal.");
            Assert.That(shieldUsed, Is.False);
        }

        [UnityTest]
        public IEnumerator MatchingCubeRequiresDirectContact()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            foreach (TrackSegment segment in game.Track.Segments)
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
            RunnerCube match = game.Track.Segments[1].Cubes[0];
            match.Configure(game.PlayerCubeColor, game.Track.ColorMaterial(game.PlayerCubeColor));
            match.transform.position = new Vector3(game.Track.LaneWidth, 0.575f, 2);
            match.gameObject.SetActive(true);

            game.Track.Advance(5, 0, 0, 0, Vector2.one * 0.575f, 0, out int collected);
            Assert.That(collected, Is.Zero, "An adjacent lane is out of reach.");
            Assert.That(match.gameObject.activeSelf, Is.True);

            match.Configure(game.PlayerCubeColor, game.Track.ColorMaterial(game.PlayerCubeColor));
            match.transform.position = new Vector3(0, 0.575f, 2);
            match.gameObject.SetActive(true);
            game.Track.Advance(5, 0, 0, 0, Vector2.one * 0.575f, 0, out collected);
            Assert.That(collected, Is.EqualTo(1), "A matching cube in the player's path is collected.");
            Assert.That(match.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator DoublePointsPickupDoublesScoringInGame()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun();
            foreach (TrackSegment segment in game.Track.Segments)
            {
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
            PowerUpPickup points = game.Track.Segments[1].PowerUps[0];
            points.Configure(PowerUpType.DoublePoints, game.Track.PowerUpMaterial(PowerUpType.DoublePoints));
            points.transform.position = new Vector3(0, 1.35f, 0.5f);
            points.gameObject.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
            Assert.That(game.DoublePointsActive, Is.True, "The Double Points pickup was collected.");

            foreach (PowerUpPickup pickup in game.Track.Segments[1].PowerUps) pickup.gameObject.SetActive(false);
            RunnerCube match = game.Track.Segments[1].Cubes[0];
            match.Configure(game.PlayerCubeColor, game.Track.ColorMaterial(game.PlayerCubeColor));
            match.transform.position = new Vector3(0, 0.575f, 0.5f);
            match.gameObject.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(game.Score, Is.EqualTo(2), "Each cube is worth two points while Double Points is active.");
            Assert.That(match.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ShieldPickupProtectsTheRunnerInGame()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun();
            foreach (TrackSegment segment in game.Track.Segments)
            {
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
            PowerUpPickup shieldUp = game.Track.Segments[1].PowerUps[0];
            shieldUp.Configure(PowerUpType.Shield, game.Track.PowerUpMaterial(PowerUpType.Shield));
            shieldUp.transform.position = new Vector3(0, 1.35f, 0.5f);
            shieldUp.gameObject.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(game.Shields, Is.EqualTo(1), "The Shield pickup granted a shield.");

            foreach (PowerUpPickup pickup in game.Track.Segments[1].PowerUps) pickup.gameObject.SetActive(false);
            RunnerCube wrong = game.Track.Segments[1].Cubes[0];
            wrong.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue));
            wrong.transform.position = new Vector3(0, 0.575f, 0.5f);
            wrong.gameObject.SetActive(true);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running), "The shield absorbed the wrong-colour hit.");
            Assert.That(game.Shields, Is.Zero);

            wrong.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue));
            wrong.transform.position = new Vector3(0, 0.575f, 0.5f);
            wrong.gameObject.SetActive(true);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.GameOver), "The next wrong-colour hit ends the run.");
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
