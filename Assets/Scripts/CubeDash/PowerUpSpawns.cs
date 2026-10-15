using System;
using UnityEngine;

namespace CubeDash
{
    /// <summary>Run-distance scheduling, independent of pool size, frame rate and obstacle RNG.</summary>
    public sealed class PowerUpSpawns
    {
        public const float FirstBonusDistance = 56f;
        public const float MinimumBonusGap = 56f;
        public const float FirstVehicleDistance = 350f;
        public const float MinimumVehicleGap = 750f;
        // Vehicle weights are combined: neither Truck nor Flight can bypass the other's cooldown.
        private static readonly int[] Weights = { 28, 32, 8, 8, 24 };
        private readonly System.Random random;
        private float nextBonus = FirstBonusDistance;
        private float nextVehicle = FirstVehicleDistance;

        public PowerUpSpawns(int seed) { random = new System.Random(seed ^ 0x51A7); }
        public int PickLane(int openMask) => RunnerRules.PickLane(random, openMask);

        public bool TryChoose(float distance, float chance, int availableTypes, out PowerUpType type)
        {
            type = PowerUpType.Shield;
            int count = Math.Min(Weights.Length, availableTypes);
            if (count <= 0 || distance < nextBonus || chance <= 0 || random.NextDouble() >= Mathf.Clamp01(chance)) return false;
            int total = 0;
            for (int i = 0; i < count; i++)
                if (!IsVehicle((PowerUpType)i) || distance >= nextVehicle) total += Weights[i];
            int choice = random.Next(total);
            for (int i = 0; i < count; i++)
            {
                if (IsVehicle((PowerUpType)i) && distance < nextVehicle) continue;
                choice -= Weights[i];
                if (choice >= 0) continue;
                type = (PowerUpType)i;
                nextBonus = distance + MinimumBonusGap;
                if (IsVehicle(type)) nextVehicle = distance + MinimumVehicleGap;
                return true;
            }
            return false;
        }

        public static bool IsVehicle(PowerUpType type) => type == PowerUpType.Truck || type == PowerUpType.FighterPlane;
    }
}
