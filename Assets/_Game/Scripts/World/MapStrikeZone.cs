using UnityEngine;

/// <summary>
/// Круговой удар механики карты: метеор космоса или подрыв фугаса.
///
/// Два режима:
///   • warningDuration > 0 — сначала телеграф (пульсирующий круг через
///     DangerZoneBatcher), потом удар с взрывом;
///   • warningDuration == 0 — мгновенный удар (фугас уже телеграфировал
///     свой замах собственной вспышкой, дополнительный круг не нужен).
///
/// Удар бьёт по игроку и опционально по врагам (снимок живых мобов,
/// урон + отброс). Визуал телеграфа и взрыва переиспользует общие
/// системы опасных зон — у зоны нет ни меша, ни рендерера.
/// </summary>
public class MapStrikeZone : MonoBehaviour, IDangerZone
{
    private float radius = 3f;
    private float damage = 30f;
    private float warningDuration = 0f;
    private float knockback = 8f;
    private float screenShake = 0f;
    private bool damagesEnemies = true;
    private bool damagesPlayer = true;
    private bool spawnsGravity;
    private float gravityRadius = 0f;
    private float gravityStrength = 0f;
    private float gravityDuration = 0f;
    private float warningHeight = 18f;
    private GameObject meteorVisual;

    private static Shader meteorShader;
    private static Material meteorMaterial;
    private static Material meteorTrailMaterial;

    private Color dangerColor =
        new Color(1f, 0.45f, 0.1f, 0.5f);

    private bool activated;
    private bool registered;
    private bool damaged;
    private float timer;
    private float flashTimer;
    private float flashDuration = 0.25f;
    private float spinPhase;

    public void Initialize(
        Vector3 position,
        float newRadius,
        float newDamage,
        float newWarning,
        float newKnockback,
        float newScreenShake,
        bool newDamagesEnemies,
        bool newDamagesPlayer,
        Color color,
        bool newSpawnsGravity = false,
        float newGravityRadius = 0f,
        float newGravityStrength = 0f,
        float newGravityDuration = 0f)
    {
        transform.position = position;

        radius = Mathf.Max(newRadius, 0.5f);
        damage = Mathf.Max(newDamage, 0f);
        warningDuration = Mathf.Max(newWarning, 0f);
        knockback = Mathf.Max(newKnockback, 0f);
        screenShake = newScreenShake;
        damagesEnemies = newDamagesEnemies;
        damagesPlayer = newDamagesPlayer;

        spawnsGravity = newSpawnsGravity;
        gravityRadius = newGravityRadius;
        gravityStrength = newGravityStrength;
        gravityDuration = newGravityDuration;

        if (color.a > 0f || color.r > 0f || color.g > 0f || color.b > 0f)
            dangerColor = color;

        timer = 0f;
        activated = false;
        damaged = false;
        flashTimer = 0f;
        spinPhase = Random.value;

        if (warningDuration <= 0f)
            Activate();
        else
        {
            Register();
            SpawnMeteorWarning();
        }
    }

    /// <summary>
    /// Фабрика удара. Возвращает зону (полезно, если нужно ей что-то
    /// докрутить), но чаще результат можно игнорировать.
    /// </summary>
    public static MapStrikeZone Strike(
        Vector3 position,
        float radius,
        float damage,
        bool damagesEnemies = true,
        float knockback = 8f,
        float screenShake = 0f,
        Color color = default,
        float warning = 0f,
        bool gravityWell = false,
        float gravityRadius = 0f,
        float gravityStrength = 0f,
        float gravityDuration = 0f)
    {
        GameObject host = new GameObject("MapStrikeZone");

        MapStrikeZone zone = host.AddComponent<MapStrikeZone>();

        zone.Initialize(
            position,
            radius,
            damage,
            warning,
            knockback,
            screenShake,
            damagesEnemies,
            true,
            color,
            gravityWell,
            gravityRadius,
            gravityStrength,
            gravityDuration
        );

        return zone;
    }

    private void Register()
    {
        if (registered)
            return;

        registered = true;
        DangerZoneBatcher.Register(this);
    }

    private void OnDestroy()
    {
        DangerZoneBatcher.Unregister(this);
    }

    private void SpawnMeteorWarning()
    {
        meteorVisual = new GameObject("MapMeteorWarning");
        meteorVisual.transform.SetParent(transform, false);

        GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "MeteorCore";

        Collider collider = rock.GetComponent<Collider>();

        if (collider != null)
            Destroy(collider);

        rock.transform.SetParent(meteorVisual.transform, false);
        rock.transform.localScale = new Vector3(0.8f, 1.15f, 0.8f);

        Renderer renderer = rock.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = GetMeteorMaterial(
                ref meteorMaterial,
                new Color(0.48f, 0.095f, 0.025f, 1f),
                "MapMeteorMaterial");

        GameObject trail = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        trail.name = "MeteorTrail";

        collider = trail.GetComponent<Collider>();

        if (collider != null)
            Destroy(collider);

        trail.transform.SetParent(meteorVisual.transform, false);
        trail.transform.localPosition = Vector3.up * 0.85f;
        trail.transform.localScale = new Vector3(0.55f, 1.5f, 0.55f);

        renderer = trail.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = GetMeteorMaterial(
                ref meteorTrailMaterial,
                new Color(1f, 0.3f, 0.025f, 1f),
                "MapMeteorTrailMaterial");

        UpdateMeteorWarning();
    }

