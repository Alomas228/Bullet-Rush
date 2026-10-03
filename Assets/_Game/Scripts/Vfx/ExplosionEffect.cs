using UnityEngine;

/// <summary>
/// Взрыв из трёх слоёв, как в «боевике», а не как плоский диск:
///
///   1. Огненный шар - настоящая сфера радиуса поражения, а не
///      билборд. Стоит с любого ракурса и заполняет всю зону
///      урона: видно, где опасно. Объём даёт сам шейдер -
///      задние грани рисуются аддитивно, поэтому в центре диска
///      два слоя, у силуэта один, плюс горячая кромка.
///   2. Угли - горящие точки, летят по конусу, гаснут и падают.
///   3. Дым - мягкие тёмные клубы, шире и позже огня.
///
/// Почему не Particle System - как и во всех остальных VFX этого
/// проекта: свой эмиттер на каждый взрыв дороже трёх общих мешей,
/// которые уже лежат в VfxSharedAssets.
///
/// ПОЧЕМУ ВСЕ ВЗРЫВЫ ИДУТ ТРЁМЯ DRAW CALL, А НЕ ТРЕМЯ НА КАЖДЫЙ.
/// Раньше у каждого экземпляра пула было три собственных меша и
/// три MeshRenderer, а цвет и геометрия писались в вершины каждый
/// кадр. При десятках взрывов это давало сотни драфколлов и
/// сотни загрузок меша за кадр.
///
/// Теперь у эффекта нет ни меша, ни рендерера, ни дочерних
/// объектов: он только считает физику и выдаёт BlastBatcher'у
/// по одному инстансу на шар, на уголь и на клуб дыма. Меши
/// общие (VfxSharedAssets.BlastFireballMesh и BlastQuadMesh),
/// размер приходит из матрицы инстанса, а per-instance цвет -
/// из массива _BlastColor, который батчер отдаёт через
/// MaterialPropertyBlock. Стадия остывания у каждого взрыва своя,
/// но меша под это больше не нужно.
///
/// Три слоя остались тремя вызовами, и это нельзя ужать в один:
/// шар и угли аддитивные (One One), дым альфа-ный
/// (SrcAlpha OneMinusSrcAlpha), а у Blend разный render state.
///
/// Чего больше нет по сравнению с мешами на объекте:
///   - frustum culling по отдельному взрыву (и раньше он был
///     один общий бокс 16x16x16 на весь меш, то есть фактически
///     не работал);
///   - сортировки прозрачного дыма между взрывами. Внутри одного
///     взрыва все клубы и раньше шли одним мешем без сортировки,
///     так что для мягкого дыма разницы не видно.
///
/// Меши углей и дыма лежали в мировых координатах относительно
/// центра взрыва: корень не повёрнут, и не нужно ни одной
/// обратной матрицы в кадре. Теперь та же геометрия собирается
/// в матрицу инстанса.
/// </summary>
public sealed class ExplosionEffect : VfxEffect
{
    // =========================================================
    // РЕКОМЕНДУЕМЫЕ ЗНАЧЕНИЯ
    //
    // Камера стоит в ~22.5 мировых единиц от плоскости боя при
    // FOV 60: видит ~26 единиц по вертикали, на 1080p это ~42
    // пикселя на единицу. Радиус взрыва в бою - 2..5 единиц,
    // то есть 84..210 пикселей: сфера радиуса поражения
    // перекрывает зону урона и читается как «здесь небезопасно».
    // =========================================================

    private const int MaxEmbers = 10;
    private const int MaxSmokePuffs = 5;

    [Header("Fireball")]
    [SerializeField, Tooltip("Время жизни огненного шара, с")]
    private float fireballDuration = 0.32f;

    [SerializeField, Tooltip("Пиковый радиус шара в радиусах взрыва")]
    private float fireballPeakScale = 1.1f;

    [Header("Embers")]
    [SerializeField, Tooltip("Сколько углей на взрыв")]
    private int emberCount = 8;

    [SerializeField, Tooltip("Время жизни углей, с")]
    private float emberDuration = 0.45f;

    [SerializeField, Tooltip("Гравитация углей")]
    private float emberGravity = 11f;

    [Header("Smoke")]
    [SerializeField, Tooltip("Сколько клубов дыма")]
    private int smokeCount = 4;

    [SerializeField, Tooltip("Задержка дыма после взрыва, с")]
    private float smokeDelay = 0.04f;

    [SerializeField, Tooltip("Время жизни дыма, с")]
    private float smokeDuration = 0.8f;

    [SerializeField, Tooltip("Насколько дым шире зоны поражения")]
    private float smokeSpread = 0.35f;

