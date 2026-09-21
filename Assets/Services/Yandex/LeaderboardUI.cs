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
    [SerializeField] private string loadingText = "LOADING...";
    [SerializeField] private string emptyText = "LEADERBOARD IS EMPTY";
    [SerializeField] private string noAuthText = "SIGN IN TO SEE YOUR RANK";

    private bool ready;
    private Coroutine refreshRoutine;

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

        if (ready && MainMenuUI.Instance != null &&
            GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Menu)
        {
            Refresh();
        }
    }

    private void OnSDKReady()
    {
        ready = true;
        Refresh();
    }

    private void OnMenuAppeared()
    {
        Refresh();
    }

    /// <summary>Запрашивает и отрисовывает таблицу заново.</summary>
    public void Refresh()
    {
        if (leaderboardText == null)
            return;

        if (YandexGameManager.Instance == null || !ready)
        {
            leaderboardText.text = loadingText;
            return;
        }

        SetText(loadingText);

        if (refreshRoutine != null)
        {
            StopCoroutine(refreshRoutine);
            refreshRoutine = null;
        }

        refreshRoutine = StartCoroutine(RequestRoutine());
    }

    private IEnumerator RequestRoutine()
    {
        bool done = false;
        LBData result = null;

        YandexGameManager.Instance.RequestLeaderboard(
            leaderboardName,
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

        if (!done)
        {
            SetText(emptyText);
            yield break;
        }

        SetText(Render(result));
    }

    /// <summary>
    /// Отрисовывает таблицу от самого высокого счёта к низкому:
    /// №1 = максимальный балл, дальше по убыванию.
    /// </summary>
    private string Render(LBData lb)
    {
        if (lb == null || lb.players == null || lb.players.Length == 0)
            return emptyText;

        var sorted = lb.players
            .Where(player => player != null && !string.IsNullOrEmpty(player.name))
            .OrderByDescending(player => player.score)
            .ToList();

        if (sorted.Count == 0)
            return emptyText;

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < sorted.Count; i++)
        {
            string name = LBMethods.AnonymousName(sorted[i].name);

            sb.AppendLine($"{i + 1}. {name}  -  {sorted[i].score}");
        }

        if (!YandexGameManager.Instance.IsAuthorized)
        {
            sb.AppendLine();
            sb.AppendLine(noAuthText);
        }

        return sb.ToString();
    }

    private void SetText(string text)
    {
        if (leaderboardText != null)
            leaderboardText.text = text;
    }

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