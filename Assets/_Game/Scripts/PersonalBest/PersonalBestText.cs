using System.Globalization;
using UnityEngine;

/// <summary>
/// Сборка текстовых подсказок рекорда: шаблон берётся из таблиц
/// локализации по ключу, а если ключа ещё нет — из fallback-шаблона
/// прямо в инспекторе. Так настройка текста работает в двух
/// направлениях: правкой Tables.cs или правкой поля на компоненте.
/// </summary>
public static class PersonalBestText
{
    /// <summary>
    /// Подставляет аргументы в шаблон. Плейсхолдеры: {0}, {1}, {2}…
    /// </summary>
    public static string Resolve(
        string langKey,
        string fallbackTemplate,
        params object[] args)
    {
        if (!string.IsNullOrEmpty(langKey) && Lang.Has(langKey))
            return Lang.Get(langKey, args);

        return Format(fallbackTemplate, args);
    }

    private static string Format(string template, object[] args)
    {
        if (string.IsNullOrEmpty(template))
            return string.Empty;

        if (args == null || args.Length == 0)
            return template;

        try
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                template,
                args
            );
        }
        catch (System.FormatException e)
        {
            Debug.LogWarning(
                "PersonalBestText: не удалось подставить аргументы " +
                $"в шаблон «{template}»: {e.Message}"
            );

            return template;
        }
    }
}
