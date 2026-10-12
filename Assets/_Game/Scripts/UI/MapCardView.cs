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
    /// Заполняет карточку данными карты. Иконку берёт из GameMap,
    /// а если её нет — рисует цветной диск цвета подложки.
    /// </summary>
    public void Bind(GameMap map)
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

        RequiredLevel = MapProgression.RequiredLevel(map);

        ApplyLock(MapProgression.IsUnlocked(map));

        SetSelected(false);
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

        if (mechanicText == null)
        {
            mechanicText = CreateLabel(
                "MechanicText",
                new Vector2(0.5f, 0f),
                new Vector2(0f, 100f),
                new Vector2(280f, 30f),
                18f
            );
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
        }

        if (font == null)
            return;

        if (mechanicText != null)
            mechanicText.font = font;

        if (lockText != null)
            lockText.font = font;
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