    private Vector3[] emberPosition;
    private Vector3[] emberVelocity;
    private float[] emberTimeLeft;
    private float[] emberSize;
    private float[] emberHeat;

    private Vector3[] smokePosition;
    private float[] smokeTimeLeft;
    private float[] smokeSize;
    private float[] smokeRise;

    private Color blastColor;
    private Vector3 blastOrigin;
    private float blastRadius;
    private float elapsed;
    private int activeEmbers;
    private int activeSmoke;

    /// <summary>
    /// Взрыв уже в очереди на отрисовку. Ставится батчером.
    /// </summary>
    internal bool Batched { get; set; }

    /// <summary>
    /// Центр взрыва: от него считается общий AABB батча.
    /// </summary>
    internal Vector3 Origin => blastOrigin;

    /// <summary>
    /// Радиус, целиком покрывающий все слои взрыва, - им
    /// расширяется общий AABB. Дым шире зоны поражения
    /// (smokeSpread), а шар раздувается до fireballPeakScale,
    /// поэтому берётся двойной радиус поражения.
    /// </summary>
    internal float BoundsRadius => blastRadius * 2f;

    /// <summary>
    /// Шаблон для пула, когда префаб не задан. Создаётся один раз.
    /// </summary>
    public static GameObject CreateTemplate()
    {
        GameObject template = new GameObject("ExplosionTemplate");

        template.SetActive(false);
        template.AddComponent<ExplosionEffect>();

        return template;
    }

    private void Awake()
    {
        gameObject.name = "Explosion";

        emberPosition = new Vector3[MaxEmbers];
        emberVelocity = new Vector3[MaxEmbers];
        emberTimeLeft = new float[MaxEmbers];
        emberSize = new float[MaxEmbers];
        emberHeat = new float[MaxEmbers];

        smokePosition = new Vector3[MaxSmokePuffs];
        smokeTimeLeft = new float[MaxSmokePuffs];
        smokeSize = new float[MaxSmokePuffs];
        smokeRise = new float[MaxSmokePuffs];
    }

    /// <summary>
    /// Запуск взрыва.
    /// </summary>
    /// <param name="position">Центр взрыва.</param>
    /// <param name="radius">Радиус поражения: от него считается
    /// шар и от него же разлетаются угли с дымом.</param>
    /// <param name="color">Цвет пламени. Белое ядро, угли и дым
    /// выводятся из него.</param>
    public void Play(
        Vector3 position,
        float radius,
        Color color)
    {
        transform.position = position;

        blastOrigin = position;
        blastColor = new Color(color.r, color.g, color.b, 1f);
        blastRadius = Mathf.Max(radius, 0.2f);
        elapsed = 0f;

        SpawnEmbers();
        SpawnSmoke();

        float total = Mathf.Max(
            fireballDuration,
            Mathf.Max(
                emberDuration,
                smokeDelay + smokeDuration
            )
        );

        BeginPlay(total);

        // Последним: к этому моменту IsPlaying уже true, и батчер
        // подхватит взрыв в ближайшем LateUpdate.
        BlastBatcher.Register(this);
    }

    private void SpawnEmbers()
    {
        activeEmbers = Mathf.Clamp(emberCount, 0, MaxEmbers);

        for (int i = 0; i < activeEmbers; i++)
        {
            Vector2 circle = Random.insideUnitCircle;

            Vector3 direction = new Vector3(
                circle.x,
                Random.Range(0.15f, 1f),
                circle.y
            );

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.up;

            direction.Normalize();

            // Скорость в мировых единицах в секунду: чем больше
            // радиус взрыва, тем дальше разлетаются угли.
            emberVelocity[i] = direction * (
                blastRadius * Random.Range(1.1f, 2.4f)
            );

            emberPosition[i] = Vector3.up * blastRadius * 0.15f;

            emberTimeLeft[i] =
                emberDuration * Random.Range(0.6f, 1f);

            emberSize[i] =
                blastRadius * Random.Range(0.07f, 0.16f);

            emberHeat[i] = Random.Range(0.75f, 1f);
        }
    }

    private void SpawnSmoke()
    {
        activeSmoke = Mathf.Clamp(smokeCount, 0, MaxSmokePuffs);

        for (int i = 0; i < activeSmoke; i++)
        {
            Vector2 circle = Random.insideUnitCircle;

            smokePosition[i] = new Vector3(
                circle.x * blastRadius * 0.3f,
                blastRadius * Random.Range(0.1f, 0.45f),
                circle.y * blastRadius * 0.3f
            );

            // Отсчёт назад идёт от полной жизни, задержка
            // отсчитывается общим elapsed: так клуб не щёлкает
            // первым кадром и не живёт дольше своей жизни.
            smokeTimeLeft[i] =
                smokeDuration * Random.Range(0.7f, 1f);

            smokeSize[i] =
                blastRadius * Random.Range(0.5f, 0.8f);

            smokeRise[i] =
                blastRadius * Random.Range(0.3f, 0.7f);
        }
    }

