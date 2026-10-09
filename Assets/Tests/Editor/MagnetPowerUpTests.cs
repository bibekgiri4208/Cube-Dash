using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CubeDash.Tests
{
    public sealed class MagnetPowerUpTests
    {
        [Test]
        public void ShieldMultiplierAndMagnetAreSavedSolidModelsAndSwitchWithoutShowingGems()
        {
            foreach (string name in new[] { "ShieldPickup", "DoublePointsPickup", "Magnet" })
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/" + name + ".prefab");
                Assert.That(model, Is.Not.Null);
                Mesh mesh = model.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh.subMeshCount, Is.EqualTo(3));
                Assert.That(mesh.vertexCount, Is.InRange(200, 2200));
                Assert.That(mesh.bounds.size.z, Is.GreaterThan(0.15f));
                Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty);
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                    Assert.That(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                        vertices[triangles[i + 2]] - vertices[triangles[i]]).sqrMagnitude, Is.GreaterThan(1e-12f));
            }
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CubeDash/PowerUp.prefab"));
            try
            {
                PowerUpPickup pickup = instance.GetComponent<PowerUpPickup>();
                foreach (PowerUpType type in System.Enum.GetValues(typeof(PowerUpType)))
                {
                    pickup.Configure(type, null);
                    Assert.That(instance.transform.Find("Gem").gameObject.activeSelf, Is.False);
                    int visible = 0;
                    foreach (Transform child in instance.transform) if (child.gameObject.activeSelf) visible++;
                    Assert.That(visible, Is.EqualTo(1), "Exactly one 3D pickup model is shown.");
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void MagnetRangeSweepDetectsPassingCoinsButRejectsDistantLanes()
        {
            Assert.That(RunnerRules.SweptRange(Vector2.zero, Vector2.zero, new Vector2(2.6f, 20),
                new Vector2(2.6f, -20), 8, out float enter, out float exit), Is.True);
            Assert.That(enter, Is.GreaterThan(0)); Assert.That(exit, Is.LessThan(1));
            Assert.That(enter, Is.LessThan(exit));
            Assert.That(RunnerRules.SweptRange(Vector2.zero, Vector2.zero, new Vector2(9, 20),
                new Vector2(9, -20), 8, out _, out _), Is.False);
            Assert.That(RunnerRules.SweptRange(Vector2.zero, Vector2.zero, Vector2.one,
                Vector2.one, 8, out _, out _), Is.True);
        }

        [UnityTest]
        public IEnumerator GeneratedPowerUpsStaySeparateFromCoinsAndObstaclesAcrossRecycling()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.enabled = false; game.Track.Reset(42);
            int objects = game.Track.GetComponentsInChildren<Transform>(true).Length;
            bool sawMagnet = false;
            for (int section = 0; section < 60; section++)
            {
                Physics.SyncTransforms();
                foreach (TrackSegment segment in game.Track.Segments)
                    for (int row = 0; row < segment.PowerUps.Length; row++)
                    {
                        PowerUpPickup pickup = segment.PowerUps[row];
                        Assert.That(pickup.transform.localPosition.z,
                            Is.EqualTo(segment.CubeHomePosition(row * 3).z - RunnerRules.PowerUpRowOffset));
                        if (!pickup.gameObject.activeSelf) continue;
                        sawMagnet |= pickup.Type == PowerUpType.Magnet;
                        foreach (TrackSegment other in game.Track.Segments)
                            foreach (RunnerCube cube in other.Cubes)
                                if (cube.gameObject.activeSelf)
                                    Assert.That(pickup.Collider.bounds.Intersects(cube.Collider.bounds), Is.False,
                                        "Power-ups never occupy a coin or obstacle's collision space.");
                    }
                game.Track.Advance(42, 0, 0, 1, Vector2.one * 0.575f, 0, false, true, false, out _, out _);
            }
            Assert.That(sawMagnet, Is.True);
            Assert.That(game.Track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
        }

        [UnityTest]
        public IEnumerator MagnetPickupPullsBothSideLanesStacksWithTwoXAndLeavesOtherPickupsAndHazardsAlone()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun(); Clear(game); Apply(game, PowerUpType.DoublePoints);
            RunnerCube left = Coin(game, 0, -game.Track.LaneWidth, 3);
            RunnerCube right = Coin(game, 1, game.Track.LaneWidth, 3);
            RunnerCube hazard = game.Track.Segments[1].Cubes[2];
            hazard.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue), false);
            hazard.transform.position = new Vector3(-game.Track.LaneWidth, 0.95f, 4.5f); hazard.gameObject.SetActive(true);
            PowerUpPickup magnet = Pickup(game, 0, PowerUpType.Magnet, 0, 0.5f);
            PowerUpPickup shield = Pickup(game, 1, PowerUpType.Shield, game.Track.LaneWidth, 2);
            yield return null; yield return null;
            Assert.That(game.MagnetActive, Is.True);
            Assert.That(magnet.gameObject.activeSelf, Is.False);
            for (float elapsed = 0; elapsed < 0.4f; elapsed += Time.deltaTime) yield return null;
            Assert.That(game.Score, Is.EqualTo(4));
            Assert.That(left.gameObject.activeSelf || right.gameObject.activeSelf, Is.False);
            Assert.That(hazard.gameObject.activeSelf, Is.True);
            Assert.That(hazard.transform.position.x, Is.EqualTo(-game.Track.LaneWidth));
            Assert.That(shield.gameObject.activeSelf, Is.True); Assert.That(game.Shields, Is.Zero);
            Assert.That(game.State, Is.EqualTo(CubeDashGame.RunState.Running));
        }

        [UnityTest]
        public IEnumerator MagnetRefreshesFreezesExpiresAndClearsOnRestartAndCrash()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun(); Clear(game); game.enabled = false;
            Apply(game, PowerUpType.Magnet); Tick(game, 4);
            Assert.That(game.MagnetRemaining, Is.EqualTo(6));
            Apply(game, PowerUpType.Magnet); Assert.That(game.MagnetRemaining, Is.EqualTo(10));
            RunnerCube coin = Coin(game, 0, 2.6f, 6);
            game.Track.Advance(0, 0, 0, 0, Vector2.one * 0.575f, 0, false, false, false, false,
                true, 8, game.Player.position, 0.025f, out _, out _);
            Vector3 position = coin.CoinVisual.position;
            Assert.That(position.x, Is.LessThan(2.6f), "The coin visibly moves toward the player before collection.");
            game.TogglePause(); Tick(game, 3); yield return null; yield return null;
            Assert.That(game.MagnetRemaining, Is.EqualTo(10)); Assert.That(coin.CoinVisual.position, Is.EqualTo(position));
            game.TogglePause(); Tick(game, 10);
            Assert.That(game.MagnetActive, Is.False);
            coin.TickCoin(0.01f);
            Assert.That(coin.CoinVisual.localPosition.x, Is.EqualTo(0), "Expiration releases uncollected coins back to their slot.");
            Apply(game, PowerUpType.Magnet); game.StartRun(); Assert.That(game.MagnetRemaining, Is.Zero);
            Apply(game, PowerUpType.Magnet);
            typeof(CubeDashGame).GetMethod("Crash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
            Assert.That(game.MagnetRemaining, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SweptMagnetActivationNeverScoresBeforePickupOrAfterFatalContactAndNeverDoubleCounts()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            Clear(game); game.enabled = false;
            Pickup(game, 0, PowerUpType.Magnet, 0, 8);
            Coin(game, 0, 2.6f, 1);
            Hazard(game, 1, 2);
            Assert.That(Advance(game, false, out int collected), Is.True);
            Assert.That(collected, Is.Zero); Assert.That(game.Track.LastPowerUps, Is.Empty);
            Clear(game);
            Pickup(game, 0, PowerUpType.Magnet, 0, 2);
            Coin(game, 0, 2.6f, 4); Hazard(game, 1, 9);
            Assert.That(Advance(game, false, out collected), Is.True);
            Assert.That(collected, Is.EqualTo(1)); Assert.That(game.Track.LastPowerUps, Does.Contain(PowerUpType.Magnet));
            Clear(game);
            Coin(game, 0, 2.6f, 9); Hazard(game, 1, 1.3f);
            Assert.That(Advance(game, true, out collected), Is.True);
            Assert.That(collected, Is.Zero, "A magnet must not award a later coin after a fatal contact.");
            Clear(game);
            Coin(game, 0, 0, 3);
            Assert.That(Advance(game, true, out collected), Is.False);
            Assert.That(collected, Is.EqualTo(1), "Direct and magnet contacts must not score the same coin twice.");
            Clear(game); RunnerCube airborneCoin = Coin(game, 0, 2.6f, 3);
            game.Track.Advance(12, 0, 0, 0, Vector2.one * 0.575f, 0, false, true, false, false,
                true, 8, game.Player.position, 1, out collected, out _);
            Assert.That(collected, Is.Zero); Assert.That(airborneCoin.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator PickupModelsAnimateOnlyWhileRunningAndResetWhenReconfigured()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.StartRun(); Clear(game);
            PowerUpPickup pickup = Pickup(game, 0, PowerUpType.Magnet, 0, 30);
            Transform model = pickup.transform.Find("Magnet Icon");
            Quaternion homeRotation = model.localRotation;
            Vector3 homePosition = model.localPosition;
            pickup.Tick(0.2f);
            Assert.That(model.localRotation, Is.Not.EqualTo(homeRotation));
            Assert.That(model.localPosition, Is.Not.EqualTo(homePosition));
            game.TogglePause();
            Quaternion rotation = model.localRotation;
            Vector3 position = model.localPosition;
            yield return null; yield return null;
            Assert.That(model.localRotation, Is.EqualTo(rotation));
            Assert.That(model.localPosition, Is.EqualTo(position));
            pickup.Configure(PowerUpType.Magnet, game.Track.PowerUpMaterial(PowerUpType.Magnet));
            pickup.Tick(0.2f);
            Vector3 firstCycle = model.localPosition;
            pickup.Configure(PowerUpType.Magnet, game.Track.PowerUpMaterial(PowerUpType.Magnet)); pickup.Tick(0.2f);
            Assert.That(model.localPosition, Is.EqualTo(firstCycle), "Recycling must not accumulate bob offsets.");
        }

        [UnityTest]
        public IEnumerator MagnetReachesTheOppositeLaneForEveryPlayerColorButIgnoresDistantCoins()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            CubeDashGame game = Object.FindAnyObjectByType<CubeDashGame>();
            game.enabled = false;
            foreach (CubeColor color in System.Enum.GetValues(typeof(CubeColor)))
            {
                game.Track.Reset(42, color); Clear(game);
                RunnerCube near = Coin(game, 0, 2.6f, 3);
                near.Configure(color, game.Track.ColorMaterial(color), true);
                RunnerCube far = Coin(game, 1, 2.6f, 12);
                far.Configure(color, game.Track.ColorMaterial(color), true);
                game.Track.Advance(0, -2.6f, -2.6f, 0, Vector2.one * 0.575f, 0, false, false, false, false,
                    true, 8, new Vector3(-2.6f, 0.575f, 0), 0.25f, out int collected, out _);
                Assert.That(collected, Is.EqualTo(1)); Assert.That(near.gameObject.activeSelf, Is.False);
                Assert.That(far.gameObject.activeSelf, Is.True);
                Assert.That(far.CoinVisual.localPosition.x, Is.EqualTo(0), "Out-of-range coins don't get pulled.");
            }
        }

        private static bool Advance(CubeDashGame game, bool magnet, out int collected)
            => game.Track.Advance(12, 0, 0, 0, Vector2.one * 0.575f, 0, false, false, false, false,
                magnet, 8, game.Player.position, 1, out collected, out _);
        private static void Apply(CubeDashGame game, PowerUpType type)
            => typeof(CubeDashGame).GetMethod("ApplyPowerUp", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { type });
        private static void Tick(CubeDashGame game, float dt)
            => typeof(CubeDashGame).GetMethod("UpdateMagnetTimer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { dt });
        private static void Clear(CubeDashGame game)
        {
            foreach (TrackSegment segment in game.Track.Segments)
            {
                foreach (RunnerCube cube in segment.Cubes) cube.gameObject.SetActive(false);
                foreach (PowerUpPickup pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
            }
        }
        private static RunnerCube Coin(CubeDashGame game, int slot, float x, float z)
        {
            RunnerCube coin = game.Track.Segments[1].Cubes[slot];
            coin.Configure(CubeColor.Red, game.Track.ColorMaterial(CubeColor.Red), true);
            coin.transform.position = new Vector3(x, 0.95f, z); coin.gameObject.SetActive(true); return coin;
        }
        private static void Hazard(CubeDashGame game, int slot, float z)
        {
            RunnerCube cube = game.Track.Segments[1].Cubes[slot];
            cube.Configure(CubeColor.Blue, game.Track.ColorMaterial(CubeColor.Blue), false);
            cube.transform.position = new Vector3(0, 0.95f, z); cube.gameObject.SetActive(true);
        }
        private static PowerUpPickup Pickup(CubeDashGame game, int slot, PowerUpType type, float x, float z)
        {
            PowerUpPickup pickup = game.Track.Segments[1].PowerUps[slot];
            pickup.Configure(type, game.Track.PowerUpMaterial(type));
            pickup.transform.position = new Vector3(x, 1.35f, z); pickup.gameObject.SetActive(true); return pickup;
        }
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
