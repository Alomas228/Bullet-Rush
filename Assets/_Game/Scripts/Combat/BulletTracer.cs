using UnityEngine;

public class BulletTracer : MonoBehaviour
{
    [SerializeField] private float trailLength = 0.45f;
    [SerializeField] private float lifetime = 0.08f;

    private Transform target;
    private LineRenderer lineRenderer;

    public void Initialize(Transform bullet)
    {
        target = bullet;
    }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
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

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}