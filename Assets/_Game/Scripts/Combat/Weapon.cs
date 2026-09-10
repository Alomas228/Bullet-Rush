using UnityEngine;
using UnityEngine.InputSystem;

public class Weapon : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField] private WeaponData weaponData;

    [Header("References")]
    [SerializeField] private Transform weaponVisualRoot;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private GameObject tracerPrefab;

    private PlayerStats playerStats;
    private PlayerController playerController;

    private float fireTimer;

    private bool burstActive;
    private int burstShotsRemaining;
    private float burstTimer;

    private bool doubleShotActive;
    private float doubleShotTimer;

    private void Awake()
    {
        playerStats = GetComponentInParent<PlayerStats>();

        if (playerStats == null)
            playerStats = FindAnyObjectByType<PlayerStats>();

        playerController = GetComponentInParent<PlayerController>();

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        ApplyWeaponVisual();
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
    }

    private void Update()
    {
        if (weaponData == null)
            return;

        UpdateFireTimer();
        UpdateBurst();
        UpdateDoubleShot();

        if (canShoot &&
            Mouse.current != null &&
            Mouse.current.leftButton.isPressed)
        {
            TryShoot();
        }
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

        if (burstActive || doubleShotActive)
            return;

        if (fireTimer > 0f)
            return;

        float fireRate =
            weaponData.FireRate *
            GetFireRateMultiplier();

        fireRate = Mathf.Max(fireRate, 0.01f);

        fireTimer = 1f / fireRate;

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

        burstTimer = 0f;

        FireBurstShot();

        burstShotsRemaining--;
    }

    private void UpdateBurst()
    {
        if (!burstActive)
            return;

        burstTimer -= Time.deltaTime;

        if (burstTimer > 0f)
            return;

        if (burstShotsRemaining <= 0)
        {
            burstActive = false;
            return;
        }

        FireBurstShot();

        burstShotsRemaining--;

        burstTimer =
            weaponData.BurstInterval;

        if (burstShotsRemaining <= 0)
            burstActive = false;
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
            weaponData.DoubleShotInterval;

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

        Transform firePoint =
            FindFirePoint();

        if (firePoint == null)
        {
            Debug.LogWarning(
                "Weapon: FirePoint was not found in weapon prefab."
            );

            return;
        }

        int projectileCount =
            Mathf.Max(
                weaponData.ProjectileCount,
                1
            ) + GetBonusProjectiles();

        for (int i = 0; i < projectileCount; i++)
        {
            FireProjectile(firePoint);
        }

        ApplyRecoil(firePoint);
    }

    private void FireProjectile(
        Transform firePoint)
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
                weaponData.SpreadAngle
            );

        Quaternion rotation =
            Quaternion.LookRotation(direction);

        GameObject bulletObject =
            Instantiate(
                bulletPrefab,
                firePoint.position,
                rotation
            );

        Bullet bullet =
            bulletObject.GetComponent<Bullet>();

        if (bullet == null)
            return;

        float damage =
            weaponData.Damage *
            GetDamageMultiplier();

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

            weaponData.BurnDamagePerSecond,
            weaponData.BurnDuration,
            weaponData.BurnTickInterval,

            weaponData.LightningChance,
            weaponData.LightningDamage,
            weaponData.LightningTargets,
            weaponData.LightningRange
        );

        SpawnTracer(bullet.transform);

        PlayFireSound();
    }

    private void PlayFireSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFXVariation(sfx.WeaponFire);
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
        Transform bullet)
    {
        if (tracerPrefab == null)
            return;

        GameObject tracerObject =
            Instantiate(
                tracerPrefab,
                bullet.position,
                Quaternion.identity
            );

        BulletTracer tracer =
            tracerObject.GetComponent<BulletTracer>();

        if (tracer != null)
            tracer.Initialize(bullet);
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
            AudioManager.Instance.PlaySFX(sfx.WeaponSwitch);
    }

    private void ApplyWeaponVisual()
    {
        if (weaponVisualRoot == null)
            return;

        if (weaponData == null)
            return;

        foreach (Transform child in weaponVisualRoot)
        {
            child.gameObject.SetActive(false);
        }

        GameObject weaponPrefab =
            weaponData.WeaponPrefab;

        if (weaponPrefab == null)
            return;

        GameObject visual =
            Instantiate(
                weaponPrefab,
                weaponVisualRoot
            );

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
        if (weaponVisualRoot == null)
            return null;

        Transform[] children =
            weaponVisualRoot
                .GetComponentsInChildren<Transform>(
                    true
                );

        foreach (Transform child in children)
        {
            if (child.name == "FirePoint")
                return child;
        }

        return null;
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
        if (playerStats == null)
            return 1f;

        return playerStats.FireRateMultiplier;
    }

    private int GetBonusPierce()
    {
        if (playerStats == null)
            return 0;

        return playerStats.BonusPierce;
    }

    private int GetBonusProjectiles()
    {
        if (playerStats == null)
            return 0;

        return playerStats.BonusProjectiles;
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
            weaponChance
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

        if (
            weaponData.SpecialType ==
            WeaponSpecialType.ScoreBonus)
        {
            playerStats.AddScorePercent(
                weaponData.ScoreBonusPercent
            );
        }
    }

    private void RemoveCurrentWeaponScoreBonus()
    {
        if (weaponData == null)
            return;

        if (playerStats == null)
            return;

        if (
            weaponData.SpecialType ==
            WeaponSpecialType.ScoreBonus)
        {
            playerStats.RemoveScorePercent(
                weaponData.ScoreBonusPercent
            );
        }
    }
}