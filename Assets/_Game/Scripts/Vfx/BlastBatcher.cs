using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Три draw call на ВСЕ взрывы сразу, а не три на каждый.
///
/// Раньше у каждого ExplosionEffect было три собственных меша и
/// три MeshRenderer, а цвет и геометрия писались в вершины каждый
/// кадр. Десяток взрывов давал десятки драфколлов и десятки
/// загрузок меша за кадр. Теперь объекты пула не рисуют ничего:
/// эффект только считает физику, а батчер собирает инстансы в три
/// батча и отправляет три вызова.
///
/// ПОЧЕМУ ИМЕННО ТРИ, А НЕ ОДИН. Слоя три, и два из них нельзя
/// объединить:
///   - шар и угли аддитивные (One One);
///   - дым альфа-ный (SrcAlpha OneMinusSrcAlpha).
/// У Blend разный render state, а он задаётся на материал, то
/// есть на весь вызов. Угли и дым вдобавок рисуются разными
/// материалами. Потолок здесь - три вызова на всю сцену.
///
/// КАК ЕДЕТ ЦВЕТ. Матрица - обычным objectToWorld через
/// Graphics.RenderMeshInstanced. Цвет - отдельным массивом
/// _BlastColor через MaterialPropertyBlock, который шейдер читает
/// как instanced-свойство. Смешивать их в одной структуре инстанса
/// нельзя: RenderMeshInstanced вытаскивает из пользовательской
/// структуры только objectToWorld, renderingLayerMask и
/// prevObjectToWorld, а остальное игнорирует - цвет остался бы на
/// CPU и шейдер получил бы мусор.
///
/// МАССИВ ЦВЕТОВ ВСЕГДА ОДНОЙ ДЛИНЫ. У SetVectorArray длина
/// массива не меняется после первой записи («The array length can't
/// be changed once it has been added to the block»), поэтому
/// drawColors в BlastBatchBuffer зафиксирован на 511 элементах и
/// каждый вызов отдаёт ровно этот массив. Потребляются первые
/// chunk элементов, остальные - хвост, который никто не читает.
///
/// Стоимость на кадр: один проход по активным взрывам, три memcpy
/// чанков и три вызова. Аллокаций нет (матрицы и цвета растут
/// только при переполнении), сортировки прозрачного нет - дым
/// мягкий, а внутри одного взрыва клубы и раньше шли одним мешем.
/// </summary>
[DefaultExecutionOrder(300)]
public sealed class BlastBatcher : MonoBehaviour
{
    /// <summary>
    /// RenderMeshInstanced с массивом Matrix4x4 кладёт в данные
    /// инстанса и objectToWorld, и worldToObject, поэтому предел -
    /// 511, а не 1023. Превышение кидает InvalidOperationException.
    ///
    /// На практике сюда не подойти: 511 углей - это 51 одновременный
    /// взрыв, а пул ещё и дедуплицирует взрывы рядом друг с другом.
    /// Но путь обработки на этот случай всё равно есть.
    /// </summary>
    private const int MaxPerDraw = 511;

    /// <summary>
    /// Запас под матрицы и цвета: шар плюс 10 углей плюс 5 клубов
    /// на взрыв, а одновременных взрывов обычно handful.
    /// </summary>
    private const int InitialCapacity = 256;

    private static readonly int BlastColorId =
        Shader.PropertyToID("_BlastColor");

    /// <summary>
    /// Один слой взрыва: накопленные пары матрица+цвет и свой MPB.
    ///
    /// MPB свой у каждого слоя намеренно: один переиспользуемый
    /// блок на три слоя означал бы, что последний вызов затирает
    /// цвета предыдущих, а затирать их нельзя - длина массива уже
    /// зафиксирована навсегда.
    /// </summary>
    private sealed class Layer
    {
        public readonly string name;

        public readonly BlastBatchBuffer Batch =
            new BlastBatchBuffer();

        public readonly MaterialPropertyBlock Properties =
            new MaterialPropertyBlock();

        public Layer(string name)
        {
            this.name = name;
        }

        public int Count => Batch.Count;
    }

