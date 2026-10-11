using System.Collections.Generic;

/// <summary>
/// Сериализуемое состояние достижений. Один экземпляр = весь прогресс,
/// который переживает перезапуск игры. Хранится через PlayerPrefs +
/// JsonUtility — тем же способом, что EquipmentManager и XpManager,
/// поэтому старые сохранения не ломаются: отсутствующие поля просто
/// остаются значениями по умолчанию.
/// </summary>
public class AchievementSaveData
{
    [System.Serializable]
    public class Entry
    {
        public string id;

        public bool unlocked;

        /// <summary>Накопленный прогресс (убийства/волны) для шкалы в UI.</summary>
        public float progress;

        /// <summary>Дата разблокировки в формате ISO (пусто, если ещё закрыто).</summary>
        public string unlockedAt;

        /// <summary>Собранные уникальные ID (оружие/боссы).</summary>
        public List<string> collected = new List<string>();
    }

    public List<Entry> entries = new List<Entry>();

    /// <summary>
    /// Ключи уже выданных наград (id достижения + индекс награды).
    /// Гарантирует идемпотентность: повторная загрузка/событие не выдаст
    /// награду второй раз.
    /// </summary>
    public List<string> grantedRewards = new List<string>();

    public Entry Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].id == id)
                return entries[i];
        }

        return null;
    }

    public Entry GetOrCreate(string id)
    {
        Entry entry = Find(id);

        if (entry != null)
            return entry;

        entry = new Entry { id = id };
        entries.Add(entry);

        return entry;
    }
}
