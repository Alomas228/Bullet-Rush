using UnityEngine;
using UnityEngine.InputSystem;

public class AbilityManager : MonoBehaviour
{
    [Header("Abilities")]
    [Tooltip("Не обязательно назначать вручную — найдутся на игроке.")]
    [SerializeField] private BombAbility bombAbility;
    [SerializeField] private ShieldAbility shieldAbility;

    private bool inputEnabled;
    private bool subscribed;

    public bool HasBomb =>
        bombAbility != null && bombAbility.IsUnlocked;

    public bool HasShield =>
        shieldAbility != null && shieldAbility.IsUnlocked;

    public BombAbility Bomb => bombAbility;
    public ShieldAbility Shield => shieldAbility;

    private void Awake()
    {
        if (bombAbility == null)
            bombAbility = GetComponent<BombAbility>();

        if (shieldAbility == null)
            shieldAbility = GetComponent<ShieldAbility>();
    }

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void Start()
    {
        EnsureSubscribed();
    }

    private void EnsureSubscribed()
    {
        if (subscribed)
            return;

        if (GameStateManager.Instance == null)
            return;

        GameStateManager.Instance.OnGameStateChanged += HandleStateChanged;
        subscribed = true;

        inputEnabled =
            GameStateManager.Instance.CurrentState == GameState.Playing;
    }

    private void OnDisable()
    {
        if (subscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;
            subscribed = false;
        }
    }

    private void HandleStateChanged(GameState state)
    {
        inputEnabled = state == GameState.Playing;
    }

    private void Update()
    {
        if (!inputEnabled)
            return;

        HandleBombInput();
        HandleShieldInput();
    }

    private void HandleBombInput()
    {
        if (bombAbility == null)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
            bombAbility.TryActivate();
    }

    private void HandleShieldInput()
    {
        if (shieldAbility == null)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.qKey.wasPressedThisFrame)
            shieldAbility.TryActivate();
    }

    public bool UnlockBomb()
    {
        if (bombAbility == null)
        {
            bombAbility =
                gameObject.AddComponent<BombAbility>();
        }

        if (bombAbility != null)
            bombAbility.IsUnlocked = true;

        return bombAbility != null;
    }

    public bool UnlockShield()
    {
        if (shieldAbility == null)
        {
            shieldAbility =
                gameObject.AddComponent<ShieldAbility>();
        }

        if (shieldAbility != null)
            shieldAbility.IsUnlocked = true;

        return shieldAbility != null;
    }
}