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

    private static Material cachedTrailMaterial;

    public void Initialize(
        Vector3 newDirection,
        float newDamage,
        float newSpeed)
    {
        direction = newDirection.normalized;
        damage = newDamage;
        speed = newSpeed;

        Debug.Log(
            $"Enemy projectile initialized. " +
            $"Direction: {direction}, " +
            $"Speed: {speed}, " +
            $"Damage: {damage}"
        );
    }

    private void Awake()
    {
        if (showTrail)
            BuildTrail();
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.position +=
            direction *
            speed *
            Time.deltaTime;

        UpdateTrail();
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

        lineRenderer.material =
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
            Destroy(gameObject);
            return;
        }

        if (!other.CompareTag("Player"))
            return;

        PlayerHealth playerHealth =
            other.GetComponent<PlayerHealth>();

        if (playerHealth != null)
            playerHealth.TakeDamage(damage);

        Destroy(gameObject);
    }
}