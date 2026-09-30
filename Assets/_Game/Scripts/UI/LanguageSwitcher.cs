using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Переключатель языка: три кнопки RU / TR / EN и кнопка
/// «Автоматически». Один и тот же компонент можно повесить
/// и на главное меню, и на паузу — работает независимо,
/// перезагрузка сцены не требуется.
///
/// Как настроить:
/// 1. Создать 4 кнопки (RU, TR, EN, Автоматически) как обычно.
/// 2. Повесить этот компонент на объект панели.
/// 3. Перетащить кнопки в поля ruButton / trButton / enButton
///    и autoButton.
///
/// Активный вариант определяется автоматически: у него
/// interactable = false, поэтому Unity сама покажет его
/// приглушённым (если у кнопок обычный ColorTint).
/// </summary>
[DisallowMultipleComponent]
public class LanguageSwitcher : MonoBehaviour, ILangRefreshable
{
    [Header("Языки")]
    [SerializeField] private Button ruButton;
    [SerializeField] private Button trButton;
    [SerializeField] private Button enButton;

    [Header("Автоматически")]
    [Tooltip("Возвращает язык из SDK Яндекс Игр / браузера.")]
    [SerializeField] private Button autoButton;

    [Header("Необязательно")]
    [Tooltip("Подпись текущего языка. Если не назначена, берётся имя " +
             "кнопки активного языка.")]
    [SerializeField] private TMP_Text currentLabel;

    [Tooltip("Подпись у кнопки «Автоматически». По умолчанию берётся " +
             "из таблицы по ключу settings.language_auto.")]
    [SerializeField] private TMP_Text autoLabel;

    private void Awake()
    {
        Bind(ruButton, LangCode.Ru);
        Bind(trButton, LangCode.Tr);
        Bind(enButton, LangCode.En);

        if (autoButton != null)
            autoButton.onClick.AddListener(SetAuto);

        if (autoLabel != null)
            autoLabel.text = Lang.Get("settings.language_auto");
    }

    private void OnEnable()
    {
        Refresh();
    }

    /// <summary>
    /// Обновляет подсветку активного языка. Вызывается общим
    /// механизмом LangBinder при смене языка.
    /// </summary>
    public void RefreshLang()
    {
        Refresh();
    }

    private void Bind(Button button, LangCode code)
    {
        if (button == null)
            return;

        button.onClick.AddListener(() => SetLanguage(code));
    }

    /// <summary>Выбрать язык вручную. Выбор сохраняется модулем
    /// локализации YG2 в LocalStorage.</summary>
    public void SetLanguage(LangCode code)
    {
        Lang.SetUserLanguage(code);
    }

    /// <summary>Вернуть автоматический выбор языка платформы.</summary>
    public void SetAuto()
    {
        Lang.ResetToAuto();
    }

    /// <summary>Обновить подсветку активного варианта.</summary>
    public void Refresh()
    {
        bool auto = !Lang.HasUserChoice;

        SetActive(ruButton, !auto && Lang.Code == LangCode.Ru);
        SetActive(trButton, !auto && Lang.Code == LangCode.Tr);
        SetActive(enButton, !auto && Lang.Code == LangCode.En);
        SetActive(autoButton, auto);

        if (currentLabel != null)
        {
            if (auto)
            {
                currentLabel.text = Lang.Get("settings.language_auto");
            }
            else
            {
                currentLabel.text =
                    Lang.LanguageName(Lang.Code);
            }
        }
    }

    private static void SetActive(Button button, bool active)
    {
        if (button == null)
            return;

        // Активный вариант нельзя нажать повторно, и Unity
        // автоматически приглушает неактивные кнопки.
        button.interactable = !active;
    }
}