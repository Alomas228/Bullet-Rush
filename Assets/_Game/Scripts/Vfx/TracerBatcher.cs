using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Один draw call на все трассеры пуль, а не по одному на каждый.
///
/// Раньше у каждого TracerEffect был свой MeshRenderer, поэтому
/// сотня пуль в воздухе давала сотню драфколлов. Сейчас объекты
/// пула не рисуют ничего, а батчер собирает их матрицы в два
/// массива и отправляет два вызова: хвосты и ядра.
///
/// ПОЧЕМУ ДВА ВЫЗОВА, А НЕ ОДИН. Раньше хвост и ядро жили в одном
/// меше и отличались только формой в шейдере, а длина хвоста и
/// размер ядра были зашиты прямо в вершины. С GPU-инстансингом
/// все инстансы обязаны иметь ОДИН И ТОТ ЖЕ меш, иначе их нечем
/// будет собрать в один вызов. Зашить разные размеры в вершины
/// больше нельзя, зато можно в матрицу инстанса - поэтому хвост
/// и ядро стали двумя разными квадами и двумя вызовами.
/// Цена: +1 драфколл. Выигрыш: было N, стало 2.
///
/// Инстанс-шейдер не менялся: у квада хвоста UV1.x = -1, у квада
/// ядра UV1.x = +1, то есть ровно те же значения, что раньше
/// стояли в меше двух квадов. Фрагментный шейдер не отличает
/// хвост от ядра вообще.
///
/// Стоимость на кадр: один проход по активным трассерам, без
/// аллокаций (массивы растут только при переполнении) и без
/// сортировки прозрачных - трассеры аддитивные, порядок не важен.
/// </summary>
[DefaultExecutionOrder(300)]
public sealed class TracerBatcher : MonoBehaviour
{
    /// <summary>
    /// Graphics.RenderMeshInstanced с массивом Matrix4x4 кладёт в
    /// данные инстанса и objectToWorld, и worldToObject, поэтому
    /// предел - 511, а не 1023. Превышение кидает
    /// InvalidOperationException, а не рисует часть батча, так
    /// что длинные очереди режутся на чанки по этому пределу.
    /// Пул прогревается на 160, реально в воздухе ~84 пули, так
    /// что на практике чанков всегда ровно один.
    /// </summary>
    private const int MaxPerDraw = 511;

    /// <summary>
    /// Запас под матрицы с двух сторон: хвост игрока плюс хвост
    /// врага в одном массиве. Та же величина, что InitialCapacity
    /// у VfxUpdater, - на максимальной стрельбе обе стороны
    /// укладываются без единого resize.
    /// </summary>
    private const int InitialCapacity = 256;

    private static TracerBatcher instance;

    private static readonly List<TracerEffect> active =
        new List<TracerEffect>(InitialCapacity);

    private static Matrix4x4[] tailMatrices =
        new Matrix4x4[InitialCapacity];

    private static Matrix4x4[] coreMatrices =
        new Matrix4x4[InitialCapacity * 2];

    /// <summary>
    /// Ставит трассер в очередь на отрисовку. Зовётся из
    /// TracerEffect.Play - то есть ровно тогда, когда пул выдаёт
    /// объект под выстрел.
    ///
    /// Дублей быть не может: пул не отдаёт объект, не вернув его
    /// обратно, а Unregister убирает его из списка по ссылке.
    /// Именно поэтому здесь НЕТ проверки "уже зарегистрирован":
    /// после перезагрузки домена статический список пуст, а флаг
    /// Batched на живых трассерах остался true. Проверка по флагу
    /// молча выкинула бы их из очереди навсегда - до конца жизни.
    /// </summary>
    public static void Register(TracerEffect tracer)
    {
        if (tracer == null)
            return;

        EnsureExists();

        tracer.Batched = true;
        active.Add(tracer);
    }

