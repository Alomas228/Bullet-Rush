using UnityEngine;

// A small timed objective that asks the player to leave the firing spot.
public class TimedCourierObjective : MonoBehaviour
{
    private Transform player;
    private WaveUI waveUI;
    private float remaining;
    private int rewardCoins;
    private float collectDistance;
    private System.Action<bool> completed;
    private bool finished;
    private int lastDisplayedSecond = -1;

    public void Initialize(
        Transform target,
        WaveUI ui,
        float duration,
        float distance,
        int coins,
        System.Action<bool> onCompleted)
    {
        player = target;
        waveUI = ui;
        remaining = Mathf.Max(duration, 1f);
        collectDistance = Mathf.Max(distance, 0.5f);
        rewardCoins = coins;
        completed = onCompleted;

        transform.position = new Vector3(transform.position.x, 0.08f, transform.position.z);
        transform.localScale = new Vector3(1.4f, 0.12f, 1.4f);
        ShowStatus();
    }

    private void Update()
    {
        if (finished)
            return;

        remaining -= Time.deltaTime;
        ShowStatus();

        if (player != null &&
            Vector3.SqrMagnitude(player.position - transform.position) <=
            collectDistance * collectDistance)
        {
            Finish(true);
            return;
        }

        if (remaining <= 0f)
            Finish(false);
    }

    private void ShowStatus()
    {
        int seconds = Mathf.CeilToInt(Mathf.Max(remaining, 0f));
        if (waveUI != null && seconds != lastDisplayedSecond)
        {
            waveUI.ShowEvent(Lang.Get("wave.event_courier_timer", seconds));
            lastDisplayedSecond = seconds;
        }
    }

    private void Finish(bool success)
    {
        if (finished)
            return;

        finished = true;

        if (success && XpManager.Instance != null)
            XpManager.Instance.AddCoins(rewardCoins);

        if (waveUI != null)
        {
            waveUI.ShowEvent(success
                ? Lang.Get("wave.event_courier_success", rewardCoins)
                : Lang.Get("wave.event_courier_failed"));
        }

        completed?.Invoke(success);
        Destroy(gameObject);
    }
}
