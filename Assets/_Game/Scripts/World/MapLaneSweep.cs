using UnityEngine;

/// <summary>
/// Полосы механик карты: статичные телеграфы используют
/// DangerZoneBatcher, а прибой рисуется отдельной объемной геометрией.
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
    private Mesh surfMesh;
    private GameObject[] boulderVisuals;
    private float[] boulderStartHeights;

    private static Shader boulderShader;
    private static Material boulderMaterial;

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

            if (damage > 0f && warningDuration > 0f)
                BuildBoulderWarningVisual();

            Register();
            return;
        }

        float halfTravel = Mathf.Max(newTravelDistance * 0.5f, 1f);

        startPosition = center - sweepDirection * halfTravel;
        endPosition = center + sweepDirection * halfTravel;

        transform.position = startPosition;

        if (!BuildSweepVisual())
            Register();
    }

    // =========================================================
    // БЕГУЩАЯ ПОЛОСА: собственный MeshRenderer
    // =========================================================
    //
    // Объемный вал рисуется отдельным MeshRenderer, а не общим
    // диском опасных зон: у него настоящая геометрия поперечного сечения.

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

    private bool BuildSweepVisual()
    {
        Material material = GetSurfMaterial();

        if (material == null)
            return false;

        GameObject visual = new GameObject("SurfVisual");

        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        visual.transform.localRotation =
            axis == LaneAxis.Horizontal
                ? Quaternion.identity
                : Quaternion.Euler(0f, 90f, 0f);

        MeshFilter filter = visual.AddComponent<MeshFilter>();
        surfMesh = BuildSurfMesh();

        if (surfMesh == null)
            return false;

        filter.sharedMesh = surfMesh;

        MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        MaterialPropertyBlock block = new MaterialPropertyBlock();

        block.SetColor(
            WaveColorId,
            new Color(0.78f, 0.96f, 1f, 0.88f));

        float height = Mathf.Max(laneHalfThickness * 0.5f, 1.2f);

        block.SetVector(
            WaveSizeId,
            new Vector4(
                laneHalfLength * 2f,
                laneHalfThickness,
                height,
                0f));

        renderer.SetPropertyBlock(block);

        return true;
    }

    private Mesh BuildSurfMesh()
    {
        const int lengthSegments = 96;
        const int profileCurveCount = 6;
        const int samplesPerCurve = 8;
        const int radialSegments = profileCurveCount * samplesPerCurve;

        Vector2[] profileCurves =
        {
            new Vector2(-1f, 0.035f),
            new Vector2(-0.78f, 0.045f),
            new Vector2(-0.62f, 0.10f),
            new Vector2(-0.40f, 0.22f),

            new Vector2(-0.40f, 0.22f),
            new Vector2(-0.18f, 0.42f),
            new Vector2(-0.08f, 0.86f),
            new Vector2(0.20f, 0.96f),

            new Vector2(0.20f, 0.96f),
            new Vector2(0.48f, 1.08f),
            new Vector2(0.84f, 1.02f),
            new Vector2(0.87f, 0.80f),

            new Vector2(0.87f, 0.80f),
            new Vector2(0.84f, 0.60f),
            new Vector2(0.51f, 0.58f),
            new Vector2(0.39f, 0.43f),

            new Vector2(0.39f, 0.43f),
            new Vector2(0.55f, 0.25f),
            new Vector2(0.80f, 0.08f),
            new Vector2(0.96f, 0.035f),

            new Vector2(0.96f, 0.035f),
            new Vector2(0.30f, 0.025f),
            new Vector2(-0.35f, 0.025f),
            new Vector2(-1f, 0.035f)
        };

        int rowSize = radialSegments + 1;
        int sideVertexCount = (lengthSegments + 1) * rowSize;
        int vertexCount = sideVertexCount + 2;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int sideTriangleCount = lengthSegments * radialSegments * 2;
        int triangleCount = sideTriangleCount + radialSegments * 2;
        int[] triangles = new int[triangleCount * 3];

        float length = laneHalfLength * 2f;
        float radiusZ = laneHalfThickness * 0.94f;
        float height = Mathf.Max(laneHalfThickness * 0.5f, 1.2f);

        for (int x = 0; x <= lengthSegments; x++)
        {
            float u = x / (float)lengthSegments;
            float along = Mathf.Lerp(-length * 0.5f, length * 0.5f, u);

            for (int ring = 0; ring <= radialSegments; ring++)
            {
                float v = ring / (float)radialSegments;
                Vector2 profile = EvaluateSurfProfile(
                    profileCurves,
                    profileCurveCount,
                    v);
                int index = x * rowSize + ring;

                vertices[index] = new Vector3(
                    along,
                    profile.y * height,
                    profile.x * radiusZ);
                uvs[index] = new Vector2(u, v);

                if (x == lengthSegments || ring == radialSegments)
                    continue;

                int a = index;
                int b = index + rowSize;
                int c = index + 1;
                int d = index + rowSize + 1;
                int triangle = (x * radialSegments + ring) * 6;

                triangles[triangle] = a;
                triangles[triangle + 1] = c;
                triangles[triangle + 2] = b;
                triangles[triangle + 3] = c;
                triangles[triangle + 4] = d;
                triangles[triangle + 5] = b;
            }
        }

        int startCenter = sideVertexCount;
        int endCenter = sideVertexCount + 1;
        vertices[startCenter] = new Vector3(-length * 0.5f, height * 0.5f, 0f);
        vertices[endCenter] = new Vector3(length * 0.5f, height * 0.5f, 0f);

        for (int ring = 0; ring < radialSegments; ring++)
        {
            int start = ring;
            int startNext = ring + 1;
            int end = lengthSegments * rowSize + ring;
            int endNext = end + 1;
            int startTriangle = (sideTriangleCount + ring * 2) * 3;
            int endTriangle = startTriangle + 3;

            triangles[startTriangle] = startCenter;
            triangles[startTriangle + 1] = startNext;
            triangles[startTriangle + 2] = start;
            triangles[endTriangle] = endCenter;
            triangles[endTriangle + 1] = end;
            triangles[endTriangle + 2] = endNext;
        }

        Mesh mesh = new Mesh
        {
            name = "SurfWaveVolume",
            hideFlags = HideFlags.HideAndDontSave
        };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Bounds bounds = mesh.bounds;
        bounds.Expand(new Vector3(0f, height * 0.35f, radiusZ * 0.2f));
        mesh.bounds = bounds;

        return mesh;
    }

    private static Vector2 EvaluateSurfProfile(
        Vector2[] curves,
        int curveCount,
        float t)
    {
        float curvePosition = Mathf.Clamp(t * curveCount, 0f, curveCount);
        int curveIndex = Mathf.Min(
            Mathf.FloorToInt(curvePosition),
            curveCount - 1);
        float localT = curvePosition >= curveCount
            ? 1f
            : curvePosition - curveIndex;
        int point = curveIndex * 4;

        Vector2 p0 = curves[point];
        Vector2 p1 = curves[point + 1];
        Vector2 p2 = curves[point + 2];
        Vector2 p3 = curves[point + 3];
        float inverseT = 1f - localT;

        return inverseT * inverseT * inverseT * p0 +
               3f * inverseT * inverseT * localT * p1 +
               3f * inverseT * localT * localT * p2 +
               localT * localT * localT * p3;
    }

    private void BuildBoulderWarningVisual()
    {
        const int count = 7;

        Material material = GetBoulderMaterial();

        if (material == null)
            return;

        boulderVisuals = new GameObject[count];
        boulderStartHeights = new float[count];

        for (int i = 0; i < count; i++)
        {
            GameObject rock = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            rock.name = "BoulderWarningRock";

            Collider collider = rock.GetComponent<Collider>();

            if (collider != null)
                Destroy(collider);

            rock.transform.SetParent(transform, false);

            float along = Mathf.Lerp(
                -laneHalfLength * 0.82f,
                laneHalfLength * 0.82f,
                i / (float)(count - 1));

            float cross = Random.Range(
                -laneHalfThickness * 0.24f,
                laneHalfThickness * 0.24f);

            boulderStartHeights[i] = Random.Range(5.5f, 9f);
            rock.transform.localPosition = axis == LaneAxis.Horizontal
                ? new Vector3(along, boulderStartHeights[i], cross)
                : new Vector3(cross, boulderStartHeights[i], along);

            float size = Random.Range(0.75f, 1.25f);
            rock.transform.localScale = new Vector3(
                size * Random.Range(0.85f, 1.25f),
                size * Random.Range(0.75f, 1.2f),
                size * Random.Range(0.85f, 1.25f));

            rock.transform.localRotation = Quaternion.Euler(
                Random.Range(0f, 360f),
                Random.Range(0f, 360f),
                Random.Range(0f, 360f));

            Renderer renderer = rock.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            boulderVisuals[i] = rock;
        }
    }

    private static Material GetBoulderMaterial()
    {
        if (boulderMaterial != null)
            return boulderMaterial;

        if (boulderShader == null)
            boulderShader = Shader.Find(
                "Universal Render Pipeline/Unlit");

        if (boulderShader == null)
            return null;

        boulderMaterial = new Material(boulderShader)
        {
            name = "MapBoulderWarningMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };

        Color color = new Color(0.36f, 0.34f, 0.31f, 1f);

        if (boulderMaterial.HasProperty("_BaseColor"))
            boulderMaterial.SetColor("_BaseColor", color);
        else if (boulderMaterial.HasProperty("_Color"))
            boulderMaterial.SetColor("_Color", color);

        return boulderMaterial;
    }

    private void UpdateBoulderWarning()
    {
        if (boulderVisuals == null)
            return;

        float progress = Mathf.Clamp01(
            timer / Mathf.Max(warningDuration, 0.001f));

        for (int i = 0; i < boulderVisuals.Length; i++)
        {
            GameObject rock = boulderVisuals[i];

            if (rock == null)
                continue;

            Vector3 position = rock.transform.localPosition;
            position.y = Mathf.Lerp(
                boulderStartHeights[i],
                0.5f,
                progress * progress);
            rock.transform.localPosition = position;
            rock.transform.Rotate(
                new Vector3(38f, 64f, 29f) * Time.deltaTime,
                Space.Self);
        }
    }

    private void ClearBoulderWarning()
    {
        if (boulderVisuals == null)
            return;

        for (int i = 0; i < boulderVisuals.Length; i++)
        {
            if (boulderVisuals[i] != null)
                Destroy(boulderVisuals[i]);
        }

        boulderVisuals = null;
        boulderStartHeights = null;
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

        if (surfMesh != null)
            Destroy(surfMesh);
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
            UpdateBoulderWarning();

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
        ClearBoulderWarning();

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
        if (knockback <= 0f && staticMode)
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

            // Only the travelling surf applies wetness, not static rock lanes
            // or road telegraphs. Resistance to shove does not prevent wetness.
            if (!staticMode)
                enemy.ApplyWet();

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
