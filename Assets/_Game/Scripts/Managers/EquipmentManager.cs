using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Персистентное снаряжение игрока: какие оружия куплены и какое
/// снаряжено, какие способности куплены и какие снаряжены, какая одежда
/// куплена и какая надета. Хранится в PlayerPrefs (как SettingsManager),
/// не требует объекта в сцене.
///
/// Правила:
///  - Предмет с ценой &lt;= 0 считается всегда купленным (стартовый набор).
///  - Купить можно только если уровень игрока &gt;= UnlockLevel.
///  - Снарядить можно только купленный предмет.
///  - Боевых способностей/одежды может быть снаряжено несколько, оружие
///    и одежда — штучно (одно снаряжённое).
/// </summary>
public static class EquipmentManager
{
    private const string PrefsKey = "ArcadeSurvivor.Equipment";

    [System.Serializable]
    private class EquipmentData
    {
        public List<string> ownedWeapons = new List<string>();
        public string equippedWeapon = string.Empty;
        public List<string> ownedAbilities = new List<string>();
        public List<string> equippedAbilities = new List<string>();
        public List<string> ownedClothing = new List<string>();
        public string equippedClothing = string.Empty;
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

    /// <summary>Имена снаряжённых способностей (могут быть обе — бомба и щит).</summary>
    public static IReadOnlyList<string> EquippedAbilityNames
    {
        get
        {
            EnsureLoaded();

            return data.equippedAbilities;
        }
    }

