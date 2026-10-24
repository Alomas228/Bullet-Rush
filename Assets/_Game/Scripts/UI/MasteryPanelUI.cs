using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MasteryPanelUI : MonoBehaviour, ILangRefreshable
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text prestigeText;
    [SerializeField] private Button prestigeButton;
    [SerializeField] private TMP_Text collectionText;
    [SerializeField] private Transform rowsRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject rowTemplate;

    private readonly List<GameObject> rows = new List<GameObject>();
    private GameObject runtimeDim;

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
            background.sprite = null;
            background.type = Image.Type.Simple;
            background.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.98f);
            background.raycastTarget = true;
        }

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

        ClearRows();
        UpgradeManager upgradeManager = UpgradeManager.Instance;
        if (upgradeManager == null || rowsRoot == null) return;

        Transform contentRoot = rowsRoot.Find("Viewport/Content") ?? rowsRoot.Find("Content");
        if (contentRoot != null)
            rowsRoot = contentRoot;

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
        rowsLayout.cellSize = new Vector2(300f, 132f);
        rowsLayout.spacing = new Vector2(14f, 14f);
        rowsLayout.padding = new RectOffset(12, 12, 12, 12);
        rowsLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        rowsLayout.constraintCount = 3;
        rowsLayout.childAlignment = TextAnchor.UpperLeft;
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
            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            if (rowLayout == null)
                rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minWidth = 300f;
            rowLayout.preferredWidth = 300f;
            rowLayout.flexibleWidth = 0f;
            rowLayout.preferredHeight = 132f;
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

    private GameObject CreateRow(WeaponData weapon)
    {
        GameObject row = new GameObject("Mastery_" + weapon.WeaponName, typeof(RectTransform), typeof(LayoutElement), typeof(Image));
        row.transform.SetParent(rowsRoot, false);

        RectTransform rect = row.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300f, 132f);
        LayoutElement element = row.GetComponent<LayoutElement>();
        element.minHeight = 132f;
        element.preferredHeight = 132f;
        element.flexibleWidth = 1f;

        Image background = row.GetComponent<Image>();
        background.sprite = ArcadeKitAssets.Card;
        background.type = Image.Type.Sliced;
        background.color = ArcadeUiTheme.CardSoft;
        background.raycastTarget = false;

        RectTransform accent = ArcadeUiFactory.At(
            row.transform,
            "Accent",
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(8f, 0f),
            new Vector2(5f, 104f)
        );
        ArcadeUiFactory.Image(accent, ArcadeKitAssets.RoundRect, ArcadeUiTheme.Orange, true);

        TMP_Text label = ArcadeUiFactory.Label(
            row.transform,
            "Label",
            weapon.LocalizedName,
            ArcadeKitAssets.BodyBold,
            17f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(24f, 42f),
            new Vector2(250f, 28f),
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
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(24f, 12f),
            new Vector2(250f, 20f)
        );

        ArcadeUiFactory.Label(
            row.transform,
            "Effect",
            string.Empty,
            ArcadeKitAssets.Body,
            12f,
            ArcadeUiTheme.GoldBright,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(24f, -18f),
            new Vector2(250f, 22f)
        );

        RectTransform barRect = ArcadeUiFactory.At(
            row.transform,
            "Bar",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-18f, -44f),
            new Vector2(180f, 8f)
        );
        ArcadeUiFactory.Bar(barRect, 0f, false, ArcadeUiTheme.Panel, ArcadeUiTheme.Cyan);

        return row;
    }

    private string GetMasteryEffect(int level)
    {
        switch (level)
        {
            case 0: return Lang.GetOr("progression.mastery.effect.locked", "Следующая награда: титул · 100 убийств");
            case 1: return Lang.GetOr("progression.mastery.effect.skin", "Получен титул · следующая награда: скин");
            case 2: return Lang.GetOr("progression.mastery.effect.effect", "Получен скин · следующая награда: эффект");
            case 3: return Lang.GetOr("progression.mastery.effect.master", "Получен эффект · следующая награда: титул мастера");
            case 4: return Lang.GetOr("progression.mastery.effect.aura", "Получен титул мастера · следующая награда: аура");
            default: return Lang.GetOr("progression.mastery.effect.max", "Мастерство завершено · все награды получены");
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
