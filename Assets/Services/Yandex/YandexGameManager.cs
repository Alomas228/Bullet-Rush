using System;
using System.Collections;
using UnityEngine;
using YG;
#if Leaderboards_yg
using YG.Utils.LB;
#endif

/// <summary>
/// Единая точка интеграции с PluginYourGames (YG2): авторизация, реклама,
/// Gameplay API, облачные сохранения и лидерборды.
/// Вешается на любой объект в сцене (один, сохраняется между сценами).
/// </summary>
public class YandexGameManager : MonoBehaviour
{
    public static YandexGameManager Instance { get; private set; }

    [Header("Interstitial Adv")]
    [Tooltip("Показывать межстраничную рекламу каждые N завершённых забегов (0 = выключено).")]
    [Min(0)]
    [SerializeField] private int interRunInterval = 3;

    [Tooltip("Задержка показа рекламы после Game Over (сек), чтобы игрок увидел свои результаты.")]
    [SerializeField] private float interstitialDelayAfterGameOver = 1.2f;

    [Header("Leaderboard")]
    [Tooltip("TechnoName лидерборда на консоли Яндекс Игр. Пусто = запись рекордов выключена.")]
    [SerializeField] private string leaderboardName = "BestScore";

    /// <summary>SDK получил данные и прогресс загружен.</summary>
    public bool IsReady { get; private set; }

    public bool IsAuthorized => YG2.player.auth;
    public string PlayerName => YG2.player.name;
    public string PlayerId => YG2.player.id;

    /// <summary>Лучший счёт за всё время (из облачных сохранений).</summary>
    public int BestScore { get; private set; }

    /// <summary>Количество завершённых забегов (для интерстишла).</summary>
    public int RunCount { get; private set; }

    public event Action OnReady;
    public event Action OnBestScoreChanged;
    public event Action<bool> OnAuthorizedChanged;

    public const string RewardId_DoubleCoins = "double_coins";
    public const string RewardId_Revive = "revive";

    private Action pendingReward;
    private string pendingRewardId;
    private bool stateSubscribed;
    private bool pauseSubscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ApplySaves();

#if InterstitialAdv_yg
        YG2.onOpenInterAdv += OnOpenInterstitial;
        YG2.onCloseInterAdv += OnCloseInterstitial;
        YG2.onErrorInterAdv += OnErrorInterstitial;
#endif
#if RewardedAdv_yg
        YG2.onRewardAdv += OnRewardAdv;
        YG2.onErrorRewardedAdv += OnErrorRewardedAdv;
#endif

        YG2.onGetSDKData += OnSDKData;
        YG2.onPauseGame += HandleYandexPause;

