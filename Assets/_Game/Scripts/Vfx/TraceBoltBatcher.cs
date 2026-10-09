using UnityEngine;

/// <summary>
/// Общий рендерер молний и рикошетов: все активные TraceBoltEffect
/// складывают свои болты в один динамический меш, который
/// рисуется одним MeshRenderer'ом. Все молнии кадра - 1 draw call.
///
/// До батчера каждая молния держала собственный MeshRenderer и
/// свой меш, то есть цепь по N целей = N draw call'ов. Пул
/// ограничивал только аллокации (12 объектов), не draw call'ы:
/// при залпе дроби по толпе молнии стоили бы десятков вызовов.
///
/// Симуляция осталась в TraceBoltEffect без изменений: он
/// считает зигзаг в мировых координатах и складывает готовые
/// вершины ленты и искры в vertexBuffer, а цвет болта - в
/// colorBuffer. Батчер просто переносит их в общий меш.
///
/// Формат болта постоянен - MaxPoints * 2 вершин ленты плюс
/// квад искры, неиспользуемые точки ленты схлопнуты на последнюю
/// живую пару (вырожденные треугольники не растеризуются).
/// Это позволяет держать индексный буфер на весь объём меша
/// и обновлять каждый кадр только вершины и цвета.
///
/// Меш растёт удвоением и никогда не сжимается. Неиспользуемый
/// хвост каждый кадр затирается нулями: индексный буфер на весь
/// объём, поэтому забытый болт остался бы висеть на экране.
///
/// Порядок выполнения 300 - после Update болтов, которые
/// пересчитывают ширину ленты на каждом кадре жизни.
/// </summary>
[DefaultExecutionOrder(300)]
public sealed class TraceBoltBatcher : MonoBehaviour
{
    /// <summary>
    /// Стартовая ёмкость в болтах: равна размеру пула болтов,
    /// хватает на спокойный бой без единого resize.
    /// </summary>
    private const int InitialBoltCapacity = 12;

    /// <summary>
    /// Потолок в болтах: 48 молний по 134 вершины = 6432 вершины.
    /// Дальше расти смысла нет, лишние болты просто не попадут
    /// в кадр.
    /// </summary>
    private const int MaxBoltCapacity = 48;

    /// <summary>
    /// Запас к боксу меша: лента и искра живут ближе к камере,
    /// чем точки попадания, - без запаса края могли бы
    /// подрезаться на последнем кадре жизни.
    /// </summary>
    private const float BoundsPadding = 0.5f;

    private static readonly Color32 InvisibleColor =
        new Color32(0, 0, 0, 0);

    private static TraceBoltBatcher instance;

    /// <summary>
    /// Активные болты в статическом массиве со swap-back
    /// удалением, как в VfxUpdater и BloodBatcher.
    /// </summary>
    private static TraceBoltEffect[] effects =
        new TraceBoltEffect[32];

    private static int count;

    private Vector3[] vertices;
    private Color32[] colors;

    /// <summary>
    /// Сколько болтов помещается в текущий меш.
    /// </summary>
    private int boltCapacity;

    /// <summary>
    /// Сколько болтов было записано в прошлом кадре. Хвост между
    /// текущим и прошлым числом надо затереть: индексный буфер
    /// покрывает весь меш, иначе погасшие болты остались бы
    /// висеть.
    /// </summary>
    private int writtenBolts;

    private Mesh mesh;
    private MeshRenderer meshRenderer;

    public static void Register(TraceBoltEffect effect)
    {
        if (effect == null || effect.Batched)
            return;

        EnsureExists();

        if (count == effects.Length)
            GrowEffects(count * 2);

        effect.Batched = true;
        effect.BatchIndex = count;

        effects[count] = effect;
        count++;
    }

