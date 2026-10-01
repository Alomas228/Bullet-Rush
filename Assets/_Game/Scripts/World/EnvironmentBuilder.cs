using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Строит окружение карты по EnvironmentTheme: препятствия внутри
/// арены (с коллайдерами, классифицируются как структуры — игрок
/// скользит вдоль, враги обходят, пули гасятся, камера растворяет
/// при заслоне), мелкая декорация на подложке и фон за стенами
/// (без коллайдеров). Все объекты — дети слоя карты и уезжают
/// вместе со слайдом между картами.
///
/// Объекты собираются из примитивов с общими мешами и по одному
/// материалу на цвет палитры: в WebGL сотня объектов обходится
/// почти даром, без текстур и префабов.
/// </summary>
public static class EnvironmentBuilder
{
    private const int MaxPlacementAttempts = 64;
    private const int DefaultSeed = 12345;
    private const int DefaultLayer = 0;

    // Общие материалы на тему: одна тема — один набор, кэш не
    // двоится при повторных слайдах между картами.
    private static readonly Dictionary<string, Material[]> materialCache =
        new Dictionary<string, Material[]>();

    private static readonly List<Vector2> placed =
        new List<Vector2>();

    private static readonly List<float> placedFootprints =
        new List<float>();

    private static readonly List<Renderer> rendererBuffer =
        new List<Renderer>(16);

    /// <summary>
    /// Создаёт всё окружение темы как дочерние объекты parent.
    /// groundY — высота пола арены: объекты «стоят» на нём.
    /// </summary>
    public static void Build(
        EnvironmentTheme theme,
        Transform parent,
        float groundY)
    {
        if (theme == null ||
            parent == null)
        {
            return;
        }

        placed.Clear();
        placedFootprints.Clear();

        System.Random rng =
            new System.Random(
                theme.seed != 0
                    ? theme.seed
                    : DefaultSeed
            );

        Material[] materials =
            GetThemeMaterials(theme);

        int worldLayer =
            LayerMask.NameToLayer("World");

        // Зоны расстановки:
        //  - центральная боевая зона занята нейтральными блоками
        //    WorldStructureGenerator — тематических объектов там нет;
        //  - тематические объекты (препятствия и декор) живут на
        //    периметре квадратной арены и в углах (Border/ThemeZone);
        //  - фон — кольцо за стенами арены (OutsideZone).
        BuildList(
            rng,
            theme.insideObstacles,
            parent,
            groundY,
            true,
            materials,
            theme.glowMaterial,
            worldLayer,
            theme.minBorderHalfSize,
            theme.maxBorderHalfSize,
            theme.minCornerHalfSize,
            theme.maxCornerHalfSize,
            theme.minBorderSpacing
        );

        BuildList(
            rng,
            theme.insideDecor,
            parent,
            groundY,
            false,
            materials,
            theme.glowMaterial,
            worldLayer,
            theme.minBorderHalfSize,
            theme.maxBorderHalfSize,
            theme.minCornerHalfSize,
            theme.maxCornerHalfSize,
            theme.minBorderSpacing
        );

        BuildList(
            rng,
            theme.outsideObjects,
            parent,
            groundY,
            false,
            materials,
            theme.glowMaterial,
            worldLayer,
            theme.minOutsideDistance,
            theme.maxOutsideDistance,
            0f,
            0f,
            theme.minOutsideSpacing
        );
    }

    private static void BuildList(
        System.Random rng,
        EnvironmentObjectDef[] defs,
        Transform parent,
        float groundY,
        bool obstacles,
        Material[] materials,
        Material glowMaterial,
        int worldLayer,
        float minDistance,
        float maxDistance,
        float cornerMin,
        float cornerMax,
        float spacing)
    {
        if (defs == null)
            return;

        for (int d = 0; d < defs.Length; d++)
        {
            EnvironmentObjectDef def = defs[d];

            if (def == null)
                continue;

            // Пустой рецепт — только если нет ни частей, ни модели.
            if (def.parts == null ||
                def.parts.Length == 0)
            {
                if (def.model == null)
                    continue;
            }

            int count = Mathf.Max(def.count, 0);

            for (int i = 0; i < count; i++)
            {
                Vector2 spot =
                    FindSpot(
                        rng,
                        def,
                        minDistance,
                        maxDistance,
                        cornerMin,
                        cornerMax,
                        spacing
                    );

                SpawnObject(
                    def,
                    spot,
                    rng,
                    parent,
                    groundY,
                    obstacles,
                    materials,
                    glowMaterial,
                    worldLayer
                );
            }
        }
    }

