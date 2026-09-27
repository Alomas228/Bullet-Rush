using UnityEngine;

/// <summary>
/// Что за предмет показывает карточка снаряжения.
///
/// WeaponData, AbilityData и ClothingData — несвязанные
/// ScriptableObject без общего базового типа, поэтому для отображения
/// они сводятся в одну плоскую структуру. Исходный ассет хранится
/// здесь же: без него нельзя перечитать состояние предмета после
/// покупки, а оно меняется на каждом Refresh.
///
/// Сами строки для отрисовки (Name, Stats, Accent) берутся из ассета
/// один раз — они не меняются, а вот Unlocked / Owned / Equipped
/// нужно обновлять каждый раз.
/// </summary>
public class EquipmentCardData
{
    public string Name;
    public string TypeLabel;
    public string RarityLabel;
    public string Stats;
    public string State;

    public int Price;
    public int UnlockLevel;

    public Color Accent;

    public bool Unlocked;
    public bool Owned;
    public bool Equipped;

    /// <summary>
    /// Способности можно снимать и надевать повторно, поэтому
    /// у них кнопка активна и в состоянии «снаряжено».
    /// </summary>
    public bool Toggles;

    public WeaponData Weapon;
    public AbilityData Ability;
    public ClothingData Clothing;

    /// <summary>
    /// Перечитывает владение и выбор из EquipmentManager. Без
    /// этого после покупки карточка продолжала бы показывать
    /// старое состояние.
    /// </summary>
    public void SyncState()
    {
        if (Weapon != null)
        {
            Unlocked = EquipmentManager.IsUnlocked(Weapon);
            Owned = EquipmentManager.IsOwned(Weapon);
            Equipped = EquipmentManager.IsEquipped(Weapon);

            return;
        }

        if (Ability != null)
        {
            Unlocked = EquipmentManager.IsUnlocked(Ability);
            Owned = EquipmentManager.IsOwned(Ability);
            Equipped = EquipmentManager.IsEquipped(Ability);

            return;
        }

        if (Clothing == null)
            return;

        Unlocked = EquipmentManager.IsUnlocked(Clothing);
        Owned = EquipmentManager.IsOwned(Clothing);
        Equipped = EquipmentManager.IsEquipped(Clothing);
    }
}
