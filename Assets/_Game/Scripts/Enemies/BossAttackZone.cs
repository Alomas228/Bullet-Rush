using UnityEngine;

public class BossAttackZone : MonoBehaviour
{
    [SerializeField] private float warningDuration = 2f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float radius = 5f;

    private float timer;
    private bool activated;

    public void Initialize(
        float newDamage,
        float newRadius,
        float newWarningDuration)
    {
        damage = newDamage;
        radius = newRadius;
        warningDuration = newWarningDuration;

        timer = 0f;
        activated = false;

        transform.localScale =
            Vector3.one * radius * 2f;
    }

    private void Update()
    {
        if (activated)
            return;

        timer += Time.deltaTime;

        if (timer >= warningDuration)
        {
            Activate();
        }
    }

    private void Activate()
    {
        activated = true;

        PlayAoeExplodeSound();

        Collider[] targets =
            Physics.OverlapSphere(
                transform.position,
                radius
            );

        foreach (Collider target in targets)
        {
            if (!target.CompareTag("Player"))
                continue;

            PlayerHealth playerHealth =
                target.GetComponent<PlayerHealth>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }
        }

        Destroy(gameObject);
    }

    private void PlayAoeExplodeSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.BossAoeExplode);
    }
}