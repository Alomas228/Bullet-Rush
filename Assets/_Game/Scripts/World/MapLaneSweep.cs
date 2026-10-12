using UnityEngine;

/// <summary>
/// Прямоугольная полоса механики карты, нарисованная растянутым
/// диском через DangerZoneBatcher (эллипс/полоса без меша).
///
/// Две роли:
///   • статичная полоса с телеграфом — камнепад подрывается по месту
///     (урон по игроку и врагам) или предупреждает про дорогу
///     (трафик города: урон потом наносит машина);
///   • бегущая полоса без паузы — волна прибоя пляжа: едет через
///     арену и расталкивает мобов кнокбеком, без урона.
///
/// Ось выбрана раз и навсегда: HORIZONTAL — полоса тянется по X
/// и едет вдоль Z, VERTICAL — наоборот.
/// </summary>
public class MapLaneSweep : MonoBehaviour, IDangerZone
{
    public enum LaneAxis
    {
        /// <summary>Полоса во всю ширину по X, едет вдоль Z.</summary>
        Horizontal,

        /// <summary>Полоса во всю длину по Z, едет вдоль X.</summary>
        Vertical
    }

    private LaneAxis axis = LaneAxis.Horizontal;
    private float laneHalfLength = 45f;
    private float laneHalfThickness = 1.25f;
    private float damage;
    private float warningDuration;
    private float travelTime;
    private float knockback;
    private Color dangerColor =
        new Color(0.55f, 0.8f, 1f, 0.5f);

    private Vector3 sweepDirection = Vector3.forward;
    private Vector3 startPosition;
    private Vector3 endPosition;

    private bool staticMode;
    private bool activated;
    private bool registered;
    private bool travelled;
    private float timer;
    private float flashTimer;
    private float flashDuration = 0.22f;
    private float phase;
    private float pushTimer;

    public void Initialize(
        Vector3 center,
        LaneAxis newAxis,
        float newHalfLength,
        float newHalfThickness,
        float newTravelDistance,
        float newTravelTime,
        float newDamage,
        float newKnockback,
        float newWarning,
        Color color)
    {
        axis = newAxis;
        laneHalfLength = Mathf.Max(newHalfLength, 1f);
        laneHalfThickness = Mathf.Max(newHalfThickness, 0.1f);
        damage = Mathf.Max(newDamage, 0f);
        warningDuration = Mathf.Max(newWarning, 0f);
        travelTime = Mathf.Max(newTravelTime, 0f);
        knockback = Mathf.Max(newKnockback, 0f);

        if (color.a > 0f ||
            color.r > 0f || color.g > 0f || color.b > 0f)
        {
            dangerColor = color;
            dangerColor.a = 0.5f;
        }

        staticMode = travelTime <= 0f;

        timer = 0f;
        phase = Random.value;

        if (newAxis == LaneAxis.Horizontal)
            sweepDirection = Vector3.forward;
        else
            sweepDirection = Vector3.right;

        if (staticMode)
        {
            transform.position = center;
            Register();
            return;
        }

        float halfTravel = Mathf.Max(newTravelDistance * 0.5f, 1f);

        startPosition = center - sweepDirection * halfTravel;
        endPosition = center + sweepDirection * halfTravel;

        transform.position = startPosition;

        BuildSweepVisual();

        // Оставляем и батчерный путь: если общий слой зон всё-таки
        // рисуется, гребень станет только ярче; если нет - его
        // полностью заменит собственный MeshRenderer.
        Register();
    }

    // =========================================================
    // БЕГУЩАЯ ПОЛОСА: собственный MeshRenderer
    // =========================================================
    //
    // Батчер зон на этом проекте рисует волну без ошибок, но её не
    // видно (общий путь инстансинга тут не читается на светлом
    // песке). Поэтому бегущий гребень рисуется своим материалом и
    // мешем прямо в объекте: это обычный MeshRenderer, он рисуется
    // так же гарантированно, как враги и декор карты.

