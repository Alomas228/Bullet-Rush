using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Карточка выбора карты на панели «Карты».
///
/// Композиция — мини-версия карты: фон = цвет неба (фона камеры),
/// снизу полоса земли (цвет подложки), по центру иконка/диск,
/// под ним название. Выбранная карта подсвечивается чипом
/// «ВЫБРАНО» и тёмной рамкой.
///
/// Закрытая карта (уровень игрока ниже GameMap.unlockLevel)
/// затеняется оверлеем с требованием уровня и не нажимается.
///
/// Данные не лежат в полях: их передаёт MapSelectionUI через
/// <see cref="Bind"/>. Клики раздаёт MapSelectionUI.
/// </summary>
public class MapCardView : MonoBehaviour
{
    [Header("Frame")]
    [SerializeField] private Image frameImage;
    [SerializeField] private Image backgroundImage;

    [Header("World preview")]
    [SerializeField] private Image landImage;
    [SerializeField] private Image iconDisc;
    [SerializeField] private Image iconImage;

    [Header("Text")]
    [SerializeField] private TMP_Text nameText;

    [Header("Mechanic")]
    [Tooltip("Подпись боевой механики карты. Пусто — создаётся в рантайме.")]
    [SerializeField] private TMP_Text mechanicText;

    [Tooltip("Короткое описание стиля боя карты. Пусто — создаётся в рантайме.")]
    [SerializeField] private TMP_Text descriptionText;

    [Header("Progression Info")]
    [Tooltip("Чип уровня, с которого открывается карта. Пусто — создаётся в рантайме.")]
    [SerializeField] private TMP_Text levelChipText;

    [Tooltip("Корень чипа уровня (фон + подпись). Пусто — создаётся в рантайме.")]
    [SerializeField] private GameObject levelChipRoot;

    [Tooltip("Чип личного рекорда (лучшая волна) по этой карте. Пусто — создаётся в рантайме.")]
    [SerializeField] private TMP_Text recordText;

    [Tooltip("Корень чипа рекорда (фон + подпись). Пусто — создаётся в рантайме.")]
    [SerializeField] private GameObject recordRoot;

    [Header("Lock")]
    [Tooltip("Оверлей, затеняющий закрытую карту. Пусто — создаётся в рантайме.")]
    [SerializeField] private Image lockOverlay;

    [Tooltip("Текст «откроется на ур. N». Пусто — создаётся в рантайме.")]
    [SerializeField] private TMP_Text lockText;

    [Header("State")]
    [Tooltip("Чип «ВЫБРАНО». Показывается/прячется целиком.")]
    [SerializeField] private RectTransform selectedChip;

    [Tooltip("Кнопка на всю карточку.")]
    [SerializeField] private Button selectButton;

    [Tooltip("Ставится в true, когда на карточку повешен обработчик.")]
    [SerializeField] private bool wired;

    private const float SelectedScale = 1.03f;

    private static readonly Color LockVeil =
        new Color(0.05f, 0.07f, 0.11f, 0.66f);

    private bool decorBuilt;
    private Color mechanicColor = Color.white;

    public Button SelectButton => selectButton;

    public bool Wired => wired;

    /// <summary>Открыта ли карта для текущего уровня игрока.</summary>
    public bool Unlocked { get; private set; } = true;

    /// <summary>Уровень, с которого открыта карта.</summary>
    public int RequiredLevel { get; private set; } = 1;

    public void MarkWired()
    {
        wired = true;
    }

    /// <summary>
    /// Заполняет карточку данными карты без привязки к рекорду.
    /// </summary>
    public void Bind(GameMap map)
    {
        Bind(map, -1);
    }

