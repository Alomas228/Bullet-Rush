using UnityEngine;

/// <summary>
/// ScriptableObject с настройками всех бонусов.
/// Создаётся один файл в Assets, потом просто меняешь числа.
/// </summary>
[CreateAssetMenu(fileName = "BonusSettings", menuName = "Bullet Rush/Bonus Settings")]
public class BonusSettings : ScriptableObject
{
    [Header("Движения")]
    [Tooltip("Бонус за рывок (dash dodge)")]
    public int dashDodgeBonus = 50;

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

    [Header("Скорость")]
    [Tooltip("Бонус за быстрый забег (< 3 мин)")]
    public int speedBonusFast = 2000;
    public float speedThresholdFast = 180f; // 3 минуты

    [Tooltip("Бонус за средний забег (< 5 мин)")]
    public int speedBonusMedium = 1000;
    public float speedThresholdMedium = 300f; // 5 минут

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