    /// <summary>
    /// Снимает трассер с отрисовки. Убираем по ссылке, а не только
    /// сбрасываем флаг: список сам является источником истины, и
    /// сброшенный флаг не даст отрисовать лишний инстанс.
    ///
    /// Поиск по списку на каждой смерти пули дёшево: список
    /// короткий, а умерших трассеров за кадр единицы.
    /// </summary>
    public static void Unregister(TracerEffect tracer)
    {
        if (tracer == null)
            return;

        tracer.Batched = false;
        active.Remove(tracer);
    }

    private static void EnsureExists()
    {
        // == null срабатывает и на уничтоженный объект, поэтому
        // батчер сам себя восстанавливает после смены сцены.
        if (instance != null)
            return;

        GameObject batcherObject = new GameObject("TracerBatcher");

        instance = batcherObject.AddComponent<TracerBatcher>();

        Object.DontDestroyOnLoad(batcherObject);
    }

    private void Awake()
    {
        instance = this;
        InitializeThemedBatch();
    }

    private void OnDestroy()
    {
        if (deathMesh != null) Destroy(deathMesh);
        if (deathMaterial != null) Destroy(deathMaterial);
        if (themedMesh != null) Destroy(themedMesh);
        if (themedMaterial != null) Destroy(themedMaterial);
        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// LateUpdate, а не Update: трассерами управляет VfxUpdater из
    /// Update, и их позиции и фаза затухания должны поспеть сюда
    /// раньше, чем батчер снимет матрицы. Порядок фаз Unity
    /// гарантирует сам - все Update выполняются раньше всех
    /// LateUpdate, независимо от DefaultExecutionOrder.
    /// </summary>
    private void LateUpdate()
    {
        if (themedMesh != null) RenderThemedBatch();
        else DrawLegacy();
        RenderDeathBodies();
    }

    private void DrawLegacy()
    {
        // После пересоздания статиков старый экземпляр остаётся в
        // сцене живым. Если бы он продолжал рисовать, трассеры
        // ушли бы в кадр дважды.
        if (instance != this)
        {
            enabled = false;
            return;
        }

        int total = active.Count;

        if (total == 0)
            return;

        EnsureCapacity(total);

        // Первый проход чистит список и считает игроков и врагов
        // РАЗДЕЛЬНО, плюс копит общий AABB.
        //
        // Слот для матрицы нельзя было вычислять по ходу этого
        // прохода (playerCount + enemyCount++): активный список
        // идёт в порядке спавна, и пули игрока с вражескими в нём
        // перемешаны. Стоило врагу попасть в список раньше хотя бы
        // одного игрока или вклиниться между ними — его матрица
        // ложилась в чужой индекс, затирала чужие данные, и после
        // этого весь батч разъезжался: часть трассеров рисовалась
        // чужим цветом, часть — из устаревшей матрицы прошлого
        // кадра (телепорт и «остановка»), часть пропадала вовсе.
        // Сам снаряд при этом продолжал лететь и бить по игроку —
        // его логика от батчера не зависит. Поэтому индексы
        // раздаём детерминированно вторым проходом.
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        int kept = 0;
        int playerCount = 0;
        int playerFlashCount = 0;
        int enemyFlashCount = 0;

        for (int read = 0; read < total; read++)
        {
            TracerEffect tracer = active[read];

            // Три способа попасть сюда: пуля умерла (Unregister),
            // объект разрушен сменой сцены, либо релоад домена
            // обнулил статики, но оставив флаг на объекте.
            if (tracer == null || !tracer.Batched || !tracer.IsPlaying)
                continue;

            // Сжатие на месте: элемент, переехавший в kept, уже
            // был обработан выше по индексу, а хвост списка затираем
            // один раз в конце.
            active[kept++] = tracer;

            if (!tracer.EnemyStyle)
            {
                playerCount++;
                if (tracer.HasMuzzleFlash) playerFlashCount++;
            }
            else if (tracer.HasMuzzleFlash) enemyFlashCount++;

            Vector3 position = tracer.transform.position;

            if (position.x < min.x) min.x = position.x;
            if (position.y < min.y) min.y = position.y;
            if (position.z < min.z) min.z = position.z;

            if (position.x > max.x) max.x = position.x;
            if (position.y > max.y) max.y = position.y;
            if (position.z > max.z) max.z = position.z;
        }

        if (kept != total)
            active.RemoveRange(kept, total - kept);

        // Все записи оказались мёртвыми. Возвращаться раньше нужно
        // ещё и потому, что min/max остались бы в исходных
        // экстремумах, а из них получается Bounds с
        // отрицательным размером - такой объект нельзя отдавать
        // в отсечение.
        if (kept == 0)
            return;

        // Второй проход: игроки ложатся строго с нуля, враги —
        // строго с playerCount. Теперь у каждого куска ровно своя
        // область, и DrawBatch читает ровно то, что записал.
        int playerSlot = 0;
        int enemySlot = playerCount;
        int enemyCount = kept - playerCount;
        int playerFlashSlot = playerCount;
        int playerCoreEnd = playerCount + playerFlashCount;
        int enemyCoreSlot = playerCoreEnd;
        int enemyFlashSlot = playerCoreEnd + enemyCount;

        for (int i = 0; i < kept; i++)
        {
            TracerEffect tracer = active[i];
            Transform tracerTransform = tracer.transform;

            tracer.BuildMatrices(
                tracerTransform.position,
                out Matrix4x4 tail,
                out Matrix4x4 core);

            if (tracer.EnemyStyle)
            {
                tailMatrices[enemySlot] = tail;
                coreMatrices[enemyCoreSlot++] = core;
                if (tracer.TryBuildMuzzleFlash(out Matrix4x4 enemyFlash))
                    coreMatrices[enemyFlashSlot++] = enemyFlash;
                enemySlot++;
            }
            else
            {
                tailMatrices[playerSlot] = tail;
                coreMatrices[playerSlot] = core;
                if (tracer.TryBuildMuzzleFlash(out Matrix4x4 flash))
                    coreMatrices[playerFlashSlot++] = flash;
                playerSlot++;
            }
        }

        Camera camera = VfxSharedAssets.MainCamera;

        // Отсечение считается по всему батчу целиком: квады у
        // трассеров развёрнуты по-разному, и считать точный AABB
        // значило бы разворачивать углы квада на каждом инстансе.
        // Вместо этого берём AABB по позициям и накрываем его
        // запасом в MaxLength. Лишние единицы в боксе ничего не
        // стоят: в кадр уходит тот же один вызов.
        float pad = TracerEffect.MaxLength + 1f;

        Bounds bounds = new Bounds(
            (max + min) * 0.5f,
            max - min + Vector3.one * (pad * 2f));

        if (playerCount > 0)
        {
            DrawBatch(
                VfxSharedAssets.TracerMaterial,
                VfxSharedAssets.TracerTailMesh,
                tailMatrices,
                0,
                playerCount,
                camera,
                bounds);

            DrawBatch(
                VfxSharedAssets.TracerMaterial,
                VfxSharedAssets.TracerCoreMesh,
                coreMatrices,
                0,
                playerCoreEnd,
                camera,
                bounds);
        }

        if (enemyCount > 0)
        {
            DrawBatch(
                VfxSharedAssets.EnemyTracerMaterial,
                VfxSharedAssets.TracerTailMesh,
                tailMatrices,
                playerCount,
                enemyCount,
                camera,
                bounds);

            DrawBatch(
                VfxSharedAssets.EnemyTracerMaterial,
                VfxSharedAssets.TracerCoreMesh,
                coreMatrices,
                playerCoreEnd,
                enemyCount + enemyFlashCount,
                camera,
                bounds);
        }
    }

    /// <summary>
    /// Один вызов отрисовки, а при нехватке инстансов - несколько.
    /// Настройки повторяют те, что раньше стояли на MeshRenderer
    /// трассера (VfxSharedAssets.SetupRenderer): аддитивному кваду
    /// без освещения не нужны ни тени, ни light probes, ни motion
    /// vectors, ни отражение.
    ///
    /// renderingLayerMask не задаётся: шейдер не читает слои
    /// рендеринга, а трассеры ничего не освещают. Наследуется
    /// пустое значение - так же, как у всех остальных вызовов
    /// RenderMesh в этом проекте.
    /// </summary>
    private static void DrawBatch(
        Material material,
        Mesh mesh,
        Matrix4x4[] data,
        int start,
        int count,
        Camera camera,
        Bounds bounds)
    {
        if (material == null || mesh == null)
            return;

        RenderParams renderParams = new RenderParams(material)
        {
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            lightProbeUsage = LightProbeUsage.Off,
            motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
            reflectionProbeUsage = ReflectionProbeUsage.Off,
            layer = 0,
            camera = camera,
            worldBounds = bounds
        };

        int drawn = 0;

        while (drawn < count)
        {
            int chunk = count - drawn > MaxPerDraw
                ? MaxPerDraw
                : count - drawn;

            Graphics.RenderMeshInstanced(
                renderParams,
                mesh,
                0,
                data,
                chunk,
                start + drawn);

            drawn += chunk;
        }
    }

    private static void EnsureCapacity(int total)
    {
        if (tailMatrices.Length >= total)
            return;

        int capacity = tailMatrices.Length * 2;

        if (capacity < total)
            capacity = total;

        System.Array.Resize(ref tailMatrices, capacity);
        System.Array.Resize(ref coreMatrices, capacity * 2);
    }

    // One dynamic mesh, one material; vertex color and UV encode every theme.
    private const int MaxQuads = 4096;
    private readonly List<Vector3> vertices = new List<Vector3>(MaxQuads * 4);
    private readonly List<Vector2> uv = new List<Vector2>(MaxQuads * 4);
    private readonly List<Vector2> styles = new List<Vector2>(MaxQuads * 4);
    private readonly List<Color32> colors = new List<Color32>(MaxQuads * 4);
    private readonly List<int> indices = new List<int>(MaxQuads * 6);
    private Mesh themedMesh;
    private Material themedMaterial;
    private struct Burst
    {
        public Vector3 Position;
        public ShotVisualTheme Theme;
        public float Start, Duration, Scale;
        public int Seed, Count;
        public bool Death;
    }
    private readonly List<Burst> bursts = new List<Burst>(64);
    private int burstFrame = -1, hits, deaths, sequence;

    private void InitializeThemedBatch()
    {
        Material original = VfxSharedAssets.TracerMaterial;
        if (original == null || !original.HasProperty("_VertexTheme") || !original.shader.isSupported) return;
        themedMaterial = new Material(original) { name = "Shared themed shot VFX" };
        themedMaterial.SetFloat("_VertexTheme", 1f);
        themedMesh = new Mesh { name = "Shared shot and impact geometry" };
        themedMesh.MarkDynamic();
    }

    public static void ClearBursts()
    {
        if (instance != null)
        {
            instance.bursts.Clear();
            instance.deathBodies.Clear();
        }
    }

    // Cosmetic snapshots only: no enemy, collider, rigidbody or per-piece renderer survives death.
    private enum DeathMotion { Scatter, Tumble, Pop, Collapse }
    private struct DeathBody
    {
        public Vector3 Center, Size, Direction;
        public Quaternion Rotation;
        public Color Tint;
        public float Start, Duration, Ground;
        public int Seed;
        public DeathMotion Motion;
    }
    private readonly List<DeathBody> deathBodies = new List<DeathBody>(48);
    private readonly List<Vector3> deathVertices = new List<Vector3>(48 * 8 * 24);
    private readonly List<Vector3> deathNormals = new List<Vector3>(48 * 8 * 24);
    private readonly List<Color32> deathColors = new List<Color32>(48 * 8 * 24);
    private readonly List<int> deathIndices = new List<int>(48 * 8 * 36);
    private Mesh deathMesh;
    private Material deathMaterial;
    private int deathSequence;
    private static readonly Vector3[] BoxNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

    internal static string EmitDeathBody(Renderer body, Vector3 away, DamageSource source, bool boss, Color skinColor)
    {
        if (body == null) return "fall";
        EnsureExists();
        TracerBatcher batch = instance;
        if (batch.deathMesh == null)
        {
            Shader shader = Shader.Find("Custom/Bullet Rush Death Pieces");
            if (shader == null || !shader.isSupported) return "fall";
            batch.deathMaterial = new Material(shader) { name = "Shared death pieces" };
            batch.deathMesh = new Mesh { name = "Shared death body geometry" };
            batch.deathMesh.MarkDynamic();
        }
        int limit = MobileControls.IsTouchDevice ? 24 : 48;
        if (batch.deathBodies.Count >= limit) batch.deathBodies.RemoveAt(0);
        int seed = ++batch.deathSequence;
        DeathMotion motion = (DeathMotion)(seed % 4);
        if (boss || source == DamageSource.Explosion) motion = DeathMotion.Scatter;
        else if (source == DamageSource.ChainLightning) motion = seed % 2 == 0 ? DeathMotion.Pop : DeathMotion.Scatter;
        else if (source == DamageSource.Burn && seed % 2 == 0) motion = DeathMotion.Collapse;
        Bounds bounds = body.bounds;
        Vector3 scale = body.transform.lossyScale;
        MeshFilter filter = body.GetComponent<MeshFilter>();
        Vector3 size = filter != null && filter.sharedMesh != null
            ? Vector3.Scale(filter.sharedMesh.bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)))
            : bounds.size;
        away.y = 0f;
        if (away.sqrMagnitude < .001f) away = Vector3.forward;
        Material material = body.sharedMaterial;
        Color tint = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
        tint *= skinColor;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) tint = tint.linear;
        batch.deathBodies.Add(new DeathBody { Center = bounds.center, Size = size, Rotation = body.transform.rotation,
            Direction = away.normalized, Tint = tint, Start = Time.time, Ground = bounds.min.y,
            Duration = motion == DeathMotion.Pop ? .32f : motion == DeathMotion.Tumble ? .64f : .48f,
            Seed = seed, Motion = motion });
        return motion == DeathMotion.Scatter ? "shatter_depth" : motion == DeathMotion.Pop ? "pop_depth" : "fall_depth";
    }

    internal static void PromoteRecentDeaths(float since, Vector3 center)
    {
        if (instance == null) return;
        for (int i = 0; i < instance.deathBodies.Count; i++)
        {
            DeathBody body = instance.deathBodies[i];
            if (body.Start < since) continue;
            body.Motion = DeathMotion.Scatter;
            body.Duration = Mathf.Max(body.Duration, .58f);
            Vector3 direction = body.Center - center; direction.y = 0f;
            if (direction.sqrMagnitude > .01f) body.Direction = direction.normalized;
            instance.deathBodies[i] = body;
        }
    }

    private void DeathBox(Vector3 center, Vector3 size, Quaternion rotation, Color tint)
    {
        for (int face = 0; face < 6; face++)
        {
            Vector3 n = BoxNormals[face];
            Vector3 u = Mathf.Abs(n.y) > .5f ? Vector3.right : Vector3.Cross(Vector3.up, n);
            Vector3 v = Vector3.Cross(n, u);
            int start = deathVertices.Count;
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = (n + u * ((i & 1) == 0 ? -1 : 1) + v * ((i & 2) == 0 ? -1 : 1)) * .5f;
                deathVertices.Add(center + rotation * Vector3.Scale(corner, size));
                deathNormals.Add(rotation * n);
                deathColors.Add(tint);
            }
            deathIndices.Add(start); deathIndices.Add(start + 1); deathIndices.Add(start + 2);
            deathIndices.Add(start + 1); deathIndices.Add(start + 3); deathIndices.Add(start + 2);
        }
    }

    private void RenderDeathBodies()
    {
        if (deathMesh == null || deathBodies.Count == 0) return;
        deathVertices.Clear(); deathNormals.Clear(); deathColors.Clear(); deathIndices.Clear();
        bool mobile = MobileControls.IsTouchDevice;
        for (int i = deathBodies.Count - 1; i >= 0; i--)
        {
            DeathBody b = deathBodies[i];
            float t = Mathf.Clamp01((Time.time - b.Start) / b.Duration);
            if (t >= 1f) { deathBodies.RemoveAt(i); continue; }
            float shrink = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.65f, 1f, t));
            float unit = Mathf.Max(b.Size.x, b.Size.z);
            switch (b.Motion)
            {
                case DeathMotion.Scatter:
                {
                    int count = mobile ? 4 : 8;
                    for (int j = 0; j < count; j++)
                    {
                        Vector3 offset = new Vector3((j & 1) == 0 ? -.25f : .25f, mobile ? 0 : (j & 4) == 0 ? -.25f : .25f, (j & 2) == 0 ? -.25f : .25f);
                        Vector3 outward = b.Rotation * new Vector3(offset.x, 0, offset.z).normalized;
                        Vector3 point = b.Center + b.Rotation * Vector3.Scale(offset, b.Size) + (outward * .8f + b.Direction * .3f) * unit * t;
                        point.y += Mathf.Sin(t * Mathf.PI) * unit * (.35f + .09f * (j % 3));
                        Vector3 piece = Vector3.Scale(b.Size, new Vector3(.49f, mobile ? .9f : .49f, .49f)) * shrink;
                        point.y = Mathf.Max(point.y, b.Ground + piece.magnitude * .5f);
                        DeathBox(point, piece, Quaternion.AngleAxis(t * (95 + j * 23), outward + Vector3.up) * b.Rotation, b.Tint * (1f - .05f * (j % 3)));
                    }
                    break;
                }
                case DeathMotion.Tumble:
                {
                    Quaternion rotation = Quaternion.AngleAxis(Mathf.Min(1f, t * 1.8f) * 90f, Vector3.Cross(Vector3.up, b.Direction)) * b.Rotation;
                    Vector3 size = b.Size * shrink;
                    float halfHeight = (Mathf.Abs((rotation * Vector3.right).y) * size.x + Mathf.Abs((rotation * Vector3.up).y) * size.y + Mathf.Abs((rotation * Vector3.forward).y) * size.z) * .5f;
                    Vector3 point = b.Center + b.Direction * unit * .85f * (1f - (1f - t) * (1f - t));
                    point.y = b.Ground + halfHeight + Mathf.Sin(Mathf.Min(1f, t * 1.8f) * Mathf.PI) * unit * .3f;
                    DeathBox(point, size, rotation, b.Tint);
                    break;
                }
                case DeathMotion.Pop:
                {
                    float scale = t < .28f ? Mathf.Lerp(1f, 1.22f, t / .28f) : 1.22f * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.28f, .72f, t)));
                    if (scale > .015f) DeathBox(b.Center, b.Size * scale, b.Rotation, b.Tint);
                    for (int j = 0; j < 4; j++)
                    {
                        if (t < .28f) continue;
                        float p = Mathf.InverseLerp(.28f, 1f, t);
                        float a = j * Mathf.PI * .5f + b.Seed * .6f;
                        Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        DeathBox(b.Center + dir * unit * p * .68f + Vector3.up * Mathf.Sin(p * Mathf.PI) * unit * .24f,
                            Vector3.one * unit * .16f * (1f - p), Quaternion.Euler(p * 95f, a * Mathf.Rad2Deg, p * 65f), b.Tint);
                    }
                    break;
                }
                default:
                {
                    Vector3 size = Vector3.Scale(b.Size, new Vector3(1f + .12f * t, Mathf.Max(.04f, 1f - t * 1.5f), 1f + .12f * t)) * shrink;
                    Vector3 point = b.Center; point.y = b.Ground + size.y * .5f;
                    DeathBox(point, size, b.Rotation, b.Tint * (1f - .24f * t));
                    break;
                }
            }
        }
        if (deathVertices.Count == 0) return;
        deathMesh.Clear(false);
        deathMesh.SetVertices(deathVertices); deathMesh.SetNormals(deathNormals); deathMesh.SetColors(deathColors);
        deathMesh.SetTriangles(deathIndices, 0, true);
        Graphics.DrawMesh(deathMesh, Matrix4x4.identity, deathMaterial, 0, VfxSharedAssets.MainCamera,
            0, null, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
    }

    internal static bool EmitImpact(Vector3 position, ShotVisualTheme theme, bool critical = false)
    {
        EnsureExists();
        return instance.EnqueueBurst(position, theme, false, critical ? 1.25f : 1f);
    }

    internal static void EmitDeath(Vector3 position, ShotVisualTheme theme, bool boss)
    {
        EnsureExists();
        instance.EnqueueBurst(position + Vector3.up * 0.25f, theme, true, boss ? 1.8f : 1f);
    }

    private bool EnqueueBurst(Vector3 position, ShotVisualTheme theme, bool death, float scale)
    {
        if (themedMesh == null) return false;
        bool mobile = MobileControls.IsTouchDevice;
        if (burstFrame != Time.frameCount) { burstFrame = Time.frameCount; hits = deaths = 0; }
        if (death ? deaths++ >= (mobile ? 4 : 8) : hits++ >= (mobile ? 5 : 8)) return true;
        int limit = mobile ? 32 : 64;
        if (bursts.Count >= limit)
        {
            if (!death) return true;
            int slot = bursts.FindIndex(b => !b.Death);
            if (slot < 0) slot = 0;
            bursts.RemoveAt(slot);
        }
        bursts.Add(new Burst { Position = position, Theme = theme, Start = Time.time,
            Duration = death ? .36f : .18f, Scale = scale, Seed = ++sequence,
            Count = death ? (scale > 1.5f ? 7 : 5) : 3, Death = death });
        return true;
    }

    private void Quad(Matrix4x4 matrix, bool radial, ShotVisualTheme theme, float brightness)
    {
        if (vertices.Count >= MaxQuads * 4) return;
        int start = vertices.Count;
        float left = radial ? -.5f : -1f, right = radial ? .5f : 0f;
        Color color = theme.Tint;
        color.a = Mathf.Clamp01(theme.Brightness * brightness);
        Color32 packed = color;
        Vector2 style = new Vector2(radial ? 1f : -1f, theme.Style);
        for (int i = 0; i < 4; i++)
        {
            float x = (i & 1) != 0 ? 1 : 0, y = (i & 2) != 0 ? 1 : 0;
            vertices.Add(matrix.MultiplyPoint3x4(new Vector3(Mathf.Lerp(left, right, x), y - .5f, 0)));
            uv.Add(new Vector2(x, y)); styles.Add(style); colors.Add(packed);
        }
        indices.Add(start); indices.Add(start + 2); indices.Add(start + 1);
        indices.Add(start + 1); indices.Add(start + 2); indices.Add(start + 3);
    }

    private void RenderThemedBatch()
    {
        if (instance != this) return;
        vertices.Clear(); uv.Clear(); styles.Clear(); colors.Clear(); indices.Clear();
        int kept = 0;
        for (int i = 0; i < active.Count; i++)
        {
            TracerEffect tracer = active[i];
            if (tracer == null || !tracer.Batched || !tracer.IsPlaying) continue;
            active[kept++] = tracer;
            tracer.BuildMatrices(tracer.transform.position, out Matrix4x4 tail, out Matrix4x4 core);
            Quad(tail, false, tracer.Theme, 1f);
            Quad(core, true, tracer.Theme, 1f);
            if (tracer.TryBuildMuzzleFlash(out Matrix4x4 flash)) Quad(flash, true, tracer.Theme, .85f);
        }
        if (kept < active.Count) active.RemoveRange(kept, active.Count - kept);
        for (int i = bursts.Count - 1; i >= 0; i--)
        {
            Burst b = bursts[i];
            float t = (Time.time - b.Start) / b.Duration;
            if (t >= 1f) { bursts.RemoveAt(i); continue; }
            t = Mathf.Clamp01(t);
            float fade = 1f - t;
            float size = (.20f + t * .3f) * b.Scale * (b.Death ? 1.45f : 1f);
            Quad(Matrix4x4.TRS(b.Position, VfxSharedAssets.FaceDirection(Vector3.forward), Vector3.one * size), true, b.Theme, fade * fade * .85f);
            for (int j = 0; j < b.Count; j++)
            {
                float angle = (j / (float)b.Count) * Mathf.PI * 2f + b.Seed * 2.39996f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), .12f, Mathf.Sin(angle));
                float distance = (b.Death ? .8f : .42f) * b.Scale * (1f - fade * fade);
                Vector3 point = b.Position + direction * distance + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .22f * b.Scale);
                Quad(Matrix4x4.TRS(point, VfxSharedAssets.FaceDirection(direction),
                    new Vector3((.12f + .18f * fade) * b.Scale, .065f * b.Scale * fade, 1f)), false, b.Theme, fade * .85f);
            }
        }
        if (vertices.Count == 0) return;
        themedMesh.Clear(false);
        themedMesh.SetVertices(vertices); themedMesh.SetUVs(0, uv); themedMesh.SetUVs(1, styles);
        themedMesh.SetColors(colors); themedMesh.SetTriangles(indices, 0, true);
        Graphics.DrawMesh(themedMesh, Matrix4x4.identity, themedMaterial, 0, VfxSharedAssets.MainCamera,
            0, null, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        active.Clear();

        tailMatrices = new Matrix4x4[InitialCapacity];
        coreMatrices = new Matrix4x4[InitialCapacity * 2];
    }
}

