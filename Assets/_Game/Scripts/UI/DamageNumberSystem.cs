using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Числа урона: один экранный канвас на всю игру, пул текстов,
/// одна выборка на кадр.
///
/// Почему не world-space канвас на каждое попадание (как было
/// раньше): канвас на попадание - это отдельный draw call и
/// отдельная перестройка вершин, а попаданий в секунду доходит
/// до десятков. Здесь все числа лежат в одном экранном канвасе
/// и рисуются одним батчем, потому что шрифт у них один.
///
/// Почему не на каждом числе свой Update: N чисел стоили бы N
/// вызовов из движка каждый кадр. Тикает один компонент, числа
/// лежат в статическом реестре - так же, как это сделано для
/// HP-баров в EnemyHealthBarSystem и для VFX в VfxUpdater.
///
/// Число остаётся в мире, а не на экране. Якорь - это точка мира,
/// в которой появилось попадание, и дальше из неё число только
/// поднимается вверх: с мобом оно не едет, зато едет вместе с
/// камерой. Проекция якоря в координаты канваса делается каждый
/// кадр - ровно ту же работу раньше делал world-space канвас на
/// каждое число, только теперь она одна на всех вместо N
/// канвасов и N Update.
///
/// Размер - тоже функция расстояния до камеры, поэтому у далёкого
/// моба число мельче, как и раньше.
///
/// Тайминг игровой (Time.deltaTime), а не реальный: на паузе
/// timeScale = 0 и числа замирают вместе с боем.
/// </summary>
public static class DamageNumberSystem
{
    // Попадений в секунду доходит до 28 (миниган) и выше с
    // множителем темпа, а число живёт 0.7 с. 48 - потолок
    // одновременных чисел: выше экран превращается в кашу из
    // одинаковых белых цифр, а сами лишние тексты всё равно
    // стоили бы работы на перестройку вершин. При переполнении
    // вытесняется самое старое число: свежий урон важнее.
    public const int MaxActive = 48;

    // Слой UI. На нём живут все экраны игры, числа не должны ни
    // попадать под физику, ни участвовать в raycast'ах.
    private const int UiLayer = 5;

    // Выше канваса HP-баров (у него 0), чтобы число перекрывало
    // полосу, а не наоборот.
    private const int CanvasSortingOrder = 1;

    // Опорное разрешение то же, что у игрового HUD, поэтому
    // канвасные единицы совпадают с пикселями остального
    // интерфейса и настройки в префабе читаются как обычно.
    private static readonly Vector2 ReferenceResolution =
        new Vector2(1920f, 1080f);

    // Расстояние камеры до игрока: |CameraFollow.offset| при
    // offset = (0, 12, -8) равно 14.42. На этом расстоянии число
    // выглядит ровно так же, как раньше, поэтому масштаб текста
    // считается как NominalDistance / дистанция.
    private const float NominalDistance = 14.42f;

    // Как часто искать камеру заново, пока её нет (меню, смена
    // сцены). Camera.main - это поиск по тегу, каждый кадр он
    // не нужен.
    private const float CameraSearchInterval = 0.5f;

    // Префаб, с которого берётся вид числа. Все мобы ссылаются
    // на один и тот же, но поле приходит в спавне, а не хранится
    // в системе: ссылка на префаб живёт в мобе.
    private static GameObject template;

    private static readonly Stack<DamageNumber> freeNumbers =
        new Stack<DamageNumber>();

    private static DamageNumber[] activeNumbers =
        new DamageNumber[MaxActive];

    private static int activeCount;
    private static int createdCount;

    private static Canvas canvas;
    private static RectTransform canvasRect;
    private static Camera cachedCamera;
    private static float nextCameraSearchTime;

    private static DamageNumberUpdater updater;

    /// <summary>
    /// Показывать нечего: тикер на этом выключается, а Spawn его
    /// включает обратно.
    /// </summary>
    public static bool IsIdle => activeCount == 0;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeCount = 0;
        createdCount = 0;

        activeNumbers = new DamageNumber[MaxActive];

        freeNumbers.Clear();

        template = null;

        canvas = null;
        canvasRect = null;
        cachedCamera = null;
        nextCameraSearchTime = 0f;
        updater = null;

