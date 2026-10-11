using System.Collections.Generic;

/// <summary>
/// Сериализуемое состояние ежедневных миссий: дата, на которую выдан
/// набор, и сами миссии с прогрессом. Хранится через PlayerPrefs +
/// JsonUtility — тем же способом, что достижения и ежедневные награды.
///
/// Набор дня целиком лежит в сохранении, а не пересобирается каждый
/// запуск: так миссии не «прыгают», если между запусками обновился пул,
/// а прогресс честно переживает перезапуск игры.
/// </summary>
public class DailyMissionSaveData
{
    [System.Serializable]
    public class Entry
    {
        /// <summary>ID шаблона, из которого собрана миссия.</summary>
        public string id;

        /// <summary>(int)DailyMissionObjective.</summary>
        public int objective;

        /// <summary>(int)DamageSource для миссий на источник урона.</summary>
        public int source;

        /// <summary>Цель миссии.</summary>
        public float target;

        /// <summary>Текущий прогресс (для миссий «лучший результат» — максимум).</summary>
        public float progress;

        /// <summary>Цель достигнута (но награда ещё может быть не забрана).</summary>
        public bool completed;

        /// <summary>Награда уже получена игроком.</summary>
        public bool claimed;

        public int coinReward;
        public int xpReward;

        public DailyMissionObjective Objective =>
            (DailyMissionObjective)objective;

        public DamageSource Source =>
            (DamageSource)source;
    }

    /// <summary>Дата выдачи набора в формате "yyyy-MM-dd" (локальная).</summary>
    public string date = string.Empty;

    public List<Entry> missions = new List<Entry>();

    public Entry Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        for (int i = 0; i < missions.Count; i++)
        {
            if (missions[i] != null && missions[i].id == id)
                return missions[i];
        }

        return null;
    }
}
