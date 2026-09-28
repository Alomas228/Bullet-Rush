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

    public int CurrentIndex => currentIndex;

    public GameMap CurrentMap =>
        currentLayer != null ? currentLayer.Map : null;

    public bool IsTransitioning { get; private set; }

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

        currentIndex =
            Mathf.Clamp(
                target,
                0,
                maps.Length - 1
            );

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

        int step = ShortestStep(currentIndex, index, maps.Length);

        if (step == 0)
            return;

        SwitchToMap(maps[index], Math.Sign(step));
    }

    private void SwitchBy(int step)
    {
        if (maps == null ||
            maps.Length < 2)
        {
            return;
        }

        int next =
            (currentIndex + step) % maps.Length;

        if (next < 0)
            next += maps.Length;

        if (next == currentIndex)
            return;

        SwitchToMap(maps[next], Math.Sign(step));
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
            target == null ||
            target == CurrentMap)
        {
            return;
        }

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

    // =============================================================
    // СТРУКТУРЫ: тема текущей карты для WorldStructureGenerator
    // =============================================================

    private void PushThemeToGenerator()
    {
        if (worldGenerator == null)
            return;

        worldGenerator.ApplyTheme(CurrentMap);
    }
}