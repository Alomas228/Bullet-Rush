using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Подставляет переводы в текст, заданный прямо в сцене и префабах.
///
/// Разметка в инспекторе: в m_text пишется ключ в фигурных скобках,
/// например "{menu.play}". При загрузке сцены ключ заменяется переводом
/// на язык, определённый автоматически из SDK Яндекс Игр.
///
/// Оригинальный ключ запоминается, поэтому при ручной смене языка
/// в настройках переводы применяются повторно без перезагрузки сцены.
///
/// Развёртка привязана к событию sceneLoaded, а не только к
/// RuntimeInitializeOnLoadMethod: тот срабатывает один раз за запуск
/// приложения, и после возврата в меню новые объекты остались бы
/// с неразвёрнутыми "{menu.profile}".
/// </summary>
[DisallowMultipleComponent]
public class LangBinder : MonoBehaviour
{
    /// <summary>instance id компонента -> сам компонент и его ключ.</summary>
    private static readonly Dictionary<int, BoundText> Bound =
        new Dictionary<int, BoundText>();

    private struct BoundText
    {
        public Component Component;
        public string Key;
    }

    /// <summary>Постоянный слушатель sceneLoaded.</summary>
    private static LangBinder _hook;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (_hook != null)
            return;

        var go = new GameObject("~LangBinder")
        {
            hideFlags = HideFlags.HideInHierarchy
        };

        DontDestroyOnLoad(go);

        _hook = go.AddComponent<LangBinder>();
    }

    /// <summary>
    /// Прогоняет все загруженные сцены. Страховка на случай, если
    /// первая сцена загрузилась раньше, чем поднялся слушатель.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyToLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);

            if (scene.isLoaded)
                ApplyToRoots(scene.GetRootGameObjects());
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        Lang.EnsurePluginSubscription();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (_hook == this)
            _hook = null;
    }

    /// <summary>
    /// Новая сцена загружена: разворачиваем ключи в её объектах.
    /// Событие приходит после Awake/OnEnable, но до Start, поэтому
    /// игрок не успевает увидеть "{menu.profile}".
    /// </summary>
    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToRoots(scene.GetRootGameObjects());
    }

    private void Awake()
    {
        ApplyToRoots(new[] { gameObject });
    }

    /// <summary>
    /// Переприменяет переводы после смены языка.
    /// Записи об уничтоженных объектах вычищаются попутно.
    /// </summary>
    public static void ReapplyAll()
    {
        List<int> dead = null;

        foreach (var pair in Bound)
        {
            var component = pair.Value.Component;

            // Unity "пустые" уничтоженные объекты сравниваются с null.
            if (component == null)
            {
                (dead ?? (dead = new List<int>())).Add(pair.Key);
                continue;
            }

            SetText(component, pair.Value.Key);
        }

        if (dead == null)
            return;

        for (int i = 0; i < dead.Count; i++)
            Bound.Remove(dead[i]);
    }

    /// <summary>
    /// Просит открытые панели перерисовать текст, который они
    /// собрали сами через Lang.Get: такие подписи в инспекторе
    /// не хранятся ключом, поэтому ReapplyAll их не видит.
    ///
    /// Смена языка не должна перезагружать сцену, поэтому панели
    /// обновляются на лету.
    /// </summary>
    public static void NotifyRefreshables()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);

            if (!scene.isLoaded)
                continue;

            var roots = scene.GetRootGameObjects();

            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r] == null)
                    continue;

                var behaviours =
                    roots[r].GetComponentsInChildren<MonoBehaviour>(true);

                for (int b = 0; b < behaviours.Length; b++)
                {
                    // Массив может содержать уничтоженные компоненты.
                    if (behaviours[b] is ILangRefreshable refreshable)
                        refreshable.RefreshLang();
                }
            }
        }
    }

    private static void ApplyToRoots(GameObject[] roots)
    {
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] == null)
                continue;

            var tmps = roots[i].GetComponentsInChildren<TMP_Text>(true);

            for (int j = 0; j < tmps.Length; j++)
                Bind(tmps[j]);

            var texts = roots[i].GetComponentsInChildren<Text>(true);

            for (int j = 0; j < texts.Length; j++)
                Bind(texts[j]);
        }
    }

    private static void Bind(Component component)
    {
        if (component == null)
            return;

        string source = Current(component);
        string key = ExtractKey(source);

        if (key == null)
            return;

        int id = component.GetInstanceID();

        Bound[id] = new BoundText
        {
            Component = component,
            Key = key
        };

        SetText(component, key);
    }

    private static void SetText(Component component, string key)
    {
        string value = Lang.Get(key);

        if (component is TMP_Text tmp)
            tmp.text = value;
        else if (component is Text uiText)
            uiText.text = value;
    }

    private static string Current(Component component)
    {
        if (component is TMP_Text tmp)
            return tmp.text;

        if (component is Text uiText)
            return uiText.text;

        return null;
    }

    /// <summary>
    /// Разворачивает строку вида "{menu.play}" в перевод.
    /// Если ключа в фигурных скобках нет, строка возвращается
    /// как есть — так можно задавать произвольный текст в инспекторе.
    /// </summary>
    public static string ResolveKey(string value)
    {
        string key = ExtractKey(value);

        return key == null ? value : Lang.Get(key);
    }

    /// <summary>
    /// Возвращает ключ, если строка выглядит как "{some.key}",
    /// иначе null.
    /// </summary>
    private static string ExtractKey(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        value = value.Trim();

        if (value.Length < 3 || value[0] != '{')
            return null;

        int end = value.IndexOf('}');

        if (end < 0)
            return null;

        string body = value.Substring(1, end - 1).Trim();

        return body.Length == 0 ? null : body;
    }
}