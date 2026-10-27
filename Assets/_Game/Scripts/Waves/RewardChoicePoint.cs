using UnityEngine;
using UnityEngine.InputSystem;

// Temporary field cache. The choice is deliberately keyboard-based to match
// the project's existing input path and does not require a Canvas prefab.
public class RewardChoicePoint : MonoBehaviour
{
    private Transform player;
    private WaveUI waveUI;
    private float lifetime;
    private bool chosen;
    private System.Action completed;
    private int lastDisplayedSecond = -1;

    public void Initialize(
        Transform target,
        WaveUI ui,
        float duration,
        System.Action onCompleted)
    {
        player = target;
        waveUI = ui;
        lifetime = Mathf.Max(duration, 1f);
        completed = onCompleted;
        transform.position = new Vector3(transform.position.x, 0.6f, transform.position.z);
        transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
    }

    private void Update()
    {
        if (chosen)
            return;

        lifetime -= Time.deltaTime;

        if (lifetime <= 0f)
        {
            completed?.Invoke();
            Destroy(gameObject);
            return;
        }

        bool nearby = player != null &&
            Vector3.SqrMagnitude(player.position - transform.position) <= 4f;
        int seconds = Mathf.CeilToInt(lifetime);
        if (seconds != lastDisplayedSecond)
            ShowPrompt(seconds);

        if (!nearby)
            return;

        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState != GameState.Playing)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame ||
            Keyboard.current.numpad1Key.wasPressedThisFrame)
        {
            Choose(0);
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame ||
                 Keyboard.current.numpad2Key.wasPressedThisFrame)
        {
            Choose(1);
        }
    }

    private void ShowPrompt(int seconds)
    {
        if (waveUI != null)
        {
            waveUI.ShowEvent(Lang.Get("wave.reward_point", seconds));
            lastDisplayedSecond = seconds;
        }
    }

    private void Choose(int option)
    {
        chosen = true;

        PlayerStats stats = player != null
            ? player.GetComponent<PlayerStats>()
            : null;
        PlayerHealth health = PlayerHealth.Instance;

        if (option == 0)
        {
            if (stats != null)
                stats.AddDamagePercent(0.12f);
            if (waveUI != null)
                waveUI.ShowEvent(Lang.Get("wave.reward_damage"));
        }
        else
        {
            if (health != null)
                health.Heal(health.MaxHealth * 0.25f);
            if (XpManager.Instance != null)
                XpManager.Instance.AddCoins(8);
            if (waveUI != null)
                waveUI.ShowEvent(Lang.Get("wave.reward_recovery"));
        }

        completed?.Invoke();
        Destroy(gameObject, 1.2f);
    }
}
