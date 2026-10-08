using TMPro;
using UnityEngine;

/// <summary>
/// Подпись «личный рекорд» для любого места игры: главное меню,
/// пауза, экран итогов. Текст собирается из ключа локализации,
/// позиция и размер задаются обычным перемещением объекта в сцене.
/// Плейсхолдеры шаблона: {0} — рекордная волна.
/// </summary>
public class PersonalBestLabel : MonoBehaviour, ILangRefreshable
{
    [Header("UI")]
    [Tooltip("Текст, в который выводится подпись.")]
    [SerializeField] private TMP_Text label;

    [Header("Text")]
    [Tooltip("Ключ локализации, например «pb.menu». Если ключа нет в таблице — используется fallback-шаблон.")]
    [SerializeField] private string langKey = "pb.menu";

    [Tooltip("Шаблон на случай отсутствия ключа. {0} — рекордная волна. Пишется прямо здесь, если менять таблицы не хочется.")]
    [SerializeField] private string fallbackTemplate = "PERSONAL BEST — WAVE {0}";

    [Header("Visibility")]
    [Tooltip("Прятать, пока рекорда нет (первый запуск игры).")]
    [SerializeField] private bool hideWhenNoRecord = true;

    [Tooltip("Объекты, которые гасятся вместе с подписью (например, рамка или иконка). Если пусто — гасится сам текст.")]
    [SerializeField] private GameObject[] hideTargets;

    // Подписка в Awake, а не в OnEnable: объект может быть погашен
    // собственным Refresh, и событие должно его вернуть обратно.
    private void Awake()
    {
        PersonalBestRecord.OnChanged += Refresh;
    }

    private void OnDestroy()
    {
        PersonalBestRecord.OnChanged -= Refresh;
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void RefreshLang()
    {
        Refresh();
    }

    private void Refresh()
    {
        bool hasRecord = PersonalBestRecord.HasRecord;
        bool visible = hasRecord || !hideWhenNoRecord;

        SetTargetsActive(visible);

        if (label == null || !visible)
            return;

        label.text = PersonalBestText.Resolve(
            langKey,
            fallbackTemplate,
            PersonalBestRecord.BestWave
        );
    }

    private void SetTargetsActive(bool visible)
    {
        if (!hideWhenNoRecord)
            return;

        if (hideTargets != null && hideTargets.Length > 0)
        {
            foreach (GameObject target in hideTargets)
            {
                if (target != null)
                    target.SetActive(visible);
            }

            return;
        }

        if (label == null)
            return;

        // Гасить собственный GameObject можно: подписка живёт в Awake
        // и переживает выключение, поэтому рекорд вернёт подпись.
        label.gameObject.SetActive(visible);
    }
}
