using UnityEngine;

/// <summary>
/// Машина проезжает полосу через всю арену (Город).
///
/// Две фазы: сначала телеграф полосы — MapLaneSweep без урона
/// (статичная полоса с warning), потом сам автомобиль. Машина —
/// куб без коллайдеров и физики на общем аддитивном материале,
/// мчится вдоль оси и сносит врагов и игрока в своей полосе.
///
/// Клиппинг по прямоугольнику: половина длины вдоль движения,
/// половина ширины поперёк. Урон врагам — за каждый тик в полосе,
/// игроку — один раз на машину.
/// </summary>
public class MapTrafficRush : MonoBehaviour
{
    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int LegacyColorId =
        Shader.PropertyToID("_Color");

    private MapLaneSweep.LaneAxis axis =
        MapLaneSweep.LaneAxis.Horizontal;

    private float laneHalfLength = 26f;
    private float carHalfWidth = 1.1f;
    private float carHalfLength = 2.6f;
    private float carSpeed = 42f;
    private float damage = 70f;
    private float knockback = 22f;
    private float warningDuration = 1.5f;
    private Color dangerColor =
        new Color(1f, 0.55f, 0.2f, 1f);

    private Vector3 travelDirection = Vector3.forward;
    private Vector3 startPosition;
    private Vector3 endPosition;

    private float elapsed;
    private bool driving;
    private bool hitPlayer;
    private float tickTimer;

    private GameObject carObject;
    private Transform carTransform;

    /// <summary>
    /// Запускает машину: телеграф полосы, затем проезд автомобиля.
    /// Вызывается менеджером карты.
    /// </summary>
    public static void Launch(
        Vector3 center,
        MapLaneSweep.LaneAxis axis,
        float laneHalfLength,
        float travelDistance,
        float speed,
        float warning,
        float damage,
        float knockback,
        Color color)
    {
        GameObject host = new GameObject("MapTrafficRush");

        MapTrafficRush rush = host.AddComponent<MapTrafficRush>();

        rush.Initialize(
            center,
            axis,
            laneHalfLength,
            travelDistance,
            speed,
            warning,
            damage,
            knockback,
            color
        );
    }

    private void Initialize(
        Vector3 center,
        MapLaneSweep.LaneAxis newAxis,
        float newLaneHalfLength,
        float travelDistance,
        float newSpeed,
        float newWarning,
        float newDamage,
        float newKnockback,
        Color color)
    {
        axis = newAxis;
        laneHalfLength = Mathf.Max(newLaneHalfLength, 1f);
        carSpeed = Mathf.Max(newSpeed, 10f);
        warningDuration = Mathf.Max(newWarning, 0f);
        damage = Mathf.Max(newDamage, 0f);
        knockback = Mathf.Max(newKnockback, 0f);

        if (color.a > 0f ||
            color.r > 0f || color.g > 0f || color.b > 0f)
        {
            dangerColor = color;
            dangerColor.a = 1f;
        }

        if (newAxis == MapLaneSweep.LaneAxis.Horizontal)
            travelDirection = Vector3.forward;
        else
            travelDirection = Vector3.right;

        float halfTravel =
            Mathf.Max(travelDistance * 0.5f, 1f);

        startPosition = center - travelDirection * halfTravel;
        endPosition = center + travelDirection * halfTravel;

        elapsed = 0f;
        driving = false;
        hitPlayer = false;

        TelegraphWarning();
    }

    private void TelegraphWarning()
    {
        // Полоса-предупреждение без урона: рисуется батчером, живёт
        // сама. После warning она вспыхивает и уничтожается.
        GameObject band = new GameObject("MapTrafficWarning");

        band.transform.position = transform.position;

        MapLaneSweep sweep = band.AddComponent<MapLaneSweep>();

        sweep.Initialize(
            transform.position,
            axis,
            laneHalfLength,
            carHalfWidth,
            0f,
            0f,
            0f,
            0f,
            warningDuration,
            dangerColor
        );

        PlayWarningSound();
    }

