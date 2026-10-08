using UnityEngine;

/// <summary>
/// Личный рекорд забега: максимальная волна, до которой дошёл игрок.
///
/// Это отправная точка мотивации («добраться хотя бы до 18»), поэтому
/// значение живёт в PlayerPrefs и переживает перезапуски игры.
/// Рекорд обновляется только в конце забега и только вверх — текущий
/// забег никогда не портит сохранённое достижение.
/// </summary>
public static class PersonalBestRecord
{
    private const string PrefsKey = "ArcadeSurvivor.PersonalBestWave";

    private static int _best;
    private static bool _loaded;

    /// <summary>Рекорд изменился (новое лучшее значение записано).</summary>
    public static event System.Action OnChanged;

    /// <summary>Есть ли уже рекорд. false — игрок ещё не дошёл ни до одной волны до конца.</summary>
    public static bool HasRecord
    {
        get { return BestWave > 0; }
    }

    /// <summary>Сохранённая рекордная волна.</summary>
    public static int BestWave
    {
        get
        {
            EnsureLoaded();
            return _best;
        }
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        _best = PlayerPrefs.GetInt(PrefsKey, 0);
        _loaded = true;
    }

    /// <summary>
    /// Записывает волну, до которой дошёл забег, если она лучше
    /// сохранённого рекорда. Значения 0 и меньше игнорируются
    /// (обучение и попытки без дошедшей до конца волны).
    /// </summary>
    public static void Submit(int wave)
    {
        if (wave <= 0)
            return;

        EnsureLoaded();

        if (wave <= _best)
            return;

        _best = wave;

        PlayerPrefs.SetInt(PrefsKey, wave);
        PlayerPrefs.Save();

        OnChanged?.Invoke();
    }

    /// <summary>Сброс рекорда — для отладки и будущей кнопки «стереть прогресс».</summary>
    public static void Clear()
    {
        _best = 0;
        _loaded = true;

        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();

        OnChanged?.Invoke();
    }
}
