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
    private bool selected;
    private Color artSky;
    private Color artGround;

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

        artSky = sky;
        artGround = ground;

        if (backgroundImage != null)
            backgroundImage.color = Color.Lerp(sky, ArcadeUiTheme.Ink, 0.1f);

        if (landImage != null)
            landImage.color = ground;

        if (iconImage != null)
        {
            bool hasIcon = map.icon != null;

            iconImage.gameObject.SetActive(hasIcon);

            if (hasIcon)
            {
                iconImage.sprite = map.icon;
                iconImage.preserveAspect = true;
                iconImage.color = Color.white;
            }
        }

        // На тёмной земле (космос) — светлый текст, иначе тёмный.
        if (nameText != null)
        {
            nameText.text = string.IsNullOrEmpty(map.LocalizedName)
                ? map.name
                : map.LocalizedName;
        }

        if (mechanicText != null)
        {
            string mechanicName = map.LocalizedMechanicName;

            bool hasMechanic =
                !string.IsNullOrEmpty(mechanicName);

            mechanicText.gameObject.SetActive(hasMechanic);

            if (hasMechanic)
                mechanicText.text = mechanicName;
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
                descriptionText.text = description;
        }

        RequiredLevel = MapProgression.RequiredLevel(map);

        ApplyUnifiedLayout(map);

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
        selected = value;

        transform.localScale =
            Vector3.one * (value ? SelectedScale : 1f);

        if (frameImage != null)
        {
            frameImage.color = value
                ? ArcadeUiTheme.Gold
                : ArcadeUiTheme.Line;
        }

        UpdateAction();
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

        UpdateAction();
    }

    // =========================================================
    // RUNTIME DECOR (замок, подпись механики)
    // =========================================================

    private void ApplyUnifiedLayout(GameMap map)
    {
        RectTransform root = transform as RectTransform;

        float height = root != null && root.rect.height > 1f
            ? root.rect.height
            : 420f;

        Image surface = ArcadeCardVisuals.EnsureImage(
            transform,
            "CardSurface",
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Panel
        );

        surface.transform.SetSiblingIndex(1);
        ArcadeCardVisuals.Stretch(surface.rectTransform, 3f);

        Image hitArea = ArcadeCardVisuals.EnsureImage(
            transform,
            "CardHitArea",
            null,
            new Color(0f, 0f, 0f, 0f)
        );

        hitArea.raycastTarget = true;
        ArcadeCardVisuals.Stretch(hitArea.rectTransform, 0f);
        hitArea.transform.SetAsFirstSibling();

        if (selectButton != null)
            selectButton.targetGraphic = hitArea;

        if (frameImage != null)
        {
            frameImage.color = selected
                ? ArcadeUiTheme.Gold
                : ArcadeUiTheme.Line;
        }

        float artTop = 24f;
        float artHeight = Mathf.Clamp(height * 0.44f, 132f, 196f);

        if (backgroundImage != null)
        {
            RectTransform art = backgroundImage.rectTransform;

            ArcadeCardVisuals.TopStretch(art, artTop, artHeight, 14f);

            backgroundImage.sprite = ArcadeKitAssets.RoundRect;
            backgroundImage.type = Image.Type.Sliced;
            backgroundImage.pixelsPerUnitMultiplier = 1f;
            backgroundImage.color = Color.Lerp(artSky, ArcadeUiTheme.Ink, 0.1f);
            backgroundImage.raycastTarget = false;

            ArcadeCardVisuals.Pattern(art);
        }

        if (landImage != null && backgroundImage != null)
        {
            RectTransform land = landImage.rectTransform;

            if (land.parent != backgroundImage.transform)
                land.SetParent(backgroundImage.transform, false);

            land.anchorMin = new Vector2(0f, 0f);
            land.anchorMax = new Vector2(1f, 0f);
            land.pivot = new Vector2(0.5f, 0f);
            land.sizeDelta = new Vector2(-24f, 6f);
            land.anchoredPosition = new Vector2(0f, 10f);

            landImage.sprite = null;
            landImage.type = Image.Type.Simple;
            landImage.pixelsPerUnitMultiplier = 1f;
            landImage.color = ArcadeUiTheme.Alpha(artGround, 0.92f);
            landImage.raycastTarget = false;
            landImage.transform.SetAsLastSibling();
        }

        bool hasIcon = map != null && map.icon != null;
        float artCenterY = -(artTop + artHeight * 0.5f);

        if (iconDisc != null)
        {
            RectTransform disc = iconDisc.rectTransform;

            if (hasIcon)
            {
                disc.anchorMin = new Vector2(0.5f, 1f);
                disc.anchorMax = new Vector2(0.5f, 1f);
                disc.pivot = new Vector2(0.5f, 0.5f);
                disc.sizeDelta = new Vector2(78f, 78f);
                disc.anchoredPosition = new Vector2(0f, artCenterY);

                iconDisc.sprite = ArcadeKitAssets.IconTile;
                iconDisc.type = Image.Type.Sliced;
                iconDisc.color = ArcadeUiTheme.Alpha(Color.white, 0.12f);
                iconDisc.gameObject.SetActive(true);
            }
            else
            {
                disc.anchorMin = new Vector2(0.5f, 1f);
                disc.anchorMax = new Vector2(0.5f, 1f);
                disc.pivot = new Vector2(0.5f, 0.5f);
                disc.sizeDelta = new Vector2(128f, 128f);
                disc.anchoredPosition = new Vector2(0f, artCenterY);

                iconDisc.sprite = ArcadeKitAssets.Glow;
                iconDisc.type = Image.Type.Simple;
                iconDisc.color = ArcadeUiTheme.Alpha(
                    Color.Lerp(artGround, Color.white, 0.65f),
                    0.28f
                );
                iconDisc.gameObject.SetActive(true);
            }

            iconDisc.preserveAspect = true;
            iconDisc.raycastTarget = false;
        }

        if (iconImage != null)
        {
            RectTransform icon = iconImage.rectTransform;

            icon.anchorMin = new Vector2(0.5f, 1f);
            icon.anchorMax = new Vector2(0.5f, 1f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(52f, 52f);
            icon.anchoredPosition = new Vector2(0f, artCenterY);
            iconImage.raycastTarget = false;
        }

        float nameTop = artTop + artHeight + 12f;

        PlaceTop(
            nameText,
            nameTop,
            30f,
            26f,
            ArcadeKitAssets.Display,
            ArcadeUiTheme.TextPrimary,
            FontStyles.Bold
        );

        PlaceTop(
            mechanicText,
            nameTop + 32f,
            20f,
            13f,
            ArcadeKitAssets.BodyBold,
            ArcadeUiTheme.Gold,
            FontStyles.Bold | FontStyles.UpperCase
        );

        PlaceTop(
            descriptionText,
            nameTop + 54f,
            44f,
            13f,
            ArcadeKitAssets.Body,
            ArcadeUiTheme.TextSecondary,
            FontStyles.Normal
        );

        StyleChip(levelChipRoot);
        StyleChip(recordRoot);

        if (levelChipRoot != null)
        {
            RectTransform rect = levelChipRoot.transform as RectTransform;

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(104f, 28f);
            rect.anchoredPosition = new Vector2(-12f, -12f);
        }

        if (recordRoot != null)
        {
            RectTransform rect = recordRoot.transform as RectTransform;

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(104f, 26f);
            rect.anchoredPosition = new Vector2(-12f, -44f);
        }

        if (selectedChip != null)
        {
            selectedChip.anchorMin = new Vector2(0f, 0f);
            selectedChip.anchorMax = new Vector2(1f, 0f);
            selectedChip.pivot = new Vector2(0.5f, 0f);
            selectedChip.sizeDelta = new Vector2(-28f, 36f);
            selectedChip.anchoredPosition = new Vector2(0f, 12f);
            selectedChip.gameObject.SetActive(true);
        }

        if (lockText != null)
        {
            ArcadeCardVisuals.Text(
                lockText,
                ArcadeKitAssets.BodyBold,
                16f,
                ArcadeUiTheme.Gold,
                TextAlignmentOptions.Center,
                FontStyles.Bold | FontStyles.UpperCase
            );
        }

        ArcadeCardVisuals.Divider(transform, height - 58f);
        ArcadeCardVisuals.AccentRail(transform, ArcadeUiTheme.Gold);

        UpdateAction();

        if (lockOverlay != null)
            lockOverlay.transform.SetAsLastSibling();
    }

    private void UpdateAction()
    {
        if (selectedChip == null)
            return;

        Image background = selectedChip.GetComponent<Image>();
        TMP_Text label = selectedChip.GetComponentInChildren<TMP_Text>(true);

        if (background != null)
        {
            background.sprite = Unlocked
                ? ArcadeKitAssets.RoundRect
                : ArcadeKitAssets.RoundRect;

            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 1f;
            background.raycastTarget = false;
        }

        if (label != null)
        {
            label.font = ArcadeKitAssets.BodyBold;
            label.fontSize = 14f;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            label.raycastTarget = false;

            RectTransform labelRect = label.rectTransform;

            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 2f);
            labelRect.offsetMax = new Vector2(-10f, -2f);
        }

        if (!Unlocked)
        {
            if (background != null)
            {
                background.color = ArcadeUiTheme.Alpha(
                    ArcadeUiTheme.Ink,
                    0.95f
                );
            }

            if (label != null)
            {
                label.text = Lang.Get("maps.action_locked");
                label.color = ArcadeUiTheme.TextMuted;
            }

            return;
        }

        if (selected)
        {
            if (background != null)
                background.color = ArcadeUiTheme.Gold;

            if (label != null)
            {
                label.text = Lang.Get("maps.selected");
                label.color = ArcadeUiTheme.BackgroundDeep;
            }
        }
        else
        {
            if (background != null)
                background.color = ArcadeUiTheme.Orange;

            if (label != null)
            {
                label.text = Lang.Get("maps.action_select");
                label.color = ArcadeUiTheme.BackgroundDeep;
            }
        }
    }

    private static void PlaceTop(
        TMP_Text text,
        float top,
        float height,
        float size,
        TMP_FontAsset font,
        Color color,
        FontStyles style)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-28f, height);
        rect.anchoredPosition = new Vector2(0f, -top);

        ArcadeCardVisuals.Text(
            text,
            font,
            size,
            color,
            TextAlignmentOptions.Left,
            style
        );
    }

    private static void StyleChip(GameObject chip)
    {
        if (chip == null)
            return;

        Image background = chip.GetComponent<Image>();
        TMP_Text label = chip.GetComponentInChildren<TMP_Text>(true);

        if (background != null)
        {
            background.sprite = ArcadeKitAssets.RoundRect;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 1f;
            background.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.Ink, 0.84f);
            background.raycastTarget = false;
        }

        if (label != null)
        {
            label.font = ArcadeKitAssets.BodyBold;
            label.fontSize = 13f;
            label.color = ArcadeUiTheme.Gold;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            label.raycastTarget = false;
        }
    }

    private void EnsureDecor()
    {
        if (decorBuilt)
            return;

        decorBuilt = true;

        TMP_FontAsset font = ArcadeKitAssets.BodyBold;

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
            lockOverlay.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.Ink, 0.72f);
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
        background.sprite = ArcadeKitAssets.RoundRect;
        background.type = Image.Type.Sliced;
        background.pixelsPerUnitMultiplier = 1f;
        background.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.Ink, 0.8f);
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

        text.font = ArcadeKitAssets.BodyBold;
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
