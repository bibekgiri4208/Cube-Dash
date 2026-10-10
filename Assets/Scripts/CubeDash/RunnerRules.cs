using System;
using UnityEngine;

namespace CubeDash
{
    /// <summary>Pure generation and collision rules, shared by the game and editor tests.</summary>
    public static class RunnerRules
    {
        public const int LaneCount = 3;
        public const float SegmentLength = 42f;
        public const float RowSpacing = 14f;
        public const float HazardLeadDistance = 3f;
        public const float PowerUpRowOffset = RowSpacing * 0.5f;
        public const float ObstacleWidth = 1.65f;
        public const float ObstacleDepth = 1.65f;
        public const float PlayerSize = 0.9f;

        public static float DifficultyForScore(int score, int scoreForMaximumDifficulty)
        {
            return Mathf.Clamp01(score / (float)Math.Max(1, scoreForMaximumDifficulty));
        }

        public static int NextSafeLane(System.Random random, int previousLane, float difficulty = 0f)
        {
            // A guaranteed path never requires crossing two lanes between adjacent rows.
            // Keep some straight stretches even at high scores, without teleporting the safe
            // route from one outside lane to the other.
            float stayChance = Mathf.Lerp(0.5f, 0.2f, Mathf.Clamp01(difficulty));
            if (random.NextDouble() < stayChance) return previousLane;
            if (previousLane == 0 || previousLane == 2) return 1;
            return random.Next(2) == 0 ? 0 : 2;
        }

        public static int BlockedLanes(System.Random random, int safeLane, float difficulty, int previousMask = 0)
        {
            float ramp = Mathf.Clamp01(difficulty);
            double roll = random.NextDouble();
            float openChance = Mathf.Lerp(0.24f, 0.12f, ramp);
            if (roll < openChance) return 0;
            int mask = 7 & ~(1 << safeLane);
            bool previousWasNarrow = (previousMask & (previousMask - 1)) != 0;
            // A narrowing must be reachable from every previously open lane, not just the reward route.
            bool reachable = safeLane == 1 || ((7 & ~previousMask) & (1 << (2 - safeLane))) == 0;
            if (!previousWasNarrow && reachable && roll < openChance + Mathf.Lerp(0.08f, 0.28f, ramp)) return mask;
            return 1 << PickLane(random, mask);
        }

        public static int CollectibleLanes(System.Random random, int blockedMask, int matchingLane)
        {
            int mask = 1 << matchingLane;
            for (int lane = 0; lane < LaneCount; lane++)
                if (lane != matchingLane && (blockedMask & (1 << lane)) == 0 && random.NextDouble() < 0.5f)
                    mask |= 1 << lane;
            return mask;
        }

        public static int PickLane(System.Random random, int mask)
        {
            mask &= 7;
            if (mask == 0) throw new ArgumentException("At least one lane must be available.", nameof(mask));
            int count = 0;
            for (int lane = 0; lane < LaneCount; lane++) if ((mask & (1 << lane)) != 0) count++;
            int choice = random.Next(count);
            for (int lane = 0; lane < LaneCount; lane++)
                if ((mask & (1 << lane)) != 0 && choice-- == 0) return lane;
            return 1;
        }

        public static CubeColor ColorForLane(int lane, int matchingLane, CubeColor playerColor, bool swapOthers)
        {
            if (lane == matchingLane) return playerColor;
            int other = lane < matchingLane ? lane : lane - 1;
            if (swapOthers) other = 1 - other;
            return (CubeColor)(((int)playerColor + 1 + other) % LaneCount);
        }

        public static bool SweptRange(Vector2 playerFrom, Vector2 playerTo, Vector2 coinFrom, Vector2 coinTo,
            float range, out float enter, out float exit)
        {
            Vector2 from = coinFrom - playerFrom;
            Vector2 delta = (coinTo - playerTo) - from;
            float a = delta.sqrMagnitude;
            float c = from.sqrMagnitude - range * range;
            enter = 0; exit = 1;
            if (a < 0.00001f) return c <= 0;
            float b = 2 * Vector2.Dot(from, delta);
            float discriminant = b * b - 4 * a * c;
            if (discriminant < 0) return false;
            float root = Mathf.Sqrt(discriminant);
            enter = Mathf.Max(0, (-b - root) / (2 * a));
            exit = Mathf.Min(1, (-b + root) / (2 * a));
            return enter <= exit;
        }

        public static bool SweptHit(Vector2 playerFrom, Vector2 playerTo,
            Vector2 obstacleFrom, Vector2 obstacleTo)
        {
            return SweptHit(playerFrom, playerTo, obstacleFrom, obstacleTo, new Vector2(
                (ObstacleWidth + PlayerSize) * 0.5f,
                (ObstacleDepth + PlayerSize) * 0.5f));
        }

        public static bool SweptHit(Vector2 playerFrom, Vector2 playerTo,
            Vector2 obstacleFrom, Vector2 obstacleTo, Vector2 halfSize)
        {
            return SweptHit(playerFrom, playerTo, obstacleFrom, obstacleTo, halfSize, out _);
        }

        public static bool SweptHit(Vector2 playerFrom, Vector2 playerTo,
            Vector2 obstacleFrom, Vector2 obstacleTo, Vector2 halfSize, out float hitTime)
            => SweptHit(playerFrom, playerTo, obstacleFrom, obstacleTo, halfSize, out hitTime, out _);

        public static bool SweptHit(Vector2 playerFrom, Vector2 playerTo,
            Vector2 obstacleFrom, Vector2 obstacleTo, Vector2 halfSize, out float enter, out float exit)
        {
            // Sweep relative motion through the Minkowski-expanded obstacle. This also
            // detects a hit when a frame moves an obstacle completely past the cube.
            Vector2 from = playerFrom - obstacleFrom;
            Vector2 delta = (playerTo - obstacleTo) - from;
            enter = 0f;
            exit = 1f;
            for (int axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(delta[axis]) < 0.00001f)
                {
                    if (Mathf.Abs(from[axis]) > halfSize[axis]) return false;
                    continue;
                }
                float first = (-halfSize[axis] - from[axis]) / delta[axis];
                float last = (halfSize[axis] - from[axis]) / delta[axis];
                if (first > last) { float swap = first; first = last; last = swap; }
                enter = Mathf.Max(enter, first);
                exit = Mathf.Min(exit, last);
                if (enter > exit) return false;
            }
            return true;
        }
    }
}
