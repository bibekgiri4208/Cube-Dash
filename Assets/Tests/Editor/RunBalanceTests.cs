using System.Collections;
using System.Collections.Generic;
using System.Text;
using CubeDash.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CubeDash.Tests
{
    public sealed class RunBalanceTests
    {
        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [Test]
        public void WeightedBonusesRespectSharedVehicleAndGeneralGapsAcrossManySeeds()
        {
            int[] counts = new int[5];
            for (int seed = 0; seed < 100; seed++)
            {
                var spawns = new PowerUpSpawns(seed);
                var same = new PowerUpSpawns(seed);
                float lastBonus = float.NegativeInfinity, lastVehicle = float.NegativeInfinity;
                for (int row = 0; row < 3000; row++)
                {
                    float distance = row * RunnerRules.RowSpacing;
                    bool spawned = spawns.TryChoose(distance, 0.18f, 5, out var type);
                    Assert.That(same.TryChoose(distance, 0.18f, 5, out var sameType), Is.EqualTo(spawned));
                    Assert.That(sameType, Is.EqualTo(type));
                    if (!spawned) continue;
                    Assert.That(distance, Is.GreaterThanOrEqualTo(PowerUpSpawns.FirstBonusDistance));
                    Assert.That(distance - lastBonus, Is.GreaterThanOrEqualTo(PowerUpSpawns.MinimumBonusGap));
                    lastBonus = distance; counts[(int)type]++;
                    if (!PowerUpSpawns.IsVehicle(type)) continue;
                    Assert.That(distance, Is.GreaterThanOrEqualTo(PowerUpSpawns.FirstVehicleDistance));
                    Assert.That(distance - lastVehicle, Is.GreaterThanOrEqualTo(PowerUpSpawns.MinimumVehicleGap), "Truck and Flight must share the same gate, not chain into one another.");
                    lastVehicle = distance;
                }
            }
            foreach (int count in counts) Assert.That(count, Is.GreaterThan(100));
            int total = 0; foreach (int count in counts) total += count;
            Assert.That((counts[2] + counts[3]) / (float)total, Is.LessThan(0.12f), "Vehicle bonuses are rare treats, not the default way to survive.");
            Assert.That(counts[0], Is.GreaterThan(counts[2] * 3));
            Assert.That(counts[4], Is.GreaterThan(counts[3] * 3));
        }

        [Test]
        public void DisabledAndPartialBonusSetsNeverProduceUnavailableTypes()
        {
            for (int available = 0; available <= 5; available++)
            {
                var spawns = new PowerUpSpawns(42);
                for (int row = 0; row < 3000; row++)
                {
                    float distance = row * 14;
                    Assert.That(spawns.TryChoose(distance, 0, available, out _), Is.False);
                    if (spawns.TryChoose(distance, 1, available, out var type))
                        Assert.That((int)type, Is.InRange(0, available - 1));
                }
            }
        }

        [Test]
        public void LaterPatternsDemandRealDodgesAndPressureEveryLaneWithoutRemovingRecovery()
        {
            int openingHazards = 0, lateHazards = 0, forcedMoves = 0, recoveryRows = 0;
            for (int seed = 0; seed < 100; seed++)
            {
                var opening = new HighwayPatterns(seed);
                for (int row = 0; row < 9; row++)
                {
                    var pattern = opening.Next(row * 14);
                    if (pattern.BlockedMask != 0) openingHazards++;
                }
                var late = new HighwayPatterns(seed);
                int previous = 1, union = 0;
                for (int row = 0; row < 180; row++)
                {
                    var pattern = late.Next(1100 + row * 14);
                    if (pattern.Encounter == HighwayEncounter.Recovery)
                    {
                        recoveryRows++;
                        Assert.That(pattern.BlockedMask, Is.Zero);
                    }
                    else if (row > 0 && pattern.SafeLane != previous)
                    {
                        Assert.That(pattern.BlockedMask & (1 << previous), Is.Not.Zero, "Following a moving route should require leaving the previous lane.");
                        forcedMoves++;
                    }
                    for (int lane = 0; lane < 3; lane++) if ((pattern.BlockedMask & (1 << lane)) != 0) lateHazards++;
                    union |= pattern.BlockedMask;
                    if (row % 30 == 29)
                    {
                        Assert.That(union, Is.EqualTo(7), "No lane may remain an idle safe lane for an entire long phrase block.");
                        union = 0;
                    }
                    previous = pattern.SafeLane;
                }
            }
            Assert.That(lateHazards / 18000f, Is.GreaterThan(openingHazards / 900f * 2));
            Assert.That(forcedMoves, Is.GreaterThan(8500));
            Assert.That(recoveryRows, Is.InRange(1500, 2800));
            Assert.That(LevelProgression.Difficulty(550), Is.EqualTo(0.5f));
        }

        [UnityTest]
        public IEnumerator AllMapsUseSpacedBonusesThroughRecyclingAndResetTheirSchedulesOnRetry()
        {
            yield return new EnterPlayMode();
            foreach (string map in PauseSoundAuthoring.Maps)
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + map + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var game = Object.FindAnyObjectByType<CubeDashGame>();
                game.enabled = false;
                var track = game.Track;
                track.Reset(42);
                int objects = track.GetComponentsInChildren<Transform>(true).Length;
                var seen = new SortedDictionary<int, PowerUpType>();
                for (int step = 0; step < 160; step++)
                {
                    Record(track, seen);
                    track.Advance(42, 0, 0, 1, Vector2.one * 0.45f, 0, false, true, false, false, out _, out _);
                }
                float previous = float.NegativeInfinity, previousVehicle = float.NegativeInfinity;
                int vehicles = 0;
                foreach (var bonus in seen)
                {
                    Assert.That(bonus.Key, Is.GreaterThanOrEqualTo(PowerUpSpawns.FirstBonusDistance), map);
                    Assert.That(bonus.Key - previous, Is.GreaterThanOrEqualTo(PowerUpSpawns.MinimumBonusGap), map);
                    previous = bonus.Key;
                    if (!PowerUpSpawns.IsVehicle(bonus.Value)) continue;
                    Assert.That(bonus.Key, Is.GreaterThanOrEqualTo(PowerUpSpawns.FirstVehicleDistance), map);
                    Assert.That(bonus.Key - previousVehicle, Is.GreaterThanOrEqualTo(PowerUpSpawns.MinimumVehicleGap), map);
                    previousVehicle = bonus.Key; vehicles++;
                }
                Assert.That(seen.Count, Is.GreaterThan(15), "Ordinary rewards must still appear: " + map);
                Assert.That(vehicles, Is.GreaterThan(1), "Vehicle bonuses must remain obtainable: " + map);
                Assert.That(track.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
                string first = Signature(track, 42);
                Assert.That(Signature(track, 42), Is.EqualTo(first), "Retry resets every spawn gate and RNG: " + map);
                var settings = new SerializedObject(track);
                settings.FindProperty("powerUpChance").floatValue = 0; settings.ApplyModifiedPropertiesWithoutUndo();
                track.Reset(42);
                for (int step = 0; step < 25; step++)
                {
                    foreach (var segment in track.Segments)
                        foreach (var pickup in segment.PowerUps) Assert.That(pickup.gameObject.activeSelf, Is.False);
                    track.Advance(42, 0, 0, 1, Vector2.one * 0.45f, 0, false, true, false, false, out _, out _);
                }
            }
        }

        private static void Record(EndlessTrack track, SortedDictionary<int, PowerUpType> seen)
            => Record(track, seen, track.transform.position.z);

        private static void Record(EndlessTrack track, SortedDictionary<int, PowerUpType> seen, float playerZ)
        {
            foreach (var segment in track.Segments)
                foreach (var pickup in segment.PowerUps)
                {
                    if (!pickup.gameObject.activeSelf) continue;
                    int distance = Mathf.RoundToInt(track.Travelled + pickup.transform.position.z - playerZ);
                    if (seen.TryGetValue(distance, out var previous)) Assert.That(pickup.Type, Is.EqualTo(previous));
                    else seen.Add(distance, pickup.Type);
                }
        }

        private static string Signature(EndlessTrack track, int seed)
        {
            track.Reset(seed);
            var seen = new SortedDictionary<int, PowerUpType>();
            for (int step = 0; step < 40; step++)
            {
                Record(track, seen);
                track.Advance(42, 0, 0, 1, Vector2.one * 0.45f, 0, false, true, false, false, out _, out _);
            }
            return Describe(seen);
        }

        private static string Describe(SortedDictionary<int, PowerUpType> seen)
        {
            var text = new StringBuilder();
            foreach (var pair in seen) text.Append(pair.Key).Append(':').Append(pair.Value).Append(';');
            return text.ToString();
        }

        [UnityTest]
        public IEnumerator LiveForwardTravelAndOriginShiftsKeepTheSameDistanceBasedBonusSchedule()
        {
            yield return new EnterPlayMode();
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindAnyObjectByType<CubeDashGame>(); game.enabled = false;
            var track = game.Track;
            string expected = Signature(track, 42);
            track.ResetForward(42, game.PlayerCubeColor, 0);
            var seen = new SortedDictionary<int, PowerUpType>();
            float playerZ = 0;
            for (int step = 0; step < 40; step++)
            {
                Record(track, seen, playerZ);
                // Split alternate frames, so spawning cannot depend on Update frequency.
                for (int frame = 0; frame < 2; frame++)
                {
                    float travel = frame == 0 ? 7 : 35;
                    playerZ += travel;
                    track.AdvanceForward(travel, 0, 0, 1, Vector2.one * 0.45f, playerZ,
                        false, true, false, false, false, 8, Vector3.zero, 0.1f, out _, out _);
                }
                if (step == 20) { track.ShiftWorldOrigin(700); playerZ -= 700; }
            }
            Assert.That(Describe(seen), Is.EqualTo(expected), "Live forward travel, frame splitting and rebasing must not bypass or shift cooldowns.");
        }
    }
}
