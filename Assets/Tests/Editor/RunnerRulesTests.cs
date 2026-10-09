using NUnit.Framework;
using UnityEngine;

namespace CubeDash.Tests
{
    public sealed class RunnerRulesTests
    {
        [Test]
        public void DifficultyRampsByScoreAndClampsAtTheConfiguredLimit()
        {
            Assert.That(RunnerRules.DifficultyForScore(-10, 120), Is.Zero);
            Assert.That(RunnerRules.DifficultyForScore(0, 120), Is.Zero);
            Assert.That(RunnerRules.DifficultyForScore(30, 120), Is.EqualTo(0.25f));
            Assert.That(RunnerRules.DifficultyForScore(60, 120), Is.EqualTo(0.5f));
            Assert.That(RunnerRules.DifficultyForScore(120, 120), Is.EqualTo(1f));
            Assert.That(RunnerRules.DifficultyForScore(300, 120), Is.EqualTo(1f));
            Assert.That(RunnerRules.DifficultyForScore(1, 0), Is.EqualTo(1f));
        }

        [Test]
        public void HighDifficultyDemandsMoreLaneChangesButNeverSkipsALane()
        {
            int[] switches = new int[2];
            for (int difficulty = 0; difficulty < 2; difficulty++)
            {
                System.Random random = new System.Random(42);
                int lane = 1;
                for (int row = 0; row < 10000; row++)
                {
                    int next = RunnerRules.NextSafeLane(random, lane, difficulty);
                    Assert.That(next, Is.InRange(0, 2));
                    Assert.That(Mathf.Abs(next - lane), Is.LessThanOrEqualTo(1));
                    if (next != lane) switches[difficulty]++;
                    lane = next;
                }
            }
            Assert.That(switches[0], Is.InRange(4500, 5500));
            Assert.That(switches[1], Is.InRange(7500, 8500));
        }

        [TestCase(30f)]
        [TestCase(42f)]
        public void AdjacentLaneChangeRemainsClearOfWrongColorsAtTopSpeed(float speed)
        {
            const float dt = 1f / 120f;
            const float halfSize = 1.15f; // Combined half-width/depth of the saved player and row cubes.
            float x = 0;
            float velocity = 0;
            float rowZ = halfSize;
            for (float elapsed = 0; elapsed < RunnerRules.RowSpacing / speed; elapsed += dt)
            {
                float nextX = Mathf.SmoothDamp(x, 2.6f, ref velocity, 0.085f, 18f, dt);
                float nextZ = rowZ - speed * dt;
                Assert.That(RunnerRules.SweptHit(new Vector2(x, 0), new Vector2(nextX, 0),
                    new Vector2(2.6f, rowZ), new Vector2(2.6f, nextZ), Vector2.one * halfSize), Is.False,
                    "Steering must not hit the neighboring cube in the row just collected.");
                Assert.That(RunnerRules.SweptHit(new Vector2(x, 0), new Vector2(nextX, 0),
                    new Vector2(0, rowZ + RunnerRules.RowSpacing), new Vector2(0, nextZ + RunnerRules.RowSpacing),
                    Vector2.one * halfSize), Is.False, "The next matching lane must be reachable before its row arrives.");
                x = nextX;
                rowZ = nextZ;
            }
            Assert.That(x, Is.GreaterThan(2.6f - halfSize));
        }

        [TestCase(30f)]
        [TestCase(42f)]
        public void StaggeredHazardsStillAllowAnAdjacentLaneChange(float speed)
        {
            const float dt = 1f / 120f;
            const float halfSize = 1.15f;
            float x = 0, velocity = 0, rowZ = halfSize;
            for (float elapsed = 0; elapsed < RunnerRules.RowSpacing / speed; elapsed += dt)
            {
                float nextX = Mathf.SmoothDamp(x, 2.6f, ref velocity, 0.085f, 18f, dt);
                float nextZ = rowZ - speed * dt;
                Assert.That(RunnerRules.SweptHit(new Vector2(x, 0), new Vector2(nextX, 0),
                    new Vector2(2.6f, rowZ), new Vector2(2.6f, nextZ), Vector2.one * halfSize), Is.False);
                Assert.That(RunnerRules.SweptHit(new Vector2(x, 0), new Vector2(nextX, 0),
                    new Vector2(0, rowZ + RunnerRules.RowSpacing - RunnerRules.HazardLeadDistance),
                    new Vector2(0, nextZ + RunnerRules.RowSpacing - RunnerRules.HazardLeadDistance),
                    Vector2.one * halfSize), Is.False, "The next row's early hazard must allow time to leave the old lane.");
                x = nextX; rowZ = nextZ;
            }
        }

        [Test]
        public void HazardColorAssignmentKeepsOtherColorsDistinctFromTheMatchingRoute()
        {
            for (int player = 0; player < 3; player++)
            {
                System.Random random = new System.Random(42);
                int previous = 1;
                for (int row = 0; row < 10000; row++)
                {
                    int lane = RunnerRules.NextSafeLane(random, previous);
                    bool swap = random.Next(2) == 0;
                    int mask = 0;
                    for (int i = 0; i < 3; i++) mask |= 1 << (int)RunnerRules.ColorForLane(i, lane, (CubeColor)player, swap);
                    Assert.That(mask, Is.EqualTo(7));
                    Assert.That(RunnerRules.ColorForLane(lane, lane, (CubeColor)player, swap), Is.EqualTo((CubeColor)player));
                    Assert.That(Mathf.Abs(lane - previous), Is.LessThanOrEqualTo(1));
                    previous = lane;
                }
            }
        }

