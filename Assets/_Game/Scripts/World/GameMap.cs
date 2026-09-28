using UnityEngine;

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

    [Header("Structures")]
    [Tooltip("Материалы структур для WorldStructureGenerator. Пусто — структуры остаются в материалах своих префабов, либо в палитре ниже (для фолбэк-кубов).")]
    public Material[] structureMaterials;

    [Tooltip("Палитра фолбэк-кубов структур. Используется только когда structureMaterials пуст.")]
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