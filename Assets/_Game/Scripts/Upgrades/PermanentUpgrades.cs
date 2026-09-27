using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Постоянные (персистентные) улучшения игрока: покупаются за монеты
/// в главном меню и действуют в каждом забеге.
///
/// Правила:
///  - у каждого параметра ровно <see cref="MaxLevel"/> уровней;
///  - цена следующего уровня растёт в <see cref="CostMultiplier"/> раз
///    от базовых <see cref="BaseCost"/> монет (округление до
///    <see cref="CostRoundStep"/>), то есть 10 -> 15 -> 20 -> ... -> 150;
///  - бонусы небольшие: полностью прокачанный параметр даёт ощутимый
///    прирост, но не ломает геймплей (скорость бега, например,
///    растёт максимум примерно на треть);
///  - прогресс хранится в PlayerPrefs (как EquipmentManager) и не
///    требует объекта в сцене.
///
/// Применение бонусов на старте забега — PermanentUpgradeApplier.
/// </summary>
public static class PermanentUpgrades
{
    private const string PrefsKey = "ArcadeSurvivor.PermanentUpgrades";

    /// <summary>Сколько уровней у каждого параметра.</summary>
    public const int MaxLevel = 10;

    private const int BaseCost = 10;
    private const float CostMultiplier = 1.35f;
    private const int CostRoundStep = 5;

    /// <summary>
    /// Как бонус одного уровня складывается с бонусами других уровней.
    /// </summary>
    private enum ValueMode
    {
        /// <summary>
        /// Процент, умножаемый на текущее значение каждый уровень
        /// (1 + value)^level — так же считает PlayerStats.
        /// </summary>
        PercentCompound,

        /// <summary>Плоская прибавка за каждый уровень.</summary>
        FlatAdd
    }

    private sealed class Def
    {
        public PermanentUpgradeStat Stat;
        public string Name;
        public string Description;

        /// <summary>Шаблон итогового бонуса, {0} — значение.</summary>
        public string Format;

        public ValueMode Mode;

        /// <summary>Бонус одного уровня (для PercentCompound — доля).</summary>
        public float ValuePerLevel;

        /// <summary>
        /// Бонус приходит в долях единицы (0.05 = 5%), поэтому в
        /// интерфейсе показываем его в процентах. Для «плоских»
        /// величин (HP/с, множитель крита) выключаем.
        /// </summary>
        public bool ValueIsFraction = true;

        public int Decimals;
    }

    [Serializable]
    private sealed class Entry
    {
        public string stat;
        public int level;
    }

    [Serializable]
    private sealed class SaveData
    {
        public List<Entry> entries = new List<Entry>();
    }

    private static readonly PermanentUpgradeStat[] StatOrder =
    {
        PermanentUpgradeStat.Damage,
        PermanentUpgradeStat.FireRate,
        PermanentUpgradeStat.MoveSpeed,
        PermanentUpgradeStat.MaxHealth,
        PermanentUpgradeStat.HealthRegen,
        PermanentUpgradeStat.CriticalChance,
        PermanentUpgradeStat.CriticalDamage,
        PermanentUpgradeStat.ProjectileSpeed,
        PermanentUpgradeStat.DashCooldown,
        PermanentUpgradeStat.AbilityCooldown
    };

    private static readonly Dictionary<PermanentUpgradeStat, Def> Defs =
        BuildDefs();

    private static SaveData data;
    private static bool loaded;

    // =========================================================
    // QUERIES
    // =========================================================

    /// <summary>Все прокачиваемые параметры (в порядке карточек меню).</summary>
    public static IReadOnlyList<PermanentUpgradeStat> Stats => StatOrder;

    /// <summary>Купленный уровень параметра (0 — не прокачивался).</summary>
    public static int GetLevel(PermanentUpgradeStat stat)
    {
        EnsureLoaded();

        Entry entry = FindEntry(stat);

        if (entry == null)
            return 0;

        return Mathf.Clamp(entry.level, 0, MaxLevel);
    }

    /// <summary>Параметр прокачан полностью.</summary>
    public static bool IsMaxLevel(PermanentUpgradeStat stat)
    {
        return GetLevel(stat) >= MaxLevel;
    }

