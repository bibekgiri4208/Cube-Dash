using UnityEngine;

namespace CubeDash
{
    public enum HighwayHazard { Cube, Barrier, Van, Truck }
    public enum HighwayEncounter { Warmup, Slalom, Gates, Traffic, Recovery }

    /// <summary>Distance-authored encounters, independent of the number of collected coins.</summary>
    public static class LevelProgression
    {
        public const float MaximumDifficultyDistance = 1500f;
        public static float Difficulty(float distance) => Mathf.Clamp01(distance / MaximumDifficultyDistance);
        public static int Stage(float distance) => distance < 180 ? 1 : distance < 480 ? 2 : distance < 900 ? 3 : distance < 1500 ? 4 : 5;
        public static bool ReachableFromEveryOpenLane(int previousMask, int mask)
        {
            for (int old = 0; old < 3; old++)
            {
                if ((previousMask & (1 << old)) != 0) continue;
                bool reachable = false;
                for (int lane = 0; lane < 3; lane++)
                    reachable |= (mask & (1 << lane)) == 0 && Mathf.Abs(lane - old) <= 1;
                if (!reachable) return false;
            }
            return mask != 7;
        }
    }

    /// <summary>Short, readable phrases with a recovery phrase every fourth encounter.</summary>
    public sealed class HighwayPatterns
    {
        public struct Row
        {
            public int SafeLane, BlockedMask;
            public HighwayHazard Hazard;
            public HighwayEncounter Encounter;
        }
        private readonly System.Random random;
        private int safeLane = 1, previousMask, rowsLeft, encounterIndex, direction = 1, phraseRow;
        private HighwayEncounter encounter;
        private bool firstRow = true;
        public HighwayPatterns(int seed) { random = new System.Random(seed); }

        public Row Next(float distance)
        {
            int stage = LevelProgression.Stage(distance);
            if (rowsLeft == 0)
            {
                encounter = stage == 1 ? HighwayEncounter.Warmup : encounterIndex % 4 == 3 ? HighwayEncounter.Recovery
                    : stage == 2 ? (encounterIndex % 2 == 0 ? HighwayEncounter.Slalom : HighwayEncounter.Gates)
                    : (HighwayEncounter)(1 + random.Next(3));
                rowsLeft = encounter == HighwayEncounter.Recovery ? 3 : 6;
                phraseRow = 0;
                encounterIndex++;
                direction = safeLane == 2 ? -1 : safeLane == 0 ? 1 : random.Next(2) == 0 ? -1 : 1;
            }
            int next = safeLane;
            if (encounter == HighwayEncounter.Slalom || encounter == HighwayEncounter.Gates)
            {
                if (safeLane + direction < 0 || safeLane + direction > 2) direction = -direction;
                if (encounter == HighwayEncounter.Slalom || phraseRow % 2 == 0) next += direction;
            }
            else if (encounter == HighwayEncounter.Traffic && phraseRow % 2 == 0)
                next = RunnerRules.NextSafeLane(random, safeLane, 0.65f);
            else if (encounter == HighwayEncounter.Warmup)
                next = RunnerRules.NextSafeLane(random, safeLane, 0);

            int availableHazards = 7 & ~(1 << next);
            if (firstRow) { next = 1; availableHazards = 5; }
            int mask = 0;
            if (distance >= 35 && encounter != HighwayEncounter.Recovery)
            {
                bool narrow = encounter == HighwayEncounter.Gates || (stage >= 4 && phraseRow % 3 != 2);
                mask = narrow ? availableHazards : 1 << RunnerRules.PickLane(random, availableHazards);
                if (!LevelProgression.ReachableFromEveryOpenLane(previousMask, mask))
                    mask = 1 << RunnerRules.PickLane(random, availableHazards);
                if (stage == 1 && phraseRow % 3 == 2) mask = 0;
            }
            if (firstRow) { mask = 0; firstRow = false; }
            HighwayHazard hazard = stage == 1 ? HighwayHazard.Cube : encounter == HighwayEncounter.Traffic
                ? (phraseRow % 2 == 0 ? HighwayHazard.Van : HighwayHazard.Truck) : HighwayHazard.Barrier;
            safeLane = next;
            previousMask = mask;
            phraseRow++;
            rowsLeft--;
            return new Row { SafeLane = next, BlockedMask = mask, Hazard = hazard, Encounter = encounter };
        }
    }
}
