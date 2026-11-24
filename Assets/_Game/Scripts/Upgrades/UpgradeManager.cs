using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [Header("Player References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Weapon weapon;
    [SerializeField] private AbilityManager abilityManager;

    [Header("Run Upgrades")]
    [SerializeField] private RunUpgrades runUpgrades;

    [Header("Normal Upgrades")]
    [SerializeField]
    private List<UpgradeData> availableUpgrades =
        new List<UpgradeData>();

    [Header("Weapons")]
    [SerializeField]
    private List<WeaponData> availableWeapons =
        new List<WeaponData>();

    [Header("Abilities (Shop / Equipment)")]
    [Tooltip(
        "Способности для вкладки «Способности» панели «Снаряжение». " +
        "Заполняется автоматически при сборке UI (Tools -> Bullet Rush)."
    )]
    [SerializeField]
    private List<AbilityData> availableAbilities =
        new List<AbilityData>();

    [Header("Clothing (Shop / Equipment)")]
    [Tooltip(
        "Одежда для вкладки «Одежда» панели «Снаряжение». " +
        "Заполняется автоматически при сборке UI (Tools -> Bullet Rush)."
    )]
    [SerializeField]
    private List<ClothingData> availableClothing =
        new List<ClothingData>();

    [Header("Selection")]
    [SerializeField] private int choicesCount = 3;

    [Header("Smart Choices")]
    [SerializeField]
    private SmartChoiceSettings smartChoices =
        new SmartChoiceSettings();

    [Header("Synergies")]
    [SerializeField]
    private List<SynergyData> synergies =
        new List<SynergyData>();

    [Tooltip(
        "Во сколько раз чаще предлагается уже взятое улучшение. " +
        "Это то, из-за чего карточки перестают быть случайными: " +
        "если игрок дважды брал горение, скорее всего он строит " +
        "билд на горении, и третий уровень тому и подтверждение."
    )]
    [Min(1f)]
    [SerializeField] private float takenAffinity = 1.6f;

    [Tooltip(
        "Штраф за повтор того же улуччения, что и на прошлой волне. " +
        "Без него три карточки подряд могли быть одним и тем же — " +
        "выбор перестаёт быть выбором."
    )]
    [Range(0f, 1f)]
    [SerializeField] private float repeatPenalty = 0.15f;

    [Header("Weapon Drop")]
    [Range(0f, 1f)]
    [SerializeField] private float weaponDropChance = 0.20f;

    private List<UpgradeData> currentChoices =
        new List<UpgradeData>();

    private List<UpgradeData> runtimeWeaponUpgrades =
        new List<UpgradeData>();

    private List<UpgradeData> runPerkCards;
    private readonly List<UpgradeData> abilityUpgradeCards = new(3);
    private bool bossChoice, evolutionChoice;
    public string ChoiceTitle => RunBuildMechanics.ChoiceTitle(bossChoice, evolutionChoice);

    // Сколько раз за текущий забег взято каждое улучшение.
    // Раньше выбор ничем не помнился, поэтому лимита не существовало
    // и улучшение с математическим множителем можно было брать
    // на каждой волне.
    private readonly Dictionary<UpgradeData, int> takenStacks =
        new Dictionary<UpgradeData, int>();

    private UpgradeData lastTaken;
    private bool subscribed;
    private bool runStarted;

    private const int RecentPickWindow = 3;

    private readonly List<UpgradeData> recentPicks =
        new List<UpgradeData>();

    private readonly List<WeaponData> takenWeapons =
        new List<WeaponData>();

    private readonly List<SynergyProgress> synergyProgress =
        new List<SynergyProgress>();

    private readonly HashSet<SynergyData> discoveredSynergies =
        new HashSet<SynergyData>();

    private readonly List<SynergyData> discoveredQueue =
        new List<SynergyData>();

    private RunBuildState buildState;

    public IReadOnlyList<UpgradeData> CurrentChoices =>
        currentChoices;

    public SmartChoiceSettings SmartChoices => smartChoices;

    public RunBuildState BuildState => buildState;

    public IReadOnlyList<SynergyProgress> ActiveSynergies =>
        synergyProgress;

    public UpgradeData LastTaken => lastTaken;

    public float TakenAffinity => takenAffinity;

    public float RepeatPenalty => repeatPenalty;

    /// <summary>
    /// Все оружия, доступные в забеге (используется панелью снаряжения
    /// и применением выбранного стартового оружия).
    /// </summary>
    public IReadOnlyList<WeaponData> GetAvailableWeapons() =>
        availableWeapons;
    public IReadOnlyList<SynergyData> GetAllSynergies() => synergies;

    /// <summary>
    /// Все способности магазина (вкладка «Способности» панели снаряжения).
    /// </summary>
    public IReadOnlyList<AbilityData> GetAvailableAbilities() =>
        availableAbilities;

    /// <summary>
    /// Вся одежда магазина (вкладка «Одежда» панели снаряжения).
    /// </summary>
    public IReadOnlyList<ClothingData> GetAvailableClothing() =>
        availableClothing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        AddAbilityUpgrade(UpgradeType.AbilityPower,.22f);
        AddAbilityUpgrade(UpgradeType.AbilityHaste,.10f);
        AddAbilityUpgrade(UpgradeType.AbilityRange,.12f);
    }

    private void AddAbilityUpgrade(UpgradeType type,float value)
    {
        var card=ScriptableObject.CreateInstance<UpgradeData>();
        card.InitializeAbilityUpgrade(type,value); abilityUpgradeCards.Add(card); availableUpgrades.Add(card);
    }

    private void Start()
    {
        // В сцене ссылка на RunUpgrades пустая, она добиралась
        // лениво, только когда игрок впервые брал эффект. Из-за
        // этого обучение, где окно выбора открывается вне волны,
        // могло применить эффект раньше, чем компонент появился.
        EnsureRunUpgrades();
        EnsureAbilityManager();

        // Сцена может стартовать сразу в игре, а может прийти из
        // меню. В обоих случаях первый забег должен быть чистым.
        if (GameStateManager.Instance == null)
            return;

        GameStateManager.Instance.OnGameStateChanged +=
            HandleStateChanged;

        subscribed = true;

        if (GameStateManager.Instance.CurrentState ==
            GameState.Playing)
        {
            BeginRun();
        }
    }

    private void OnDisable()
    {
        if (subscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -=
                HandleStateChanged;

            subscribed = false;
        }
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Playing)
        {
            BeginRun();
            return;
        }

        if (state == GameState.Menu)
            ResetRunStacks();
    }

    /// <summary>
    /// Чистит память о выборах перед новым забегом. Статы игрока и
    /// компонент RunUpgrades живут на префабе и перезапускаются
    /// вместе с игроком — здесь нужны только счётчики стеков.
    /// </summary>
    private void BeginRun()
    {
        if (runStarted)
            return;

        ResetRunStacks();
        runStarted = true;
    }

    private void ResetRunStacks()
    {
        runStarted = false;
        RunBuildMechanics.Reset();

        ClearPreviousChoices();

        takenStacks.Clear();

        lastTaken = null;

        recentPicks.Clear();
        takenWeapons.Clear();
        discoveredSynergies.Clear();
        discoveredQueue.Clear();

        RebuildState();
    }

    private void RebuildState()
    {
        buildState = RunBuildState.Build(
            takenStacks,
            recentPicks,
            takenWeapons
        );

        synergyProgress.Clear();

        if (synergies == null)
            return;

        for (int i = 0; i < synergies.Count; i++)
        {
            SynergyData synergy = synergies[i];

            if (synergy == null)
                continue;

            if (!synergy.IsAvailable(buildState))
                continue;

            var progress = new SynergyProgress(
                synergy,
                buildState
            );

            synergyProgress.Add(progress);

            if (progress.IsComplete &&
                discoveredSynergies.Add(synergy))
            {
                ApplySynergyRewards(synergy);

                discoveredQueue.Add(synergy);
                GameplayJourney.Instance?.Discover(synergy);

                Debug.Log(
                    $"SYNERGY DISCOVERED: " +
                    $"{synergy.LocalizedName}"
                );
            }
        }
    }

    private void ApplySynergyRewards(SynergyData synergy)
    {
        ApplyRewardList(synergy.Rewards);

        if (synergy.HasAllOptional(buildState))
            ApplyRewardList(synergy.OptionalRewards);
    }

    private void ApplyRewardList(IReadOnlyList<UpgradeData> rewards)
    {
        if (rewards == null)
            return;

        foreach (UpgradeData reward in rewards)
        {
            if (reward == null || IsMaxed(reward))
                continue;

            ApplyUpgrade(reward);
        }
    }

    public void EnsureState()
    {
        if (buildState == null)
            RebuildState();
    }

    public List<SynergyData> DrainDiscoveredSynergies()
    {
        if (discoveredQueue.Count == 0)
            return null;

        var drained = new List<SynergyData>(discoveredQueue);

        discoveredQueue.Clear();

        return drained;
    }

    /// <summary>
    /// Сколько раз улучшение уже взято в этом забеге.
    /// Оружие из дропа в счёт не идёт: оно каждый раз новое.
    /// </summary>
    public int GetStacks(UpgradeData upgrade)
    {
        if (upgrade == null)
            return 0;

        if (takenStacks.TryGetValue(upgrade, out int stacks))
            return stacks;

        return 0;
    }

    public int GetMaxStacks(UpgradeData upgrade) =>
        upgrade != null
            ? upgrade.MaxStacks
            : 0;

    public bool IsMaxed(UpgradeData upgrade) =>
        upgrade != null &&
        GetStacks(upgrade) >= GetMaxStacks(upgrade);

    /// <summary>
    /// Стоит ли вообще показывать это улучшение игроку. Отсеивает
    /// уже прокачанное до лимита и то, что не может сработать
    /// с текущим оружием.
    /// </summary>
    public bool IsUsable(UpgradeData upgrade) =>
        upgrade != null &&
        !IsMaxed(upgrade) &&
        IsUsefulForCurrentWeapon(upgrade);

    public void GenerateChoices() => GenerateChoices(0, false);

    public void GenerateChoices(int wave, bool bossReward)
    {
        ClearPreviousChoices();

        bossChoice = evolutionChoice = false;
        EnsureState();
        if (wave > 0 && !GameModeManager.IsPvp)
        {
            runPerkCards ??= RunBuildMechanics.CreateCards();
            var special = new List<UpgradeData>();
            if (bossReward)
            {
                foreach (var card in runPerkCards)
                    if (RunBuildMechanics.IsRelic(card.Perk) && !RunBuildMechanics.Has(card.Perk)) special.Add(card);
                bossChoice = special.Count > 0;
            }
            else if (wave >= 5 && weapon != null && RunBuildMechanics.Evolution(weapon.Data) == RunPerk.None)
            {
                foreach (var card in runPerkCards)
                    if (RunBuildMechanics.CanEvolve(card.Perk, weapon.Data)) special.Add(card);
                evolutionChoice = special.Count > 0;
            }
            if (special.Count > 0)
            {
                if (evolutionChoice)
                {
                    var signature = special.Find(card => RunBuildMechanics.IsSignature(card.Perk));
                    if (signature != null) { currentChoices.Add(signature); special.Remove(signature); }
                }
                while (currentChoices.Count < choicesCount && special.Count > 0)
                {
                    int index = Random.Range(0, special.Count);
                    currentChoices.Add(special[index]); special.RemoveAt(index);
                }
                return;
            }
        }

        List<UpgradeData> pool = BuildValidPool();
        // Reserve a slot for a reachable branch. The other cards keep the
        // existing smart continuation, cross-build and wildcard logic.
        if (wave >= 2 && runPerkCards != null && !GameModeManager.IsPvp)
        {
            var branch = new List<UpgradeData>();
            foreach (var card in runPerkCards)
                if (RunBuildMechanics.CanUseTree(card.Perk, buildState)) branch.Add(card);
            if (branch.Count > 0)
            {
                var preferred = new List<UpgradeData>();
                foreach (var card in branch)
                    if (buildState.HasAnyFamily(card.Families)) preferred.Add(card);
                var options = preferred.Count > 0 && Random.value < .75f ? preferred : branch;
                currentChoices.Add(options[Random.Range(0, options.Count)]);
            }
        }

        if (pool.Count == 0)
        {
            // Пул исчерпан. Молча закрывать окно нельзя: панель
            // осталась бы висеть с нулём кнопок и забег встал бы
            // навсегда. Поэтому снимаем только лимит стаков.
            pool = BuildPoolIgnoringStacks();

            if (pool.Count == 0)
            {
                Debug.LogWarning(
                    "UpgradeManager: нет доступных улучшений."
                );

                return;
            }
        }

        bool weaponDrop =
            availableWeapons.Count > 0 &&
            Random.value < weaponDropChance;

        if (weaponDrop)
        {
            GenerateChoicesWithWeapon(pool);

            Debug.Log(
                "WEAPON DROP! A weapon appeared in upgrade choices."
            );
        }
        else
        {
            GenerateNormalChoices(pool);

            Debug.Log(
                "No weapon this wave. Normal upgrades generated."
            );
        }

        Debug.Log("Generated upgrade choices:");

        foreach (UpgradeData upgrade in currentChoices)
        {
            Debug.Log(
                $"{upgrade.UpgradeName} " +
                $"[{upgrade.Rarity}] " +
                $"уровень {GetStacks(upgrade) + 1}/" +
                $"{GetMaxStacks(upgrade)}"
            );
        }
    }

    /// <summary>
    /// Пул, из которого игрок реально может выбирать: без пустых
    /// ассетов, без прокачанного до лимита и без бессмысленного
    /// для текущего ствола.
    /// </summary>
    private List<UpgradeData> BuildValidPool()
    {
        var pool = new List<UpgradeData>(availableUpgrades.Count);

        for (int i = 0; i < availableUpgrades.Count; i++)
        {
            if (IsUsable(availableUpgrades[i]))
                pool.Add(availableUpgrades[i]);
        }

        return pool;
    }

    private List<UpgradeData> BuildPoolIgnoringStacks()
    {
        var pool = new List<UpgradeData>(availableUpgrades.Count);

        for (int i = 0; i < availableUpgrades.Count; i++)
        {
            UpgradeData candidate = availableUpgrades[i];

            if (candidate == null)
                continue;

            if (!IsUsefulForCurrentWeapon(candidate))
                continue;

            pool.Add(candidate);
        }

        return pool;
    }

    /// <summary>
    /// Убирает улучшения, которые с текущим оружием ничего не
    /// меняют. Сейчас такой случай один: Projectile Count у
    /// дробовика, который уже упёрся в потолок снарядов. Раньше
    /// карточка продолжала выпадать и обещать «+70%», хотя число
    /// снарядов уже не менялось.
    /// </summary>
    private bool IsUsefulForCurrentWeapon(UpgradeData upgrade)
    {
        if (upgrade.Type == UpgradeType.Bomb || upgrade.Type == UpgradeType.Shield)
            return abilityManager == null || abilityManager.CanUnlock(upgrade.Type == UpgradeType.Bomb ? AbilityKind.Bomb : AbilityKind.Shield);
        if (upgrade.Type == UpgradeType.AbilityRange) return abilityManager != null && abilityManager.HasDamagingAbility;
        if (upgrade.Type == UpgradeType.AbilityPower || upgrade.Type == UpgradeType.AbilityHaste) return abilityManager != null && abilityManager.HasAny;
        WeaponData current =
            weapon != null
                ? weapon.Data
                : null;

        if (current == null)
            return true;

        if (upgrade.Type != UpgradeType.ProjectileCount)
            return true;

        int baseCount =
            Mathf.Max(current.ProjectileCount, 1);

        float percent =
            playerStats != null
                ? playerStats.ProjectileCountPercent
                : 0f;

        int now =
            WeaponData.ResolveProjectileCount(
                baseCount,
                percent
            );

        int next =
            WeaponData.ResolveProjectileCount(
                baseCount,
                percent + upgrade.PercentValue
            );

        return next > now;
    }

    private void GenerateNormalChoices(List<UpgradeData> pool)
    {
        FillChoiceSlots(pool, choicesCount - currentChoices.Count);
    }

    private void GenerateChoicesWithWeapon(List<UpgradeData> pool)
    {
        WeaponData selectedWeapon =
            GetRandomWeapon();

        if (selectedWeapon != null && currentChoices.Count < choicesCount)
        {
            UpgradeData weaponUpgrade =
                ScriptableObject.CreateInstance<UpgradeData>();

            weaponUpgrade.InitializeWeapon(
                selectedWeapon
            );

            runtimeWeaponUpgrades.Add(
                weaponUpgrade
            );

            currentChoices.Add(
                weaponUpgrade
            );
        }

        FillChoiceSlots(
            pool,
            choicesCount - currentChoices.Count
        );
    }

    /// <summary>
    /// Заполняет слоты выбора тремя ролями: продолжение билда,
    /// пересечение синергий и wildcard. Гарантия «хотя бы одна
    /// свежая карточка» живёт в wildcard — последнем слоте.
    /// </summary>
    private void FillChoiceSlots(
        List<UpgradeData> pool,
        int slotCount)
    {
        UpgradeChoiceGenerator.Fill(
            this,
            pool,
            slotCount,
            currentChoices
        );
    }

    private WeaponData GetRandomWeapon()
    {
        // Уже купленное оружие не предлагаем: карточка с ним
        // ничего не даёт, выбор тратится впустую. Стартовое
        // оружие бесплатное, поэтому IsOwned всегда истинно и
        // выпадать не будет.
        List<WeaponData> candidates =
            new List<WeaponData>(availableWeapons.Count);

        WaveManager waves = FindAnyObjectByType<WaveManager>();
        int wave = waves != null ? waves.CurrentWave : 0;
        foreach (WeaponData weapon in availableWeapons)
        {
            if (weapon == null)
                continue;

            if (EquipmentManager.IsOwned(weapon) || takenWeapons.Contains(weapon))
                continue;

            if (!EquipmentManager.IsUnlocked(weapon) && wave < weapon.UnlockLevel)
                continue;

            candidates.Add(weapon);
        }

        if (candidates.Count == 0)
            return null;

        int totalWeight = 0;

        foreach (WeaponData weapon in candidates)
        {
            totalWeight += GetRarityWeight(weapon.Rarity);
        }

        if (totalWeight <= 0)
        {
            return candidates[
                Random.Range(0, candidates.Count)
            ];
        }

        int roll =
            Random.Range(0, totalWeight);

        foreach (WeaponData weapon in candidates)
        {
            roll -= GetRarityWeight(weapon.Rarity);

            if (roll < 0)
                return weapon;
        }

        return candidates[candidates.Count - 1];
    }

    public int GetRarityWeight(Rarity rarity)
    {
        return Mathf.Max(5 - (int)rarity, 1);
    }

    public void ChooseUpgrade(int index)
    {
        if (index < 0 ||
            index >= currentChoices.Count)
        {
            Debug.LogWarning(
                $"Invalid upgrade index: {index}"
            );

            return;
        }

        UpgradeData selected =
            currentChoices[index];

        ApplyUpgrade(selected);

        RegisterPick(selected);

        PlayUpgradePickSound();

        Debug.Log(
            $"Player chose: " +
            $"{selected.UpgradeName} " +
            $"[{selected.Rarity}] " +
            $"уровень {GetStacks(selected)}/" +
            $"{GetMaxStacks(selected)}"
        );
    }

    /// <summary>
    /// Запоминает выбор, чтобы он влиял и на дальнейшие выдачи,
    /// и на счётчик уровня в карточке.
    /// </summary>
    private void RegisterPick(UpgradeData upgrade)
    {
        if (upgrade == null)
            return;

        if (upgrade.Type == UpgradeType.Weapon &&
            upgrade.WeaponData != null &&
            !takenWeapons.Contains(upgrade.WeaponData))
        {
            takenWeapons.Add(upgrade.WeaponData);
        }

        // Оружие из дропа создаётся в рантайме и каждый раз новое,
        // поэтому счётчики стаков по нему не ведём. Иначе штраф за
        // повтор съедал бы настоящее улучшение, выбранное следом.
        if (!availableUpgrades.Contains(upgrade) && !RunBuildMechanics.IsTree(upgrade.Perk))
            return;

        takenStacks.TryGetValue(upgrade, out int stacks);

        takenStacks[upgrade] = stacks + 1;

        lastTaken = upgrade;

        recentPicks.Add(upgrade);

        while (recentPicks.Count > RecentPickWindow)
            recentPicks.RemoveAt(0);

        RebuildState();
    }

    public string PreviewStat(UpgradeData upgrade)
    {
        float value = upgrade.PercentValue;
        switch (upgrade.Type)
        {
            case UpgradeType.MaxHealth when playerHealth != null:
                return Change(playerHealth.MaxHealth, playerHealth.MaxHealth * (1f + Mathf.Max(0, value)));
            case UpgradeType.Damage when playerStats != null && weapon != null && weapon.Data != null:
                float damage = weapon.Data.Damage * playerStats.DamageMultiplier;
                return Change(damage, damage * (1f + value));
            case UpgradeType.MoveSpeed when playerStats != null:
                return Change(playerStats.MoveSpeed, playerStats.MoveSpeed * (1f + value));
            case UpgradeType.FireRate when playerStats != null:
                return Change(playerStats.FireRateMultiplier, playerStats.FireRateMultiplier * (1f + value), "×");
            case UpgradeType.ProjectileSpeed when playerStats != null:
                return Change(playerStats.ProjectileSpeedMultiplier, playerStats.ProjectileSpeedMultiplier * (1f + value), "×");
            case UpgradeType.CriticalChance when playerStats != null:
                return Change(playerStats.CriticalChance * 100, Mathf.Clamp01(playerStats.CriticalChance + value) * 100, "%");
            case UpgradeType.CriticalDamage when playerStats != null:
                return Change(playerStats.CriticalDamageMultiplier, playerStats.CriticalDamageMultiplier + value, "×");
            case UpgradeType.ProjectileCount when playerStats != null:
                return Change((1f + playerStats.ProjectileCountPercent) * 100, (1f + playerStats.ProjectileCountPercent + value) * 100, "%");
            case UpgradeType.Pierce when playerStats != null:
                return Change(playerStats.BonusPierce, playerStats.BonusPierce + Mathf.RoundToInt(value));
            default:
                return null;
        }
    }

    private static string Change(float before, float after, string suffix = "")
    {
        var format = System.Globalization.CultureInfo.InvariantCulture;
        string text = before.ToString("0.##", format) + suffix + " → " + after.ToString("0.##", format) + suffix;
        return Lang.Code == LangCode.En ? text : text.Replace('.', ',');
    }

    public void ApplyUpgrade(UpgradeData upgrade)
    {
        if (upgrade == null)
            return;

        if (upgrade.Perk != RunPerk.None)
        {
            RunBuildMechanics.Apply(upgrade.Perk, weapon, playerStats, playerHealth);
            return;
        }

        EnsureAbilityManager();

        switch (upgrade.Type)
        {
            case UpgradeType.Damage:

                if (playerStats != null)
                {
                    playerStats.AddDamagePercent(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.FireRate:

                if (playerStats != null)
                {
                    playerStats.AddFireRatePercent(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.MoveSpeed:

                if (playerStats != null)
                {
                    playerStats.AddMoveSpeedPercent(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.ProjectileSpeed:

                if (playerStats != null)
                {
                    playerStats.AddProjectileSpeedPercent(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.MaxHealth:

                if (playerHealth != null)
                {
                    playerHealth.AddMaxHealthPercent(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.CriticalChance:

                if (playerStats != null)
                {
                    playerStats.AddCriticalChance(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.CriticalDamage:

                if (playerStats != null)
                {
                    playerStats.AddCriticalDamageMultiplier(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.HealthRegen:

                if (playerHealth != null)
                {
                    playerHealth.AddHealthRegen(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.ProjectileCount:

                if (playerStats != null)
                {
                    // Процент, а не «+1 снаряд»: у однозарядного
                    // ствола прибавка одного снаряда удваивала урон
                    // залпа. Само число считает Weapon.
                    playerStats.AddProjectileCountPercent(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.Pierce:

                if (playerStats != null)
                {
                    playerStats.AddPierce(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.Weapon:

                if (weapon != null &&
                    upgrade.WeaponData != null)
                {
                    weapon.SetWeapon(
                        upgrade.WeaponData
                    );
                }

                break;

            case UpgradeType.Special:

                Debug.Log(
                    "Special upgrade will be connected later."
                );

                break;

            case UpgradeType.AbilityPower:
                abilityManager?.UpgradePower(upgrade.PercentValue);
                break;
            case UpgradeType.AbilityHaste:
                abilityManager?.ReduceCooldown(upgrade.PercentValue);
                break;
            case UpgradeType.AbilityRange:
                abilityManager?.UpgradeRange(upgrade.PercentValue);
                break;
            case UpgradeType.Bomb:

                if (abilityManager != null)
                {
                    if (!abilityManager.HasBomb)
                    {
                        abilityManager.UnlockBomb();

                        Debug.Log(
                            "Bomb ability unlocked! Press E to blast nearby enemies."
                        );
                    }
                    else
                    {
                        BombAbility bomb =
                            abilityManager.Bomb;

                        if (bomb != null)
                        {
                            bomb.UpgradeDamage(
                                upgrade.PercentValue
                            );

                            bomb.UpgradeCooldown(0.10f);
                        }
                    }
                }

                break;

            case UpgradeType.Shield:

                if (abilityManager != null)
                {
                    if (!abilityManager.HasShield)
                    {
                        abilityManager.UnlockShield();

                        Debug.Log(
                            "Shield ability unlocked! Press Q to block damage."
                        );
                    }
                    else
                    {
                        ShieldAbility shield =
                            abilityManager.Shield;

                        if (shield != null)
                        {
                            shield.UpgradeDuration(
                                upgrade.PercentValue
                            );

                            shield.UpgradeCooldown(0.10f);
                        }
                    }
                }

                break;

            case UpgradeType.BurningRounds:

                EnsureRunUpgrades();

                if (runUpgrades != null)
                {
                    runUpgrades.AddBurn(
                        upgrade.EffectData as BurnEffectData
                    );
                }

                break;

            case UpgradeType.ExplosiveRounds:

                EnsureRunUpgrades();

                if (runUpgrades != null)
                {
                    runUpgrades.AddExplosion(
                        upgrade.EffectData as ExplosionEffectData
                    );
                }

                break;

            case UpgradeType.Bleeding:

                EnsureRunUpgrades();

                if (runUpgrades != null)
                {
                    runUpgrades.AddBleed(
                        upgrade.EffectData as BleedingEffectData
                    );
                }

                break;

            case UpgradeType.Lifesteal:

                EnsureRunUpgrades();

                if (runUpgrades != null)
                {
                    runUpgrades.AddLifesteal(
                        upgrade.EffectData as LifestealEffectData
                    );
                }

                break;

            case UpgradeType.ChainLightning:

                EnsureRunUpgrades();

                if (runUpgrades != null)
                {
                    runUpgrades.AddChainLightning(
                        upgrade.EffectData as ChainLightningEffectData
                    );
                }

                break;

            case UpgradeType.Ricochet:

                EnsureRunUpgrades();

                if (runUpgrades != null)
                {
                    runUpgrades.AddRicochet(
                        upgrade.EffectData as RicochetEffectData
                    );
                }

                break;
        }

        Debug.Log(
            $"Upgrade applied: " +
            $"{upgrade.UpgradeName} " +
            $"[{upgrade.Rarity}]"
        );
    }

    private void EnsureAbilityManager()
    {
        if (abilityManager != null)
            return;

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            abilityManager =
                playerObject.GetComponent<AbilityManager>();

            if (abilityManager == null)
                abilityManager =
                    playerObject.AddComponent<AbilityManager>();
        }
    }

    private void EnsureRunUpgrades()
    {
        if (runUpgrades != null)
            return;

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            runUpgrades =
                playerObject.GetComponent<RunUpgrades>();

            if (runUpgrades == null)
                runUpgrades =
                    playerObject.AddComponent<RunUpgrades>();
        }
    }

    private void ClearPreviousChoices()
    {
        currentChoices.Clear();

        foreach (UpgradeData upgrade in runtimeWeaponUpgrades)
        {
            if (upgrade != null)
                Destroy(upgrade);
        }

        runtimeWeaponUpgrades.Clear();
    }

    private void PlayUpgradePickSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(
                sfx.UpgradePick,
                priority: SfxPriority.High
            );
    }

    private void OnDestroy()
    {
        foreach(var card in abilityUpgradeCards) if(card!=null) Destroy(card);
        if (runPerkCards != null)
            foreach (var card in runPerkCards) if (card != null) Destroy(card);
        if (subscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -=
                HandleStateChanged;

            subscribed = false;
        }

        foreach (UpgradeData upgrade in runtimeWeaponUpgrades)
        {
            if (upgrade != null)
                Destroy(upgrade);
        }

        runtimeWeaponUpgrades.Clear();
    }
}
