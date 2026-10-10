using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Выдаёт забегу одну тему (RunModifier) и отдаёт её эффекты всем,
/// кто спрашивает: врагам, спавнеру, счёту, игроку.
///
/// Живёт на объекте сцены и создаётся лениво (EnsureExists), поэтому
/// при перезагрузке сцены (рестарт, выход в меню) он уничтожается
/// вместе со сценой и следующий забег получает новую тему. Ролл
/// вызывается из WaveManager.BeginRun — в тот момент, когда забег
/// действительно начинается.
///
/// Все эффекты по умолчанию нейтральны (множитель 1), поэтому
/// потребители могут спрашивать менеджер, не проверяя, активен ли он.
/// </summary>
public class RunModifierManager : MonoBehaviour
{
    [Header("Pool")]
    [Tooltip("Какие модификаторы могут выпасть. По умолчанию — все. Убери флаг, чтобы исключить тему из забегов.")]
    [SerializeField] private RunModifier pool = RunModifier.All;

    [Header("Berserk")]
    [SerializeField] private float berserkEnemySpeed = 1.25f;
    [SerializeField] private float berserkEnemyDamage = 1.2f;
    [SerializeField] private float berserkScore = 1.3f;

    [Header("Horde")]
    [SerializeField] private float hordeEnemyCount = 1.4f;
    [SerializeField] private float hordeEnemyHealth = 0.75f;

    [Header("Bulwark")]
    [SerializeField] private float bulwarkEnemyHealth = 1.35f;
    [SerializeField] private float bulwarkEnemySpeed = 0.9f;
    [Range(0f, 1f)]
    [SerializeField] private float bulwarkSpawnBiasChance = 0.4f;

    [Header("Blitz")]
    [SerializeField] private float blitzEnemySpeed = 1.15f;
    [SerializeField] private float blitzEnemyHealth = 0.85f;
    [Range(0f, 1f)]
    [SerializeField] private float blitzSpawnBiasChance = 0.45f;

    [Header("Marksmen")]
    [SerializeField] private float marksmenEnemyDamage = 1.15f;
    [SerializeField] private float marksmenEnemyHealth = 0.9f;
    [Range(0f, 1f)]
    [SerializeField] private float marksmenSpawnBiasChance = 0.4f;

    [Header("Iron Will")]
    [Tooltip("Прибавка к урону игрока (0.3 — +30%).")]
    [SerializeField] private float ironWillPlayerDamage = 0.3f;
    [Tooltip("Множитель максимального здоровья игрока (0.75 — минус четверть).")]
    [SerializeField] private float ironWillPlayerHealth = 0.75f;

    public static RunModifierManager Instance { get; private set; }

    /// <summary>
    /// Ручной выбор темы из главного меню. Если false — тема роллится
    /// случайно (поведение по умолчанию), если true — берётся ManualModifier.
    /// Статика переживает перезагрузку сцены, чтобы выбор из меню дожил до
    /// старта забега.
    /// </summary>
    public static bool ManualSelection;

    /// <summary>
    /// Тема, выбранная вручную. RunModifier.None = СТАНДАРТ (без темы).
    /// Используется только когда ManualSelection == true.
    /// </summary>
    public static RunModifier ManualModifier = RunModifier.None;

    public RunModifier Active { get; private set; } = RunModifier.None;

    public bool HasAny =>
        Active != RunModifier.None;

    private static readonly RunModifier[] Catalogue =
    {
        RunModifier.Berserk,
        RunModifier.Horde,
        RunModifier.Bulwark,
        RunModifier.Blitz,
        RunModifier.Marksmen,
        RunModifier.IronWill
    };

    private static readonly List<RunModifier> candidates =
        new List<RunModifier>(Catalogue.Length);

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Находит менеджер в сцене или создаёт его. Нужен тому, кто
    /// начинает забег, чтобы не требовать ручной расстановки объекта.
    /// </summary>
    public static RunModifierManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        RunModifierManager existing =
            FindAnyObjectByType<RunModifierManager>();

        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = new GameObject("RunModifierManager");

