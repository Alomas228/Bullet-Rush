using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KillZone : MonoBehaviour
{
    private void Awake()
    {
        Collider zoneCollider =
            GetComponent<Collider>();

        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerHealth playerHealth =
                other.GetComponent<PlayerHealth>();

            if (playerHealth != null)
                playerHealth.Kill();

            return;
        }

        Enemy enemy =
            other.GetComponentInParent<Enemy>();

        if (enemy != null)
            enemy.Kill();
    }
}