using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Хранит текущую карту и меняет её горизонтальным слайдом:
/// старый слой уезжает в одну сторону, новый приезжает с другой.
/// Игрок и камера стоят на месте — мир «едет» под ними.
/// Вместе со слайдом кроссфейдится небо (фон камеры) и туман.
/// </summary>
public class EnvironmentController : MonoBehaviour
{
    /// <summary>
    /// Выбранная карта, переживающая перезагрузку сцены в рамках
    /// одной сессии. -1 — ещё не выбирали.
    /// </summary>
    public static int SelectedMapIndex = -1;

    /// <summary>Событие после завершения перехода на карту.</summary>
    public event Action<GameMap> OnMapChanged;
    // The map is committed while the menu transition is opaque; presentation
    // systems can prepare the new scene before it is revealed.
    public event Action<GameMap> OnMenuMapCommitted;

    [Header("Maps")]
    [Tooltip("Доступные карты в порядке карусели.")]
    [SerializeField] private GameMap[] maps;

    [Tooltip("Карта при старте, если SelectedMapIndex не задан.")]
    [SerializeField] private int startMapIndex;

    [Header("Transition")]
    [Tooltip("Длительность слайда одной карты.")]
    [SerializeField] private float slideDuration = 1.1f;

    [Tooltip("Дистанция проезда карты. Эффективно не меньше groundSize: если меньше, уходящая карта не успеет выехать и наедет на приезжающую. При travel >= groundSize плиты всё время впритык, без наложений и дыр.")]
    [SerializeField] private float slideTravel = 100f;

    [Tooltip("Ширина подложки карты (диаметр). Покрывает арену (100) и путь слайда.")]
    [SerializeField] private float groundSize = 100f;

    [Tooltip("Небольшой запас высоты приезжающей карты на время слайда. Страховка от z-fighting на стыке при неточных настройках. 0 — плиты строго в одной плоскости.")]
    [SerializeField] private float overlapClearance = 0.005f;

    /// <summary>
    /// Реальное расстояние проезда: не меньше ширины карты, чтобы
    /// уходящая карта гарантированно выехала и не наложилась на
    /// приезжающую в финале слайда.
    /// </summary>
    private float EffectiveSlideTravel
    {
        get
        {
            return Mathf.Max(
                slideTravel,
                groundSize
            );
        }
    }

    [Tooltip("Камера, чей фон (небо) переключается. Пусто — Camera.main.")]
    [SerializeField] private Camera environmentCamera;

    private EnvironmentLayer currentLayer;

    private Camera cam;
    private WorldStructureGenerator worldGenerator;
    private int currentIndex;
    private bool initialized;
    private GameMap runtimePreviousMap;
    private int runtimePreviousIndex = -1;
    private bool restoringRuntimeMap;
    private GameMap preparedMenuMap;
    private MenuMapTransitionOverlay menuTransitionOverlay;
    private GameStateManager stateManager;
    private Action pendingSessionPreparation;
    private bool menuSessionRefreshRequested;
    private int queuedMenuMapIndex = -1;
    private Light environmentSun;
    private Color originalSunColor;
    private float originalSunIntensity;

    public int CurrentIndex => currentIndex;

    public GameMap CurrentMap =>
        currentLayer != null ? currentLayer.Map : null;

    public bool IsTransitioning { get; private set; }
    public bool IsRestoringRuntimeMap => restoringRuntimeMap;
    public bool IsSessionRefreshPending => menuSessionRefreshRequested;
    public int RequestedMapIndex => queuedMenuMapIndex >= 0 ? queuedMenuMapIndex : currentIndex;
    public bool IsMenuPreparationPending => IsTransitioning || restoringRuntimeMap ||
        menuSessionRefreshRequested || queuedMenuMapIndex >= 0 ||
        (worldGenerator != null && worldGenerator.IsGenerating);

    public bool IsMenuWorldPrepared(GameMap map) => map != null && map == preparedMenuMap &&
        map == CurrentMap && worldGenerator != null && !worldGenerator.IsGenerating;

    public int MapsCount =>
        maps != null ? maps.Length : 0;

