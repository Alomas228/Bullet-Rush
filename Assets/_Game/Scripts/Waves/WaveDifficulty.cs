using UnityEngine;

/// <summary>
/// Уровень волны: лёгкая или сложная. Волны чередуются по индексу —
/// первая лёгкая, вторая сложная, третья снова лёгкая.
/// </summary>
public enum WaveTier
{
    Easy,
    Hard
}

/// <summary>
/// Ритм сложности: волны идут парами «лёгкая → сложная».
///
/// Зачем это нужно. Раньше каждая волна была одинаково «средней», и
/// апгрейд, взятый после волны, не читался: игрок брал карту, но следующая
/// волна была ровно такой же, и казалось, что ничего не изменилось. Теперь
/// после сложной волны игрок получает улучшение, следующая волна приходит
/// лёгкой — и он видит, что враги стали слабее относительно него, то есть
/// что он действительно прокачался. Потом снова сложная, и цикл повторяется.
///
/// Баланс при этом не меняется. Все множители подобраны так, что среднее по
/// паре равно единице (0.76 + 1.24 = 2, 0.74 + 1.26 = 2, 0.90 + 1.10 = 2),
/// поэтому общая кривая роста сложности — она остаётся в ассетах врагов и в
/// сжатии размера волны — сохраняется, а появляется только размах
/// примерно вдвое между соседними волнами.
///
/// Класс статический и чистый: номер волны на входе, множители на выходе.
/// Им пользуются и WaveManager (размер волны), и Enemy (HP и урон), и
/// WaveEventDirector (урон зон), поэтому номер волны — единственное, что им
/// нужно согласовать.
/// </summary>
public static class WaveDifficulty
{
    // Множители «лёгкой» и «сложной» волны.
    //
    // Урон получает более широкий размах, чем здоровье: смерть от снаряда
    // читается хуже, чем долгий бой с толстым мобом. Поэтому на лёгкой
    // волне урон проседает сильнее, а на сложной — уходит выше единицы.
    private const float EasyHealthMultiplier = 0.76f;
    private const float HardHealthMultiplier = 1.24f;

    private const float EasyDamageMultiplier = 0.74f;
    private const float HardDamageMultiplier = 1.26f;

    // Размер волны берёт поменьше: количество врагов влияет и на длину
    // волны, и на число отвлекающих целей, поэтому трогать его сильнее
    // здоровья нельзя — иначе «лёгкая» волна стала бы просто короче по
    // времени, а не легче по ощущению.
    private const float EasyCountMultiplier = 0.90f;
    private const float HardCountMultiplier = 1.10f;

    /// <summary>
    /// Уровень волны. Нечётные волны (1, 3, 5, …) — лёгкие, чётные —
    /// сложные. Номер меньше единицы считается первой волной, а не
    /// «сложной нулевой».
    /// </summary>
    public static WaveTier GetTier(int wave)
    {
        int index = Mathf.Max(wave, 1);

        return index % 2 == 1
            ? WaveTier.Easy
            : WaveTier.Hard;
    }

    /// <summary>
    /// Уровень волны с поправкой на босса. Босс-волна всегда сложная, даже
    /// если её номер выпал нечётным. При интервале 10 это и так совпадает с
    /// чётными волнами, но полагаться на совпадение нельзя: смена интервала
    /// тихонько сделала бы босса «лёгкой волной» с самым высоким уроном на
    /// арене.
    /// </summary>
    public static WaveTier GetTier(int wave, bool isBossWave)
    {
        return isBossWave
            ? WaveTier.Hard
            : GetTier(wave);
    }

    /// <summary>
    /// Множитель здоровья врага на этой волне.
    /// </summary>
    public static float GetHealthMultiplier(
        int wave,
        bool isBossWave = false)
    {
        return GetTier(wave, isBossWave) == WaveTier.Easy
            ? EasyHealthMultiplier
            : HardHealthMultiplier;
    }

    /// <summary>
    /// Множитель урона врага на этой волне.
    /// </summary>
    public static float GetDamageMultiplier(
        int wave,
        bool isBossWave = false)
    {
        return GetTier(wave, isBossWave) == WaveTier.Easy
            ? EasyDamageMultiplier
            : HardDamageMultiplier;
    }

    /// <summary>
    /// Множитель количества врагов на этой волне. Босс-волне не нужен: её
    /// состав задаёт сам спавнер босса, а не счётчик волны.
    /// </summary>
    public static float GetCountMultiplier(int wave)
    {
        return GetTier(wave) == WaveTier.Easy
            ? EasyCountMultiplier
            : HardCountMultiplier;
    }
}