    private void PlayWarningSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
        {
            AudioManager.Instance.PlaySFXAt(
                sfx.BossAbility,
                transform.position,
                priority: SfxPriority.Medium
            );
        }
    }

    private void Update()
    {
        if (!driving)
        {
            elapsed += Time.deltaTime;

            if (elapsed >= warningDuration)
                StartDriving();

            return;
        }

        Drive();
    }

    private void StartDriving()
    {
        driving = true;
        elapsed = 0f;
        tickTimer = 0f;

        transform.position = startPosition;

        SpawnCarVisual();
    }

    private void SpawnCarVisual()
    {
        carObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carObject.name = "MapTrafficCar";

        Collider collider = carObject.GetComponent<Collider>();

        if (collider != null)
            Destroy(collider);

        float length =
            axis == MapLaneSweep.LaneAxis.Horizontal
                ? carHalfLength * 2f
                : carHalfWidth * 2f;

        float width =
            axis == MapLaneSweep.LaneAxis.Horizontal
                ? carHalfWidth * 2f
                : carHalfLength * 2f;

        Transform car = carObject.transform;

        car.SetParent(transform, false);
        car.localPosition = Vector3.up * 0.45f;
        car.localScale = new Vector3(width, 0.9f, length);

        Renderer renderer = carObject.GetComponent<Renderer>();

        if (renderer != null &&
            VfxSharedAssets.ExplosionSphereMaterial != null)
        {
            Material material = new Material(
                VfxSharedAssets.ExplosionSphereMaterial
            )
            {
                name = "MapTrafficCarMaterial"
            };

            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, dangerColor);
            else if (material.HasProperty(LegacyColorId))
                material.SetColor(LegacyColorId, dangerColor);

            renderer.sharedMaterial = material;
        }
    }

    private void Drive()
    {
        Vector3 carPosition =
            Vector3.MoveTowards(
                transform.position,
                endPosition,
                carSpeed * Time.deltaTime
            );

        transform.position = carPosition;

        tickTimer -= Time.deltaTime;

        if (tickTimer <= 0f)
        {
            tickTimer = 0.06f;
            HitBand();
        }

        if (carPosition == endPosition)
            Destroy(gameObject);
    }

    private void HitBand()
    {
        Vector3 origin = transform.position;

        DamagePlayer(origin);
        CrashEnemies(origin);
    }

    private void DamagePlayer(Vector3 origin)
    {
        if (hitPlayer)
            return;

        PlayerHealth health = PlayerHealth.Instance;

        if (health == null ||
            health.CurrentHealth <= 0f)
        {
            return;
        }

        if (!IsInsideCar(health.transform.position, origin))
            return;

        hitPlayer = true;
        health.TakeDamage(damage);
    }

    private void CrashEnemies(Vector3 origin)
    {
        Enemy[] enemies = Enemy.SnapshotAlive();

        for (int i = 0; i < enemies.Length; i++)
        {
            Enemy enemy = enemies[i];

            if (enemy == null || enemy.IsDead)
                continue;

            if (!IsInsideCar(enemy.transform.position, origin))
                continue;

            enemy.TakeDamage(
                damage,
                false,
                DamageSource.Environment
            );

            if (knockback > 0f)
            {
                enemy.ApplyKnockback(
                    travelDirection,
                    knockback
                );
            }
        }
    }

    private bool IsInsideCar(
        Vector3 worldPosition,
        Vector3 carCenter)
    {
        float forward =
            Mathf.Abs(
                Vector3.Dot(
                    worldPosition - carCenter,
                    travelDirection
                )
            );

        float lateral =
            Mathf.Abs(
                Vector3.Dot(
                    worldPosition - carCenter,
                    axis == MapLaneSweep.LaneAxis.Horizontal
                        ? Vector3.right
                        : Vector3.forward
                )
            );

        return forward <= carHalfLength &&
               lateral <= carHalfWidth;
    }
}