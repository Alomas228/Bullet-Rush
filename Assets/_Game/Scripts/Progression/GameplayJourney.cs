using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class GameplayJourney : MonoBehaviour
{
    [Serializable] public sealed class Trial { public string id; public int progress; }
    [Serializable] public sealed class MapRecord { public string id; public int best; public int cleanBosses; public bool won; public bool special; }
    [Serializable] public sealed class Profile
    {
        public int version = 1;
        public List<Trial> trials = new List<Trial>();
        public List<MapRecord> maps = new List<MapRecord>();
        public List<string> discoveries = new List<string>();
        public List<string> cleanBossIds = new List<string>();
        public int rank;
        public string equippedTrial = "";
    }
    public static GameplayJourney Instance { get; private set; }
    public Profile Data { get; private set; } = new Profile();
    public WaveManager Waves { get; private set; }
    public JourneyUI UI { get; private set; }
    public readonly HashSet<string> RunSynergies = new HashSet<string>();
    public readonly List<string> Highlights = new List<string>();
    public int Dodges { get; private set; }
    public int CachesWon { get; private set; }
    public int IceBroken { get; private set; }
    public int WetLightning { get; private set; }
    public int BestBeforeRun { get; private set; }
    public int LastCleared { get; private set; }
    public RunSpawnBias EndlessBias { get; private set; }
    public int EndlessThrough { get; private set; }
    public float EndlessScoreScale => EndlessThrough >= (Waves != null ? Waves.CurrentWave : 0) && EndlessBias != RunSpawnBias.None ? 1.15f : 1f;
    public string Notice { get; private set; }
    public float NoticeUntil { get; private set; }
    public int PendingCacheRewards { get; private set; }
    private MapRecord currentMap;
    private int lastBossWave;
    private bool damagedDuringBoss;
    private bool active;
    private bool dirty;
    private float nextSave;
    private const string SaveKey = "BulletRush.Journey.v1";

    public static string T(string ru, string en, string tr) => Lang.Code == LangCode.Ru ? ru : Lang.Code == LangCode.Tr ? tr : en;

    public void Bind(WaveManager waves)
    {
        Instance = this;
        Waves = waves;
        try { Data = JsonUtility.FromJson<Profile>(PlayerPrefs.GetString(SaveKey, "")) ?? new Profile(); }
        catch (Exception) { Data = new Profile(); }
        Data.trials ??= new List<Trial>(); Data.maps ??= new List<MapRecord>();
        Data.discoveries ??= new List<string>(); Data.cleanBossIds ??= new List<string>();
        UI = gameObject.AddComponent<JourneyUI>();
        UI.Bind(this);
        AchievementEvents.RunStarted += BeginRun;
        AchievementEvents.WaveCleared += WaveCleared;
        AchievementEvents.PlayerDamaged += Damaged;
        AchievementEvents.EnemyKilled += EnemyKilled;
        AchievementEvents.RunEnded += RunEnded;
    }
    private void OnDestroy()
    {
        Save();
        AchievementEvents.RunStarted -= BeginRun; AchievementEvents.WaveCleared -= WaveCleared;
        AchievementEvents.PlayerDamaged -= Damaged; AchievementEvents.EnemyKilled -= EnemyKilled;
        AchievementEvents.RunEnded -= RunEnded;
        if (Instance == this) Instance = null;
    }
    private void OnApplicationPause(bool paused) { if (paused) Save(); }
    private void OnApplicationQuit() => Save();
    private void Update()
    {
        if (dirty && Time.unscaledTime >= nextSave) Save();
        if (!active || Waves == null) return;
    }
    public void WaveStarted(int wave) { lastBossWave = wave; damagedDuringBoss = false; }
    private void BeginRun()
    {
        if (GameModeManager.IsPvp) { active = false; return; }
        active = true; RunSynergies.Clear(); Highlights.Clear();
        Dodges = CachesWon = IceBroken = WetLightning = LastCleared = PendingCacheRewards = 0;
        lastBossWave = 0; damagedDuringBoss = false;
        EndlessBias = RunSpawnBias.None; EndlessThrough = 0;
        var map = FindAnyObjectByType<EnvironmentController>()?.CurrentMap;
        string id = map != null ? map.langKey : "unknown";
        currentMap = Map(id); BestBeforeRun = currentMap.best;
        UI.Close();
    }
    public MapRecord Map(string id)
    {
        var entry = Data.maps.Find(x => x.id == id);
        if (entry == null) { entry = new MapRecord { id = id }; Data.maps.Add(entry); }
        return entry;
    }
    public Trial WeaponTrial(string id)
    {
        var entry = Data.trials.Find(x => x.id == id);
        if (entry == null) { entry = new Trial { id = id }; Data.trials.Add(entry); }
        return entry;
    }
    public bool TrialComplete(string id) => WeaponTrial(id).progress >= JourneyRules.TrialTarget(id);
    public void ProgressTrial(string id, int amount = 1, bool best = false)
    {
        if (!active || string.IsNullOrEmpty(id) || GameModeManager.IsPvp) return;
        var entry = WeaponTrial(id); int target = JourneyRules.TrialTarget(id);
        if (entry.progress >= target) return;
        entry.progress = Mathf.Min(target, best ? Mathf.Max(entry.progress, amount) : entry.progress + amount);
        dirty = true;
        if (entry.progress >= target)
        {
            AddHighlight(T("Испытание оружия: ", "Weapon trial: ", "Silah sınavı: ") + WeaponName(id));
            Data.equippedTrial = id;
            EvaluateRank(); Save();
        }
    }
    public string WeaponName(string id)
    {
        var weapons = UpgradeManager.Instance?.GetAvailableWeapons();
        if (weapons != null) foreach (var weapon in weapons) if (weapon != null && weapon.WeaponName == id) return weapon.LocalizedName;
        return id;
    }
    private void EnemyKilled(EnemyKillInfo info)
    {
        if (!active) return;
        string id = info.weaponId;
        if (!string.IsNullOrEmpty(id) && !JourneyRules.UsesPiercingTrial(id) && !JourneyRules.UsesPairTrial(id) &&
            id != "Lightning SMG" && id != "Golden Rifle" && id != "Assassin SMG" && id != "Minigun")
        {
            if (id != "Inferno Shotgun" || info.enemy.IsBurningAtDeath) ProgressTrial(id);
        }
        if (info.isBoss)
        {
            if (!damagedDuringBoss && Waves != null && Waves.IsWaveActive)
            {
                if (!Data.cleanBossIds.Contains(info.bossId)) Data.cleanBossIds.Add(info.bossId);
                if (currentMap != null) currentMap.cleanBosses = Mathf.Min(5, currentMap.cleanBosses + 1);
                AddHighlight(T("Босс без урона: ", "Untouched boss: ", "Hasarsız patron: ") + info.enemy.BossDisplayName);
            }
            EvaluateRank(); dirty = true;
        }
    }
    private void Damaged() { if (Waves != null && Waves.IsBossWave(Waves.CurrentWave)) damagedDuringBoss = true; }
    private void WaveCleared(int wave, bool perfect)
    {
        if (!active || currentMap == null) return;
        LastCleared = Mathf.Max(LastCleared, wave); currentMap.best = Mathf.Max(currentMap.best, wave);
        if (wave == 50) { currentMap.won = true; AddHighlight(T("Кампания пройдена", "Campaign cleared", "Sefer tamamlandı")); }
        // The thematic medal is earned in a single attempt; no damage/weapon gates for map access.
        switch (currentMap.id)
        {
            case "beach": case "plyazh": currentMap.special |= WetLightning >= 12; break;
            case "mountains": case "gory": currentMap.special |= IceBroken >= 12; break;
            default: currentMap.special |= wave >= 20 && Dodges >= 8 && CachesWon >= 1; break;
        }
        EvaluateRank(); dirty = true; Save();
    }
    private void RunEnded(RunEndInfo info)
    {
        if (LastCleared > BestBeforeRun) AddHighlight(T("Новый рекорд карты: ", "New map record: ", "Yeni harita rekoru: ") + LastCleared);
        dirty = true; Save(); // Keep the attempt alive for the existing revive flow.
    }
    public void RecordDodge() { if (!active) return; Dodges++; Notify(T("ТОЧНОЕ УКЛОНЕНИЕ", "PRECISE DODGE", "TAM KAÇIŞ")); }
    public void RecordIce() { if (active) IceBroken++; }
    public void RecordLightning(bool wet, string weaponId)
    {
        if (!active) return;
        if (wet) WetLightning++;
        if (weaponId == "Lightning SMG") ProgressTrial(weaponId);
    }
    public void CacheWon()
    {
        CachesWon++; PendingCacheRewards++;
        AddHighlight(T("Тайник взят — дополнительный выбор после волны", "Cache secured — extra choice after this wave", "Sandık alındı — dalga sonunda ek seçim"));
    }
    public bool ConsumeCacheReward()
    {
        if (PendingCacheRewards <= 0) return false;
        PendingCacheRewards--; return true;
    }
    public void Discover(SynergyData synergy)
    {
        if (!active || synergy == null) return;
        RunSynergies.Add(synergy.name);
        if (!Data.discoveries.Contains(synergy.name)) { Data.discoveries.Add(synergy.name); dirty = true; }
        AddHighlight(synergy.LocalizedName);
    }
    public bool HasSynergy(string id) => active && RunSynergies.Contains(id);
    public void SetEndless(RunSpawnBias bias, int nextWave)
    {
        EndlessBias = bias; EndlessThrough = JourneyRules.SegmentEnd(nextWave);
        Notify(T("Условия до волны ", "Conditions through wave ", "Koşulların son dalgası: ") + EndlessThrough);
    }
    public void Notify(string message) { Notice = message; NoticeUntil = Time.unscaledTime + 4f; }
    public void AddHighlight(string message)
    {
        if (!Highlights.Contains(message)) Highlights.Add(message);
        if (Highlights.Count > 8) Highlights.RemoveAt(0);
        Notify(message);
    }
    private void EvaluateRank()
    {
        int earned = JourneyRules.Rank(Data.maps.Count(x => x.won), Data.trials.Count(x => x.progress >= JourneyRules.TrialTarget(x.id)),
            Data.cleanBossIds.Count, Data.maps.Count == 0 ? 0 : Data.maps.Max(x => x.best));
        if (earned <= Data.rank) return;
        Data.rank = earned; dirty = true;
        AddHighlight(T("Новое звание: ", "New rank: ", "Yeni rütbe: ") + RankName(earned));
    }
    public static string RankName(int rank)
    {
        string[] ru = { "Новичок", "Боец", "Ветеран", "Мастер", "Легенда" };
        string[] en = { "Rookie", "Fighter", "Veteran", "Master", "Legend" };
        string[] tr = { "Acemi", "Savaşçı", "Kıdemli", "Usta", "Efsane" };
        return T(ru[Mathf.Clamp(rank, 0, 4)], en[Mathf.Clamp(rank, 0, 4)], tr[Mathf.Clamp(rank, 0, 4)]);
    }
    public void Equip(string id) { if (!TrialComplete(id)) return; Data.equippedTrial = id; dirty = true; Save(); }
    private void Save()
    {
        if (!dirty) return;
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data)); PlayerPrefs.Save();
        dirty = false; nextSave = Time.unscaledTime + 10f;
    }
}
