using TMPro;
using UnityEngine;

/// <summary>
/// Игровой блок рекорда в HUD: текущая волна против рекорда,
/// прогресс до рекорда и предупреждение «осталось N волн».
///
/// Смысл системы: пока игрок близко к своему рекорду, игра сама
/// напоминает цель — это стоит копейки в разработке, но сильно
/// держит внимание. Всё поведение (когда показывать, какой текст)
/// настраивается полями ниже, позиция — перемещением объектов.
/// </summary>
public class PersonalBestHud : MonoBehaviour, ILangRefreshable
{
    [Header("UI")]
    [Tooltip("Строка «Wave 14 / PB 17» рядом с текущей волной.")]
    [SerializeField] private TMP_Text wavePbText;

    [Tooltip("Прогресс относительно рекорда (число над строкой волны).")]
    [SerializeField] private TMP_Text progressText;

    [Tooltip("Предупреждение «3 WAVES TO BEAT YOUR RECORD» — включается, когда до рекорда осталось немного.")]
    [SerializeField] private TMP_Text approachText;

    [Tooltip("Баннер нового рекорда — появляется один раз, когда игрок превзошёл рекорд.")]
    [SerializeField] private TMP_Text newRecordText;

    [Header("References")]
    [Tooltip("Если не назначен — ищется автоматически при старте.")]
    [SerializeField] private WaveManager waveManager;

    [Header("Text (ключи локализации + fallback-шаблоны)")]
    [Tooltip("Строка волны и рекорда. {0} — волна, {1} — рекорд, {2} — осталось волн до рекорда, {3} — на сколько рекорд превышен, {4} — прогресс в процентах к рекорду.")]
    [SerializeField] private string lineLangKey = "pb.hud_line";
    [SerializeField] private string lineFallback = "Wave {0} / PB {1}";

    [Tooltip("Прогресс. Те же плейсхолдеры, что и у строки волны.")]
    [SerializeField] private string progressLangKey = "pb.progress";
    [SerializeField] private string progressFallback = "{2}";

    [Tooltip("Предупреждение, когда до рекорда осталось 2 и больше волн. {2} — оставшееся число волн.")]
    [SerializeField] private string approachLangKey = "pb.approach";
    [SerializeField] private string approachFallback = "{2} WAVES TO BEAT YOUR RECORD";

    [Tooltip("То же, но для случая «осталась ровно одна волна» — склонения и артикли требуют отдельного шаблона.")]
    [SerializeField] private string approachOneLangKey = "pb.approach_one";
    [SerializeField] private string approachOneFallback = "{2} WAVE TO BEAT YOUR RECORD";

    [Tooltip("Подпись на самой рекордной волне (осталось 0). {2} в ней будет равно нулю, обычно плейсхолдер не нужен.")]
    [SerializeField] private string onRecordLangKey = "pb.on_record";
    [SerializeField] private string onRecordFallback = "THIS IS YOUR RECORD WAVE — HOLD ON!";

    [Tooltip("Баннер нового рекорда. Плейсхолдеры те же, что у строки волны.")]
    [SerializeField] private string newRecordLangKey = "pb.new_record";
    [SerializeField] private string newRecordFallback = "NEW PERSONAL BEST!";

    [Header("Rules")]
    [Tooltip("Показывать предупреждение, пока до рекорда не больше этого числа волн. 0 — выключить.")]
    [SerializeField] private int approachThreshold = 3;

    [Tooltip("Сколько секунд держать баннер нового рекорда.")]
    [SerializeField] private float newRecordDuration = 4f;

    [Tooltip("Прятать все подписи, пока рекорда нет (первый забег).")]
    [SerializeField] private bool hideWithoutRecord = true;

    private int cachedWave = int.MinValue;
    private bool beyondRecord;
    private float newRecordTimer;

    private void Awake()
    {
        if (waveManager == null)
            waveManager = FindAnyObjectByType<WaveManager>();

        // Подписка в Awake, а не в OnEnable: блок может быть погашен
        // корневым HUD, и событие должно пережить это.
        PersonalBestRecord.OnChanged += HandleRecordChanged;
    }

    private void OnDestroy()
    {
        PersonalBestRecord.OnChanged -= HandleRecordChanged;
    }

