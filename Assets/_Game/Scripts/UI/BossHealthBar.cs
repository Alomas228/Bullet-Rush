using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossHealthBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject bossPanel;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text bossNameText;

    private Enemy currentBoss;

    private void Update()
    {
        if (currentBoss == null ||
            currentBoss.IsDead)
        {
            FindBoss();

            if (currentBoss == null)
            {
                Hide();
                return;
            }
        }

        UpdateBar();
    }

    private void FindBoss()
    {
        Enemy[] enemies =
            FindObjectsByType<Enemy>(
                FindObjectsInactive.Exclude
            );

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null)
                continue;

            EnemyData data =
                enemy.GetEnemyData();

            if (data != null &&
                data.EnemyType == EnemyType.Boss)
            {
                currentBoss = enemy;

                Show();

                return;
            }
        }
    }

    private void UpdateBar()
    {
        if (healthSlider == null ||
            currentBoss == null)
            return;

        float maxHealth =
            currentBoss.MaxHealth;

        if (maxHealth <= 0f)
            return;

        float healthPercent =
            currentBoss.CurrentHealth /
            maxHealth;

        healthSlider.value =
            Mathf.Clamp01(healthPercent);

        if (bossNameText != null)
            bossNameText.text = "BOSS";
    }

    private void Show()
    {
        if (bossPanel != null)
            bossPanel.SetActive(true);
    }

    private void Hide()
    {
        if (bossPanel != null)
            bossPanel.SetActive(false);

        currentBoss = null;
    }
}