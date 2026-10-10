using System;

// Модификатор забега — тема, которая держится весь забег, а не
// отдельную волну. Волновые акценты (WaveModifier) меняют одну волну;
// здесь меняется весь забег: враги быстрее, их больше, они живучее,
// или игрок получает риск-награду. Ни новых врагов, ни новых систем —
// только множители поверх уже существующих механик.
[Flags]
public enum RunModifier
{
    None = 0,

    // Враги заметно быстрее и больнее, но за них платят больше очков.
    Berserk = 1 << 0,

    // Врагов становится больше, но каждый слабее.
    Horde = 1 << 1,

    // Перевес танков: живучие, но медленные.
    Bulwark = 1 << 2,

    // Перевес быстрых: волны бегунов.
    Blitz = 1 << 3,

    // Перевес дальников: держать дистанцию труднее.
    Marksmen = 1 << 4,

    // Риск-награда самому игроку: больше урона, но меньше здоровья.
    IronWill = 1 << 5,

    All =
        Berserk |
        Horde |
        Bulwark |
        Blitz |
        Marksmen |
        IronWill
}

// Какой тип врага модификатор забега подмешивает чаще обычного.
public enum RunSpawnBias
{
    None,
    Fast,
    Ranged,
    Tank
}

public static class RunModifierExtensions
{
    public static bool Has(
        this RunModifier modifier,
        RunModifier flag)
    {
        return (modifier & flag) != RunModifier.None;
    }
}
