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
            // Long stationary stretches become rare as the score climbs, without teleporting
            // the safe route from one outside lane to the other.
            float stayChance = Mathf.Lerp(0.35f, 0.05f, Mathf.Clamp01(difficulty));
            if (random.NextDouble() < stayChance) return previousLane;
            if (previousLane == 0 || previousLane == 2) return 1;
            return random.Next(2) == 0 ? 0 : 2;
        }

        public static int BlockedLanes(System.Random random, int safeLane, float difficulty)
        {
            int mask = 7 & ~(1 << safeLane);
            if (random.NextDouble() > Mathf.Lerp(0.2f, 0.8f, Mathf.Clamp01(difficulty)))
            {
                int lane;
                do { lane = random.Next(LaneCount); } while (lane == safeLane);
                mask = 1 << lane;
            }
            return mask;
        }

        public static CubeColor ColorForLane(int lane, int matchingLane, CubeColor playerColor, bool swapOthers)
        {
            if (lane == matchingLane) return playerColor;
            int other = lane < matchingLane ? lane : lane - 1;
            if (swapOthers) other = 1 - other;
            return (CubeColor)(((int)playerColor + 1 + other) % LaneCount);
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
        {
            hitTime = 0;
            // Sweep relative motion through the Minkowski-expanded obstacle. This also
            // detects a hit when a frame moves an obstacle completely past the cube.
            Vector2 from = playerFrom - obstacleFrom;
            Vector2 delta = (playerTo - obstacleTo) - from;
            float enter = 0f;
            float exit = 1f;
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
            hitTime = enter;
            return true;
        }
    }
}
