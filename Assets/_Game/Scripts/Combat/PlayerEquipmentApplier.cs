using UnityEngine;

/// <summary>
/// Применяет персистентное снаряжение на старте забега:
///  - снаряжённые способности разблокируются на AbilityManager
///    (бомба [E], щит [Q]);
///  - снаряжённая одежда красит тело игрока (MeshRenderer на корне).
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

    private bool subscribed;
    private bool hasDefaultColor;
    private Color defaultColor = Color.white;

    private MeshRenderer bodyRenderer;
    private MaterialPropertyBlock bodyBlock;

    private void Awake()
    {
        bodyRenderer = GetComponent<MeshRenderer>();

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

        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Playing)
        {
            ApplyAll();
        }
    }

    private void Start()
    {
        EnsureSubscribed();
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
        if (state == GameState.Playing)
            ApplyAll();
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
        AbilityManager abilityManager =
            GetComponent<AbilityManager>();

        if (abilityManager == null)
            return;

        UpgradeManager upgradeManager =
            UpgradeManager.Instance;

        if (upgradeManager == null)
            return;

        foreach (string abilityName in
                 EquipmentManager.EquippedAbilityNames)
        {
            if (string.IsNullOrEmpty(abilityName))
                continue;

            AbilityData data =
                FindAbility(upgradeManager, abilityName);

            if (data == null)
                continue;

            switch (data.Kind)
            {
                case AbilityKind.Bomb:
                    if (abilityManager.UnlockBomb())
                        Debug.Log(
                            $"[Equipment] Способность '{abilityName}' " +
                            "снаряжена: бомба разблокирована (клавиша E)."
                        );
                    break;

                case AbilityKind.Shield:
                    if (abilityManager.UnlockShield())
                        Debug.Log(
                            $"[Equipment] Способность '{abilityName}' " +
                            "снаряжена: щит разблокирован (клавиша Q)."
                        );
                    break;
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

    private void ApplyClothing()
    {
        if (bodyRenderer == null)
            bodyRenderer = GetComponent<MeshRenderer>();

        if (bodyRenderer == null)
            return;

        Color target =
            hasDefaultColor
                ? defaultColor
                : Color.white;

        string clothingName =
            EquipmentManager.EquippedClothingName;

        if (!string.IsNullOrEmpty(clothingName))
        {
            UpgradeManager upgradeManager =
                UpgradeManager.Instance;

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
                    target = data.BodyColor;
                }
            }
        }

        ApplyBodyColor(target);

        if (!string.IsNullOrEmpty(clothingName))
        {
            Debug.Log(
                $"[Equipment] Одежда '{clothingName}' применена."
            );
        }
    }

    private void ApplyBodyColor(Color color)
    {
        Material material = bodyRenderer.sharedMaterial;

        // У URP/Lit цвет живёт в _BaseColor, у старых шейдеров — в _Color.
        // Оба пишем только если свойство есть у материала, иначе
        // PropertyBlock тащил бы в батч лишний неиспользуемный проп.
        bool hasBaseColor = material != null &&
                            material.HasProperty(BaseColorId);
        bool hasColor = material != null &&
                        material.HasProperty(LegacyColorId);

        if (bodyBlock == null)
            bodyBlock = new MaterialPropertyBlock();

        bodyBlock.Clear();

        if (hasBaseColor)
            bodyBlock.SetColor(BaseColorId, color);

        if (hasColor)
            bodyBlock.SetColor(LegacyColorId, color);

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