    /// <summary>
    /// Заполняет карточку данными карты. Иконку берёт из GameMap,
    /// а если её нет — рисует цветной диск цвета подложки.
    /// mapIndex — позиция карты в карусели EnvironmentController,
    /// по ней читается личный рекорд (лучшая волна). -1 — без рекорда.
    /// </summary>
    public void Bind(GameMap map, int mapIndex)
    {
        if (map == null)
            return;

        EnsureDecor();

        Color ground = GroundColor(map);
        Color sky = map.skyColor;

        if (backgroundImage != null)
            backgroundImage.color = sky;

        if (landImage != null)
            landImage.color = ground;

        Color discColor =
            Color.Lerp(ground, Color.white, 0.35f);

        if (iconDisc != null)
            iconDisc.color = discColor;

        if (iconImage != null)
        {
            bool hasIcon = map.icon != null;

            iconImage.gameObject.SetActive(hasIcon);

            if (hasIcon)
            {
                iconImage.sprite = map.icon;
                iconImage.color = Color.white;
            }
        }

        // На тёмной земле (космос) — светлый текст, иначе тёмный.
        float luminance =
            ground.r * 0.299f +
            ground.g * 0.587f +
            ground.b * 0.114f;

        Color lightText = Color.white;
        Color darkText = MapSelectionTheme.TextPrimary;

        if (nameText != null)
        {
            nameText.text = string.IsNullOrEmpty(map.LocalizedName)
                ? map.name
                : map.LocalizedName;

            nameText.color = luminance < 0.45f
                ? lightText
                : darkText;
        }

        if (mechanicText != null)
        {
            string mechanicName = map.LocalizedMechanicName;

            bool hasMechanic =
                !string.IsNullOrEmpty(mechanicName);

            mechanicText.gameObject.SetActive(hasMechanic);

            if (hasMechanic)
            {
                mechanicText.text = mechanicName;

                Color baseColor = luminance < 0.45f
                    ? lightText
                    : darkText;

                mechanicColor = new Color(
                    baseColor.r,
                    baseColor.g,
                    baseColor.b,
                    0.82f
                );

                mechanicText.color = mechanicColor;
            }
        }

        // Описание стиля боя висит на фоне «неба», а не на земле,
        // поэтому контраст считаем по цвету неба.
        if (descriptionText != null)
        {
            string description =
                MapMechanicText.Description(map.mechanic);

            bool hasDescription =
                !string.IsNullOrEmpty(description);

            descriptionText.gameObject.SetActive(hasDescription);

            if (hasDescription)
            {
                float skyLuminance =
                    sky.r * 0.299f +
                    sky.g * 0.587f +
                    sky.b * 0.114f;

                Color baseColor = skyLuminance < 0.45f
                    ? lightText
                    : darkText;

                descriptionText.text = description;
                descriptionText.color = new Color(
                    baseColor.r,
                    baseColor.g,
                    baseColor.b,
                    0.85f
                );
            }
        }

        RequiredLevel = MapProgression.RequiredLevel(map);

        if (levelChipText != null && levelChipRoot != null)
        {
            levelChipRoot.SetActive(true);
            levelChipText.text =
                Lang.Get("maps.level_chip", RequiredLevel);
        }

        ApplyRecord(mapIndex);

        ApplyLock(MapProgression.IsUnlocked(map));

        SetSelected(false);
    }

    /// <summary>
    /// Чип личного рекорда: лучшая волна, до которой игрок дошёл
    /// на этой карте. Нет рекорда — чип скрыт, чтобы не занимать
    /// карточку пустой подписью.
    /// </summary>
    private void ApplyRecord(int mapIndex)
    {
        if (recordRoot == null || recordText == null)
            return;

        int best = mapIndex >= 0
            ? PersonalBestRecord.BestWaveFor(mapIndex)
            : 0;

        bool hasRecord = best > 0;

        recordRoot.SetActive(hasRecord);

        if (hasRecord)
            recordText.text = Lang.Get("maps.record", best);
    }

    public void SetSelected(bool value)
    {
        transform.localScale =
            Vector3.one * (value ? SelectedScale : 1f);

        if (frameImage != null)
        {
            frameImage.color = value
                ? MapSelectionTheme.FrameSelected
                : MapSelectionTheme.FrameIdle;
        }

        if (selectedChip != null &&
            selectedChip.gameObject.activeSelf != value)
        {
            selectedChip.gameObject.SetActive(value);
        }
    }

    /// <summary>
    /// Прячет или показывает замок. Закрытая карта остаётся видимой
    /// (игрок понимает, что его ждёт), но затемняется и не нажимается.
    /// </summary>
    private void ApplyLock(bool unlocked)
    {
        Unlocked = unlocked;

        if (lockOverlay != null)
            lockOverlay.gameObject.SetActive(!unlocked);

        if (lockText != null)
        {
            lockText.gameObject.SetActive(!unlocked);

            if (!unlocked)
            {
                lockText.text =
                    Lang.Get("maps.locked_level", RequiredLevel);
            }
        }

        if (selectButton != null)
            selectButton.interactable = unlocked;
    }

    // =========================================================
    // RUNTIME DECOR (замок, подпись механики)
    // =========================================================

