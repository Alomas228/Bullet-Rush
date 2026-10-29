/// <summary>
/// Уникальная боевая механика локации.
///
/// Помимо вида окружения и раскладки арены (ArenaLayout) каждая карта
/// накладывает свой отпечаток на бой: состав волн, поведение врагов,
/// дары или кары игроку. Так поздние карты отличаются не только
/// картинкой, но и задачей.
///
/// Значения enum сериализуются в ассеты GameMap напрямую —
/// не переставляй существующие числа, только добавляй в конец.
/// </summary>
public enum MapMechanic
{
    /// <summary>Базовая арена без особых правил.</summary>
    None = 0,

    /// <summary>Открытая местность: мало укрытий, дальние враги, лёгкая прибавка к урону.</summary>
    OpenField = 1,

    /// <summary>Высота и укрытия: медленные, но более живучие враги.</summary>
    Highland = 2,

    /// <summary>Городские коридоры: плотнее поток и засады дальников.</summary>
    Urban = 3,

    /// <summary>Песчаная буря: периодические зоны урона, вязкий песок замедляет всех.</summary>
    Sandstorm = 4,

    /// <summary>Низкая гравитация: всё движется быстрее, периодические метеориты.</summary>
    LowGravity = 5
}

/// <summary>
/// Локализованные названия и подписи механик карт.
/// Ключи совпадают во всех наборах переводов (ru/tr/en).
/// </summary>
public static class MapMechanicText
{
    /// <summary>Короткое имя стиля боя для карточки карты.</summary>
    public static string Name(MapMechanic mechanic)
    {
        string key = KeyOf(mechanic);

        return key == null ? string.Empty : Lang.Get(key + ".name");
    }

    /// <summary>Описание стиля боя, включая нейтральную лесную арену.</summary>
    public static string Description(MapMechanic mechanic)
    {
        string key = KeyOf(mechanic);

        return key == null ? string.Empty : Lang.Get(key + ".desc");
    }

    private static string KeyOf(MapMechanic mechanic)
    {
        switch (mechanic)
        {
            case MapMechanic.None: return "mech.forest";
            case MapMechanic.OpenField: return "mech.openfield";
            case MapMechanic.Highland: return "mech.highland";
            case MapMechanic.Urban: return "mech.urban";
            case MapMechanic.Sandstorm: return "mech.sandstorm";
            case MapMechanic.LowGravity: return "mech.lowgravity";
            default: return null;
        }
    }
}
