using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    public bool IsGameOver { get; private set; }

    private bool pendingRestart;
    private bool menuReturnPending;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingRestart)
        {
            pendingRestart = false;

            if (GameStateManager.Instance != null)
                GameStateManager.Instance.SetState(GameState.Playing);
        }
    }

    public void GameOver()
    {
        if (IsGameOver)
            return;

        IsGameOver = true;

        Debug.Log("[GameOver] entered; XpManager.Instance present: " + (XpManager.Instance != null) + ", IsRunActive: " + (XpManager.Instance != null ? XpManager.Instance.IsRunActive.ToString() : "n/a"));

        if (XpManager.Instance != null)
            XpManager.Instance.ProcessRunEnd();

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(GameState.GameOver);

        PlayGameOverSound();

        if (AudioManager.Instance != null)
            AudioManager.Instance.StopMusic();

        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        IsGameOver = false;
        pendingRestart = true;
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    public void BackToMenu()
    {
        IsGameOver = false;

        if (AudioManager.Instance != null)
            AudioManager.Instance.SwitchToMainMusic();

        Time.timeScale = 1f;

        MainMenuUI.MenuReloadPending = true;

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(GameState.Menu);

        WorldFadeOutManager fadeOut =
            FindAnyObjectByType<WorldFadeOutManager>();

        if (fadeOut != null)
            fadeOut.FadeOutEverything();

        if (!menuReturnPending)
            StartCoroutine(ReloadAfterCameraReachesMenu());
    }

    private IEnumerator ReloadAfterCameraReachesMenu()
    {
        menuReturnPending = true;

        const float maxWait = 4f;
        float timer = 0f;

        CameraFollow camera = FindAnyObjectByType<CameraFollow>();

        while (
            timer < maxWait &&
            camera != null &&
            camera.IsTransitioning
        )
        {
            timer += Time.unscaledDeltaTime;

            if (GameStateManager.Instance == null ||
                GameStateManager.Instance.CurrentState != GameState.Menu)
            {
                CancelMenuReturn();
                yield break;
            }

            yield return null;
        }

        if (GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameState.Menu)
        {
            CancelMenuReturn();
            yield break;
        }

        menuReturnPending = false;
        MainMenuUI.MenuReloadPending = false;
        MainMenuUI.ShowWithoutAnimation = true;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    private void CancelMenuReturn()
    {
        menuReturnPending = false;
        MainMenuUI.MenuReloadPending = false;
        MainMenuUI.ShowWithoutAnimation = false;
    }

    private void PlayGameOverSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.GameOver);
    }
}