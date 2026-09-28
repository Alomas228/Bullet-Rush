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

        BuildList(
            rng,
            theme.insideObstacles,
            parent,
            groundY,
            true,
            materials,
            theme.glowMaterial,
            worldLayer,
            theme.minCenterDistance,
            theme.maxCenterDistance,
            theme.minObstacleSpacing
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
            theme.minDecorDistance,
            theme.maxDecorDistance,
            theme.minObstacleSpacing
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
        float spacing)
    {
        if (defs == null)
            return;

        for (int d = 0; d < defs.Length; d++)
        {
            EnvironmentObjectDef def = defs[d];

            if (def == null ||
                def.parts == null ||
                def.parts.Length == 0)
            {
                continue;
            }

            float defMin =
                def.overrideMinRadius > 0f
                    ? def.overrideMinRadius
                    : minDistance;

            float defMax =
                def.overrideMaxRadius > 0f
                    ? def.overrideMaxRadius
                    : maxDistance;

            int count = Mathf.Max(def.count, 0);

            for (int i = 0; i < count; i++)
            {
                Vector2 spot =
                    FindSpot(
                        rng,
                        def,
                        defMin,
                        defMax,
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

    // Ищет свободное место в кольце. fixedPlacement ставится в точку
    // без проверок (море, планета, луна).
    private static Vector2 FindSpot(
        System.Random rng,
        EnvironmentObjectDef def,
        float minDistance,
        float maxDistance,
        float spacing)
    {
        if (def.fixedPlacement)
            return def.fixedOffset;

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

        // Место не нашлось за все попытки — ставим на середине
        // кольца и занимаем его, иначе объект молча теряется.
        float midR =
            Mathf.Max(minR, (minR + maxR) * 0.5f);

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