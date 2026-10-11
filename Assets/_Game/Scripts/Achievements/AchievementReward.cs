using System;
using UnityEngine;

/// <summary>
/// Награда за достижение. Настраивается в инспекторе самого ассета
/// достижения, а не хардкодится в менеджере: обработчик умеет выдать
/// любой поддерживаемый тип и знать не знает, за что именно её дают.
///
/// Ссылки на ассеты (weapon/clothing/ability) приоритетнее строчного
/// идентификатора customId — так в инспекторе удобнее, а строки остаются
/// запасным вариантом для процедурно созданных достижений.
/// </summary>
[Serializable]
public class AchievementReward
{
    public AchievementRewardType type = AchievementRewardType.Currency;

    [Tooltip("Сколько монет выдать (тип Currency).")]
    public int currencyAmount = 100;

    [Tooltip("Оружие, которое открывается (тип WeaponUnlock).")]
    public WeaponData weapon;

    [Tooltip("Одежда, которая открывается (тип ClothingUnlock).")]
    public ClothingData clothing;

    [Tooltip("Способность, которая открывается (тип AbilityUnlock).")]
    public AbilityData ability;

    [Tooltip("Модификатор забега, который открывается (тип RunModifier).")]
    public RunModifier modifier = RunModifier.None;

    [Tooltip("Строковый идентификатор предмета, если ссылка на ассет не задана.")]
    public string customId = string.Empty;

    public string ResolveWeaponName() =>
        weapon != null ? weapon.WeaponName : customId;

    public string ResolveClothingName() =>
        clothing != null ? clothing.ClothingName : customId;

    public string ResolveAbilityName() =>
        ability != null ? ability.AbilityName : customId;

    /// <summary>Короткая человекочитаемая подпись награды для UI.</summary>
    public string GetLabel()
    {
        switch (type)
        {
            case AchievementRewardType.Currency:
                return Lang.GetOr("ach.reward.coins", "Монеты") + " x" + currencyAmount;

            case AchievementRewardType.WeaponUnlock:
                string weaponName = weapon != null
                    ? weapon.LocalizedName
                    : customId;
                return Lang.GetOr("ach.reward.weapon", "Оружие") + ": " + weaponName;

            case AchievementRewardType.ClothingUnlock:
                string clothingName = clothing != null
                    ? clothing.LocalizedName
                    : customId;
                return Lang.GetOr("ach.reward.clothing", "Одежда") + ": " + clothingName;

            case AchievementRewardType.AbilityUnlock:
                string abilityName = ability != null
                    ? ability.LocalizedName
                    : customId;
                return Lang.GetOr("ach.reward.ability", "Способность") + ": " + abilityName;

            case AchievementRewardType.RunModifier:
                return Lang.GetOr("ach.reward.modifier", "Модификатор")
                    + ": " + RunModifierUnlocks.GetDisplayName(modifier);
        }

        return string.Empty;
    }
}

/// <summary>
/// Постоянный набор открытых «ачивочных» модификаторов забега.
/// Хранится отдельно от обычного пула RunModifierManager: базовые темы
/// доступны всегда, а эти включаются только после получения достижения.
/// </summary>
public static class RunModifierUnlocks
{
    private const string PrefsKey = "ArcadeSurvivor.AchievementRunModifiers";

    /// <summary>Модификаторы, которые выдаются только достижениями.</summary>
    public static readonly RunModifier[] AchievementModifiers =
    {
        RunModifier.Overcharge,
        RunModifier.GlassCannon
    };

    public static bool IsAchievementModifier(RunModifier modifier)
    {
        for (int i = 0; i < AchievementModifiers.Length; i++)
        {
            if (AchievementModifiers[i] == modifier)
                return true;
        }

        return false;
    }

    public static bool IsUnlocked(RunModifier modifier)
    {
        if (modifier == RunModifier.None)
            return false;

        int mask = PlayerPrefs.GetInt(PrefsKey, 0);
        return (mask & (1 << (int)modifier)) != 0;
    }

    /// <summary>Открывает модификатор. Идемпотентно: повторный вызов ничего не меняет.</summary>
    public static void Unlock(RunModifier modifier)
    {
        if (modifier == RunModifier.None)
            return;

        int mask = PlayerPrefs.GetInt(PrefsKey, 0);
        int bit = 1 << (int)modifier;

        if ((mask & bit) != 0)
            return;

        mask |= bit;

        PlayerPrefs.SetInt(PrefsKey, mask);
        PlayerPrefs.Save();
    }

    public static string GetDisplayName(RunModifier modifier)
    {
        switch (modifier)
        {
            case RunModifier.Overcharge:
                return Lang.GetOr("run.overcharge", "Перегрузка");

            case RunModifier.GlassCannon:
                return Lang.GetOr("run.glass_cannon", "Стеклянная пушка");

            default:
                return modifier.ToString();
        }
    }
}