    /// <summary>
    /// Цена следующего уровня параметра (0, если максимум).
    /// Рост: 10, 15, 20, 25, 35, 45, 60, 80, 110, 150.
    /// </summary>
    public static int GetCost(PermanentUpgradeStat stat)
    {
        return GetCostForLevel(GetLevel(stat));
    }

    /// <summary>Цена покупки уровня level (level — уже купленный).</summary>
    public static int GetCostForLevel(int level)
    {
        if (level < 0)
            level = 0;

        if (level >= MaxLevel)
            return 0;

        float raw = BaseCost * Mathf.Pow(CostMultiplier, level);

        int rounded =
            Mathf.RoundToInt(raw / CostRoundStep) * CostRoundStep;

        return Mathf.Max(BaseCost, rounded);
    }

    /// <summary>Суммарная цена прокачки одного параметра от нуля до MaxLevel.</summary>
    public static int GetTotalCost()
    {
        int total = 0;

        for (int level = 0; level < MaxLevel; level++)
            total += GetCostForLevel(level);

        return total;
    }

    public static string GetName(PermanentUpgradeStat stat)
    {
        return GetDef(stat).Name;
    }

    public static string GetDescription(PermanentUpgradeStat stat)
    {
        return GetDef(stat).Description;
    }

    /// <summary>Суммарный бонус параметра в виде готовой строки.</summary>
    public static string FormatTotal(PermanentUpgradeStat stat)
    {
        int level = GetLevel(stat);

        if (level <= 0)
            return "Без бонуса";

        return Format(
            GetDef(stat),
            ToDisplayValue(
                GetDef(stat),
                GetApplyValue(stat, level)
            )
        );
    }

    private static float ToDisplayValue(Def def, float applyValue)
    {
        return def.ValueIsFraction
            ? applyValue * 100f
            : applyValue;
    }

    /// <summary>
    /// Подставляет значение бонуса в шаблон параметра. Знак держит
    /// сам шаблон, поэтому берём модуль (перезарядка — отрицательная).
    /// </summary>
    private static string Format(Def def, float value)
    {
        string number =
            Mathf.Abs(value).ToString(
                NumberFormat(def.Decimals),
                System.Globalization.CultureInfo.InvariantCulture
            );

        return def.Format.Replace("{0}", number);
    }

    private static string NumberFormat(int decimals)
    {
        if (decimals <= 0)
            return "0";

        return "0." + new string('#', decimals);
    }

    /// <summary>Сколько параметров куплено (0..MaxLevel*StatOrder.Length).</summary>
    public static int TotalLevelsBought
    {
        get
        {
            int total = 0;

            for (int i = 0; i < StatOrder.Length; i++)
                total += GetLevel(StatOrder[i]);

            return total;
        }
    }

    /// <summary>Сколько уровней куплено вообще (для заголовка панели).</summary>
    public static int TotalLevelsAvailable =>
        MaxLevel * StatOrder.Length;

    // =========================================================
    // VALUES
    // =========================================================

    /// <summary>
    /// Значение, которое нужно отдать в PlayerStats / PlayerHealth /
    /// перезарядкам, чтобы получить суммарный бонус уровня.
    /// </summary>
    /// <summary>Бонус уровня level, посчитанный от базового значения.</summary>
    /// <remarks>
    /// Значение передаётся в те же методы, что и бонусы забега
    /// (PlayerStats.Add*, PlayerHealth.Add*, UpgradeCooldown), поэтому
    /// знак уже учтён: для перезарядок он отрицательный, так как
    /// множитель получается меньше единицы.
    /// </remarks>
    public static float GetApplyValue(PermanentUpgradeStat stat, int level)
    {
        Def def = GetDef(stat);

        if (level <= 0)
            return 0f;

        if (def.Mode == ValueMode.FlatAdd)
            return def.ValuePerLevel * level;

        return
            Mathf.Pow(1f + def.ValuePerLevel, level) - 1f;
    }

    /// <summary>Прирост бонуса от покупки следующего уровня (0 на максимуме).</summary>
    public static float GetNextLevelGain(PermanentUpgradeStat stat)
    {
        int level = GetLevel(stat);

        if (level >= MaxLevel)
            return 0f;

        Def def = GetDef(stat);

        float current = GetApplyValue(stat, level);
        float next = GetApplyValue(stat, level + 1);

        return ToDisplayValue(def, next - current);
    }

