using System;
using UnityEngine;

/// <summary>Примитивы, из которых собираются объекты окружения.</summary>
public enum EnvironmentShape
{
    Cube,
    Sphere,
    Cylinder,
    Capsule,
    Quad
}

/// <summary>
/// Способ расстановки объекта окружения. Тематическое окружение
/// живёт у краёв арены, а центральная боевая зона занята общими
/// нейтральными блоками (WorldStructureGenerator).
/// </summary>
public enum EnvironmentObjectPlacement
{
    /// <summary>Кольцо вокруг центра (старое поведение) — для совместимости.</summary>
    RadialRing = 0,

    /// <summary>Рамка вдоль краёв квадратной арены (периметр).</summary>
    SquareFrame = 1,

    /// <summary>Углы арены: крупные акцентные объекты.</summary>
    Corner = 2
}

/// <summary>
/// Одна часть собираемого объекта: примитив с положением, поворотом
/// и масштабом относительно основания объекта (y = 0 — пол арены).
/// </summary>
[Serializable]
public class EnvironmentPart
{
    [Tooltip("Примитив части.")]
    public EnvironmentShape shape = EnvironmentShape.Cube;

    [Tooltip("Индекс цвета в палитре темы (palette).")]
    public int color;

    [Tooltip("Использовать светящийся материал темы (glowMaterial). Звёзды, кристаллы, планета.")]
    public bool glow;

    [Tooltip("Смещение относительно основания объекта. y = 0 — пол арены.")]
    public Vector3 position;

    [Tooltip("Поворот части в градусах.")]
    public Vector3 euler;

    [Tooltip("Размер части. У примитивов единица — 1×1×1 (цилиндр: диаметр 1, высота 1).")]
    public Vector3 scale = Vector3.one;
}

/// <summary>
/// Описание типа объекта окружения: из каких частей собран, сколько
/// экземпляров ставить и насколько далеко от центра арены.
/// </summary>
[Serializable]
public class EnvironmentObjectDef
{
    [Tooltip("Имя объекта (для отладки в иерархии).")]
    public string name = "Object";

    [Tooltip("Сколько экземпляров расставить.")]
    public int count = 1;

    [Tooltip("Способ расстановки: SquareFrame — периметр арены, Corner — крупные акценты в углах, RadialRing — старое кольцо вокруг центра.")]
    public EnvironmentObjectPlacement placement =
        EnvironmentObjectPlacement.SquareFrame;

    [Tooltip("Общий масштаб объекта (мин). Умножается на каждую часть.")]
    public float scaleMin = 1f;

    [Tooltip("Общий масштаб объекта (макс). Умножается на каждую часть.")]
    public float scaleMax = 1f;

    [Tooltip("Радиус занятого объектом места для расстановки. Препятствия расталкиваются по этой сумме.")]
    public float footprint = 0.5f;

    [Tooltip("Своё кольцо расстановки (мин), если 0 — используются поля темы.")]
    public float overrideMinRadius;

    [Tooltip("Своё кольцо расстановки (макс), если 0 — используются поля темы.")]
    public float overrideMaxRadius;

    [Tooltip("Ставить в фиксированную точку вместо случайной в кольце: море, планета и т.п.")]
    public bool fixedPlacement;

    [Tooltip("Смещение от центра арены (x/z) при fixedPlacement.")]
    public Vector2 fixedOffset;

    [Tooltip("Части, из которых собран объект.")]
    public EnvironmentPart[] parts;

    [Tooltip("Готовая 3D-модель (префаб) вместо сборки из примитивов. Если задана, parts игнорируются. Расстановка, случайный поворот, масштаб и статус препятствия работают так же, как у примитивных объектов.")]
    public GameObject model;
}

/// <summary>
/// Данные окружения одной карты: наборы объектов внутри арены
/// (препятствия с коллайдерами и мелкая декорация) и за её пределами
/// (фон без коллайдеров). Собирается процедурно EnvironmentBuilder
/// из примитивов — без префабов и текстур, поэтому в WebGL дёшево.
/// </summary>
[CreateAssetMenu(
    fileName = "Env_",
    menuName = "ArcadeSurvivor/Environment Theme",
    order = 601)]
public class EnvironmentTheme : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Название темы (Лес, Пляж, ...).")]
    public string themeName;

    [Tooltip("Зерно генерации: одна и та же карта всегда выглядит одинаково. 0 — случайное при каждом создании слоя.")]
    public int seed;

    [Tooltip("Палитра цветов объектов. Индекс ссылается из EnvironmentPart.color.")]
    public Color[] palette;

    [Tooltip("Светящийся материал для частей с glow = true. Создаётся редактором как ассет, чтобы шейдер гарантированно попал в билд.")]
    public Material glowMaterial;

    [Header("Inside Placement")]
    [Tooltip("Внутренний радиус кольца препятствий от центра. Центр арены остаётся свободным для боя.")]
    public float minCenterDistance = 11f;

    [Tooltip("Внешний радиус кольца препятствий. Держи внутри арены, не на стенах (±50).")]
    public float maxCenterDistance = 27f;

    [Tooltip("Минимальный зазор между препятствиями (в дополнение к сумме footprint).")]
    public float minObstacleSpacing = 1.2f;

    [Tooltip("Кольцо мелкой декорации внутри арены (без коллайдеров) — мин радиус.")]
    public float minDecorDistance = 12f;

    [Tooltip("Кольцо мелкой декорации внутри арены (без коллайдеров) — макс радиус.")]
    public float maxDecorDistance = 31f;

    [Header("Outside Placement")]
    [Tooltip("Кольцо фона за стенами арены (полуширина арены — 50) — мин радиус.")]
    public float minOutsideDistance = 52f;

    [Tooltip("Кольцо фона за стенами арены — макс радиус.")]
    public float maxOutsideDistance = 92f;

    [Tooltip("Зазор между объектами фона.")]
    public float minOutsideSpacing = 1.5f;

    [Header("Border Placement")]
    [Tooltip("Внутренняя полуширина тематической рамки (квадрат). Объекты SquareFrame стоят по периметру от этой дистанции до стен (±50). Оставь зазор к зоне нейтральных блоков (по умолчанию блоки до ±32).")]
    public float minBorderHalfSize = 34f;

    [Tooltip("Внешняя полуширина тематической рамки. Не заходи за стены арены (максимум 50).")]
    public float maxBorderHalfSize = 49f;

    [Tooltip("Угловая зона: внутренний радиус — от этой дистанции по осям ставятся крупные акцентные объекты (Corner).")]
    public float minCornerHalfSize = 46f;

    [Tooltip("Угловая зона: внешний радиус (не заходи за стены).")]
    public float maxCornerHalfSize = 49f;

    [Tooltip("Зазор между тематическими объектами рамки.")]
    public float minBorderSpacing = 1.5f;

    [Header("Objects")]
    [Tooltip("Препятствия внутри арены: с коллайдерами, блокируют игрока/врагов/пули, растворяются при заслоне камеры.")]
    public EnvironmentObjectDef[] insideObstacles;

    [Tooltip("Мелкая декорация внутри арены: без коллайдеров.")]
    public EnvironmentObjectDef[] insideDecor;

    [Tooltip("Фон за стенами арены: без коллайдеров.")]
    public EnvironmentObjectDef[] outsideObjects;
}