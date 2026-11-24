using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays the arena preview, unlock requirement, record and selection state.</summary>
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

    private bool decorBuilt;
    private Image paper;
    private TMP_Text statusLabel;
    public Button SelectButton => selectButton;
    public bool Wired => wired;
    public bool Unlocked { get; private set; } = true;
    public int RequiredLevel { get; private set; } = 1;
    public void MarkWired() => wired = true;

    public void Prepare(Transform sheet, float x)
    {
        EnsureDecor();
        MenuSheet.Place(this, sheet, x, 155, 450, 541);
        MenuSheet.HideChildren(transform);
        MenuSheet.Place(frameImage, transform, 0, 0, 450, 541);
        MenuSheet.Surface(frameImage, MenuSheet.Rule);
        paper = MenuSheet.Box(transform, "Paper", 3, 3, 444, 534, MenuSheet.Card);
        paper.raycastTarget = true;
        selectButton.targetGraphic = paper;
        MenuSheet.Place(backgroundImage, transform, 15, 16, 420, 194);
        backgroundImage.raycastTarget = false;
        backgroundImage.type = Image.Type.Simple;
        MenuSheet.Place(lockOverlay, transform, 15, 16, 420, 194);
        MenuSheet.Surface(lockOverlay, new Color(0.15f, 0.19f, 0.18f, 0.26f));
        MenuSheet.Label(nameText, transform, 20, 229, 412, 48, 34, MenuSheet.Ink, heading: true);
        MenuSheet.Label(mechanicText, transform, 22, 284, 406, 35, 23, MenuSheet.Ink);
        MenuSheet.Label(descriptionText, transform, 22, 325, 406, 83, 21, MenuSheet.Muted);
        descriptionText.font = ArcadeKitAssets.Body;
        MenuSheet.Label(levelChipText, transform, 22, 416, 142, 31, 18, MenuSheet.Muted);
        MenuSheet.Label(recordText, transform, 165, 416, 263, 31, 18, MenuSheet.Muted, TextAlignmentOptions.Right);
        MenuSheet.Place(selectedChip, transform, 18, 469, 414, 52);
        statusLabel = selectedChip.GetComponentInChildren<TMP_Text>(true);
        MenuSheet.Label(statusLabel, selectedChip, 10, 0, 394, 52, 21, MenuSheet.Ink, TextAlignmentOptions.Center);
    }

    public void Bind(GameMap map) => Bind(map, -1);

    public void Bind(GameMap map, int mapIndex)
    {
        if (map == null) return;
        var preview = MenuSheetArt.Arena(map);
        backgroundImage.sprite = preview;
        backgroundImage.color = preview != null ? Color.white : map.skyColor;
        nameText.text = string.IsNullOrEmpty(map.LocalizedName) ? map.name : map.LocalizedName;
        if (GameplayJourney.Instance != null)
        {
            var record = GameplayJourney.Instance.Map(map.langKey);
            nameText.text += "  <size=55%>" + (record.best >= 20 ? "★" : "☆")
                + (record.won ? "★" : "☆") + (record.special ? "★" : "☆") + "</size>";
        }
        mechanicText.text = map.LocalizedMechanicName;
        descriptionText.text = MapMechanicText.Description(map.mechanic);
        RequiredLevel = MapProgression.RequiredLevel(map);
        Unlocked = MapProgression.IsUnlocked(map);
        lockOverlay.gameObject.SetActive(!Unlocked);
        levelChipText.text = Lang.Get("maps.level_chip", RequiredLevel);
        int best = mapIndex >= 0 ? PersonalBestRecord.BestWaveFor(mapIndex) : 0;
        recordText.text = best > 0 ? Lang.Get("maps.record", best) : "";
        selectButton.interactable = Unlocked;
    }

    public void SetState(bool focused, bool active)
    {
        frameImage.color = focused ? MenuSheet.Ink : MenuSheet.Rule;
        var chip = selectedChip.GetComponent<Image>();
        MenuSheet.Surface(chip, active ? MenuSheet.Sage : focused ? MenuSheet.Ochre : MenuSheet.Recess);
        statusLabel.text = !Unlocked ? Lang.Get("maps.locked_level", RequiredLevel)
            : active ? Lang.Get("maps.current")
            : focused ? Lang.Get("maps.selected") : Lang.Get("maps.action_select");
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
