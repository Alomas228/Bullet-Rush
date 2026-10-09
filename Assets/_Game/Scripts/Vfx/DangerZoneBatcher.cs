using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Что-то с жизненным циклом опасной зоны, что умеет выдать себя
/// батчеру. Реализуют HazardZone (элита и событие волны) и
/// BossAttackZone. Интерфейс, а не общий базовый класс, чтобы не
/// тащить в плоскую зону ничего лишнего из их разной логики.
/// </summary>
public interface IDangerZone
{
    /// <summary>
    /// Зона ещё жива и должна рисоваться. Как только false -
    /// батчер перестаёт её спрашивать в этом же кадре.
    /// </summary>
    bool ZonePlaying { get; }

    /// <summary>Центр зоны: от него считается общий AABB.</summary>
    Vector3 ZoneOrigin { get; }

    /// <summary>Радиус, целиком покрывающий визуал зоны.</summary>
    float ZoneBoundsRadius { get; }

    /// <summary>
    /// Кладёт один инстанс зоны в буфер: матрицу, цвет и параметры
    /// фаз. Вызывается батчером раз в кадр.
    /// </summary>
    void AppendZone(DangerZoneBatchBuffer batch);
}

/// <summary>
/// Один draw call на ВСЕ опасные зоны сцены.
///
/// Раньше каждая зона собирала диск, 16 кубиков бордюра и сферу
/// вспышки - до 18 объектов и столько же вызовов на зону. Залп
/// элит или событие волны с десятком зон давали сотни вызовов.
///
/// Теперь у зоны нет ни меша, ни рендерера, ни дочерних объектов:
/// она только считает таймеры урона, а вся картинка (заливка,
/// пунктирный бордюр, полоски, кольцо вспышки) рисуется одним
/// общим диском (VfxSharedAssets.DangerDiscMesh) через инстансинг.
/// Меш и материал общие на все зоны, цвет и фазы приходят
/// per-instance через MaterialPropertyBlock, а сам вызов один.
///
/// Прозрачной сортировки между зонами нет - и не нужно: зоны
/// аддитивные, лежат в одной плоскости и накладываются друг на
/// друга без артефактов порядка.
///
/// Порядок выполнения 320 - после VfxUpdater (200), который тикает
/// обычные VFX, и после BloodBatcher (300). LateUpdate всё равно
/// идёт после всех Update, и зоны успевают выставить свои фазы.
/// </summary>
[DefaultExecutionOrder(320)]
public sealed class DangerZoneBatcher : MonoBehaviour
{
    /// <summary>
    /// RenderMeshInstanced с массивом Matrix4x4 кладёт в данные
    /// инстанса и objectToWorld, и worldToObject, поэтому предел -
    /// 511, а не 1023. На этот случай есть разбивка на чанки.
    /// </summary>
    private const int MaxPerDraw = 511;

    private static readonly int ZoneColorId =
        Shader.PropertyToID("_ZoneColor");

    private static readonly int ZoneParamsId =
        Shader.PropertyToID("_ZoneParams");

    private static DangerZoneBatcher instance;

    private static readonly List<IDangerZone> active =
        new List<IDangerZone>(32);

    private static readonly DangerZoneBatchBuffer batch =
        new DangerZoneBatchBuffer();

    private static readonly MaterialPropertyBlock properties =
        new MaterialPropertyBlock();

    private static bool loggedEnvironment;

    /// <summary>
    /// Разовый лог окружения: если материал пришёл не тот или
    /// инстансинг недоступен, это видно из первой же отрисовки.
    /// </summary>
    private static void LogEnvironmentOnce(Material material)
    {
        if (loggedEnvironment)
            return;

        loggedEnvironment = true;

        Shader shader = material.shader;

        Debug.Log(
            "Bullet Rush VFX: DangerZoneBatcher env. count=" +
            batch.Count +
            " material=" + material.name +
            " shader=" + (shader == null ? "null" : shader.name) +
            " shaderSupported=" +
            (shader != null && shader.isSupported) +
            " enableInstancing=" + material.enableInstancing +
            " supportsInstancing=" + SystemInfo.supportsInstancing +
            " gfx=" + SystemInfo.graphicsDeviceType);
    }

    public static void Register(IDangerZone zone)
    {
        if (zone == null)
            return;

        EnsureExists();

        if (active.Contains(zone))
            return;

        active.Add(zone);
    }

    public static void Unregister(IDangerZone zone)
    {
        if (zone == null)
            return;

        active.Remove(zone);
    }

    private static void EnsureExists()
    {
        if (instance != null)
            return;

        GameObject batcherObject =
            new GameObject("DangerZoneBatcher");

        instance = batcherObject.AddComponent<DangerZoneBatcher>();

        Object.DontDestroyOnLoad(batcherObject);
    }

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        // MPB и буфер статические и переживают смену сцены;
        // освобождать нечего.
        if (instance == this)
            instance = null;
    }

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

        batch.Clear();

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
            IDangerZone zone = active[read];

            // as MonoBehaviour, а не просто zone == null: у
            // интерфейсной ссылки == сравнивает ссылки C#, и
            // уничтоженный, но ещё не снятый с регистрации объект
            // прошёл бы проверку и упал на обращении к transform.
            if (!(zone is MonoBehaviour behaviour) ||
                behaviour == null ||
                !zone.ZonePlaying)
            {
                continue;
            }

            active[kept++] = zone;

            zone.AppendZone(batch);

            Vector3 origin = zone.ZoneOrigin;
            float radius = zone.ZoneBoundsRadius;

            if (origin.x - radius < min.x) min.x = origin.x - radius;
            if (origin.z - radius < min.z) min.z = origin.z - radius;

            if (origin.x + radius > max.x) max.x = origin.x + radius;
            if (origin.z + radius > max.z) max.z = origin.z + radius;

            // Зона плоская: по высоте держим небольшой запас,
            // чтобы отсечение не срезало диск у земли.
            if (origin.y - radius < min.y) min.y = origin.y - radius;
            if (origin.y + radius > max.y) max.y = origin.y + radius;
        }

        if (kept != total)
            active.RemoveRange(kept, total - kept);

        if (kept == 0)
            return;

        Bounds bounds = new Bounds(
            (max + min) * 0.5f,
            max - min);

        Draw(bounds);
    }

    private static void Draw(Bounds bounds)
    {
        int count = batch.Count;

        Material material = VfxSharedAssets.DangerZoneMaterial;
        Mesh mesh = VfxSharedAssets.DangerDiscMesh;

        if (count == 0 || material == null || mesh == null)
            return;

        LogEnvironmentOnce(material);

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
            matProps = properties
        };

        int drawn = 0;

        while (drawn < count)
        {
            int chunk = count - drawn > MaxPerDraw
                ? MaxPerDraw
                : count - drawn;

            // Чанк всегда готовим с нуля, включая первый: индекс
            // матрицы и индекс в обоих массивах MPB совпадают для
            // каждого вызова, а старт всегда 0.
            batch.PrepareChunk(drawn, chunk);

            // Длина массивов всегда 511 (см. DangerZoneBatchBuffer),
            // поэтому блок не переаллоцируется.
            properties.SetVectorArray(
                ZoneColorId,
                batch.DrawColors);

            properties.SetVectorArray(
                ZoneParamsId,
                batch.DrawParams);

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
                    "Bullet Rush VFX: RenderMeshInstanced упал на " +
                    "опасной зоне: " + e.GetType().Name + " - " +
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
        batch.Clear();
        loggedEnvironment = false;
    }
}
