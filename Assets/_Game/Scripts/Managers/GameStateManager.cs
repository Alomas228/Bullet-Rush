using UnityEngine;
using System;
using System.Collections;

public enum GameState
{
    Menu,
    Playing,
    GameOver
}

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public GameState CurrentState { get; private set; } = GameState.Menu;

    public event Action<GameState> OnGameStateChanged;
    private Coroutine pendingStart;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState)
            return;

        CurrentState = newState;

        Debug.Log($"Game state -> {newState}");

        OnGameStateChanged?.Invoke(newState);
    }

    public void StartGame()
    {
        if (CurrentState != GameState.Menu || pendingStart != null) return;
        EnvironmentController environment = FindAnyObjectByType<EnvironmentController>();
        if (environment != null && environment.IsMenuPreparationPending)
        {
            pendingStart = StartCoroutine(StartWhenPrepared(environment, GameModeManager.SelectedMode));
            return;
        }
        StartPreparedSession();
    }

    private IEnumerator StartWhenPrepared(EnvironmentController environment, GameMode mode)
    {
        yield return null;
        while (environment != null && environment.IsMenuPreparationPending)
        {
            if (CurrentState != GameState.Menu || GameModeManager.SelectedMode != mode)
            {
                pendingStart = null;
                if (CurrentState == GameState.Menu) MainMenuUI.Instance?.ShowMenu();
                yield break;
            }
            yield return null;
        }
        pendingStart = null;
        if (CurrentState == GameState.Menu && GameModeManager.SelectedMode == mode) StartPreparedSession();
        else if (CurrentState == GameState.Menu) MainMenuUI.Instance?.ShowMenu();
    }

    private void StartPreparedSession()
    {
        if (!RunSessionResetter.PrepareForGameplay())
        {
            MainMenuUI.Instance?.ShowMenu();
            return;
        }
        GameOverManager.Instance?.ResetForNewRun();
        Time.timeScale = 1f;
        SetState(GameState.Playing);
    }

    public void ReturnToMenu()
    {
        if (CurrentState == GameState.Menu) return;
        if (GameOverManager.Instance != null)
        {
            GameOverManager.Instance.BackToMenu();
            return;
        }
        SetState(GameState.Menu);
        Time.timeScale = 0f;
        RunSessionResetter.ReturnToMenu();
    }
}
