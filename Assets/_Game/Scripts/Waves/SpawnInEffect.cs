using UnityEngine;

// «Материализация» врага при появлении: объект растёт из нуля до полного
// размера, а пока растёт — не имеет коллайдеров и не управляется Enemy,
// то есть не может атаковать или получить урон в момент появления.
public class SpawnInEffect : MonoBehaviour
{
    [Tooltip("Длительность «материализации» врага.")]
    [SerializeField] private float duration = 0.35f;

    [Tooltip("Насколько масштаб «всплёскивает» выше 1 в середине роста.")]
    [SerializeField] private float overshoot = 1.12f;

    private float currentDuration = 0.35f;
    private float elapsed;
    private Vector3 originalScale = Vector3.one;
    private Collider[] colliders;
    private Enemy enemy;
    private bool completed;

    private void Awake()
    {
        originalScale = transform.localScale;
        currentDuration = Mathf.Max(duration, 0.01f);

        colliders = GetComponentsInChildren<Collider>(true);
        enemy = GetComponent<Enemy>();

        SetActive(false);
        transform.localScale = Vector3.zero;
    }

    public void Initialize(float spawnDuration)
    {
        currentDuration = Mathf.Max(spawnDuration, 0.01f);
    }

    private void SetActive(bool active)
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = active;
        }

        if (enemy != null)
            enemy.enabled = active;
    }

    private void Update()
    {
        if (completed)
            return;

        elapsed += Time.deltaTime;

        float t = Mathf.Clamp01(elapsed / currentDuration);
        float eased = t * t * (3f - 2f * t);
        float overshootFactor =
            1f +
            (overshoot - 1f) *
            Mathf.Sin(t * Mathf.PI);

        transform.localScale = originalScale * (eased * overshootFactor);

        if (t >= 1f)
        {
            completed = true;
            End();
        }
    }

    private void End()
    {
        SetActive(true);
        transform.localScale = originalScale;
        Destroy(this);
    }

    private void OnDestroy()
    {
        if (completed)
            return;

        SetActive(true);
        transform.localScale = originalScale;
    }
}