    private static BlastBatcher instance;

    private static readonly List<ExplosionEffect> active =
        new List<ExplosionEffect>(InitialCapacity);

    private static readonly Layer fireballs = new Layer("fireballs");

    private static readonly Layer embers = new Layer("embers");

    private static readonly Layer smoke = new Layer("smoke");

    private static bool loggedEnvironment;

    /// <summary>
    /// Разовая диагностика окружения. Цветной шейдер дважды уходил
    /// на неинстансную ветку, а разбор файлов этого не объяснял -
    /// нужны значения из рантайма.
    /// </summary>
    private static void LogEnvironmentOnce(Layer layer, Material material)
    {
        if (loggedEnvironment)
            return;

        loggedEnvironment = true;

        Shader shader = material.shader;

        Debug.Log(
            "Bullet Rush VFX: BlastBatcher env. слой=" + layer.name +
            " count=" + layer.Count +
            " material=" + material.name +
            " shader=" + (shader == null ? "null" : shader.name) +
            " shaderSupported=" + (shader != null && shader.isSupported) +
            " enableInstancing=" + material.enableInstancing +
            " supportsInstancing=" + SystemInfo.supportsInstancing +
            " gfx=" + SystemInfo.graphicsDeviceType +
            " firstColor=" + DescribeColor(layer.Batch.DrawColors[0]));
    }

    private static string DescribeColor(Vector4 c)
    {
        return "(" + c.x.ToString("F3") + ", " +
            c.y.ToString("F3") + ", " +
            c.z.ToString("F3") + ", " +
            c.w.ToString("F3") + ")";
    }

    /// <summary>
    /// Ставит взрыв в очередь на отрисовку. Зовётся из
    /// ExplosionEffect.Play - то есть ровно тогда, когда пул выдаёт
    /// объект.
    ///
    /// Дублей быть не может: пул не отдаёт объект, не вернув его
    /// обратно, а Unregister убирает его из списка по ссылке.
    /// Поэтому здесь НЕТ проверки "уже зарегистрирован": после
    /// перезагрузки домена статический список пуст, а флаг Batched
    /// на живых взрывах остался бы true, и проверка по флагу молча
    /// выкинула бы их из очереди навсегда.
    /// </summary>
    public static void Register(ExplosionEffect explosion)
    {
        if (explosion == null)
            return;

        EnsureExists();

        explosion.Batched = true;
        active.Add(explosion);
    }

    public static void Unregister(ExplosionEffect explosion)
    {
        if (explosion == null)
            return;

        explosion.Batched = false;
        active.Remove(explosion);
    }

