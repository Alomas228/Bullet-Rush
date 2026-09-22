using System.Collections;
using UnityEngine;

// Случайные события посреди волны: вынуждают игрока двигаться,
// а не стоять на месте. Одно событие на волну, типы не повторяются
// подряд из-за страховки от однообразия.
public class WaveEventDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private WaveUI waveUI;

    [Header("Scheduling")]
    [Tooltip("С какой волны начинаются события.")]
    [SerializeField] private int minEventWave = 3;
    [Tooltip("Вероятность события в пригодной волне.")]
    [SerializeField] private float eventChancePerWave = 0.9f;
    [Tooltip("Минимальная задержка события после начала волны.")]
    [SerializeField] private float eventMinDelay = 5f;
    [Tooltip("Максимальная задержка события после начала волны.")]
    [SerializeField] private float eventMaxDelay = 11f;

    [Header("Ambush Event")]
    [Tooltip("Базовое число рванувших к игроку врагов.")]
    [SerializeField] private int ambushBaseCount = 4;
    [Tooltip("Добавляется за каждые N волн (начиная с минимальной).")]
    [SerializeField] private int ambushCountPerWave = 3;
    [SerializeField] private float ambushMinDistance = 9f;
    [SerializeField] private float ambushMaxDistance = 13f;
    [Tooltip("Потолок числа врагов в амбуше.")]
    [SerializeField] private int ambushMaxCount = 10;

    [Header("Rush Event")]
    [Tooltip("Стенда с одного фланга.")]
    [SerializeField] private int rushBaseCount = 6;
    [SerializeField] private int rushCountPerWave = 3;
    [Tooltip("Ширина дуги, из которой выходит пачка (градусы).")]
    [SerializeField] private float rushArcDegrees = 70f;
    [Tooltip("Потолок числа врагов в пачке.")]
    [SerializeField] private int rushMaxCount = 12;

    [Header("Danger Zone Event")]
    [SerializeField] private float zoneWarning = 1.4f;
    [SerializeField] private float zoneRadius = 3f;
    [SerializeField] private float zoneDamagePerSecond = 3f;
    [SerializeField] private float zoneDuration = 4f;
    [SerializeField] private float zoneGrowMultiplier = 1.8f;

    [Header("Presentation")]
    [SerializeField] private float bannerDuration = 1.6f;

    private enum WaveEventType
    {
        Ambush,
        DangerZone,
        Rush
    }

    private WaveEventType lastEventType;
    private bool waveRunning;
    private Coroutine scheduleCoroutine;

    public void Initialize(
        EnemySpawner spawner,
        WaveUI ui)
    {
        enemySpawner = spawner;
        waveUI = ui;
    }

    // Вызывается менеджером волн при старте волны.
    public void OnWaveStarted(
        int wave,
        bool allowEvents)
    {
        Stop();

        if (!allowEvents)
            return;

        if (wave < minEventWave)
            return;

        waveRunning = true;

        scheduleCoroutine = StartCoroutine(
            ScheduleEvent(wave)
        );
    }

    // Вызывается при завершении волны.
    public void OnWaveEnded()
    {
        waveRunning = false;

        CancelSchedule();
    }

    // Полная остановка (меню, стоп игры).
    public void Stop()
    {
        waveRunning = false;

        CancelSchedule();
    }

    private void CancelSchedule()
    {
        if (scheduleCoroutine != null)
        {
            StopCoroutine(scheduleCoroutine);
            scheduleCoroutine = null;
        }
    }

    private IEnumerator ScheduleEvent(int wave)
    {
        if (Random.value > eventChancePerWave)
            yield break;

        float delay =
            Random.Range(
                eventMinDelay,
                eventMaxDelay
            );

        yield return new WaitForSeconds(delay);

        if (!CanRunEvent())
            yield break;

        WaveEventType type =
            PickEventType();

        yield return RunEvent(type, wave);
    }

    private bool CanRunEvent()
    {
        if (!waveRunning)
            return false;

        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState !=
            GameState.Playing)
        {
            return false;
        }

        // Событие бессмысленно, если волна почти закончилась
        // и живых врагов уже нет.
        if (Enemy.AliveCount <= 0)
            return false;

        return true;
    }

    // Не повторяет тип события, если оно только что было.
    private WaveEventType PickEventType()
    {
        int roll =
            Random.Range(0, 3);

        if (roll == (int)lastEventType)
        {
            roll =
                (roll + 1 + Random.Range(0, 2)) %
                3;
        }

        lastEventType =
            (WaveEventType)roll;

        return (WaveEventType)roll;
    }

    private IEnumerator RunEvent(
        WaveEventType type,
        int wave)
    {
        switch (type)
        {
            case WaveEventType.Ambush:
                RunAmbush(wave);
                break;

            case WaveEventType.Rush:
                RunRush(wave);
                break;

            case WaveEventType.DangerZone:
                SpawnDangerZone();
                break;
        }

        yield return new WaitForSeconds(bannerDuration);

        if (waveUI != null)
            waveUI.Hide();
    }

    private void RunAmbush(int wave)
    {
        int count =
            ambushBaseCount +
            Mathf.Max(wave - minEventWave, 0) /
            Mathf.Max(ambushCountPerWave, 1);

        count = Mathf.Min(count, ambushMaxCount);

        if (enemySpawner != null)
        {
            enemySpawner.SpawnAmbush(
                count,
                ambushMinDistance,
                ambushMaxDistance
            );
        }

        ShowBanner("ЗАСАДА!");
    }

    private void RunRush(int wave)
    {
        int count =
            rushBaseCount +
            Mathf.Max(wave - minEventWave, 0) /
            Mathf.Max(rushCountPerWave, 1);

        count = Mathf.Min(count, rushMaxCount);

        if (enemySpawner != null)
        {
            enemySpawner.SpawnRushPack(
                count,
                rushArcDegrees,
                wave
            );
        }

        ShowBanner("РЫВОК!");
    }

    private void SpawnDangerZone()
    {
        PlayerHealth playerHealth =
            PlayerHealth.Instance;

        if (playerHealth == null)
            return;

        Vector2 offset =
            Random.insideUnitCircle *
            4f;

        Vector3 position =
            playerHealth.transform.position +
            new Vector3(
                offset.x,
                0f,
                offset.y
            );

        position.y = 0.02f;

        GameObject zoneObject =
            new GameObject("HazardZone_Event");

        zoneObject.transform.position = position;
        zoneObject.transform.rotation = Quaternion.identity;

        HazardZone zone =
            zoneObject.AddComponent<HazardZone>();

        zone.Initialize(
            zoneWarning,
            zoneRadius,
            zoneDamagePerSecond,
            zoneDuration,
            zoneGrowMultiplier
        );

        ShowBanner("ОПАСНОСТЬ!");
    }

    private void ShowBanner(string label)
    {
        if (waveUI != null)
            waveUI.ShowEvent(label);
    }
}