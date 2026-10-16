using System;
using UnityEngine;

namespace CubeDash
{
    // Saved numeric IDs: never reorder or reuse them (1 is the original Supercar selection).
    public enum PlayerSkin { Cube = 0, Supercar = 1, MonsterTruck = 2, MilitaryVan = 3 }

    /// <summary>Local saved wallet and skin ownership. Physical coins, not multiplied score, fund purchases.</summary>
    public static class GameEconomy
    {
        public const string CoinsKey = "CubeDash.Coins";
        public const string SupercarOwnedKey = "CubeDash.Skins.Supercar.Owned";
        public const string MonsterTruckOwnedKey = "CubeDash.Skins.MonsterTruck.Owned";
        public const string MilitaryVanOwnedKey = "CubeDash.Skins.MilitaryVan.Owned";
        public const string EquippedKey = "CubeDash.Skins.Equipped";
        public const int SkinPrice = 100;
        public const int SupercarPrice = SkinPrice;
        public static event Action Changed;
        public static int Coins => Mathf.Max(0, PlayerPrefs.GetInt(CoinsKey, 0));
        public static bool OwnsSupercar => PlayerPrefs.GetInt(SupercarOwnedKey, 0) == 1;
        public static bool SupercarEquipped => EquippedSkin == PlayerSkin.Supercar;
        public static PlayerSkin EquippedSkin
        {
            get
            {
                var skin = (PlayerSkin)PlayerPrefs.GetInt(EquippedKey, 0);
                return Owns(skin) ? skin : PlayerSkin.Cube;
            }
        }

        public static string SkinName(PlayerSkin skin) => skin == PlayerSkin.Supercar ? "NEON SUPERCAR"
            : skin == PlayerSkin.MonsterTruck ? "MONSTER TRUCK" : skin == PlayerSkin.MilitaryVan ? "MILITARY VAN" : "CUBE";

        private static string OwnershipKey(PlayerSkin skin) => skin == PlayerSkin.Supercar ? SupercarOwnedKey
            : skin == PlayerSkin.MonsterTruck ? MonsterTruckOwnedKey : skin == PlayerSkin.MilitaryVan ? MilitaryVanOwnedKey : null;

        public static bool Owns(PlayerSkin skin) => skin == PlayerSkin.Cube ||
            (OwnershipKey(skin) is string key && PlayerPrefs.GetInt(key, 0) == 1);

        public static void AddCoins(int count)
        {
            if (count <= 0) return;
            PlayerPrefs.SetInt(CoinsKey, (int)Math.Min(int.MaxValue, (long)Coins + count));
            Save();
        }

        public static bool TryBuySupercar() => TryBuy(PlayerSkin.Supercar);

        public static bool TryBuy(PlayerSkin skin)
        {
            string key = OwnershipKey(skin);
            if (key == null || Owns(skin) || Coins < SkinPrice) return false;
            PlayerPrefs.SetInt(CoinsKey, Coins - SkinPrice);
            PlayerPrefs.SetInt(key, 1);
            Save();
            return true;
        }

        public static bool EquipSupercar(bool equipped) => Equip(equipped ? PlayerSkin.Supercar : PlayerSkin.Cube);

        public static bool Equip(PlayerSkin skin)
        {
            if (!Owns(skin)) return false;
            PlayerPrefs.SetInt(EquippedKey, (int)skin);
            Save();
            return true;
        }

        private static void Save() { PlayerPrefs.Save(); Changed?.Invoke(); }
    }
}
