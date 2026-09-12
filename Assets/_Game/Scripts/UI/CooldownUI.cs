using UnityEngine;
using UnityEngine.UI;

public class CooldownUI : MonoBehaviour
{
    [Header("Sliders")]
    [Tooltip("Слайдер индикатора кулдауна рывка. Ты сам создаёшь его в сцене.")]
    [SerializeField] private Slider dashSlider;
    [Tooltip("Слайдер индикатора кулдауна выстрела. Ты сам создаёшь его в сцене.")]
    [SerializeField] private Slider fireSlider;

    [Header("Optional")]
    [Tooltip("Можно не назначать: компонент сам найдёт игрока и оружие.")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Weapon weapon;

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
    }
}