    public GameMap GetMap(int index)
    {
        if (maps == null ||
            index < 0 ||
            index >= maps.Length)
        {
            return null;
        }

        return maps[index];
    }

    /// <summary>Открыта ли карта по индексу для текущего уровня игрока.</summary>
    public bool IsMapUnlocked(int index)
    {
        return MapProgression.IsUnlocked(GetMap(index));
    }

    /// <summary>
    /// Ближайший открытый индекс к предпочтительному: сам предпочтительный,
    /// иначе ближайший открытый снизу, затем сверху. Лес (уровень 1) открыт
    /// всегда, поэтому метод никогда не вернёт закрытую карту.
    /// </summary>
    private int FindUnlockedIndex(int preferred)
    {
        if (maps == null || maps.Length == 0)
            return 0;

        preferred = Mathf.Clamp(preferred, 0, maps.Length - 1);

        if (IsMapUnlocked(preferred))
            return preferred;

        for (int i = preferred - 1; i >= 0; i--)
        {
            if (IsMapUnlocked(i))
                return i;
        }

        for (int i = preferred + 1; i < maps.Length; i++)
        {
            if (IsMapUnlocked(i))
                return i;
        }

        return preferred;
    }

    private void Awake()
    {
        cam =
            environmentCamera != null
                ? environmentCamera
                : Camera.main;

        worldGenerator =
            FindAnyObjectByType<WorldStructureGenerator>();

        if (maps == null ||
            maps.Length == 0)
        {
            Debug.LogWarning(
                "[EnvironmentController] Список карт пуст. " +
                "Создай GameMap-ассеты и назначь их в инспекторе.",
                this
            );

            return;
        }

        int target =
            SelectedMapIndex >= 0
                ? SelectedMapIndex
                : startMapIndex;

        // Нельзя стартовать на закрытой карте: если сохранённый выбор
        // требует ещё не достигнутого уровня — откатываемся к ближайшей
        // доступной локации.
        currentIndex =
            FindUnlockedIndex(target);

        SelectedMapIndex = currentIndex;

        currentLayer =
            EnvironmentLayer.Create(
                maps[currentIndex],
                "Environment_Active",
                groundSize
            );

        currentLayer.transform.SetParent(transform, false);
        currentLayer.transform.localPosition = Vector3.zero;

        ApplyEnvironment(currentLayer.Map);

        PushThemeToGenerator();

        initialized = true;
    }

    private void Start()
    {
        stateManager = GameStateManager.Instance;
        if (stateManager != null) stateManager.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDestroy()
    {
        if (stateManager != null) stateManager.OnGameStateChanged -= HandleGameStateChanged;
        if (menuTransitionOverlay != null) Destroy(menuTransitionOverlay.gameObject);
        if (environmentSun != null)
        {
            environmentSun.color = originalSunColor;
            environmentSun.intensity = originalSunIntensity;
        }
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state != GameState.Menu)
        {
            preparedMenuMap = null;
            queuedMenuMapIndex = -1;
        }
    }

    // Keep the UI alive on session return. A map change already requested by the
    // player can reuse the same covered rebuild instead of rebuilding twice.
    public void RequestMenuSessionRefresh(Action preparation)
    {
        pendingSessionPreparation = preparation;
        menuSessionRefreshRequested = true;
        preparedMenuMap = null;
    }

    public bool RequestMenuMap(int index)
    {
        if (!initialized || GameModeManager.IsPvp || GetMap(index) == null || !IsMapUnlocked(index)) return false;
        queuedMenuMapIndex = index;
        return true;
    }

    private void Update()
    {
        if (!initialized || IsTransitioning || restoringRuntimeMap ||
            GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Menu) return;

        if (queuedMenuMapIndex >= 0)
        {
            int index = queuedMenuMapIndex;
            queuedMenuMapIndex = -1;
            if (!GameModeManager.IsPvp && IsMapUnlocked(index) && GetMap(index) != CurrentMap)
            {
                SwitchTo(index);
                return;
            }
        }

        if (menuSessionRefreshRequested && CurrentMap != null)
        {
            StartCoroutine(MenuTransitionRoutine(CurrentMap, 1, false));
        }
    }

