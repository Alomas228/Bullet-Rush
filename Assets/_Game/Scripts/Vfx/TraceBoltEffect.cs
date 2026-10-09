using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Процедурная молния / линия рикошета.
///
/// Своего рендерера у эффекта нет вообще. Все болты всех
/// срабатываний собирает TraceBoltBatcher в один общий меш и
/// рисует одним MeshRenderer'ом, то есть все молнии кадра -
/// 1 draw call. Раньше каждая молния держала собственный меш и
/// MeshRenderer, а цвет задавался материалом из кэша (на практике
/// их два: молния и рикошет) - N болтов = N draw call'ов. Теперь
/// цвет болта приходит через vertex color, а один материал
/// обслуживает и молнию, и рикошет.
///
/// Геометрия осталась прежней: лента из двух вершин на точку с
/// тем же срезом ширины и билбордингом к камере, что у
/// LineRenderer, плюс квад искры на конце. Обе части лежат в одном
/// буфере, поэтому дают один draw call, а не два.
///
/// Затухание идёт через ширину ленты, а не через альфу: это
/// свойство геометрии, а не цвета, поэтому болт утончается и
/// исчезает, оставаясь тем же оттенком.
///
/// Объект остаётся в нуле с единичным масштабом, а вершины
/// пишутся сразу в мировых координатах - батчеру остаётся только
/// скопировать буферы. Буферы выделяются один раз в Awake, в
/// рантайме аллокаций нет.
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
    internal const int MaxPoints = 65;

    /// <summary>
    /// Вершин на болт: по две на точку ленты плюс квад искры.
    /// </summary>
    internal const int VerticesPerBolt = MaxPoints * 2 + 4;

    /// <summary>
    /// Индексов на болт: по два треугольника на каждый отрезок
    /// ленты плюс два на квад искры.
    /// </summary>
    internal const int IndicesPerBolt = (MaxPoints - 1) * 6 + 6;

    private static readonly Stack<TraceBoltEffect> pool =
        new Stack<TraceBoltEffect>(PoolSize);

    // Осевая линия ленты: по две вершины на точку.
    private Vector3[] centerPoints;
    private Vector3[] pointPerp;
    private float[] pointHalfWidth;

    // Искра - квад, добавленный в тот же буфер.
    private Vector3 sparkPosition;
    private float sparkScale;
    private bool sparkVisible;

    // Позиции болта в мировых координатах; их забирает батчер.
    private Vector3[] vertexBuffer;

    // Цвет болта на каждую вершину; задаётся один раз при
    // инициализации - затухание идёт по ширине, не по альфе.
    private Color32[] colorBuffer;

    private float targetSparkScale = 1f;
    private float elapsed;
    private int pointCount;
    private bool initialized;

    /// <summary>
    /// Эффект рисуется общим мешем TraceBoltBatcher, а не своим
    /// MeshRenderer'ом.
    /// </summary>
    internal bool Batched;

    /// <summary>
    /// Позиция в статическом массиве TraceBoltBatcher: удаление
    /// за O(1) вместо поиска по массиву.
    /// </summary>
    internal int BatchIndex = -1;

    internal Vector3[] LocalVertices
    {
        get { return vertexBuffer; }
    }

    internal Color32[] LocalColors
    {
        get { return colorBuffer; }
    }

    internal bool IsActive
    {
        get { return initialized; }
    }

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

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pool.Clear();
    }

    private void Awake()
    {
        AllocateState();
    }

    private void OnDestroy()
    {
        TraceBoltBatcher.Unregister(this);
    }

    private void AllocateState()
    {
        centerPoints = new Vector3[MaxPoints];
        pointPerp = new Vector3[MaxPoints];
        pointHalfWidth = new float[MaxPoints];

        vertexBuffer = new Vector3[VerticesPerBolt];
        colorBuffer = new Color32[VerticesPerBolt];
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

        Color32 tint = color;

        for (int i = 0; i < VerticesPerBolt; i++)
            colorBuffer[i] = tint;

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

        TraceBoltBatcher.Register(this);
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

        TraceBoltBatcher.Unregister(this);

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

    // Затухание идёт по ширине ленты: величина живёт в вершинах
    // меша, цвет и батч не трогаются.
    private void ApplyFade(float alpha)
    {
        if (pointCount <= 0)
            return;

        float width = Mathf.Max(0f, alpha);

        for (int i = 0; i < pointCount; i++)
        {
            int vertex = i * 2;

            if (width <= 0f)
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

        // Хвост схлопываем на последнюю живую пару, а не в ноль:
        // иначе треугольник между концом ленты и началом
        // координат вытянулся бы в длинную видимую полосу.
        // Одинаковые вершины дают вырожденные треугольники,
        // которые не растеризуются.
        Vector3 tailA = vertexBuffer[(pointCount - 1) * 2];
        Vector3 tailB = vertexBuffer[(pointCount - 1) * 2 + 1];

        for (int i = pointCount; i < MaxPoints; i++)
        {
            int vertex = i * 2;

            vertexBuffer[vertex + 0] = tailA;
            vertexBuffer[vertex + 1] = tailB;
        }

        WriteSpark();
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
