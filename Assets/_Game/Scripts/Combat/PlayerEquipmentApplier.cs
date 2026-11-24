using UnityEngine;

/// <summary>
/// Применяет персистентное снаряжение на старте забега:
///  - снаряжённые способности разблокируются на AbilityManager
///    (бомба [E], щит [Q]);
///  - снаряжённая одежда натягивает текстуру скина на тело игрока
///    (MeshRenderer на корне), а при её отсутствии красит куб цветом.
///
/// Компонент добавляется на игрока панелью «Снаряжение»
/// (Tools -> Bullet Rush -> Build Equipment UI).
/// </summary>
public class PlayerEquipmentApplier : MonoBehaviour
{
    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int LegacyColorId =
        Shader.PropertyToID("_Color");

    private static readonly int BaseMapId =
        Shader.PropertyToID("_BaseMap");

    private static readonly int LegacyMainTexId =
        Shader.PropertyToID("_MainTex");

    private bool subscribed;
    private bool started;
    private bool gameplayApplied;
    private bool hasDefaultColor;
    private Color defaultColor = Color.white;

    private MeshRenderer bodyRenderer;
    private MeshFilter bodyFilter;
    private Mesh defaultMesh;
    private MaterialPropertyBlock bodyBlock;

    private void Awake()
    {
        bodyRenderer = GetComponent<MeshRenderer>();
        bodyFilter = GetComponent<MeshFilter>();
        defaultMesh = bodyFilter != null ? bodyFilter.sharedMesh : null;

        if (bodyRenderer == null)
            return;

        // Цвет читается из общего материала, а не из renderer.material:
        // обращение к material создаёт копию на каждый вызов и держит
        // копию живой всё время забега. Общий материал префаба при
        // этом не меняется — красится рендерер через PropertyBlock.
        defaultColor = GetMaterialColor(bodyRenderer.sharedMaterial);
        hasDefaultColor = true;
    }

    private void OnEnable()
    {
        EnsureSubscribed();
        if (started) ApplyCurrentState();
    }

    private void Start()
    {
        started = true;
        EnsureSubscribed();
        // A newly loaded scene can already have a state before this component
        // subscribes. Always synchronize its actual state on initialization.
        ApplyCurrentState();
    }

    private void OnDisable()
    {
        if (subscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -=
                HandleStateChanged;
            subscribed = false;
        }
    }

    private void EnsureSubscribed()
    {
        if (subscribed)
            return;

        if (GameStateManager.Instance == null)
            return;

        GameStateManager.Instance.OnGameStateChanged +=
            HandleStateChanged;
        subscribed = true;
    }

    private void HandleStateChanged(GameState state)
    {
        if (!started) return;
        if (state == GameState.Menu) gameplayApplied = false;
        ApplyCurrentState();
    }

    private void ApplyCurrentState()
    {
        if (GameStateManager.Instance == null) return;
        GameState state = GameStateManager.Instance.CurrentState;
        if (state == GameState.Menu)
        {
            ApplyClothing();
            return;
        }
        if (state != GameState.Playing) return;
        if (!gameplayApplied && UpgradeManager.Instance != null)
        {
            gameplayApplied = true;
            ApplyAll();
        }
        else
        {
            ApplyClothing();
        }
    }

    internal void ResetForSession(bool startingRun)
    {
        gameplayApplied = false;
        if (startingRun)
        {
            // Do this before any Playing subscribers apply permanent upgrades;
            // ability references have to exist before cooldown bonuses are applied.
            ApplyAll();
            gameplayApplied = true;
        }
        else
        {
            ApplyClothing();
        }
    }

    private void ApplyAll()
    {
        ApplyAbilities();
        ApplyClothing();

        // Постоянные улучшения идут последними: к этому моменту
        // бомба/щит уже разблокированы, значит перезарядку
        // способностей есть на что повесить.
        PermanentUpgradeApplier upgradeApplier =
            GetComponent<PermanentUpgradeApplier>();

        if (upgradeApplier != null)
            upgradeApplier.ApplyPermanentUpgrades();
    }

    // =========================================================
    // ABILITIES
    // =========================================================