    /// <summary>Следующая карта по кругу. Входит справа, старая уходит влево.</summary>
    public void SwitchToNext()
    {
        SwitchBy(1);
    }

    /// <summary>Предыдущая карта по кругу. Входит слева, старая уходит вправо.</summary>
    public void SwitchToPrevious()
    {
        SwitchBy(-1);
    }

    /// <summary>Переход на конкретную карту по индексу. Кратчайшая сторона.</summary>
    public void SwitchTo(int index)
    {
        if (maps == null ||
            maps.Length == 0 ||
            index < 0 ||
            index >= maps.Length)
        {
            return;
        }

        // Закрытая карта недоступна: молча игнорируем выбор.
        if (!IsMapUnlocked(index))
            return;

        int step = ShortestStep(currentIndex, index, maps.Length);

        if (step == 0)
            return;

        SwitchToMap(maps[index], Math.Sign(step));
    }

    public bool SwitchToRuntimeMap(GameMap map)
    {
        if (!initialized || IsTransitioning || restoringRuntimeMap || map == null)
            return false;

        runtimePreviousMap = CurrentMap;
        runtimePreviousIndex = currentIndex;
        EnterMap(map, 1);
        return true;
    }

    public bool RestorePreviousMap()
    {
        if (runtimePreviousMap == null)
            return restoringRuntimeMap;

        if (restoringRuntimeMap)
            return true;

        restoringRuntimeMap = true;
        StartCoroutine(RestorePreviousMapRoutine());
        return true;
    }

    private IEnumerator RestorePreviousMapRoutine()
    {
        while (IsTransitioning)
            yield return null;

        BeginRestorePreviousMap();

        while (IsTransitioning)
            yield return null;

        restoringRuntimeMap = false;
    }

    private void BeginRestorePreviousMap()
    {
        GameMap previous = runtimePreviousMap;
        int previousIndex = runtimePreviousIndex;
        runtimePreviousMap = null;
        runtimePreviousIndex = -1;
        currentIndex = previousIndex;
        SelectedMapIndex = previousIndex;
        EnterMap(previous, -1);
    }

    private void SwitchBy(int step)
    {
        if (maps == null ||
            maps.Length < 2)
        {
            return;
        }

        if (step == 0)
            return;

        int direction = Math.Sign(step);
        int next = currentIndex;

        // Прыгаем через закрытые карты: листание не должно приводить
        // на локацию, которая ещё не открыта.
        for (int i = 0; i < maps.Length - 1; i++)
        {
            next = (next + direction) % maps.Length;

            if (next < 0)
                next += maps.Length;

            if (next == currentIndex)
                return;

            if (IsMapUnlocked(next))
            {
                SwitchToMap(maps[next], direction);
                return;
            }
        }
    }

    /// <summary>
    /// Кратчайший шаг по кольцу от from к to: -1, 0 или +1.
    /// </summary>
    private static int ShortestStep(
        int from,
        int to,
        int count)
    {
        int forward = ((to - from) % count + count) % count;
        int backward = count - forward;

        if (forward == 0)
            return 0;

        return forward <= backward ? 1 : -1;
    }

    private void SwitchToMap(
        GameMap target,
        int enterSide)
    {
        if (!initialized ||
            IsTransitioning ||
            restoringRuntimeMap ||
            target == null ||
            target == CurrentMap)
        {
            return;
        }

        if (!MapProgression.IsUnlocked(target))
            return;

        int targetIndex =
            Array.IndexOf(maps, target);

        if (targetIndex < 0)
            return;

        currentIndex = targetIndex;
        SelectedMapIndex = targetIndex;

        EnterMap(
            target,
            enterSide
        );
    }

    /// <summary>
    /// Подготавливает приезжающий слой и запускает слайд.
    /// enterSide = +1 — карта въезжает справа, -1 — слева.
    /// </summary>
    private void EnterMap(
        GameMap target,
        int enterSide)
    {
        preparedMenuMap = null;
        if (ArcadeMenuSkin.CampMenuLayoutEnabled && GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Menu)
        {
            StartCoroutine(MenuTransitionRoutine(target, enterSide));
            return;
        }

        EnvironmentLayer incoming =
            EnvironmentLayer.Create(
                target,
                "Environment_Incoming",
                groundSize
            );

        incoming.transform.SetParent(transform, false);
        incoming.transform.localPosition =
            new Vector3(
                enterSide * EffectiveSlideTravel,
                0f,
                0f
            );

        StartCoroutine(
            SlideRoutine(
                currentLayer,
                incoming,
                enterSide
            )
        );
    }

