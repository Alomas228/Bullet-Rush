using UnityEngine;

// Зона способности босса. Раньше сама строила диск, 16 кубиков
// бордюра и сферу вспышки (до 18 объектов = столько же draw
// call'ов на каждую зону). Теперь вся картинка идёт через общий
// DangerZoneBatcher: у зоны нет ни своего меша, ни материалов, ни
// дочерних объектов, она только считает таймеры и выдаёт батчеру
// один инстанс на кадр.
//
// Префаб приходит с видимым цилиндром-заглушкой: его рендерер
// гасится в Awake, чтобы под батчером не висела вторая геометрия.
public class BossAttackZone : MonoBehaviour, IDangerZone
{
    [Header("Settings")]
    [SerializeField] private float warningDuration = 2f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float radius = 5f;

    [Header("Visual")]
    [SerializeField] private Color dangerColor =
        new Color(1f, 0.15f, 0.1f, 0.45f);

    [Tooltip("Сколько секунд держится кольцо-вспышка после удара.")]
    [SerializeField] private float flashDuration = 0.35f;

    private bool activated;
    private bool registered;

    private float timer;
    private float phaseTime;

    // Случайная фаза вращения бордюра: зоны не крутятся в такт.
    private float spinPhase;

    public void Initialize(
        float newDamage,
        float newRadius,
        float newWarningDuration)
    {
        damage = newDamage;
        radius = newRadius;
        warningDuration = newWarningDuration;

        timer = 0f;
        phaseTime = 0f;
        activated = false;

        spinPhase = Random.value;

        transform.localScale = Vector3.one;

        Register();
    }

    private void Awake()
    {
        HideOriginalChildren();
    }

    private void HideOriginalChildren()
    {
        MeshRenderer[] renderers =
            GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer.transform == transform)
                continue;

            renderer.enabled = false;
        }
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

    public float ZoneBoundsRadius => radius * 1.2f;

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

        float diameter = radius * 2f;

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

        if (phaseTime >= flashDuration)
            Destroy(gameObject);
    }

    private void Activate()
    {
        activated = true;
        phaseTime = 0f;

        DamagePlayer();

        // Вспышка срабатывания: настоящий огненный шар из общего
        // пула (через BlastBatcher, без собственных draw call'ов).
        VfxFactory.TrySpawnExplosion(
            transform.position,
            radius * 1.1f,
            dangerColor
        );

        PlayAoeExplodeSound();
    }

    private void DamagePlayer()
    {
        Collider[] targets =
            StructureQuery.OverlapSphere(
                transform.position,
                radius,
                out int targetCount
            );

        for (int i = 0; i < targetCount; i++)
        {
            Collider target = targets[i];

            if (!target.CompareTag("Player"))
                continue;

            PlayerHealth playerHealth =
                target.GetComponent<PlayerHealth>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }
        }
    }

    private void PlayAoeExplodeSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
        {
            AudioManager.Instance.PlaySFXAt(
                sfx.BossAoeExplode,
                transform.position,
                priority: SfxPriority.High
            );
        }
    }
}
