using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RunAmmoHud : MonoBehaviour
{
    private RectTransform panel;
    private TMP_Text ammoLabel, evolutionLabel;
    private Image meter;
    private Button reloadButton;
    private Weapon weapon;
    private int lastAmmo = -1, lastCapacity = -1;
    private bool lastReload;
    private RunPerk lastEvolution;
    private WeaponData lastWeapon;
    private LangCode language;
    private float nextLookup;

    public void Build(RectTransform vitals)
    {
        if (panel != null) return;
        Vector2 right = Vector2.one, left = new Vector2(0,1);
        panel = ArcadeUiFactory.At(vitals, "Magazine", right, right, new Vector2(-5,-99), new Vector2(210,56));
        var face = ArcadeUiFactory.Image(panel, ArcadeKitAssets.CutPanel, MenuSheet.Ink, true);
        face.raycastTarget = true;
        reloadButton = panel.gameObject.AddComponent<Button>();
        reloadButton.targetGraphic = face;
        reloadButton.navigation = new Navigation { mode = Navigation.Mode.None };
        reloadButton.onClick.AddListener(() => weapon?.RequestReload());
        ammoLabel = ArcadeUiFactory.Label(panel, "Ammo", "", ArcadeKitAssets.BodyBold, 19, MenuSheet.Paper,
            TextAlignmentOptions.Left, left, left, new Vector2(10,-5), new Vector2(192,25));
        ammoLabel.raycastTarget = false;
        evolutionLabel = ArcadeUiFactory.Label(panel, "Evolution", "", ArcadeKitAssets.BodyBold, 13, MenuSheet.Ochre,
            TextAlignmentOptions.Left, left, left, new Vector2(10,-28), new Vector2(192,18));
        evolutionLabel.raycastTarget = false;
        var track = ArcadeUiFactory.At(panel, "Magazine track", new Vector2(.5f,0), new Vector2(.5f,0), new Vector2(0,4), new Vector2(187,4));
        ArcadeUiFactory.Image(track, null, ArcadeUiTheme.TextMuted);
        meter = ArcadeUiFactory.Image(ArcadeUiFactory.Stretch(track, "Fill"), ArcadeKitAssets.RoundRect, MenuSheet.Sage);
        meter.type = Image.Type.Filled; meter.fillMethod = Image.FillMethod.Horizontal;
        panel.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (panel == null) return;
        bool playing = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Playing && !GameModeManager.IsPvp;
        if (playing && weapon == null && Time.unscaledTime >= nextLookup)
        {
            nextLookup = Time.unscaledTime + .25f;
            if (PlayerHealth.Instance != null) weapon = PlayerHealth.Instance.GetComponentInChildren<Weapon>();
        }
        bool visible = playing && Time.timeScale > 0f && !RamLiftArena.InTransit && weapon != null && weapon.UsesMagazine;
        if (panel.gameObject.activeSelf != visible) panel.gameObject.SetActive(visible);
        if (!visible) return;
        bool reload = weapon.IsReloading;
        int ammo = weapon.Ammo, capacity = weapon.MagazineCapacity;
        RunPerk evolution = RunBuildMechanics.Evolution(weapon.Data);
        if (ammo != lastAmmo || capacity != lastCapacity || reload != lastReload || evolution != lastEvolution || weapon.Data != lastWeapon || language != Lang.Code)
        {
            lastAmmo = ammo; lastCapacity = capacity; lastReload = reload; lastEvolution = evolution; lastWeapon = weapon.Data; language = Lang.Code;
            ammoLabel.text = reload ? GameplayJourney.T("ПЕРЕЗАРЯДКА", "RELOADING", "RELOADING") :
                ammo + " / " + capacity + (MobileControls.IsTouchDevice ? "   ↻" : "   [R]");
            evolutionLabel.text = evolution == RunPerk.None ? weapon.Data.LocalizedName : RunBuildMechanics.Name(evolution);
        }
        meter.fillAmount = reload ? weapon.ReloadProgress : Mathf.Clamp01((float)ammo / Mathf.Max(1, capacity));
        meter.color = reload ? MenuSheet.Ochre : Color.Lerp(new Color32(225,85,70,255), MenuSheet.Sage, meter.fillAmount);
        reloadButton.interactable = !reload && ammo < capacity && Time.timeScale > 0f;
    }
}
