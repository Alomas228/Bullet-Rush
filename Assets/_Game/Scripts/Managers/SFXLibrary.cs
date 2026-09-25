using UnityEngine;

[CreateAssetMenu(
    fileName = "SFXLibrary",
    menuName = "Arcade Survivor/SFX Library"
)]
public class SFXLibrary : ScriptableObject
{
    [Header("Player")]
    [SerializeField] private AudioClip playerHit;
    [SerializeField] private AudioClip playerDie;

    [Header("Weapon")]
    [SerializeField] private AudioClip weaponFire;
    [SerializeField] private AudioClip weaponSwitch;

    [Header("Enemy")]
    [SerializeField] private AudioClip enemySpawn;
    [SerializeField] private AudioClip enemyHit;
    [SerializeField] private AudioClip enemyDie;

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

    public AudioClip WeaponFire => weaponFire;
    public AudioClip WeaponSwitch => weaponSwitch;

    public AudioClip EnemySpawn => enemySpawn;
    public AudioClip EnemyHit => enemyHit;
    public AudioClip EnemyDie => enemyDie;

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
}