    /// <summary>Имя надетой одежды (пустая строка, если ещё не выбрана).</summary>
    public static string EquippedClothingName
    {
        get
        {
            EnsureLoaded();

            return data.equippedClothing;
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

        return IsLevelReached(weapon.UnlockLevel);
    }

    /// <summary>Уровень игрока достаточен для покупки/показа способности.</summary>
    public static bool IsUnlocked(AbilityData ability)
    {
        if (ability == null)
            return false;

        return IsLevelReached(ability.UnlockLevel);
    }

    /// <summary>Уровень игрока достаточен для покупки/показа одежды.</summary>
    public static bool IsUnlocked(ClothingData clothing)
    {
        if (clothing == null)
            return false;

        return IsLevelReached(clothing.UnlockLevel);
    }

    /// <summary>Оружие куплено (бесплатные считаются купленными всегда).</summary>
    public static bool IsOwned(WeaponData weapon)
    {
        if (weapon == null)
            return false;

        EnsureLoaded();

        return IsOwnedName(
            weapon.WeaponName,
            weapon.Price,
            data.ownedWeapons
        );
    }

    /// <summary>Способность куплена (бесплатные считаются купленными всегда).</summary>
    public static bool IsOwned(AbilityData ability)
    {
        if (ability == null)
            return false;

        EnsureLoaded();

        return IsOwnedName(
            ability.AbilityName,
            ability.Price,
            data.ownedAbilities
        );
    }

    /// <summary>Одежда куплена (бесплатные считаются купленными всегда).</summary>
    public static bool IsOwned(ClothingData clothing)
    {
        if (clothing == null)
            return false;

        EnsureLoaded();

        return IsOwnedName(
            clothing.ClothingName,
            clothing.Price,
            data.ownedClothing
        );
    }

    /// <summary>Способность сейчас снаряжена для забега.</summary>
    public static bool IsEquipped(AbilityData ability)
    {
        if (ability == null)
            return false;

        EnsureLoaded();

        return data.equippedAbilities.Contains(
            ability.AbilityName
        );
    }

    /// <summary>Одежда сейчас надета для забега.</summary>
    public static bool IsEquipped(ClothingData clothing)
    {
        if (clothing == null)
            return false;

        return string.Equals(
            EquippedClothingName,
            clothing.ClothingName,
            System.StringComparison.Ordinal
        );
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

        EnsureLoaded();

        return TryPurchaseCore(
            weapon.WeaponName,
            weapon.Price,
            weapon.UnlockLevel,
            data.ownedWeapons,
            "weapon"
        );
    }

    /// <summary>
    /// Покупка способности: проверяет уровень игрока, текущее владение и баланс монет.
    /// При успехе списывает монеты и сохраняет покупку.
    /// </summary>
    public static bool TryPurchase(AbilityData ability)
    {
        if (ability == null)
            return false;

        EnsureLoaded();

        return TryPurchaseCore(
            ability.AbilityName,
            ability.Price,
            ability.UnlockLevel,
            data.ownedAbilities,
            "ability"
        );
    }

    /// <summary>
    /// Покупка одежды: проверяет уровень игрока, текущее владение и баланс монет.
    /// При успехе списывает монеты и сохраняет покупку.
    /// </summary>
    public static bool TryPurchase(ClothingData clothing)
    {
        if (clothing == null)
            return false;

        EnsureLoaded();

        return TryPurchaseCore(
            clothing.ClothingName,
            clothing.Price,
            clothing.UnlockLevel,
            data.ownedClothing,
            "clothing"
        );
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

    /// <summary>
    /// Переключает снаряжение купленной способности (можно несколько сразу).
    /// Если способность уже снаряжена — снимает её, иначе снаряжает.
    /// </summary>
    public static bool TryToggleAbility(AbilityData ability)
    {
        if (ability == null)
            return false;

        if (!IsOwned(ability))
        {
            Debug.LogWarning(
                $"[Equipment] {ability.AbilityName}: ещё не куплено."
            );

            return false;
        }

        EnsureLoaded();

        if (data.equippedAbilities.Contains(ability.AbilityName))
        {
            data.equippedAbilities.Remove(ability.AbilityName);

            Save();

            Debug.Log(
                $"[Equipment] Снято: {ability.AbilityName}."
            );
        }
        else
        {
            if (!data.equippedAbilities.Contains(ability.AbilityName))
                data.equippedAbilities.Add(ability.AbilityName);

            Save();

            Debug.Log(
                $"[Equipment] Снаряжено: {ability.AbilityName}."
            );
        }

        return true;
    }

    /// <summary>Надевает купленную одежду (одна за раз).</summary>
    public static bool TryEquip(ClothingData clothing)
    {
        if (clothing == null)
            return false;

        if (!IsOwned(clothing))
        {
            Debug.LogWarning(
                $"[Equipment] {clothing.ClothingName}: ещё не куплено."
            );

            return false;
        }

        EnsureLoaded();

        data.equippedClothing = clothing.ClothingName;

        Save();

        Debug.Log(
            $"[Equipment] Надето: {clothing.ClothingName}."
        );

        return true;
    }

    // =========================================================
    // SHARED HELPERS
    // =========================================================

    private static bool IsLevelReached(int unlockLevel)
    {
        XpManager xp = XpManager.Instance;

        int playerLevel =
            xp != null
                ? xp.GetPlayerLevel()
                : 1;

        return playerLevel >= unlockLevel;
    }

    private static bool IsOwnedName(
        string itemName,
        int price,
        List<string> ownedList)
    {
        if (string.IsNullOrEmpty(itemName))
            return false;

        if (price <= 0)
            return true;

        return ownedList.Contains(itemName);
    }

    private static bool TryPurchaseCore(
        string itemName,
        int price,
        int unlockLevel,
        List<string> ownedList,
        string kindLabel)
    {
        if (string.IsNullOrEmpty(itemName))
            return false;

        if (!IsLevelReached(unlockLevel))
        {
            Debug.LogWarning(
                $"[Equipment] {itemName}: уровень {unlockLevel} " +
                "не достигнут."
            );

            return false;
        }

        if (IsOwnedName(itemName, price, ownedList))
        {
            Debug.LogWarning(
                $"[Equipment] {itemName}: уже куплено."
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

        if (xp.GlobalCoins < price)
        {
            Debug.LogWarning(
                $"[Equipment] {itemName}: " +
                $"нужно {price}, есть {xp.GlobalCoins}."
            );

            return false;
        }

        if (!xp.TrySpendCoins(price))
            return false;

        if (!ownedList.Contains(itemName))
            ownedList.Add(itemName);

        Save();

        Debug.Log(
            $"[Equipment] Куплено: {itemName} " +
            $"({kindLabel}) за {price} монет."
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

        Normalize(data);
    }

    /// <summary>
    /// Старые сохранения не содержат новых списков — приводим их к
    /// рабочему виду, чтобы не падать на null-перечислениях.
    /// </summary>
    private static void Normalize(EquipmentData target)
    {
        if (target.ownedWeapons == null)
            target.ownedWeapons = new List<string>();

        if (target.ownedAbilities == null)
            target.ownedAbilities = new List<string>();

        if (target.equippedAbilities == null)
            target.equippedAbilities = new List<string>();

        if (target.ownedClothing == null)
            target.ownedClothing = new List<string>();

        if (target.equippedWeapon == null)
            target.equippedWeapon = string.Empty;

        if (target.equippedClothing == null)
            target.equippedClothing = string.Empty;
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