    private static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject batcherObject = new GameObject("BlastBatcher");
        instance = batcherObject.AddComponent<BlastBatcher>();
        Object.DontDestroyOnLoad(batcherObject);
    }

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        // MPB статические и переживают смену сцены. Ничего
        // освобождать не нужно - ни GPU-ресурсов, ни ссылок.
        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// LateUpdate, а не Update: взрывы тикает VfxUpdater из
    /// Update, и их позиции и стадия остывания должны поспеть сюда
    /// раньше, чем батчер снимет инстансы. Порядок фаз Unity
    /// гарантирует сам - все Update выполняются раньше всех
    /// LateUpdate, независимо от DefaultExecutionOrder.
    /// </summary>
    private void LateUpdate()
    {
        if (instance != this)
        {
            enabled = false;
            return;
        }

        int total = active.Count;

        if (total == 0)
            return;

        fireballs.Batch.Clear();
        embers.Batch.Clear();
        smoke.Batch.Clear();

        Vector3 min = new Vector3(
            float.MaxValue,
            float.MaxValue,
            float.MaxValue);

        Vector3 max = new Vector3(
            float.MinValue,
            float.MinValue,
            float.MinValue);

        int kept = 0;

        for (int read = 0; read < total; read++)
        {
            ExplosionEffect explosion = active[read];

            if (explosion == null || !explosion.IsPlaying)
                continue;

            active[kept++] = explosion;

            explosion.AppendFireball(fireballs.Batch);
            explosion.AppendEmbers(embers.Batch);
            explosion.AppendSmoke(smoke.Batch);

            Vector3 origin = explosion.Origin;
            float radius = explosion.BoundsRadius;

            if (origin.x - radius < min.x) min.x = origin.x - radius;
            if (origin.y - radius < min.y) min.y = origin.y - radius;
            if (origin.z - radius < min.z) min.z = origin.z - radius;

            if (origin.x + radius > max.x) max.x = origin.x + radius;
            if (origin.y + radius > max.y) max.y = origin.y + radius;
            if (origin.z + radius > max.z) max.z = origin.z + radius;
        }

        if (kept != total)
            active.RemoveRange(kept, total - kept);

        // Все записи оказались мёртвыми. Возвращаться раньше нужно
        // ещё и потому, что min/max остались бы в исходных
        // экстремумах, а из них получается Bounds с
        // отрицательным размером - такой объект нельзя отдавать
        // в отсечение.
        if (kept == 0)
            return;

        // Отсечение считается по всему батчу целиком: квады у
        // взрывов развёрнуты по-разному, и считать точный AABB
        // значило бы разворачивать углы квада на каждом инстансе.
        Bounds bounds = new Bounds(
            (max + min) * 0.5f,
            max - min);

        Draw(
            fireballs,
            VfxSharedAssets.ExplosionSphereMaterial,
            VfxSharedAssets.BlastFireballMesh,
            bounds);

        Draw(
            embers,
            VfxSharedAssets.BlastEmberMaterial,
            VfxSharedAssets.BlastQuadMesh,
            bounds);

        Draw(
            smoke,
            VfxSharedAssets.BlastSmokeMaterial,
            VfxSharedAssets.BlastQuadMesh,
            bounds);
    }

    /// <summary>
    /// Один вызов отрисовки, а при нехватке инстансов - несколько.
    /// Настройки повторяют те, что раньше стояли на MeshRenderer
    /// взрыва: аддитивному шару и квадам без освещения не нужны
    /// ни тени, ни light probes, ни motion vectors, ни отражение.
    /// </summary>
    private static void Draw(
        Layer layer,
        Material material,
        Mesh mesh,
        Bounds bounds)
    {
        int count = layer.Count;

        if (count == 0 || material == null || mesh == null)
            return;

        LogEnvironmentOnce(layer, material);

        RenderParams renderParams = new RenderParams(material)
        {
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            lightProbeUsage = LightProbeUsage.Off,
            motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
            reflectionProbeUsage = ReflectionProbeUsage.Off,
            layer = 0,
            camera = VfxSharedAssets.MainCamera,
            worldBounds = bounds,
            matProps = layer.Properties
        };

        BlastBatchBuffer batch = layer.Batch;

        int drawn = 0;

        while (drawn < count)
        {
            int chunk = count - drawn > MaxPerDraw
                ? MaxPerDraw
                : count - drawn;

            // Чанк всегда готовим с нуля, включая первый: так
            // индекс матрицы в структуре и индекс цвета в массиве
            // MPB совпадают для каждого вызова, а сам вызов всегда
            // идёт с startInstance = 0.
            batch.PrepareChunk(drawn, chunk);

            // Длина массива всегда 511 (см. BlastBatchBuffer),
            // поэтому блок не переаллоцируется. Этот вызов читает
            // первые chunk элементов.
            layer.Properties.SetVectorArray(
                BlastColorId,
                batch.DrawColors);

            try
            {
                Graphics.RenderMeshInstanced(
                    renderParams,
                    mesh,
                    0,
                    batch.DrawItems,
                    chunk,
                    0);
            }
            catch (System.Exception e)
            {
                Debug.LogError(
                    "Bullet Rush VFX: RenderMeshInstanced упал на слое " +
                    layer.name + ": " + e.GetType().Name + " - " +
                    e.Message);

                return;
            }

            drawn += chunk;
        }
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        active.Clear();
        loggedEnvironment = false;

        fireballs.Batch.Clear();
        embers.Batch.Clear();
        smoke.Batch.Clear();
    }
}