    public static void Unregister(TraceBoltEffect effect)
    {
        if (effect == null)
            return;

        int index = effect.BatchIndex;

        if (index < 0)
            return;

        effect.BatchIndex = -1;
        effect.Batched = false;

        int last = count - 1;
        TraceBoltEffect moved = effects[last];

        effects[last] = null;
        count = last;

        if (index == last)
            return;

        effects[index] = moved;

        if (moved != null)
            moved.BatchIndex = index;
    }

    private static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject batcherObject =
            new GameObject("TraceBoltBatcher");

        instance = batcherObject.AddComponent<TraceBoltBatcher>();
        Object.DontDestroyOnLoad(batcherObject);
    }

    private static void GrowEffects(int capacity)
    {
        TraceBoltEffect[] grown = new TraceBoltEffect[capacity];

        for (int i = 0; i < count; i++)
            grown[i] = effects[i];

        effects = grown;
    }

    private void Awake()
    {
        instance = this;

        GameObject host = gameObject;

        // Меш пишется в мировых координатах, поэтому батчер
        // обязан стоять в нуле с единичным масштабом.
        host.transform.position = Vector3.zero;
        host.transform.rotation = Quaternion.identity;
        host.transform.localScale = Vector3.one;
        host.layer = 0;

        Grow(InitialBoltCapacity);

        meshRenderer = host.AddComponent<MeshRenderer>();

        VfxSharedAssets.SetupRenderer(
            meshRenderer,
            mesh,
            VfxSharedAssets.TraceBoltMaterial
        );

        meshRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);

        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// LateUpdate, а не Update: ширину ленты пересчитывает
    /// TraceBoltEffect в Update.
    /// </summary>
    private void LateUpdate()
    {
        if (count == 0)
        {
            if (meshRenderer.enabled)
                meshRenderer.enabled = false;

            return;
        }

        // Два прохода: сначала считаем живые болты, чтобы при
        // необходимости расширить меш, и только потом пишем в его
        // буферы. Расширение перевыделяет vertices/colors, поэтому
        // наполнять их до grow бессмысленно - часть счёта
        // потерялась бы. Второй проход копеечный: болтов единицы,
        // и вершины на каждый уже посчитаны эффектом.
        int live = CountLiveBolts();

        if (live == 0)
        {
            if (meshRenderer.enabled)
                meshRenderer.enabled = false;

            return;
        }

        if (live > boltCapacity)
            Grow(live);

        int vertexCapacity = vertices.Length;
        int cursor = 0;

        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < count; i++)
        {
            TraceBoltEffect effect = effects[i];

            if (effect == null || !effect.Batched ||
                !effect.IsActive)
            {
                continue;
            }

            if (cursor + TraceBoltEffect.VerticesPerBolt >
                vertexCapacity)
            {
                break;
            }

            // Единственная работа на болт: копия готовых буферов.
            // Лента уже в мировых координатах (объект эффекта
            // остаётся в нуле), преобразовывать нечего.
            Vector3[] sourceVertices = effect.LocalVertices;
            Color32[] sourceColors = effect.LocalColors;

            for (int v = 0; v < TraceBoltEffect.VerticesPerBolt; v++)
            {
                Vector3 world = sourceVertices[v];

                vertices[cursor] = world;
                colors[cursor] = sourceColors[v];

                if (!hasBounds)
                {
                    bounds = new Bounds(world, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(world);
                }

                cursor++;
            }
        }

        if (cursor == 0)
        {
            if (meshRenderer.enabled)
                meshRenderer.enabled = false;

            return;
        }

        // Затираем хвост: болты, которые были видны в прошлом
        // кадре и не попали в этот. Индексный буфер покрывает весь
        // меш, поэтому без этого погасшие болты остались бы
        // висеть на экране. Всё, что дальше writtenBolts, никогда
        // не записывалось и в буфере уже нули.
        int tail = writtenBolts * TraceBoltEffect.VerticesPerBolt;

        for (int i = cursor; i < tail; i++)
        {
            vertices[i] = Vector3.zero;
            colors[i] = InvisibleColor;
        }

        writtenBolts =
            cursor / TraceBoltEffect.VerticesPerBolt;

        mesh.vertices = vertices;
        mesh.colors32 = colors;

        // Границы задаём после вершин: присваивание vertices
        // пересчитывает бокс сам, а молнии - это длинные ленты
        // между двумя точками по всей арене.
        mesh.bounds = new Bounds(
            bounds.center,
            bounds.extents + new Vector3(
                BoundsPadding,
                BoundsPadding,
                BoundsPadding
            )
        );

        if (!meshRenderer.enabled)
            meshRenderer.enabled = true;
    }

    /// <summary>
    /// Сколько болтов реально видно в этом кадре.
    /// </summary>
    private int CountLiveBolts()
    {
        int live = 0;

        for (int i = 0; i < count; i++)
        {
            TraceBoltEffect effect = effects[i];

            if (effect == null || !effect.Batched ||
                !effect.IsActive)
            {
                continue;
            }

            live++;
        }

        return live;
    }

    /// <summary>
    /// Выделение меша под нужное число болтов. Меш только растёт:
    /// при усадке пересобирать индексы незачем, а экономия всё
    /// равно затирается нулями.
    /// </summary>
    private void Grow(int requiredBolts)
    {
        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = "VfxTraceBoltBatched",
                hideFlags = HideFlags.HideAndDontSave
            };

            // Буферы перезаписываются каждый кадр: без MarkDynamic
            // Unity считает меш статическим и перезаливает его.
            mesh.MarkDynamic();
        }

        int capacity = boltCapacity;

        if (capacity < InitialBoltCapacity)
            capacity = InitialBoltCapacity;

        while (capacity < requiredBolts)
            capacity *= 2;

        if (capacity > MaxBoltCapacity)
            capacity = MaxBoltCapacity;

        if (capacity == boltCapacity)
            return;

        boltCapacity = capacity;

        int vertexCount =
            capacity * TraceBoltEffect.VerticesPerBolt;

        vertices = new Vector3[vertexCount];
        colors = new Color32[vertexCount];

        writtenBolts = 0;

        // Индексы одинаковы для каждого слота болта: лента
        // MaxPoints * 2 вершин плюс квад искры из четырёх.
        int[] triangles =
            new int[capacity * TraceBoltEffect.IndicesPerBolt];

        int ribbonVertices = TraceBoltEffect.MaxPoints * 2;
        int ribbonIndices =
            (TraceBoltEffect.MaxPoints - 1) * 6;

        for (int slot = 0; slot < capacity; slot++)
        {
            int vertex = slot * TraceBoltEffect.VerticesPerBolt;
            int triangle = slot * TraceBoltEffect.IndicesPerBolt;

            for (int i = 0; i < TraceBoltEffect.MaxPoints - 1; i++)
            {
                int point = vertex + i * 2;
                int t = triangle + i * 6;

                triangles[t + 0] = point + 0;
                triangles[t + 1] = point + 2;
                triangles[t + 2] = point + 1;
                triangles[t + 3] = point + 2;
                triangles[t + 4] = point + 3;
                triangles[t + 5] = point + 1;
            }

            int spark = vertex + ribbonVertices;
            int sparkTriangle = triangle + ribbonIndices;

            triangles[sparkTriangle + 0] = spark + 0;
            triangles[sparkTriangle + 1] = spark + 2;
            triangles[sparkTriangle + 2] = spark + 1;
            triangles[sparkTriangle + 3] = spark + 2;
            triangles[sparkTriangle + 4] = spark + 3;
            triangles[sparkTriangle + 5] = spark + 1;
        }

        // Вершины задаём до индексов: у меша уже должен быть
        // достаточный размер вершинного буфера, иначе Unity
        // отклонит индексы, ссылающиеся за его пределы.
        mesh.vertices = vertices;
        mesh.colors32 = colors;
        mesh.triangles = triangles;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        count = 0;
        effects = new TraceBoltEffect[32];
    }
}
