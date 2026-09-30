using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using YG;

/// <summary>
/// Локализация игрового текста.
///
/// Определение и переключение языка делегированы штатному модулю
/// локализации Yandex Games (YG2): он читает environment.i18n.lang
/// из SDK, хранит выбор игрока в LocalStorage и сообщает об
/// изменениях через YG2.onSwitchLang. Этот класс отвечает только
/// за отображение кода языка на один из поддерживаемых наборов
/// переводов и за переключение таблиц.
///
/// Основной набор языков: ru, tr, en.
/// Резервный набор: ru для be, kk, uk, uz; en для остальных.
///
/// Язык применяется ДО первой загрузки сцены — требование п. 2.14
/// Требований к игре. Индикатор 文 на debug-панели меняет цвет
/// с красного на зелёный именно на старте, а не в процессе игры.
/// </summary>
public static class Lang
{
    /// <summary>
    /// Ключ, которым модуль локализации YG2 хранит выбранный язык
    /// (константа LANG_KEY в Lang_yg.cs плагина).
    /// </summary>
    private const string PluginLangKey = "langYG";

    /// <summary>Код языка, определённый при запуске.</summary>
    public static LangCode Code { get; private set; } = LangCode.Ru;

    /// <summary>Сырой код языка ("ru", "tr", "uk", ...).</summary>
    public static string SdkLang { get; private set; } = string.Empty;

    /// <summary>
    /// Сработало после применения нового языка. Нужен UI, который
    /// показывает текущий выбор: переключатель языка в меню и в паузе
    /// должны обновляться, даже если язык сменили в другом месте.
    /// </summary>
    public static event Action<LangCode> OnLanguageChanged;

    /// <summary>Активная таблица переводов.</summary>
    private static Dictionary<string, string> _table;

    private static bool _subscribed;

    /// <summary>
    /// Вызывается автоматически до загрузки первой сцены.
    ///
    /// Порядок: ручной выбор игрока (LocalStorage) → язык платформы.
    /// Результат дополнительно уточняется событием YG2.onSwitchLang,
    /// если модуль локализации определит язык позже — тогда уже
    /// загруженные элементы переприменяются через LangBinder.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        EnsureSubscribed();

        SdkLang = DetectRawLang();
        Code = Resolve(SdkLang);

