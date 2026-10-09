using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Общий рендерер луж крови.
//
// Все живые BloodPool складывают свою геометрию в два динамических
// меша: центральные пятна в один, брызги в другой. Каждый меш рисует
// один MeshRenderer, поэтому вся кровь на карте - 2 draw call'а,
// сколько бы луж ни лежало (раньше было 2 на каждую лужу).
//
// Отличие от BloodBatcher капель: форма лужи статична на всё время
// её жизни. Поэтому общий меш пересобирается не каждый кадр, а
// только когда BloodPool пометил его грязным - при спавне и при
// истечении лужи. В спокойном бою это единицы пересборок в секунду.
//
// Вершины пишутся сразу в мир (как в BloodBatcher), батчер стоит в
// нуле с единичным масштабом. Формат квада постоянен: 4 вершины,
// uv и индексы не зависят от числа луж, поэтому топология строится
// один раз при выделении меша, а каждый rebuild обновляет только
// позиции.
//
// Затухание делается здесь же и сразу для всех луж: FadeOut плавно
// сжимает квады каждой лужи к её центру (тот же "фейд", что
// WorldFadeOutManager делает масштабом для остальных объектов, но
// общий), после чего гасит рендереры.
[DefaultExecutionOrder(300)]
public sealed class BloodPoolBatcher : MonoBehaviour
{
    /// <summary>
    /// Вершин на квад.
    /// </summary>
    private const int VerticesPerQuad = 4;

    /// <summary>
    /// Стартовая ёмкость в лужах. Держим в одном месте и для пятна,
    /// и для брызг: брызг нужно MaxSplatters на каждую лужу.
    /// </summary>
    private const int InitialPoolCapacity = 16;

    /// <summary>
    /// Потолок в лужах. Выше MaxPooled у BloodPool на карте быть не
    /// должно, запас оставлен на случай изменения лимита.
    /// </summary>
    private const int MaxPoolCapacity = 64;

    private const float BoundsPadding = 0.5f;

    private static BloodPoolBatcher instance;

    private static bool dirty;
    private static bool fading;
    private static float shrink = 1f;

    private static Material mainMaterial;
    private static Material splatterMaterial;

    private Mesh mainMesh;
    private Mesh splatterMesh;
    private MeshRenderer mainRenderer;
    private MeshRenderer splatterRenderer;

    private Vector3[] mainVertices = new Vector3[0];
    private Vector3[] splatterVertices = new Vector3[0];

    // Ёмкость в лужах: mainQuadCapacity для пятна,
    // splatterQuadCapacity для брызг (лужи * MaxSplatters).
    private int mainQuadCapacity;
    private int splatterQuadCapacity;

    private int writtenMainQuads;
    private int writtenSplatterQuads;

    /// <summary>
    /// Идёт ли сейчас общее затухание крови.
    /// </summary>
    public static bool IsFading => fading;

    /// <summary>
    /// Пометить общий меш грязным. Зовётся из BloodPool при спавне,
    /// истечении и очистке.
    /// </summary>
    public static void NotifyChanged()
    {
        dirty = true;
        EnsureExists();
    }

    /// <summary>
    /// Общее затухание всех луж разом. Сжимает каждое пятно к его
    /// центру за duration (unscaled), затем гасит рендереры и
    /// возвращает масштаб к единице, чтобы следующий забег начался
    /// с чистого листа.
    /// </summary>
    public static void FadeOut(float duration)
    {
        // Если батчера нет, значит в этой сцене кровь не спавнилась.
        if (instance == null)
            return;

        if (fading)
            return;

        if (!HasContent())
            return;

        instance.StartCoroutine(instance.FadeRoutine(duration));
    }

    /// <summary>
    /// Есть ли живые лужи для затухания. Если их нет, гонять
    /// корутину на всю длительность перелёта незачем - переход в
    /// меню ждал бы её зря.
    /// </summary>
    private static bool HasContent()
    {
        List<BloodPool> pools = BloodPool.ActivePools;

        for (int i = 0; i < pools.Count; i++)
        {
            if (pools[i] != null)
                return true;
        }

        return false;
    }

    private static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject host = new GameObject("BloodPoolBatcher");

