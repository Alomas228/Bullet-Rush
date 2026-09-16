using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossHealthBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject bossPanel;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text bossNameText;

    // Как часто искать босса, пока его нет в сцене. Снижает нагрузку
    // в обычных волнах (поиск по сцене раз в кадр).
    private const float BossSearchInterval = 0.5f;

    private Enemy currentBoss;
    private float nextBossSearchTime;
    private float cachedHealthPercent = -1f;

    private void Update()
    {
        if (currentBoss == null ||
            currentBoss.IsDead)
        {
            currentBoss = null;

            Hide();

            if (Time.unscaledTime >= nextBossSearchTime)
            {
                nextBossSearchTime =
                    Time.unscaledTime + BossSearchInterval;

                FindBoss();
            }
        }

        if (currentBoss == null)
            return;

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
            Mathf.Clamp01(
                currentBoss.CurrentHealth /
                maxHealth
            );

        if (!Mathf.Approximately(healthPercent, cachedHealthPercent))
        {
            cachedHealthPercent = healthPercent;

            healthSlider.value = healthPercent;
        }

        if (bossNameText != null &&
            bossNameText.text != "BOSS")
        {
            bossNameText.text = "BOSS";
        }
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
        cachedHealthPercent = -1f;
    }
}