    // Ищет свободное место по способу расстановки объекта.
    // fixedPlacement ставится в точку без проверок (море, планета).
    private static Vector2 FindSpot(
        System.Random rng,
        EnvironmentObjectDef def,
        float minDistance,
        float maxDistance,
        float cornerMin,
        float cornerMax,
        float spacing)
    {
        if (def.fixedPlacement)
            return def.fixedOffset;

        float defMin =
            def.overrideMinRadius > 0f
                ? def.overrideMinRadius
                : minDistance;

        float defMax =
            def.overrideMaxRadius > 0f
                ? def.overrideMaxRadius
                : maxDistance;

        switch (def.placement)
        {
            case EnvironmentObjectPlacement.Corner:
            {
                float cornerEffectiveMin =
                    def.overrideMinRadius > 0f
                        ? def.overrideMinRadius
                        : cornerMin;

                float cornerEffectiveMax =
                    def.overrideMaxRadius > 0f
                        ? def.overrideMaxRadius
                        : (cornerMax > 0f ? cornerMax : defMax);

                return FindCornerSpot(
                    rng,
                    def,
                    cornerEffectiveMin,
                    cornerEffectiveMax,
                    spacing
                );
            }

            case EnvironmentObjectPlacement.SquareFrame:
                return FindSpotOnFrame(
                    rng,
                    def,
                    defMin,
                    defMax,
                    spacing
                );

            default:
                return FindSpotInRing(
                    rng,
                    def,
                    defMin,
                    defMax,
                    spacing
                );
        }
    }

    // Кольцо вокруг центра (старое поведение) — фан за стенами
    // и совместимость с темами, где placement не задан.
    private static Vector2 FindSpotInRing(
        System.Random rng,
        EnvironmentObjectDef def,
        float minDistance,
        float maxDistance,
        float spacing)
    {
        float minR = Mathf.Max(minDistance, 0f);
        float maxR = Mathf.Max(maxDistance, minR);

        for (int attempt = 0;
             attempt < MaxPlacementAttempts;
             attempt++)
        {
            float angle =
                (float)(rng.NextDouble() * Mathf.PI * 2.0);

            float distance =
                NextFloat(rng, minR, maxR);

            Vector2 offset =
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                ) *
                distance;

            if (IsSpotFree(offset, def.footprint, spacing))
            {
                placed.Add(offset);
                placedFootprints.Add(def.footprint);

                return offset;
            }
        }

