using UnityEngine;
using UnityEngine.UI;

public class CooldownUI : MonoBehaviour
{
    [Header("Sliders")]
    [Tooltip("Слайдер индикатора кулдауна рывка. Ты сам создаёшь его в сцене.")]
    [SerializeField] private Slider dashSlider;
    [Tooltip("Слайдер индикатора кулдауна выстрела. Ты сам создаёшь его в сцене.")]
    [SerializeField] private Slider fireSlider;
    [Tooltip("Слайдер индикатора кулдауна бомбы. Ты сам создаёшь его в сцене.")]
    [SerializeField] private Slider bombSlider;
    [Tooltip("Слайдер индикатора кулдауна щита. Ты сам создаёшь его в сцене.")]
    [SerializeField] private Slider shieldSlider;

    [Header("Optional")]
    [Tooltip("Можно не назначать: компонент сам найдёт игрока и оружие.")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Weapon weapon;
    [SerializeField] private AbilityManager abilityManager;

    private void Update()
    {
        if (GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameState.Playing)
        {
            return;
        }

        EnsureRefs();

        UpdateDashSlider();
        UpdateFireSlider();
        UpdateBombSlider();
        UpdateShieldSlider();
    }

    private void UpdateBombSlider()
    {
        if (bombSlider == null || abilityManager == null)
            return;

        if (!abilityManager.HasBomb)
        {
            if (bombSlider.gameObject.activeSelf)
                bombSlider.gameObject.SetActive(false);

            return;
        }

        BombAbility bomb = abilityManager.Bomb;

        if (bomb == null)
            return;

        UpdateCooldownSlider(
            bombSlider,
            bomb.Cooldown,
            bomb.CooldownRemaining
        );
    }

    private void UpdateShieldSlider()
    {
        if (shieldSlider == null || abilityManager == null)
            return;

        if (!abilityManager.HasShield)
        {
            if (shieldSlider.gameObject.activeSelf)
                shieldSlider.gameObject.SetActive(false);

            return;
        }

        ShieldAbility shield = abilityManager.Shield;

        if (shield == null)
            return;

        UpdateCooldownSlider(
            shieldSlider,
            shield.Cooldown,
            shield.CooldownRemaining
        );
    }

    private void UpdateCooldownSlider(
        Slider slider,
        float maxValue,
        float remaining)
    {
        bool active = remaining > 0.001f;

        if (active)
        {
            slider.gameObject.SetActive(true);

            slider.value =
                maxValue > 0f
                    ? Mathf.Clamp01(1f - remaining / maxValue)
                    : 0f;
        }
        else
        {
            slider.gameObject.SetActive(false);
        }
    }

    private void UpdateDashSlider()
    {
        if (dashSlider == null || playerController == null)
            return;

        float max = playerController.DashCooldown;
        float remaining =
            playerController.DashCooldownRemaining;

        bool active = remaining > 0.001f;

        if (active)
        {
            dashSlider.gameObject.SetActive(true);

            dashSlider.value =
                max > 0f
                    ? Mathf.Clamp01(1f - remaining / max)
                    : 0f;
        }
        else
        {
            dashSlider.gameObject.SetActive(false);
        }
    }

    private void UpdateFireSlider()
    {
        if (fireSlider == null || weapon == null)
            return;

        bool active =
            weapon.FireCooldownRemaining > 0.001f;

        if (active)
        {
            fireSlider.gameObject.SetActive(true);
            fireSlider.value = weapon.FireCooldownProgress;
        }
        else
        {
            fireSlider.gameObject.SetActive(false);
        }
    }

    private void EnsureRefs()
    {
        if (playerController == null && weapon == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                playerController =
                    playerObject.GetComponent<PlayerController>();

                weapon =
                    playerObject.GetComponentInChildren<Weapon>();
            }
        }

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        if (weapon == null)
            weapon = FindAnyObjectByType<Weapon>();

        if (abilityManager == null)
            abilityManager = FindAnyObjectByType<AbilityManager>();
    }
}