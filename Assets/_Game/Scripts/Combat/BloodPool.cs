using System.Collections.Generic;
using UnityEngine;

// Лужа крови на земле.
//
// Раньше здесь на каждую смерть моба создавался новый GameObject, а
// на каждое пятно — ещё и CreatePrimitive(Quad) с последующим
// Destroy. На боссе это 11 MeshRenderer и 12 GameObject, которые
// живут 10 секунд и потом уничтожаются (см. Enemy.SpawnBloodPool).
//
// Теперь пятна не объекты, а вершины. Один лужа держит два
// MeshRenderer: центральное пятно и все брызги одним мешем, поэтому
// вместо 11 рендереров на смерть моба приходится 2, а вместо
// Instantiate/Destroy — перезапись вершин уже существующего меша.
// Сам объект лужи берётся из пула и не уничтожается.
//
// Материалы два на весь забег: BloodPool.MainMaterial на центральное
// пятно и BloodPool.SplatterMaterial на брызги. Цвет задаётся в
// материале, а не в PropertyBlock, иначе лужи выпадали бы из
// SRP Batcher — а их на поле может быть десяток за раз.
public sealed class BloodPool : MonoBehaviour
{
    [Header("Main Pool")]
    [SerializeField] private float mainPoolSize = 1.2f;
    [SerializeField, Range(0f, 1f)] private float mainSizeRandomness = 0.2f;

    [Header("Splatters")]
    [SerializeField] private int splatterCount = 4;
    [SerializeField] private float minSplatterSize = 0.12f;
    [SerializeField] private float maxSplatterSize = 0.38f;
    [SerializeField] private float maxSplatterOffset = 1.1f;

    [Header("Lifetime")]
    [SerializeField] private float lifetime = 10f;

    // Больше 10 брызг не запрашивает ни один тип моба, но запас
    // оставлен на случай добавления нового.
    private const int MaxSplatters = 12;

    // Больше 16 луж одновременно на карте не лежит: каждая живёт
    // lifetime секунд, а мобы за это время успевают закончиться.
    private const int MaxPooled = 16;

    private const float GroundY = 0.006f;

    private static Material mainMaterial;
    private static Material splatterMaterial;

    private static readonly Queue<BloodPool> pool =
        new Queue<BloodPool>(MaxPooled);

    // Все живые лужи в одном списке: их обслуживает один Update,
    // а не по MonoBehaviour на каждое пятно крови.
    private static readonly List<BloodPool> active =
        new List<BloodPool>(32);

    private static BloodPoolTicker ticker;

    private MeshFilter mainFilter;
    private MeshRenderer mainRenderer;
    private MeshFilter splatterFilter;
    private MeshRenderer splatterRenderer;

    private Vector3[] mainVertices;
    private Vector3[] mainNormals;
    private Vector2[] mainUvs;
    private int[] mainTriangles;

    private Vector3[] splatterVertices;
    private Vector3[] splatterNormals;
    private Vector2[] splatterUvs;
    private int[] splatterTriangles;

    private float remaining;

    /// <summary>
    /// Материал центрального пятна. Создаётся один раз на забег.
    /// </summary>
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

    /// <summary>
    /// Материал брызг. Создаётся один раз на забег.
    /// </summary>
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

    public static void SpawnAt(
        Vector3 position,
        float poolSize = 1.2f,
        int splatters = 4,
        float maxOffset = 1.1f)
    {
        BloodPool blood = Rent();

        if (blood == null)
            return;

        blood.Play(position, poolSize, splatters, maxOffset);
    }

    private static BloodPool Rent()
    {
        EnsureTicker();

        while (pool.Count > 0)
        {
            // Объект мог быть уничтожен сменой сцены.
            BloodPool candidate = pool.Dequeue();

            if (candidate != null)
            {
                candidate.transform.SetParent(null);
                candidate.transform.rotation = Quaternion.identity;
                candidate.gameObject.SetActive(true);

                if (!active.Contains(candidate))
                    active.Add(candidate);

                return candidate;
            }
        }

        GameObject created = new GameObject("BloodPool");

        BloodPool instance = created.AddComponent<BloodPool>();

        active.Add(instance);

        return instance;
    }

