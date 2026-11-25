using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(
    fileName = "SFXLibrary",
    menuName = "Arcade Survivor/SFX Library"
)]
public class SFXLibrary : ScriptableObject
{

    [System.Serializable]
    private struct SfxVolumeEntry
    {
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume;
    }

    [Header("Per-Sound Volumes")]
    [Tooltip("Индивидуальная громкость для конкретных клипов. " +
        "Итоговая громкость = общая SFX/UI * громкость клипа * масштаб вызова. " +
        "Клипы без записи играют на полной громкости.")]
    [SerializeField] private SfxVolumeEntry[] sfxVolumeOverrides;

    private readonly Dictionary<AudioClip, float> clipVolumes =
        new Dictionary<AudioClip, float>();

    private bool clipVolumesBuilt;

    [Header("Player")]
    [SerializeField] private AudioClip playerHit;
    [SerializeField] private AudioClip playerDie;
    [Tooltip("Взрыв способности игрока (бомба).")]
    [SerializeField] private AudioClip playerAbilityExplosion;
    [Tooltip("Рывок игрока (пробел).")]
    [SerializeField] private AudioClip dash;
    [Tooltip("Активация щита.")]
    [SerializeField] private AudioClip shieldActivate;

    [Header("Weapon")]
    [SerializeField] private AudioClip weaponFire;
    [SerializeField] private AudioClip weaponSwitch;
    [Tooltip("Взрыв пули (взрывной урон).")]
    [SerializeField] private AudioClip bulletExplosion;
    [Tooltip("Рикошет пули в другую цель.")]
    [SerializeField] private AudioClip ricochet;
    [Tooltip("Молния (прок оружия / цепная молния).")]
    [SerializeField] private AudioClip lightning;

    [Header("Enemy")]
    [SerializeField] private AudioClip enemySpawn;
    [SerializeField] private AudioClip enemyHit;
    [SerializeField] private AudioClip enemyDie;
    [Tooltip("Выстрел противника снарядом.")]
    [SerializeField] private AudioClip enemyShot;

    [Header("Boss")]
    [SerializeField] private AudioClip bossSpawn;
    [SerializeField] private AudioClip bossPhaseChange;
    [SerializeField] private AudioClip bossAbility;
    [SerializeField] private AudioClip bossAoeExplode;
    [SerializeField] private AudioClip bossDie;

    [Header("Pickup")]
    [SerializeField] private AudioClip pickupSpawn;
    [SerializeField] private AudioClip pickupCollect;

    [Header("UI")]
    [SerializeField] private AudioClip uiClick;
    [SerializeField] private AudioClip countdownTick;
    [SerializeField] private AudioClip waveStart;
    [SerializeField] private AudioClip upgradePick;
    [SerializeField] private AudioClip menuOpen;
    [SerializeField] private AudioClip startGame;
    [SerializeField] private AudioClip gameOver;
    [Tooltip("Все враги волны уничтожены.")]
    [SerializeField] private AudioClip waveComplete;
    [Tooltip("Повышение уровня игрока.")]
    [SerializeField] private AudioClip levelUp;
    [Tooltip("Достигнут новый порог комбо.")]
    [SerializeField] private AudioClip comboMilestone;
    [Tooltip("Всплывающее уведомление (достижение, миссия).")]
    [SerializeField] private AudioClip notification;

    [Header("Map")]
    [Tooltip("Смена карты в меню выбора карт.")]
    [SerializeField] private AudioClip mapChange;

    public AudioClip PlayerHit => CombatClip("player_hurt_depth", playerHit);
    public AudioClip PlayerDie => playerDie;
    public AudioClip PlayerAbilityExplosion => CombatClip("blast_bomb", playerAbilityExplosion);
    public AudioClip Dash => dash;
    public AudioClip ShieldActivate => CombatClip("shield", shieldActivate);

    public AudioClip WeaponFire => weaponFire;
    public AudioClip WeaponSwitch => weaponSwitch;
    public AudioClip BulletExplosion => CombatClip("blast_round", bulletExplosion);
    public AudioClip Ricochet => CombatClip("ricochet_depth", ricochet);
    public AudioClip Lightning => CombatClip("chain_discharge", lightning);

    public AudioClip EnemySpawn => CombatClip("spawn", enemySpawn);
    public AudioClip EnemyHit => CombatClip("hit", enemyHit);
    public AudioClip EnemyDie => CombatClip("fall", enemyDie);
    public AudioClip EnemyShot => enemyShot;

    public AudioClip BossSpawn => bossSpawn;
    public AudioClip BossPhaseChange => bossPhaseChange;
    public AudioClip BossAbility => bossAbility;
    public AudioClip BossAoeExplode => CombatClip("blast_boss", bossAoeExplode);
    public AudioClip HazardExplosion => CombatClip("blast_hazard", bossAoeExplode);
public AudioClip BossDie => bossDie;

    public AudioClip CountdownTick => countdownTick;
    public AudioClip WaveStart => waveStart;
    public AudioClip UpgradePick => upgradePick;
    public AudioClip MenuOpen => menuOpen;
    public AudioClip StartGame => startGame;
    public AudioClip GameOver => gameOver;
    public AudioClip WaveComplete => waveComplete;
    public AudioClip LevelUp => levelUp;
    public AudioClip ComboMilestone => comboMilestone;
    public AudioClip Notification => notification;
    public AudioClip UiClick => uiClick;
    public AudioClip MapChange => mapChange;

