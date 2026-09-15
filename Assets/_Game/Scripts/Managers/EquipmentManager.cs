using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Персистентное снаряжение игрока: какие оружия куплены и какое
/// снаряжено. Хранится в PlayerPrefs (как SettingsManager), не требует
/// объекта в сцене.
///
/// Правила:
///  - Оружие с ценой &lt;= 0 считается всегда купленным (стартовый набор).
///  - Купить можно только если уровень игрока &gt;= UnlockLevel.
///  - Снарядить можно только купленное оружие.
/// </summary>
public static class EquipmentManager
{
    private const string PrefsKey = "ArcadeSurvivor.Equipment";

    [System.Serializable]
    private class EquipmentData
    {
        public List<string> ownedWeapons = new List<string>();
        public string equippedWeapon = string.Empty;
    }

    private static EquipmentData data;
    private static bool loaded;

    // =========================================================
    // STATE
    // =========================================================

    /// <summary>Имя снаряжённого оружия (пустая строка, если ещё не выбрано).</summary>
    public static string EquippedWeaponName
    {
        get
        {
            EnsureLoaded();

            return data.equippedWeapon;
        }
    }

    /// <summary>Купленные имена оружий (без учёта бесплатных).</summary>
    public static IReadOnlyList<string> OwnedWeapons
    {
        get
        {
            EnsureLoaded();

            return data.ownedWeapons;
        }
    }

    // =========================================================
    // QUERIES
    // =========================================================

    /// <summary>Уровень игрока достаточен для покупки/показа оружия.</summary>
    public static bool IsUnlocked(WeaponData weapon)
    {
        if (weapon == null)
            return false;

        XpManager xp = XpManager.Instance;

        int playerLevel =
            xp != null
                ? xp.GetPlayerLevel()
                : 1;

        return playerLevel >= weapon.UnlockLevel;
    }

    /// <summary>Оружие куплено (бесплатные считаются купленными всегда).</summary>
    public static bool IsOwned(WeaponData weapon)
    {
        if (weapon == null)
            return false;

        if (weapon.Price <= 0)
            return true;

        EnsureLoaded();

        return data.ownedWeapons.Contains(weapon.WeaponName);
    }

    /// <summary>Оружие сейчас снаряжено для забега.</summary>
    public static bool IsEquipped(WeaponData weapon)
    {
        if (weapon == null)
            return false;

        return string.Equals(
            EquippedWeaponName,
            weapon.WeaponName,
            System.StringComparison.Ordinal
        );
    }

    // =========================================================
    // ACTIONS
    // =========================================================

    /// <summary>
    /// Покупка оружия: проверяет уровень игрока, текущее владение и баланс монет.
    /// При успехе списывает монеты и сохраняет покупку.
    /// </summary>
    public static bool TryPurchase(WeaponData weapon)
    {
        if (weapon == null)
            return false;

        if (!IsUnlocked(weapon))
        {
            Debug.LogWarning(
                $"[Equipment] {weapon.WeaponName}: уровень {weapon.UnlockLevel} " +
                $"не достигнут."
            );

            return false;
        }

        if (IsOwned(weapon))
        {
            Debug.LogWarning(
                $"[Equipment] {weapon.WeaponName}: уже куплено."
            );

            return false;
        }

        XpManager xp = XpManager.Instance;

        if (xp == null)
        {
            Debug.LogWarning(
                "[Equipment] XpManager.Instance is null."
            );

            return false;
        }

        if (xp.GlobalCoins < weapon.Price)
        {
            Debug.LogWarning(
                $"[Equipment] {weapon.WeaponName}: " +
                $"нужно {weapon.Price}, есть {xp.GlobalCoins}."
            );

            return false;
        }

        if (!xp.TrySpendCoins(weapon.Price))
            return false;

        EnsureLoaded();

        if (!data.ownedWeapons.Contains(weapon.WeaponName))
            data.ownedWeapons.Add(weapon.WeaponName);

        Save();

        Debug.Log(
            $"[Equipment] Куплено: {weapon.WeaponName} за {weapon.Price} монет."
        );

        return true;
    }

    /// <summary>Снаряжает купленное оружие для следующего забега.</summary>
    public static bool TryEquip(WeaponData weapon)
    {
        if (weapon == null)
            return false;

        if (!IsOwned(weapon))
        {
            Debug.LogWarning(
                $"[Equipment] {weapon.WeaponName}: ещё не куплено."
            );

            return false;
        }

        EnsureLoaded();

        data.equippedWeapon = weapon.WeaponName;

        Save();

        Debug.Log(
            $"[Equipment] Снаряжено: {weapon.WeaponName}."
        );

        return true;
    }

    // =========================================================
    // PERSISTENCE
    // =========================================================

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;

        data = new EquipmentData();

        string json =
            PlayerPrefs.GetString(PrefsKey, string.Empty);

        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            EquipmentData parsed =
                JsonUtility.FromJson<EquipmentData>(json);

            if (parsed != null)
                data = parsed;
        }
        catch
        {
            data = new EquipmentData();
        }
    }

    private static void Save()
    {
        EnsureLoaded();

        PlayerPrefs.SetString(
            PrefsKey,
            JsonUtility.ToJson(data)
        );

        PlayerPrefs.Save();
    }
}