    private static Material surfMaterial;

    private static Material GetSurfMaterial()
    {
        if (surfMaterial != null)
            return surfMaterial;

        Shader shader =
            Shader.Find("Custom/Bullet Rush Surf Wave");

        if (shader == null)
            return null;

        surfMaterial = new Material(shader)
        {
            name = "SurfWaveVfxMat"
        };

        return surfMaterial;
    }

    private void BuildSweepVisual()
    {
        Material material = GetSurfMaterial();

        if (material == null)
            return;

        float width =
            axis == LaneAxis.Horizontal
                ? laneHalfLength * 2f
                : laneHalfThickness * 2f;

        float depth =
            axis == LaneAxis.Horizontal
                ? laneHalfThickness * 2f
                : laneHalfLength * 2f;

        GameObject visual = new GameObject("SurfVisual");

        visual.transform.SetParent(transform, false);

        // Лента приподнята над полом, иначе ground-меш
        // перекрывает плоский диск ровно на его глубине.
        visual.transform.localPosition =
            new Vector3(0f, 0.09f, 0f);

        visual.transform.localScale =
            new Vector3(width, 1f, depth);

        MeshFilter filter = visual.AddComponent<MeshFilter>();
        filter.sharedMesh = VfxSharedAssets.DangerDiscMesh;

        MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        MaterialPropertyBlock block = new MaterialPropertyBlock();

        block.SetColor(
            WaveColorId,
            new Color(
                dangerColor.r,
                dangerColor.g,
                dangerColor.b,
                0.92f));

        // Размер ленты нужен шейдеру, чтобы пена не растягивалась
        // вместе с диском: она считается в мировых единицах.
        block.SetVector(
            WaveSizeId,
            new Vector4(width, depth, 0f, 0f));

        renderer.SetPropertyBlock(block);
    }

    private static readonly int WaveColorId =
        Shader.PropertyToID("_Color");

    private static readonly int WaveSizeId =
        Shader.PropertyToID("_WaveSize");

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

    private void Update()
    {
        if (staticMode)
        {
            UpdateStatic();
            return;
        }

        UpdateSweep();
    }

    // Камнепад / предупреждение трафика: стоишь, предупреждаешь,
    // потом бьёшь один раз.
    private void UpdateStatic()
    {
        timer += Time.deltaTime;

        if (!activated)
        {
            if (timer >= warningDuration)
                Activate();

            return;
        }

        flashTimer += Time.deltaTime;

        if (flashTimer >= flashDuration)
            Destroy(gameObject);
    }

    // Волна прибоя: едет от одного края до другого и всё время
    // расталкивает мобов в полосе.
    private void UpdateSweep()
    {
        timer += Time.deltaTime;

        float progress =
            Mathf.Clamp01(timer / Mathf.Max(travelTime, 0.01f));

        if (progress >= 1f)
        {
            if (!travelled)
            {
                travelled = true;
                PushBandEnemies();
            }

            Destroy(gameObject);
            return;
        }

        float eased =
            progress * progress * (3f - 2f * progress);

        transform.position =
            Vector3.Lerp(
                startPosition,
                endPosition,
                eased);

        PushBandEnemies();
    }

    private void Activate()
    {
        activated = true;
        flashTimer = 0f;

        HitBand();

        VfxFactory.TrySpawnExplosion(
            transform.position,
            Mathf.Max(laneHalfThickness, 0.5f) *
                Mathf.Max(laneHalfLength, 1f) * 0.18f,
            dangerColor
        );

        if (damage > 0f)
        {
            CameraFollow camera =
                FindAnyObjectByType<CameraFollow>();

            if (camera != null)
                camera.AddShake(0.4f);
        }
    }

    private void HitBand()
    {
        Vector3 origin = transform.position;

        if (damage > 0f)
            DamagePlayer(origin);

        if (damage > 0f || knockback > 0f)
            KnockAndDamageEnemies(origin);
    }