        _table = Tables.Get(Code);
    }

    private static void EnsureSubscribed()
    {
        Action<string> handler = OnPluginSwitchLang;

        // Модуль локализации YG2 обнуляет onSwitchLang при собственной
        // инициализации (Lang_yg.InitLang, только в редакторе). Если это
        // произошло после нашей подписки, обработчик молча потерян, и
        // смена языка применялась бы только при следующем запуске.
        // Поэтому флагу не доверяем — проверяем реальный список.
        if (_subscribed && YG2.onSwitchLang != null)
        {
            var handlers = YG2.onSwitchLang.GetInvocationList();

            for (int i = 0; i < handlers.Length; i++)
            {
                if (handlers[i].Equals(handler))
                    return;
            }
        }

        YG2.onSwitchLang += handler;

        _subscribed = true;
    }

    /// <summary>
    /// Модуль локализации сменил язык: применяем новую таблицу
    /// ко всем уже забинженным элементам интерфейса.
    /// </summary>
    private static void OnPluginSwitchLang(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return;

        // Ручной выбор игрока важнее языка платформы. В режиме
        // EveryGameLaunch модуль локализации при каждом запуске
        // присылает язык системы, и без этой проверки выбор игрока
        // не пережил бы перезапуск.
        string stored = YG.Utils.LocalStorage.GetKey(PluginLangKey);

        if (!string.IsNullOrEmpty(stored))
            raw = stored;

        SdkLang = raw;

        Apply(Resolve(raw));
    }

    /// <summary>
    /// Определяет сырой код языка для первого кадра.
    ///
    /// Сначала учитывается ручной выбор игрока, который хранит
    /// модуль локализации. Затем язык платформы: на WebGL это
    /// SDK Яндекс Игр с откатом на язык браузера, в редакторе —
    /// язык системы.
    /// </summary>
    private static string DetectRawLang()
    {
        string stored = YG.Utils.LocalStorage.GetKey(PluginLangKey);

        if (!string.IsNullOrEmpty(stored))
            return stored;

        string platform = ReadPlatformLang();

        if (!string.IsNullOrEmpty(platform))
            return platform;

        string pluginLang = YG2.lang;

        return string.IsNullOrEmpty(pluginLang) ? "ru" : pluginLang;
    }

    // =========================================================
    // LANGUAGE RESOLUTION
    // =========================================================

    /// <summary>
    /// Отображает любой код языка в один из поддерживаемых.
    ///
    /// Резервный набор: be, kk, uk, uz → русский;
    /// всё остальное, включая en → английский.
    /// </summary>
    public static LangCode Resolve(string raw)
    {
        string code = Normalize(raw);

        switch (code)
        {
            case "ru":
            case "be":
            case "kk":
            case "uk":
            case "uz":
                return LangCode.Ru;

            case "tr":
                return LangCode.Tr;

            default:
                return LangCode.En;
        }
    }

    /// <summary>
    /// Приводит код к виду "en": без регистра и без региона
    /// ("tr-TR" → "tr", "uk-UA" → "uk").
    /// </summary>
    public static string Normalize(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        string s = raw.Trim().ToLowerInvariant().Replace('_', '-');

        int dash = s.IndexOf('-');
        if (dash > 0)
            s = s.Substring(0, dash);

        return s;
    }

    /// <summary>Код языка строкой: "ru", "tr" или "en".</summary>
    public static string CodeText
    {
        get { return CodeTextOf(Code); }
    }

    private static string CodeTextOf(LangCode code)
    {
        switch (code)
        {
            case LangCode.Ru:
                return "ru";

            case LangCode.Tr:
                return "tr";

            default:
                return "en";
        }
    }

    /// <summary>Название языка для переключателя в настройках.</summary>
    public static string LanguageName(LangCode code)
    {
        switch (code)
        {
            case LangCode.Ru:
                return "Русский";

            case LangCode.Tr:
                return "Türkçe";

            default:
                return "English";
        }
    }

    // =========================================================
    // USER OVERRIDE
    // =========================================================

    /// <summary>
    /// Ручной выбор языка игроком. Значение уходит в модуль
    /// локализации YG2, который сохраняет его в LocalStorage
    /// и уведомляет остальной код через YG2.onSwitchLang.
    /// Это разрешённый требованиями сценарий хранения языка в кеше.
    /// </summary>
    public static void SetUserLanguage(LangCode code)
    {
        EnsureSubscribed();

        string text = CodeTextOf(code);

        YG2.SwitchLanguage(text);

        // Переключатель языка — единственное место, где игрок ждёт
        // мгновенной реакции. Не полагаемся только на событие
        // модуля локализации: оно может не прийти, если YG2 сбросил
        // список обработчиков при инициализации.
        SdkLang = text;

        Apply(code);
    }

    /// <summary>Задан ли игроком язык вручную.</summary>
    public static bool HasUserChoice
    {
        get { return YG.Utils.LocalStorage.HasKey(PluginLangKey); }
    }

    /// <summary>
    /// Сбросить ручной выбор: удаляем ключ из LocalStorage и
    /// просим модуль локализации заново определить язык платформы.
    /// </summary>
    public static void ResetToAuto()
    {
        EnsureSubscribed();

        YG.Utils.LocalStorage.DeleteKey(PluginLangKey);

        YG2.GetLanguage();

        // GetLanguage() уведомляет только когда SDK включён,
        // поэтому применяем язык платформы и явно.
        SdkLang = ReadPlatformLang();

        if (string.IsNullOrEmpty(SdkLang))
            SdkLang = YG2.lang;

        Apply(Resolve(SdkLang));
    }

    /// <summary>
    /// Переподписаться на событие модуля локализации.
    /// Вызывается после загрузки сцены: YG2 обнуляет обработчики
    /// при собственной инициализации, которая в редакторе случается
    /// уже после нашей первой подписки.
    /// </summary>
    public static void EnsurePluginSubscription()
    {
        EnsureSubscribed();
    }

    private static void Apply(LangCode code)
    {
        Code = code;
        _table = Tables.Get(code);

        LangBinder.ReapplyAll();

        // Панели, которые собрали текст сами через Lang.Get,
        // не попадают в ReapplyAll — у них в поле уже перевод.
        LangBinder.NotifyRefreshables();

        OnLanguageChanged?.Invoke(code);
    }

    // =========================================================
    // PLATFORM BRIDGE
    // =========================================================

    /// <summary>
    /// Язык платформы для первого кадра, когда модуль локализации
    /// YG2 ещё не инициализирован. На WebGL это SDK Яндекс Игр
    /// с откатом на язык браузера.
    /// </summary>
    private static string ReadPlatformLang()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            string fromSdk = GetSdkLangJs();

            if (!string.IsNullOrEmpty(fromSdk))
                return fromSdk;
        }
        catch
        {
            // SDK ещё не готов — используем язык браузера.
        }
