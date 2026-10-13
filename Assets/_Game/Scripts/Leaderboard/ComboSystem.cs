using UnityEngine;

/// <summary>
/// Убийства без получения урона увеличивают комбо и множитель.
/// Комбо сбрасывается при получении урона.
/// </summary>
public class ComboSystem : MonoBehaviour
{
    [Header("Combo Settings")]
    [SerializeField] private float comboResetTime = 3f;
    [SerializeField] private float multiKillWindow = 3f;

    [Header("Bonuses")]
    [Tooltip("Пороги и размеры бонусов за комбо. Если не назначен — берётся из ScoreManager.")]
    [SerializeField] private BonusSettings bonusSettings;
    [Tooltip("Куда начислять бонусы за пороги. Если не назначен — ищется в сцене.")]
    [SerializeField] private ScoreManager scoreManager;

    private int comboCount;
    private float comboTimer;
    private float lastKillTime;
    private int lastComboThresholdReached = 0; // Для отслеживания порогов бонусов

    public int ComboCount => comboCount;

    public float ComboMultiplier
    {
        get
        {
            if (comboCount >= 50) return 5f;
            if (comboCount >= 30) return 3f;
            if (comboCount >= 20) return 2f;
            if (comboCount >= 10) return 1.5f;
            return 1f;
        }
    }

    public event System.Action<int> OnComboChanged;
    public event System.Action<int> OnMaxComboReached;

    /// <summary>
    /// Несколько убийств подряд за короткий промежуток.
    /// </summary>
    public event System.Action<int> OnMultiKill;

    private void Awake()
    {
        ResolveRefs();
    }

    private void ResolveRefs()
    {
        if (scoreManager == null)
            scoreManager = FindAnyObjectByType<ScoreManager>();

        if (bonusSettings == null && scoreManager != null)
            bonusSettings = scoreManager.Settings;

        if (bonusSettings == null)
            bonusSettings = BonusSettings.Instance;
    }

    private void Update()
    {
        if (comboTimer > 0f)
        {
            comboTimer -= Time.deltaTime;

            if (comboTimer <= 0f)
            {
                ResetCombo();
            }
        }
    }

    /// <summary>
    /// Вызывается при убийстве врага.
    /// </summary>
    public void OnEnemyKilled()
    {
        comboCount++;
        comboTimer = comboResetTime;

        // Мульти-килл считаем по предыдущему убийству: если сначала
        // обновить lastKillTime, разница всегда будет нулевой.
        bool isMultiKill =
            lastKillTime > 0f &&
            comboCount > 1 &&
            Time.time - lastKillTime < multiKillWindow;

        lastKillTime = Time.time;

        OnComboChanged?.Invoke(comboCount);

        if (isMultiKill)
            OnMultiKill?.Invoke(comboCount);

        // Проверяем макс. комбо
        if (comboCount >= 50 && comboCount == 50)
            OnMaxComboReached?.Invoke(50);
        else if (comboCount >= 30 && comboCount == 30)
            OnMaxComboReached?.Invoke(30);
        else if (comboCount >= 20 && comboCount == 20)
            OnMaxComboReached?.Invoke(20);
        else if (comboCount >= 10 && comboCount == 10)
            OnMaxComboReached?.Invoke(10);

        // Бонусы за пройденные пороги комбо выдаёт сама система комбо —
        // это правило начисления очков, а не отрисовка интерфейса.
        CheckThresholds();
    }

    /// <summary>
    /// Вызывается при получении урона игроком.
    /// Сбрасывает порог бонусов!
    /// </summary>
    public void OnPlayerHit()
    {
        lastComboThresholdReached = 0;
        ResetCombo();
    }

    private void ResetCombo()
    {
        if (comboCount > 0)
        {
            comboCount = 0;
            comboTimer = 0f;
            OnComboChanged?.Invoke(0);
        }
    }

    /// <summary>
    /// Вызывается в начале новой волны.
    /// </summary>
    public void OnWaveStarted()
    {
        // Комбо сохраняется между волнами — это стиль!
    }

    /// <summary>
    /// Вызывается при завершении волны без получения урона.
    /// </summary>
    public void OnPerfectWave()
    {
        // Бонус к комбо за perfect wave
        comboCount += 5;
        comboTimer = comboResetTime;
        OnComboChanged?.Invoke(comboCount);

        // Идеальная волна может перескочить через порог — проверяем бонусы.
        CheckThresholds();
    }

    /// <summary>
    /// Начисляет бонус за каждый пройденный порог комбо. Пороги и
    /// размеры берутся из BonusSettings — единого источника правды.
    /// </summary>
    private void CheckThresholds()
    {
        if (scoreManager == null || bonusSettings == null)
            ResolveRefs();

        if (scoreManager == null || bonusSettings == null)
            return;

        if (comboCount >= bonusSettings.comboThreshold50 &&
            lastComboThresholdReached < 50)
        {
            scoreManager.AddBonus(bonusSettings.comboBonus50, Lang.Get("bonus.combo50"));
            lastComboThresholdReached = 50;
        }
        else if (comboCount >= bonusSettings.comboThreshold30 &&
            lastComboThresholdReached < 30)
        {
            scoreManager.AddBonus(bonusSettings.comboBonus30, Lang.Get("bonus.combo30"));
            lastComboThresholdReached = 30;
        }
        else if (comboCount >= bonusSettings.comboThreshold20 &&
            lastComboThresholdReached < 20)
        {
            scoreManager.AddBonus(bonusSettings.comboBonus20, Lang.Get("bonus.combo20"));
            lastComboThresholdReached = 20;
        }
        else if (comboCount >= bonusSettings.comboThreshold10 &&
            lastComboThresholdReached < 10)
        {
            scoreManager.AddBonus(bonusSettings.comboBonus10, Lang.Get("bonus.combo10"));
            lastComboThresholdReached = 10;
        }
    }

    /// <summary>
    /// Получить последний достигнутый порог комбо.
    /// </summary>
    public int GetLastComboThresholdReached()
    {
        return lastComboThresholdReached;
    }

    /// <summary>
    /// Установить последний достигнутый порог комбо.
    /// </summary>
    public void SetLastComboThresholdReached(int threshold)
    {
        lastComboThresholdReached = threshold;
    }

    public void ResetAll()
    {
        comboCount = 0;
        comboTimer = 0f;
        lastKillTime = 0f;
        lastComboThresholdReached = 0;
    }
}