    private static Material GetMeteorMaterial(
        ref Material cached,
        Color color,
        string name)
    {
        if (cached != null)
            return cached;

        if (meteorShader == null)
            meteorShader = Shader.Find(
                "Universal Render Pipeline/Unlit");

        if (meteorShader == null)
            return null;

        cached = new Material(meteorShader)
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

    private void UpdateMeteorWarning()
    {
        if (meteorVisual == null || activated)
            return;

        float progress = Mathf.Clamp01(
            timer / Mathf.Max(warningDuration, 0.001f));

        float pulse = 1f + Mathf.Sin(Time.time * 11f) * 0.07f;
        float height = Mathf.Lerp(warningHeight, 0.9f, progress);

        meteorVisual.transform.position =
            transform.position + Vector3.up * height;
        meteorVisual.transform.localScale = Vector3.one * pulse;
        meteorVisual.transform.rotation = Quaternion.Euler(
            timer * 110f,
            timer * 170f,
            timer * 80f);
    }

    // =========================================================
    // УДАР
    // =========================================================

    private void Update()
    {
        timer += Time.deltaTime;

        if (!activated)
        {
            UpdateMeteorWarning();

            if (timer >= warningDuration)
                Activate();

            return;
        }

        flashTimer += Time.deltaTime;

        if (flashTimer >= flashDuration)
            Destroy(gameObject);
    }

    private void Activate()
    {
        activated = true;
        flashTimer = 0f;

        if (meteorVisual != null)
            Destroy(meteorVisual);

        Register();

        VfxFactory.TrySpawnExplosion(
            transform.position,
            radius * 1.15f,
            dangerColor
        );

        PlaySound();

        if (screenShake > 0f)
        {
            CameraFollow camera =
                FindAnyObjectByType<CameraFollow>();

            if (camera != null)
                camera.AddShake(screenShake);
        }

        if (!damaged)
        {
            damaged = true;
            ApplyImpact();
        }
    }

    private void ApplyImpact()
    {
        Vector3 origin = transform.position;

        if (damagesPlayer)
            DamagePlayer(origin);

        if (damagesEnemies)
            DamageEnemies(origin);

        if (spawnsGravity &&
            gravityRadius > 0f &&
            gravityDuration > 0f)
        {
            MapGravityCrater.Spawn(
                origin,
                gravityRadius,
                gravityStrength,
                gravityDuration,
                new Color(0.38f, 0.28f, 1f, 1f)
            );
        }
    }

    private void DamagePlayer(Vector3 origin)
    {
        PlayerHealth health = PlayerHealth.Instance;

        if (health == null ||
            health.CurrentHealth <= 0f)
        {
            return;
        }

        if (HorizontalDistance(
                health.transform.position,
                origin) > radius)
        {
            return;
        }

        health.TakeDamage(damage);
    }

    private void DamageEnemies(Vector3 origin)
    {
        Enemy[] enemies = Enemy.SnapshotAlive();

        for (int i = 0; i < enemies.Length; i++)
        {
            Enemy enemy = enemies[i];

            if (enemy == null || enemy.IsDead)
                continue;

            Vector3 toEnemy =
                enemy.transform.position - origin;

            toEnemy.y = 0f;

            if (toEnemy.sqrMagnitude > radius * radius)
                continue;

            enemy.TakeDamage(
                damage,
                false,
                DamageSource.Environment
            );

            if (knockback > 0f &&
                toEnemy.sqrMagnitude > 0.0001f)
            {
                enemy.ApplyKnockback(
                    toEnemy.normalized,
                    knockback
                );
            }
        }
    }

    private static float HorizontalDistance(
        Vector3 a,
        Vector3 b)
    {
        return Vector3.Distance(
            new Vector3(a.x, 0f, a.z),
            new Vector3(b.x, 0f, b.z)
        );
    }

    private void PlaySound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx =
            AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFXAt(
                sfx.BossAoeExplode,
                transform.position,
                priority: SfxPriority.High
            );
    }

    // =========================================================
    // БАТЧЕР: телеграф и вспышка
    // =========================================================

    public bool ZonePlaying =>
        registered && isActiveAndEnabled;

    public Vector3 ZoneOrigin => transform.position;

    public float ZoneBoundsRadius => radius * 1.25f;

    public void AppendZone(DangerZoneBatchBuffer batch)
    {
        float warnProgress = activated
            ? 1f
            : Mathf.Clamp01(
                timer / Mathf.Max(warningDuration, 0.001f));

        float flash = activated
            ? Mathf.Clamp01(
                1f - flashTimer / Mathf.Max(flashDuration, 0.001f))
            : 0f;

        Vector4 color = new Vector4(
            dangerColor.r,
            dangerColor.g,
            dangerColor.b,
            1f);

        Vector4 parameters = new Vector4(
            warnProgress,
            spinPhase,
            activated ? 1f : 0f,
            flash);

        float diameter = radius * 2f;

        batch.Add(
            Matrix4x4.TRS(
                transform.position,
                Quaternion.identity,
                new Vector3(diameter, 1f, diameter)),
            color,
            parameters);
    }
}
