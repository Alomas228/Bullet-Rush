using UnityEngine;

/// <summary>
/// Архетип боевой геометрии карты. Один и тот же генератор блоков
/// по стилю кладёт их по-разному: это и делает карты разными по
/// задаче, а не только по цвету.
/// </summary>
public enum ArenaLayoutStyle
{
    /// <summary>Равномерный разброс одинаково распределённых блоков. Базовая арена.</summary>
    Scattered = 0,

    /// <summary>Почти пустое поле с низким укрытием. Длинные прострелы, видно всё.</summary>
    Open = 1,

    /// <summary>Редкие высокие колонны. Линию огня рвут на короткие отрезки.</summary>
    Pillars = 2,

    /// <summary>Длинные стены с проходами. Коридоры и контроль позиции.</summary>
    Walls = 3,

    /// <summary>Гроздья укрытий с широкими просветами между ними.</summary>
    Clusters = 4
}

/// <summary>
/// Профиль боевой геометрии карты: вид раскладки и её масштаб.
/// Нужен, чтобы карты отличались задачей, а не палитрой.
/// </summary>
[System.Serializable]
public class ArenaLayout
{
    [Tooltip("Архетип раскладки блоков: как геометрия ломает линию огня и пути отхода.")]
    public ArenaLayoutStyle style = ArenaLayoutStyle.Scattered;

    [Tooltip("Нижняя граница числа блоков-препятствий.")]
    [Range(0, 200)]
    public int minStructures = 25;

    [Tooltip("Верхняя граница числа блоков-препятствий.")]
    [Range(0, 200)]
    public int maxStructures = 40;

    [Tooltip("Минимальная ширина блока (до авторасштаба префаба).")]
    public float minWidth = 1.5f;

    [Tooltip("Максимальная ширина блока.")]
    public float maxWidth = 4f;

    [Tooltip("Минимальная высота блока.")]
    public float minHeight = 1f;

    [Tooltip("Максимальная высота блока. Слишком высокие блоки начинают закрывать обзор с камеры.")]
    public float maxHeight = 6f;

    [Tooltip("Минимальный зазор между блоками поверх их габаритов.")]
    public float minSpacing = 1f;

    [Tooltip("Полуширина квадрата расстановки. Меньше — бой идёт плотнее к центру.")]
    [Range(4f, 48f)]
    public float halfSize = 30f;

    [Tooltip("Свободный радиус в центре: игрок и волна не должны начинать в стене.")]
    [Range(0f, 40f)]
    public float centerClearRadius = 7f;
}

/// <summary>
/// Один ручной объект фиксированного дизайна карты: префаб и его
/// точное положение/поворот/масштаб относительно центра арены (0,0).
/// Позволяет автору расставить горы, здания и акценты вручную,
/// не полагаясь на процедурную генерацию.
/// </summary>
[System.Serializable]
public class FixedDecorItem
{
    [Tooltip("Префаб объекта (гора, здание, дерево...).")]
    public GameObject prefab;

    [Tooltip("Позиция относительно центра арены. Горы за пределами арены: |x| > 50 или |z| > 50.")]
    public Vector3 position;

    [Tooltip("Поворот в градусах.")]
    public Vector3 rotation;

    [Tooltip("Масштаб объекта.")]
    public Vector3 scale = Vector3.one;

    [Tooltip("Оставить коллайдеры префаба (ручные препятствия). Выключено — декор/фон без коллайдеров, чтобы не мешали окклюзии и пулям.")]
    public bool keepColliders;
}

/// <summary>
/// Описание одной карты (лес, пляж, горы, пустыня, космос и т.п.).
/// Никаких сцен и загрузок: карта — это только данные «обёртки» над
/// игровой ареной. Все механики общие и не зависят от карты.
/// </summary>
[CreateAssetMenu(
    fileName = "Map_",
    menuName = "ArcadeSurvivor/Map",
    order = 600)]
