using UnityEngine;

/// <summary>
/// Общий рендерер брызг крови: все активные BloodEffect складывают
/// свои капли в один динамический меш, который рисуется одним
/// MeshRenderer'ом. Вся система крови - 1 draw call.
///
/// До батчера каждое попадание держало собственный MeshRenderer,
/// то есть N попаданий = N draw call'ов. Лимит MaxBloodPerFrame = 6
/// в VfxFactory ограничивает только новые эффекты за кадр, а капля
/// живёт до 0.68 с, поэтому одновременно живо до ~245 эффектов,
/// и в плохом сценарии кровь стоила бы сотни draw call'ов.
///
/// Симуляция капель осталась в BloodEffect без изменений: он
/// считает капли в локальных координатах своего билборда и
/// складывает готовые квады в vertexBuffer/colorBuffer. Батчер
/// только переносит их в мир одним transform'ом и пишет в общий
/// меш, поэтому ни физика, ни внешний вид брызг не меняются.
///
/// Формат капли постоянен - 4 вершины, uv и индексы не зависят от
/// числа капель. Это позволяет держать индексный буфер на весь
/// объём меша и обновлять каждый кадр только вершины и цвета,
/// без пересборки топологии.
///
/// Меш растёт удвоением и никогда не сжимается. Неиспользуемый
/// хвост каждый кадр затирается нулями: индексный буфер на весь
/// объём, поэтому забытая капля осталась бы висеть на экране.
///
/// Порядок выполнения 300 - после VfxUpdater (200), который
/// двигает капли.
/// </summary>
[DefaultExecutionOrder(300)]
public sealed class BloodBatcher : MonoBehaviour
{
    /// <summary>
    /// Вершин на каплю: квад.
    /// </summary>
    private const int VerticesPerDroplet = 4;

    /// <summary>
    /// Стартовая ёмкость в каплях. 96 капель = 12 попаданий по
    /// умолчанию, хватает на спокойный бой без единого resize.
    /// </summary>
    private const int InitialDropletCapacity = 96;

    /// <summary>
    /// Потолок в каплях: 500 попаданий по 12 капель = 6000 капель
    /// = 24000 вершин. Дальше расти смысла нет, лишние капли
    /// просто не попадут в кадр.
    /// </summary>
    private const int MaxDropletCapacity = 6000;

    /// <summary>
    /// Запас к боксу меша. Капли приземляются, а binned mesh
    /// отсекает по границам - без запаса нижний край мог бы
    /// подрезаться на последнем кадре жизни.
    /// </summary>
    private const float BoundsPadding = 0.5f;

    private static readonly Color32 InvisibleColor =
        new Color32(0, 0, 0, 0);

    private static BloodBatcher instance;

    /// <summary>
    /// Активные эффекты в статическом массиве со swap-back
    /// удалением, как в VfxUpdater.
    /// </summary>
    private static BloodEffect[] effects = new BloodEffect[64];

    private static int count;

    private Vector3[] vertices;
    private Color32[] colors;

    /// <summary>
    /// Сколько капель помещается в текущий меш.
    /// </summary>
    private int dropletCapacity;

    /// <summary>
    /// Сколько капель было записано в прошлом кадре. Хвост между
    /// текущим и прошлым числом надо затереть: индексный буфер
    /// покрывает весь меш, иначе погасшие капли остались бы
    /// висеть.
    /// </summary>
    private int writtenDroplets;

    private Mesh mesh;
    private MeshRenderer meshRenderer;

    /// <summary>
    /// Сколько попаданий сейчас рисуется батчером.
    /// </summary>
    public static int ActiveEffectCount
    {
        get { return count; }
    }

    /// <summary>
    /// Сколько капель помещается в общий меш прямо сейчас.
    /// </summary>
    public static int DropletCapacity
    {
        get { return instance != null ? instance.dropletCapacity : 0; }
    }

    public static void Register(BloodEffect effect)
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

    public static void Unregister(BloodEffect effect)
    {
        if (effect == null)
            return;

        int index = effect.BatchIndex;

        if (index < 0)
            return;

        effect.BatchIndex = -1;
        effect.Batched = false;

        int last = count - 1;
        BloodEffect moved = effects[last];

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

        GameObject batcherObject = new GameObject("BloodBatcher");
        instance = batcherObject.AddComponent<BloodBatcher>();
        Object.DontDestroyOnLoad(batcherObject);
    }

