using UnityEngine;

/// <summary>
/// Источник урона по врагу. Прокидывается через Enemy.TakeDamage,
/// чтобы смертельные убийства можно было честно приписать конкретному
/// эффекту (огню, взрыву, цепной молнии), а не просто «игроку».
///
/// Раньше система урона была безымянной: TakeDamage(float, bool) и всё.
/// Поле нужно именно для достижений вида «убито огнём» и «одной цепной
/// реакцией», где важно не кто ударил вообще, а что именно нанесло
/// последний (смертельный) удар.
/// </summary>
public enum DamageSource
{
    Unknown = 0,
    Weapon = 1,
    Burn = 2,
    Bleed = 3,
    Explosion = 4,
    ChainLightning = 5,
    Ability = 6,
    Environment = 7
}

/// <summary>
/// Раздатчик уникальных номеров «событий урона». Один взрыв или одна
/// цепная молния получают один номер и передают его всем своим целям,
/// поэтому убийства от одного события можно сгруппировать, а разные
/// события никогда не сливаются вместе.
/// </summary>
public static class DamageEventId
{
    private static int counter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        counter = 0;
    }

    /// <summary>Новый номер события. Никогда не возвращает 0 (Unknown).</summary>
    public static int Next()
    {
        counter++;

        if (counter == 0)
            counter = 1;

        return counter;
    }
}
