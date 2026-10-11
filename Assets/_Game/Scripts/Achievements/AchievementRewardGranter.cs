using UnityEngine;

/// <summary>
/// Выдаёт награды за достижения, не зная, за какое достижение их дают.
/// Достаточно передать AchievementReward — он сам разберётся по типу.
/// Это и есть та самая обработка новых типов без правок логики
/// разблокировки: добавил тип в enum — добавил case здесь.
/// </summary>
public static class AchievementRewardGranter
{
    public static void Grant(AchievementReward reward)
    {
        if (reward == null)
            return;

        switch (reward.type)
        {
            case AchievementRewardType.Currency:
                GrantCurrency(reward.currencyAmount);
                break;

            case AchievementRewardType.WeaponUnlock:
                EquipmentManager.GrantWeapon(reward.ResolveWeaponName());
                break;

            case AchievementRewardType.ClothingUnlock:
                EquipmentManager.GrantClothing(reward.ResolveClothingName());
                break;

            case AchievementRewardType.AbilityUnlock:
                EquipmentManager.GrantAbility(reward.ResolveAbilityName());
                break;

            case AchievementRewardType.RunModifier:
                RunModifierUnlocks.Unlock(reward.modifier);
                break;

            default:
                Debug.LogWarning($"[Achievements] Неизвестный тип награды: {reward.type}.");
                break;
        }
    }

    private static void GrantCurrency(int amount)
    {
        if (amount <= 0)
            return;

        XpManager xp = XpManager.Instance;

        if (xp == null)
        {
            Debug.LogWarning("[Achievements] Награда монетами: XpManager.Instance is null.");
            return;
        }

        xp.AddGlobalCoins(amount);
    }
}