    public AudioClip PickupSpawn => pickupSpawn;
    public AudioClip PickupCollect => pickupCollect;

    private readonly Dictionary<string, AudioClip> combatClips = new Dictionary<string, AudioClip>();
    private readonly Dictionary<AudioClip, float> combatVolumes = new Dictionary<AudioClip, float>();
    private bool combatLoaded;

    internal AudioClip CombatClip(string key, AudioClip fallback = null)
    {
        if (!combatLoaded)
        {
            combatLoaded = true;
            foreach (AudioClip clip in Resources.LoadAll<AudioClip>("CombatSfx"))
            {
                combatClips[clip.name] = clip;
                combatVolumes[clip] = clip.name == "cinematic_hit" ? .34f : clip.name.StartsWith("ui_") ? .36f : clip.name == "spawn" ? .22f : clip.name == "hit" || clip.name.StartsWith("impact_") ? .24f :
                    clip.name == "player_hurt_depth" ? .68f : clip.name == "blast_round" ? .57f :
                    clip.name.StartsWith("blast_") ? .67f : clip.name == "fire_ignition" ? .30f :
                    clip.name == "fall_depth" || clip.name == "pop_depth" || clip.name == "shatter_depth" ? .31f :
                    clip.name == "fall" || clip.name == "pop" || clip.name == "shatter" ? .30f :
                    clip.name == "explosion" || clip.name == "slam" ? .57f : .47f;
            }
        }
        return combatClips.TryGetValue(key, out AudioClip result) ? result : fallback;
    }

    public AudioClip WeaponSound(string weaponId)
    {
        string key = weaponId switch
        {
            "Burst Rifle" => "burst", "Golden Rifle" => "gold", "Hunter Rifle" => "hunter", "Plasma Rifle" => "plasma",
            "Basic Shotgun" => "shotgun", "Double Shotgun" => "double", "Heavy Shotgun" => "heavy",
            "Inferno Shotgun" => "fire", "Void Shotgun" => "void", "Basic SMG" => "smg", "Assassin SMG" => "assassin",
            "Lightning SMG" => "electric", "Minigun" => "minigun", "Twin SMG" => "twin", _ => "rifle"
        };
        return CombatClip(key, weaponFire);
    }

    internal AudioClip ImpactSound(DamageSource source, string weaponId)
    {
        if (source == DamageSource.ChainLightning) return CombatClip("impact_electric", EnemyHit);
        if (source == DamageSource.Burn || source == DamageSource.Bleed) return EnemyHit;
        string key = weaponId switch
        {
            "Plasma Rifle" => "impact_plasma", "Inferno Shotgun" => "impact_fire",
            "Void Shotgun" => "impact_void", "Lightning SMG" => "impact_electric", _ => "hit"
        };
        return CombatClip(key, EnemyHit);
    }

    internal void PlayAbility(AbilityKind kind, Vector3 position, bool automatic)
    {
        string key = kind switch
        {
            AbilityKind.CrescentSlash => "slash_depth", AbilityKind.ThunderSlam => "slam_depth", AbilityKind.FrostNova => "frost_depth",
            AbilityKind.GravityWell => "gravity_depth", AbilityKind.OrbitBlades => "orbit_depth", AbilityKind.ShadowFamiliar => "familiar_depth",
            AbilityKind.LightningStorm => "lightning_storm_depth", AbilityKind.FlameRing => "fire_ring_depth", AbilityKind.IceLance => "ice_lance_depth",
            AbilityKind.FallingStar => "blast_meteor", AbilityKind.VampirePulse => "vampire_depth", AbilityKind.BeeSwarm => "swarm_depth",
            AbilityKind.RicochetVolley => "ricochet_volley_depth", AbilityKind.GuardianAura => "heal_depth", _ => null
        };
        if (key != null) AudioManager.Instance?.PlaySFXVariationAt(CombatClip(key), position, automatic ? .62f : .92f,
            automatic ? SfxPriority.Low : SfxPriority.Medium);
    }

    // Индивидуальная громкость клипа: 1f, если запись не задана.
    public float GetClipVolume(AudioClip clip)
    {
        if (clip == null)
            return 1f;

        if (combatVolumes.TryGetValue(clip, out float combatVolume)) return combatVolume;

        EnsureClipVolumesBuilt();

        if (clipVolumes.TryGetValue(
            clip,
            out float volume))
        {
            return volume;
        }

        return 1f;
    }

    private void EnsureClipVolumesBuilt()
    {
        if (clipVolumesBuilt)
            return;

        clipVolumesBuilt = true;

        clipVolumes.Clear();

        if (sfxVolumeOverrides == null)
            return;

        for (int i = 0; i < sfxVolumeOverrides.Length; i++)
        {
            SfxVolumeEntry entry = sfxVolumeOverrides[i];

            if (entry.clip == null)
                continue;

            clipVolumes[entry.clip] =
                Mathf.Clamp01(entry.volume);
        }
    }
}
