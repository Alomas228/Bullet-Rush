using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldStructureGenerator : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("Центр арены. Если не назначен — используется позиция этого объекта.")]
    [SerializeField] private Transform center;

    [Tooltip("Игрок, в котором нельзя ставить структуры. Если не назначен — ищется по тегу Player.")]
    [SerializeField] private Transform player;

    [Tooltip("Запас безопасности: структура не ставится ближе этого радиуса к игроку.")]
    [SerializeField] private float playerSafeRadius = 1.5f;

    [Header("Placement")]
    [Tooltip("Радиус кольца спавна: используется врагами (EnemySpawner) как граница боевой зоны, а не предел блоков.")]
    [SerializeField] private float arenaRadius = 30f;

    [Tooltip("Безопасная центральная зона: ближе этого радиуса к центру блоки не ставятся — игрок стартует на свободном центре.")]
    [SerializeField] private float minDistanceFromCenter = 7f;

    [Tooltip("Минимальный зазор между блоками (в дополнение к половинным габаритам).")]
    [SerializeField] private float minSpacing = 1f;

    [Tooltip("Сколько попыток стоит найти свободное место под один блок.")]
    [SerializeField] private int maxPlacementAttempts = 30;

    [Header("Square Arena")]
    [Tooltip("Расставлять блоки по квадратной арене (равномерно по площади), а не кольцом. Референс: карта квадратная, блоки по всей боевой зоне.")]
    [SerializeField] private bool useSquarePlacement = true;

    [Tooltip("Полуширина квадрата расстановки блоков. Стены арены на ±50; держи меньше, чтобы блоки не стояли вплотную к стенам.")]
    [SerializeField] private float structureHalfSize = 32f;

    [Header("Prefabs")]
    [Tooltip("Префабы, которые будут спавниться вместо кубов. Для каждой структуры выбирается один случайный из списка. Пусто — фолбэк на примитивный куб.")]
    [SerializeField] private GameObject[] structurePrefabs;

    [Header("Count")]
    [Tooltip("Меньше блоков-препятствий в арене (мин).")]
    [SerializeField] private int minStructures = 25;

    [Tooltip("Больше блоков-препятствий в арене (макс).")]
    [SerializeField] private int maxStructures = 40;

    [Header("Sizes")]
    [Tooltip("Размеры остаются как у кубов: габариты префаба умножаются на эти значения. 1 — авторский размер префаба.")]
    [SerializeField] private float minWidth = 1.5f;
    [SerializeField] private float maxWidth = 4f;
    [SerializeField] private float minHeight = 1f;
    [SerializeField] private float maxHeight = 6f;

    [Header("Look")]
    [Tooltip("Готовые материалы. Если заданы — накладываются на все рендереры префаба. Пусто — префаб остаётся со своим материалом.")]
    [SerializeField] private Material[] materials;
    [Tooltip("Палитра цветов. Используется только для фолбэк-куба, когда материалы не заданы.")]
    [SerializeField] private Color[] palette;

    [Tooltip("Базовое зерно генерации. 0 — случайное при запуске.")]
    [SerializeField] private int baseSeed;

    [Header("Spawn Animation")]
    [Tooltip("Пауза между появлением структур — объекты вырастают цепочкой, как исчезали при возврате в меню.")]
    [SerializeField] private float spawnStagger = 0.05f;
    [Tooltip("Длительность «вырастания» одной структуры.")]
    [SerializeField] private float scaleInDuration = 0.25f;
    [Tooltip("Сколько структур обрабатывается за один шаг стаггера. Больше — быстрее перестройка карты между волнами.")]
    [SerializeField] private int structuresPerTick = 4;

    [Header("Quick Transition")]
    [Tooltip("Длительность «вырастания» блока при быстрой пересборке между волнами. Короткая, чтобы перестройка не затягивала паузу.")]
    [SerializeField] private float quickScaleInDuration = 0.18f;
    [Tooltip("Зазор между блоками при быстрой пересборке: каскад даёт ощущение смены арены, а не одновременного хаоса.")]
    [SerializeField] private float quickStagger = 0.015f;
    [Tooltip("Множитель скорости укорочения блоков при быстрой пересборке. 1 — исчезают за quickScaleInDuration, 0.5 — вдвое быстрее.")]
    [SerializeField] private float quickFadeOutScale = 0.6f;

    [Header("Layer")]
    [Tooltip("Слой, на который помещаются создаваемые структуры (включая дочерние объекты префаба). Должен совпадать с occlusionMask в StructureOcclusionManager. Пусто — слой не меняется.")]
    [SerializeField] private string structureLayerName = "World";

    private readonly List<GameObject> structures =
        new List<GameObject>();

    private readonly List<Vector2> placedPositions =
        new List<Vector2>();

    private readonly List<float> placedClearances =
        new List<float>();

    // =========================================================
    // ARENA LAYOUT
    // =========================================================

    // Архетип боевой геометрии текущей карты. Scattered — исходное
    // поведение: независимые броски по всей площади. Остальные
    // стили сначала строят план геометрии (стены с проходами,
    // колонны, гроздья), а уже он раздаётся блокам по одному.
    private ArenaLayoutStyle activeLayoutStyle =
        ArenaLayoutStyle.Scattered;

    // План раскладки текущей генерации. Строится один раз на
    // генерацию из того же rng, поэтому карта с тем же зерном
    // всегда даёт одну и ту же геометрию.
    private readonly List<PlannedStructure> layoutPlan =
        new List<PlannedStructure>(64);

    private int layoutCursor;

    // Одна запланированная структура. dimensions — габариты до
    // умножения на авторасштаб префаба; spacingOverride < 0 означает
    // «взять общий minSpacing». Отдельный зазор нужен стенам и
    // участкам грозди: они стоят впритык по замыслу, и общий
    // зазор их бы вычеркнул.
    private struct PlannedStructure
    {
        public Vector2 position;
        public Vector3 dimensions;
        public float yaw;
        public float spacingOverride;
    }

    // Значения инспектора до применения первого профиля. Профиль
    // пишет в те же поля, из которых генератор читает параметры по
    // умолчанию, поэтому карта без профиля обязана вернуть именно их,
    // а не раскладку предыдущей карты.
    private struct DefaultLayout
    {
        public int minStructures;
        public int maxStructures;
        public float minWidth;
        public float maxWidth;
        public float minHeight;
        public float maxHeight;
        public float minSpacing;
        public float structureHalfSize;
        public float minDistanceFromCenter;
    }

    private DefaultLayout inspectorDefaults;
    private bool inspectorDefaultsCaptured;

    // Список префабов без пустых слотов: случайный выбор не должен
    // упираться в null и молча подменяться кубом.
    private GameObject[] spawnPrefabs;
    private bool warnedMissingPrefabs;

    private Coroutine generateCoroutine;
    private int cachedWorldLayer = -1;

    // Корутины «вырастания»/исчезновения, запущенные текущей
    // генерацией. Отдельные корутины на каждый блок, поэтому
    // StopCoroutine на головном их не гасит — без явной остановки
    // они продолжат анимировать уже снятые блоки.
    private readonly List<Coroutine> transitionRoutines =
        new List<Coroutine>();

    // Длительность «вырастания» текущей генерации. Быстрая
    // пересборка между волнами ставит своё короткое значение,
    // поэтому ScaleInStructure берёт время отсюда, а не из поля
    // инспектора напрямую.
    private float currentScaleInDuration;

    // Общие материалы для палитры: один материал на цвет вместо
    // создания копии на каждый куб (иначе каждая волна плодит
    // десятки экземпляров материалов).
    private Material[] paletteMaterials;

    private float groundY;

    public bool IsGenerating =>
        generateCoroutine != null;

    public Vector3 ArenaCenter =>
        center != null
            ? center.position
            : transform.position;

    public float ArenaRadius =>
        arenaRadius;

    public int BaseSeed =>
        baseSeed;

    private void Awake()
    {
        if (baseSeed == 0)
            baseSeed = Random.Range(1, int.MaxValue);

        spawnPrefabs = CollectPrefabs();

        WarnPrefabsWithoutColliders();

        cachedWorldLayer =
            LayerMask.NameToLayer(structureLayerName);

        if (cachedWorldLayer < 0 &&
            !string.IsNullOrEmpty(structureLayerName))
        {
            Debug.LogWarning(
                $"[WorldStructureGenerator] Слой '{structureLayerName}' " +
                "не найден. Создай его в Project Settings → Tags and Layers."
            );
        }

        EnsureOcclusionManager();

        currentScaleInDuration = scaleInDuration;
    }

    // Отбрасывает пустые слоты, чтобы случайный выбор всегда
    // давал реальный префаб.
    private GameObject[] CollectPrefabs()
    {
        if (structurePrefabs == null ||
            structurePrefabs.Length == 0)
        {
            return null;
        }

        List<GameObject> valid =
            new List<GameObject>(structurePrefabs.Length);

        for (int i = 0; i < structurePrefabs.Length; i++)
        {
            if (structurePrefabs[i] != null)
                valid.Add(structurePrefabs[i]);
        }

        return valid.Count > 0
            ? valid.ToArray()
            : null;
    }

    // Добавляет менеджер растворяющихся при заслонении блоков,
    // если его ещё нет в сцене (не требует ручной настройки).
    private void EnsureOcclusionManager()
    {
        if (FindAnyObjectByType<StructureOcclusionManager>() == null)
            gameObject.AddComponent<StructureOcclusionManager>();
    }

    /// <summary>
    /// Анимированная сборка арены: блоки появляются цепочкой с
    /// «вырастанием». Используется для первого появления арены
    /// (старт забега, обучение), где на анимацию есть время.
    /// </summary>
    public void GenerateAnimated(int mapSeed, int wave)
    {
        BeginGeneration(
            new System.Random(
                mapSeed * 31 + wave * 131
            )
        );
    }

    /// <summary>
    /// Перестройка арены под новую волну. Раскладка зависит и от
    /// карты, и от номера волны: блоки меняются каждый забег, но
    /// одна и та же волна на одной карте всегда даёт одну и ту же
    /// геометрию. Анимация короткая, чтобы пауза между волнами
    /// почти не росла, но переход не выглядел скачком.
    /// </summary>
    public void RebuildForWave(int mapSeed, int wave)
    {
        BeginGeneration(
            new System.Random(
                mapSeed * 31 + wave * 131
            ),
            RebuildProfile()
        );
    }

    // Короткие тайминги нужны только чтобы ускорить смену одной
    // раскладки на другую. Если арены ещё нет (обучение пропущено,
    // первая волна идёт в пустоту), пересобирать нечего — и
    // сокращённый каскад лишь делает появление блоков резким.
    // Первое появление идёт на штатных таймингах.
    private TransitionProfile RebuildProfile()
    {
        if (structures.Count > 0)
            return QuickTransitionProfile();

        return AnimatedProfile();
    }

    private TransitionProfile QuickTransitionProfile()
    {
        return new TransitionProfile
        {
            stagger = quickStagger,
            scaleInDuration = quickScaleInDuration,
            fadeOutScale = quickFadeOutScale
        };
    }

    private TransitionProfile AnimatedProfile()
    {
        return new TransitionProfile
        {
            stagger = spawnStagger,
            scaleInDuration = scaleInDuration,
            fadeOutScale = 1f
        };
    }

    // Тайминги одной пересборки. Основной путь берёт значения из
    // инспектора, быстрая пересборка между волнами — свои, короткие.
    private struct TransitionProfile
    {
        public float stagger;
        public float scaleInDuration;
        public float fadeOutScale;
    }

    // Общий вход обеих генераций: гасит текущий корутин, ищет игрока
    // и считает количество блоков по параметрам инспектора.
    private void BeginGeneration(
        System.Random rng,
        TransitionProfile? profile = null)
    {
        CancelRunningGeneration();

        PrepareForGeneration();

        Vector3 centerPos = ArenaCenter;

        groundY = centerPos.y;

        int count =
            rng.Next(
                minStructures,
                maxStructures + 1
            );

        BuildLayoutPlan(rng, count);

        generateCoroutine = StartCoroutine(
            TransitionToWaveRoutine(
                rng,
                centerPos,
                count,
                profile
            )
        );
    }

    // Гасит и головную корутину, и корутины перехода: они живут
    // отдельно, StopCoroutine на головной их не трогает, и отменённая
    // генерация продолжила бы спавнить блоки поверх новой раскладки.
    private void CancelRunningGeneration()
    {
        StopTrackedCoroutines();

        if (generateCoroutine == null)
            return;

        StopCoroutine(generateCoroutine);

        generateCoroutine = null;
    }

    private void PrepareForGeneration()
    {
        if (player != null)
            return;

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
    }

    // Анимация идёт в два захода: сначала старая раскладка уходит
    // каскадом, затем новая вырастает на её месте. Порядок важен —
    // при одновременном старте на экране секунду живут обе раскладки.
    private IEnumerator TransitionToWaveRoutine(
        System.Random rng,
        Vector3 centerPos,
        int count,
        TransitionProfile? profileOverride)
    {
        TransitionProfile profile =
            profileOverride ?? AnimatedProfile();

        float growDuration = profile.scaleInDuration;

        WaitForSecondsRealtime staggerWait =
            new WaitForSecondsRealtime(profile.stagger);

        WaitForSecondsRealtime growWait =
            new WaitForSecondsRealtime(growDuration);

        int perTick = Mathf.Max(structuresPerTick, 1);

        if (structures.Count > 0)
        {
            List<GameObject> oldStructures =
                new List<GameObject>(structures);

            structures.Clear();
            placedPositions.Clear();
            placedClearances.Clear();

            // Коллайдеры гасим сразу, а не в начале fade-out: старые
            // блоки не должны блокировать игрока и пули, пока новые
            // ещё не выросли. Иначе на пересборке между волнами
            // остаётся невидимая стена.
            for (int i = 0; i < oldStructures.Count; i++)
            {
                if (oldStructures[i] == null)
                    continue;

                Collider[] colliders =
                    oldStructures[i]
                        .GetComponentsInChildren<Collider>(true);

                for (int j = 0; j < colliders.Length; j++)
                    colliders[j].enabled = false;
            }

            for (int i = 0; i < oldStructures.Count; i += perTick)
            {
                int batchEnd =
                    Mathf.Min(
                        i + perTick,
                        oldStructures.Count
                    );

                for (int j = i; j < batchEnd; j++)
                {
                    if (oldStructures[j] != null)
                        StartTrackedCoroutine(
                            FadeOutStructure(
                                oldStructures[j],
                                growDuration * profile.fadeOutScale
                            )
                        );
                }

                yield return staggerWait;
            }

            // Ждём исчезновения старых блоков, иначе на экране
            // на секунду окажутся две раскладки одновременно.
            yield return new WaitForSecondsRealtime(
                growDuration * profile.fadeOutScale
            );
        }

        currentScaleInDuration = growDuration;

        try
        {
            for (int i = 0; i < count; i += perTick)
            {
                int batchEnd =
                    Mathf.Min(
                        i + perTick,
                        count
                    );

                for (int j = i; j < batchEnd; j++)
                {
                    TrySpawnStructure(rng, centerPos);
                }

                yield return staggerWait;
            }

            // Ждём роста последнего блока, чтобы генерация
            // считалась завершённой только когда всё выросло.
            yield return growWait;
        }
        finally
        {
            currentScaleInDuration = scaleInDuration;

            generateCoroutine = null;
        }
    }

    // Корутины перехода живут отдельно от головного, поэтому их
    // нужно помнить: StopCoroutine(generateCoroutine) их не гасит,
    // и задержанные блоки продолжили бы анимироваться поверх новой
    // раскладки.
    private Coroutine StartTrackedCoroutine(IEnumerator routine)
    {
        Coroutine coroutine = StartCoroutine(routine);

        transitionRoutines.Add(coroutine);

        return coroutine;
    }

    private void StopTrackedCoroutines()
    {
        for (int i = 0; i < transitionRoutines.Count; i++)
        {
            if (transitionRoutines[i] != null)
                StopCoroutine(transitionRoutines[i]);
        }

        transitionRoutines.Clear();
    }

    private IEnumerator FadeOutStructure(
        GameObject structure,
        float duration)
    {
        if (structure == null)
            yield break;

        // Коллайдеры гасит вызывающий: на быстрой пересборке
        // старые блоки перестают мешать сразу, не дожидаясь анимации.

        Vector3 startScale =
            structure.transform.localScale;

        float timer = 0f;

        while (timer < duration)
        {
            if (structure == null)
                yield break;

            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / duration
                );

            t = Mathf.SmoothStep(0f, 1f, t);

            structure.transform.localScale =
                Vector3.Lerp(
                    startScale,
                    Vector3.zero,
                    t
                );

            yield return null;
        }

        if (structure != null)
            Destroy(structure);
    }

    private IEnumerator ScaleInStructure(
        GameObject structure,
        Vector3 fullScale)
    {
        float duration = currentScaleInDuration;

        structure.transform.localScale = Vector3.zero;

        float timer = 0f;

        while (timer < duration)
        {
            if (structure == null)
                yield break;

            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / duration
                );

            t = Mathf.SmoothStep(0f, 1f, t);

            structure.transform.localScale =
                Vector3.Lerp(
                    Vector3.zero,
                    fullScale,
                    t
                );

            yield return null;
        }

        if (structure != null)
            structure.transform.localScale = fullScale;
    }

    public void Clear()
    {
        CancelRunningGeneration();

        foreach (GameObject structure in structures)
        {
            if (structure != null)
                Destroy(structure);
        }

        structures.Clear();
        placedPositions.Clear();
        placedClearances.Clear();
        layoutPlan.Clear();
        layoutCursor = 0;
    }

    /// <summary>
    /// Задаёт тему структур под текущую карту: явные материалы или
    /// палитра для фолбэк-кубов. Пустые списки — структуры остаются
    /// в материалах своих префабов (значение по умолчанию).
    /// Вызывается из EnvironmentController при смене карты.
    /// </summary>
    public void ApplyTheme(GameMap map)
    {
        if (map == null)
        {
            materials = null;
            palette = null;

            ApplyArenaLayout(null);

            return;
        }

        materials = map.structureMaterials;
        palette = map.structurePalette;

        ApplyArenaLayout(map.arenaLayout);
    }

    // Профиль боевой геометрии — единственное, что делает карты
    // разными по задаче. Значения переносятся в те же поля, из
    // которых генератор читает параметры по умолчанию, поэтому
    // путь «профиль → блоки» остаётся один и новый стиль не
    // требует второго генератора.
    private void ApplyArenaLayout(ArenaLayout layout)
    {
        CaptureInspectorDefaults();

        layoutPlan.Clear();
        layoutCursor = 0;

        if (layout == null)
        {
            activeLayoutStyle = ArenaLayoutStyle.Scattered;

            RestoreInspectorDefaults();

            return;
        }

        activeLayoutStyle = layout.style;

        minStructures = Mathf.Clamp(layout.minStructures, 0, 200);

        maxStructures = Mathf.Clamp(
            layout.maxStructures,
            minStructures,
            200
        );

        minWidth = Mathf.Max(layout.minWidth, 0.25f);
        maxWidth = Mathf.Max(layout.maxWidth, minWidth);

        minHeight = Mathf.Max(layout.minHeight, 0.25f);
        maxHeight = Mathf.Max(layout.maxHeight, minHeight);

        minSpacing = Mathf.Max(layout.minSpacing, 0f);

        structureHalfSize =
            Mathf.Clamp(layout.halfSize, 4f, 48f);

        minDistanceFromCenter =
            Mathf.Clamp(
                layout.centerClearRadius,
                0f,
                structureHalfSize
            );
    }

    private void CaptureInspectorDefaults()
    {
        if (inspectorDefaultsCaptured)
            return;

        inspectorDefaults = new DefaultLayout
        {
            minStructures = minStructures,
            maxStructures = maxStructures,
            minWidth = minWidth,
            maxWidth = maxWidth,
            minHeight = minHeight,
            maxHeight = maxHeight,
            minSpacing = minSpacing,
            structureHalfSize = structureHalfSize,
            minDistanceFromCenter = minDistanceFromCenter
        };

        inspectorDefaultsCaptured = true;
    }

    private void RestoreInspectorDefaults()
    {
        minStructures = inspectorDefaults.minStructures;
        maxStructures = inspectorDefaults.maxStructures;
        minWidth = inspectorDefaults.minWidth;
        maxWidth = inspectorDefaults.maxWidth;
        minHeight = inspectorDefaults.minHeight;
        maxHeight = inspectorDefaults.maxHeight;
        minSpacing = inspectorDefaults.minSpacing;
        structureHalfSize = inspectorDefaults.structureHalfSize;
        minDistanceFromCenter = inspectorDefaults.minDistanceFromCenter;
    }

    private void TrySpawnStructure(
        System.Random rng,
        Vector3 centerPos)
    {
        GameObject prefab = PickPrefab(rng);

        // Габариты считаются от авторасштаба префаба: у примитивного
        // куба он равен единице, у префаба — его масштаб в проекте.
        Vector3 baseScale =
            prefab != null
                ? prefab.transform.localScale
                : Vector3.one;

        Vector2 centerH =
            new Vector2(
                centerPos.x,
                centerPos.z
            );

        for (int attempt = 0;
             attempt < maxPlacementAttempts;
             attempt++)
        {
            if (!TryGetCandidate(rng, out PlannedStructure planned))
                continue;

            Vector3 scale = new Vector3(
                planned.dimensions.x * baseScale.x,
                planned.dimensions.y * baseScale.y,
                planned.dimensions.z * baseScale.z
            );

            float spacing =
                planned.spacingOverride >= 0f
                    ? planned.spacingOverride
                    : minSpacing;

            float clearance =
                Mathf.Max(scale.x, scale.z) * 0.5f +
                spacing;

            Vector2 hPos = planned.position + centerH;

            // Свободный центр арены: безопасная зона старта игрока.
            if ((hPos - centerH).magnitude <
                minDistanceFromCenter)
            {
                continue;
            }

            if (IsTooCloseToPlayer(hPos, clearance) ||
                !IsPositionFree(hPos, clearance))
            {
                continue;
            }

            SpawnStructure(prefab, hPos, scale, rng, planned.yaw);

            placedPositions.Add(hPos);
            placedClearances.Add(clearance);

            return;
        }
    }

    // =========================================================
    // LAYOUT PLANNING
    // =========================================================

    // Следующая кандидатура на блок: из плана, если стиль его
    // построил, иначе — исходный независимый бросок по площади.
    private bool TryGetCandidate(
        System.Random rng,
        out PlannedStructure planned)
    {
        if (activeLayoutStyle == ArenaLayoutStyle.Scattered ||
            layoutCursor >= layoutPlan.Count)
        {
            return TryGetScatterCandidate(rng, out planned);
        }

        // План — каркас, а не жёсткая сетка: небольшой разброс
        // убирает вид «разложенного по линейке» ряда.
        PlannedStructure slot = layoutPlan[layoutCursor++];

        slot.position +=
            new Vector2(
                NextFloat(rng, -0.8f, 0.8f),
                NextFloat(rng, -0.8f, 0.8f)
            );

        slot.yaw += NextFloat(rng, -6f, 6f);

        planned = slot;

        return true;
    }

    // Исходная раскладка: независимые броски по квадрату арены.
    // Осталась как запасной путь, когда план исчерпан.
    private bool TryGetScatterCandidate(
        System.Random rng,
        out PlannedStructure planned)
    {
        planned = new PlannedStructure
        {
            yaw = (float)(rng.NextDouble() * 360.0),
            spacingOverride = -1f
        };

        planned.dimensions = new Vector3(
            NextFloat(rng, minWidth, maxWidth),
            NextFloat(rng, minHeight, maxHeight),
            NextFloat(rng, minWidth, maxWidth)
        );

        if (useSquarePlacement)
        {
            // Квадрат: равномерно по боевой площади арены,
            // а не кольцом — как на референсе.
            float half = Mathf.Max(structureHalfSize, 1f);

            planned.position = new Vector2(
                NextFloat(rng, -half, half),
                NextFloat(rng, -half, half)
            );

            return true;
        }

        float angle =
            (float)(rng.NextDouble() * Mathf.PI * 2.0);

        float distance =
            NextFloat(rng, minDistanceFromCenter, arenaRadius);

        planned.position =
            new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) *
            distance;

        return true;
    }

    private void BuildLayoutPlan(System.Random rng, int count)
    {
        layoutPlan.Clear();
        layoutCursor = 0;

        // Плана делаем с запасом: часть слотов всё равно отбросят
        // проверки (центр арены, игрок, пересечения), и без запаса
        // арена вышла бы заметно пустее задуманного.
        int budget = Mathf.CeilToInt(count * 1.35f) + 4;

        float half = Mathf.Max(structureHalfSize, 1f);

        switch (activeLayoutStyle)
        {
            case ArenaLayoutStyle.Open:
                BuildOpenPlan(rng, budget, half);
                break;

            case ArenaLayoutStyle.Pillars:
                BuildPillarPlan(rng, budget, half);
                break;

            case ArenaLayoutStyle.Walls:
                BuildWallPlan(rng, budget, half);
                break;

            case ArenaLayoutStyle.Clusters:
                BuildClusterPlan(rng, budget, half);
                break;
        }
    }

