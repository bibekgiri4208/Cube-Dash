using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class ForwardMovementTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }

        private static void Invoke(CubeDashGame game, string method, params object[] args)
        {
            var target = typeof(CubeDashGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(target, Is.Not.Null, method);
            target.Invoke(game, args);
        }

        // Keep captured locals outside EnterPlayMode iterators: Unity cannot restore compiler
        // display classes created before its domain reload.
        private static Vector3[] RelativePositions(IEnumerable<Transform> transforms, Vector3 origin)
            => transforms.Select(t => t.position - origin).ToArray();

        [Test]
        public void CountrysideTilesStayStillUntilEntirelyBehindAndKeepCoveringLongRuns()
        {
            foreach (var layout in new[] { (284f, 248f, -32f), (568f, 700f, -200f) })
            {
                for (int i = 0; i < 3; i++)
                {
                    float previous = i * layout.Item1;
                    for (int step = 0; step <= 24000; step++)
                    {
                        float distance = step * 0.5f;
                        float z = CountrySideRunnerScenery.ForwardTilePosition(distance, i, 3, layout.Item1, layout.Item2, layout.Item3);
                        Assert.That(z + layout.Item2, Is.GreaterThanOrEqualTo(distance + layout.Item3), "Never leave an exhausted tile behind the retirement threshold.");
                        // The old modulo scroll prematurely jumped distant landscape tiles at a lap boundary.
                        // Check the world-space coverage instead, including those long landscape silhouettes.
                        float[] coverage = Enumerable.Range(0, 3).Select(tile => CountrySideRunnerScenery.ForwardTilePosition(distance, tile, 3, layout.Item1, layout.Item2, layout.Item3)).OrderBy(value => value).ToArray();
                        Assert.That(coverage[1] - coverage[0], Is.EqualTo(layout.Item1));
                        Assert.That(coverage[2] - coverage[1], Is.EqualTo(layout.Item1));
                        if (z != previous)
                        {
                            Assert.That(previous + layout.Item2, Is.LessThan(distance + layout.Item3));
                            Assert.That(z - previous, Is.EqualTo(3 * layout.Item1));
                        }
                        previous = z;
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator EveryGameplaySceneMovesPlayerAndCameraThroughStationarySections()
        {
            yield return new EnterPlayMode();
            foreach (string name in new[] { "Level", "City", "Desert", "Beach", "Jungle", "Country Side" })
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + name + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun();
                if (!game.InvincibilityEnabled) game.ToggleInvincibility();
                Vector3 start = game.Player.position;
                float cameraGap = game.GameCamera.transform.position.z - start.z;
                float startDistance = game.Distance;
                var positions = game.Track.Segments.Select(s => s.transform.position).ToArray();
                var country = Object.FindAnyObjectByType<CountrySideRunnerScenery>();
                if (country != null) country.SyncDistance();
                var tiles = country != null ? country.Tiles.Select(t => t.position).ToArray() : null;
                for (int frame = 0; frame < 8; frame++) yield return null;
                Assert.That(game.Track.PlayerMovesForward, Is.True, name);
                Assert.That(game.Player.position.z, Is.GreaterThan(start.z), name);
                Assert.That(game.Player.position.z - start.z, Is.EqualTo(game.Distance - startDistance).Within(0.002f), name);
                Assert.That(game.GameCamera.transform.position.z - game.Player.position.z, Is.EqualTo(cameraGap).Within(0.002f), name);
                for (int i = 0; i < positions.Length; i++)
                {
                    if (positions[i].z + RunnerRules.SegmentLength >= game.Player.position.z - 18)
                        Assert.That(game.Track.Segments[i].transform.position, Is.EqualTo(positions[i]), name);
                    else Assert.That(game.Track.Segments[i].transform.position.z, Is.GreaterThan(game.Player.position.z), name);
                }
                if (country != null)
                    for (int i = 0; i < tiles.Length; i++) Assert.That(country.Tiles[i].position, Is.EqualTo(tiles[i]));
                game.TogglePause(); Vector3 frozen = game.Player.position;
                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(game.Player.position, Is.EqualTo(frozen), name);
                game.StartRun();
                Assert.That(game.Player.position, Is.EqualTo(start), name);
                Assert.That(game.Track.WorldOriginOffset, Is.Zero);
                Assert.That(game.GameCamera.transform.position.z - game.Player.position.z, Is.EqualTo(cameraGap).Within(0.002f));
                game.TogglePause();
            }
        }

        private sealed class Frame
        {
            public int Coins;
            public PowerUpType[] Bonuses;
            public Vector3[] Sections;
            public string Layout;
        }

        private static string Layout(EndlessTrack track)
            => string.Join(";", track.Segments.SelectMany(s => s.Cubes).Select(c => c.gameObject.activeSelf + ":" + c.Color + ":" + c.Hazard + ":" + c.transform.localPosition));

        [UnityTest]
        public IEnumerator ForwardSweepsMatchSeededContactsAndRecycleAheadOverTenKilometres()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/City.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); game.TogglePause();
            var track = game.Track; int count = track.GetComponentsInChildren<Transform>(true).Length;
            var expected = new List<Frame>(); const int steps = 250; const float travel = 42;
            track.Reset(7321, game.PlayerCubeColor);
            for (int step = 0; step < steps; step++)
            {
                track.Advance(travel, 0, 0, LevelProgression.Difficulty((step + 1) * travel), Vector2.one * 0.45f,
                    0, false, false, true, false, out int coins, out _);
                expected.Add(new Frame { Coins = coins, Bonuses = track.LastPowerUps.ToArray(),
                    Sections = track.Segments.Select(s => s.transform.position).ToArray(), Layout = Layout(track) });
            }
            track.ResetForward(7321, game.PlayerCubeColor, 0);
            float z = 0;
            for (int step = 0; step < steps; step++)
            {
                Vector3[] before = track.Segments.Select(s => s.transform.position).ToArray();
                z += travel;
                bool fatal = track.AdvanceForward(travel, 0, 0, LevelProgression.Difficulty((step + 1) * travel),
                    Vector2.one * 0.45f, z, false, false, true, false, false, 8, new Vector3(0, 0.575f, z), 0.1f,
                    out int coins, out _);
                Assert.That(fatal, Is.False);
                Assert.That(coins, Is.EqualTo(expected[step].Coins), "Swept coin contact order");
                Assert.That(track.LastPowerUps, Is.EqualTo(expected[step].Bonuses), "Swept bonus contact order");
                Assert.That(Layout(track), Is.EqualTo(expected[step].Layout), "Seeded hazards and coins after recycling");
                for (int i = 0; i < track.Segments.Length; i++)
                {
                    Vector3 position = track.Segments[i].transform.position;
                    Assert.That(position - Vector3.forward * z, Is.EqualTo(expected[step].Sections[i]));
                    if (position != before[i])
                    {
                        Assert.That(before[i].z + RunnerRules.SegmentLength, Is.LessThan(z - 18));
                        Assert.That(position.z, Is.GreaterThan(z));
                    }
                }
                if (z >= 4096) { track.ShiftWorldOrigin(4096); z -= 4096; }
                Vector4 curve = Shader.GetGlobalVector("_CubeDashRoadCurve");
                Assert.That(curve.w, Is.EqualTo(z));
            }
            Assert.That(track.Travelled, Is.EqualTo(10500));
            Assert.That(track.WorldOriginOffset, Is.EqualTo(8192));
            Assert.That(track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        }

        [UnityTest]
        public IEnumerator ForwardContactsRespectShieldTruckFlightAndMagnetAwayFromWorldOrigin()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/City.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.StartRun(); game.TogglePause();
            var track = game.Track;
            for (int scenario = 0; scenario < 6; scenario++)
            {
                const float startZ = 600, travel = 6;
                track.ResetForward(7321, game.PlayerCubeColor, startZ);
                foreach (var segment in track.Segments)
                {
                    foreach (var cube in segment.Cubes) cube.gameObject.SetActive(false);
                    foreach (var pickup in segment.PowerUps) pickup.gameObject.SetActive(false);
                }
                var obstacle = track.Segments[1].Cubes[0];
                CubeColor wrong = game.PlayerCubeColor == CubeColor.Blue ? CubeColor.Red : CubeColor.Blue;
                obstacle.Configure(wrong, track.ColorMaterial(wrong));
                obstacle.transform.position = new Vector3(0, 0.575f, startZ + 5); obstacle.gameObject.SetActive(scenario != 4);
                var coin = track.Segments[1].Cubes[1];
                coin.Configure(game.PlayerCubeColor, track.ColorMaterial(game.PlayerCubeColor), true);
                coin.transform.position = new Vector3(scenario == 4 ? track.LaneWidth : 0, 0.95f, startZ + 3);
                coin.gameObject.SetActive(scenario >= 3);
                var bonus = track.Segments[1].PowerUps[0];
                if (scenario == 5)
                {
                    bonus.Configure(PowerUpType.FighterPlane, track.PowerUpMaterial(PowerUpType.FighterPlane));
                    bonus.transform.position = new Vector3(0, 1.35f, startZ + 1); bonus.gameObject.SetActive(true);
                }
                bool airborne = scenario == 3;
                bool fatal = track.AdvanceForward(travel, 0, 0, 0, Vector2.one * 0.45f, startZ + travel,
                    scenario == 1, airborne, false, scenario == 2, scenario == 4, 8,
                    new Vector3(0, airborne ? 3.775f : 0.575f, startZ + travel), 1,
                    out int collected, out bool shieldUsed);
                Assert.That(fatal, Is.EqualTo(scenario == 0), "Scenario " + scenario);
                Assert.That(shieldUsed, Is.EqualTo(scenario == 1));
                Assert.That(track.LastTruckSmashes, Is.EqualTo(scenario == 2 ? 1 : 0));
                Assert.That(collected, Is.EqualTo(scenario >= 3 ? 1 : 0), "Moving-target coin attraction, scenario " + scenario);
                if (scenario == 5) Assert.That(track.LastPowerUps, Does.Contain(PowerUpType.FighterPlane));
            }
        }

        [UnityTest]
        public IEnumerator PrecisionResetKeepsCameraTrackSceneryAndWakeTogetherAndRetryRestoresOrigin()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Country Side.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>();
            Assert.That(game, Is.Not.Null);
            game.StartRun();
            game.TogglePause();
            var track = game.Track;
            Vector3 origin = game.Player.position; float z = origin.z + 4100;
            game.Player.position += Vector3.forward * 4100;
            track.AdvanceForward(4100, 0, 0, 1, Vector2.one * 0.45f, z, false, false, true, false, false, 8,
                game.Player.position, 0.1f, out _, out _);
            Invoke(game, "UpdateCamera", 1f);
            var country = Object.FindAnyObjectByType<CountrySideRunnerScenery>(); Assert.That(country, Is.Not.Null); country.SyncDistance();
            var wake = Object.FindAnyObjectByType<CubeWake>(FindObjectsInactive.Include); Assert.That(wake, Is.Not.Null); wake.ResetWake();
            var segments = RelativePositions(track.Segments.Select(s => s.transform), game.Player.position);
            var tiles = RelativePositions(country.Tiles, game.Player.position);
            var trail = RelativePositions(wake.Pieces, game.Player.position);
            Vector3 camera = game.GameCamera.transform.position - game.Player.position;
            Invoke(game, "RebaseWorldIfNeeded"); country.SyncDistance();
            Assert.That(game.Player.position.z, Is.EqualTo(origin.z + 4));
            Assert.That(track.WorldOriginOffset, Is.EqualTo(4096));
            Assert.That(game.GameCamera.transform.position - game.Player.position, Is.EqualTo(camera));
            for (int i = 0; i < segments.Length; i++) Assert.That(track.Segments[i].transform.position - game.Player.position, Is.EqualTo(segments[i]));
            for (int i = 0; i < tiles.Length; i++) Assert.That(country.Tiles[i].position - game.Player.position, Is.EqualTo(tiles[i]));
            for (int i = 0; i < trail.Length; i++) Assert.That(Vector3.Distance(wake.Pieces[i].position - game.Player.position, trail[i]), Is.LessThan(0.001f));
            game.StartRun(); country.SyncDistance(); game.TogglePause();
            Assert.That(game.Player.position, Is.EqualTo(origin));
            Assert.That(track.Travelled, Is.Zero); Assert.That(track.WorldOriginOffset, Is.Zero);
            for (int i = 0; i < country.Tiles.Length; i++) Assert.That(country.Tiles[i].localPosition.z, Is.EqualTo(i * country.TileLength));
        }
    }
}
