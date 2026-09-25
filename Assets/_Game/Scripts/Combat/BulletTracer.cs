using UnityEngine;

public class BulletTracer : MonoBehaviour
{
    [SerializeField] private float trailLength = 0.45f;
    [SerializeField] private float lifetime = 0.08f;

    private Transform target;
    private LineRenderer lineRenderer;
    private float lifetimeRemaining;

    // Ключ пула (по префабу): пустой = объект живёт вне пула.
    public int PoolKey { get; internal set; }

    public void Initialize(Transform bullet)
    {
        target = bullet;
        lifetimeRemaining = lifetime;
    }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        // Пуля не уничтожается, а возвращается в пул (SetActive(false)).
        // Неактивная цель = пуля «мертва»: трассер сразу уходит.
        if (target == null ||
            !target.gameObject.activeInHierarchy)
        {
            ReturnToPool();
            return;
        }

        lifetimeRemaining -= Time.deltaTime;

        if (lifetimeRemaining <= 0f)
        {
            ReturnToPool();
            return;
        }

        Vector3 bulletPosition = target.position;
        Vector3 direction = target.forward;

        Vector3 trailStart =
            bulletPosition - direction * trailLength;

        Vector3 trailEnd =
            bulletPosition;

        lineRenderer.SetPosition(0, trailStart);
        lineRenderer.SetPosition(1, trailEnd);
    }

    public void ReturnToPool()
    {
        target = null;
        lifetimeRemaining = 0f;

        BulletTracerPool.Despawn(this);
    }
}