    protected override void Tick(float deltaTime)
    {
        elapsed += deltaTime;

        TickEmbers(deltaTime);
        TickSmoke(deltaTime);

        if (elapsed >= Duration)
            Finish();
    }

    private void TickEmbers(float deltaTime)
    {
        for (int i = 0; i < activeEmbers; i++)
        {
            emberTimeLeft[i] -= deltaTime;

            Vector3 velocity = emberVelocity[i];

            velocity.y -= emberGravity * deltaTime;
            emberVelocity[i] = velocity;

            emberPosition[i] += velocity * deltaTime;
        }
    }

    private void TickSmoke(float deltaTime)
    {
        for (int i = 0; i < activeSmoke; i++)
        {
            smokeTimeLeft[i] -= deltaTime;

            // Дым всплывает: под пятном на земле это единственное,
            // что показывает, что клуб уходит вверх, а не
            // расползается по полу.
            smokePosition[i] +=
                Vector3.up * smokeRise[i] * deltaTime;
        }
    }

    // =========================================================
    // ВЫДАЧА ИНСТАНСОВ БАТЧЕРУ
    //
    // Три метода - по одному на слой. Каждый кладёт в батчер
    // матрицу и цвет на каждый видимый кусок этого слоя и
    // ничего не делает, если кусков нет. Батчер сам решает,
    // рисовать ли слой в этом кадре.
    // =========================================================

    /// <summary>
    /// Огненный шар: один инстанс на взрыв, сфера радиуса
    /// поражения. За первые 30% жизни раздувается с перелётом,
    /// потом остывает и гаснет.
    ///
    /// Цвет идёт от белого ядра через жёлтый к тёмно-красным
    /// углям, яркость падает в разы - именно это даёт вспышку в
    /// первые кадры и спокойное затухание дальше.
    ///
    /// Меш единичный (радиус 0.5), поэтому масштаб - это диаметр.
    /// </summary>
    internal void AppendFireball(BlastBatchBuffer batch)
    {
        float progress =
            Mathf.Clamp01(elapsed / fireballDuration);

        float scale;

        if (progress < 0.3f)
        {
            float t = progress / 0.3f;

            scale = Mathf.Lerp(
                0.3f,
                fireballPeakScale,
                1f - Mathf.Pow(1f - t, 3f)
            );
        }
        else
        {
            float t = (progress - 0.3f) / 0.7f;

            scale = Mathf.Lerp(
                fireballPeakScale,
                0.7f,
                t * t
            );
        }

        float size = blastRadius * scale * 2f;

        // Старт не белый: 85% белого в первом кадре давали
        // белый шар, а не вспышку огня. Белёется только
        // самый центр кадра.
        Color hot = Color.Lerp(blastColor, Color.white, 0.35f);

        Color color = progress < 0.25f
            ? Color.Lerp(hot, blastColor, progress / 0.25f)
            : Color.Lerp(
                blastColor,
                blastColor * 0.2f,
                (progress - 0.25f) / 0.75f
            );

        // HDR: в первые кадры вдвое выше порога Bloom (0.9),
        // к концу почти до нуля. Значение ниже, чем было у
        // билборда, потому что аддитивная сфера складывает две
        // грани - яркость и так удваивается в центре.
        float intensity = Mathf.Lerp(1.8f, 0f, progress * progress);

        Color finalColor = Scale(color, intensity);

        batch.Add(
            Matrix4x4.TRS(
                blastOrigin,
                Quaternion.identity,
                new Vector3(size, size, size)),
            finalColor
        );
    }

    /// <summary>
    /// Угли: по инстансу на каждый живой уголь. Искра вытянута по
    /// направлению движения: круглые точки читаются как «брызги»,
    /// а полоса - как летящий горячий кусок. Длина зависит от
    /// скорости, поэтому быстрые искры длиннее медленных.
    /// </summary>
    internal void AppendEmbers(BlastBatchBuffer batch)
    {
        for (int i = 0; i < activeEmbers; i++)
        {
            if (emberTimeLeft[i] <= 0f)
                continue;

            float life = emberTimeLeft[i] / emberDuration;

            StreakAxes(
                emberVelocity[i],
                emberSize[i] * Mathf.Lerp(0.4f, 1f, life),
                out Vector3 axisX,
                out Vector3 axisY);

            batch.Add(
                MakeInstance(
                    blastOrigin + emberPosition[i],
                    axisX,
                    axisY),
                EmberColor(life, emberHeat[i])
            );
        }
    }

