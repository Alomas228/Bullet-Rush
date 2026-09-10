using UnityEngine;
using System.Collections;

public class WaveManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private WaveUI waveUI;
    [SerializeField] private UpgradeUI upgradeUI;
    [SerializeField] private WorldStructureGenerator worldGenerator;

    [Header("Wave Settings")]
    [SerializeField] private int startingEnemies = 5;
    [SerializeField] private int enemiesAddedPerWave = 3;

    [Header("Timing")]
    [SerializeField] private float waveDisplayTime = 1.5f;
    [SerializeField] private float prepareTime = 1.5f;
    [SerializeField] private float countdownStepTime = 1f;
    [SerializeField] private float timeBetweenWaves = 1f;

    public int CurrentWave { get; private set; }

    private bool waveActive;
    private bool waitingForNextWave;
    private bool gameStarted;

    private void Start()
    {
        if (waveUI != null)
            waveUI.Hide();

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged += HandleStateChanged;

            if (GameStateManager.Instance.CurrentState == GameState.Playing && !gameStarted)
            {
                gameStarted = true;
                CurrentWave = 0;
                StartCoroutine(StartWaveSequence());
            }
        }
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Playing && !gameStarted)
        {
            gameStarted = true;
            CurrentWave = 0;
            StartCoroutine(StartWaveSequence());
        }
        else if (state == GameState.Menu)
        {
            gameStarted = false;
            waveActive = false;
            waitingForNextWave = false;
            StopAllCoroutines();

            if (waveUI != null)
                waveUI.Hide();
        }
    }

    private void Update()
    {
        if (!waveActive || waitingForNextWave)
            return;

        if (GameStateManager.Instance != null
            && GameStateManager.Instance.CurrentState != GameState.Playing)
            return;

        GameObject[] enemies =
            GameObject.FindGameObjectsWithTag("Enemy");

        if (enemies.Length == 0)
        {
            waitingForNextWave = true;
            waveActive = false;

            if (upgradeUI != null)
            {
                upgradeUI.Show();
            }
            else
            {
                Debug.LogWarning(
                    "WaveManager: UpgradeUI is not assigned."
                );

                ContinueAfterUpgrade();
            }
        }
    }

    private IEnumerator StartWaveSequence()
    {
        CurrentWave++;
        waveActive = false;
        waitingForNextWave = false;

        if (worldGenerator != null)
            worldGenerator.GenerateForWave(CurrentWave);

        if (waveUI != null)
            waveUI.ShowWave(CurrentWave);

        yield return new WaitForSeconds(waveDisplayTime);

        if (waveUI != null)
            waveUI.ShowPrepare();

        yield return new WaitForSeconds(prepareTime);

        if (waveUI != null)
            waveUI.ShowCountdown(3);

        PlayCountdownSound();

        yield return new WaitForSeconds(countdownStepTime);

        if (waveUI != null)
            waveUI.ShowCountdown(2);

        PlayCountdownSound();

        yield return new WaitForSeconds(countdownStepTime);

        if (waveUI != null)
            waveUI.ShowCountdown(1);

        PlayCountdownSound();

        yield return new WaitForSeconds(countdownStepTime);

        if (waveUI != null)
            waveUI.Hide();

        SpawnCurrentWave();

        waveActive = true;
    }

    private IEnumerator StartNextWaveAfterDelay()
    {
        yield return new WaitForSeconds(timeBetweenWaves);

        StartCoroutine(StartWaveSequence());
    }

    private void SpawnCurrentWave()
    {
        int enemyCount =
            startingEnemies +
            (CurrentWave - 1) *
            enemiesAddedPerWave;

        Debug.Log(
            $"WAVE {CurrentWave} START"
        );

        PlayWaveStartSound();

        if (enemySpawner == null)
            return;

        enemySpawner.CurrentWave = CurrentWave;

        if (CurrentWave % 5 == 0)
        {
            SpawnBossWave();
            return;
        }

        enemySpawner.SpawnWave(
            enemyCount,
            CurrentWave
        );
    }

    private void SpawnBossWave()
    {
        Debug.Log(
            $"========== BOSS WAVE {CurrentWave} =========="
        );

        PlayBossSpawnSound();

        enemySpawner.SpawnEnemyAtPosition(
            EnemyType.Boss,
            GetBossSpawnPosition()
        );

        for (int i = 0; i < 5; i++)
        {
            enemySpawner.SpawnEnemyAtPosition(
                EnemyType.Normal,
                GetBossSpawnPosition()
            );
        }

        SwitchToBossMusic();
    }

    private Vector3 GetBossSpawnPosition()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
            return Vector3.zero;

        Vector2 randomDirection =
            Random.insideUnitCircle.normalized;

        return
            playerObject.transform.position +
            new Vector3(
                randomDirection.x,
                0f,
                randomDirection.y
            ) *
            12f;
    }

    public void ContinueAfterUpgrade()
    {
        if (!waitingForNextWave)
            return;

        Time.timeScale = 1f;

        if (upgradeUI != null)
            upgradeUI.Hide();

        waitingForNextWave = false;

        StartCoroutine(StartNextWaveAfterDelay());
    }

    private void PlayCountdownSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.CountdownTick);
    }

    private void PlayWaveStartSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.WaveStart);
    }

    private void PlayBossSpawnSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.BossSpawn);
    }

    private void SwitchToBossMusic()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SwitchToBossMusic();
    }

    private void SwitchToMainMusic()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SwitchToMainMusic();
    }
}
