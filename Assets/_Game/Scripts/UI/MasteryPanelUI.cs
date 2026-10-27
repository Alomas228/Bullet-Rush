using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MasteryPanelUI : MonoBehaviour, ILangRefreshable
{
    private const float CardWidth = 320f;
    private const float CardHeight = 230f;
    private const float RowsTopInset = 258f;

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text prestigeText;
    [SerializeField] private Button prestigeButton;
    [SerializeField] private TMP_Text collectionText;
    [SerializeField] private Transform rowsRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject rowTemplate;

    private readonly List<GameObject> rows = new List<GameObject>();
    private GameObject runtimeDim;
    private TMP_Text runtimeDescription;

    private void Awake()
    {
        if (prestigeButton != null) prestigeButton.onClick.AddListener(HandlePrestige);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        ApplyRuntimeBackdrop();

        if (MasteryManager.Instance != null)
        {
            MasteryManager.Instance.OnWeaponMasteryChanged += HandleMasteryChanged;
            MasteryManager.Instance.OnCollectionChanged += Refresh;
            MasteryManager.Instance.OnPrestigeChanged += HandlePrestigeChanged;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (runtimeDim != null)
            runtimeDim.SetActive(false);

        if (MasteryManager.Instance != null)
        {
            MasteryManager.Instance.OnWeaponMasteryChanged -= HandleMasteryChanged;
            MasteryManager.Instance.OnCollectionChanged -= Refresh;
            MasteryManager.Instance.OnPrestigeChanged -= HandlePrestigeChanged;
        }
    }

    private void HandleMasteryChanged(string weaponId) => Refresh();
    private void HandlePrestigeChanged(int prestige) => Refresh();

    private void OnDestroy()
    {
        if (prestigeButton != null) prestigeButton.onClick.RemoveListener(HandlePrestige);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    public void RefreshLang() => Refresh();

    private void ApplyRuntimeBackdrop()
    {
        Image background = GetComponent<Image>();
        if (background != null)
        {
            background.sprite = ArcadeKitAssets.RoundRect;
            background.type = Image.Type.Sliced;
            background.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.98f);
            background.raycastTarget = true;
        }

        Shadow shadow = GetComponent<Shadow>();
        if (shadow == null)
            shadow = gameObject.AddComponent<Shadow>();
        shadow.effectColor = ArcadeUiTheme.Shadow;
        shadow.effectDistance = new Vector2(0f, -8f);
        shadow.useGraphicAlpha = true;

        if (runtimeDim == null)
        {
            runtimeDim = new GameObject("MasteryRuntimeDim", typeof(RectTransform), typeof(Image));
            runtimeDim.transform.SetParent(transform.parent, false);
            RectTransform dimRect = runtimeDim.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            Image dim = runtimeDim.GetComponent<Image>();
            dim.color = ArcadeUiTheme.Backdrop;
            dim.raycastTarget = true;
        }

        runtimeDim.transform.SetAsFirstSibling();
        runtimeDim.SetActive(true);
    }

    public void Refresh()
    {
        MasteryManager mastery = MasteryManager.EnsureExists();
        if (titleText != null) titleText.text = Lang.GetOr("progression.mastery.title", "MASTERY & COLLECTION");
        if (prestigeText != null)
        {
            bool available = mastery.CanPrestige();
            prestigeText.text = string.Format(
                Lang.GetOr("progression.prestige.status", "Prestige: {0} · {1}"),
                mastery.Prestige,
                available ? Lang.GetOr("progression.prestige.available", "READY") : Lang.GetOr("progression.prestige.locked", "Reach level 50")
            );
        }
        if (prestigeButton != null) prestigeButton.interactable = mastery.CanPrestige();
        if (collectionText != null)
        {
            collectionText.text = string.Format(
                Lang.GetOr("progression.collection.summary", "Titles: {0} · Cosmetics: {1}"),
                mastery.UnlockedTitles.Count,
                mastery.UnlockedCosmetics.Count
            );
        }
        EnsureRuntimeDescription();

        ClearRows();
        UpgradeManager upgradeManager = UpgradeManager.Instance;
        if (upgradeManager == null || rowsRoot == null) return;

        Transform contentRoot = rowsRoot.Find("Viewport/Content") ?? rowsRoot.Find("Content");
        if (contentRoot != null)
            rowsRoot = contentRoot;

        if (rowsRoot.parent != null && rowsRoot.parent.parent is RectTransform rowsArea)
            rowsArea.offsetMax = new Vector2(rowsArea.offsetMax.x, -RowsTopInset);

        VerticalLayoutGroup oldLayout = rowsRoot.GetComponent<VerticalLayoutGroup>();
        GridLayoutGroup rowsLayout = rowsRoot.GetComponent<GridLayoutGroup>();

        if (rowsLayout == null && oldLayout != null)
        {
            // Unity не допускает два LayoutGroup на одном объекте.
            // Удаляем старый layout, созданный предыдущей версией панели.
            DestroyImmediate(oldLayout);
        }

        rowsLayout = rowsRoot.GetComponent<GridLayoutGroup>();
        if (rowsLayout == null)
            rowsLayout = rowsRoot.gameObject.AddComponent<GridLayoutGroup>();

        if (rowsLayout == null)
            return;
        Canvas.ForceUpdateCanvases();
        rowsLayout.cellSize = new Vector2(CardWidth, CardHeight);
        rowsLayout.spacing = new Vector2(18f, 14f);
        rowsLayout.padding = new RectOffset(12, 12, 12, 12);
        rowsLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        RectTransform rowsRect = rowsRoot as RectTransform;
        float availableWidth = rowsRect != null ? rowsRect.rect.width : 0f;
        rowsLayout.constraintCount = availableWidth > 0f
            ? Mathf.Clamp(
                Mathf.FloorToInt(
                    (availableWidth - rowsLayout.padding.horizontal + rowsLayout.spacing.x) /
                    (CardWidth + rowsLayout.spacing.x)
                ),
                1,
                3
            )
            : 3;
        rowsLayout.childAlignment = TextAnchor.UpperCenter;
        IReadOnlyList<WeaponData> weapons = upgradeManager.GetAvailableWeapons();
        if (weapons == null)
            return;

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponData weapon = weapons[i];
            if (weapon == null) continue;
            GameObject row = rowTemplate != null
                ? Instantiate(rowTemplate, rowsRoot)
                : CreateRow(weapon);
            row.SetActive(true);
            ApplyCardStyle(row, weapon);
            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            if (rowLayout == null)
                rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minWidth = CardWidth;
            rowLayout.preferredWidth = CardWidth;
            rowLayout.flexibleWidth = 0f;
            rowLayout.preferredHeight = CardHeight;
            int kills = mastery.GetWeaponKills(weapon.WeaponName);
            int level = mastery.GetWeaponMasteryLevel(weapon.WeaponName);
            int next = mastery.GetNextMasteryThreshold(weapon.WeaponName);
            TMP_Text text = row.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (text != null)
                text.text = weapon.LocalizedName;
            TMP_Text progress = row.transform.Find("Progress")?.GetComponent<TMP_Text>();
            if (progress != null)
                progress.text = string.Format(
                    Lang.GetOr("progression.weapon.progress", "Mastery {0} · {1}/{2} kills"),
                    level,
                    kills,
                    next
                );
            TMP_Text effect = row.transform.Find("Effect")?.GetComponent<TMP_Text>();
            if (effect != null)
                effect.text = GetMasteryEffect(level);
            Slider bar = row.transform.Find("Bar")?.GetComponent<Slider>();
            if (bar != null)
                bar.SetValueWithoutNotify(next > 0 ? Mathf.Clamp01((float)kills / next) : 1f);
            rows.Add(row);
        }
    }

    private void EnsureRuntimeDescription()
    {
        if (runtimeDescription == null)
        {
            Transform existing = transform.Find("MasteryRuntimeDescription");
            if (existing != null)
                runtimeDescription = existing.GetComponent<TMP_Text>();

            if (runtimeDescription == null)
            {
                RectTransform rect = ArcadeUiFactory.At(
                    transform,
                    "MasteryRuntimeDescription",
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -216f),
                    new Vector2(-80f, 40f)
                );
                runtimeDescription = ArcadeUiFactory.Label(
                    rect,
                    string.Empty,
                    ArcadeKitAssets.Body,
                    14f,
                    ArcadeUiTheme.TextSecondary,
                    TextAlignmentOptions.Center
                );
            }
        }

        runtimeDescription.text = Lang.Get("progression.mastery.summary");
        runtimeDescription.font = ArcadeKitAssets.Body;
        runtimeDescription.fontSize = 14f;
        runtimeDescription.color = ArcadeUiTheme.TextSecondary;
        runtimeDescription.alignment = TextAlignmentOptions.Center;
        runtimeDescription.enableWordWrapping = true;
        runtimeDescription.overflowMode = TextOverflowModes.Ellipsis;
        runtimeDescription.raycastTarget = false;
    }

    private GameObject CreateRow(WeaponData weapon)
    {
        GameObject row = new GameObject("Mastery_" + weapon.WeaponName, typeof(RectTransform), typeof(LayoutElement), typeof(Image));
        row.transform.SetParent(rowsRoot, false);

        RectTransform rect = row.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(CardWidth, CardHeight);
        LayoutElement element = row.GetComponent<LayoutElement>();
        element.minWidth = CardWidth;
        element.preferredWidth = CardWidth;
        element.minHeight = CardHeight;
        element.preferredHeight = CardHeight;
        element.flexibleWidth = 0f;

        Image background = row.GetComponent<Image>();
        background.sprite = ArcadeKitAssets.Card;
        background.type = Image.Type.Sliced;
        background.color = Color.clear;
        background.raycastTarget = false;

        RectTransform shadow = ArcadeUiFactory.At(
            row.transform,
            "CardShadow",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -5f),
            new Vector2(CardWidth + 8f, CardHeight + 8f)
        );
        ArcadeUiFactory.Image(shadow, ArcadeKitAssets.Card, ArcadeUiTheme.Shadow, true);
        shadow.SetAsFirstSibling();

        RectTransform frame = ArcadeUiFactory.Stretch(row.transform, "CardFrame");
        ArcadeUiFactory.Image(frame, ArcadeKitAssets.Card, ArcadeUiTheme.Line, true);

        RectTransform face = ArcadeUiFactory.Stretch(row.transform, "CardFace", 3f, 3f, 3f, 3f);
        ArcadeUiFactory.Image(face, ArcadeKitAssets.RoundRect, ArcadeUiTheme.Panel, true);

        RectTransform accent = ArcadeUiFactory.At(
            row.transform,
            "Accent",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -10f),
            new Vector2(286f, 4f)
        );
        ArcadeUiFactory.Image(accent, ArcadeKitAssets.RoundRect, ArcadeUiTheme.Orange, true);

        RectTransform art = ArcadeUiFactory.At(
            row.transform,
            "WeaponArt",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(24f, -22f),
            new Vector2(272f, 82f)
        );
        ArcadeUiFactory.Image(art, ArcadeKitAssets.RoundRect, ArcadeUiTheme.CardSoft, true);
        ArcadeCardVisuals.Pattern(art);

        Image icon = ArcadeUiFactory.Icon(
            art,
            "WeaponIcon",
            "target",
            ArcadeUiTheme.Cyan,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(22f, 0f),
            new Vector2(52f, 52f)
        );
        icon.preserveAspect = true;

        TMP_Text type = ArcadeUiFactory.Label(
            art,
            "WeaponType",
            GetWeaponTypeLabel(weapon.WeaponType),
            ArcadeKitAssets.BodyBold,
            12f,
            ArcadeUiTheme.Cyan,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(82f, 12f),
            new Vector2(176f, 18f),
            true
        );
        type.fontStyle = FontStyles.Bold | FontStyles.UpperCase;

        TMP_Text label = ArcadeUiFactory.Label(
            row.transform,
            "Label",
            weapon.LocalizedName,
            ArcadeKitAssets.BodyBold,
            20f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(24f, -118f),
            new Vector2(272f, 28f),
            true
        );

        ArcadeUiFactory.Label(
            row.transform,
            "Progress",
            string.Empty,
            ArcadeKitAssets.Body,
            13f,
            ArcadeUiTheme.TextSecondary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(24f, -150f),
            new Vector2(272f, 20f)
        );

        ArcadeUiFactory.Label(
            row.transform,
            "Effect",
            string.Empty,
            ArcadeKitAssets.Body,
            12f,
            ArcadeUiTheme.GoldBright,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(24f, -174f),
            new Vector2(272f, 32f)
        );

        RectTransform barRect = ArcadeUiFactory.At(
            row.transform,
            "Bar",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(24f, -210f),
            new Vector2(272f, 8f)
        );
        ArcadeUiFactory.Bar(barRect, 0f, false, ArcadeUiTheme.Panel, ArcadeUiTheme.Cyan);

        return row;
    }

    private void ApplyCardStyle(GameObject row, WeaponData weapon)
    {
        if (row == null || weapon == null)
            return;

        Image background = row.GetComponent<Image>();
        if (background != null)
        {
            background.sprite = ArcadeKitAssets.Card;
            background.type = Image.Type.Sliced;
            background.color = Color.clear;
            background.raycastTarget = false;
        }

        Image frame = row.transform.Find("CardFrame")?.GetComponent<Image>();
        if (frame != null)
        {
            frame.sprite = ArcadeKitAssets.Card;
            frame.type = Image.Type.Sliced;
            frame.color = ArcadeUiTheme.Line;
            frame.raycastTarget = false;
        }

        Color accent = EquipmentWireframeTheme.GetRarityColor(weapon.Rarity);
        Image accentImage = row.transform.Find("Accent")?.GetComponent<Image>();
        if (accentImage != null)
            accentImage.color = accent;

        Image art = row.transform.Find("WeaponArt")?.GetComponent<Image>();
        if (art != null)
        {
            art.sprite = ArcadeKitAssets.RoundRect;
            art.type = Image.Type.Sliced;
            art.color = ArcadeCardVisuals.ArtTint(accent);
            ArcadeCardVisuals.Pattern(art.rectTransform);
        }

        Image icon = row.transform.Find("WeaponArt/WeaponIcon")?.GetComponent<Image>();
        if (icon != null)
        {
            icon.sprite = ArcadeKitAssets.Icon("target");
            icon.color = Color.Lerp(accent, Color.white, 0.28f);
            icon.preserveAspect = true;
        }

        TMP_Text type = row.transform.Find("WeaponType")?.GetComponent<TMP_Text>();
        if (type == null)
            type = row.transform.Find("WeaponArt/WeaponType")?.GetComponent<TMP_Text>();
        if (type != null)
        {
            type.text = GetWeaponTypeLabel(weapon.WeaponType);
            type.color = accent;
            type.font = ArcadeKitAssets.BodyBold;
            type.fontSize = 12f;
            type.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        }
    }

    private static string GetWeaponTypeLabel(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Rifle: return Lang.Get("eq.type_rifle");
            case WeaponType.Shotgun: return Lang.Get("eq.type_shotgun");
            case WeaponType.SMG: return Lang.Get("eq.type_smg");
            default: return type.ToString();
        }
    }

    private string GetMasteryEffect(int level)
    {
        switch (level)
        {
            case 0: return Lang.Get("progression.mastery.effect.locked");
            case 1: return Lang.Get("progression.mastery.effect.skin");
            case 2: return Lang.Get("progression.mastery.effect.effect");
            case 3: return Lang.Get("progression.mastery.effect.master");
            case 4: return Lang.Get("progression.mastery.effect.aura");
            default: return Lang.Get("progression.mastery.effect.max");
        }
    }

    private void ClearRows()
    {
        for (int i = 0; i < rows.Count; i++)
            if (rows[i] != null) Destroy(rows[i]);
        rows.Clear();
    }

    private void HandlePrestige()
    {
        MasteryManager.EnsureExists().TryPrestige();
        Refresh();
    }

    private void Close()
    {
        if (MainMenuUI.Instance != null) MainMenuUI.Instance.CloseSubPanels();
        else gameObject.SetActive(false);
    }
}
