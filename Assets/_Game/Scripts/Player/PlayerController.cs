using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Dash")]
    [SerializeField] private float dashDistance = 4f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 1.2f;

    [Header("Aiming")]
    [SerializeField] private Camera playerCamera;

    [Header("Spawn Effect")]
    [Tooltip("Длительность «вырастания» игрока в начале волны 1.")]
    [SerializeField] private float spawnGrowDuration = 0.25f;

    private Rigidbody rb;
    private Collider playerCollider;
    private PlayerStats playerStats;

    private Vector2 input;
    private Vector3 movement;
    private Vector3 mapDriftVelocity;

    private Vector3 recoilOffset;
    [SerializeField] private float recoilDamping = 10f;

    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection;

    private bool spawnGrowInProgress;
    private Vector3 menuSpawnPosition;
    private Quaternion menuSpawnRotation;
    private Vector3 menuSpawnScale;
    private float baseDashCooldown;

    public BonusSettings bonusSettings;

    public void PlaySpawnIn()
    {
        if (spawnGrowInProgress)
            return;

        spawnGrowInProgress = true;

        if (playerCollider != null)
            playerCollider.enabled = false;

        if (rb != null)
            rb.useGravity = false;

        StartCoroutine(SpawnGrowRoutine());
    }

    private IEnumerator SpawnGrowRoutine()
    {
        transform.localScale = Vector3.zero;

        float timer = 0f;

        while (timer < spawnGrowDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / spawnGrowDuration
                );

            t = Mathf.SmoothStep(0f, 1f, t);

            transform.localScale =
                Vector3.Lerp(
                    Vector3.zero,
                    Vector3.one,
                    t
                );

            yield return null;
        }

        transform.localScale = Vector3.one;

        if (rb != null)
            rb.useGravity = true;

        if (playerCollider != null)
            playerCollider.enabled = true;

        spawnGrowInProgress = false;
    }

    private void Awake()
    {
        menuSpawnPosition = transform.position;
        menuSpawnRotation = transform.rotation;
        menuSpawnScale = transform.localScale;
        baseDashCooldown = dashCooldown;
        rb = GetComponent<Rigidbody>();

        playerCollider = GetComponent<Collider>();

        rb.isKinematic = true;

        rb.interpolation =
            RigidbodyInterpolation.Interpolate;

        playerStats =
            GetComponent<PlayerStats>();


        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    internal void PrepareMenuPreview()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        StopAllCoroutines();
        spawnGrowInProgress = false;
        inputEnabled = false;
        mapDriftVelocity = Vector3.zero;
        input = Vector2.zero;
        movement = Vector3.zero;
        recoilOffset = Vector3.zero;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
        transform.localScale = menuSpawnScale;
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        RestoreMenuPose(true);
        if (playerCollider != null) playerCollider.enabled = true;
        GetComponentInChildren<ShieldAbility>(true)?.ClearMenuVisual();
        GetComponent<PlayerEquipmentApplier>()?.RefreshMenuAppearance();
        GetComponentInChildren<Weapon>(true)?.RefreshMenuWeapon();
    }

    internal void ResetForSession()
    {
        dashCooldown = baseDashCooldown;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
        dashDirection = Vector3.zero;
        recoilOffset = Vector3.zero;
        input = Vector2.zero;
        movement = Vector3.zero;
        autoAimTarget = null;
        autoAimSearchTimer = 0f;
        autoAimManualUntil = 0f;
        autoAimFrame = -1;
        preciseDodgeAwarded = false;
        dodgedAttacks.Clear();
        recentDodges.Clear();
    }

    private void RestoreMenuPose(bool resetPosition)
    {
        // Rigidbody interpolation retains the previous physics pose when time
        // is paused in the menu. Disable it before changing the visual pose.
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.None;
            if (resetPosition) rb.position = menuSpawnPosition;
            rb.rotation = menuSpawnRotation;
        }
        if (resetPosition) transform.SetPositionAndRotation(menuSpawnPosition, menuSpawnRotation);
        else transform.rotation = menuSpawnRotation;
        Physics.SyncTransforms();
    }

    private bool inputEnabled;
    private bool subscribed;

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void Start()
    {
        EnsureSubscribed();
        if (!GameModeManager.IsPvp) gameObject.AddComponent<JourneyCosmetic>();

        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Menu)
        {
            RestoreMenuPose(false);
            PlaySpawnIn();
        }
    }

    private void EnsureSubscribed()
    {
        if (subscribed)
            return;

        if (GameStateManager.Instance == null)
            return;

        GameStateManager.Instance.OnGameStateChanged += HandleStateChanged;
        subscribed = true;

        inputEnabled = GameStateManager.Instance.CurrentState == GameState.Playing;
    }

    private void OnDisable()
    {
        DashVfxBatcher.EndDash(transform);
        if (subscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;
            subscribed = false;
        }
    }

    private void HandleStateChanged(GameState state)
    {
        inputEnabled = state == GameState.Playing;
        if (!inputEnabled) mapDriftVelocity = Vector3.zero;

        if (state == GameState.Menu) RestoreMenuPose(false);
        else if (state == GameState.Playing && rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.useGravity = true;
        }

        if (!inputEnabled)
        {
            input = Vector2.zero;
            movement = Vector3.zero;
        }
    }

    private void Update()
    {
        if (!inputEnabled || RamLiftArena.InTransit || Time.timeScale <= 0f)
            return;

        HandleMovementInput();
        HandleAiming();
        UpdateDashTimers();
        TryStartDash();
    }

    private void FixedUpdate()
    {
        if (!inputEnabled || RamLiftArena.InTransit)
            return;

        Move();
    }

    private void HandleMovementInput()
    {
        input = Vector2.zero;

        if (MobileControls.IsTouchDevice)
        {
            input = MobileControls.MoveInput;
            movement = MobileControls.WorldDirection(input, playerCamera);
            return;
        }

        if (Keyboard.current == null)
        {
            movement = Vector3.zero;
            return;
        }

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            input.x -= 1f;

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            input.x += 1f;

        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            input.y -= 1f;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            input.y += 1f;

        movement =
            new Vector3(
                input.x,
                0f,
                input.y
            ).normalized;
    }

    private void Move()
    {
        if (dashTimer > 0f)
        {
            MoveDuringDash();
            return;
        }

        float currentSpeed =
            moveSpeed;

        if (playerStats != null)
            currentSpeed =
                playerStats.MoveSpeed;

        Vector3 desiredVelocity = movement * currentSpeed;
        mapDriftVelocity = MapMechanicManager.CurrentMechanic == MapMechanic.LowGravity
            ? Vector3.MoveTowards(mapDriftVelocity, desiredVelocity, currentSpeed * 3.2f * Time.fixedDeltaTime)
            : desiredVelocity;
        Vector3 delta = mapDriftVelocity * Time.fixedDeltaTime;

        if (recoilOffset.sqrMagnitude > 0.0001f)
        {
            delta +=
                recoilOffset;

            recoilOffset =
                Vector3.Lerp(
                    recoilOffset,
                    Vector3.zero,
                    recoilDamping *
                    Time.fixedDeltaTime
                );
        }

        // Превентивное скольжение: свободные оси двигаются,
        // заблокированные стеной — не двигаются вовсе.
        delta = SlideMovement(delta, true);

        MoveOnArena(delta);

        ResolveStructureOverlap();
    }

    public bool IsDashing => dashTimer > 0f;

    /// <summary>
    /// Плоская скорость игрока в мировых единицах за секунду.
    /// Нужна быстрым врагам, чтобы целиться не в текущую точку,
    /// а в упреждение: побежал от рывка по прямой — попал, ушёл
    /// вбок — промах. Свойство простое и не аллоцирует: это
    /// масштабированный вектор направления ввода.
    ///
    /// Во время рывка игрока скорость выше, и здесь она не учтена:
    /// упреждение считается по обычному бегу, поэтому быстрый
    /// враг никогда не ставит «идеальный» выстрел в рывок.
    /// </summary>
    public Vector3 PlanarVelocity
    {
        get
        {
            float speed =
                playerStats != null
                    ? playerStats.MoveSpeed
                    : moveSpeed;

            return MapMechanicManager.CurrentMechanic == MapMechanic.LowGravity ? mapDriftVelocity : movement * speed;
        }
    }

    public float CurrentMoveSpeed =>
        playerStats != null
            ? playerStats.MoveSpeed
            : moveSpeed;

    public float DashCooldownRemaining => dashCooldownTimer;

    public float DashCooldown => dashCooldown;

    /// <summary>
    /// Уменьшает перезарядку рывка на долю (0.04 = -4%).
    /// Персистентные улучшения применяют это на старте забега.
    /// </summary>
    public void ReduceDashCooldown(float percent)
    {
        dashCooldown *= 1f - percent;

        dashCooldown = Mathf.Max(dashCooldown, 0.2f);
    }

    private void UpdateDashTimers()
    {
        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f) DashVfxBatcher.EndDash(transform);
        }

        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;
    }

    private void TryStartDash()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) TryDash();
    }

    public void TryDash()
    {
        if (!inputEnabled || Time.timeScale <= 0f || RamLiftArena.InTransit || JourneyUI.IsBlocking) return;

        if (dashCooldownTimer > 0f)
            return;

        if (dashTimer > 0f)
            return;

        if (MobileControls.IsTouchDevice) HandleMovementInput();

        if (movement.sqrMagnitude > 0.01f)
        {
            dashDirection =
                movement.normalized;
        }
        else
        {
            dashDirection =
                transform.forward;

            dashDirection.y = 0f;

            if (dashDirection.sqrMagnitude <= 0.01f)
                dashDirection = Vector3.forward;
        }

        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        DashVfxBatcher.BeginDash(transform, dashDirection, dashDuration, false);

        recoilOffset = Vector3.zero;

        PlayDashSound();
        preciseDodgeAwarded = false;
    }

    private void MoveDuringDash()
    {
        float dashSpeed =
            dashDistance /
            dashDuration;

        Vector3 delta =
            dashDirection *
            dashSpeed *
            Time.fixedDeltaTime;

        // Дэш скользит по стенам, но пролетает сквозь врагов —
        // иначе из окружённой толпы не выбраться.
        delta = SlideMovement(delta, false);

        MoveOnArena(delta);

        ResolveStructureOverlap();
    }

    private void MoveOnArena(Vector3 delta)
    {
        Vector3 next = rb.position + delta;
        if (RamLiftArena.Active != null)
            next = RamLiftArena.Active.ClampFighter(next, 0.85f);
        rb.MovePosition(next);
    }

    private void ResolveStructureOverlap()
    {
        if (playerCollider == null)
            return;

        Vector3 resolvedPosition = rb.position;

        Vector3 size = playerCollider.bounds.size;

        float checkRadius =
            Mathf.Max(
                size.x,
                Mathf.Max(size.y, size.z)
            ) *
            0.5f +
            1f;

        Collider[] nearby = StructureQuery.OverlapSphere(
            playerCollider.bounds.center,
            checkRadius,
            out int nearbyCount
        );

        for (int i = 0; i < nearbyCount; i++)
        {
            Collider structure = nearby[i];

            if (!StructureQuery.IsWorldStructure(structure))
                continue;

            if (!Physics.ComputePenetration(
                playerCollider,
                resolvedPosition,
                transform.rotation,
                structure,
                structure.transform.position,
                structure.transform.rotation,
                out Vector3 direction,
                out float distance))
            {
                continue;
            }

            if (distance <= 0.005f)
                continue;

            // Выталкиваем строго по горизонтали, иначе при
            // касании верхней грани/угла стены игрока поднимает —
            // он «залезает» на стену и прыгает по ней.
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
                continue;

            direction.Normalize();

            resolvedPosition +=
                direction *
                (distance + 0.005f);
        }

        if ((resolvedPosition - rb.position).sqrMagnitude > 0.0001f)
            rb.MovePosition(resolvedPosition);
    }

    // Превентивное скольжение: если желаемое перемещение ведёт
    // внутрь препятствия (стена или враг), убираем его
    // составляющую по нормали. Тангенциальная часть сохраняется
    // полностью — игрок едет вдоль препятствия на полной скорости.
    private Vector3 SlideMovement(
        Vector3 delta,
        bool includeEnemies)
    {
        if (playerCollider == null ||
            delta.sqrMagnitude <= 0.0001f)
        {
            return delta;
        }

        Vector3 result = delta;

        for (int i = 0; i < 4; i++)
        {
            if (!PenetratesObstacle(
                rb.position + result,
                out Vector3 normal,
                out _,
                includeEnemies))
            {
                break;
            }

            result -=
                normal *
                Vector3.Dot(result, normal);

            if (result.sqrMagnitude <= 0.0001f)
                break;
        }

        return result;
    }

    private bool PenetratesObstacle(
        Vector3 position,
        out Vector3 normal,
        out float depth,
        bool includeEnemies)
    {
        normal = Vector3.zero;
        depth = 0f;

        if (playerCollider == null)
            return false;

        Vector3 center =
            position +
            (playerCollider.bounds.center - rb.position);

        Vector3 size =
            playerCollider.bounds.size;

        float checkRadius =
            Mathf.Max(
                size.x,
                Mathf.Max(size.y, size.z)
            ) *
            0.5f +
            1f;

        Collider[] nearby =
            StructureQuery.OverlapSphere(
                center,
                checkRadius,
                out int nearbyCount
            );

        for (int i = 0; i < nearbyCount; i++)
        {
            Collider obstacle = nearby[i];

            bool isStructure =
                StructureQuery.IsWorldStructure(obstacle);

            bool isEnemy = false;

            if (!isStructure &&
                includeEnemies)
            {
                Enemy enemy =
                    ColliderKindQuery.GetEnemy(obstacle);

                if (enemy != null &&
                    !enemy.IsDead)
                {
                    isEnemy = true;
                }
            }

            if (!isStructure && !isEnemy)
                continue;

            if (Physics.ComputePenetration(
                playerCollider,
                position,
                transform.rotation,
                obstacle,
                obstacle.transform.position,
                obstacle.transform.rotation,
                out Vector3 direction,
                out float distance))
            {
                if (distance > 0.0005f)
                {
                    normal = direction;
                    depth = distance;
                    return true;
                }
            }
        }

        return false;
    }

    public void ApplyRecoil(
        Vector3 direction,
        float force)
    {
        if (IsDashing)
            return;

        Vector3 flatDirection =
            direction;

        flatDirection.y = 0f;

        recoilOffset +=
            flatDirection.normalized *
            Mathf.Max(force, 0f);
    }

    private void PlayDashSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx =
            AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFXVariation(
                sfx.Dash,
                priority: SfxPriority.Medium
            );
    }

    private void HandleAiming()
    {
        if (MobileControls.IsTouchDevice)
        {
            ApplyMobileAim();
            return;
        }
        if (Mouse.current == null ||
            playerCamera == null)
            return;

        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        Ray ray =
            playerCamera.ScreenPointToRay(
                mousePosition
            );

        Plane groundPlane =
            new Plane(
                Vector3.up,
                Vector3.up * RamLiftArena.FloorY
            );

        if (groundPlane.Raycast(
            ray,
            out float distance))
        {
            Vector3 targetPoint =
                ray.GetPoint(distance);

            Vector3 direction =
                targetPoint -
                transform.position;

            direction.y = 0f;

            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation =
                    Quaternion.LookRotation(
                        direction
                    );
            }
        }
    }

}
