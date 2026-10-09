using UnityEngine;

// Временная зона урона: сначала пульсирующее предупреждение, потом
// расширяющийся круг, который жжёт игрока, пока он внутри. Игрок
// вынужден покинуть зону — наказание за стояние на месте.
//
// Картинку целиком рисует DangerZoneBatcher одним инстансным
// вызовом на все зоны сцены. У самой зоны нет ни дочерних
// объектов, ни материалов, ни рендереров: она только считает
// таймеры урона и отдаёт батчеру матрицу, цвет и фазы. Раньше
// зона собирала диск, 16 кубиков бордюра и сферу вспышки — до 18
// объектов и столько же draw call'ов на каждую, и залп элит или
// событие волны упирались в отрисовку.
public class HazardZone : MonoBehaviour, IDangerZone
{
    [Header("Settings")]
    [SerializeField] private float warningDuration = 1.4f;
    [SerializeField] private float radius = 3f;
    [SerializeField] private float damagePerSecond = 30f;
    [SerializeField] private float duration = 4f;
    [SerializeField] private float growMultiplier = 1.8f;
    [SerializeField] private float damageTickInterval = 0.5f;

    [Header("Visual")]
    [SerializeField] private Color dangerColor =
        new Color(1f, 0.6f, 0.05f, 0.45f);

    [Tooltip("Сколько секунд держится кольцо-вспышка после срабатывания.")]
    [SerializeField] private float flashDuration = 0.35f;

    private bool activated;
    private bool registered;

    private float timer;
    private float phaseTime;
    private float currentRadius;
    private float damageTickTimer;

    // Случайная фаза вращения бордюра: залп зон не крутится в такт.
    private float spinPhase;

    private PlayerHealth playerHealth;

    public void Initialize(
        float newWarningDuration,
        float newRadius,
        float newDamagePerSecond,
        float newDuration,
        float newGrowMultiplier)
    {
        warningDuration = newWarningDuration;
        radius = newRadius;
        damagePerSecond = newDamagePerSecond;
        duration = newDuration;
        growMultiplier = newGrowMultiplier;

        timer = 0f;
        phaseTime = 0f;
        currentRadius = radius;
        damageTickTimer = 0f;
        activated = false;

        spinPhase = Random.value;

        transform.localScale = Vector3.one;

        Register();
        PlayWarningSound();
    }

    private void Awake()
    {
        playerHealth = PlayerHealth.Instance;
    }

    private void Register()
    {
        if (registered)
            return;

        registered = true;

        DangerZoneBatcher.Register(this);
    }

    private void OnDestroy()
    {
        DangerZoneBatcher.Unregister(this);
    }

    // =========================================================
    // ВЫДАЧА ИНСТАНСА БАТЧЕРУ
    // =========================================================

    public bool ZonePlaying =>
        registered && isActiveAndEnabled;

    public Vector3 ZoneOrigin => transform.position;

    public float ZoneBoundsRadius => currentRadius * 1.2f;

    public void AppendZone(DangerZoneBatchBuffer batch)
    {
        float warnProgress = activated
            ? 1f
            : Mathf.Clamp01(timer / Mathf.Max(warningDuration, 0.001f));

        float flash = activated
            ? Mathf.Clamp01(1f - phaseTime / Mathf.Max(flashDuration, 0.001f))
            : 0f;

        Vector4 color = new Vector4(
            dangerColor.r,
            dangerColor.g,
            dangerColor.b,
            1f);

        Vector4 parameters = new Vector4(
            warnProgress,
            spinPhase,
            activated ? 1f : 0f,
            flash);

        float diameter = currentRadius * 2f;

        batch.Add(
            Matrix4x4.TRS(
                transform.position,
                Quaternion.identity,
                new Vector3(diameter, 1f, diameter)),
            color,
            parameters);
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (!activated)
        {
            if (timer >= warningDuration)
                Activate();

            return;
        }

        phaseTime += Time.deltaTime;

        float progress =
            Mathf.Clamp01(phaseTime / duration);

        float eased =
            progress * progress;

        currentRadius =
            Mathf.Lerp(
                radius,
                radius * Mathf.Max(growMultiplier, 1f),
                eased
            );

        TickDamage();

        if (phaseTime >= duration)
            Destroy(gameObject);
    }

    private void TickDamage()
    {
        damageTickTimer -= Time.deltaTime;

        if (damageTickTimer > 0f)
            return;

        damageTickTimer = damageTickInterval;

        if (playerHealth == null)
            playerHealth = PlayerHealth.Instance;

        if (playerHealth == null ||
            playerHealth.CurrentHealth <= 0f)
        {
            return;
        }

        Vector3 playerPosition =
            playerHealth.transform.position;

        Vector3 zonePosition =
            transform.position;

        float horizontalDistance =
            Vector3.Distance(
                new Vector3(
                    playerPosition.x,
                    0f,
                    playerPosition.z
                ),
                new Vector3(
                    zonePosition.x,
                    0f,
                    zonePosition.z
                )
            );

        if (horizontalDistance > currentRadius)
            return;

        playerHealth.TakeDamage(
            damagePerSecond * damageTickInterval
        );
    }

    private void Activate()
    {
        activated = true;
        phaseTime = 0f;
        currentRadius = radius;

        // Вспышка срабатывания: настоящий огненный шар из общего
        // пула. Он идёт через BlastBatcher, то есть взрыв тоже не
        // стоит ни одного собственного draw call'а.
        VfxFactory.TrySpawnExplosion(
            transform.position,
            radius * 1.1f,
            dangerColor
        );

        PlayActivateSound();
    }

    private void PlayWarningSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx =
            AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFXAt(
                sfx.BossAbility,
                transform.position,
                priority: SfxPriority.Medium
            );
    }

    private void PlayActivateSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx =
            AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFXAt(
                sfx.BossAoeExplode,
                transform.position,
                priority: SfxPriority.High
            );
    }
}
