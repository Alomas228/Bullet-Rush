using UnityEngine;

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
    [Tooltip("Название карты для карточек выбора.")]
    public string mapName;

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