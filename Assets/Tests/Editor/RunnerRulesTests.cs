using NUnit.Framework;
using UnityEngine;

namespace CubeDash.Tests
{
    public sealed class RunnerRulesTests
    {
        [Test]
        public void ColorRowsContainOneOfEachColorAndAReachableMatchingRoute()
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
                for (int row = 0; row < 1000; row++)
                {
                    int safe = RunnerRules.NextSafeLane(random, previous);
                    int mask = RunnerRules.BlockedLanes(random, safe, row / 999f);
                    Assert.That(safe, Is.InRange(0, 2));
                    Assert.That(Mathf.Abs(safe - previous), Is.LessThanOrEqualTo(1));
                    Assert.That(mask & (1 << safe), Is.Zero);
                    Assert.That(mask, Is.InRange(1, 6));
                    previous = safe;
                }
            }
        }

        [Test]
        public void SameSeedProducesTheSameTrack()
        {
            System.Random first = new System.Random(42);
            System.Random second = new System.Random(42);
            int lane = 1;
            for (int row = 0; row < 1000; row++)
            {
                int next = RunnerRules.NextSafeLane(first, lane);
                Assert.That(RunnerRules.NextSafeLane(second, lane), Is.EqualTo(next));
                Assert.That(RunnerRules.BlockedLanes(second, next, 0.5f),
                    Is.EqualTo(RunnerRules.BlockedLanes(first, next, 0.5f)));
                lane = next;
            }
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
