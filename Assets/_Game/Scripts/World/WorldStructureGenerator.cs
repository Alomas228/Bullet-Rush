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
    [SerializeField] private float arenaRadius = 30f;
    [SerializeField] private float minDistanceFromCenter = 4f;
    [SerializeField] private float minSpacing = 1f;
    [SerializeField] private int maxPlacementAttempts = 30;

    [Header("Prefabs")]
    [Tooltip("Префабы, которые будут спавниться вместо кубов. Для каждой структуры выбирается один случайный из списка. Пусто — фолбэк на примитивный куб.")]
    [SerializeField] private GameObject[] structurePrefabs;

    [Header("Count")]
    [SerializeField] private int minStructures = 14;
    [SerializeField] private int maxStructures = 20;

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

    [Header("Layer")]
    [Tooltip("Слой, на который помещаются создаваемые структуры (включая дочерние объекты префаба). Должен совпадать с occlusionMask в StructureOcclusionManager. Пусто — слой не меняется.")]
    [SerializeField] private string structureLayerName = "World";

    private readonly List<GameObject> structures =
        new List<GameObject>();

    private readonly List<Vector2> placedPositions =
        new List<Vector2>();

    private readonly List<float> placedClearances =
        new List<float>();

    // Список префабов без пустых слотов: случайный выбор не должен
    // упираться в null и молча подменяться кубом.
    private GameObject[] spawnPrefabs;
    private bool warnedMissingPrefabs;

    private Coroutine generateCoroutine;
    private int cachedWorldLayer = -1;

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

    public void GenerateForWave(int wave)
    {
        if (generateCoroutine != null)
            StopCoroutine(generateCoroutine);

        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }

        Vector3 centerPos =
            center != null
                ? center.position
                : transform.position;

        groundY = centerPos.y;

        System.Random rng =
            new System.Random(
                baseSeed * 31 + wave * 131
            );

        int count =
            rng.Next(
                minStructures,
                maxStructures + 1
            );

        generateCoroutine = StartCoroutine(
            TransitionToWaveRoutine(
                rng,
                centerPos,
                count
            )
        );
    }

    private IEnumerator TransitionToWaveRoutine(
        System.Random rng,
        Vector3 centerPos,
        int count)
    {
        WaitForSecondsRealtime staggerWait =
            new WaitForSecondsRealtime(spawnStagger);

        WaitForSecondsRealtime growWait =
            new WaitForSecondsRealtime(scaleInDuration);

        if (structures.Count > 0)
        {
            List<GameObject> oldStructures =
                new List<GameObject>(structures);

            structures.Clear();
            placedPositions.Clear();
            placedClearances.Clear();

            int perTick = Mathf.Max(structuresPerTick, 1);

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
                        StartCoroutine(
                            FadeOutStructure(
                                oldStructures[j]
                            )
                        );
                }

                yield return staggerWait;
            }

            yield return growWait;
        }

        int spawnPerTick = Mathf.Max(structuresPerTick, 1);

        for (int i = 0; i < count; i += spawnPerTick)
        {
            int batchEnd =
                Mathf.Min(
                    i + spawnPerTick,
                    count
                );

            for (int j = i; j < batchEnd; j++)
            {
                TrySpawnStructure(rng, centerPos);
            }

            yield return staggerWait;
        }

        // Ждём роста последнего куба, чтобы генерация
        // считалась завершённой только когда всё выросло.
        yield return growWait;

        generateCoroutine = null;
    }

    private IEnumerator FadeOutStructure(
        GameObject structure)
    {
        if (structure == null)
            yield break;

        // Коллайдеры префаба часто висят на дочерних объектах,
        // иначе исчезающая структура продолжитт блокировать пули
        // и перемещение до конца анимации.
        Collider[] colliders =
            structure.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Vector3 startScale =
            structure.transform.localScale;

        float timer = 0f;

        while (timer < scaleInDuration)
        {
            if (structure == null)
                yield break;

            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / scaleInDuration
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
        structure.transform.localScale = Vector3.zero;

        float timer = 0f;

        while (timer < scaleInDuration)
        {
            if (structure == null)
                yield break;

            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / scaleInDuration
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
        foreach (GameObject structure in structures)
        {
            if (structure != null)
                Destroy(structure);
        }

        structures.Clear();
        placedPositions.Clear();
        placedClearances.Clear();
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

        for (int attempt = 0;
             attempt < maxPlacementAttempts;
             attempt++)
        {
            float width = NextFloat(rng, minWidth, maxWidth);
            float depth = NextFloat(rng, minWidth, maxWidth);
            float height = NextFloat(rng, minHeight, maxHeight);

            Vector3 scale = new Vector3(
                width * baseScale.x,
                height * baseScale.y,
                depth * baseScale.z
            );

            float clearance =
                Mathf.Max(scale.x, scale.z) * 0.5f +
                minSpacing;

            float angle =
                (float)(rng.NextDouble() * Mathf.PI * 2.0);

            float distance =
                NextFloat(
                    rng,
                    minDistanceFromCenter,
                    arenaRadius
                );

            Vector2 offset =
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                ) *
                distance;

            Vector2 hPos =
                new Vector2(
                    centerPos.x,
                    centerPos.z
                ) +
                offset;

            if (IsTooCloseToPlayer(hPos, clearance) ||
                !IsPositionFree(hPos, clearance))
            {
                continue;
            }

            SpawnStructure(prefab, hPos, scale, rng);

            placedPositions.Add(hPos);
            placedClearances.Add(clearance);

            return;
        }
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
        System.Random rng)
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
                (float)(rng.NextDouble() * 360.0),
                0f
            );

        ApplyLook(structure, rng, useFallbackCube);

        structures.Add(structure);

        StartCoroutine(
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

    // Создаёт по одному материалу на цвет из шейдера, который
    // гарантированно попадает в билд (в отличие от Default-Material,
    // который CreatePrimitive вешает в редакторе, но чей шейдер
    // вырезается из собранной игры). Если палитра пуста — берёт
    // нейтральный серый, чтобы кубы не были магентовыми.
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
                : new[]
                {
                    new Color(0.7f, 0.7f, 0.7f, 1f)
                };

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