#endif
        // Application.systemLanguage.ToString() возвращает имя,
        // например "Russian", а не ISO-код. Без приведения Resolve()
        // не узнал бы язык и всегда отдавал английский.
        switch (Application.systemLanguage.ToString())
        {
            case "Russian":
                return "ru";

            case "Ukrainian":
                return "uk";

            case "Turkish":
                return "tr";

            default:
                return "en";
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern string GetSdkLangJs();
#endif

    // =========================================================
    // LOOKUP
    // =========================================================

    /// <summary>
    /// Перевод по ключу. Неизвестный ключ возвращается как есть,
    /// чтобы об ошибке было видно в игре, а не молча пропало.
    /// </summary>
    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        if (_table == null)
            _table = Tables.Get(Code);

        string value;

        if (_table.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
            return value;

        Debug.LogWarning($"Lang: ключ '{key}' не найден в языке '{CodeText}'.");

        return key;
    }

    /// <summary>
    /// Есть ли ключ в активной таблице. Нужен там, где перевод
    /// может быть не заполнен (например, имя предмета), и вместо
    /// отсутствующего ключа надо показать исходный текст ассета.
    /// </summary>
    public static bool Has(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        if (_table == null)
            _table = Tables.Get(Code);

        string value;

        return _table.TryGetValue(key, out value) &&
               !string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Перевод по ключу, а если ключа нет — исходная строка из
    /// ассета. Без предупреждения в консоль.
    /// </summary>
    public static string GetOr(string key, string fallback)
    {
        return Has(key) ? Get(key) : fallback;
    }

    /// <summary>
    /// Перевод с подстановкой аргументов.
    /// Плейсхолдеры в переводе: {0}, {1}, {2}…
    /// </summary>
    public static string Get(string key, params object[] args)
    {
        string template = Get(key);

        if (args == null || args.Length == 0)
            return template;

        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException e)
        {
            Debug.LogWarning($"Lang: не удалось подставить ключ '{key}': {e.Message}");
            return template;
        }
    }

    /// <summary>
    /// Перевод из набора ключей (вложенные строки вида "ui.play").
    ///
    /// Имя намеренно не Get: перегрузка Get(string, string) перебивала
    /// Get(string, params object[]) при вызовах вида
    /// Lang.Get("upg.next_gain", "…") и молча ломала подстановку.
    /// </summary>
    public static string GetFrom(string prefix, string key)
    {
        return Get(prefix + "." + key);
    }

    /// <summary>
    /// Разбирает шаблон вида "{0}" из ScriptableObject-описаний
    /// (PermanentUpgrades) и подставляет значение.
    /// </summary>
    public static string Apply(string template, string value)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        if (string.IsNullOrEmpty(value))
            return template;

        return template.Replace("{0}", value);
    }
}