        instance = host.AddComponent<BloodPoolBatcher>();
    }

    public static Material MainMaterial
    {
        get
        {
            if (mainMaterial == null)
                mainMaterial =
                    CreateMaterial(
                        new Color(0.52f, 0.03f, 0.04f),
                        "BloodMain"
                    );

            return mainMaterial;
        }
    }

    public static Material SplatterMaterial
    {
        get
        {
            if (splatterMaterial == null)
                splatterMaterial =
                    CreateMaterial(
                        new Color(0.44f, 0.025f, 0.035f),
                        "BloodSplatter"
                    );

            return splatterMaterial;
        }
    }

    private void Awake()
    {
        instance = this;

        // Сцена могла перезагрузиться посреди затухания: сбрасываем
        // общее состояние, иначе новый батчер унаследовал бы
        // незавершённый fade.
        dirty = false;
        fading = false;
        shrink = 1f;

        // Меш пишется в мировых координатах, поэтому батчер
        // обязан стоять в нуле с единичным масштабом.
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        CreateRenderers();
        Resize(InitialPoolCapacity);
    }

    private void OnDestroy()
    {
        if (mainMesh != null)
            Destroy(mainMesh);

        if (splatterMesh != null)
            Destroy(splatterMesh);

        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// LateUpdate: затухание помечает меш грязным из кадра в кадр.
    /// </summary>
    private void LateUpdate()
    {
        if (!dirty)
            return;

        dirty = false;
        Rebuild();
    }

    private void CreateRenderers()
    {
        mainRenderer = CreateRenderer(
            "BloodPoolMain",
            MainMaterial,
            out mainMesh
        );

        splatterRenderer = CreateRenderer(
            "BloodPoolSplatters",
            SplatterMaterial,
            out splatterMesh
        );

        mainRenderer.enabled = false;
        splatterRenderer.enabled = false;
    }

    private static MeshRenderer CreateRenderer(
        string name,
        Material material,
        out Mesh mesh)
    {
        GameObject child = new GameObject(name);

        // Батчер стоит в нуле, меш в мировых координатах, поэтому
        // дочерний объект можно оставить с единичным трансформом.
        child.transform.SetParent(instance.transform, false);

        mesh = new Mesh
        {
            name = name + "Mesh"
        };

        // Буферы переписываются при каждой пересборке: без
        // MarkDynamic Unity считает меш статическим и перезаливает его.
        mesh.MarkDynamic();

        MeshFilter filter = child.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer meshRenderer = child.AddComponent<MeshRenderer>();

        meshRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage =
            UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage =
            UnityEngine.Rendering.ReflectionProbeUsage.Off;
        meshRenderer.sharedMaterial = material;

        return meshRenderer;
    }

    private void Resize(int poolCapacity)
    {
        mainQuadCapacity = poolCapacity;
        splatterQuadCapacity = poolCapacity * BloodPool.MaxSplatters;

        mainVertices = new Vector3[mainQuadCapacity * VerticesPerQuad];

        splatterVertices =
            new Vector3[splatterQuadCapacity * VerticesPerQuad];

        // Вершины выставляем до индексов: Unity не принимает triangles,
        // пока в меше нет ни одной вершины ("indices referencing out of
        // bounds vertices"). ApplyMesh потом перезапишет их на rebuild.
        mainMesh.vertices = mainVertices;
        splatterMesh.vertices = splatterVertices;

        BuildTopology(mainMesh, mainQuadCapacity);
        BuildTopology(splatterMesh, splatterQuadCapacity);

        writtenMainQuads = 0;
        writtenSplatterQuads = 0;
    }

    /// <summary>
    /// Формат квада не зависит от содержимого, поэтому uv, нормали и
    /// индексы строятся один раз на всю ёмкость меша.
    /// </summary>
    private static void BuildTopology(Mesh mesh, int quadCount)
    {
        int vertexCount = quadCount * VerticesPerQuad;

        Vector2[] uvs = new Vector2[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        int[] triangles = new int[quadCount * 6];

        for (int i = 0; i < quadCount; i++)
        {
            int vertex = i * VerticesPerQuad;

            uvs[vertex + 0] = new Vector2(0f, 0f);
            uvs[vertex + 1] = new Vector2(1f, 0f);
            uvs[vertex + 2] = new Vector2(1f, 1f);
            uvs[vertex + 3] = new Vector2(0f, 1f);

            for (int n = 0; n < VerticesPerQuad; n++)
                normals[vertex + n] = Vector3.up;

            // Порядок обратный, чтобы нормаль треугольника смотрела
            // вверх (как в старом собственном меше лужи).
            int triangle = i * 6;

            triangles[triangle + 0] = vertex + 0;
            triangles[triangle + 1] = vertex + 2;
            triangles[triangle + 2] = vertex + 1;
            triangles[triangle + 3] = vertex + 0;
            triangles[triangle + 4] = vertex + 3;
            triangles[triangle + 5] = vertex + 2;
        }

        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = triangles;
    }

    private void Rebuild()
    {
        List<BloodPool> pools = BloodPool.ActivePools;

        int livePools = 0;

        for (int i = 0; i < pools.Count; i++)
        {
            if (pools[i] != null)
                livePools++;
        }

        if (livePools == 0)
        {
            HideRenderers();
            return;
        }

        EnsureCapacity(livePools);

        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        int mainCursor = 0;
        int mainLimit = mainVertices.Length;

        int splatterCursor = 0;
        int splatterLimit = splatterVertices.Length;

        for (int i = 0; i < pools.Count; i++)
        {
            BloodPool blood = pools[i];

            if (blood == null)
                continue;

            Vector3 origin = blood.transform.position;

            // Пятно: shrink сжимает квад к центру лужи при затухании.
            Vector3[] quad = blood.MainQuad;

            if (mainCursor + VerticesPerQuad <= mainLimit)
            {
                for (int v = 0; v < VerticesPerQuad; v++)
                {
                    Vector3 world = origin + quad[v] * shrink;

                    mainVertices[mainCursor] = world;
                    AddPoint(ref bounds, ref hasBounds, world);
                    mainCursor++;
                }
            }

            // Брызги: у лужи их ActiveSplatters.
            int splatters = blood.ActiveSplatters;
            Vector3[] splats = blood.SplatterQuads;

            for (int s = 0; s < splatters; s++)
            {
                if (splatterCursor + VerticesPerQuad > splatterLimit)
                    break;

                int source = s * VerticesPerQuad;

                for (int v = 0; v < VerticesPerQuad; v++)
                {
                    Vector3 world = origin + splats[source + v] * shrink;

                    splatterVertices[splatterCursor] = world;
                    AddPoint(ref bounds, ref hasBounds, world);
                    splatterCursor++;
                }
            }
        }

        // Затираем хвост: лужа, которая была видна в прошлом кадре и
        // не попала в этот. Индексный буфер покрывает весь меш, иначе
        // погасшая лужа осталась бы висеть на экране.
        WriteTail(mainVertices, mainCursor, writtenMainQuads);
        WriteTail(splatterVertices, splatterCursor, writtenSplatterQuads);

        writtenMainQuads = mainCursor / VerticesPerQuad;
        writtenSplatterQuads = splatterCursor / VerticesPerQuad;

        Bounds padded = new Bounds(
            bounds.center,
            bounds.extents + new Vector3(
                BoundsPadding,
                BoundsPadding,
                BoundsPadding
            )
        );

        ApplyMesh(mainMesh, mainRenderer, mainVertices, mainCursor, padded);
        ApplyMesh(
            splatterMesh,
            splatterRenderer,
            splatterVertices,
            splatterCursor,
            padded
        );
    }

    private static void WriteTail(
        Vector3[] vertices,
        int cursor,
        int writtenQuads)
    {
        int tail = writtenQuads * VerticesPerQuad;

        for (int i = cursor; i < tail; i++)
            vertices[i] = Vector3.zero;
    }

    private static void ApplyMesh(
        Mesh mesh,
        MeshRenderer renderer,
        Vector3[] vertices,
        int usedVertices,
        Bounds bounds)
    {
        // Границы задаём после вершин: присваивание vertices
        // пересчитывает бокс само.
        mesh.vertices = vertices;
        mesh.bounds = bounds;

        bool visible = usedVertices > 0;

        if (renderer.enabled != visible)
            renderer.enabled = visible;
    }

    private static void AddPoint(
        ref Bounds bounds,
        ref bool hasBounds,
        Vector3 point)
    {
        if (!hasBounds)
        {
            bounds = new Bounds(point, Vector3.zero);
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(point);
    }

    /// <summary>
    /// Меш только растёт: при усадке пересобирать uv и индексы незачем,
    /// а лишние вершины всё равно затираются нулями.
    /// </summary>
    private void EnsureCapacity(int requiredPools)
    {
        if (requiredPools <= mainQuadCapacity)
            return;

        int capacity = mainQuadCapacity;

        if (capacity < InitialPoolCapacity)
            capacity = InitialPoolCapacity;

        while (capacity < requiredPools)
            capacity *= 2;

        if (capacity > MaxPoolCapacity)
            capacity = MaxPoolCapacity;

        if (capacity == mainQuadCapacity)
            return;

        Resize(capacity);
    }

    private void HideRenderers()
    {
        if (mainRenderer.enabled)
            mainRenderer.enabled = false;

        if (splatterRenderer.enabled)
            splatterRenderer.enabled = false;
    }

    private IEnumerator FadeRoutine(float duration)
    {
        fading = true;

        if (duration <= 0.001f)
            duration = 0.001f;

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(timer / duration)
            );

            shrink = 1f - t;
            dirty = true;

            yield return null;
        }

        // Последний кадр: лужи уже стянуты в точки, можно гасить.
        shrink = 0f;
        dirty = true;

        HideRenderers();

        writtenMainQuads = 0;
        writtenSplatterQuads = 0;

        // Следующий забег начинается без затухания.
        shrink = 1f;
        dirty = false;
        fading = false;
    }

    private static Material CreateMaterial(
        Color color,
        string name)
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            return null;

        Material created = new Material(shader)
        {
            name = name
        };

        created.color = color;
        created.enableInstancing = true;

        return created;
    }
}
