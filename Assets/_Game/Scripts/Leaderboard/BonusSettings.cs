using UnityEngine;

/// <summary>
/// ScriptableObject с настройками всех бонусов.
/// Создаётся один файл в Assets, потом просто меняешь числа.
/// </summary>
[CreateAssetMenu(fileName = "BonusSettings", menuName = "Bullet Rush/Bonus Settings")]
public class BonusSettings : ScriptableObject
{
    /// <summary>
    /// Последний загруженный ассет настроек. Позволяет системам без
    /// прямой ссылки (например, ComboSystem) получить единый источник
    /// правил начисления очков без ручного назначения в инспекторе.
    /// </summary>
    public static BonusSettings Instance { get; private set; }

    private void OnEnable()
    {
        Instance = this;
    }

    [Header("Движения")]
    [Tooltip("Бонус за рывок (dash dodge)")]
    public int dashDodgeBonus = 20;

    [Tooltip("Бонус за использование способности")]
    public int abilityUseBonus = 25;

    [Header("Комбо")]
    [Tooltip("Порог комбо 10x и бонус")]
    public int comboThreshold10 = 10;
    public int comboBonus10 = 100;

    [Tooltip("Порог комбо 20x и бонус")]
    public int comboThreshold20 = 20;
    public int comboBonus20 = 250;

    [Tooltip("Порог комбо 30x и бонус")]
    public int comboThreshold30 = 30;
    public int comboBonus30 = 500;

    [Tooltip("Порог комбо 50x и бонус")]
    public int comboThreshold50 = 50;
    public int comboBonus50 = 1000;

    [Header("Идеальные волны")]
    [Tooltip("Бонус за идеальную волну (без урона)")]
    public int perfectWaveBonus = 500;

    [Header("Глубина забега")]
    [Tooltip("Бонус за каждую пройденную волну. Забег бесконечный, поэтому " +
             "итог награждает глубину, а не скорость завершения.")]
    public int waveBonusPerWave = 200;

    [Tooltip("Дополнительный бонус за достижение глубоких волн.")]
    public int deepWaveThreshold = 20;
    public int deepWaveBonus = 3000;

    [Header("Эффективность")]
    [Tooltip("Бонус за высокий урон / низкий полученный")]
    public int efficiencyBonusHigh = 1500;
    public float efficiencyThresholdHigh = 10f; // 10x

    [Tooltip("Бонус за средний урон / полученный")]
    public int efficiencyBonusMedium = 750;
    public float efficiencyThresholdMedium = 5f; // 5x

    [Header("Мульти-килл")]
    [Tooltip("Бонус за серию быстрых убийств (2+ за 3 сек)")]
    public int multiKillBonus = 100;
}