        [Test]
        public void GeneratedRowsAlwaysHaveAReachableSafeLane()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                System.Random random = new System.Random(seed);
                int previous = 1;
                int previousMask = 0;
                for (int row = 0; row < 1000; row++)
                {
                    int safe = RunnerRules.NextSafeLane(random, previous);
                    int mask = RunnerRules.BlockedLanes(random, safe, row / 999f, previousMask);
                    Assert.That(safe, Is.InRange(0, 2));
                    Assert.That(Mathf.Abs(safe - previous), Is.LessThanOrEqualTo(1));
                    Assert.That(mask & (1 << safe), Is.Zero);
                    Assert.That(mask, Is.InRange(0, 6));
                    for (int oldLane = 0; oldLane < 3; oldLane++)
                    {
                        if ((previousMask & (1 << oldLane)) != 0) continue;
                        bool reachable = false;
                        for (int lane = 0; lane < 3; lane++)
                            reachable |= (mask & (1 << lane)) == 0 && Mathf.Abs(oldLane - lane) <= 1;
                        Assert.That(reachable, Is.True, "Every previously open lane must retain a reachable option.");
                    }
                    int rewards = RunnerRules.CollectibleLanes(random, mask, safe);
                    Assert.That(rewards & mask, Is.Zero);
                    Assert.That(rewards & (1 << safe), Is.Not.Zero);
                    previous = safe;
                    previousMask = mask;
                }
            }
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void SameSeedProducesTheSameTrack(float difficulty)
        {
            System.Random first = new System.Random(42);
            System.Random second = new System.Random(42);
            int lane = 1;
            int previousMask = 0;
            for (int row = 0; row < 1000; row++)
            {
                int next = RunnerRules.NextSafeLane(first, lane, difficulty);
                Assert.That(RunnerRules.NextSafeLane(second, lane, difficulty), Is.EqualTo(next));
                int mask = RunnerRules.BlockedLanes(first, next, difficulty, previousMask);
                Assert.That(RunnerRules.BlockedLanes(second, next, difficulty, previousMask), Is.EqualTo(mask));
                Assert.That(RunnerRules.CollectibleLanes(second, mask, next),
                    Is.EqualTo(RunnerRules.CollectibleLanes(first, mask, next)));
                Assert.That(RunnerRules.PickLane(second, 7 & ~mask), Is.EqualTo(RunnerRules.PickLane(first, 7 & ~mask)));
                lane = next;
                previousMask = mask;
            }
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void PatternsMostlyOfferMultipleOpenLanesAndNeverRepeatNarrowBlocks(float difficulty)
        {
            var random = new System.Random(42);
            int[] counts = new int[3];
            int previousMask = 0, previousCount = 0, lane = 1, rewardChoices = 0;
            for (int row = 0; row < 10000; row++)
            {
                lane = RunnerRules.NextSafeLane(random, lane, difficulty);
                int mask = RunnerRules.BlockedLanes(random, lane, difficulty, previousMask);
                int blocked = 0;
                for (int i = 0; i < 3; i++) if ((mask & (1 << i)) != 0) blocked++;
                counts[blocked]++;
                Assert.That(blocked == 2 && previousCount == 2, Is.False);
                int rewards = RunnerRules.CollectibleLanes(random, mask, lane);
                if ((rewards & (rewards - 1)) != 0) rewardChoices++;
                previousMask = mask; previousCount = blocked;
            }
            Assert.That(counts[0], Is.InRange(900, 2800), "Open stretches provide breathing room.");
            Assert.That(counts[1], Is.GreaterThan(6000), "Single-lane obstacles are the main pattern.");
            Assert.That(counts[2], Is.InRange(10, 2000), "Narrow sections are occasional, never the default.");
            Assert.That(counts[0] + counts[1], Is.GreaterThan(8000));
            Assert.That(rewardChoices, Is.GreaterThan(3000), "Players can score along alternative routes.");
        }

        [Test]
        public void LaneSelectionNeverChoosesABlockedLane()
        {
            var random = new System.Random(42);
            for (int mask = 1; mask < 8; mask++)
                for (int i = 0; i < 100; i++)
                    Assert.That(mask & (1 << RunnerRules.PickLane(random, mask)), Is.Not.Zero);
            Assert.Throws<System.ArgumentException>(() => RunnerRules.PickLane(random, 0));
        }

        [Test]
        public void SweepDetectsObstaclePassingEntirePlayerInOneFrame()
        {
            Assert.That(RunnerRules.SweptHit(Vector2.zero, Vector2.zero,
                new Vector2(0, 10), new Vector2(0, -10)), Is.True);
        }

        [Test]
        public void SweepDoesNotHitAnAdjacentLane()
        {
            Assert.That(RunnerRules.SweptHit(Vector2.zero, Vector2.zero,
                new Vector2(2.6f, 10), new Vector2(2.6f, -10)), Is.False);
        }

        [Test]
        public void SweepDetectsSidewaysCollisionDuringLaneChange()
        {
            Assert.That(RunnerRules.SweptHit(Vector2.zero, new Vector2(2.6f, 0),
                new Vector2(2.6f, 0), new Vector2(2.6f, 0)), Is.True);
        }

        [Test]
        public void SweepDoesNotHitObstacleBehindPlayer()
        {
            Assert.That(RunnerRules.SweptHit(Vector2.zero, Vector2.zero,
                new Vector2(0, -3), new Vector2(0, -7)), Is.False);
        }
    }
}