        return host.AddComponent<RunModifierManager>();
    }

    /// <summary>Сброс темы забега (выход в меню).</summary>
    public void ResetRun()
    {
        Active = RunModifier.None;
    }

    /// <summary>
    /// Ролл темы забега и применение баффов игрока. Вызывается один
    /// раз в начале забега.
    /// </summary>
    public void RollForRun()
    {
        Active = ManualSelection ? ManualModifier : PickModifier();

        ApplyPlayerBuffs();

        Debug.Log($"RUN MODIFIER: {Active}");
    }

    private RunModifier PickModifier()
    {
        candidates.Clear();

        for (int i = 0; i < Catalogue.Length; i++)
        {
            if ((pool & Catalogue[i]) != RunModifier.None)
                candidates.Add(Catalogue[i]);
        }

        if (candidates.Count == 0)
            return RunModifier.None;

        return candidates[Random.Range(0, candidates.Count)];
    }

    private void ApplyPlayerBuffs()
    {
        if (PlayerDamageBonus > 0f)
        {
            PlayerStats stats = FindAnyObjectByType<PlayerStats>();

            if (stats != null)
                stats.AddDamagePercent(PlayerDamageBonus);
        }

        if (!Mathf.Approximately(PlayerMaxHealthMultiplier, 1f))
        {
            PlayerHealth health = FindAnyObjectByType<PlayerHealth>();

            if (health != null)
                health.ScaleMaxHealth(PlayerMaxHealthMultiplier);
        }
    }

    // =========================================================
    // EFFECT QUERIES
    // =========================================================

    public float EnemySpeedMultiplier
    {
        get
        {
            float value = 1f;

            if (Active.Has(RunModifier.Berserk))
                value *= berserkEnemySpeed;

            if (Active.Has(RunModifier.Bulwark))
                value *= bulwarkEnemySpeed;

            if (Active.Has(RunModifier.Blitz))
                value *= blitzEnemySpeed;

            return value;
        }
    }

    public float EnemyHealthMultiplier
    {
        get
        {
            float value = 1f;

            if (Active.Has(RunModifier.Horde))
                value *= hordeEnemyHealth;

            if (Active.Has(RunModifier.Bulwark))
                value *= bulwarkEnemyHealth;

            if (Active.Has(RunModifier.Blitz))
                value *= blitzEnemyHealth;

            if (Active.Has(RunModifier.Marksmen))
                value *= marksmenEnemyHealth;

            return value;
        }
    }

    public float EnemyDamageMultiplier
    {
        get
        {
            float value = 1f;

            if (Active.Has(RunModifier.Berserk))
                value *= berserkEnemyDamage;

            if (Active.Has(RunModifier.Marksmen))
                value *= marksmenEnemyDamage;

            return value;
        }
    }

    public float EnemyCountMultiplier
    {
        get
        {
            return Active.Has(RunModifier.Horde)
                ? hordeEnemyCount
                : 1f;
        }
    }

    public float ScoreMultiplier
    {
        get
        {
            return Active.Has(RunModifier.Berserk)
                ? berserkScore
                : 1f;
        }
    }

    public float PlayerDamageBonus
    {
        get
        {
            return Active.Has(RunModifier.IronWill)
                ? ironWillPlayerDamage
                : 0f;
        }
    }

    public float PlayerMaxHealthMultiplier
    {
        get
        {
            return Active.Has(RunModifier.IronWill)
                ? ironWillPlayerHealth
                : 1f;
        }
    }

    public RunSpawnBias SpawnBias
    {
        get
        {
            if (Active.Has(RunModifier.Bulwark))
                return RunSpawnBias.Tank;

            if (Active.Has(RunModifier.Blitz))
                return RunSpawnBias.Fast;

            if (Active.Has(RunModifier.Marksmen))
                return RunSpawnBias.Ranged;

            return RunSpawnBias.None;
        }
    }

    public float SpawnBiasChance
    {
        get
        {
            switch (SpawnBias)
            {
                case RunSpawnBias.Tank:
                    return bulwarkSpawnBiasChance;

                case RunSpawnBias.Fast:
                    return blitzSpawnBiasChance;

                case RunSpawnBias.Ranged:
                    return marksmenSpawnBiasChance;

                default:
                    return 0f;
            }
        }
    }

    /// <summary>Короткая подпись темы для баннера волны.</summary>
    public string ShortName
    {
        get
        {
            if (Active.Has(RunModifier.Berserk))
                return Lang.Get("run.berserk");

            if (Active.Has(RunModifier.Horde))
                return Lang.Get("run.horde");

            if (Active.Has(RunModifier.Bulwark))
                return Lang.Get("run.bulwark");

            if (Active.Has(RunModifier.Blitz))
                return Lang.Get("run.blitz");

            if (Active.Has(RunModifier.Marksmen))
                return Lang.Get("run.marksmen");

            if (Active.Has(RunModifier.IronWill))
                return Lang.Get("run.iron_will");

            return string.Empty;
        }
    }

    // =========================================================
    // STATIC ACCESSORS (identity when no run is active)
    // =========================================================

    public static float EnemySpeedScale =>
        Instance != null ? Instance.EnemySpeedMultiplier : 1f;

    public static float EnemyHealthScale =>
        Instance != null ? Instance.EnemyHealthMultiplier : 1f;

    public static float EnemyDamageScale =>
        Instance != null ? Instance.EnemyDamageMultiplier : 1f;

    public static float EnemyCountScale =>
        Instance != null ? Instance.EnemyCountMultiplier : 1f;

    public static float ScoreScale =>
        Instance != null ? Instance.ScoreMultiplier : 1f;

    public static RunSpawnBias SpawnBiasType =>
        Instance != null ? Instance.SpawnBias : RunSpawnBias.None;

    public static float SpawnBiasProbability =>
        Instance != null ? Instance.SpawnBiasChance : 0f;
}
