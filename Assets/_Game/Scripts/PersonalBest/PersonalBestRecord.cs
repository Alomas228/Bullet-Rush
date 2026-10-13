using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Личный рекорд забега: максимальная волна, до которой дошёл игрок.
///
/// Рекорд ведётся отдельно по каждой карте: забег на «Космосе» с его
/// механикой нельзя честно сравнивать с забегом в «Лесу». Ключ карты —
/// индекс в карусели EnvironmentController.
///
/// Это отправная точка мотивации («добраться хотя бы до 18»), поэтому
/// значения живут в PlayerPrefs и переживают перезапуски игры.
/// Рекорд обновляется только в конце забега и только вверх — текущий
/// забег никогда не портит сохранённое достижение.
/// </summary>
public static class PersonalBestRecord
{
    private const string PrefsKey = "ArcadeSurvivor.PersonalBestWave";

    private static readonly Dictionary<int, int> cache =
        new Dictionary<int, int>();

    /// <summary>Рекорд изменился (новое лучшее значение записано).</summary>
    public static event System.Action OnChanged;

    /// <summary>Карта, для которой читается/пишется рекорд. -1 — глобальный.</summary>
    private static int CurrentMapIndex =>
        EnvironmentController.SelectedMapIndex;

    private static string KeyFor(int mapIndex)
    {
        return mapIndex >= 0
            ? PrefsKey + ".Map" + mapIndex
            : PrefsKey;
    }

    /// <summary>Есть ли уже рекорд. false — игрок ещё не дошёл ни до одной волны до конца.</summary>
    public static bool HasRecord
    {
        get { return BestWave > 0; }
    }

    /// <summary>Сохранённая рекордная волна для текущей карты.</summary>
    public static int BestWave
    {
        get { return BestWaveFor(CurrentMapIndex); }
    }

    /// <summary>Рекордная волна для конкретной карты.</summary>
    public static int BestWaveFor(int mapIndex)
    {
        if (cache.TryGetValue(mapIndex, out int best))
            return best;

        best = PlayerPrefs.GetInt(KeyFor(mapIndex), 0);

        // Мягкая миграция: старый общий рекорд считаем рекордом
        // первой карты, чтобы прогресс игроков не пропал.
        if (best <= 0 && mapIndex <= 0)
        {
            int legacy = PlayerPrefs.GetInt(PrefsKey, 0);

            if (legacy > best)
                best = legacy;
        }

        cache[mapIndex] = best;

        return best;
    }

    /// <summary>
    /// Записывает волну, до которой дошёл забег, если она лучше
    /// сохранённого рекорда текущей карты. Значения 0 и меньше
    /// игнорируются (обучение и попытки без дошедшей до конца волны).
    /// </summary>
    public static void Submit(int wave)
    {
        Submit(wave, CurrentMapIndex);
    }

    /// <summary>Записать рекорд для конкретной карты.</summary>
    public static void Submit(int wave, int mapIndex)
    {
        if (wave <= 0)
            return;

        int best = BestWaveFor(mapIndex);

        if (wave <= best)
            return;

        cache[mapIndex] = wave;

        PlayerPrefs.SetInt(KeyFor(mapIndex), wave);
        PlayerPrefs.Save();

        OnChanged?.Invoke();
    }

    /// <summary>Сброс рекорда — для отладки и будущей кнопки «стереть прогресс».</summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(PrefsKey);

        foreach (int mapIndex in cache.Keys)
            PlayerPrefs.DeleteKey(KeyFor(mapIndex));

        cache.Clear();

        // Удаляем и возможные карты, которых нет в кэше.
        for (int i = 0; i < 64; i++)
            PlayerPrefs.DeleteKey(KeyFor(i));

        PlayerPrefs.Save();

        OnChanged?.Invoke();
    }
}