    private void EnsureDecor()
    {
        if (decorBuilt)
            return;

        decorBuilt = true;

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;

        // Оверлей мог быть создан билдером (тогда его порядок уже
        // задан в сцене и трогать его не нужно).
        bool createdOverlay = false;

        if (mechanicText == null)
        {
            mechanicText = CreateLabel(
                "MechanicText",
                new Vector2(0.5f, 0f),
                new Vector2(0f, 100f),
                new Vector2(280f, 30f),
                18f
            );

            if (font != null)
                mechanicText.font = font;
        }

        if (lockOverlay == null)
        {
            GameObject overlay = new GameObject(
                "LockOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            RectTransform rt = overlay.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 6f);
            rt.offsetMax = new Vector2(-6f, -6f);
            rt.SetAsLastSibling();

            lockOverlay = overlay.GetComponent<Image>();
            lockOverlay.color = LockVeil;
            lockOverlay.raycastTarget = false;

            createdOverlay = true;
        }

        if (lockText == null)
        {
            lockText = CreateLabel(
                "LockText",
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(260f, 100f),
                28f
            );

            lockText.alignment = TextAlignmentOptions.Center;
            lockText.fontStyle = FontStyles.Bold;
            lockText.color = Color.white;

            lockText.transform.SetAsLastSibling();

            if (font != null)
                lockText.font = font;
        }

        if (descriptionText == null)
        {
            descriptionText = CreateLabel(
                "DescriptionText",
                new Vector2(0.5f, 0f),
                new Vector2(0f, 134f),
                new Vector2(272f, 38f),
                13f
            );

            descriptionText.fontStyle = FontStyles.Normal;

            if (font != null)
                descriptionText.font = font;
        }

        if (levelChipRoot == null)
        {
            levelChipRoot = CreateChip(
                "LevelChip",
                new Vector2(1f, 1f),
                new Vector2(-14f, -14f),
                new Vector2(110f, 36f),
                out levelChipText
            );
        }

        if (recordRoot == null)
        {
            recordRoot = CreateChip(
                "RecordChip",
                new Vector2(1f, 1f),
                new Vector2(-14f, -54f),
                new Vector2(110f, 34f),
                out recordText
            );

            recordRoot.SetActive(false);
        }

        // Описание — часть «тела» карты: на закрытой карте оно должно
        // так же уходить под затеняющий оверлей, как имя и механика.
        // Чипы уровня и рекорда, наоборот, остаются поверх оверлея.
        // Переставляем только если оверлей создали мы сами: у оверлея
        // из билдера порядок уже выставлен в сцене.
        if (createdOverlay &&
            descriptionText != null &&
            lockOverlay != null)
        {
            descriptionText.transform.SetSiblingIndex(
                lockOverlay.transform.GetSiblingIndex()
            );
        }
    }

    // Плашка со своей тёмной подложкой: уровень разблокировки и
    // рекорд должны читаться на любом цвете неба карты.
    private GameObject CreateChip(
        string objectName,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        out TMP_Text label)
    {
        GameObject root = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        rt.SetAsLastSibling();

        Image background = root.GetComponent<Image>();
        background.color = new Color(0.05f, 0.07f, 0.11f, 0.72f);
        background.raycastTarget = false;

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform)
        );

        labelObject.transform.SetParent(root.transform, false);

        RectTransform labelRect =
            labelObject.GetComponent<RectTransform>();

        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(4f, 0f);
        labelRect.offsetMax = new Vector2(-4f, 0f);

        TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();

        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 15f;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        label = text;

        return root;
    }

    private TMP_Text CreateLabel(
        string objectName,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        float fontSize)
    {
        GameObject host = new GameObject(
            objectName,
            typeof(RectTransform)
        );

        RectTransform rt = host.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        TextMeshProUGUI text = host.AddComponent<TextMeshProUGUI>();

        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        return text;
    }

    /// <summary>
    /// Представительный цвет карты для полосы земли и диска:
    /// берётся из _BaseColor материала подложки, если он задан.
    /// </summary>
    public static Color GroundColor(GameMap map)
    {
        Color fallback = new Color(0.6f, 0.6f, 0.6f);

        if (map == null)
            return fallback;

        if (map.groundMaterial != null &&
            map.groundMaterial.HasProperty("_BaseColor"))
        {
            return map.groundMaterial.GetColor("_BaseColor");
        }

        return fallback;
    }
}