public class GameMap : ScriptableObject
{
    [Header("Identity")]
    [Tooltip(
        "Ключ перевода без префикса — например \"les\" для ключа " +
        "map.les. Пусто — используется поле «Название карты»."
    )]
    public string langKey;

    [Tooltip("Название карты для карточек выбора.")]
    public string mapName;

    /// <summary>Название карты на языке игрока.</summary>
    public string LocalizedName =>
        Lang.GetOr("map." + langKey, mapName);

    [Tooltip("Иконка для карточек выбора карты.")]
    public Sprite icon;

    [Header("Ground")]
    [Tooltip("Материал подложки арены. Пусто — нейтральный серый.")]
    public Material groundMaterial;

    [Tooltip("Опциональный префаб, который кладётся поверх подложки (текстуры, пятна песка, узоры и т.п.). Коллайдеры префаба отключаются автоматически.")]
    public GameObject groundDecorPrefab;

    [Tooltip("Окружение вокруг арены: деревья, скалы, пальмы, звёзды. Ложится как дочерний объект слоя карты и уезжает вместе с ним. Коллайдеры отключаются автоматически.")]
    public GameObject environmentPrefab;

    [Header("Environment")]
    [Tooltip("Данные процедурного окружения: препятствия с коллайдерами и декор внутри арены, фон за её пределами. Пусто — процедурное окружение не строится.")]
    public EnvironmentTheme environmentTheme;

    [Header("Fixed Design")]
    [Tooltip("Строить ли процедурное окружение темы на этой карте. Выключено — работает только ручной дизайн: fixedDecor + environmentPrefab/groundDecorPrefab.")]
    public bool buildThemeEnvironment = true;

    [Tooltip("Ручные объекты карты: горы за пределами арены, особые здания, акценты. Каждый пункт — префаб + точная позиция/поворот/масштаб относительно центра арены. Фиксированный дизайн карты, не зависит от генерации. Заполняется вручную или через ArcadeSurvivor → Bake Fixed Decor to Map.")]
    public FixedDecorItem[] fixedDecor;

    [Header("Arena")]
    [Tooltip("Зерно стабильной геометрии арены: одни и те же игровые блоки в одних и тех же местах при каждом запуске карты. Настройка: один seed = одна конфигурация блоков. Смена карты = новый seed = новая конфигурация.")]
    public int layoutSeed = 7007;

    [Tooltip("Боевая геометрия карты: вид раскладки блоков и её масштаб. Пусто — генератор берёт свои значения из инспектора и раскладывает блоки равномерным разбросом.")]
    public ArenaLayout arenaLayout;

    [Header("Structures")]
    [Tooltip("Материалы структур для WorldStructureGenerator. Пусто — структуры остаются в материалах своих префабов, либо в палитре ниже (для фолбэк-кубов).")]
    public Material[] structureMaterials;

    [Tooltip("Палитра фолбэк-кубов структур. Используется только когда structureMaterials пуст. Игровые препятствия нейтральны для всех карт: не подкрашивай их в цвета темы — для этого есть EnvironmentTheme.")]
    public Color[] structurePalette;

    [Header("Ambient")]
    [Tooltip("Цвет фона камеры (небо/космос) этой карты.")]
    public Color skyColor = new Color(0.192f, 0.302f, 0.475f);

    [Tooltip("Цвет глобального окружающего света. Держи нейтральным, чтобы не перекрашивал игрока и врагов.")]
    public Color ambientLightColor = new Color(0.3f, 0.3f, 0.3f);

    [Header("Fog")]
    [Tooltip("Включить линейный туман на этой карте.")]
    public bool fogEnabled;

    [Tooltip("Цвет тумана.")]
    public Color fogColor = new Color(0.5f, 0.5f, 0.5f);

    [Tooltip("Начало линейного тумана по дистанции от камеры.")]
    public float fogStartDistance = 35f;

    [Tooltip("Конец линейного тумана — после этой дистанции всё полностью в тумане.")]
    public float fogEndDistance = 75f;
}