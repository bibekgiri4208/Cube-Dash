using System;
using System.Reflection;
using UnityEngine;

namespace CubeDash.Tests
{
    internal sealed class EconomyTestScope : IDisposable
    {
        private readonly string[] keys = { GameEconomy.CoinsKey, GameEconomy.SupercarOwnedKey, GameEconomy.EquippedKey,
            GameEconomy.MonsterTruckOwnedKey, GameEconomy.MilitaryVanOwnedKey };
        private readonly bool[] existed = new bool[5];
        private readonly int[] values = new int[5];
        public EconomyTestScope()
        {
            for (int i = 0; i < keys.Length; i++) { existed[i] = PlayerPrefs.HasKey(keys[i]); values[i] = PlayerPrefs.GetInt(keys[i]); PlayerPrefs.DeleteKey(keys[i]); }
            GameEconomy.EquipSupercar(false);
        }
        public static void Equip(CubeDashGame game, bool equipped = true)
        {
            if (!GameEconomy.OwnsSupercar) { GameEconomy.AddCoins(100); GameEconomy.TryBuySupercar(); }
            GameEconomy.EquipSupercar(equipped);
            // Programmatic simulations disable Update/OnEnable; explicitly refresh their saved selection.
            typeof(CubeDashGame).GetMethod("RefreshEquippedSkin", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
        }
        public void Dispose()
        {
            for (int i = 0; i < keys.Length; i++)
                if (existed[i]) PlayerPrefs.SetInt(keys[i], values[i]); else PlayerPrefs.DeleteKey(keys[i]);
            PlayerPrefs.Save();
        }
    }
}