    private void OnEnable()
    {
        cachedWave = int.MinValue;
        beyondRecord = false;
        newRecordTimer = 0f;

        HideAll();
        Render();
    }

    private void Update()
    {
        int wave =
            waveManager != null ? waveManager.CurrentWave : 0;

        if (wave != cachedWave)
        {
            cachedWave = wave;
            Render();
        }

        if (newRecordTimer > 0f)
        {
            newRecordTimer -= Time.deltaTime;

            if (newRecordTimer <= 0f)
                SetActiveSafe(newRecordText, false);
        }
    }

    public void RefreshLang()
    {
        Render();
    }

    private void HandleRecordChanged()
    {
        Render();
    }

    private void Render()
    {
        int wave = cachedWave == int.MinValue
            ? (waveManager != null ? waveManager.CurrentWave : 0)
            : cachedWave;

        cachedWave = wave;

        int pb = PersonalBestRecord.BestWave;
        bool hasRecord = PersonalBestRecord.HasRecord;
        bool inRun = wave > 0;

        bool showBlock = inRun && (hasRecord || !hideWithoutRecord);

        int remaining = pb - wave;
        int beaten = wave - pb;
        int percent =
            pb > 0 ? Mathf.RoundToInt(wave * 100f / pb) : 0;

        // Пересечение рекорда: один раз за забег показываем баннер.
        if (inRun && hasRecord && wave > pb && !beyondRecord)
        {
            beyondRecord = true;
            TriggerNewRecord(wave, pb, remaining, beaten, percent);
        }
        else if (wave <= pb)
        {
            beyondRecord = false;
        }

        bool newRecordVisible = newRecordTimer > 0f;

        SetText(
            progressText,
            showBlock,
            progressLangKey,
            progressFallback,
            wave, pb, remaining, beaten, percent
        );

        SetText(
            wavePbText,
            showBlock,
            lineLangKey,
            lineFallback,
            wave, pb, remaining, beaten, percent
        );

        bool approachVisible =
            showBlock &&
            hasRecord &&
            approachThreshold > 0 &&
            remaining >= 0 &&
            remaining <= approachThreshold &&
            !newRecordVisible;

        if (approachText != null)
        {
            approachText.gameObject.SetActive(approachVisible);

            if (approachVisible)
            {
                string key;
                string fallback;

                if (remaining <= 0)
                {
                    key = onRecordLangKey;
                    fallback = onRecordFallback;
                }
                else if (remaining == 1)
                {
                    key = approachOneLangKey;
                    fallback = approachOneFallback;
                }
                else
                {
                    key = approachLangKey;
                    fallback = approachFallback;
                }

                approachText.text = PersonalBestText.Resolve(
                    key,
                    fallback,
                    wave, pb, remaining, beaten, percent
                );
            }
        }
    }

    private void TriggerNewRecord(
        int wave,
        int pb,
        int remaining,
        int beaten,
        int percent)
    {
        if (newRecordText == null)
            return;

        newRecordText.text = PersonalBestText.Resolve(
            newRecordLangKey,
            newRecordFallback,
            wave, pb, remaining, beaten, percent
        );

        SetActiveSafe(newRecordText, true);

        newRecordTimer = Mathf.Max(newRecordDuration, 0.1f);
    }

    private void SetText(
        TMP_Text target,
        bool visible,
        string langKey,
        string fallback,
        params object[] args)
    {
        if (target == null)
            return;

        SetActiveSafe(target, visible);

        if (visible)
            target.text = PersonalBestText.Resolve(langKey, fallback, args);
    }

    // Гасим только чужие объекты: собственный GameObject держит
    // подписку и должен оставаться активным, иначе баннер нового
    // рекорда не вернётся после выключения.
    private void SetActiveSafe(TMP_Text target, bool visible)
    {
        GameObject go = target.gameObject;

        if (go == gameObject)
        {
            target.text = visible ? target.text : string.Empty;
            return;
        }

        if (go.activeSelf != visible)
            go.SetActive(visible);
    }

    private void HideAll()
    {
        if (progressText != null)
            SetActiveSafe(progressText, false);

        if (wavePbText != null)
            SetActiveSafe(wavePbText, false);

        if (approachText != null)
            SetActiveSafe(approachText, false);

        if (newRecordText != null)
            SetActiveSafe(newRecordText, false);
    }
}