internal readonly struct ShotVisualTheme
{
    public readonly Color Tint;
    public readonly float Style, Length, Width, Brightness;
    private ShotVisualTheme(float r, float g, float b, float style = 0, float length = 1, float width = 1, float brightness = .85f)
    { Tint = new Color(r, g, b); Style = style; Length = length; Width = width; Brightness = brightness; }
    public static ShotVisualTheme Enemy => new ShotVisualTheme(1, .12f, .07f, 9, 1, 1, .76f);
    public static ShotVisualTheme ForWeapon(string id)
    {
        switch (id)
        {
            case "Burst Rifle": return new ShotVisualTheme(1,.58f,.18f,6,1.08f,.95f);
            case "Golden Rifle": return new ShotVisualTheme(1,.83f,.22f,7,1.12f,.9f);
            case "Hunter Rifle": return new ShotVisualTheme(1,.84f,.65f,7,1.2f,.76f);
            case "Plasma Rifle": return new ShotVisualTheme(.18f,1,.83f,3,1.1f,1.2f);
            case "Basic Shotgun": return new ShotVisualTheme(1,.78f,.37f,0,.72f,1.15f);
            case "Double Shotgun": return new ShotVisualTheme(1,.59f,.28f,8,.78f,1.12f);
            case "Heavy Shotgun": return new ShotVisualTheme(1,.46f,.16f,5,.85f,1.4f);
            case "Inferno Shotgun": return new ShotVisualTheme(1,.28f,.07f,2,.9f,1.3f);
            case "Void Shotgun": return new ShotVisualTheme(.65f,.28f,1,4,1,1.16f);
            case "Assassin SMG": return new ShotVisualTheme(.75f,.72f,1,7,.82f,.75f);
            case "Basic SMG": return new ShotVisualTheme(1,.86f,.53f,0,.73f,.8f);
            case "Lightning SMG": return new ShotVisualTheme(.25f,.65f,1,1,1.15f,1.5f);
            case "Minigun": return new ShotVisualTheme(1,.51f,.22f,6,.92f,.8f,.70f);
            case "Twin SMG": return new ShotVisualTheme(.75f,1,.38f,8,.78f,.88f);
            default: return new ShotVisualTheme(1,.68f,.23f);
        }
    }
    public static ShotVisualTheme ForDamage(DamageSource source, string id)
    {
        switch (source)
        {
            case DamageSource.Burn: return ForWeapon("Inferno Shotgun");
            case DamageSource.ChainLightning: return ForWeapon("Lightning SMG");
            case DamageSource.Explosion: return ForWeapon("Heavy Shotgun");
            case DamageSource.Bleed: return new ShotVisualTheme(.9f,.17f,.18f);
            case DamageSource.Ability: return ForWeapon("Plasma Rifle");
            default: return ForWeapon(id);
        }
    }
}
