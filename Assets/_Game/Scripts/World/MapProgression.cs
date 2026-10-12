/// <summary>
/// Доступность карт по уровню игрока.
///
/// Карта открывается, когда глобальный уровень игрока (XpManager)
/// достигает GameMap.UnlockLevel. Лес открыт с первого уровня, дальше
/// карты идут лестницей: пляж → горы → город → пустыня → космос.
///
/// Все проверки идут через один класс, чтобы UI выбора карт,
/// EnvironmentController и редакторские билдеры видели одну правду.
/// </summary>
public static class MapProgression
{
    /// <summary>Текущий глобальный уровень игрока (1, если XpManager ещё нет).</summary>
    public static int PlayerLevel
    {
        get
        {
            XpManager xp = XpManager.Instance;

            return xp != null
                ? xp.GetPlayerLevel()
                : 1;
        }
    }

    /// <summary>Открыта ли карта для текущего игрока.</summary>
    public static bool IsUnlocked(GameMap map)
    {
        return IsUnlocked(map, PlayerLevel);
    }

    /// <summary>Открыта ли карта для заданного уровня.</summary>
    public static bool IsUnlocked(GameMap map, int playerLevel)
    {
        if (map == null)
            return false;

        return playerLevel >= RequiredLevel(map);
    }

    /// <summary>Уровень, с которого открывается карта.</summary>
    public static int RequiredLevel(GameMap map)
    {
        return map != null
            ? map.unlockLevel
            : 1;
    }
}
