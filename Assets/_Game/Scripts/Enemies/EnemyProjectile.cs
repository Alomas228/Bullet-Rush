using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float damage = 5f;

    [Header("Trail")]
    [Tooltip("Красный след как у игрока, но красный.")]
    [SerializeField] private bool showTrail = true;
    [SerializeField] private float trailLength = 0.7f;
    [SerializeField] private float trailWidth = 0.12f;

    private Vector3 direction;
    private LineRenderer lineRenderer;
    private float lifetimeRemaining;

    // Ключ пула (по префабу): пустой = объект живёт вне пула.
    public int PoolKey { get; internal set; }

    private static Material cachedTrailMaterial;

    public void Initialize(
        Vector3 newDirection,
        float newDamage,
        float newSpeed)
    {
        direction = newDirection.normalized;
        damage = newDamage;
        speed = newSpeed;

        lifetimeRemaining = lifetime;
    }

    private void Awake()
    {
        if (showTrail)
            BuildTrail();
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

        UpdateTrail();
    }

    public void ReturnToPool()
    {
        lifetimeRemaining = 0f;

        EnemyProjectilePool.Despawn(this);
    }

    private void BuildTrail()
    {
        GameObject trailObject =
            new GameObject("EnemyTracer");

        trailObject.transform.SetParent(
            transform,
            false
        );

        lineRenderer =
            trailObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 2;

        lineRenderer.startWidth = trailWidth;
        lineRenderer.endWidth = trailWidth * 0.9f;

        lineRenderer.numCapVertices = 2;

        lineRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        lineRenderer.receiveShadows = false;

        lineRenderer.sharedMaterial =
            GetTrailMaterial();
    }

    private void UpdateTrail()
    {
        if (lineRenderer == null)
            return;

        Vector3 position =
            transform.position;

        lineRenderer.SetPosition(
            0,
            position - direction * trailLength
        );

        lineRenderer.SetPosition(1, position);
    }

    private static Material GetTrailMaterial()
    {
        if (cachedTrailMaterial != null)
            return cachedTrailMaterial;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);

        Color color =
            new Color(1f, 0.3f, 0.25f, 1f);

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);

        mat.name = "EnemyTrailMat";

        cachedTrailMaterial = mat;

        return mat;
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
            playerHealth.TakeDamage(damage);

        ReturnToPool();
    }
}