    private void ApplyAbilities()
    {
        if (GameModeManager.IsPvp)
            return;

        AbilityManager abilityManager =
            GetComponent<AbilityManager>();

        if (abilityManager == null)
            return;

        UpgradeManager upgradeManager =
            UpgradeManager.Instance;

        if (upgradeManager == null)
            return;

        // Reserve the original Q/E slots first whenever the old abilities
        // are selected; new active abilities occupy the remaining slots.
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (string abilityName in EquipmentManager.EquippedAbilityNames)
            {
                if (string.IsNullOrEmpty(abilityName)) continue;
                AbilityData data = FindAbility(upgradeManager, abilityName);
                if (data == null || data.IsLegacy != (pass == 0) || !EquipmentManager.IsOwned(data)) continue;
                abilityManager.EquipAbility(data);
            }
        }
    }

    private static AbilityData FindAbility(
        UpgradeManager manager,
        string abilityName)
    {
        foreach (AbilityData candidate in
                 manager.GetAvailableAbilities())
        {
            if (candidate == null)
                continue;

            if (candidate.AbilityName == abilityName)
                return candidate;
        }

        return null;
    }

    // =========================================================
    // CLOTHING
    // =========================================================

    internal void RefreshMenuAppearance()
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Menu)
            ApplyClothing();
    }

    private void ApplyClothing()
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponent<MeshRenderer>();

        if (bodyRenderer == null)
            return;

        Color targetColor =
            hasDefaultColor
                ? defaultColor
                : Color.white;

        Texture2D targetTexture = null;
        Mesh targetMesh = defaultMesh;

        string clothingName =
            EquipmentManager.EquippedClothingName;

        if (!string.IsNullOrEmpty(clothingName))
        {
            UpgradeManager upgradeManager =
                UpgradeManager.Instance;

            // Do not replace an already equipped skin with the default cube
            // while the catalog is still initializing.
            if (upgradeManager == null) return;

            if (upgradeManager != null)
            {
                ClothingData data =
                    FindClothing(
                        upgradeManager,
                        clothingName
                    );

                if (data != null &&
                    EquipmentManager.IsOwned(data))
                {
                    if (data.BodyMesh != null)
                        targetMesh = data.BodyMesh;

                    if (data.HasSkinTexture)
                    {
                        // Текстура заменяет цвет: колор ставим белым,
                        // чтобы URP/Lit не подкрашивал картинку.
                        targetTexture = data.SkinTexture;
                        targetColor = Color.white;
                    }
                    else
                    {
                        targetColor = data.BodyColor;
                    }
                }
            }
        }

        if (bodyFilter != null)
            bodyFilter.sharedMesh = targetMesh;

        ApplyBodyVisual(targetTexture, targetColor);

        if (!string.IsNullOrEmpty(clothingName))
        {
            Debug.Log(
                $"[Equipment] Одежда '{clothingName}' применена."
            );
        }
    }

    private void ApplyBodyVisual(Texture2D texture, Color color)
    {
        Material material = bodyRenderer.sharedMaterial;

        // У URP/Lit цвет живёт в _BaseColor, у старых шейдеров — в _Color.
        // Оба пишем только если свойство есть у материала, иначе
        // PropertyBlock тащил бы в батч лишний неиспользуемный проп.
        bool hasBaseColor = material != null &&
                            material.HasProperty(BaseColorId);
        bool hasColor = material != null &&
                        material.HasProperty(LegacyColorId);

        bool hasBaseMap = material != null &&
                          material.HasProperty(BaseMapId);
        bool hasMainTex = material != null &&
                          material.HasProperty(LegacyMainTexId);

        if (bodyBlock == null)
            bodyBlock = new MaterialPropertyBlock();

        // Clear снимает и текстуру прошлого скина, и цвет: без него
        // после снятия текстуры на кубе остался бы прежний рисунок.
        bodyBlock.Clear();

        if (hasBaseColor)
            bodyBlock.SetColor(BaseColorId, color);

        if (hasColor)
            bodyBlock.SetColor(LegacyColorId, color);

        if (texture != null)
        {
            if (hasBaseMap)
                bodyBlock.SetTexture(BaseMapId, texture);

            if (hasMainTex)
                bodyBlock.SetTexture(LegacyMainTexId, texture);
        }

        bodyRenderer.SetPropertyBlock(bodyBlock);
    }

    private static Color GetMaterialColor(Material material)
    {
        if (material == null)
            return Color.white;

        if (material.HasProperty(BaseColorId))
            return material.GetColor(BaseColorId);

        if (material.HasProperty(LegacyColorId))
            return material.GetColor(LegacyColorId);

        return Color.white;
    }

    private static ClothingData FindClothing(
        UpgradeManager manager,
        string clothingName)
    {
        foreach (ClothingData candidate in
                 manager.GetAvailableClothing())
        {
            if (candidate == null)
                continue;

            if (candidate.ClothingName == clothingName)
                return candidate;
        }

        return null;
    }
}
