using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float damage = 5f;

    [Header("Tracer")]
    [Tooltip("Красный трассер как у пуль игрока.")]
    [SerializeField] private bool showTracer = true;

    private Vector3 direction;
    private float lifetimeRemaining;
    private int attackId;

    // Свой трассер этого выстрела. Храним ссылку, чтобы явно
    // отцепить хвост при возврате снаряда в пул (см. ReturnToPool).
    private TracerEffect tracer;

    // Ключ пула (по префабу): пустой = объект живёт вне пула.
    public EntityId? PoolKey { get; internal set; }

    public void Initialize(
        Vector3 newDirection,
        float newDamage,
        float newSpeed,
        Vector3? visualMuzzle = null,
        bool muzzleFlash = false)
    {
        direction = newDirection.normalized;
        attackId = DamageEventId.Next();
        damage = newDamage;
        speed = newSpeed;

        lifetimeRemaining = lifetime;

        if (showTracer)
            SpawnTracer(visualMuzzle, muzzleFlash);
    }

    /// <summary>
    /// Хвост едет вместе со снарядом и гаснет, когда снаряд
    /// вернулся в пул. Своего Update у трассера нет - тикает
    /// общий VfxUpdater, а о возврате снаряда в пул хвосту
    /// сообщает сам снаряд через Detach (см. ReturnToPool), не
    /// полагаясь на опрос activeInHierarchy.
    /// </summary>
    private void SpawnTracer(Vector3? visualMuzzle, bool muzzleFlash)
    {
        tracer = VfxFactory.SpawnEnemyTracer(
            transform.position,
            direction,
            speed,
            transform,
            visualMuzzle,
            muzzleFlash
        );
    }

    private void Awake()
    {
        ApplyGlowMaterial();
    }

    /// <summary>
    /// Тело снаряда раньше было отдельным Sphere-префабом со своим
    /// MeshRenderer, и снаряд стоил 2 draw call: шар плюс хвост.
    /// Теперь круглое ядро нарисовано вторым квадом внутри меша
    /// трассера (см. TracerEffect), поэтому рендерер шара
    /// выключается и остаётся в префабе нерисущим.
    ///
    /// Рендерер НЕ удаляется из префаба намеренно - гашение
    /// обратимо одной строкой.
    /// </summary>
    private void ApplyGlowMaterial()
    {
        MeshRenderer meshRenderer =
            GetComponent<MeshRenderer>();

        if (meshRenderer == null)
            return;

        meshRenderer.enabled = false;
    }

    private void Update()
    {
        lifetimeRemaining -= Time.deltaTime;

        if (lifetimeRemaining <= 0f)
        {
            ReturnToPool();
            return;
        }

        transform.position +=
            direction *
            speed *
            Time.deltaTime;
    }

    public void ReturnToPool()
    {
        lifetimeRemaining = 0f;

        // Сначала отпускаем хвост: он останется в точке попадания
        // и погаснет сам, а снаряд можно безопасно вернуть в пул и
        // тут же выдать под новый выстрел.
        if (tracer != null)
        {
            tracer.Detach(transform);
            tracer = null;
        }

        EnemyProjectilePool.Despawn(this);
    }

    private void OnDisable()
    {
        if (tracer != null)
            tracer.Detach(transform);
        tracer = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (StructureQuery.IsWorldStructure(other))
        {
            ReturnToPool();
            return;
        }

        if (!other.CompareTag("Player"))
            return;

        PlayerHealth playerHealth =
            other.GetComponent<PlayerHealth>();

        if (playerHealth != null)
            playerHealth.TakeDamage(damage, attackId);

        ReturnToPool();
    }
}
