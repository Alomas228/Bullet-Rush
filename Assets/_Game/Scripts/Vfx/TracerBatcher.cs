using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Один draw call на все трассеры пуль, а не по одному на каждый.
///
/// Раньше у каждого TracerEffect был свой MeshRenderer, поэтому
/// сотня пуль в воздухе давала сотню драфколлов. Сейчас объекты
/// пула не рисуют ничего, а батчер собирает их матрицы в два
/// массива и отправляет два вызова: хвосты и ядра.
///
/// ПОЧЕМУ ДВА ВЫЗОВА, А НЕ ОДИН. Раньше хвост и ядро жили в одном
/// меше и отличались только формой в шейдере, а длина хвоста и
/// размер ядра были зашиты прямо в вершины. С GPU-инстансингом
/// все инстансы обязаны иметь ОДИН И ТОТ ЖЕ меш, иначе их нечем
/// будет собрать в один вызов. Зашить разные размеры в вершины
/// больше нельзя, зато можно в матрицу инстанса - поэтому хвост
/// и ядро стали двумя разными квадами и двумя вызовами.
/// Цена: +1 драфколл. Выигрыш: было N, стало 2.
///
/// Инстанс-шейдер не менялся: у квада хвоста UV1.x = -1, у квада
/// ядра UV1.x = +1, то есть ровно те же значения, что раньше
/// стояли в меше двух квадов. Фрагментный шейдер не отличает
/// хвост от ядра вообще.
///
/// Стоимость на кадр: один проход по активным трассерам, без
/// аллокаций (массивы растут только при переполнении) и без
/// сортировки прозрачных - трассеры аддитивные, порядок не важен.
/// </summary>
[DefaultExecutionOrder(300)]
public sealed class TracerBatcher : MonoBehaviour
{
    /// <summary>
    /// Graphics.RenderMeshInstanced с массивом Matrix4x4 кладёт в
    /// данные инстанса и objectToWorld, и worldToObject, поэтому
    /// предел - 511, а не 1023. Превышение кидает
    /// InvalidOperationException, а не рисует часть батча, так
    /// что длинные очереди режутся на чанки по этому пределу.
    /// Пул прогревается на 160, реально в воздухе ~84 пули, так
    /// что на практике чанков всегда ровно один.
    /// </summary>
    private const int MaxPerDraw = 511;

    /// <summary>
    /// Запас под матрицы с двух сторон: хвост игрока плюс хвост
    /// врага в одном массиве. Та же величина, что InitialCapacity
    /// у VfxUpdater, - на максимальной стрельбе обе стороны
    /// укладываются без единого resize.
    /// </summary>
    private const int InitialCapacity = 256;

    private static TracerBatcher instance;

    private static readonly List<TracerEffect> active =
        new List<TracerEffect>(InitialCapacity);

    private static Matrix4x4[] tailMatrices =
        new Matrix4x4[InitialCapacity];

    private static Matrix4x4[] coreMatrices =
        new Matrix4x4[InitialCapacity];

    /// <summary>
    /// Ставит трассер в очередь на отрисовку. Зовётся из
    /// TracerEffect.Play - то есть ровно тогда, когда пул выдаёт
    /// объект под выстрел.
    ///
    /// Дублей быть не может: пул не отдаёт объект, не вернув его
    /// обратно, а Unregister убирает его из списка по ссылке.
    /// Именно поэтому здесь НЕТ проверки "уже зарегистрирован":
    /// после перезагрузки домена статический список пуст, а флаг
    /// Batched на живых трассерах остался true. Проверка по флагу
    /// молча выкинула бы их из очереди навсегда - до конца жизни.
    /// </summary>
    public static void Register(TracerEffect tracer)
    {
        if (tracer == null)
            return;

        EnsureExists();

        tracer.Batched = true;
        active.Add(tracer);
    }