    private IEnumerator MenuTransitionRoutine(GameMap target, int enterSide, bool replaceEnvironment = true)
    {
        IsTransitioning = true;
        try
        {
            menuTransitionOverlay = MenuMapTransitionOverlay.Create(target, enterSide);
            float totalDuration = Mathf.Clamp(slideDuration, .85f, 1.5f);
            yield return menuTransitionOverlay.Cover(totalDuration * .42f);
            float coveredAt = Time.unscaledTime;

            // Render at least one fully covered frame before creating/destroying
            // objects. No old terrain, props or half-grown structures can leak through.
            yield return null;

            if (replaceEnvironment && currentLayer != null)
            {
                currentLayer.gameObject.SetActive(false);
                Destroy(currentLayer.gameObject);
            }
            if (worldGenerator == null) worldGenerator = FindAnyObjectByType<WorldStructureGenerator>();
            if (worldGenerator != null) worldGenerator.Clear();

            if (replaceEnvironment || currentLayer == null)
            {
                currentLayer = EnvironmentLayer.Create(target, "Environment_Active", groundSize);
                currentLayer.transform.SetParent(transform, false);
                currentLayer.transform.localPosition = Vector3.zero;
            }
            currentIndex = Array.IndexOf(maps, target);
            ApplyEnvironment(target);
            PushThemeToGenerator();

            if (menuSessionRefreshRequested)
            {
                Action prepare = pendingSessionPreparation;
                pendingSessionPreparation = null;
                menuSessionRefreshRequested = false;
                prepare?.Invoke();
            }

            // Camp scenery is rebuilt underneath the opaque curtain, not after
            // OnMapChanged. Existing map-changed listeners still run at the end.
            OnMenuMapCommitted?.Invoke(target);
            yield return null;

            if (worldGenerator != null)
            {
                worldGenerator.RebuildForMenu(target.layoutSeed);
                while (worldGenerator != null && worldGenerator.IsGenerating) yield return null;
                if (worldGenerator != null) preparedMenuMap = target;
            }

            // The scene and all renderers are ready before the reveal begins.
            yield return null;
            while (Time.unscaledTime - coveredAt < .16f) yield return null;
            yield return menuTransitionOverlay.Reveal(totalDuration * .43f);
        }
        finally
        {
            if (menuTransitionOverlay != null)
            {
                menuTransitionOverlay.gameObject.SetActive(false);
                Destroy(menuTransitionOverlay.gameObject);
            }
            menuTransitionOverlay = null;
            IsTransitioning = false;
        }

        OnMapChanged?.Invoke(target);
    }

    private IEnumerator SlideRoutine(
        EnvironmentLayer outgoingRoot,
        EnvironmentLayer incomingRoot,
        int enterSide)
    {
        IsTransitioning = true;

        EnvState begin = Capture(outgoingRoot.Map);
        EnvState end = Capture(incomingRoot.Map);

        float duration =
            Mathf.Max(slideDuration, 0.05f);

        float timer = 0f;

        Vector3 outgoingTarget =
            new Vector3(
                -enterSide * EffectiveSlideTravel,
                0f,
                0f
            );

        Vector3 incomingStart =
            new Vector3(
                enterSide * EffectiveSlideTravel,
                overlapClearance,
                0f
            );

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / duration
                );

            // Плавный выезд: быстрый старт, мягкое торможение.
            float ease =
                1f - Mathf.Pow(1f - t, 3f);

            outgoingRoot.transform.localPosition =
                Vector3.Lerp(
                    Vector3.zero,
                    outgoingTarget,
                    ease
                );

            incomingRoot.transform.localPosition =
                Vector3.Lerp(
                    incomingStart,
                    Vector3.zero,
                    ease
                );

            LerpEnvironment(begin, end, ease);

