using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
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

    private Vector3 recoilOffset;
    [SerializeField] private float recoilDamping = 10f;

    private float dashTimer;
    private float dashCooldownTimer;
    private Vector3 dashDirection;

    private bool spawnGrowInProgress;

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

    private bool inputEnabled;
    private bool subscribed;

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void Start()
    {
        EnsureSubscribed();

        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Menu)
        {
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
        if (subscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;
            subscribed = false;
        }
    }

    private void HandleStateChanged(GameState state)
    {
        inputEnabled = state == GameState.Playing;

        if (!inputEnabled)
        {
            input = Vector2.zero;
            movement = Vector3.zero;
        }
    }

    private void Update()
    {
        if (!inputEnabled)
            return;

        HandleMovementInput();
        HandleAiming();
        UpdateDashTimers();
        TryStartDash();
    }

    private void FixedUpdate()
    {
        if (!inputEnabled)
            return;

        Move();
    }

    private void HandleMovementInput()
    {
        input = Vector2.zero;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.aKey.isPressed)
            input.x -= 1f;

        if (Keyboard.current.dKey.isPressed)
            input.x += 1f;

        if (Keyboard.current.sKey.isPressed)
            input.y -= 1f;

        if (Keyboard.current.wKey.isPressed)
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

        Vector3 newPosition =
            rb.position +
            movement *
            currentSpeed *
            Time.fixedDeltaTime;

        if (recoilOffset.sqrMagnitude > 0.0001f)
        {
            newPosition +=
                recoilOffset;

            recoilOffset =
                Vector3.Lerp(
                    recoilOffset,
                    Vector3.zero,
                    recoilDamping *
                    Time.fixedDeltaTime
                );
        }

        rb.MovePosition(newPosition);

        ResolveStructureOverlap();
    }

    public bool IsDashing => dashTimer > 0f;

    public float DashCooldownRemaining => dashCooldownTimer;

    public float DashCooldown => dashCooldown;

    private void UpdateDashTimers()
    {
        if (dashTimer > 0f)
            dashTimer -= Time.deltaTime;

        if (dashCooldownTimer > 0f)
            dashCooldownTimer -= Time.deltaTime;
    }

    private void TryStartDash()
    {
        if (Keyboard.current == null)
            return;

        if (dashCooldownTimer > 0f)
            return;

        if (dashTimer > 0f)
            return;

        if (!Keyboard.current.spaceKey.wasPressedThisFrame)
            return;

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

        recoilOffset = Vector3.zero;

        RunMetrics rm = FindAnyObjectByType<RunMetrics>();
        if (rm != null)
            rm.RecordDashDodged();

        if (bonusSettings == null)
            bonusSettings =
                FindAnyObjectByType<BonusSettings>();

        ScoreManager sm = ScoreManager.Instance;
        if (sm != null &&
            bonusSettings != null)
        {
            sm.AddBonus(bonusSettings.dashDodgeBonus, "Dash Dodge");
        }
    }

    private void MoveDuringDash()
    {
        float dashSpeed =
            dashDistance /
            dashDuration;

        Vector3 newPosition =
            rb.position +
            dashDirection *
            dashSpeed *
            Time.fixedDeltaTime;

        rb.MovePosition(newPosition);

        ResolveStructureOverlap();
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

            if (distance > 0.05f)
                resolvedPosition += direction * (distance + 0.02f);
        }

        if ((resolvedPosition - rb.position).sqrMagnitude > 0.0001f)
            rb.MovePosition(resolvedPosition);
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

    private void HandleAiming()
    {
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
                Vector3.zero
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