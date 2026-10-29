using UnityEngine;

/// <summary>
/// Машина проезжает полосу через всю арену (Город).
///
/// Две фазы: сначала телеграф полосы — MapLaneSweep без урона
/// (статичная полоса с warning), потом сам автомобиль. Машина —
/// составная модель без коллайдеров и физики,
/// мчится вдоль оси и сносит врагов и игрока в своей полосе.
///
/// Клиппинг по прямоугольнику: половина длины вдоль движения,
/// половина ширины поперёк. Урон врагам — за каждый тик в полосе,
/// игроку — один раз на машину.
/// </summary>
public class MapTrafficRush : MonoBehaviour
{
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

    private static Shader carShader;
    private static Material carBodyMaterial;
    private static Material carCabinMaterial;
    private static Material carWheelMaterial;
    private static Material carLightMaterial;

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

        // TelegraphWarning reads transform.position; it must match this road.
        transform.position = center;

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
            // Sweep axis describes the band's length; traffic axis describes
            // movement across that band. A road warning runs along movement.
            axis == MapLaneSweep.LaneAxis.Horizontal
                ? MapLaneSweep.LaneAxis.Vertical : MapLaneSweep.LaneAxis.Horizontal,
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
        float travelSize = carHalfLength * 2f;
        float sideSize = carHalfWidth * 2f;
        bool travelsAlongZ =
            axis == MapLaneSweep.LaneAxis.Horizontal;

        float sizeX = travelsAlongZ ? sideSize : travelSize;
        float sizeZ = travelsAlongZ ? travelSize : sideSize;

        carObject = new GameObject("MapTrafficCar");
        carObject.transform.SetParent(transform, false);
        carObject.transform.localPosition = Vector3.up * 0.45f;

        Material bodyMaterial = GetCarMaterial(
            ref carBodyMaterial,
            dangerColor,
            "MapTrafficCarBodyMaterial");

        Material cabinMaterial = GetCarMaterial(
            ref carCabinMaterial,
            new Color(0.06f, 0.1f, 0.14f, 1f),
            "MapTrafficCarCabinMaterial");

        Material wheelMaterial = GetCarMaterial(
            ref carWheelMaterial,
            new Color(0.015f, 0.018f, 0.022f, 1f),
            "MapTrafficCarWheelMaterial");

        Material lightMaterial = GetCarMaterial(
            ref carLightMaterial,
            new Color(2f, 1.55f, 0.55f, 1f),
            "MapTrafficCarLightMaterial");

        CreateCarPart(
            PrimitiveType.Cube,
            "Body",
            carObject.transform,
            Vector3.zero,
            new Vector3(sizeX, 0.75f, sizeZ),
            bodyMaterial);

        CreateCarPart(
            PrimitiveType.Cube,
            "Cabin",
            carObject.transform,
            Vector3.up * 0.48f,
            new Vector3(sizeX * 0.72f, 0.55f, sizeZ * 0.48f),
            cabinMaterial);

        Vector3 side = axis == MapLaneSweep.LaneAxis.Horizontal
            ? Vector3.right
            : Vector3.forward;

        Vector3 front = travelDirection * (carHalfLength * 0.83f);

        CreateCarPart(
            PrimitiveType.Cube,
            "FrontLightA",
            carObject.transform,
            front + side * (carHalfWidth * 0.52f) + Vector3.up * 0.03f,
            new Vector3(
                axis == MapLaneSweep.LaneAxis.Horizontal ? 0.22f : 0.12f,
                0.12f,
                axis == MapLaneSweep.LaneAxis.Horizontal ? 0.12f : 0.22f),
            lightMaterial);

        CreateCarPart(
            PrimitiveType.Cube,
            "FrontLightB",
            carObject.transform,
            front - side * (carHalfWidth * 0.52f) + Vector3.up * 0.03f,
            new Vector3(
                axis == MapLaneSweep.LaneAxis.Horizontal ? 0.22f : 0.12f,
                0.12f,
                axis == MapLaneSweep.LaneAxis.Horizontal ? 0.12f : 0.22f),
            lightMaterial);

        Quaternion wheelRotation =
            axis == MapLaneSweep.LaneAxis.Horizontal
                ? Quaternion.Euler(0f, 0f, 90f)
                : Quaternion.Euler(90f, 0f, 0f);

        CreateCarPart(
            PrimitiveType.Cylinder,
            "WheelFrontA",
            carObject.transform,
            travelDirection * (carHalfLength * 0.68f) +
                side * (carHalfWidth * 0.82f) - Vector3.up * 0.28f,
            new Vector3(0.28f, 0.16f, 0.28f),
            wheelMaterial,
            wheelRotation);

        CreateCarPart(
            PrimitiveType.Cylinder,
            "WheelFrontB",
            carObject.transform,
            travelDirection * (carHalfLength * 0.68f) -
                side * (carHalfWidth * 0.82f) - Vector3.up * 0.28f,
            new Vector3(0.28f, 0.16f, 0.28f),
            wheelMaterial,
            wheelRotation);

        CreateCarPart(
            PrimitiveType.Cylinder,
            "WheelRearA",
            carObject.transform,
            -travelDirection * (carHalfLength * 0.68f) +
                side * (carHalfWidth * 0.82f) - Vector3.up * 0.28f,
            new Vector3(0.28f, 0.16f, 0.28f),
            wheelMaterial,
            wheelRotation);

        CreateCarPart(
            PrimitiveType.Cylinder,
            "WheelRearB",
            carObject.transform,
            -travelDirection * (carHalfLength * 0.68f) -
                side * (carHalfWidth * 0.82f) - Vector3.up * 0.28f,
            new Vector3(0.28f, 0.16f, 0.28f),
            wheelMaterial,
            wheelRotation);
    }

    private static GameObject CreateCarPart(
        PrimitiveType primitive,
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material,
        Quaternion localRotation = default)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = name;

        Collider collider = part.GetComponent<Collider>();

        if (collider != null)
            Destroy(collider);

        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation =
            localRotation == default
                ? Quaternion.identity
                : localRotation;
        part.transform.localScale = localScale;

        Renderer renderer = part.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = material;

        return part;
    }

    private static Material GetCarMaterial(
        ref Material cached,
        Color color,
        string name)
    {
        if (cached != null)
            return cached;

        if (carShader == null)
            carShader = Shader.Find("Universal Render Pipeline/Unlit");

        if (carShader == null)
            return null;

        cached = new Material(carShader)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave
        };

        if (cached.HasProperty("_BaseColor"))
            cached.SetColor("_BaseColor", color);
        else if (cached.HasProperty("_Color"))
            cached.SetColor("_Color", color);

        return cached;
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