    private void PushBandEnemies()
    {
        if (knockback <= 0f)
            return;

        pushTimer -= Time.deltaTime;

        if (pushTimer > 0f)
            return;

        pushTimer = 0.1f;

        KnockAndDamageEnemies(transform.position);
    }

    private void DamagePlayer(Vector3 origin)
    {
        PlayerHealth health = PlayerHealth.Instance;

        if (health == null ||
            health.CurrentHealth <= 0f)
        {
            return;
        }

        if (!IsInsideBand(health.transform.position, origin))
            return;

        health.TakeDamage(damage);
    }

    private void KnockAndDamageEnemies(Vector3 origin)
    {
        Vector3 normal = sweepDirection;

        Enemy[] enemies = Enemy.SnapshotAlive();

        for (int i = 0; i < enemies.Length; i++)
        {
            Enemy enemy = enemies[i];

            if (enemy == null || enemy.IsDead)
                continue;

            if (!IsInsideBand(enemy.transform.position, origin))
                continue;

            if (damage > 0f)
            {
                enemy.TakeDamage(
                    damage,
                    false,
                    DamageSource.Environment
                );
            }

            if (knockback > 0f)
            {
                Vector3 toEnemy =
                    enemy.transform.position - origin;

                toEnemy.y = 0f;

                Vector3 direction =
                    Vector3.Dot(toEnemy, normal) >= 0f
                        ? normal
                        : -normal;

                enemy.ApplyShove(direction * knockback);
            }
        }
    }

    private bool IsInsideBand(
        Vector3 worldPosition,
        Vector3 bandCenter)
    {
        float dx =
            Mathf.Abs(worldPosition.x - bandCenter.x);

        float dz =
            Mathf.Abs(worldPosition.z - bandCenter.z);

        if (axis == LaneAxis.Horizontal)
        {
            return dx <= laneHalfLength &&
                   dz <= laneHalfThickness;
        }

        return dx <= laneHalfThickness &&
               dz <= laneHalfLength;
    }

    // =========================================================
    // БАТЧЕР: растянутый диск = полоса
    // =========================================================

    public bool ZonePlaying =>
        registered && isActiveAndEnabled;

    public Vector3 ZoneOrigin => transform.position;

    public float ZoneBoundsRadius =>
        Mathf.Max(laneHalfLength, laneHalfThickness);

    public void AppendZone(DangerZoneBatchBuffer batch)
    {
        // Бегущая волна не бьёт и телеграфа у неё нет, поэтому
        // единственный признак для игрока — сам светящийся фронт.
        // Рисуем её сразу яркой «активной» фазой, иначе на светлом
        // песке аддитивный приглушённый вариант почти не виден.
        bool sweeping = !staticMode;

        float warnProgress = activated
            ? 1f
            : Mathf.Clamp01(
                timer / Mathf.Max(warningDuration, 0.001f));

        float flash = activated
            ? Mathf.Clamp01(
                1f - flashTimer / Mathf.Max(flashDuration, 0.001f))
            : 0f;

        float activeFlag =
            (activated || sweeping) ? 1f : 0f;

        float width =
            axis == LaneAxis.Horizontal
                ? laneHalfLength * 2f
                : laneHalfThickness * 2f;

        float depth =
            axis == LaneAxis.Horizontal
                ? laneHalfThickness * 2f
                : laneHalfLength * 2f;

        Vector4 color = new Vector4(
            dangerColor.r,
            dangerColor.g,
            dangerColor.b,
            1f);

        Vector4 parameters = new Vector4(
            warnProgress,
            phase,
            activeFlag,
            flash);

        batch.Add(
            Matrix4x4.TRS(
                transform.position,
                Quaternion.identity,
                new Vector3(width, 1f, depth)),
            color,
            parameters);
    }
}