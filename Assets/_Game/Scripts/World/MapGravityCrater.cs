using UnityEngine;

/// <summary>
/// Гравитационная воронка после метеоритного удара (Космос).
///
/// Несколько секунд тянет живых мобов к центру: игрок получает
/// инструмент сгрудить толпу под свой AoE. Тяга идёт через общий
/// кнокбек врага (Enemy.ApplyKnockback) — танки, элиты и боссы
/// остаются невосприимчивы по своему сопротивлению.
///
/// Воронка рисуется как постоянный круг через DangerZoneBatcher,
/// поэтому у неё нет ни меша, ни рендерера.
/// </summary>
public class MapGravityCrater : MonoBehaviour, IDangerZone
{
    private float radius = 4f;
    private float strength = 3f;
    private float duration = 3.5f;
    private float age;

    private Color craterColor =
        new Color(0.45f, 0.35f, 0.9f, 1f);

    private bool registered;
    private float pullTimer;

    public void Initialize(
        Vector3 position,
        float newRadius,
        float newStrength,
        float newDuration,
        Color color)
    {
        transform.position = position;

        radius = Mathf.Max(newRadius, 1f);
        strength = Mathf.Max(newStrength, 0f);
        duration = Mathf.Max(newDuration, 0.1f);

        if (color.a > 0f ||
            color.r > 0f || color.g > 0f || color.b > 0f)
        {
            craterColor = color;
            craterColor.a = 1f;
        }

        age = 0f;
        pullTimer = 0f;

        DangerZoneBatcher.Register(this);
        registered = true;
    }

    public static MapGravityCrater Spawn(
        Vector3 position,
        float radius,
        float strength,
        float duration,
        Color color)
    {
        GameObject host = new GameObject("MapGravityCrater");

        MapGravityCrater crater =
            host.AddComponent<MapGravityCrater>();

        crater.Initialize(
            position,
            radius,
            strength,
            duration,
            color
        );

        return crater;
    }

    private void OnDestroy()
    {
        DangerZoneBatcher.Unregister(this);
    }

    private void Update()
    {
        age += Time.deltaTime;

        if (age >= duration)
        {
            Destroy(gameObject);
            return;
        }

        pullTimer -= Time.deltaTime;

        if (pullTimer > 0f)
            return;

        pullTimer = 0.12f;

        PullEnemies();
    }

    private void PullEnemies()
    {
        Vector3 origin = transform.position;

        Enemy[] enemies = Enemy.SnapshotAlive();

        for (int i = 0; i < enemies.Length; i++)
        {
            Enemy enemy = enemies[i];

            if (enemy == null || enemy.IsDead)
                continue;

            Vector3 toEnemy = enemy.transform.position - origin;

            toEnemy.y = 0f;

            if (toEnemy.sqrMagnitude > radius * radius)
                continue;

            if (toEnemy.sqrMagnitude <= 0.0001f)
                continue;

            enemy.ApplyKnockback(
                -toEnemy.normalized,
                strength * 0.04f
            );
        }
    }

    // =========================================================
    // БАТЧЕР: статичный круг
    // =========================================================

    public bool ZonePlaying =>
        registered && isActiveAndEnabled;

    public Vector3 ZoneOrigin => transform.position;

    public float ZoneBoundsRadius => radius;

    public void AppendZone(DangerZoneBatchBuffer batch)
    {
        float fade =
            1f - Mathf.Clamp01(age / duration);

        Vector4 parameters = new Vector4(
            1f,
            age * 0.35f,
            2f,
            fade * 0.6f);

        float diameter = radius * 2f;

        batch.Add(
            Matrix4x4.TRS(
                transform.position,
                Quaternion.identity,
                new Vector3(diameter, 1f, diameter)),
            craterColor,
            parameters);
    }
}