    private void Play(
        Vector3 position,
        float poolSize,
        int splatters,
        float maxOffset)
    {
        if (poolSize > 0f)
            mainPoolSize = poolSize;

        if (splatters >= 0)
            splatterCount = splatters;

        if (maxOffset > 0f)
            maxSplatterOffset = maxOffset;

        remaining = lifetime;

        transform.localPosition = new Vector3(
            position.x,
            GroundY,
            position.z
        );

        transform.localScale = Vector3.one;

        WriteMainDecal();
        WriteSplatters();
    }

    // Пятно рисуется в плоскости земли: четыре вершины квадрата
    // лежат в XZ, нормаль смотрит вверх.
    private void WriteMainDecal()
    {
        float size =
            mainPoolSize *
            (1f - Random.value * mainSizeRandomness);

        Vector3 center = new Vector3(
            Random.Range(-0.15f, 0.15f),
            0f,
            Random.Range(-0.15f, 0.15f)
        );

        WriteQuad(
            mainVertices,
            mainNormals,
            mainUvs,
            mainTriangles,
            0,
            center,
            size,
            Random.Range(0f, 360f)
        );

        mainFilter.mesh.vertices = mainVertices;
        mainFilter.mesh.normals = mainNormals;
        mainFilter.mesh.uv = mainUvs;
        mainFilter.mesh.triangles = mainTriangles;

        mainRenderer.sharedMaterial = MainMaterial;
    }

    private void WriteSplatters()
    {
        int requested = Mathf.Clamp(splatterCount, 0, MaxSplatters);

        // Незапрошенные брызги остаются вырожденными: у них нулевой
        // размер, поэтому они не видны и не стоят ничего. Так размеры
        // массивов постоянны и меш не переаллоцируется каждый спавн.
        for (int i = 0; i < MaxSplatters; i++)
        {
            int vertex = i * 4;
            int triangle = i * 6;

            if (i >= requested)
            {
                for (int v = 0; v < 4; v++)
                    splatterVertices[vertex + v] = Vector3.zero;

                for (int t = 0; t < 6; t++)
                    splatterTriangles[triangle + t] = 0;

                continue;
            }

            float size =
                Random.Range(minSplatterSize, maxSplatterSize);

            Vector2 offset =
                Random.insideUnitCircle * maxSplatterOffset;

            WriteQuad(
                splatterVertices,
                splatterNormals,
                splatterUvs,
                splatterTriangles,
                vertex,
                new Vector3(offset.x, 0f, offset.y),
                size,
                Random.Range(0f, 360f),
                triangle
            );
        }

        splatterFilter.mesh.vertices = splatterVertices;
        splatterFilter.mesh.normals = splatterNormals;
        splatterFilter.mesh.uv = splatterUvs;
        splatterFilter.mesh.triangles = splatterTriangles;

        bool hasSplatters = requested > 0;

        if (splatterRenderer.enabled != hasSplatters)
            splatterRenderer.enabled = hasSplatters;

        if (hasSplatters)
            splatterRenderer.sharedMaterial = SplatterMaterial;
    }

