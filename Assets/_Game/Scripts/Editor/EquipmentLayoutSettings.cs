#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Размеры экрана «Снаряжение» — вынесены из билдера в ассет,
/// чтобы макет можно было подгонять, не трогая код.
///
/// Всё считается под референсный экран 1620x900 (см.
/// referenceWidth / referenceHeight). Правки применяются только
/// после повторного запуска Tools -> Bullet Rush -> Build Equipment UI:
/// билдер пересоздаёт панель целиком, поэтому новые размеры сразу
/// видно в иерархии и в Figma-экспорте.
///
/// Цвета живут отдельно, в EquipmentWireframeTheme.
/// </summary>
[CreateAssetMenu(
    fileName = "EquipmentLayoutSettings",
    menuName = "Arcade Survivor/Equipment Layout Settings"
)]
public class EquipmentLayoutSettings : ScriptableObject
{
    public const string AssetPath =
        "Assets/_Game/UI/EquipmentLayoutSettings.asset";

    // =====================================================
    // ЭКРАН
    // =====================================================

    [Header("Экран (справочно, для проверки влезания)")]
    [Tooltip("Ширина макета, на которую считаются размеры")]
    [Min(320f)]
    public float referenceWidth = 1620f;

    [Tooltip("Высота макета, на которую считаются размеры")]
    [Min(240f)]
    public float referenceHeight = 900f;

    [Header("Полосы экрана")]
    [Tooltip("Высота шапки: заголовок, монеты, уровень")]
    [Min(40f)]
    public float topBarHeight = 130f;

    [Tooltip("Высота полосы вкладок")]
    [Min(40f)]
    public float tabBarHeight = 100f;

    [Tooltip("Отступ от верха экрана до ряда карточек")]
    [Min(0f)]
    public float cardAreaTop = 246f;

    [Tooltip("Отступ от низа экрана до ряда карточек. "
        + "Здесь живут точки, счётчик страниц и кнопка «Назад»")]
    [Min(0f)]
    public float cardAreaBottom = 190f;

    // =====================================================
    // РЯД КАРТОЧЕК
    // =====================================================

    [Header("Ряд карточек")]
    [Tooltip("Сколько карточек помещается на одну страницу. "
        + "Меняет и разбивку на страницы, и число созданных слотов")]
    [Range(1, 6)]
    public int cardsPerPage = 4;

    [Tooltip("Ширина карточки")]
    [Min(120f)]
    public float cardWidth = 330f;

    [Tooltip("Высота карточки")]
    [Min(160f)]
    public float cardHeight = 450f;

    [Tooltip("Зазор между карточками")]
    [Min(0f)]
    public float cardGap = 20f;

    [Tooltip("Боковые отступы ряда. В них помещаются стрелки пагинации")]
    [Min(0f)]
    public float rowPadding = 120f;

    [Tooltip("Отступ рамки от края карточки")]
    [Min(0f)]
    public float frameInset = 3f;

    [Tooltip("Зазор между строками внутри карточки")]
    [Min(0f)]
    public float rowSpacing = 8f;

    [Header("Внутренние отступы карточки")]
    [Min(0f)]
    public float facePaddingLeft = 20f;

    [Min(0f)]
    public float facePaddingRight = 20f;

    [Min(0f)]
    public float facePaddingTop = 24f;

    [Min(0f)]
    public float facePaddingBottom = 20f;

    [Tooltip("Отступ акцентной полосы от краёв карточки сверху и снизу")]
    [Min(0f)]
    public float accentLineInset = 40f;

    // =====================================================
    // ВЫСОТЫ СТРОК КАРТОЧКИ
    // =====================================================

    [Header("Строки карточки")]
    [Tooltip("Блок под иконку: подложка, круг-заглушка, тип")]
    [Min(20f)]
    public float iconAreaHeight = 100f;

    [Tooltip("Диаметр круга-заглушки иконки")]
    [Min(8f)]
    public float iconDiscSize = 56f;

    [Tooltip("Высота строки с типом предмета под иконкой")]
    [Min(8f)]
    public float typeTextHeight = 24f;

    [Tooltip("Высота строки с названием")]
    [Min(8f)]
    public float nameHeight = 34f;

    [Tooltip("Высота строки с редкостью. У одежды и способностей "
        + "она выключается, поэтому высота освобождается под контент")]
    [Min(8f)]
    public float rarityHeight = 24f;

