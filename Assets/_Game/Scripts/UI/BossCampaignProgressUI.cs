using UnityEngine;

// The persistent wave/boss status is part of GameplayUI's vitals now.
// Only reserve space for the boss HP bar.
public sealed class BossCampaignProgressUI : MonoBehaviour
{
    private WaveManager waves;
    private RectTransform canvasRect, bossPanel;
    private float lastWidth = -1f;
    private bool hadBoss;

    public void Bind(WaveManager manager) => waves = manager;

    private void LateUpdate()
    {
        if (waves == null || GameModeManager.IsPvp || GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameState.Playing) return;

        if (canvasRect == null)
        {
            BossHealthBar healthBar = FindAnyObjectByType<BossHealthBar>(FindObjectsInactive.Include);
            Canvas canvas = healthBar != null ? healthBar.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return;
            canvasRect = (RectTransform)canvas.transform;
            bossPanel = canvasRect.Find("HUD/GameplayUI/BossHpPanel") as RectTransform;
            lastWidth = -1f;
        }

        float width = bossPanel != null && bossPanel.parent is RectTransform parent
            ? parent.rect.width : ResponsiveUi.SafeRect(canvasRect).width;
        bool bossVisible = bossPanel != null && bossPanel.gameObject.activeInHierarchy;
        if (width <= 36f || (Mathf.Approximately(width, lastWidth) && bossVisible == hadBoss)) return;
        lastWidth = width;
        hadBoss = bossVisible;

        // Keep the centre clear of vitals, active boosts and personal-best text.
        float top = width >= 1660f ? 16f : width >= 1360f ? 176f : 278f;
        var anchor = new Vector2(.5f, 1f);
        if (bossPanel != null)
        {
            UiArtboard.Place(bossPanel, anchor, anchor, new Vector2(0f, -top), new Vector2(660f, 102f));
            bossPanel.localScale = Vector3.one * Mathf.Clamp01((width - 36f) / 660f);
        }

    }
}
