using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class Weapon : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField] private WeaponData weaponData;

    [Header("References")]
    [SerializeField] private Transform weaponVisualRoot;
    [SerializeField] private GameObject bulletPrefab;

    // Необязательный префаб трассера. Если пусто - трассер
    // строится процедурно (см. VfxFactory.SpawnTracer).
    [SerializeField] private GameObject tracerPrefab;

    [Header("Test Override")]
    [Tooltip(
        "Тестовый префаб оружия. Если задан, визуал всегда берётся " +
        "из него — независимо от WeaponData и выбора в панели " +
        "снаряжения. Пусто = обычное поведение."
    )]
    [SerializeField] private GameObject testVisualOverride;

    private PlayerStats playerStats;
    private PlayerController playerController;
    private CameraFollow cachedCamera;
    private Transform cachedFirePoint;
    private readonly System.Collections.Generic.List<Transform> cachedVisualMuzzles = new();
    private int nextVisualMuzzle;
    private GameObject activeWeaponVisual;
    private float appliedWeaponScoreBonus;

    private RunUpgrades runUpgrades;
    private PlayerHealth playerHealth;

    private float fireTimer;
    private int shotCycle, volleyIndex;

    private bool burstActive;
    private int burstShotsRemaining;
    private float burstTimer;
    private float burstShotInterval;

    private bool doubleShotActive;
    private float doubleShotTimer;

    private int ammo;
    private float reloadRemaining, reloadDuration, overdriveCharge, lastVolleyTime;
    private bool firedSinceReload;
    public bool UsesMagazine => !GameModeManager.IsPvp && weaponData != null;
    public int MagazineCapacity => weaponData == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(weaponData.BaseMagazineSize * RunBuildMechanics.MagazineMultiplier(weaponData)));
    public int Ammo => ammo;
    public bool IsReloading => reloadRemaining > 0f;
    public float ReloadRemaining => Mathf.Max(0f, reloadRemaining);
    public float ReloadProgress => IsReloading ? 1f - reloadRemaining / Mathf.Max(.01f, reloadDuration) : 1f;

    public void RefreshMagazine()
    {
        ammo = MagazineCapacity;
        reloadRemaining = reloadDuration = overdriveCharge = 0f;
        firedSinceReload = false;
        lastVolleyTime = -100f;
    }

    public void RestoreAmmo(int rounds)
    {
        if (UsesMagazine && !IsReloading && rounds > 0) ammo = Mathf.Min(MagazineCapacity, ammo + rounds);
    }

    public void RequestReload()
    {
        if (!UsesMagazine || !canShoot || Time.timeScale <= 0f || IsReloading || ammo >= MagazineCapacity) return;
        burstActive = doubleShotActive = false;
        burstShotsRemaining = 0;
        reloadRemaining = reloadDuration = Mathf.Max(.25f, weaponData.BaseReloadSeconds * RunBuildMechanics.ReloadMultiplier(weaponData));
        overdriveCharge = 0f;
    }

    public float FireCooldownRemaining =>
        Mathf.Max(fireTimer, 0f);

    /// <summary>
    /// Текущий ствол. Нужен UpgradeManager, чтобы не предлагать
    /// бессмысленные улучшения — например Projectile Count
    /// дробовику, который упирается в потолок снарядов.
    /// </summary>
    public WeaponData Data => weaponData;

    public float FireCooldownMax
    {
        get
        {
            if (weaponData == null)
                return 1f;

            return weaponData.GetFireCycleDuration(GetFireRateMultiplier());
        }
    }

    public float FireCooldownProgress
    {
        get
        {
            float max = FireCooldownMax;

            if (max <= 0f)
                return 1f;

            return
                Mathf.Clamp01(
                    1f - FireCooldownRemaining / max
                );
        }
    }

    private WeaponData menuDefaultWeapon;

    private void Awake()
    {
        menuDefaultWeapon = weaponData;
        playerStats = GetComponentInParent<PlayerStats>();

        if (playerStats == null)
            playerStats = FindAnyObjectByType<PlayerStats>();

        playerController = GetComponentInParent<PlayerController>();

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        runUpgrades =
            GetComponentInParent<RunUpgrades>();

        if (runUpgrades == null)
        {
            Transform root =
                transform.root;

            if (root != null)
                runUpgrades =
                    root.gameObject.AddComponent<RunUpgrades>();
        }

        playerHealth =
            GetComponentInParent<PlayerHealth>();

        ApplyWeaponVisual();
        RefreshMagazine();
    }

    internal void RefreshMenuWeapon()
    {
        if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Menu) return;
        WeaponData preview = ResolveSessionWeapon();
        if (preview == null || preview == weaponData) return;
        // Read-only preview: do not apply a score bonus or combat upgrades.
        weaponData = preview;
        ApplyWeaponVisual();
    }

    private WeaponData ResolveSessionWeapon()
    {
        WeaponData preview = menuDefaultWeapon;
        string selected = EquipmentManager.EquippedWeaponName;
        var upgrades = UpgradeManager.Instance;
        if (upgrades != null && !string.IsNullOrEmpty(selected))
        {
            foreach (WeaponData candidate in upgrades.GetAvailableWeapons())
            {
                if (candidate != null && candidate.WeaponName == selected && EquipmentManager.IsOwned(candidate))
                {
                    preview = candidate;
                    break;
                }
            }
        }
        return preview;
    }

    internal void ResetForSession(bool startingRun)
    {
        // PlayerStats has already restored its base multiplier.
        appliedWeaponScoreBonus = 0f;
        canShoot = false;
        fireTimer = 0f;
        shotCycle = 0;
        volleyIndex = 0;
        nextVisualMuzzle = 0;
        burstActive = false;
        burstShotsRemaining = 0;
        burstTimer = 0f;
        burstShotInterval = 0f;
        doubleShotActive = false;
        doubleShotTimer = 0f;
        WeaponData selected = ResolveSessionWeapon();
        if (selected != weaponData)
        {
            weaponData = selected;
            ApplyWeaponVisual();
        }
        if (startingRun) ApplyWeaponScoreBonus();
        RefreshMagazine();
    }

    private bool canShoot;
    private bool subscribed;

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void Start()
    {
        EnsureSubscribed();

        LoadEquippedWeapon();
    }

    /// <summary>
    /// Применяет оружие, выбранное в панели «Снаряжение». Если игрок
    /// ничего не выбрал или выбор недоступен — остаётся дефолтное
    /// оружие, зашитое в префабе.
    /// </summary>
    private void LoadEquippedWeapon()
    {
        string equippedName =
            EquipmentManager.EquippedWeaponName;

        if (string.IsNullOrEmpty(equippedName))
            return;

        if (weaponData != null &&
            weaponData.WeaponName == equippedName)
        {
            return;
        }

        UpgradeManager upgradeManager =
            UpgradeManager.Instance;

        if (upgradeManager == null)
            return;

        foreach (WeaponData candidate in
                 upgradeManager.GetAvailableWeapons())
        {
            if (candidate == null)
                continue;

            if (candidate.WeaponName != equippedName)
                continue;

            if (!EquipmentManager.IsOwned(candidate))
            {
                Debug.LogWarning(
                    $"Weapon: снаряжённое оружие '{equippedName}' " +
                    "не куплено, оставляю дефолтное."
                );

                return;
            }

            SetWeapon(candidate);

            return;
        }

        Debug.LogWarning(
            $"Weapon: снаряжённое оружие '{equippedName}' " +
            "не найдено среди availableWeapons, оставляю дефолтное."
        );
    }

    private void EnsureSubscribed()
    {
        if (subscribed)
            return;

        if (GameStateManager.Instance == null)
            return;

        GameStateManager.Instance.OnGameStateChanged += HandleStateChanged;
        subscribed = true;

        canShoot = GameStateManager.Instance.CurrentState == GameState.Playing;
    }

    private void OnDisable()
    {
        if (subscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;
            subscribed = false;
        }
    }

    private void HandleStateChanged(GameState state)
    {
        canShoot = state == GameState.Playing;

        // Выбор оружия в меню применяется именно в момент старта забега.
        if (state == GameState.Playing)
            LoadEquippedWeapon();
    }

    private void Update()
    {
        if (!canShoot || Time.timeScale <= 0f || weaponData == null || RamLiftArena.InTransit)
            return;

        UpdateFireTimer();
        RunBuildMechanics.TickDrone(this, playerStats);
        if (Time.time - lastVolleyTime > .28f)
            overdriveCharge = Mathf.MoveTowards(overdriveCharge, 0f, Time.deltaTime * 1.5f);

        if (UsesMagazine)
        {
            if (IsReloading)
            {
                reloadRemaining -= Time.deltaTime;
                if (reloadRemaining <= 0f) RefreshMagazine();
                else return;
            }
            if (ammo <= 0 || (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame))
            {
                RequestReload();
                if (IsReloading) return;
            }
        }
        UpdateBurst();
        UpdateDoubleShot();

        if (!CanShootNow())
            return;

        if (MobileControls.IsTouchDevice)
        {
            if (MobileControls.IsFiring)
            {
                playerController?.ApplyMobileAim();
                TryShoot();
            }
            return;
        }

        if (Mouse.current == null ||
            !Mouse.current.leftButton.isPressed)
            return;

        TryShoot();
    }

    private bool CanShootNow()
    {
        if (!canShoot)
            return false;

        // Пауза, меню, экран улучшений
        if (Time.timeScale <= 0f)
            return false;

        if (MobileControls.IsTouchDevice) return MobileControls.CanControl;

        // Клик по кнопке UI не должен стрелять
        if (IsPointerOverUi())
            return false;

        return true;
    }

    private bool IsPointerOverUi()
    {
        EventSystem eventSystem =
            EventSystem.current;

        if (eventSystem == null)
            return false;

        return eventSystem.IsPointerOverGameObject();
    }

    private void UpdateFireTimer()
    {
        if (fireTimer > 0f)
            fireTimer -= Time.deltaTime;
    }

    private void TryShoot()
    {
        if (weaponData == null)
            return;

        if (UsesMagazine && (IsReloading || ammo <= 0)) return;

        if (burstActive || doubleShotActive)
            return;

        if (fireTimer > 0f)
            return;

        fireTimer = FireCooldownMax;
        shotCycle = DamageEventId.Next();
        volleyIndex = 0;

        switch (weaponData.SpecialType)
        {
            case WeaponSpecialType.Burst:
                StartBurst();
                break;

            case WeaponSpecialType.DoubleShot:
                StartDoubleShot();
                break;

            default:
                FireVolley();
                break;
        }
    }

    // =========================================================
    // BURST
    // =========================================================

    private void StartBurst()
    {
        burstActive = true;

        burstShotsRemaining =
            weaponData.ShotsPerBurst;

        burstShotInterval = weaponData.GetBurstInterval(GetFireRateMultiplier());
        burstTimer = burstShotInterval;

        FireBurstShot();

        burstShotsRemaining--;
        burstActive = burstShotsRemaining > 0;
    }

    private void UpdateBurst()
    {
        if (!burstActive)
            return;

        burstTimer -= Time.deltaTime;

        // Carry elapsed time forward rather than losing it every frame.
        // The remaining shot count bounds this loop even for a zero interval.
        while (burstShotsRemaining > 0 && burstTimer <= 0f)
        {
            FireBurstShot();
            burstShotsRemaining--;
            burstTimer += burstShotInterval;
        }

        burstActive = burstShotsRemaining > 0;
    }

    private void FireBurstShot()
    {
        FireVolley();
    }

    // =========================================================
    // DOUBLE SHOT
    // =========================================================

    private void StartDoubleShot()
    {
        doubleShotActive = true;

        doubleShotTimer =
            weaponData.GetDoubleShotInterval(GetFireRateMultiplier());

        FireVolley();
    }

    private void UpdateDoubleShot()
    {
        if (!doubleShotActive)
            return;

        doubleShotTimer -= Time.deltaTime;

        if (doubleShotTimer > 0f)
            return;

        FireVolley();

        doubleShotActive = false;
    }

    // =========================================================
    // VOLLEY
    // =========================================================

    private void FireVolley()
    {
        if (weaponData == null)
            return;
        if (UsesMagazine && (IsReloading || ammo <= 0)) return;
        volleyIndex++;

        Transform firePoint =
            FindFirePoint();

        if (firePoint == null)
        {
            Debug.LogWarning(
                "Weapon: FirePoint was not found in weapon prefab."
            );

            return;
        }

        bool first = UsesMagazine && !firedSinceReload;
        bool last = UsesMagazine && ammo == 1;
        int ammoBefore = ammo;
        if (UsesMagazine) { ammo--; firedSinceReload = true; }
        RunPerk evolution = RunBuildMechanics.Evolution(weaponData);
        if (evolution == RunPerk.SmgOverdrive || evolution == RunPerk.MinigunHeat)
        {
            float gap = Time.time - lastVolleyTime;
            if (gap <= .28f) overdriveCharge = Mathf.Clamp01(overdriveCharge + gap / (evolution == RunPerk.MinigunHeat ? 2f : 1.65f));
        }
        lastVolleyTime = Time.time;
        Vector3 visualMuzzle = GetVisualMuzzle(firePoint).position;
        int count = GetProjectileCount();
        float volleyScale = WeaponData.GetVolleyDamageScale(weaponData.ProjectileCount, count);
        if (evolution == RunPerk.ShotgunSlug && playerStats != null)
        {
            int virtualCount = WeaponData.ResolveProjectileCount(weaponData.ProjectileCount, playerStats.ProjectileCountPercent);
            volleyScale *= 1f + .45f * (virtualCount - weaponData.ProjectileCount) / Mathf.Max(1, weaponData.ProjectileCount);
        }
        for (int i = 0; i < count; i++)
        {
            FireProjectile(firePoint, visualMuzzle, i == 0, volleyScale, first, last, evolution, ammoBefore);
        }

        ApplyRecoil(firePoint);

        ApplyFireShake();
    }

    /// <summary>
    /// Сколько снарядов уходит за один выстрел.
    ///
    /// Бонус Projectile Count — процентный, а не «+1 снаряд»:
    /// у однозарядного оружия прибавка одного снаряда удваивала
    /// урон залпа, а не «немного усиливала» его. Формула и потолок
    /// живут в WeaponData, чтобы фильтр карточек считал ровно то же
    /// самое число.
    /// </summary>
    private int GetProjectileCount()
    {
        int baseCount =
            Mathf.Max(
                weaponData.ProjectileCount,
                1
            );

        RunPerk evolution = RunBuildMechanics.Evolution(weaponData);
        if (evolution == RunPerk.ShotgunSlug) return 1;
        int count = playerStats == null ? baseCount : WeaponData.ResolveProjectileCount(baseCount, playerStats.ProjectileCountPercent);
        return evolution == RunPerk.ShotgunWide ? Mathf.Min(baseCount + 4, count + 2) : count;
    }

    private void ApplyFireShake()
    {
        if (weaponData == null)
            return;

        float amount = weaponData.ShakeOnFire;

        if (amount <= 0f)
            return;

        if (cachedCamera == null)
            cachedCamera = FindAnyObjectByType<CameraFollow>();

        if (cachedCamera != null)
            cachedCamera.AddFireShake(amount);
    }

    private void FireProjectile(
        Transform firePoint,
        Vector3 visualMuzzle,
        bool muzzleFlash,
        float volleyScale,
        bool first, bool last, RunPerk evolution, int ammoBefore)
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning(
                "Weapon: Bullet Prefab is not assigned."
            );

            return;
        }

        Vector3 direction =
            GetProjectileDirection(
                firePoint,
                evolution == RunPerk.ShotgunSlug ? 0f : evolution == RunPerk.ShotgunWide ? weaponData.SpreadAngle * 1.25f : weaponData.SpreadAngle
            );

        Quaternion rotation =
            Quaternion.LookRotation(direction);

        Bullet bullet =
            BulletPool.Spawn(
                bulletPrefab,
                firePoint.position,
                rotation
            );

        if (bullet == null)
            return;

        float damageMultiplier =
            GetDamageMultiplier() * volleyScale * RunBuildMechanics.VolleyDamage(weaponData, first, last, volleyIndex, overdriveCharge, ammoBefore, MagazineCapacity);

        float damage =
            weaponData.Damage *
            damageMultiplier;

        bool isCritical =
            Random.value <
            GetCriticalChance();

        if (isCritical)
        {
            damage *=
                GetCriticalDamageMultiplier();
        }

        float projectileSpeed =
            weaponData.ProjectileSpeed *
            GetProjectileSpeedMultiplier();

        bullet.Initialize(
            damage,
            projectileSpeed,
            isCritical,
            weaponData.PierceCount + GetBonusPierce(),

            weaponData.BurnDamagePerSecond * damageMultiplier * RunBuildMechanics.BurnFactor * (evolution == RunPerk.PlasmaFurnace ? 1.65f / .9f : evolution == RunPerk.InfernoCinders ? 1.35f : 1f),
            weaponData.BurnDuration * (evolution == RunPerk.PlasmaFurnace ? 1.30f : 1f),
            weaponData.BurnTickInterval,

            Mathf.Clamp01(weaponData.LightningChance + (evolution == RunPerk.LightningSurge ? .18f : 0f)),
            weaponData.LightningDamage * damageMultiplier * RunBuildMechanics.LightningFactor * (evolution == RunPerk.LightningSurge ? 1.2f : 1f),
            weaponData.LightningTargets + (evolution == RunPerk.LightningSurge ? 1 : 0),
            weaponData.LightningRange
        );

        bullet.SetWeaponIdentity(weaponData.WeaponName);
        bullet.SetShotIdentity(shotCycle, volleyIndex);
        bullet.SetRunMechanics(this, evolution);
        bullet.SetRunEffects(
            runUpgrades,
            playerHealth,
            bulletPrefab,
            damageMultiplier
        );

        SpawnTracer(
            bullet,
            direction,
            projectileSpeed,
            visualMuzzle,
            muzzleFlash
        );

        PlayFireSound();
    }

    private void PlayFireSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFXVariation(
                sfx.WeaponSound(weaponData != null ? weaponData.WeaponName : null),
                priority: SfxPriority.High
            );
    }

    private void ApplyRecoil(Transform firePoint)
    {
        if (weaponData == null)
            return;

        if (weaponData.RecoilForce <= 0f)
            return;

        if (playerController == null)
            return;

        Vector3 recoilDirection = -firePoint.forward;

        playerController.ApplyRecoil(
            recoilDirection,
            weaponData.RecoilForce
        );
    }

    private Vector3 GetProjectileDirection(
        Transform firePoint,
        float spreadAngle)
    {
        if (spreadAngle <= 0f)
            return firePoint.forward;

        Vector2 randomCircle =
            Random.insideUnitCircle;

        float horizontalAngle =
            randomCircle.x *
            spreadAngle;

        float verticalAngle =
            randomCircle.y *
            spreadAngle;

        Quaternion spreadRotation =
            Quaternion.Euler(
                verticalAngle,
                horizontalAngle,
                0f
            );

        return
            spreadRotation *
            firePoint.forward;
    }

    // =========================================================
    // TRACER
    // =========================================================

    private void SpawnTracer(
        Bullet bullet,
        Vector3 direction,
        float speed,
        Vector3 visualMuzzle,
        bool muzzleFlash)
    {
        bullet.SetVisualTracer(VfxFactory.SpawnTracer(
            bullet.transform.position,
            direction,
            speed,
            bullet.transform,
            tracerPrefab,
            visualMuzzle,
            muzzleFlash,
            bullet.WeaponId
        ));
    }

    // =========================================================
    // WEAPON SWITCH
    // =========================================================

    public void SetWeapon(
        WeaponData newWeaponData)
    {
        if (newWeaponData == null)
            return;

        RemoveCurrentWeaponScoreBonus();

        weaponData =
            newWeaponData;

        RefreshMagazine();

        burstActive = false;
        burstShotsRemaining = 0;
        burstTimer = 0f;

        doubleShotActive = false;
        doubleShotTimer = 0f;

        ApplyWeaponScoreBonus();
        ApplyWeaponVisual();

        PlaySwitchSound();

        Debug.Log(
            $"Weapon equipped: " +
            $"{weaponData.WeaponName} " +
            $"[{weaponData.Rarity}]"
        );
    }

    private void PlaySwitchSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(
                sfx.WeaponSwitch,
                priority: SfxPriority.Medium
            );
    }

    private void ApplyWeaponVisual()
    {
        if (weaponVisualRoot == null)
            return;

        GameObject weaponPrefab =
            testVisualOverride;

        if (weaponPrefab == null && weaponData != null)
            weaponPrefab = weaponData.WeaponPrefab;

        if (activeWeaponVisual != null)
        {
            activeWeaponVisual.SetActive(false);
            Destroy(activeWeaponVisual);
            activeWeaponVisual = null;
        }
        cachedFirePoint = null;
        cachedVisualMuzzles.Clear();
        nextVisualMuzzle = 0;
        foreach (Transform child in weaponVisualRoot)
        {
            child.gameObject.SetActive(false);
        }

        if (weaponPrefab == null)
            return;

        GameObject visual =
            Instantiate(
                weaponPrefab,
                weaponVisualRoot
            );
        activeWeaponVisual = visual;

        visual.transform.localPosition =
            Vector3.zero;

        visual.transform.localRotation =
            Quaternion.identity;

        visual.transform.localScale =
            Vector3.one;
    }

    // =========================================================
    // FIRE POINT
    // =========================================================

    private Transform FindFirePoint()
    {
        if (cachedFirePoint != null)
            return cachedFirePoint;

        if (weaponVisualRoot == null)
            return null;

        cachedVisualMuzzles.Clear();
        nextVisualMuzzle = 0;
        Transform[] children =
            (activeWeaponVisual != null ? activeWeaponVisual.transform : weaponVisualRoot)
                .GetComponentsInChildren<Transform>(
                    false
                );

        foreach (Transform child in children)
        {
            if (child.name == "FirePoint" && cachedFirePoint == null)
                cachedFirePoint = child;
            else if (child.name.StartsWith("VisualMuzzle_", System.StringComparison.Ordinal))
                cachedVisualMuzzles.Add(child);
        }
        if (cachedFirePoint != null) return cachedFirePoint;

        // У префаба оружия может не быть FirePoint — стреляем
        // из точки крепления оружия.
        cachedFirePoint = weaponVisualRoot;

        return cachedFirePoint;
    }

    private Transform GetVisualMuzzle(Transform fallback)
    {
        int count = cachedVisualMuzzles.Count;
        if (count == 0) return fallback;
        Transform muzzle = cachedVisualMuzzles[nextVisualMuzzle];
        nextVisualMuzzle = (nextVisualMuzzle + 1) % count;
        return muzzle != null ? muzzle : fallback;
    }

    // =========================================================
    // PLAYER STATS
    // =========================================================

    private float GetDamageMultiplier()
    {
        if (playerStats == null)
            return 1f;

        return playerStats.DamageMultiplier;
    }

    private float GetFireRateMultiplier()
    {
        float value = playerStats != null ? playerStats.FireRateMultiplier : 1f;
        RunPerk evolution = RunBuildMechanics.Evolution(weaponData);
        if (evolution == RunPerk.RifleRapid) value *= 1.35f;
        if (evolution == RunPerk.BasicSmgDrum) value *= 1.15f;
        if (evolution == RunPerk.SmgOverdrive) value *= 1f + .55f * overdriveCharge;
        return value;
    }

    private int GetBonusPierce()
    {
        RunPerk evolution = RunBuildMechanics.Evolution(weaponData);
        return (playerStats != null ? playerStats.BonusPierce : 0) +
            (evolution == RunPerk.RiflePiercer ? 2 : evolution == RunPerk.ShotgunSlug || evolution == RunPerk.HunterChain ||
            evolution == RunPerk.HeavyRam || evolution == RunPerk.VoidGravity || (evolution == RunPerk.TwinCrossfire && volleyIndex == 2) ? 1 : 0);
    }

    private float GetProjectileSpeedMultiplier()
    {
        if (playerStats == null)
            return 1f;

        return playerStats.ProjectileSpeedMultiplier;
    }

    private float GetCriticalChance()
    {
        float playerChance = 0f;

        if (playerStats != null)
            playerChance =
                playerStats.CriticalChance;

        float weaponChance = 0f;

        if (weaponData != null)
            weaponChance =
                weaponData.CriticalChanceBonus;

        return Mathf.Clamp01(
            playerChance +
            weaponChance + (RunBuildMechanics.Evolution(weaponData) == RunPerk.RifleMarksman ? .20f : RunBuildMechanics.Evolution(weaponData) == RunPerk.GoldenFortune ? .12f : 0f)
        );
    }

    private float GetCriticalDamageMultiplier()
    {
        float multiplier = 2f;

        if (playerStats != null)
            multiplier =
                playerStats.CriticalDamageMultiplier;

        if (weaponData != null)
            multiplier +=
                weaponData.CriticalDamageBonus;

        if (RunBuildMechanics.Evolution(weaponData) == RunPerk.RifleMarksman) multiplier += .5f;
        if (RunBuildMechanics.Evolution(weaponData) == RunPerk.GoldenFortune) multiplier += 1f;

        return Mathf.Max(
            multiplier,
            1f
        );
    }

    // =========================================================
    // SCORE BONUS
    // =========================================================

    private void ApplyWeaponScoreBonus()
    {
        if (weaponData == null)
            return;

        if (playerStats == null)
            return;

        if (appliedWeaponScoreBonus == 0f &&
            weaponData.SpecialType ==
            WeaponSpecialType.ScoreBonus)
        {
            appliedWeaponScoreBonus = weaponData.ScoreBonusPercent;
            playerStats.AddScorePercent(appliedWeaponScoreBonus);
        }
    }

    private void RemoveCurrentWeaponScoreBonus()
    {
        if (playerStats != null && appliedWeaponScoreBonus != 0f)
            playerStats.RemoveScorePercent(appliedWeaponScoreBonus);
        appliedWeaponScoreBonus = 0f;
    }
}
