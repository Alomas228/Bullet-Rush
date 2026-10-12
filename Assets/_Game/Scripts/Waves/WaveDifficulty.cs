using UnityEngine;

/// <summary>
/// Уровень волны: лёгкая или сложная.
/// </summary>
public enum WaveTier
{
    Easy,
    Hard
}

/// <summary>
/// Ритм сложности: первые три волны идут по плавной кривой (мягкий разгон),
/// затем волны чередуются парами «лёгкая → сложная».
///
/// Зачем это нужно. Раньше каждая волна была одинаково «средней», и
/// апгрейд, взятый после волны, не читался: игрок брал карту, но следующая
/// волна была ровно такой же, и казалось, что ничего не изменилось. Теперь
/// после сложной волны игрок получает улучшение, следующая волна приходит
/// лёгкой — и он видит, что враги стали слабее относительно него, то есть
/// что он действительно прокачался. Потом снова сложная, и цикл повторяется.
///
/// Кривая роста сложности остаётся в ассетах врагов и в сжатии размера
/// волны; здесь задан только размах примерно вдвое между соседними волнами,
/// а первые волны сделаны мягче, чтобы новичок не умирал на входе.
///
/// Босс-волна НЕ считается «сложной»: у неё нейтральные множители (1.0), и
/// весь вес определяет собственный тюнинг босса. Иначе босс, попадая на
/// чётную волну, получал ещё и двойной размах сложности и убивал мгновенно.
///
/// Класс статический и чистый: номер волны на входе, множители на выходе.
/// Им пользуются и WaveManager (размер волны), и Enemy (HP и урон), и
/// WaveEventDirector (урон зон), поэтому номер волны — единственное, что им
/// нужно согласовать.
/// </summary>
public static class WaveDifficulty
{
    // Волны 1..3 — мягкий разгон и всегда уровень Easy.
    private const int OpeningEasyWaves = 3;

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

    // Босс-волна — отдельная сцена, а не скачок сложности: множители
    // нейтральные (среднее между Easy и Hard), тюнинг босса делает остальное.
    private const float BossHealthMultiplier = 1f;
    private const float BossDamageMultiplier = 1f;

    // Дополнительное облегчение первых двух волн поверх уровня Easy —
    // чтобы старт был совсем спокойным и игрок успел освоиться.
    private const float OpeningWave1Health = 0.70f;
    private const float OpeningWave1Damage = 0.68f;
    private const float OpeningWave2Health = 0.73f;
    private const float OpeningWave2Damage = 0.71f;

    /// <summary>
    /// Уровень волны. Волны 1..3 — лёгкие (мягкий разгон). Начиная с
    /// четвёртой нечётные волны (5, 7, …) — лёгкие, чётные (4, 6, …) —
    /// сложные. Номер меньше единицы считается первой волной.
    /// </summary>
    public static WaveTier GetTier(int wave)
    {
        int index = Mathf.Max(wave, 1);

        if (index <= OpeningEasyWaves)
            return WaveTier.Easy;

        return index % 2 == 1
            ? WaveTier.Easy
            : WaveTier.Hard;
    }

    /// <summary>
    /// Уровень волны с поправкой на босса. Босс больше не форсируется в
    /// «сложные»: он идёт по обычному ритму, а нейтральные множители в
    /// методах ниже снимают скачок. Параметр оставлен для совместимости
    /// вызовов.
    /// </summary>
    public static WaveTier GetTier(int wave, bool isBossWave)
    {
        return GetTier(wave);
    }

    /// <summary>
    /// Множитель здоровья врага на этой волне.
    /// </summary>
    public static float GetHealthMultiplier(
        int wave,
        bool isBossWave = false)
    {
        if (isBossWave)
            return BossHealthMultiplier;

        int index = Mathf.Max(wave, 1);

        if (index == 1)
            return OpeningWave1Health;

        if (index == 2)
            return OpeningWave2Health;

        return GetTier(index) == WaveTier.Easy
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
        if (isBossWave)
            return BossDamageMultiplier;

        int index = Mathf.Max(wave, 1);

        if (index == 1)
            return OpeningWave1Damage;

        if (index == 2)
            return OpeningWave2Damage;

        return GetTier(index) == WaveTier.Easy
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
