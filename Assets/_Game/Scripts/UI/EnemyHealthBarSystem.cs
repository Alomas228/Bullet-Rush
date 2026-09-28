using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HP-бары мобов: один канвас на всю игру, пул полос, одна выборка
/// на кадр.
///
/// Почему не свой world-space канвас на моба (как у DamageNumber):
/// канвас на моба - это отдельный draw call и отдельная
/// перестройка вершин, а мобов на экране десятки и полоса нужна
/// каждому раненому. Здесь все полосы лежат в одном экранном
/// канвасе, рисуются одним батчем и обновляются одним тикером
/// вместо N собственных Update.
///
/// Полоса берётся из пула только когда моб ранен и попал в кадр,
/// поэтому нераненый моб не стоит вообще ничего. Снятие с экрана
/// происходит наоборот: как только моб ушёл из кадра или умер,
/// полоса возвращается в пул.
///
/// Реестр мобов - тот же приём, что у ColliderKindQuery: моб
/// встаёт в список на Awake и выходит на OnDestroy, поэтому
/// здесь лежат ровно живые мобы, а не все за забег.
/// </summary>
public static class EnemyHealthBarSystem
{
    // Мобов в бою десятки, но вонзаются и волны толпой. 128 -
    // с запасом, чтобы массив не рос (аллокация) посреди боя.
    private const int InitialCapacity = 128;

    // Сколько полос рисуется одновременно. Выше 24 экран
    // превращается в кашу из одинаковых красных полос, и на
    // мобов вдалеке их всё равно не разглядеть.
    public const int MaxVisibleBars = 24;

    // Слой UI. На нём живут все экраны игры, полосы не должны ни
    // попадать под физику, ни участвовать в raycast'ах.
    private const int UiLayer = 5;

    private const int CanvasSortingOrder = 0;

    // Запас за краем экрана, чтобы моб, стоящий на самом краю
    // кадра, не терял половину полосы.
    private const float ViewportMargin = 16f;

    // Как часто искать камеру заново, пока её нет (меню, смена
    // сцены). Camera.main - это поиск по тегу, каждый кадр он
    // не нужен.
    private const float CameraSearchInterval = 0.5f;

    // Как часто пересматривать мобов, пока ни одна полоса не
    // показана. 10 раз в секунду хватает, чтобы полоса появилась
    // тем же кадром, что и урон, и не гонять камеру впустую на
    // полусотне целых мобов.
    private const float IdleScanInterval = 0.1f;

    private static readonly Vector2 ReferenceResolution =
        new Vector2(1920f, 1080f);

    private static Enemy[] enemies = new Enemy[InitialCapacity];
    private static int count;

    private static readonly Stack<EnemyHealthBarView> freeBars =
        new Stack<EnemyHealthBarView>();

    private static EnemyHealthBarView[] activeBars =
        new EnemyHealthBarView[MaxVisibleBars];

    private static int activeCount;
    private static int createdCount;

    // Кандидаты на отрисовку: раненые мобы в кадре, отсортированные
    // по близости. Если их больше, чем мест в бюджете, рисуются
    // ближние - их игрок видит и в них стреляет.
    private static Enemy[] candidates = new Enemy[MaxVisibleBars];
    private static float[] candidateDepths = new float[MaxVisibleBars];
    private static Vector2[] candidatePositions =
        new Vector2[MaxVisibleBars];

    private static Canvas canvas;
    private static RectTransform canvasRect;
    private static Camera cachedCamera;
    private static float nextCameraSearchTime;
    private static float nextScanTime;

    private static EnemyHealthBarUpdater updater;

    /// <summary>
    /// Нечего обновлять: мобов нет и показывать нечего. Тикер
    /// на этом выключается, а Register его включает обратно.
    /// </summary>
    public static bool IsIdle => count == 0 && activeCount == 0;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        count = 0;
        activeCount = 0;
        createdCount = 0;