        DropStrayCanvas();
    }

    /// <summary>
    /// Показать число урона в мировой точке. Зовётся из
    /// Enemy.SpawnDamageNumber на каждое попадание.
    ///
    /// numberTemplate - префаб числа с моба. Он же источник
    /// вида: система берёт кегль и настройки из префаба, а не из
    /// дублирующих их констант.
    /// </summary>
    public static void Spawn(
        Vector3 worldPosition,
        float damage,
        bool isCritical,
        GameObject numberTemplate)
    {
        if (numberTemplate == null)
            return;

        // Все мобы ссылаются на один префаб. Если попался вдруг
        // другой - берётся первый: смешивать в одном батче два
        // разных кегля незачем, а ломать чужой префаб нельзя.
        if (template == null)
            template = numberTemplate;

        if (template == null)
            return;

        EnsureUpdater();
        EnsureCanvas();

        if (canvasRect == null)
            return;

        DamageNumber number = Rent();

        if (number == null)
            return;

        // Список активных меньше, чем MaxActive, только в ту же
        // секунду, когда Rent вытеснил из него самое старое
        // число: место под вытесненное уже освободилось. Дыры
        // тут не бывает, но проверка дешевле, чем пустой сдвиг.
        if (activeCount >= activeNumbers.Length)
        {
            number.Unbind();
            freeNumbers.Push(number);
            return;
        }

        activeNumbers[activeCount] = number;
        activeCount++;

        // Якорь ставится один раз и больше не двигается, кроме
        // подъёма вверх. С мобом число не едет, зато едет вместе
        // с камерой - ровно как в старом world-space варианте.
        number.Bind(damage, isCritical, worldPosition);
    }

    /// <summary>
    /// Скрыть все числа разом. Зовётся из WorldFadeOutManager
    /// перед сжатием мира: раньше он собирал их
    /// FindObjectsByType, то есть каждый раз выделял массив по
    /// всем живым числам в сцене.
    /// </summary>
    public static void HideAll()
    {
        for (int i = activeCount - 1; i >= 0; i--)
        {
            DamageNumber number = activeNumbers[i];

            if (number != null)
            {
                number.Unbind();
                freeNumbers.Push(number);
            }

            activeNumbers[i] = null;
        }

        activeCount = 0;
    }

    /// <summary>
    /// Единственная точка, где живёт работа: раз на кадр все
    /// показанные числа поднимаются, проецируются на канвас и
    /// снимаются те, что дожили. Зовётся тикером.
    /// </summary>
    internal static void Tick()
    {
        if (activeCount == 0)
            return;

        if (canvasRect == null)
            return;

        Camera cam = ResolveCamera();

        if (cam == null)
            return;

        float deltaTime = Time.deltaTime;

        // Обратный обход + swap-back: элемент, переехавший в i,
        // уже был обработан выше по индексу.
        for (int i = activeCount - 1; i >= 0; i--)
        {
            DamageNumber number = activeNumbers[i];

            if (number == null)
            {
                RemoveActiveAt(i);
                continue;
            }

            if (!number.Advance(deltaTime))
            {
                RemoveActiveAt(i);

                number.Unbind();
                freeNumbers.Push(number);
                continue;
            }

            // Проекция идёт каждый кадр, а не один раз на спавн:
            // якорь числа стоит в мире, поэтому сдвиг камеры
            // должен двигать его по экрану. Раньше ту же работу
            // делал world-space канвас на каждое число.
            Vector3 screen =
                cam.WorldToScreenPoint(
                    number.WorldPosition);

            // За спиной камеры проекция зеркальная: точка
            // отразилась бы через центр экрана и число моргнуло
            // бы в другой половине кадра. Гасим, а не проецируем.
            if (screen.z <= 0f)
            {
                number.Hide();
                continue;
            }

            RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screen,
                    null,
                    out Vector2 local);

            number.Show(
                local,
                NominalDistance / screen.z);
        }
    }

    private static DamageNumber Rent()
    {
        if (freeNumbers.Count > 0)
            return freeNumbers.Pop();

        if (createdCount < MaxActive)
        {
            GameObject instance =
                (GameObject)Object.Instantiate(
                    template,
                    canvasRect,
                    false);

            instance.name = "DamageNumber";

            DamageNumber number =
                instance.GetComponent<DamageNumber>();

            // Компонента на префабе нет - значит префаб сломан.
            // Место в бюджете освобождаем обратно, иначе пул
            // молча перестанет расти и каждое попадание будет
            // упираться в тот же Instantiate.
            if (number == null)
            {
                Object.Destroy(instance);
                return null;
            }

            createdCount++;

            // Наружу из пула число выходит только через Bind,
            // который и включает объект. Пока оно лежит в пуле,
            // оно не участвует в перестройке канваса.
            instance.SetActive(false);

            return number;
        }

        // Пул пуст и потолок достигнут. Вытесняется самое
        // старое число: то, что висит дольше всех, уже почти
        // исчезло, а свежий урон игрок обязан увидеть.
        int oldest = FindOldest();

        DamageNumber evicted = activeNumbers[oldest];

        RemoveActiveAt(oldest);
        evicted.Unbind();

        return evicted;
    }

    private static int FindOldest()
    {
        int oldest = 0;
        float oldestAge = -1f;

        for (int i = 0; i < activeCount; i++)
        {
            DamageNumber number = activeNumbers[i];

            if (number == null)
                continue;

            float age = number.Age;

            if (age <= oldestAge)
                continue;

            oldestAge = age;
            oldest = i;
        }

        return oldest;
    }

    private static void RemoveActiveAt(int index)
    {
        int last = activeCount - 1;
        DamageNumber moved = activeNumbers[last];

        activeNumbers[last] = null;
        activeCount = last;

        if (index == last)
            return;

        activeNumbers[index] = moved;
    }

    private static Camera ResolveCamera()
    {
        // == null срабатывает и на уничтоженную камеру, поэтому
        // система сама переживает смену сцены.
        if (cachedCamera != null)
            return cachedCamera;

        if (Time.unscaledTime < nextCameraSearchTime)
            return null;

        nextCameraSearchTime =
            Time.unscaledTime + CameraSearchInterval;

        cachedCamera = Camera.main;
        return cachedCamera;
    }

    private static void EnsureCanvas()
    {
        if (canvas != null)
            return;

        GameObject canvasObject =
            new GameObject(
                "DamageNumberCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));

        canvasObject.layer = UiLayer;

        // Канвас переживает сцену, иначе на каждом входе в бой
        // он пересоздавался бы вместе с числами.
        Object.DontDestroyOnLoad(canvasObject);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;

        // Масштаб как у игрового HUD, поэтому канвасные единицы
        // здесь - те же пиксели, что и в настройках остального
        // интерфейса.
        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;

        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        scaler.matchWidthOrHeight = 0.5f;

        canvasRect = (RectTransform)canvasObject.transform;
    }

    private static void EnsureUpdater()
    {
        if (updater != null)
        {
            if (!updater.enabled)
                updater.enabled = true;

            return;
        }

        GameObject updaterObject =
            new GameObject("DamageNumberUpdater");

        updater =
            updaterObject.AddComponent<DamageNumberUpdater>();

        Object.DontDestroyOnLoad(updaterObject);
    }

    internal static void HandleUpdaterAwake(
        DamageNumberUpdater instance)
    {
        if (updater == null || updater == instance)
        {
            updater = instance;
            return;
        }

        // Дубль тикера. Обычно это переживший перезагрузку домена
        // объект из DontDestroyOnLoad: у него Awake уже не
        // повторится, а тикать он продолжит.
        Object.Destroy(instance.gameObject);
    }

    internal static void HandleUpdaterDestroyed(
        DamageNumberUpdater instance)
    {
        if (updater == instance)
            updater = null;
    }

    /// <summary>
    /// Сносит канвас, оставшийся от прошлой доменной перезагрузки.
    ///
    /// Скрипты перекомпилируются прямо в play mode, и объекты в
    /// DontDestroyOnLoad при этом НЕ умирают: Unity заново создаёт
    /// для них managed-обёртки и восстанавливает сериализованные
    /// поля, а статические поля обнуляются. Числа в этом окне
    /// оказываются сиротами - они не в пуле, никто их не двигает
    /// и не гасит, но на экране они остаются.
    ///
    /// Один поиск на перезагрузку, не на кадр.
    /// </summary>
    private static void DropStrayCanvas()
    {
        if (!Application.isPlaying)
            return;

        DamageNumber[] strays =
            Object.FindObjectsByType<DamageNumber>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        if (strays.Length == 0)
            return;

        // Все числа лежат под одним канвасом, поэтому сносится он
        // целиком вместе с ними.
        Transform root = strays[0].transform.root;

        if (root != null)
            Object.Destroy(root.gameObject);
    }
}