        if (YG2.isSDKEnabled)
            OnSDKData();
    }

    private void OnDestroy()
    {
#if InterstitialAdv_yg
        YG2.onOpenInterAdv -= OnOpenInterstitial;
        YG2.onCloseInterAdv -= OnCloseInterstitial;
        YG2.onErrorInterAdv -= OnErrorInterstitial;
#endif
#if RewardedAdv_yg
        YG2.onRewardAdv -= OnRewardAdv;
        YG2.onErrorRewardedAdv -= OnErrorRewardedAdv;
#endif
        YG2.onGetSDKData -= OnSDKData;
        YG2.onPauseGame -= HandleYandexPause;

        if (stateSubscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;
            stateSubscribed = false;
        }

        if (pauseSubscribed && PauseManager.Instance != null)
        {
            PauseManager.Instance.OnPauseChanged -= HandlePauseChanged;
            pauseSubscribed = false;
        }
    }

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void Update()
    {
        EnsureSubscribed();
    }

    // =========================================================
    // INIT
    // =========================================================

    private void EnsureSubscribed()
    {
        if (GameStateManager.Instance == null)
        {
            stateSubscribed = false;
        }
        else if (!stateSubscribed)
        {
            GameStateManager.Instance.OnGameStateChanged += HandleStateChanged;
            stateSubscribed = true;
            HandleStateChanged(GameStateManager.Instance.CurrentState);
        }

        if (PauseManager.Instance == null)
        {
            pauseSubscribed = false;
        }
        else if (!pauseSubscribed)
        {
            PauseManager.Instance.OnPauseChanged += HandlePauseChanged;
            pauseSubscribed = true;
        }
    }

    private void OnSDKData()
    {
        YG2.onGetSDKData -= OnSDKData;

        ApplySaves();
        IsReady = true;

        OnReady?.Invoke();
        OnAuthorizedChanged?.Invoke(IsAuthorized);
        OnBestScoreChanged?.Invoke();
    }

    private void ApplySaves()
    {
#if Storage_yg
        BestScore = Mathf.Max(YG2.saves.bestScore, 0);
        RunCount = Mathf.Max(YG2.saves.runCount, 0);
#endif
    }

    private void PersistSaves()
    {
#if Storage_yg
        YG2.saves.bestScore = BestScore;
        YG2.saves.runCount = RunCount;

        if (YG2.isSDKEnabled)
            YG2.SaveProgress();
#endif
    }

    // =========================================================
    // GAMEPLAY STATE -> Gameplay API
    // =========================================================

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Playing)
        {
            YG2.GameplayStart();
        }
        else if (state == GameState.GameOver)
        {
            YG2.GameplayStop();
            RegisterRunEnd();
        }
        else
        {
            YG2.GameplayStop();
        }
    }

    private void HandlePauseChanged(bool paused)
    {
        if (paused)
            YG2.GameplayStop();
        else
            YG2.GameplayStart();
    }

    /// <summary>
    /// Реагирует на паузу/возобновление, которое шлёт сама платформа
    /// (реклама, сворачивание окна). Чинит гонку с timeScale:
    /// PauseGameYG запоминает timeScale в момент открытия рекламы (при revайве это
    /// зачастую 0 из-за Game Over) и на закрытии восстанавливает его ПОСЛЕ нашего
    /// RevivePlayer, замораживая игру. Поэтому после resume принудительно
    /// возвращаем timeScale = 1, если мы в бою и наша пауза не активна.
    /// </summary>
    private void HandleYandexPause(bool paused)
    {
        if (paused)
            return;

        StartCoroutine(RestoreTimeScaleAfterPlatformPause());
    }

    private System.Collections.IEnumerator RestoreTimeScaleAfterPlatformPause()
    {
        yield return null;
        yield return null;

        if (GameStateManager.Instance == null)
            yield break;

        if (GameStateManager.Instance.CurrentState != GameState.Playing)
            yield break;

        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            yield break;

        if (Time.timeScale == 0f)
            Time.timeScale = 1f;
    }

    // =========================================================
    // RUN END (забег завершён)
    // =========================================================

    /// <summary>
    /// Вызывается автоматически при переходе в GameOver. Учитывает забег,
    /// обновляет рекорд, пишет лидерборд и показывает интерстишл каждые N забегов.
    /// </summary>
    public void RegisterRunEnd()
    {
        if (!IsReady)
            return;

        int finalScore =
            ScoreManager.Instance != null
                ? ScoreManager.Instance.Score
                : 0;

        #if Storage_yg
        RunCount++;
        #endif

        if (finalScore > BestScore)
        {
            BestScore = finalScore;
            OnBestScoreChanged?.Invoke();
        }

        PersistSaves();
        SubmitLeaderboardScore(BestScore);

        if (interRunInterval > 0 &&
            RunCount > 0 &&
            RunCount % interRunInterval == 0)
        {
            StartCoroutine(ShowInterstitialDelayed(interstitialDelayAfterGameOver));
        }
    }

    private void SubmitLeaderboardScore(int score)
    {
#if Leaderboards_yg
        if (string.IsNullOrEmpty(leaderboardName))
            return;

        if (!YG2.player.auth)
            return;

        YG2.SetLeaderboard(leaderboardName, score);
#endif
    }

    // =========================================================
    // INTERSTITIAL
    // =========================================================

    public void ShowInterstitial()
    {
#if InterstitialAdv_yg
        if (!IsReady || YG2.nowAdsShow)
            return;

        YG2.InterstitialAdvShow();
#endif
    }

    private IEnumerator ShowInterstitialDelayed(float delay)
    {
        float time = 0f;

        while (time < delay)
        {
            time += Time.unscaledDeltaTime;
            yield return null;
        }

        ShowInterstitial();
    }

    private void OnOpenInterstitial()
    {
        YG2.GameplayStop();
    }

    private void OnCloseInterstitial()
    {
        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Playing)
        {
            YG2.GameplayStart();
        }
    }

    private void OnErrorInterstitial()
    {
    }

    // =========================================================
    // REWARDED
    // =========================================================

    /// <summary>
    /// Показывает rewarded-рекламу. onReward вызывается ТОЛЬКО при успешном
    /// досмотре (награда не выдаётся, если реклама закрыта досрочно или сломана).
    /// </summary>
    public void ShowRewarded(string id, Action onReward)
    {
#if RewardedAdv_yg
        if (!IsReady)
        {
            onReward?.Invoke();
            return;
        }

        if (pendingReward != null)
            return;

        if (YG2.nowInterAdv || YG2.nowRewardAdv)
            return;

        pendingReward = onReward;
        pendingRewardId = id;
        YG2.RewardedAdvShow(id);
#else
        onReward?.Invoke();
#endif
    }

    private void OnRewardAdv(string id)
    {
        if (pendingReward == null)
            return;

        if (id != pendingRewardId && !string.IsNullOrEmpty(id))
            return;

        Action reward = pendingReward;
        pendingReward = null;
        pendingRewardId = null;

        reward?.Invoke();
    }

    private void OnErrorRewardedAdv()
    {
        pendingReward = null;
        pendingRewardId = null;
    }

    // =========================================================
    // AUTHORIZATION
    // =========================================================

    public void OpenAuthDialog()
    {
        if (!IsReady || IsAuthorized)
            return;

        YG2.OpenAuthDialog();
    }

    // =========================================================
    // LEADERBOARD QUERY
    // =========================================================

    /// <summary>
    /// Запрашивает топ лидерборда. onResult: null при ошибке/выключенном модуле.
    /// </summary>
    public void RequestLeaderboard(string nameLB, int quantityTop, int quantityAround, Action<LBData> onResult)
    {
#if Leaderboards_yg
        void Handler(LBData lb)
        {
            YG2.onGetLeaderboard -= Handler;
            onResult?.Invoke(lb);
        }

        YG2.onGetLeaderboard += Handler;
        YG2.GetLeaderboard(nameLB, quantityTop, quantityAround);
#else
        onResult?.Invoke(null);
#endif
    }
}