        enemies = new Enemy[InitialCapacity];
        activeBars = new EnemyHealthBarView[MaxVisibleBars];
        candidates = new Enemy[MaxVisibleBars];
        candidateDepths = new float[MaxVisibleBars];
        candidatePositions = new Vector2[MaxVisibleBars];

        freeBars.Clear();

        canvas = null;
        canvasRect = null;
        cachedCamera = null;
        nextCameraSearchTime = 0f;
        nextScanTime = 0f;
        updater = null;

        DropStrayCanvas();
    }

    /// <summary>
    /// Моб встал в реестр. Зовётся из Enemy.Awake.
    /// </summary>
    public static void Register(Enemy enemy)
    {
        if (enemy == null)
            return;

        if (enemy.HealthBarRegistryIndex >= 0)
            return;

        EnsureUpdater();

        if (count == enemies.Length)
            Grow(count * 2);

        enemy.HealthBarRegistryIndex = count;
        enemies[count] = enemy;
        count++;
    }

    /// <summary>
    /// Моб вышел из реестра. Зовётся из Enemy.OnDestroy.
    /// Полоса снимается сразу, а не на следующем кадре: после
    /// Destroy(gameObject) моба на экране ещё кадр висит его полоса.
    /// </summary>
    public static void Unregister(Enemy enemy)
    {
        if (enemy == null)
            return;

        int index = enemy.HealthBarRegistryIndex;
        enemy.HealthBarRegistryIndex = -1;

        // Проверка принадлежности обязательна: после перезагрузки
        // домена индекс у живого моба остаётся от прошлой сессии,
        // а массив уже пересоздан с нуля.
        if (index >= 0 &&
            index < count &&
            enemies[index] == enemy)
        {
            RemoveAt(index);
        }

        ReleaseBinding(enemy);
    }

    /// <summary>
    /// Единственная точка, где живёт работа: один раз на кадр
    /// пересматривает мобов и двигает показанные полосы.
    /// Зовётся тикером.
    /// </summary>
    internal static void Tick()
    {
        if (count == 0)
            return;

        Camera cam = ResolveCamera();

        if (cam == null)
            return;

        // Пока не показана ни одна полоса, пересматривать мобов
        // каждый кадр смысла нет: 10 раз в секунду хватает, чтобы
        // полоса появилась тем же кадром, что и урон. Канвас
        // создаётся после этой проверки - до первого раненого
        // моба он в сцене просто не нужен.
        if (activeCount == 0)
        {
            if (Time.unscaledTime < nextScanTime)
                return;

            nextScanTime =
                Time.unscaledTime + IdleScanInterval;
        }

        EnsureCanvas();

        if (canvasRect == null)
            return;

        int found = Collect(cam);

        PlaceBars(found);
        ReleaseUnclaimed();
    }

    /// <summary>
    /// Отбор мобов, которым в этом кадре нужна полоса: ранены,
    /// живы, видимы, не дальше своей дистанции. Возвращает
    /// количество отобранных, они лежат в candidates.
    /// </summary>
    private static int Collect(Camera cam)
    {
        int found = 0;

        float halfWidth =
            canvasRect.rect.width * 0.5f + ViewportMargin;

        float halfHeight =
            canvasRect.rect.height * 0.5f + ViewportMargin;

        for (int i = 0; i < count; i++)
        {
            Enemy enemy = enemies[i];

            // Моб мог быть уничтожен сценой, а OnDestroy не успел.
            if (enemy == null)
                continue;

            // Полосы нет, пока моб целый. Проверка дешевле любой
            // работы с камерой, поэтому идёт первой.
            if (enemy.IsDead || !enemy.HasTakenDamage)
                continue;

            EnemyHealthBarSettings settings =
                enemy.HealthBarSettings;

            if (settings == null || !settings.Visible)
                continue;

            Vector3 screen = cam.WorldToScreenPoint(
                enemy.transform.position);

            // z - расстояние вдоль взгляда камеры. За спиной
            // камеры оно отрицательное, это и есть "за экраном".
            if (screen.z <= 0f)
                continue;

            float maxDistance = settings.MaxDistance;

            if (maxDistance > 0f && screen.z > maxDistance)
                continue;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screen,
                null,
                out Vector2 local);

            // В кадре проверяется сам моб, а не полоса: сдвиг на
            // пару десятков пикселей вверх не должен убирать
            // полосу у моба, стоящего на самом краю экрана.
            if (Mathf.Abs(local.x) > halfWidth ||
                Mathf.Abs(local.y) > halfHeight)
            {
                continue;
            }

            // Сдвиг задаётся в пикселях экрана, а не в мире: так
            // его можно крутить руками в инспекторе, и он не
            // зависит от того, под каким углом стоит камера.
            InsertCandidate(
                enemy,
                local + settings.ScreenOffset,
                screen.z,
                ref found);
        }

        return found;
    }

    /// <summary>
    /// Вставка в отсортированный по близости список ограниченного
    /// размера. Полная сортировка на кадре аллоцировала бы буфер и
    /// упорядочивала бы в том числе тех, кто за бюджетом не войдёт,
    /// поэтому сортируется только верхний край.
    /// </summary>
    private static void InsertCandidate(
        Enemy enemy,
        Vector2 position,
        float depth,
        ref int found)
    {
        int insertIndex;

        if (found < MaxVisibleBars)
        {
            insertIndex = found;
            found++;
        }
        else
        {
            if (depth >= candidateDepths[MaxVisibleBars - 1])
                return;

            insertIndex = MaxVisibleBars - 1;
        }

        while (insertIndex > 0 &&
               candidateDepths[insertIndex - 1] > depth)
        {
            candidateDepths[insertIndex] =
                candidateDepths[insertIndex - 1];

            candidates[insertIndex] =
                candidates[insertIndex - 1];

            candidatePositions[insertIndex] =
                candidatePositions[insertIndex - 1];

            insertIndex--;
        }

        candidateDepths[insertIndex] = depth;
        candidates[insertIndex] = enemy;
        candidatePositions[insertIndex] = position;
    }

    /// <summary>
    /// Привязывает полосы к отобранным мобам: берёт из пула или
    /// переиспользует ту, что уже была на этом мобе, и двигает её.
    /// </summary>
    private static void PlaceBars(int found)
    {
        for (int i = 0; i < found; i++)
        {
            Enemy enemy = candidates[i];
            EnemyHealthBarView bar = enemy.HealthBarView;

            if (bar == null)
            {
                bar = Rent();

                // Больше MaxVisibleBars полос не создаётся
                // принципиально: это жёсткий потолок, а не
                // округление вниз. Моб без полосы ничего не теряет
                // - в следующем кадре он снова попадёт в отбор.
                if (bar == null)
                    continue;

                if (activeCount >= activeBars.Length)
                {
                    // Потолок списка активных. Полоса только что
                    // взята из пула, не привязана - просто назад.
                    Release(bar);
                    continue;
                }

                enemy.HealthBarView = bar;
                bar.Owner = enemy;

                bar.ApplySettings(enemy.HealthBarSettings);
                bar.SetVisible(true);

                activeBars[activeCount] = bar;
                activeCount++;
            }

            bar.Claimed = true;
            bar.SetPosition(candidatePositions[i]);
            bar.SetHealth(GetHealthPercent(enemy));
        }
    }

    /// <summary>
    /// Все полосы, которые в этом кадре не привязались ни к одному
    /// мобу (моб ушёл из кадра, умер, дошёл до бюджета) -
    /// обратно в пул.
    /// </summary>
    private static void ReleaseUnclaimed()
    {
        // Обратный обход + swap-back: элемент, переехавший в i,
        // уже был обработан выше по индексу.
        for (int i = activeCount - 1; i >= 0; i--)
        {
            EnemyHealthBarView bar = activeBars[i];

            if (bar == null || bar.Claimed)
                continue;

            RemoveActiveAt(i);
            Release(bar);
        }
    }

    private static float GetHealthPercent(Enemy enemy)
    {
        float maxHealth = enemy.MaxHealth;

        if (maxHealth <= 0f)
            return 0f;

        return Mathf.Clamp01(enemy.CurrentHealth / maxHealth);
    }

    private static EnemyHealthBarView Rent()
    {
        if (freeBars.Count > 0)
            return freeBars.Pop();

        if (createdCount >= MaxVisibleBars)
            return null;

        createdCount++;

        return EnemyHealthBarView.Create(canvasRect);
    }

    private static void Release(EnemyHealthBarView bar)
    {
        if (bar == null)
            return;

        Enemy owner = bar.Owner;

        if (owner != null)
            owner.HealthBarView = null;

        bar.Unbind();
        freeBars.Push(bar);
    }

    /// <summary>
    /// Снимает полосу именно этого моба: моб уничтожен, а сама
    /// полоса ещё висит в списке активных.
    /// </summary>
    private static void ReleaseBinding(Enemy enemy)
    {
        EnemyHealthBarView bar = enemy.HealthBarView;

        if (bar == null)
            return;

        enemy.HealthBarView = null;

        for (int i = activeCount - 1; i >= 0; i--)
        {
            if (activeBars[i] != bar)
                continue;

            RemoveActiveAt(i);
            break;
        }

        bar.Unbind();
        freeBars.Push(bar);
    }

    private static void RemoveAt(int index)
    {
        int last = count - 1;
        Enemy moved = enemies[last];

        enemies[last] = null;
        count = last;

        if (index == last)
            return;

        enemies[index] = moved;

        if (moved != null)
            moved.HealthBarRegistryIndex = index;
    }

    private static void RemoveActiveAt(int index)
    {
        int last = activeCount - 1;
        EnemyHealthBarView moved = activeBars[last];

        activeBars[last] = null;
        activeCount = last;

        if (index == last)
            return;

        activeBars[index] = moved;
    }

    private static void Grow(int capacity)
    {
        Enemy[] grown = new Enemy[capacity];

        for (int i = 0; i < count; i++)
            grown[i] = enemies[i];

        enemies = grown;
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
                "EnemyHealthBarCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));

        canvasObject.layer = UiLayer;

        // Канвас переживает сцену, иначе на каждом входе в бой
        // он пересоздавался бы вместе с полосами.
        Object.DontDestroyOnLoad(canvasObject);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;

        // Масштаб как у игрового HUD (1920x1080, match 0.5),
        // поэтому пиксели в настройках полосы совпадают с
        // пикселями остального интерфейса.
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
            new GameObject("EnemyHealthBarUpdater");

        updater =
            updaterObject.AddComponent<EnemyHealthBarUpdater>();

        Object.DontDestroyOnLoad(updaterObject);
    }

    internal static void HandleUpdaterAwake(
        EnemyHealthBarUpdater instance)
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
        EnemyHealthBarUpdater instance)
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
    /// поля, а статические поля обнуляются. Полосы в этом окне
    /// оказываются сиротами - они не в пуле, никто их не двигает и
    /// не гасит, но на экране они остаются.
    ///
    /// Один поиск на перезагрузку, не на кадр. Без него канвас
    /// плодился бы от релоада к релоаду, и редактор терял бы
    /// кадры на пустом UI.
    /// </summary>
    private static void DropStrayCanvas()
    {
        if (!Application.isPlaying)
            return;

        EnemyHealthBarView[] strays =
            Object.FindObjectsByType<EnemyHealthBarView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        if (strays.Length == 0)
            return;

        // Все полосы лежат под одним канвасом, поэтому сносится
        // он целиком вместе с ними.
        Transform root = strays[0].transform.root;

        if (root != null)
            Object.Destroy(root.gameObject);
    }
}