// Открытое поле: блоков мало, они мелкие и низкие. Задача карты —
    // читать поле и маневрировать, а не прятаться. Высота не
    // уменьшается: блок ниже габарита игрока становится невидимым
    // препятствием, о которое спотыкаешься, а не укрытием.
    private void BuildOpenPlan(
        System.Random rng,
        int budget,
        float half)
    {
        for (int i = 0; i < budget; i++)
        {
            float width =
                NextFloat(rng, minWidth, maxWidth) * 0.7f;

            layoutPlan.Add(
                new PlannedStructure
                {
                    position = RandomPoint(rng, half),
                    dimensions =
                        new Vector3(
                            width,
                            NextFloat(rng, minHeight, maxHeight),
                            width
                        ),
                    yaw = (float)(rng.NextDouble() * 360.0),
                    spacingOverride = minSpacing * 3f
                }
            );
        }
    }

    // Колонны: узкие и высокие. Дальний бой встречает их как
    // разрывы линии огня, а не как сплошную стену укрытий.
    private void BuildPillarPlan(
        System.Random rng,
        int budget,
        float half)
    {
        for (int i = 0; i < budget; i++)
        {
            float thickness =
                NextFloat(rng, minWidth, maxWidth) * 0.6f;

            // Высота берётся из верхней части диапазона: колонна
            // ниже 4 метров не разрывает линию огня, а выше 6
            // начинает закрывать обзор с камеры.
            float height =
                Mathf.Lerp(
                    minHeight,
                    maxHeight,
                    NextFloat(rng, 0.8f, 1f)
                );

            layoutPlan.Add(
                new PlannedStructure
                {
                    position = RandomPoint(rng, half),
                    dimensions = new Vector3(
                        thickness,
                        height,
                        thickness
                    ),
                    yaw = (float)(rng.NextDouble() * 360.0),
                    spacingOverride = minSpacing * 2.5f
                }
            );
        }
    }

    // Стены с проходами. Каждая линия режется на две части разрывом
    // шириной не меньше двух метров — проём, в который проходит
    // игрок и любой противник. Ширина промежутков между линиями
    // заметно больше проёма, поэтому зажать игрока в коридоре
    // шириной с одного противника раскладка не может.
    private void BuildWallPlan(
        System.Random rng,
        int budget,
        float half)
    {
        int runs =
            Mathf.Clamp(
                Mathf.RoundToInt(budget / 7f),
                3,
                8
            );

        float wallThickness =
            NextFloat(rng, 1.2f, 2f);

        // Зазор внутри стены: блоки одной линии стоят рядом, и общий
        // minSpacing (плюс половина длины сегмента) вычеркнул бы их
        // все. Сегменты длинные, поэтому отталкиваемся только от
        // толщины стены.
        float wallSpacing = wallThickness * 0.5f + 0.4f;

        for (int i = 0; i < runs; i++)
        {
            bool alongX = rng.Next(0, 2) == 0;

            // Координата линии и её длина держатся внутри half, иначе
            // стены вылезут к тематическому декору на периметрии.
            float lane =
                NextFloat(rng, -half * 0.7f, half * 0.7f);

            float runCenter =
                NextFloat(rng, -half * 0.35f, half * 0.35f);

            float halfRun =
                NextFloat(rng, half * 0.28f, half * 0.42f);

            float runStart = runCenter - halfRun;
            float runLength = halfRun * 2f;

            // Проём не должен съесть линию: с обеих сторон остаётся
            // отрезок не короче 6 метров.
            float doorway =
                Mathf.Min(
                    NextFloat(rng, 5f, 8f),
                    Mathf.Max(runLength - 12f, 1f)
                );

            float doorwayCenter =
                NextFloat(
                    rng,
                    runStart + 6f + doorway * 0.5f,
                    runStart + runLength - 6f - doorway * 0.5f
                );

            float height =
                NextFloat(
                    rng,
                    2.5f,
                    Mathf.Max(Mathf.Min(maxHeight, 5f), 2.5f)
                );

            AddWallSegment(
                alongX,
                lane,
                runStart,
                doorwayCenter - doorway * 0.5f,
                wallThickness,
                height,
                wallSpacing
            );

            AddWallSegment(
                alongX,
                lane,
                doorwayCenter + doorway * 0.5f,
                runStart + runLength,
                wallThickness,
                height,
                wallSpacing
            );
        }

        // Остаток бюджета — обычные блоки между линиями: стены
        // дают коридоры, но арена не должна выглядеть пустой.
        for (int i = 0; i < budget; i++)
        {
            layoutPlan.Add(
                new PlannedStructure
                {
                    position = RandomPoint(rng, half),
                    dimensions =
                        new Vector3(
                            NextFloat(rng, minWidth, maxWidth),
                            NextFloat(rng, minHeight, maxHeight),
                            NextFloat(rng, minWidth, maxWidth)
                        ),
                    yaw = (float)(rng.NextDouble() * 360.0),
                    spacingOverride = -1f
                }
            );
        }
    }

    private void AddWallSegment(
        bool alongX,
        float lane,
        float start,
        float end,
        float thickness,
        float height,
        float spacing)
    {
        float length = end - start;

        if (length < 1f)
            return;

        float middle = (start + end) * 0.5f;

        Vector2 position =
            alongX
                ? new Vector2(middle, lane)
                : new Vector2(lane, middle);

        layoutPlan.Add(
            new PlannedStructure
            {
                position = position,
                dimensions =
                    alongX
                        ? new Vector3(length, height, thickness)
                        : new Vector3(thickness, height, length),
                yaw = 0f,
                spacingOverride = spacing
            }
        );
    }

    // Гроздья: плотные куски укрытия, между которыми остаются
    // широкие просветы. Внутри грозди блоки стоят впритык и дают
    // настоящее укрытие, снаружи — открытые коридоры для манёвра.
    private void BuildClusterPlan(
        System.Random rng,
        int budget,
        float half)
    {
        int clusters =
            Mathf.Clamp(
                Mathf.RoundToInt(budget / 7f),
                3,
                7
            );

        float innerSpacing = minSpacing * 0.5f;

        int planned = 0;

        for (int i = 0; i < clusters && planned < budget; i++)
        {
            Vector2 anchor = RandomPoint(rng, half * 0.85f);

            float blobRadius =
                NextFloat(rng, 3.5f, 5.5f);

            int blocks =
                NextInt(rng, 4, 8);

            for (int j = 0;
                 j < blocks && planned < budget;
                 j++, planned++)
            {
                layoutPlan.Add(
                    new PlannedStructure
                    {
                        position =
                            anchor +
                            RandomUnitVector(rng) *
                            NextFloat(rng, 0f, blobRadius),
                        dimensions =
                            new Vector3(
                                NextFloat(rng, minWidth, maxWidth),
                                NextFloat(rng, minHeight, maxHeight),
                                NextFloat(rng, minWidth, maxWidth)
                            ),
                        yaw = (float)(rng.NextDouble() * 360.0),
                        spacingOverride = innerSpacing
                    }
                );
            }
        }

        // Хвост — разбросанные блоки, чтобы пустоты между гроздьями
        // не выглядели вырезанными.
        for (int i = 0; i < budget; i++)
        {
            layoutPlan.Add(
                new PlannedStructure
                {
                    position = RandomPoint(rng, half),
                    dimensions =
                        new Vector3(
                            NextFloat(rng, minWidth, maxWidth),
                            NextFloat(rng, minHeight, maxHeight),
                            NextFloat(rng, minWidth, maxWidth)
                        ),
                    yaw = (float)(rng.NextDouble() * 360.0),
                    spacingOverride = -1f
                }
            );
        }
    }

    private Vector2 RandomPoint(System.Random rng, float half)
    {
        float bound = Mathf.Max(half, 1f);

        return new Vector2(
            NextFloat(rng, -bound, bound),
            NextFloat(rng, -bound, bound)
        );
    }

    private static Vector2 RandomUnitVector(System.Random rng)
    {
        float angle =
            (float)(rng.NextDouble() * Mathf.PI * 2.0);

        return new Vector2(
            Mathf.Cos(angle),
            Mathf.Sin(angle)
        );
    }

    private static int NextInt(
        System.Random rng,
        int minInclusive,
        int maxExclusive)
    {
        return rng.Next(minInclusive, maxExclusive);
    }

    private bool IsPositionFree(
        Vector2 hPos,
        float clearance)
    {
        for (int i = 0; i < placedPositions.Count; i++)
        {
            float distance =
                (placedPositions[i] - hPos).magnitude;

            if (distance <
                placedClearances[i] + clearance)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsTooCloseToPlayer(
        Vector2 hPos,
        float clearance)
    {
        if (player == null)
            return false;

        Vector2 playerH =
            new Vector2(
                player.position.x,
                player.position.z
            );

        float distance =
            (playerH - hPos).magnitude;

        return distance < clearance + playerSafeRadius;
    }

    // Без коллайдера префаб не блокирует ни игрока, ни пули, хотя
    // StructureQuery ищет его по collider. Для FBX это лечится
    // галочкой Generate Colliders в настройках импорта.
    private void WarnPrefabsWithoutColliders()
    {
        if (spawnPrefabs == null)
            return;

        for (int i = 0; i < spawnPrefabs.Length; i++)
        {
            if (spawnPrefabs[i].GetComponentInChildren<Collider>(true) != null)
                continue;

            Debug.LogWarning(
                $"[WorldStructureGenerator] У префаба " +
                $"'{spawnPrefabs[i].name}' нет коллайдера — он не " +
                "будет блокировать игрока и пули. Включи Generate " +
                "Colliders в настройках импорта модели или добавь " +
                "BoxCollider в префаб.",
                spawnPrefabs[i]
            );
        }
    }

    // Случайный префаб из списка. null — сигнал спавнить
    // примитивный куб вместо него.
    private GameObject PickPrefab(System.Random rng)
    {
        if (spawnPrefabs == null ||
            spawnPrefabs.Length == 0)
        {
            if (!warnedMissingPrefabs)
            {
                warnedMissingPrefabs = true;

                Debug.LogWarning(
                    "[WorldStructureGenerator] Список Structure Prefabs " +
                    "пуст — структуры создаются примитивными кубами. " +
                    "Заполни поле, чтобы спавнить свои префабы."
                );
            }

            return null;
        }

        return spawnPrefabs[rng.Next(0, spawnPrefabs.Length)];
    }

    private void SpawnStructure(
        GameObject prefab,
        Vector2 hPos,
        Vector3 scale,
        System.Random rng,
        float yaw)
    {
        bool useFallbackCube = prefab == null;

        GameObject structure =
            useFallbackCube
                ? GameObject.CreatePrimitive(PrimitiveType.Cube)
                : Instantiate(prefab);

        structure.name = $"Structure_{structures.Count}";

        // Префаб мог прийти уже с маркером — второй WorldStructure
        // не нужен.
        if (structure.GetComponent<WorldStructure>() == null)
            structure.AddComponent<WorldStructure>();

        if (cachedWorldLayer >= 0)
            SetLayerRecursive(structure, cachedWorldLayer);

        structure.transform.SetParent(transform, false);

        PlaceOnGround(structure, hPos, scale);

        structure.transform.localRotation =
            Quaternion.Euler(
                0f,
                yaw,
                0f
            );

        ApplyLook(structure, rng, useFallbackCube);

        structures.Add(structure);

        StartTrackedCoroutine(
            ScaleInStructure(structure, scale)
        );
    }

    // Ставит структуру на землю нижней гранью. У префабов пивот
    // обычно в основании, а не в центре габаритов, поэтому
    // смещение считается по реальным габаритам объекта.
    private void PlaceOnGround(
        GameObject structure,
        Vector2 hPos,
        Vector3 scale)
    {
        Transform instanceTransform = structure.transform;

        // Поворот выставляется позже: габариты читаются в мировых
        // координатах, и при повороте вокруг Y «раздуваются» по X/Z.
        instanceTransform.localRotation = Quaternion.identity;
        instanceTransform.localScale = scale;

        float localBottom = GetLocalBottom(structure, scale);

        instanceTransform.position =
            new Vector3(
                hPos.x,
                groundY - localBottom,
                hPos.y
            );
    }

    // Коллайдеры — основной источник габаритов: bounds рендереров
    // после смены масштаба могут ещё кадр отдавать прежние
    // значения. Без коллайдеров (типично для FBX-моделей) считаем
    // по мешам через матрицу трансформа — она обновляется сразу, —
    // а если мешей нет, подстраховываемся половиной высоты.
    private static float GetLocalBottom(
        GameObject structure,
        Vector3 scale)
    {
        Transform instanceTransform = structure.transform;

        Bounds bounds = new Bounds();
        bool hasBounds = false;

        Collider[] colliders =
            structure.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (hasBounds)
                bounds.Encapsulate(colliders[i].bounds);
            else
            {
                bounds = colliders[i].bounds;
                hasBounds = true;
            }
        }

        if (!hasBounds)
        {
            MeshFilter[] filters =
                structure.GetComponentsInChildren<MeshFilter>(true);

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;

                if (mesh == null)
                    continue;

                Matrix4x4 matrix =
                    filters[i].transform.localToWorldMatrix;

                Vector3 center =
                    matrix.MultiplyPoint3x4(mesh.bounds.center);

                Vector3 extents = Abs(
                    matrix.MultiplyVector(mesh.bounds.extents)
                );

                Vector3 meshMin = center - extents;
                Vector3 meshMax = center + extents;

                if (!hasBounds)
                {
                    bounds.SetMinMax(meshMin, meshMax);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(meshMin);
                    bounds.Encapsulate(meshMax);
                }
            }
        }

        return hasBounds
            ? instanceTransform.InverseTransformPoint(bounds.min).y
            : -0.5f * scale.y;
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z)
        );
    }

    private static void SetLayerRecursive(
        GameObject target,
        int layer)
    {
        target.layer = layer;

        Transform targetTransform = target.transform;

        for (int i = 0;
             i < targetTransform.childCount;
             i++)
        {
            SetLayerRecursive(
                targetTransform.GetChild(i).gameObject,
                layer
            );
        }
    }

    // Явные материалы накладываются на все рендереры префаба.
    // Палитра — только для фолбэк-куба, у которого своего
    // материала нет.
    private void ApplyLook(
        GameObject structure,
        System.Random rng,
        bool allowPalette)
    {
        Material material = null;

        if (materials != null &&
            materials.Length > 0)
        {
            // sharedMaterial не создаёт копию материала на каждый спавн.
            material =
                materials[
                    rng.Next(0, materials.Length)
                ];
        }
        else if (allowPalette)
        {
            EnsurePaletteMaterials();

            if (paletteMaterials != null)
            {
                material =
                    paletteMaterials[
                        rng.Next(0, paletteMaterials.Length)
                    ];
            }
        }

        if (material == null)
            return;

        Renderer[] renderers =
            structure.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].sharedMaterial = material;
    }

    // Нейтральные серые блоки: игровые препятствия одни для всех карт
    // и не должны перекрашиваться в цвета темы (только если палитра
    // карты явно задана в её полях structures).
    private static readonly Color[] NeutralBlockColors =
    {
        new Color(0.58f, 0.62f, 0.66f, 1f),
        new Color(0.67f, 0.71f, 0.74f, 1f),
        new Color(0.77f, 0.79f, 0.81f, 1f)
    };

    // Создаёт по одному материалу на цвет: явная палитра карты —
    // или нейтральные серые кубы, когда палитра не задана (референс:
    // блоки арены одинаковые на всех картах). Шейдер берём тот, что
    // гарантированно попадает в билд (в отличие от Default-Material,
    // который CreatePrimitive вешает в редакторе, но чей шейдер
    // вырезается из собранной игры).
    private void EnsurePaletteMaterials()
    {
        if (paletteMaterials != null)
            return;

        Shader shader = GetBuildSafeShader();

        if (shader == null)
            return;

        Color[] colors =
            (palette != null &&
             palette.Length > 0)
                ? palette
                : NeutralBlockColors;

        paletteMaterials =
            new Material[colors.Length];

        for (int i = 0; i < colors.Length; i++)
        {
            Material material =
                new Material(shader);

            material.name =
                $"Structure Palette {i}";

            SetMaterialColor(material, colors[i]);

            paletteMaterials[i] = material;
        }
    }

    // Возвращает шейдер, который точно есть в билде: URP Lit/Unlit
    // используется материалами проекта, Standard — фолбэк.
    private static Shader GetBuildSafeShader()
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Lit");

        if (shader != null)
            return shader;

        shader =
            Shader.Find("Universal Render Pipeline/Unlit");

        if (shader != null)
            return shader;

        shader = Shader.Find("Standard");

        if (shader != null)
            return shader;

        return Shader.Find("Unlit/Color");
    }

    // URP-Lit хранит базовый цвет в _BaseColor, а не в _Color.
    private static void SetMaterialColor(
        Material material,
        Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static float NextFloat(
        System.Random rng,
        float min,
        float max)
    {
        return
            min +
            (max - min) *
            (float)rng.NextDouble();
    }

    private void OnDestroy()
    {
        Clear();
    }
}