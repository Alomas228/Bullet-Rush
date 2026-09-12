using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] private float speed = 8f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float damage = 5f;

    private Vector3 direction;

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
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<WorldStructure>() != null)
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