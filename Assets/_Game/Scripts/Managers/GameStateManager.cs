using UnityEngine;
using System;

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
        SetState(GameState.Playing);
    }

    public void ReturnToMenu()
    {
        SetState(GameState.Menu);
    }
}