    private static void GrowEffects(int capacity)
    {
        BloodEffect[] grown = new BloodEffect[capacity];

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

        Grow(InitialDropletCapacity);

        meshRenderer = host.AddComponent<MeshRenderer>();

        VfxSharedAssets.SetupRenderer(
            meshRenderer,
            mesh,
            VfxSharedAssets.BloodMaterial
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
    /// LateUpdate, а не Update: капли двигает VfxUpdater в Update.
    /// </summary>
    private void LateUpdate()
    {
        if (count == 0)
        {
            if (meshRenderer.enabled)
                meshRenderer.enabled = false;

            return;
        }

        // Два прохода: сначала считаем живые капли, чтобы при
        // необходимости расширить меш, и только потом пишем в его
        // буферы. Расширение перевыделяет vertices/colors, поэтому
        // наполнять их до grow бессмысленно - часть счёта потерялась
        // бы. Второй проход копеечный: эффектов до ~250, капель до
        // 12 на каждом, и обе цифры не растут вместе с числом
        // попаданий на экране.
        int live = CountLiveDroplets();

        if (live == 0)
        {
            if (meshRenderer.enabled)
                meshRenderer.enabled = false;

            return;
        }

        if (live > dropletCapacity)
            Grow(live);

        int vertexCapacity = vertices.Length;
        int cursor = 0;

        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < count; i++)
        {
            BloodEffect effect = effects[i];

            if (effect == null || !effect.Batched || !effect.IsPlaying)
                continue;

            // Единственное преобразование на каплю: квад из
            // локальных координат билборда в мир.
            Matrix4x4 matrix = effect.transform.localToWorldMatrix;

            Vector3[] localVertices = effect.LocalVertices;
            Color32[] localColors = effect.LocalColors;

            for (int droplet = 0; droplet < BloodEffect.MaxDroplets; droplet++)
            {
                int source = droplet * VerticesPerDroplet;

                // Погасшая капля уже помечена нулевой альфой в
                // BloodEffect, в общий меш её тащить незачем.
                // У живой капли на последнем кадре жизни альфа
                // тоже уходит в ноль - она уже не видна.
                if (localColors[source].a == 0)
                    continue;

                if (cursor + VerticesPerDroplet > vertexCapacity)
                    break;

                for (int corner = 0; corner < VerticesPerDroplet; corner++)
                {
                    Vector3 world = matrix.MultiplyPoint3x4(
                        localVertices[source + corner]
                    );

                    vertices[cursor] = world;
                    colors[cursor] = localColors[source + corner];

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
        }

        if (!hasBounds)
        {
            if (meshRenderer.enabled)
                meshRenderer.enabled = false;

            return;
        }

        // Затираем хвост: капли, которые были видны в прошлом
        // кадре и не попали в этот. Индексный буфер покрывает весь
        // меш, поэтому без этого погасшие капли остались бы
        // висеть на экране. Всё, что дальше writtenDroplets,
        // никогда не записывалось и в буфере уже нули.
        int tail = writtenDroplets * VerticesPerDroplet;

        for (int i = cursor; i < tail; i++)
        {
            vertices[i] = Vector3.zero;
            colors[i] = InvisibleColor;
        }

        writtenDroplets = cursor / VerticesPerDroplet;

        mesh.vertices = vertices;
        mesh.colors32 = colors;

        // Границы задаём после вершин: присваивание vertices
        // пересчитывает бокс сам, а капли разлетаются на 2-3
        // единицы от точки попадания по всей арене.
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
    /// Сколько капель реально видно в этом кадре. Погасшая капля
    /// уже помечена нулевой альфой в BloodEffect, её можно не
    /// считать: у живой капли на последнем кадре жизни альфа тоже
    /// уходит в ноль, и она уже не видна.
    /// </summary>
    private int CountLiveDroplets()
    {
        int live = 0;

        for (int i = 0; i < count; i++)
        {
            BloodEffect effect = effects[i];

            if (effect == null || !effect.Batched || !effect.IsPlaying)
                continue;

            Color32[] localColors = effect.LocalColors;

            for (int droplet = 0; droplet < BloodEffect.MaxDroplets; droplet++)
            {
                if (localColors[droplet * VerticesPerDroplet].a != 0)
                    live++;
            }
        }

        return live;
    }

    /// <summary>
    /// Выделение меша под нужное число капель. Меш только растёт:
    /// при усадке пересобирать uv и индексы незачем, а сэкономленные
    /// капли всё равно затираются нулями.
    /// </summary>
    private void Grow(int requiredDroplets)
    {
        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = "VfxBloodBatchedDroplets",
                hideFlags = HideFlags.HideAndDontSave
            };

            // Буферы перезаписываются каждый кадр: без MarkDynamic
            // Unity считает меш статическим и перезаливает его.
            mesh.MarkDynamic();
        }

        int capacity = dropletCapacity;

        if (capacity < InitialDropletCapacity)
            capacity = InitialDropletCapacity;

        while (capacity < requiredDroplets)
            capacity *= 2;

        if (capacity > MaxDropletCapacity)
            capacity = MaxDropletCapacity;

        if (capacity == dropletCapacity)
            return;

        dropletCapacity = capacity;

        int vertexCount = capacity * VerticesPerDroplet;

        vertices = new Vector3[vertexCount];
        colors = new Color32[vertexCount];

        writtenDroplets = 0;

        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[capacity * 6];

        for (int i = 0; i < capacity; i++)
        {
            int vertex = i * VerticesPerDroplet;

            uvs[vertex + 0] = new Vector2(0f, 0f);
            uvs[vertex + 1] = new Vector2(0f, 1f);
            uvs[vertex + 2] = new Vector2(1f, 0f);
            uvs[vertex + 3] = new Vector2(1f, 1f);

            int triangle = i * 6;

            triangles[triangle + 0] = vertex + 0;
            triangles[triangle + 1] = vertex + 2;
            triangles[triangle + 2] = vertex + 1;
            triangles[triangle + 3] = vertex + 2;
            triangles[triangle + 4] = vertex + 3;
            triangles[triangle + 5] = vertex + 1;
        }

        // Вершины нужны до uv и индексов: Unity не принимает triangles,
        // пока в меше нет ни одной вершины ("indices referencing out of
        // bounds vertices"), и меш остаётся без треугольников. LateUpdate
        // перезаписывает вершины каждый кадр, но именно этот массив
        // задаёт длину буфера, по которой валидируются индексы.
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
    }
}