    /// <summary>Строка «сколько даст следующий уровень» для карточки.</summary>
    public static string FormatNextLevelGain(PermanentUpgradeStat stat)
    {
        if (IsMaxLevel(stat))
            return "Максимальный уровень";

        return
            "Дальше: " +
            Format(
                GetDef(stat),
                GetNextLevelGain(stat)
            );
    }

    // =========================================================
    // ACTIONS
    // =========================================================

    /// <summary>
    /// Покупает следующий уровень параметра: списывает монеты
    /// и сохраняет прогресс. Возвращает false, если максимум
    /// или не хватает монет.
    /// </summary>
    public static bool TryBuy(PermanentUpgradeStat stat)
    {
        EnsureLoaded();

        int level = GetLevel(stat);

        if (level >= MaxLevel)
        {
            Debug.LogWarning(
                $"[Upgrades] {GetDef(stat).Name}: уже максимальный уровень."
            );

            return false;
        }

        int cost = GetCostForLevel(level);

        XpManager xp = XpManager.Instance;

        if (xp == null)
        {
            Debug.LogWarning("[Upgrades] XpManager.Instance is null.");

            return false;
        }

        if (xp.GlobalCoins < cost)
        {
            Debug.LogWarning(
                $"[Upgrades] {GetDef(stat).Name}: нужно {cost}, " +
                $"есть {xp.GlobalCoins}."
            );

            return false;
        }

        if (!xp.TrySpendCoins(cost))
            return false;

        Entry entry = FindEntry(stat);

        if (entry == null)
        {
            entry = new Entry
            {
                stat = stat.ToString(),
                level = 0
            };

            data.entries.Add(entry);
        }

        entry.level = level + 1;

        Save();

        Debug.Log(
            $"[Upgrades] {GetDef(stat).Name} -> ур. {entry.level}/{MaxLevel} " +
            $"за {cost} монет."
        );

        return true;
    }

    /// <summary>Сбрасывает весь прогресс улучшений (монеты не возвращает).</summary>
    public static void ResetAll()
    {
        data = new SaveData();

        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();

        Debug.Log("[Upgrades] Прогресс улучшений сброшен.");
    }

    // =========================================================
    // CATALOG
    // =========================================================

