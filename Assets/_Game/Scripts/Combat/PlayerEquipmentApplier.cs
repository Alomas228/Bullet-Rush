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
    private bool subscribed;
    private bool hasDefaultColor;
    private Color defaultColor = Color.white;

    private void Awake()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            defaultColor = renderer.material.color;
            hasDefaultColor = true;
        }
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
        MeshRenderer renderer =
            GetComponent<MeshRenderer>();

        if (renderer == null)
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

        renderer.material.color = target;

        if (!string.IsNullOrEmpty(clothingName))
        {
            Debug.Log(
                $"[Equipment] Одежда '{clothingName}' применена."
            );
        }
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