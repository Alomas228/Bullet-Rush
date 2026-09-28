using System;
using UnityEngine;

/// <summary>
/// Внешний вид HP-бара моба. Лежит прямо на префабе (поле
/// healthBar в Enemy), поэтому вид полосы задаётся руками на
/// каждом мобе отдельно: цвет, размер, отступ над мобом,
/// дистанция отрисовки. Правка одного префаба не трогает остальные.
///
/// Логики здесь нет намеренно: полосы создаёт, двигает и прячет
/// EnemyHealthBarSystem. Класс только хранит числа, чтобы их можно
/// было крутить в инспекторе.
/// </summary>
[Serializable]
public class EnemyHealthBarSettings
{
    [Header("Видимость")]
    [Tooltip("Рисовать ли полосу HP этому мобу. У босса своя полоса наверху экрана - её можно выключить.")]
    [SerializeField] private bool visible = true;

    [Header("Цвета")]
    [Tooltip("Заливка полосы - сама шкала здоровья.")]
    [SerializeField] private Color fillColor = new Color(0.86f, 0.11f, 0.11f, 1f);

    [Tooltip("Подложка - тёмный фон за шкалой. Alpha 0 убирает подложку совсем.")]
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.7f);

    [Header("Размер (пиксели канваса)")]
    [Tooltip("Ширина и высота полосы. Считается в пикселях опорного разрешения 1920x1080.")]
    [SerializeField] private Vector2 barSize = new Vector2(70f, 9f);

    [Tooltip("Отступ заливки внутрь подложки. Из него получается тонкая рамка вокруг шкалы.")]
    [SerializeField] private float fillPadding = 1.5f;

    [Header("Положение")]
    [Tooltip("Сдвиг полосы относительно моба в пикселях экрана. Y вверх - полоса над мобом.")]
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 46f);

    [Header("Дальность")]
    [Tooltip("Дальше этого расстояния до камеры полоса не рисуется. 0 - без ограничения.")]
    [SerializeField] private float maxDistance = 24f;

    /// <summary>
    /// Рисовать ли полосу этому мобу вообще.
    /// </summary>
    public bool Visible => visible;

    /// <summary>
    /// Цвет шкалы здоровья.
    /// </summary>
    public Color FillColor => fillColor;

    /// <summary>
    /// Цвет подложки за шкалой.
    /// </summary>
    public Color BackgroundColor => backgroundColor;

    /// <summary>
    /// Размер полосы. Ниже 4x1 пикселя полоса нечитаема,
    /// поэтому снизу подрезается, а не рисуется в ноль.
    /// </summary>
    public Vector2 BarSize => new Vector2(
        Mathf.Max(barSize.x, 4f),
        Mathf.Max(barSize.y, 1f));

    /// <summary>
    /// Внутренний отступ заливки. Отрицательное значение
    /// съело бы края шкалы, поэтому обрезается в ноль.
    /// </summary>
    public float FillPadding => Mathf.Max(fillPadding, 0f);

    /// <summary>
    /// Сдвиг полосы над мобом в пикселях экрана.
    /// </summary>
    public Vector2 ScreenOffset => screenOffset;

    /// <summary>
    /// Предельная дистанция до камеры. Ноль или меньше -
    /// рисовать независимо от расстояния.
    /// </summary>
    public float MaxDistance => maxDistance;
}
