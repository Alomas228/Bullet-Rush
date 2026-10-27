using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;
using System.Collections;
using System.Linq;
#if Leaderboards_yg
using YG.Utils.LB;
#endif

/// <summary>
/// Лидерборд на экране меню: показывает топ-N игроков и обновляется
/// при каждом появлении меню (через MainMenuUI.OnMenuAppeared) и при загрузке SDK.
/// Если leaderboardText не назначен в сцене — текст создастся автоматически
/// на панели меню (режим autoCreate).
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    [Header("Leaderboard")]
    [Tooltip("TechnoName лидерборда на консоли Яндекс Игр (должен совпадать с созданным).")]
    [SerializeField] private string leaderboardName = "BestScore";

    [Tooltip("Сколько топ-записей запрашивать у сервера (вместе с записями вокруг игрока).")]
    [Min(1)]
    [SerializeField] private int topCount = 10;

    [Tooltip("Сколько записей показывать вокруг текущего игрока.")]
    [Min(0)]
    [SerializeField] private int aroundCount = 1;

    [Header("Text")]
    [Tooltip("Текст таблицы. Пусто — создаётся автоматически (см. Auto Create).")]
    [SerializeField] private TMP_Text leaderboardText;

    [Header("Auto Create")]
    [Tooltip("Создавать лидерборд автоматически, если leaderboardText не назначен.")]
    [SerializeField] private bool autoCreate = true;

    [Tooltip("Панель меню, на которую вешать авто-текст (обычно menuPanel из MainMenuUI).")]
    [SerializeField] private GameObject menuPanel;

    [Header("Auto Create Placement")]
    [SerializeField] private Vector2 autoAnchoredPosition = new Vector2(-24f, -24f);
    [SerializeField] private Vector2 autoSize = new Vector2(360f, 340f);
    [SerializeField] private int autoFontSize = 16;
    [SerializeField] private float autoLineSpacing = 1f;

    [Header("Loading / Empty")]
    [Tooltip("Ключи переводов вида {lb.loading}. Разворачиваются в язык, определённый при запуске.")]
    [SerializeField] private string loadingText = "{lb.loading}";
    [SerializeField] private string emptyText = "{lb.empty}";
    [SerializeField] private string noAuthText = "{lb.no_auth}";

    private bool ready;
    private Coroutine refreshRoutine;
    private bool showLeague;
    private bool visible;
    private bool visibilityControlled;
    private bool requestInProgress;
    private bool refreshPending;

    public void Configure(string name, TMP_Text target, bool includeLeague)
    {
        leaderboardName = name;
        leaderboardText = target;
        autoCreate = false;
        showLeague = includeLeague;
        visibilityControlled = true;
        ready = YandexGameManager.Instance != null && YandexGameManager.Instance.IsReady;
    }

    public void SetVisible(bool value)
    {
        visible = value;
        if (!visible)
            refreshPending = false;
    }

    private void OnDestroy()
    {
        if (YandexGameManager.Instance != null)
            YandexGameManager.Instance.OnReady -= OnSDKReady;
        MainMenuUI.OnMenuAppeared -= OnMenuAppeared;

        if (refreshRoutine != null)
            StopCoroutine(refreshRoutine);
    }

    private void OnEnable()
    {
        if (YandexGameManager.Instance != null)
            YandexGameManager.Instance.OnReady += OnSDKReady;

        MainMenuUI.OnMenuAppeared += OnMenuAppeared;
    }

    private void OnDisable()
    {
        if (YandexGameManager.Instance != null)
            YandexGameManager.Instance.OnReady -= OnSDKReady;

        MainMenuUI.OnMenuAppeared -= OnMenuAppeared;

        if (refreshRoutine != null)
        {
            StopCoroutine(refreshRoutine);
            refreshRoutine = null;
        }
        requestInProgress = false;
    }

    private void Start()
    {
        EnsureText();

        if (leaderboardText == null)
            return;

        if (YandexGameManager.Instance != null &&
            YandexGameManager.Instance.IsReady)
        {
            ready = true;
        }

        if (IsVisible && ready && MainMenuUI.Instance != null &&
            GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Menu)
        {
            Refresh();
        }
    }

    private void OnSDKReady()
    {
        ready = true;
        if (IsVisible)
            Refresh();
    }

    private void OnMenuAppeared()
    {
        if (IsVisible)
            Refresh();
    }

    /// <summary>Запрашивает и отрисовывает таблицу заново.</summary>
    public void Refresh()
    {
        if (!IsVisible || leaderboardText == null)
            return;

        if (YandexGameManager.Instance == null || !ready)
        {
            leaderboardText.text = LangBinder.ResolveKey(loadingText);
            return;
        }

        if (requestInProgress)
        {
            refreshPending = true;
            return;
        }

        SetText(loadingText);
        requestInProgress = true;
        refreshRoutine = StartCoroutine(RequestRoutine(leaderboardName, showLeague));
    }

    private IEnumerator RequestRoutine(string requestedName, bool includeLeague)
    {
        bool done = false;
        LBData result = null;

        YandexGameManager.Instance.RequestLeaderboard(
            requestedName,
            topCount,
            aroundCount,
            lb =>
            {
                result = lb;
                done = true;
            }
        );

        // У Яндекса данные приходят асинхронно; ждём коллбек.
        float timeout = 10f;
        float timer = 0f;

        while (!done && timer < timeout)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        refreshRoutine = null;
        requestInProgress = false;

        if (IsVisible && requestedName == leaderboardName && !done)
        {
            SetText(emptyText);
        }
        else if (IsVisible && requestedName == leaderboardName)
        {
            SetText(Render(result, includeLeague));
        }

        if (refreshPending || requestedName != leaderboardName)
        {
            refreshPending = false;
            Refresh();
        }
    }

    /// <summary>
    /// Отрисовывает таблицу от самого высокого счёта к низкому:
    /// №1 = максимальный балл, дальше по убыванию.
    /// </summary>
    private string Render(LBData lb, bool includeLeague)
    {
        if (lb == null || lb.players == null || lb.players.Length == 0)
            return LangBinder.ResolveKey(emptyText);

        var sorted = lb.players
            .Where(player => player != null && !string.IsNullOrEmpty(player.name))
            .OrderByDescending(player => player.score)
            .ToList();

        if (sorted.Count == 0)
            return LangBinder.ResolveKey(emptyText);

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < sorted.Count; i++)
        {
            string name = LBMethods.AnonymousName(sorted[i].name);

            string score = includeLeague
                ? PvpProgressionManager.GetLeague(sorted[i].score) + " · " + sorted[i].score
                : sorted[i].score.ToString();
            sb.AppendLine(Lang.Get("lb.row", i + 1, name, score));
        }

        if (!YandexGameManager.Instance.IsAuthorized)
        {
            sb.AppendLine();
            sb.AppendLine(LangBinder.ResolveKey(noAuthText));
        }

        return sb.ToString();
    }

    private void SetText(string text)
    {
        if (leaderboardText != null)
            leaderboardText.text = LangBinder.ResolveKey(text);
    }

    private bool IsVisible => !visibilityControlled || visible;

    // =========================================================
    // AUTO CREATE
    // =========================================================

    private void EnsureText()
    {
        if (leaderboardText != null)
            return;

        if (!autoCreate || menuPanel == null)
            return;

        GameObject textObj = new GameObject("LeaderboardText");
        textObj.transform.SetParent(menuPanel.transform, false);

        leaderboardText = textObj.AddComponent<TextMeshProUGUI>();
        leaderboardText.fontSize = autoFontSize;
        leaderboardText.lineSpacing = autoLineSpacing;
        leaderboardText.textWrappingMode = TextWrappingModes.Normal;
        leaderboardText.alignment = TextAlignmentOptions.TopLeft;

        RectTransform rect = leaderboardText.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = autoAnchoredPosition;
        rect.sizeDelta = autoSize;
    }
}
