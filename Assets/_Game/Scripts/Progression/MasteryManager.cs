using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-900)]
public sealed class MasteryManager : MonoBehaviour
{
    [Serializable]
    private sealed class ProfileData
    {
        public int prestige;
        public List<WeaponEntry> weapons = new List<WeaponEntry>();
        public List<string> unlockedCosmetics = new List<string>();
        public string equippedCosmetic = string.Empty;
        public List<string> unlockedTitles = new List<string>();
        public string equippedTitle = string.Empty;
    }

    [Serializable]
    private sealed class WeaponEntry
    {
        public string weaponId;
        public int kills;
    }

    private const string PrefsKey = "ArcadeSurvivor.Mastery";
    private const int PrestigeLevel = 50;
    private static readonly int[] MasteryKillThresholds = { 100, 300, 700, 1400, 2500 };

    public static MasteryManager Instance { get; private set; }
    private ProfileData data = new ProfileData();

    public int Prestige => data.prestige;
    public IReadOnlyList<string> UnlockedCosmetics => data.unlockedCosmetics;
    public IReadOnlyList<string> UnlockedTitles => data.unlockedTitles;
    public string EquippedCosmetic => data.equippedCosmetic;
    public string EquippedTitle => data.equippedTitle;

    public event Action<string> OnWeaponMasteryChanged;
    public event Action OnCollectionChanged;
    public event Action<int> OnPrestigeChanged;

    public static MasteryManager EnsureExists()
    {
        if (Instance != null) return Instance;
        MasteryManager existing = FindAnyObjectByType<MasteryManager>();
        if (existing != null) return existing;
        return new GameObject("MasteryManager").AddComponent<MasteryManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
        EnsureCosmetics();
    }

    private void OnEnable() => AchievementEvents.EnemyKilled += HandleEnemyKilled;
    private void OnDisable() => AchievementEvents.EnemyKilled -= HandleEnemyKilled;

    public int GetWeaponKills(string weaponId)
    {
        WeaponEntry entry = FindWeapon(weaponId);
        return entry == null ? 0 : entry.kills;
    }

    public int GetWeaponMasteryLevel(string weaponId)
    {
        int kills = GetWeaponKills(weaponId);
        int level = 0;
        for (int i = 0; i < MasteryKillThresholds.Length; i++)
        {
            if (kills < MasteryKillThresholds[i]) break;
            level++;
        }
        return level;
    }

    public int GetNextMasteryThreshold(string weaponId)
    {
        int level = GetWeaponMasteryLevel(weaponId);
        return level >= MasteryKillThresholds.Length ? MasteryKillThresholds[MasteryKillThresholds.Length - 1] : MasteryKillThresholds[level];
    }

    public bool CanPrestige()
    {
        return XpManager.Instance != null &&
               XpManager.Instance.GlobalLevel >= PrestigeLevel &&
               XpManager.Instance.GlobalXP >= XpManager.Instance.GetXPForLevel(PrestigeLevel) &&
               XpManager.Instance.GlobalXP > 0;
    }

    public bool TryPrestige()
    {
        if (!CanPrestige()) return false;
        XpManager xp = XpManager.Instance;
        if (!xp.ResetPlayerXPForPrestige()) return false;
        data.prestige++;
        UnlockCosmetic("prestige_aura_" + data.prestige);
        UnlockTitle("prestige_title_" + data.prestige);
        Save();
        OnPrestigeChanged?.Invoke(data.prestige);
        return true;
    }

    public bool IsCosmeticUnlocked(string id) => !string.IsNullOrEmpty(id) && data.unlockedCosmetics.Contains(id);
    public bool IsTitleUnlocked(string id) => !string.IsNullOrEmpty(id) && data.unlockedTitles.Contains(id);

    public bool TryEquipCosmetic(string id)
    {
        if (!IsCosmeticUnlocked(id)) return false;
        data.equippedCosmetic = id;
        Save();
        OnCollectionChanged?.Invoke();
        return true;
    }

    public bool TryEquipTitle(string id)
    {
        if (!IsTitleUnlocked(id)) return false;
        data.equippedTitle = id;
        Save();
        OnCollectionChanged?.Invoke();
        return true;
    }

    public void UnlockCosmetic(string id)
    {
        if (string.IsNullOrEmpty(id) || data.unlockedCosmetics.Contains(id)) return;
        data.unlockedCosmetics.Add(id);
        Save();
        OnCollectionChanged?.Invoke();
    }

    public void UnlockTitle(string id)
    {
        if (string.IsNullOrEmpty(id) || data.unlockedTitles.Contains(id)) return;
        data.unlockedTitles.Add(id);
        Save();
        OnCollectionChanged?.Invoke();
    }

    public string GetCosmeticDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;
        string key = "progression.item." + id;
        return Lang.GetOr(key, id.Replace("_", " "));
    }

    public string GetTitleDisplayName(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;
        string key = "progression.item." + id;
        return Lang.GetOr(key, id.Replace("_", " "));
    }

    private void HandleEnemyKilled(EnemyKillInfo info)
    {
        if (string.IsNullOrEmpty(info.weaponId)) return;
        WeaponEntry entry = FindWeapon(info.weaponId);
        if (entry == null)
        {
            entry = new WeaponEntry { weaponId = info.weaponId };
            data.weapons.Add(entry);
        }

        int oldLevel = GetWeaponMasteryLevel(info.weaponId);
        entry.kills++;
        int newLevel = GetWeaponMasteryLevel(info.weaponId);
        if (newLevel > oldLevel)
        {
            if (newLevel == 1) UnlockTitle("weapon_title_" + SafeId(info.weaponId));
            if (newLevel == 2) UnlockCosmetic("weapon_skin_" + SafeId(info.weaponId));
            if (newLevel == 3) UnlockCosmetic("weapon_effect_" + SafeId(info.weaponId));
            if (newLevel == 4) UnlockTitle("weapon_master_" + SafeId(info.weaponId));
            if (newLevel == 5) UnlockCosmetic("weapon_aura_" + SafeId(info.weaponId));
        }
        Save();
        OnWeaponMasteryChanged?.Invoke(info.weaponId);
    }

    private void EnsureCosmetics()
    {
        if (string.IsNullOrEmpty(data.equippedCosmetic))
            data.equippedCosmetic = "cosmetic_neon_trail";
        if (string.IsNullOrEmpty(data.equippedTitle))
            data.equippedTitle = "rookie";
        if (!data.unlockedCosmetics.Contains("cosmetic_neon_trail"))
            data.unlockedCosmetics.Add("cosmetic_neon_trail");
        if (!data.unlockedTitles.Contains("rookie"))
            data.unlockedTitles.Add("rookie");
        Save();
    }

    private static string SafeId(string value) => value.ToLowerInvariant().Replace(" ", "_");

    private WeaponEntry FindWeapon(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < data.weapons.Count; i++)
            if (data.weapons[i] != null && data.weapons[i].weaponId == id) return data.weapons[i];
        return null;
    }

    private void Load()
    {
        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            ProfileData loaded = JsonUtility.FromJson<ProfileData>(json);
            if (loaded == null) return;
            data = loaded;
            if (data.weapons == null) data.weapons = new List<WeaponEntry>();
            if (data.unlockedCosmetics == null) data.unlockedCosmetics = new List<string>();
            if (data.unlockedTitles == null) data.unlockedTitles = new List<string>();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Mastery] Failed to load profile: " + exception.Message);
            data = new ProfileData();
        }
    }

    private void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }
}