    /// <summary>
    /// Снимает трассер с отрисовки. Убираем по ссылке, а не только
    /// сбрасываем флаг: список сам является источником истины, и
    /// сброшенный флаг не даст отрисовать лишний инстанс.
    ///
    /// Поиск по списку на каждой смерти пули дёшево: список
    /// короткий, а умерших трассеров за кадр единицы.
    /// </summary>
    public static void Unregister(TracerEffect tracer)
    {
        if (tracer == null)
            return;

        tracer.Batched = false;
        active.Remove(tracer);
    }

    private static void EnsureExists()
    {
        // == null срабатывает и на уничтоженный объект, поэтому
        // батчер сам себя восстанавливает после смены сцены.
        if (instance != null)
            return;

        GameObject batcherObject = new GameObject("TracerBatcher");

        instance = batcherObject.AddComponent<TracerBatcher>();

        Object.DontDestroyOnLoad(batcherObject);
    }

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// LateUpdate, а не Update: трассерами управляет VfxUpdater из
    /// Update, и их позиции и фаза затухания должны поспеть сюда
    /// раньше, чем батчер снимет матрицы. Порядок фаз Unity
    /// гарантирует сам - все Update выполняются раньше всех
    /// LateUpdate, независимо от DefaultExecutionOrder.
    /// </summary>
    private void LateUpdate()
    {
        // После пересоздания статиков старый экземпляр остаётся в
        // сцене живым. Если бы он продолжал рисовать, трассеры
        // ушли бы в кадр дважды.
        if (instance != this)
        {
            enabled = false;
            return;
        }

        int total = active.Count;

        if (total == 0)
            return;

        EnsureCapacity(total);

        // Первый проход чистит список и считает игроков и врагов
        // РАЗДЕЛЬНО, плюс копит общий AABB.
        //
        // Слот для матрицы нельзя было вычислять по ходу этого
        // прохода (playerCount + enemyCount++): активный список
        // идёт в порядке спавна, и пули игрока с вражескими в нём
        // перемешаны. Стоило врагу попасть в список раньше хотя бы
        // одного игрока или вклиниться между ними — его матрица
        // ложилась в чужой индекс, затирала чужие данные, и после
        // этого весь батч разъезжался: часть трассеров рисовалась
        // чужим цветом, часть — из устаревшей матрицы прошлого
        // кадра (телепорт и «остановка»), часть пропадала вовсе.
        // Сам снаряд при этом продолжал лететь и бить по игроку —
        // его логика от батчера не зависит. Поэтому индексы
        // раздаём детерминированно вторым проходом.
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        int kept = 0;
        int playerCount = 0;

        for (int read = 0; read < total; read++)
        {
            TracerEffect tracer = active[read];

            // Три способа попасть сюда: пуля умерла (Unregister),
            // объект разрушен сменой сцены, либо релоад домена
            // обнулил статики, но оставив флаг на объекте.
            if (tracer == null || !tracer.Batched || !tracer.IsPlaying)
                continue;

            // Сжатие на месте: элемент, переехавший в kept, уже
            // был обработан выше по индексу, а хвост списка затираем
            // один раз в конце.
            active[kept++] = tracer;

            if (!tracer.EnemyStyle)
                playerCount++;

            Vector3 position = tracer.transform.position;

            if (position.x < min.x) min.x = position.x;
            if (position.y < min.y) min.y = position.y;
            if (position.z < min.z) min.z = position.z;

            if (position.x > max.x) max.x = position.x;
            if (position.y > max.y) max.y = position.y;
            if (position.z > max.z) max.z = position.z;
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

        // Второй проход: игроки ложатся строго с нуля, враги —
        // строго с playerCount. Теперь у каждого куска ровно своя
        // область, и DrawBatch читает ровно то, что записал.
        int playerSlot = 0;
        int enemySlot = playerCount;

        for (int i = 0; i < kept; i++)
        {
            TracerEffect tracer = active[i];
            Transform tracerTransform = tracer.transform;

            tracer.BuildMatrices(
                tracerTransform.position,
                tracerTransform.rotation,
                out Matrix4x4 tail,
                out Matrix4x4 core);

            if (tracer.EnemyStyle)
            {
                tailMatrices[enemySlot] = tail;
                coreMatrices[enemySlot] = core;
                enemySlot++;
            }
            else
            {
                tailMatrices[playerSlot] = tail;
                coreMatrices[playerSlot] = core;
                playerSlot++;
            }
        }

        int enemyCount = kept - playerCount;

        Camera camera = VfxSharedAssets.MainCamera;

        // Отсечение считается по всему батчу целиком: квады у
        // трассеров развёрнуты по-разному, и считать точный AABB
        // значило бы разворачивать углы квада на каждом инстансе.
        // Вместо этого берём AABB по позициям и накрываем его
        // запасом в MaxLength. Лишние единицы в боксе ничего не
        // стоят: в кадр уходит тот же один вызов.
        float pad = TracerEffect.MaxLength + 1f;

        Bounds bounds = new Bounds(
            (max + min) * 0.5f,
            max - min + Vector3.one * (pad * 2f));

        if (playerCount > 0)
        {
            DrawBatch(
                VfxSharedAssets.TracerMaterial,
                VfxSharedAssets.TracerTailMesh,
                tailMatrices,
                0,
                playerCount,
                camera,
                bounds);

            DrawBatch(
                VfxSharedAssets.TracerMaterial,
                VfxSharedAssets.TracerCoreMesh,
                coreMatrices,
                0,
                playerCount,
                camera,
                bounds);
        }

        if (enemyCount > 0)
        {
            DrawBatch(
                VfxSharedAssets.EnemyTracerMaterial,
                VfxSharedAssets.TracerTailMesh,
                tailMatrices,
                playerCount,
                enemyCount,
                camera,
                bounds);

            DrawBatch(
                VfxSharedAssets.EnemyTracerMaterial,
                VfxSharedAssets.TracerCoreMesh,
                coreMatrices,
                playerCount,
                enemyCount,
                camera,
                bounds);
        }
    }

    /// <summary>
    /// Один вызов отрисовки, а при нехватке инстансов - несколько.
    /// Настройки повторяют те, что раньше стояли на MeshRenderer
    /// трассера (VfxSharedAssets.SetupRenderer): аддитивному кваду
    /// без освещения не нужны ни тени, ни light probes, ни motion
    /// vectors, ни отражение.
    ///
    /// renderingLayerMask не задаётся: шейдер не читает слои
    /// рендеринга, а трассеры ничего не освещают. Наследуется
    /// пустое значение - так же, как у всех остальных вызовов
    /// RenderMesh в этом проекте.
    /// </summary>
    private static void DrawBatch(
        Material material,
        Mesh mesh,
        Matrix4x4[] data,
        int start,
        int count,
        Camera camera,
        Bounds bounds)
    {
        if (material == null || mesh == null)
            return;

        RenderParams renderParams = new RenderParams(material)
        {
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            lightProbeUsage = LightProbeUsage.Off,
            motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
            reflectionProbeUsage = ReflectionProbeUsage.Off,
            layer = 0,
            camera = camera,
            worldBounds = bounds
        };

        int drawn = 0;

        while (drawn < count)
        {
            int chunk = count - drawn > MaxPerDraw
                ? MaxPerDraw
                : count - drawn;

            Graphics.RenderMeshInstanced(
                renderParams,
                mesh,
                0,
                data,
                chunk,
                start + drawn);

            drawn += chunk;
        }
    }

    private static void EnsureCapacity(int total)
    {
        if (tailMatrices.Length >= total)
            return;

        int capacity = tailMatrices.Length * 2;

        if (capacity < total)
            capacity = total;

        System.Array.Resize(ref tailMatrices, capacity);
        System.Array.Resize(ref coreMatrices, capacity);
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        active.Clear();

        tailMatrices = new Matrix4x4[InitialCapacity];
        coreMatrices = new Matrix4x4[InitialCapacity];
    }
}