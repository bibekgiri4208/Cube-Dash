using System;
using UnityEngine;

namespace CubeDash
{
    /// <summary>Local saved wallet and skin ownership. Physical coins, not multiplied score, fund purchases.</summary>
    public static class GameEconomy
    {
        public const string CoinsKey = "CubeDash.Coins";
        public const string SupercarOwnedKey = "CubeDash.Skins.Supercar.Owned";
        public const string EquippedKey = "CubeDash.Skins.Equipped";
        public const int SupercarPrice = 100;
        public static event Action Changed;
        public static int Coins => Mathf.Max(0, PlayerPrefs.GetInt(CoinsKey, 0));
        public static bool OwnsSupercar => PlayerPrefs.GetInt(SupercarOwnedKey, 0) == 1;
        public static bool SupercarEquipped => OwnsSupercar && PlayerPrefs.GetInt(EquippedKey, 0) == 1;

        public static void AddCoins(int count)
        {
            if (count <= 0) return;
            PlayerPrefs.SetInt(CoinsKey, (int)Math.Min(int.MaxValue, (long)Coins + count));
            Save();
        }

        public static bool TryBuySupercar()
        {
            if (OwnsSupercar || Coins < SupercarPrice) return false;
            PlayerPrefs.SetInt(CoinsKey, Coins - SupercarPrice);
            PlayerPrefs.SetInt(SupercarOwnedKey, 1);
            Save();
            return true;
        }

        public static bool EquipSupercar(bool equipped)
        {
            if (equipped && !OwnsSupercar) return false;
            PlayerPrefs.SetInt(EquippedKey, equipped ? 1 : 0);
            Save();
            return true;
        }

        private static void Save() { PlayerPrefs.Save(); Changed?.Invoke(); }
    }
}
