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

    public AudioClip PlayerHit => playerHit;
    public AudioClip PlayerDie => playerDie;
    public AudioClip PlayerAbilityExplosion => playerAbilityExplosion;
    public AudioClip Dash => dash;

    public AudioClip WeaponFire => weaponFire;
    public AudioClip WeaponSwitch => weaponSwitch;
    public AudioClip BulletExplosion => bulletExplosion;
    public AudioClip Ricochet => ricochet;
    public AudioClip Lightning => lightning;

    public AudioClip EnemySpawn => enemySpawn;
    public AudioClip EnemyHit => enemyHit;
    public AudioClip EnemyDie => enemyDie;
    public AudioClip EnemyShot => enemyShot;

    public AudioClip BossSpawn => bossSpawn;
    public AudioClip BossPhaseChange => bossPhaseChange;
    public AudioClip BossAbility => bossAbility;
    public AudioClip BossAoeExplode => bossAoeExplode;

    public AudioClip CountdownTick => countdownTick;
    public AudioClip WaveStart => waveStart;
    public AudioClip UpgradePick => upgradePick;
    public AudioClip MenuOpen => menuOpen;
    public AudioClip StartGame => startGame;
    public AudioClip GameOver => gameOver;
    public AudioClip UiClick => uiClick;

    public AudioClip PickupSpawn => pickupSpawn;
    public AudioClip PickupCollect => pickupCollect;

    // Индивидуальная громкость клипа: 1f, если запись не задана.
    public float GetClipVolume(AudioClip clip)
    {
        if (clip == null)
            return 1f;

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