            yield return null;
        }

        Destroy(outgoingRoot.gameObject);

        currentLayer = incomingRoot;

        currentIndex =
            Array.IndexOf(
                maps,
                incomingRoot.Map
            );

        ApplyEnvironment(incomingRoot.Map);

        PushThemeToGenerator();

        IsTransitioning = false;

        OnMapChanged?.Invoke(incomingRoot.Map);
    }

    // =============================================================
    // ОКРУЖЕНИЕ: небо камеры, свет, туман
    // =============================================================

    private struct EnvState
    {
        public Color backgroundColor;
        public Color ambientLight;
        public Color sunTint;
        public bool fogEnabled;
        public Color fogColor;
        public float fogStart;
        public float fogEnd;
    }

    private static EnvState Capture(GameMap map)
    {
        EnvState state = default;

        if (map == null)
            return state;

        state.backgroundColor = map.skyColor;
        state.ambientLight = map.ambientLightColor;
        state.sunTint = map.langKey switch
        {
            "gory" => new Color(.83f,.94f,1f),
            "gorod" => new Color(.92f,.96f,1f),
            _ => new Color(1f,.92f,.78f)
        };
        state.fogEnabled = map.fogEnabled;
        state.fogColor = map.fogColor;
        state.fogStart = map.fogStartDistance;
        state.fogEnd = map.fogEndDistance;

        return state;
    }

    private void ApplyEnvironment(GameMap map)
    {
        ApplyEnvironmentState(Capture(map));
    }

    private void ApplyEnvironmentState(in EnvState state)
    {
        if (cam != null)
            cam.backgroundColor = state.backgroundColor;

        RenderSettings.ambientLight = state.ambientLight;
        ApplySunPalette(state.sunTint);

        RenderSettings.fog = state.fogEnabled;
        RenderSettings.fogColor = state.fogColor;

        if (state.fogEnabled)
        {
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = state.fogStart;
            RenderSettings.fogEndDistance = state.fogEnd;
        }
    }

    private void LerpEnvironment(
        in EnvState begin,
        in EnvState end,
        float t)
    {
        if (cam != null)
        {
            cam.backgroundColor =
                Color.Lerp(
                    begin.backgroundColor,
                    end.backgroundColor,
                    t
                );
        }

        RenderSettings.ambientLight =
            Color.Lerp(
                begin.ambientLight,
                end.ambientLight,
                t
            );
        ApplySunPalette(Color.Lerp(begin.sunTint,end.sunTint,t));

        // Пока хотя бы одна из карт использует туман — он включён,
        // чтобы переход плавал, а не дёргался.
        RenderSettings.fog = begin.fogEnabled || end.fogEnabled;

        if (RenderSettings.fog)
        {
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor =
                Color.Lerp(
                    begin.fogColor,
                    end.fogColor,
                    t
                );

            RenderSettings.fogStartDistance =
                Mathf.Lerp(
                    begin.fogStart,
                    end.fogStart,
                    t
                );

            RenderSettings.fogEndDistance =
                Mathf.Lerp(
                    begin.fogEnd,
                    end.fogEnd,
                    t
                );
        }
    }

    // A map's light palette belongs to the environment, not the menu props.
    // Keep the same color and exposure when entering a run.
    private void ApplySunPalette(Color tint)
    {
        if (environmentSun == null)
        {
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light.enabled &&
                    (environmentSun == null || light.intensity > environmentSun.intensity))
                    environmentSun = light;
            if (environmentSun == null) return;
            originalSunColor = environmentSun.color;
            originalSunIntensity = environmentSun.intensity;
        }
        environmentSun.color = Color.Lerp(originalSunColor,tint,.66f);
        environmentSun.intensity = Mathf.Clamp(originalSunIntensity*1.12f,.7f,1.8f);
    }

    // =============================================================
    // СТРУКТУРЫ: тема текущей карты для WorldStructureGenerator
    // =============================================================

    private void PushThemeToGenerator()
    {
        ArenaBoundaryVisuals.ApplyTheme(CurrentMap);
        if (worldGenerator == null)
            return;

        worldGenerator.ApplyTheme(CurrentMap);
    }
}
