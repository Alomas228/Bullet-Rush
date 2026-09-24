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

    private int comboCount;
    private float comboTimer;
    private float lastKillTime;

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
        lastKillTime = Time.time;

        OnComboChanged?.Invoke(comboCount);

        // Проверяем мульти-килл (несколько убийств за короткое время)
        if (Time.time - lastKillTime < multiKillWindow && comboCount > 1)
        {
            // Это будет обработано в RunMetrics
        }

        // Проверяем макс. комбо
        if (comboCount >= 50 && comboCount == 50)
            OnMaxComboReached?.Invoke(50);
        else if (comboCount >= 30 && comboCount == 30)
            OnMaxComboReached?.Invoke(30);
        else if (comboCount >= 20 && comboCount == 20)
            OnMaxComboReached?.Invoke(20);
        else if (comboCount >= 10 && comboCount == 10)
            OnMaxComboReached?.Invoke(10);
    }

    /// <summary>
    /// Вызывается при получении урона игроком.
    /// </summary>
    public void OnPlayerHit()
    {
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
    }

    public void ResetAll()
    {
        comboCount = 0;
        comboTimer = 0f;
        lastKillTime = 0f;
    }
}