    [Tooltip("Высота блока с характеристиками")]
    [Min(8f)]
    public float statsHeight = 92f;

    [Tooltip("Высота плашки состояния")]
    [Min(8f)]
    public float stateHeight = 36f;

    [Tooltip("Высота строки действия с ценой")]
    [Min(8f)]
    public float actionHeight = 66f;

    // =====================================================
    // ПРОВЕРКА ВЛЕЗАНИЯ
    // =====================================================

    /// <summary>
    /// Ширина ряда карточек вместе с зазорами.
    /// </summary>
    public float RowWidth =>
        cardsPerPage * cardWidth + Mathf.Max(0, cardsPerPage - 1) * cardGap;

    /// <summary>
    /// Ширина, доступная ряду после боковых отступов.
    /// </summary>
    public float AvailableWidth => referenceWidth - 2f * rowPadding;

    /// <summary>
    /// Высота, доступная ряду между полосой вкладок и низом экрана.
    /// </summary>
    public float AvailableCardHeight =>
        referenceHeight - cardAreaTop - cardAreaBottom;

    /// <summary>
    /// Сколько высоты занимает содержимое карточки при текущих
    /// высотах строк. Должно помещаться в cardHeight.
    /// </summary>
    public float ContentHeight =>
        frameInset * 2f
        + facePaddingTop
        + facePaddingBottom
        + rowSpacing * 4f
        + iconAreaHeight
        + nameHeight
        + rarityHeight
        + statsHeight
        + stateHeight
        + actionHeight;

    public bool RowFits => RowWidth <= AvailableWidth;

    public bool CardFits => ContentHeight <= cardHeight;

    public bool BandFits =>
        cardAreaTop >= topBarHeight + tabBarHeight
        && AvailableCardHeight > 0f;

    /// <summary>
    /// Ищет ассет настроек, а если его нет — создаёт со значениями
    /// по умолчанию. Билдер вызывает это всегда, поэтому пункт меню
    /// работает без ручной подготовки.
    /// </summary>
    public static EquipmentLayoutSettings Load()
    {
        string[] guids =
            AssetDatabase.FindAssets($"t:{nameof(EquipmentLayoutSettings)}");

        if (guids.Length > 0)
        {
            EquipmentLayoutSettings existing =
                AssetDatabase.LoadAssetAtPath<EquipmentLayoutSettings>(
                    AssetDatabase.GUIDToAssetPath(guids[0])
                );

            if (existing != null)
                return existing;
        }

        EnsureFolder(Path.GetDirectoryName(AssetPath));

        var created = CreateInstance<EquipmentLayoutSettings>();

        AssetDatabase.CreateAsset(created, AssetPath);
        AssetDatabase.SaveAssets();

        return created;
    }

    /// <summary>
    /// Возвращает все размеры к исходным. Кнопка в инспекторе.
    /// </summary>
    public void ApplyDefaults()
    {
        referenceWidth = 1620f;
        referenceHeight = 900f;

        topBarHeight = 130f;
        tabBarHeight = 100f;
        cardAreaTop = 246f;
        cardAreaBottom = 190f;

        cardsPerPage = 4;
        cardWidth = 330f;
        cardHeight = 450f;
        cardGap = 20f;
        rowPadding = 120f;
        frameInset = 3f;
        rowSpacing = 8f;

        facePaddingLeft = 20f;
        facePaddingRight = 20f;
        facePaddingTop = 24f;
        facePaddingBottom = 20f;
        accentLineInset = 40f;

        iconAreaHeight = 100f;
        iconDiscSize = 56f;
        typeTextHeight = 24f;
        nameHeight = 34f;
        rarityHeight = 24f;
        statsHeight = 92f;
        stateHeight = 36f;
        actionHeight = 66f;
    }

    private void OnValidate()
    {
        // Карточка не должна стать тоньше своей рамки, а полоса
        // вкладок не должна наезжать на ряд карточек.
        if (frameInset * 2f >= cardWidth)
            frameInset = Mathf.Max(0f, cardWidth * 0.5f - 1f);

        if (cardAreaTop < topBarHeight + tabBarHeight)
            cardAreaTop = topBarHeight + tabBarHeight;
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            return;

        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}

#endif