    private static Dictionary<PermanentUpgradeStat, Def> BuildDefs()
    {
        var map =
            new Dictionary<PermanentUpgradeStat, Def>();

        // Урон: +5% за уровень -> +62.9% на максимуме.
        Add(
            map,
            new Def
            {
                Stat = PermanentUpgradeStat.Damage,
                Name = "Урон",
                Description = "Усиливает все попадания оружия.",
                Format = "+{0}% к урону",
                Mode = ValueMode.PercentCompound,
                ValuePerLevel = 0.05f,
                Decimals = 1
            }
        );

        // Скорострельность: +5% за уровень -> +62.9% на максимуме.
        Add(
            map,
            new Def
            {
                Stat = PermanentUpgradeStat.FireRate,
                Name = "Скорострельность",
                Description = "Оружие стреляет чаще.",
                Format = "+{0}% к скорострельности",
                Mode = ValueMode.PercentCompound,
                ValuePerLevel = 0.05f,
                Decimals = 1
            }
        );

        // Скорость бега: +3% за уровень -> +34.4% на максимуме.
        Add(
            map,
            new Def
            {
                Stat = PermanentUpgradeStat.MoveSpeed,
                Name = "Скорость бега",
                Description = "Позволяет уворачиваться, но не делает игрока бегометелем.",
                Format = "+{0}% к скорости бега",
                Mode = ValueMode.PercentCompound,
                ValuePerLevel = 0.03f,
                Decimals = 1
            }
        );

        // Максимум HP: +7% за уровень -> +96.7% на максимуме.
        Add(
            map,
            new Def
            {
                Stat = PermanentUpgradeStat.MaxHealth,
                Name = "Запас здоровья",
                Description = "Больше HP в каждом забеге.",
                Format = "+{0}% к максимуму здоровья",
                Mode = ValueMode.PercentCompound,
                ValuePerLevel = 0.07f,
                Decimals = 1
            }
        );

        // Регенерация: +0.1 HP/с за уровень -> +1 HP/с на максимуме.
        Add(
            map,
            new Def
            {
                Stat = PermanentUpgradeStat.HealthRegen,
                Name = "Регенерация",
                Description = "Здоровье восстанавливается само.",
                Format = "+{0} HP/с",
                Mode = ValueMode.FlatAdd,
                ValuePerLevel = 0.1f,
                ValueIsFraction = false,
                Decimals = 1
            }
        );

        // Шанс крита: +2% за уровень -> +20% на максимуме.
        Add(
            map,
            new Def
            {
                Stat = PermanentUpgradeStat.CriticalChance,
                Name = "Шанс крита",
                Description = "Шанс нанести критический урон.",
                Format = "+{0}% к шансу крита",
                Mode = ValueMode.FlatAdd,
                ValuePerLevel = 0.02f,
                Decimals = 0
            }
        );

        // Сила крита: +0.15x за уровень -> +1.5x к криту (2.0 -> 3.5).
        Add(
            map,
            new Def
            {
                Stat = PermanentUpgradeStat.CriticalDamage,
                Name = "Сила крита",
                Description = "Критический удар наносит больше урона.",
                Format = "+{0}x к силе крита",
                Mode = ValueMode.FlatAdd,
                ValuePerLevel = 0.15f,
                ValueIsFraction = false,
                Decimals = 2
            }
        );

        // Скорость снарядов: +5% за уровень -> +62.9% на максимуме.
        Add(
            map,
            new Def
            {
                Stat = PermanentUpgradeStat.ProjectileSpeed,
                Name = "Скорость снарядов",
                Description = "Пуля летит до цели быстрее.",
                Format = "+{0}% к скорости снарядов",
                Mode = ValueMode.PercentCompound,
                ValuePerLevel = 0.05f,
                Decimals = 1
            }
        );

        // Перезарядка рывка: -4% за уровень -> -33% на максимуме.
        Add(
            map,
            new Def
            {
                Stat = PermanentUpgradeStat.DashCooldown,
                Name = "Перезарядка рывка",
                Description = "Рывок (Space) восстанавливается быстрее.",
                Format = "-{0}% к перезарядке рывка",
                Mode = ValueMode.PercentCompound,
                ValuePerLevel = -0.04f,
                Decimals = 1
            }
        );

        // Перезарядка способностей: -4% за уровень -> -33% на максимуме.
        Add(
            map,
            new Def
            {
                Stat = PermanentUpgradeStat.AbilityCooldown,
                Name = "Перезарядка способностей",
                Description = "Бомба (E) и щит (Q) восстанавливаются быстрее.",
                Format = "-{0}% к перезарядке способностей",
                Mode = ValueMode.PercentCompound,
                ValuePerLevel = -0.04f,
                Decimals = 1
            }
        );

        return map;
    }

    private static void Add(
        Dictionary<PermanentUpgradeStat, Def> map,
        Def def)
    {
        map[def.Stat] = def;
    }

    private static Def GetDef(PermanentUpgradeStat stat)
    {
        if (Defs.TryGetValue(stat, out Def def))
            return def;

        return Defs[PermanentUpgradeStat.Damage];
    }

    // =========================================================
    // PERSISTENCE
    // =========================================================

    private static Entry FindEntry(PermanentUpgradeStat stat)
    {
        EnsureLoaded();

        string key = stat.ToString();

        for (int i = 0; i < data.entries.Count; i++)
        {
            Entry entry = data.entries[i];

            if (entry == null)
                continue;

            if (entry.stat == key)
                return entry;
        }

        return null;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;

        data = new SaveData();

        string json =
            PlayerPrefs.GetString(PrefsKey, string.Empty);

        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            SaveData parsed =
                JsonUtility.FromJson<SaveData>(json);

            if (parsed != null)
                data = parsed;
        }
        catch
        {
            data = new SaveData();
        }

        Normalize(data);
    }

    /// <summary>
    /// Старые сохранения не содержат списка — приводим к рабочему виду,
    /// чтобы не падать на null-перечислениях.
    /// </summary>
    private static void Normalize(SaveData target)
    {
        if (target.entries == null)
            target.entries = new List<Entry>();

        for (int i = target.entries.Count - 1; i >= 0; i--)
        {
            Entry entry = target.entries[i];

            if (entry == null || string.IsNullOrEmpty(entry.stat))
                target.entries.RemoveAt(i);
        }
    }

    private static void Save()
    {
        EnsureLoaded();

        PlayerPrefs.SetString(
            PrefsKey,
            JsonUtility.ToJson(data)
        );

        PlayerPrefs.Save();
    }
}