    /// <summary>
    /// Клубы дыма: по инстансу на каждый живой клуб. Квад
    /// развёрнут по осям мира и раскрывается по мере подъёма.
    /// </summary>
    internal void AppendSmoke(BlastBatchBuffer batch)
    {
        // До задержки дыма нет вообще: иначе он появлялся бы
        // первым кадром и потом только таял.
        if (elapsed < smokeDelay)
            return;

        for (int i = 0; i < activeSmoke; i++)
        {
            if (smokeTimeLeft[i] <= 0f)
                continue;

            float life = Mathf.Clamp01(
                smokeTimeLeft[i] / smokeDuration
            );

            // Клуб раскрывается по мере подъёма.
            float size = smokeSize[i] * (
                1f + smokeSpread + (1f - life) * 0.6f
            );

            batch.Add(
                Matrix4x4.TRS(
                    blastOrigin + smokePosition[i],
                    Quaternion.identity,
                    new Vector3(size, size, 1f)),
                // Альфа в квадрате: клуб не только тает, но и
                // мягко появляется, а не щёлкает на первом кадре.
                //
                // 0.14, а не 0.42: клубы всех взрывов теперь идут одним
                // вызовом без сортировки между собой, и в залпе
                // плотность складывалась в непрозрачную стену,
                // через которую не видно поле боя. На одном
                // взрыве разницы почти нет - там клуб один.
                new Vector4(1f, 1f, 1f, 0.14f * life * life)
            );
        }
    }

    /// <summary>
    /// Уголь остывает от жёлто-белого к тёмно-красному и при
    /// этом тускнеет: в начале он ярче порога Bloom и виден как
    /// искра, в конце - тёмная точка.
    /// </summary>
    private Color EmberColor(float life, float heat)
    {
        Color hot = Color.Lerp(blastColor, Color.white, 0.7f);
        Color cold = blastColor * 0.25f;

        Color color = life > 0.45f
            ? Color.Lerp(hot, blastColor, (life - 0.45f) / 0.55f)
            : Color.Lerp(blastColor, cold, 1f - life / 0.45f);

        return Scale(color, Mathf.Lerp(0.2f, 3.2f, life) * heat);
    }

    /// <summary>
    /// Оси квада искры. Ось полосы лежит в плоскости боя: с камеры
    /// сверху видно и длину, и направление, поэтому искра читается
    /// как летящая, а не как точка.
    ///
    /// Вторая ось берётся крест-накрест в той же плоскости: квад
    /// остаётся тонким по ширине и длинным по ходу. Верхняя
    /// граница длины нужна, иначе на первых кадрах у самого центра
    /// взрыва получаются длинные полосы через весь шар.
    ///
    /// Длины возвращаются уже полными сторонами квада: меш
    /// единичный, его вершины лежат на ±0.5, поэтому в матрицу
    /// идёт удвоенная величина.
    /// </summary>
    private static void StreakAxes(
        Vector3 velocity,
        float size,
        out Vector3 axisX,
        out Vector3 axisY)
    {
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);

        float speed = flat.magnitude;

        if (speed < 0.0001f)
        {
            flat = Vector3.forward;
            speed = 1f;
        }
        else
        {
            flat /= speed;
        }

        Vector3 side = new Vector3(-flat.z, 0f, flat.x);

        float length = Mathf.Min(
            size,
            speed * 0.055f
        ) * 0.5f;

        float width = size * 0.22f * 0.5f;

        axisX = flat * (length * 2f);
        axisY = side * (width * 2f);
    }

    /// <summary>
    /// Матрица инстанса из позиции и двух осей квада. Третья ось
    /// всегда вверх: она нужна только чтобы базис был
    /// правым, на видимость не влияет (Cull Off).
    /// </summary>
    private static Matrix4x4 MakeInstance(
        Vector3 position,
        Vector3 axisX,
        Vector3 axisY)
    {
        return new Matrix4x4(
            new Vector4(axisX.x, axisX.y, axisX.z, 0f),
            new Vector4(axisY.x, axisY.y, axisY.z, 0f),
            new Vector4(0f, 1f, 0f, 0f),
            new Vector4(position.x, position.y, position.z, 1f)
        );
    }

    private static Color Scale(Color color, float intensity)
    {
        return new Color(
            color.r * intensity,
            color.g * intensity,
            color.b * intensity,
            1f
        );
    }

    protected override void ReturnToPool()
    {
        // Снимаем до возврата в пул: следующий взрыв на этом же
        // объекте стартует в тот же кадр, и старый не должен
        // остаться в очереди на отрисовку.
        BlastBatcher.Unregister(this);

        VfxPools.Explosions.Despawn(this);
    }
}