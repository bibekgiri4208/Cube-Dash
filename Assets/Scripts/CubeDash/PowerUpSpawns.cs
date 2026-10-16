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
        public const float FirstShieldDistance = 900f;
        public const float MinimumShieldGap = 1800f;
        // Vehicle weights are combined: neither Truck nor Flight can bypass the other's cooldown.
        // Shield is rarer than either vehicle and has its own, longer cooldown.
        private static readonly int[] Weights = { 2, 32, 8, 8, 24 };
        private readonly System.Random random;
        private float nextBonus = FirstBonusDistance;
        private float nextVehicle = FirstVehicleDistance;
        private float nextShield = FirstShieldDistance;

        public PowerUpSpawns(int seed) { random = new System.Random(seed ^ 0x51A7); }
        public int PickLane(int openMask) => RunnerRules.PickLane(random, openMask);

        public bool TryChoose(float distance, float chance, int availableTypes, out PowerUpType type)
        {
            type = PowerUpType.Shield;
            int count = Math.Min(Weights.Length, availableTypes);
            if (count <= 0 || distance < nextBonus || chance <= 0 || random.NextDouble() >= Mathf.Clamp01(chance)) return false;
            int total = 0;
            for (int i = 0; i < count; i++)
                if (Available((PowerUpType)i, distance)) total += Weights[i];
            if (total == 0) return false;
            int choice = random.Next(total);
            for (int i = 0; i < count; i++)
            {
                if (!Available((PowerUpType)i, distance)) continue;
                choice -= Weights[i];
                if (choice >= 0) continue;
                type = (PowerUpType)i;
                nextBonus = distance + MinimumBonusGap;
                if (IsVehicle(type)) nextVehicle = distance + MinimumVehicleGap;
                if (type == PowerUpType.Shield) nextShield = distance + MinimumShieldGap;
                return true;
            }
            return false;
        }

        private bool Available(PowerUpType type, float distance)
            => (!IsVehicle(type) || distance >= nextVehicle) && (type != PowerUpType.Shield || distance >= nextShield);

        public static bool IsVehicle(PowerUpType type) => type == PowerUpType.Truck || type == PowerUpType.FighterPlane;
    }
}