    private static void WriteQuad(
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] uvs,
        int[] triangles,
        int vertexBase,
        Vector3 center,
        float size,
        float yawDegrees,
        int triangleBase = 0)
    {
        float half = size * 0.5f;
        float yaw = yawDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(yaw);
        float sin = Mathf.Sin(yaw);

        // Поворот квадрата в плоскости земли: тот же Euler(90, yaw, 0),
        // что и раньше на кваде, только посчитанный сразу в вершины.
        WriteCorner(vertices, vertexBase + 0, center, -half, -half, cos, sin);
        WriteCorner(vertices, vertexBase + 1, center, half, -half, cos, sin);
        WriteCorner(vertices, vertexBase + 2, center, half, half, cos, sin);
        WriteCorner(vertices, vertexBase + 3, center, -half, half, cos, sin);

        uvs[vertexBase + 0] = new Vector2(0f, 0f);
        uvs[vertexBase + 1] = new Vector2(1f, 0f);
        uvs[vertexBase + 2] = new Vector2(1f, 1f);
        uvs[vertexBase + 3] = new Vector2(0f, 1f);

        for (int v = 0; v < 4; v++)
            normals[vertexBase + v] = Vector3.up;

        // Порядок обратный, чтобы нормаль треугольника смотрела вверх.
        triangles[triangleBase + 0] = vertexBase + 0;
        triangles[triangleBase + 1] = vertexBase + 2;
        triangles[triangleBase + 2] = vertexBase + 1;
        triangles[triangleBase + 3] = vertexBase + 0;
        triangles[triangleBase + 4] = vertexBase + 3;
        triangles[triangleBase + 5] = vertexBase + 2;
    }

    private static void WriteCorner(
        Vector3[] vertices,
        int index,
        Vector3 center,
        float x,
        float z,
        float cos,
        float sin)
    {
        vertices[index] = new Vector3(
            center.x + x * cos - z * sin,
            0f,
            center.z + x * sin + z * cos
        );
    }

    private void Awake()
    {
        CreateRenderers();
    }

    private void CreateRenderers()
    {
        mainVertices = new Vector3[4];
        mainNormals = new Vector3[4];
        mainUvs = new Vector2[4];
        mainTriangles = new int[6];

        splatterVertices = new Vector3[MaxSplatters * 4];
        splatterNormals = new Vector3[MaxSplatters * 4];
        splatterUvs = new Vector2[MaxSplatters * 4];
        splatterTriangles = new int[MaxSplatters * 6];

        CreateDecalRenderer("BloodMain", out mainFilter, out mainRenderer);
        CreateDecalRenderer("BloodSplatters", out splatterFilter, out splatterRenderer);

        splatterRenderer.enabled = false;
    }

    private void CreateDecalRenderer(
        string name,
        out MeshFilter filter,
        out MeshRenderer meshRenderer)
    {
        GameObject quad = new GameObject(name);

        quad.transform.SetParent(transform, false);

        Mesh mesh = new Mesh
        {
            name = name
        };

        // Меш живёт столько же, сколько лужа, и переиспользуется
        // вместе с ней, поэтому ручной вертикальный слепок не нужен.
        mesh.MarkDynamic();

        filter = quad.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        meshRenderer = quad.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage =
            UnityEngine.Rendering.ReflectionProbeUsage.Off;
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

    private static void EnsureTicker()
    {
        if (ticker != null)
            return;

        GameObject host = new GameObject("BloodPoolTicker");

        Object.DontDestroyOnLoad(host);

        ticker = host.AddComponent<BloodPoolTicker>();
    }

    // Обслуживает срок жизни всех луж разом.
    private sealed class BloodPoolTicker : MonoBehaviour
    {
        private void Update()
        {
            float delta = Time.deltaTime;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                BloodPool blood = active[i];

                if (blood == null)
                {
                    active.RemoveAt(i);
                    continue;
                }

                blood.remaining -= delta;

                if (blood.remaining > 0f)
                    continue;

                blood.gameObject.SetActive(false);

                if (pool.Count < MaxPooled)
                    pool.Enqueue(blood);

                active.RemoveAt(i);
            }

            // Тикер нужен только пока есть живые лужи. Когда их не
            // осталось, объект снимает себя, чтобы не болтаться в сцене
            // пустым GameObject между волнами.
            if (active.Count == 0)
                Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (mainFilter != null &&
            mainFilter.sharedMesh != null)
        {
            Destroy(mainFilter.sharedMesh);
        }

        if (splatterFilter != null &&
            splatterFilter.sharedMesh != null)
        {
            Destroy(splatterFilter.sharedMesh);
        }
    }
}