        return PlaceOnFallback(
            rng,
            def,
            minR,
            (minR + maxR) * 0.5f,
            spacing
        );
    }

    // Периметр квадратной арены: случайная сторона и точка вдоль неё.
    // Так рамка повторяет форму арены (референс), а не круг.
    private static Vector2 FindSpotOnFrame(
        System.Random rng,
        EnvironmentObjectDef def,
        float minHalf,
        float maxHalf,
        float spacing)
    {
        float minD = Mathf.Max(minHalf, 0f);
        float maxD = Mathf.Max(maxHalf, minD);

        for (int attempt = 0;
             attempt < MaxPlacementAttempts;
             attempt++)
        {
            float depth = NextFloat(rng, minD, maxD);
            float lateral = NextFloat(rng, -depth, depth);

            Vector2 point = SidePoint(rng.Next(0, 4), lateral, depth);

            if (IsSpotFree(point, def.footprint, spacing))
            {
                placed.Add(point);
                placedFootprints.Add(def.footprint);

                return point;
            }
        }

        return PlaceOnFallback(
            rng,
            def,
            minD,
            (minD + maxD) * 0.5f,
            spacing
        );
    }

    // Углы квадрата (±d, ±d): крупные акцентные объекты визуально
    // замыкают рамку, как на референсе.
    private static Vector2 FindCornerSpot(
        System.Random rng,
        EnvironmentObjectDef def,
        float minHalf,
        float maxHalf,
        float spacing)
    {
        float minD = Mathf.Max(minHalf, 0f);
        float maxD = Mathf.Max(maxHalf, minD);

        for (int attempt = 0;
             attempt < MaxPlacementAttempts;
             attempt++)
        {
            float d = NextFloat(rng, minD, maxD);

            float signX = rng.Next(0, 2) == 0 ? -1f : 1f;
            float signZ = rng.Next(0, 2) == 0 ? -1f : 1f;

            var point = new Vector2(
                signX * (d + NextFloat(rng, -0.9f, 0.9f)),
                signZ * (d + NextFloat(rng, -0.9f, 0.9f))
            );

            if (IsSpotFree(point, def.footprint, spacing))
            {
                placed.Add(point);
                placedFootprints.Add(def.footprint);

                return point;
            }
        }

        return PlaceOnFallback(
            rng,
            def,
            minD,
            (minD + maxD) * 0.5f,
            spacing
        );
    }

    // Точка на стороне квадрата: (lateral, ±depth) или (±depth, lateral).
    private static Vector2 SidePoint(
        int side,
        float lateral,
        float depth)
    {
        switch (side)
        {
            case 0:
                return new Vector2(lateral, depth);

            case 1:
                return new Vector2(lateral, -depth);

            case 2:
                return new Vector2(depth, lateral);

            default:
                return new Vector2(-depth, lateral);
        }
    }

    // Место не нашлось за все попытки — ставим на середине зоны
    // и занимаем его, иначе объект молча теряется.
    private static Vector2 PlaceOnFallback(
        System.Random rng,
        EnvironmentObjectDef def,
        float minDistance,
        float fallbackDistance,
        float spacing)
    {
        float midR =
            Mathf.Max(
                minDistance,
                fallbackDistance
            );

        float lastAngle =
            (float)(rng.NextDouble() * Mathf.PI * 2.0);

        Vector2 fallback =
            new Vector2(
                Mathf.Cos(lastAngle),
                Mathf.Sin(lastAngle)
            ) *
            midR;

        placed.Add(fallback);
        placedFootprints.Add(def.footprint);

        return fallback;
    }

    private static bool IsSpotFree(
        Vector2 point,
        float footprint,
        float spacing)
    {
        for (int i = 0; i < placed.Count; i++)
        {
            float distance =
                (placed[i] - point).magnitude;

            if (distance <
                placedFootprints[i] + footprint + spacing)
            {
                return false;
            }
        }

        return true;
    }

    private static void SpawnObject(
        EnvironmentObjectDef def,
        Vector2 spot,
        System.Random rng,
        Transform parent,
        float groundY,
        bool obstacle,
        Material[] materials,
        Material glowMaterial,
        int worldLayer)
    {
        GameObject root =
            new GameObject(def.name);

        root.transform.SetParent(parent, false);

        root.transform.localPosition =
            new Vector3(
                spot.x,
                groundY,
                spot.y
            );

        root.transform.localRotation =
            Quaternion.Euler(
                0f,
                (float)(rng.NextDouble() * 360.0),
                0f
            );

        float globalScale =
            NextFloat(
                rng,
                def.scaleMin,
                def.scaleMax
            );

        if (def.model != null)
        {
            SpawnModel(
                root.transform,
                def.model,
                globalScale,
                obstacle
            );
        }
        else
        {
            BuildParts(
                root.transform,
                def.parts,
                materials,
                glowMaterial,
                globalScale
            );
        }

        if (!obstacle)
            return;

        // Слой World задаём после появления детей, чтобы и рендереры,
        // и коллайдеры (части или модели) попали на слой структур.
        if (worldLayer >= DefaultLayer)
            SetLayerRecursive(root, worldLayer);

        // Свои коллайдеры модели остаются как есть — это её форма.
        // Если их нет, считаем один общий BoxCollider по габаритам,
        // как у примитивных объектов.
        if (!HasAnyCollider(root))
            AddObstacleCollider(root);

        root.AddComponent<WorldStructure>();
    }

    // Собственный префаб-объект вместо сборки из примитивов: общий
    // масштаб задаётся корню целиком, коллайдеры модели сохраняются
    // (препятствия), декору коллайдеры отключаются.
    private static void SpawnModel(
        Transform root,
        GameObject modelPrefab,
        float globalScale,
        bool obstacle)
    {
        GameObject instance =
            Object.Instantiate(
                modelPrefab,
                root,
                false
            );

        instance.name = "Model";

        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        root.localScale =
            new Vector3(
                globalScale,
                globalScale,
                globalScale
            );

        if (obstacle)
            return;

        Collider[] colliders =
            instance.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    private static bool HasAnyCollider(GameObject root)
    {
        Collider[] colliders =
            root.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null &&
                colliders[i].enabled)
            {
                return true;
            }
        }

        return false;
    }

    private static void BuildParts(
        Transform root,
        EnvironmentPart[] parts,
        Material[] materials,
        Material glowMaterial,
        float globalScale)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            EnvironmentPart part = parts[i];

            if (part == null)
                continue;

            GameObject primitive =
                GameObject.CreatePrimitive(
                    ToPrimitiveType(part.shape)
                );

            primitive.name =
                root.name + "_Part" + i;

            primitive.transform.SetParent(root, false);
            primitive.transform.localPosition =
                part.position * globalScale;
            primitive.transform.localEulerAngles =
                part.euler;
            primitive.transform.localScale =
                part.scale * globalScale;

            RemovePrimitiveCollider(primitive);

            Renderer renderer =
                primitive.GetComponent<Renderer>();

            if (renderer == null)
                continue;

            Material material =
                part.glow && glowMaterial != null
                    ? glowMaterial
                    : materials[Mathf.Clamp(
                        part.color,
                        0,
                        materials.Length - 1
                    )];

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode =
                ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    // Коллайдеры примитивов не нужны ни у декора, ни у препятствий:
    // у декора физики нет вообще, у препятствий свой BoxCollider на
    // корне. Заодно мешающийся коллайдер Quad/Sphere убирается.
    private static void RemovePrimitiveCollider(
        GameObject primitive)
    {
        Collider collider =
            primitive.GetComponent<Collider>();

        if (collider == null)
            return;

        collider.enabled = false;
        Object.Destroy(collider);
    }

    // BoxCollider на корне по габаритам всех частей. Локальные
    // габариты считаются через инверсную матрицу корня: у корня
    // случайный поворот, а коллайдер должен повторять фигуру.
    private static void AddObstacleCollider(
        GameObject root)
    {
        rendererBuffer.Clear();

        root.GetComponentsInChildren(
            false,
            rendererBuffer
        );

        Bounds bounds = new Bounds();
        bool hasBounds = false;

        for (int i = 0; i < rendererBuffer.Count; i++)
        {
            Renderer renderer = rendererBuffer[i];

            if (renderer == null)
                continue;

            if (hasBounds)
                bounds.Encapsulate(renderer.bounds);
            else
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
        }

        if (!hasBounds)
            return;

        Transform rootTransform = root.transform;

        Vector3 localCenter =
            rootTransform.InverseTransformPoint(bounds.center);

        Vector3 localSize =
            Abs(
                rootTransform.InverseTransformVector(bounds.size)
            );

        if (localSize.x < 0.01f ||
            localSize.y < 0.01f ||
            localSize.z < 0.01f)
        {
            return;
        }

        BoxCollider collider =
            root.AddComponent<BoxCollider>();

        collider.center = localCenter;

        // Небольшой запас по X/Z, чтобы игрок не проскакивал
        // в щель между видимой кроной и коллайдером.
        collider.size =
            localSize +
            new Vector3(0.04f, 0f, 0.04f);
    }

    private static PrimitiveType ToPrimitiveType(
        EnvironmentShape shape)
    {
        switch (shape)
        {
            case EnvironmentShape.Sphere:
                return PrimitiveType.Sphere;

            case EnvironmentShape.Cylinder:
                return PrimitiveType.Cylinder;

            case EnvironmentShape.Capsule:
                return PrimitiveType.Capsule;

            case EnvironmentShape.Quad:
                return PrimitiveType.Quad;

            default:
                return PrimitiveType.Cube;
        }
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

    // По одному материалу на цвет палитры темы. Кэш по имени темы:
    // повторный слайд на ту же карту переиспользует материалы.
    private static Material[] GetThemeMaterials(
        EnvironmentTheme theme)
    {
        string key = theme.name;

        if (!string.IsNullOrEmpty(key) &&
            materialCache.TryGetValue(
                key,
                out Material[] cached))
        {
            return cached;
        }

        Shader shader =
            Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Standard");

        Color[] colors =
            (theme.palette != null &&
             theme.palette.Length > 0)
                ? theme.palette
                : new[]
                {
                    new Color(0.7f, 0.7f, 0.7f, 1f)
                };

        Material[] materials =
            new Material[colors.Length];

        for (int i = 0; i < colors.Length; i++)
        {
            Material material =
                new Material(shader)
                {
                    name = key + " Env " + i
                };

            SetMaterialColor(material, colors[i]);

            // GPU instancing, а не надежда на SRP Batcher.
            //
            // SRP Batcher НЕ снижает число draw call - он только
            // удешевляет их подготовку (bind состояния), каждый
            // Renderer всё равно даёт свою пару bind+draw.
            //
            // Instancing же режет число draw call по-настоящему:
            // все части декора строятся из встроенных примитивов
            // (все кубы ссылаются на один Cube.fbx), поэтому
            // одинаковые меш+материал собираются в один
            // инстансированный вызов. На декоре это ~300
            // рендереров на 6-8 материалов - ровно тот случай,
            // где инстансинг окупается.
            //
            // На препятствиях и структурах instancing НЕ включаем:
            // StructureOcclusionFader подменяет sharedMaterial на
            // конкретной структуре, а инстансинг группирует по
            // меш+материал и такой подмены не переживает.
            //
            // Картинка не меняется: instancing даёт тот же шейдер
            // и те же текстуры, только матрицы едут в буфере.
            material.enableInstancing = true;

            materials[i] = material;
        }

        if (!string.IsNullOrEmpty(key))
            materialCache[key] = materials;

        return materials;
    }

    // URP-Lit хранит базовый цвет в _BaseColor, встроенный
    // Standard — в _Color.
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

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z)
        );
    }
}