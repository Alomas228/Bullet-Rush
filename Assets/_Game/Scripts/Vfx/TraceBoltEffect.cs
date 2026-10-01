using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Procedural lightning / ricochet line effect.
///
/// Раньше цвет и затухание писались через MaterialPropertyBlock
/// каждый кадр жизни эффекта. Это не бесплатная оптимизация:
/// любой PropertyBlock выводит рендерер из SRP Batcher, поэтому
/// весь этот VFX превращался в отдельные draw call.
///
/// Теперь цвет задаётся материалом из кэша (на практике их два:
/// молния и рикошет), а затухание идёт через ширину ленты и
/// размер искры — это свойства геометрии, не материалов, батч
/// остаётся целым. Заодно эффект берётся из пула, а не создаётся
/// Instantiate и не уничтожается Destroy на каждом срабатывании.
///
/// Почему лента и искра в одном рендерере, а не два объекта.
/// LineRenderer - это отдельный рендерер, и сфера-искра была
/// вторым: одна молния = 2 draw call. Оба они на одном материале,
/// но SRP Batcher не складывает рендереры в один вызов, а
/// инстансинг здесь не применим - у ленты и у сферы разные меши.
/// Единственный способ получить 1 draw call - построить обе
/// части в один меш вручную, что и делает этот класс.
///
/// Про внешний вид: лента рисуется теми же четырьмя вершинами на
/// точку с тем же срезом ширины и тем же билбордингом к камере,
/// что и LineRenderer, а сфера заменена на квад того же
/// размера. Под URP/Unlit сфера без освещения видна ровно своим
/// силуэтом - кругом, - поэтому на неаддитивном материале квад
/// выглядит так же. Отличие одно и незаметное: у LineRenderer с
/// numCapVertices = 2 торцы чуть скруглены, здесь они плоские
/// (разница в пару пикселей на ширине 0.12 единицы).
///
/// Меш перезаписывается каждый кадр жизни эффекта - меняется
/// только ширина ленты и размер искры, - поэтому он динамический.
/// Буферы выделяются один раз в Awake, в рантайме аллокаций нет.
/// </summary>
public class TraceBoltEffect : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private float lifetime = 0.25f;
    [SerializeField] private float startWidth = 0.12f;
    [SerializeField] private float endWidth = 0.05f;
    [SerializeField] private int segments = 14;
    [SerializeField] private float sparkRadius = 0.22f;

    private const int PoolSize = 12;

    // Потолок точек ленты. 64 сегмента за глаза хватает, поле
    // segments сериализовано, поэтому нужен запас и клампинг.
    private const int MaxPoints = 65;

    private static Material sharedMaterial;
    private static readonly Stack<TraceBoltEffect> pool =
        new Stack<TraceBoltEffect>(PoolSize);

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId =
        Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    // Кэш «цвет → материал». Ключ квантуется по 1/64, чтобы близкие
    // оттенки не плодили почти одинаковые копии.
    private static readonly Dictionary<int, Material> materialCache =
        new Dictionary<int, Material>(8);

    private static bool materialHasBaseColor;
    private static bool materialHasColor;
    private static bool materialHasEmission;

    // Осевая линия ленты: по две вершины на точку.
    private Vector3[] centerPoints;
    private Vector3[] pointPerp;
    private float[] pointHalfWidth;

    // Искра - квад, добавленный в тот же меш.
    private Vector3 sparkPosition;
    private float sparkScale;
    private bool sparkVisible;

    private Vector3[] vertexBuffer;
    private Mesh mesh;
    private MeshRenderer meshRenderer;

    private float targetSparkScale = 1f;
    private float elapsed;
    private int pointCount;
    private bool initialized;

    public static TraceBoltEffect Spawn(
        Vector3 from,
        Vector3 to,
        Color color,
        bool zigZag,
        bool endSpark)
    {
        TraceBoltEffect effect;

        if (pool.Count > 0)
            effect = pool.Pop();
        else
            effect = CreateInstance();

        if (effect == null)
            return null;

        effect.gameObject.SetActive(true);
        effect.Initialize(from, to, color, zigZag, endSpark);

        return effect;
    }

    private static TraceBoltEffect CreateInstance()
    {
        GameObject go = new GameObject("TraceBolt");

        return go.AddComponent<TraceBoltEffect>();
    }

    private void Awake()
    {
        AllocateState();
        BuildMesh();
    }

    private void OnDestroy()
    {
        // Меш создан в рантайме, а HideAndDontSave не спасает его от
        // утечки: Unity не знает про него и не уберёт сам.
        if (mesh != null)
            Destroy(mesh);
    }

    private void AllocateState()
    {
        centerPoints = new Vector3[MaxPoints];
        pointPerp = new Vector3[MaxPoints];
        pointHalfWidth = new float[MaxPoints];

        // Две вершины на каждую точку ленты плюс квад искры.
        vertexBuffer = new Vector3[MaxPoints * 2 + 4];
    }

    private void BuildMesh()
    {
        mesh = new Mesh
        {
            name = "VfxTraceBolt",
            hideFlags = HideFlags.HideAndDontSave
        };

        mesh.MarkDynamic();

        Vector2[] uvs = new Vector2[vertexBuffer.Length];
        int[] triangles = new int[(MaxPoints - 1) * 6 + 6];

        for (int i = 0; i < MaxPoints; i++)
        {
            int vertex = i * 2;

            uvs[vertex + 0] = new Vector2(0f, 0f);
            uvs[vertex + 1] = new Vector2(0f, 1f);

            if (i < MaxPoints - 1)
            {
                int triangle = i * 6;

                triangles[triangle + 0] = vertex + 0;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 2;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 1;
            }
        }

        // Квад искры идёт последними четырьмя вершинами.
        int sparkVertex = MaxPoints * 2;

        uvs[sparkVertex + 0] = new Vector2(0f, 0f);
        uvs[sparkVertex + 1] = new Vector2(0f, 1f);
        uvs[sparkVertex + 2] = new Vector2(1f, 0f);
        uvs[sparkVertex + 3] = new Vector2(1f, 1f);

        int sparkTriangle = (MaxPoints - 1) * 6;

        triangles[sparkTriangle + 0] = sparkVertex + 0;
        triangles[sparkTriangle + 1] = sparkVertex + 2;
        triangles[sparkTriangle + 2] = sparkVertex + 1;
        triangles[sparkTriangle + 3] = sparkVertex + 2;
        triangles[sparkTriangle + 4] = sparkVertex + 3;
        triangles[sparkTriangle + 5] = sparkVertex + 1;

        mesh.vertices = vertexBuffer;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        // Лента живёт в мировых координатах (объект остаётся в
        // нуле с единичным масштабом), а разлетается она дальше
        // размера бокса по умолчанию - поэтому бокс задаётся
        // руками, иначе эффект вылетел бы из frustum culling.
        mesh.bounds = new Bounds(
            Vector3.zero,
            new Vector3(200f, 200f, 200f)
        );

        meshRenderer = gameObject.GetComponent<MeshRenderer>();

        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        VfxSharedAssets.SetupRenderer(
            meshRenderer,
            mesh,
            GetSharedMaterial()
        );
    }

    public void Initialize(
        Vector3 from,
        Vector3 to,
        Color color,
        bool zigZag,
        bool endSpark)
    {
        elapsed = 0f;
        initialized = true;

        BuildBolt(from, to, zigZag);

        if (meshRenderer != null)
            meshRenderer.sharedMaterial = GetMaterialForColor(color);

        if (endSpark)
        {
            sparkVisible = true;
            sparkPosition = to;
            sparkScale = sparkRadius * 0.4f;
            targetSparkScale = sparkRadius * 2f;
        }
        else
        {
            sparkVisible = false;
        }

        ApplyFade(1f);
    }

    private void Update()
    {
        if (!initialized)
            return;

        elapsed += Time.deltaTime;

        float progress = Mathf.Clamp01(
            elapsed / Mathf.Max(lifetime, 0.01f)
        );

        ApplyFade(1f - progress);
        UpdateSpark(progress);

        if (progress >= 1f)
            Release();
    }

    private void Release()
    {
        initialized = false;

        if (pool.Count < PoolSize)
        {
            pool.Push(this);
            gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void BuildBolt(
        Vector3 from,
        Vector3 to,
        bool zigZag)
    {
        pointCount =
            Mathf.Clamp(segments, 1, MaxPoints - 1) + 1;

        Vector3 direction = to - from;
        float distance = direction.magnitude;

        if (distance < 0.001f)
        {
            direction = Vector3.forward;
            distance = 1f;
        }

        Vector3 axis = direction / distance;
        Vector3 perp = Vector3.Cross(axis, Vector3.up);

        if (perp.sqrMagnitude < 0.0001f)
            perp = Vector3.Cross(axis, Vector3.right);

        perp.Normalize();

        int innerCount = pointCount - 1;

        for (int i = 0; i < pointCount; i++)
        {
            float t = innerCount > 0 ? i / (float)innerCount : 0f;
            Vector3 point = Vector3.Lerp(from, to, t);

            if (zigZag && i != 0 && i != pointCount - 1)
            {
                float jitter = distance * 0.1f * Random.Range(0.6f, 1.4f);
                point += perp * Random.Range(-jitter, jitter);
                point += Vector3.up * Random.Range(-jitter * 0.4f, jitter * 0.4f);
            }

            centerPoints[i] = point;

            // Срез ширины как у LineRenderer: от startWidth на
            // первом отрезке к endWidth на последнем.
            pointHalfWidth[i] =
                Mathf.Lerp(startWidth, endWidth, t) * 0.5f;
        }

        BuildPerps();

        if (sparkVisible)
            sparkPosition = centerPoints[pointCount - 1];
    }

    /// <summary>
    /// Направление ширины в каждой точке. Считается по касательной
    /// ленты и нормали билборда, ровно как это делает LineRenderer:
    /// иначе лента станет ребром к камере и пропадёт, когда пуля
    /// летит вверх экрана.
    /// </summary>
    private void BuildPerps()
    {
        Vector3 normal =
            VfxSharedAssets.FaceDirection(
                centerPoints[pointCount - 1] -
                centerPoints[0]
            ) * Vector3.forward;

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 tangent;

            if (i == 0)
                tangent = centerPoints[1] - centerPoints[0];
            else if (i == pointCount - 1)
                tangent =
                    centerPoints[pointCount - 1] -
                    centerPoints[pointCount - 2];
            else
                tangent = centerPoints[i + 1] - centerPoints[i - 1];

            Vector3 side =
                Vector3.Cross(tangent, normal);

            if (side.sqrMagnitude < 0.000001f)
                side = Vector3.Cross(tangent, Vector3.up);

            if (side.sqrMagnitude < 0.000001f)
                side = Vector3.right;

            pointPerp[i] = side.normalized;
        }
    }

    private static Material GetSharedMaterial()
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        sharedMaterial = new Material(shader)
        {
            name = "TraceBoltSharedMat"
        };

        sharedMaterial.SetOverrideTag("RenderType", "Transparent");

        if (sharedMaterial.HasProperty("_Surface"))
            sharedMaterial.SetFloat("_Surface", 1f);
        if (sharedMaterial.HasProperty("_Blend"))
            sharedMaterial.SetFloat("_Blend", 0f);
        if (sharedMaterial.HasProperty("_SrcBlend"))
            sharedMaterial.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (sharedMaterial.HasProperty("_DstBlend"))
            sharedMaterial.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (sharedMaterial.HasProperty("_ZWrite"))
            sharedMaterial.SetFloat("_ZWrite", 0f);

        sharedMaterial.renderQueue = (int)RenderQueue.Transparent;
        materialHasBaseColor = sharedMaterial.HasProperty("_BaseColor");
        materialHasColor = sharedMaterial.HasProperty("_Color");
        materialHasEmission = sharedMaterial.HasProperty("_EmissionColor");

        if (materialHasEmission)
            sharedMaterial.EnableKeyword("_EMISSION");

        sharedMaterial.enableInstancing = true;

        return sharedMaterial;
    }

    private static Material GetMaterialForColor(Color color)
    {
        Material baseMaterial = GetSharedMaterial();

        if (baseMaterial == null)
            return null;

        int key = QuantizeKey(color);

        if (materialCache.TryGetValue(key, out Material cached) &&
            cached != null)
        {
            return cached;
        }

        Color emission = new Color(
            color.r * 2f,
            color.g * 2f,
            color.b * 2f,
            1f
        );

        Material tinted = new Material(baseMaterial)
        {
            name = baseMaterial.name + " " + key
        };

        if (materialHasBaseColor)
            tinted.SetColor(BaseColorId, color);

        if (materialHasColor)
            tinted.SetColor(ColorId, color);

        if (materialHasEmission)
            tinted.SetColor(EmissionColorId, emission);

        tinted.enableInstancing = true;

        materialCache[key] = tinted;

        return tinted;
    }

    private static int QuantizeKey(Color color)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(color.r * 64f), 0, 64);
        int g = Mathf.Clamp(Mathf.RoundToInt(color.g * 64f), 0, 64);
        int b = Mathf.Clamp(Mathf.RoundToInt(color.b * 64f), 0, 64);

        return (r * 64 + g) * 64 + b;
    }

    // Затухание идёт по ширине ленты и размеру искры: обе величины
    // живут в вершинах меша, материал и батч не трогаются.
    private void ApplyFade(float alpha)
    {
        if (mesh == null || pointCount <= 0)
            return;

        float width = Mathf.Max(0f, alpha);

        for (int i = 0; i < pointCount; i++)
        {
            int vertex = i * 2;

            // Точки за пределами pointCount держат прошлую молнию,
            // их надо затереть, иначе она останется висеть.
            if (i >= pointCount || width <= 0f)
            {
                vertexBuffer[vertex + 0] = Vector3.zero;
                vertexBuffer[vertex + 1] = Vector3.zero;
                continue;
            }

            Vector3 side =
                pointPerp[i] * (pointHalfWidth[i] * width);

            vertexBuffer[vertex + 0] =
                centerPoints[i] - side;
            vertexBuffer[vertex + 1] =
                centerPoints[i] + side;
        }

        for (int i = pointCount; i < MaxPoints; i++)
        {
            int vertex = i * 2;

            vertexBuffer[vertex + 0] = Vector3.zero;
            vertexBuffer[vertex + 1] = Vector3.zero;
        }

        WriteSpark();

        mesh.vertices = vertexBuffer;
    }

    private void UpdateSpark(float progress)
    {
        if (!sparkVisible)
            return;

        float eased = 1f - Mathf.Pow(1f - progress, 2f);

        sparkScale = Mathf.Lerp(
            sparkRadius * 0.4f,
            targetSparkScale,
            eased
        );
    }

    private void WriteSpark()
    {
        int vertex = MaxPoints * 2;

        if (!sparkVisible || sparkScale <= 0.0001f)
        {
            vertexBuffer[vertex + 0] = Vector3.zero;
            vertexBuffer[vertex + 1] = Vector3.zero;
            vertexBuffer[vertex + 2] = Vector3.zero;
            vertexBuffer[vertex + 3] = Vector3.zero;
            return;
        }

        Quaternion face = VfxSharedAssets.FaceDirection(
            Vector3.forward
        );

        Vector3 right = face * Vector3.right * (sparkScale * 0.5f);
        Vector3 up = face * Vector3.up * (sparkScale * 0.5f);

        vertexBuffer[vertex + 0] = sparkPosition - right - up;
        vertexBuffer[vertex + 1] = sparkPosition - right + up;
        vertexBuffer[vertex + 2] = sparkPosition + right - up;
        vertexBuffer[vertex + 3] = sparkPosition + right + up;
    }
}