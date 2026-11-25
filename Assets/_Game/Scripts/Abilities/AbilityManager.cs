using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class AbilityManager : MonoBehaviour
{
    [Header("Abilities")]
    [Tooltip("Не обязательно назначать вручную — найдутся на игроке.")]
    [SerializeField] private BombAbility bombAbility;
    [SerializeField] private ShieldAbility shieldAbility;

    private bool inputEnabled;
    private bool subscribed;
    private sealed class RuntimeAbility
    {
        public AbilityData Data;
        public float Left, RetryAt;
    }
    public readonly struct SlotInfo
    {
        public readonly bool Exists, Ready;
        public readonly AbilityKind Kind;
        public readonly float Remaining, Duration;
        public SlotInfo(AbilityKind kind, float remaining, float duration, bool ready)
        { Exists = true; Kind = kind; Remaining = Mathf.Max(0f,remaining); Duration = duration; Ready = ready; }
    }
    private readonly AbilityKind?[] activeKinds = new AbilityKind?[AbilityPresentation.ActiveSlots];
    private readonly List<RuntimeAbility> autoAbilities = new(AbilityPresentation.AutomaticSlots);
    private readonly List<RuntimeAbility> activeAbilities = new(AbilityPresentation.ActiveSlots);
    private readonly ArenaAbilityCombat combat = new();
    private PlayerStats stats;
    private RunMetrics runMetrics;
    private float abilityPower = 1f, abilityRange = 1f, abilityCooldown = 1f;
    private float characterPower=1f, characterRange=1f, characterCooldown=1f;
    private AbilityKind? innateActive, innateAuto;
    private bool IsInnate(AbilityKind kind) => innateActive==kind||innateAuto==kind;
    public void SetInnate(AbilityKind active,AbilityKind auto) { innateActive=active; innateAuto=auto; }
    public void UpgradeCharacter(float power,float range,float haste)
    {
        float oldPower=characterPower,oldRange=characterRange,oldCooldown=characterCooldown;
        characterPower=Mathf.Min(2.2f,characterPower*(1f+power)); characterRange=Mathf.Min(1.5f,characterRange*(1f+range)); characterCooldown=Mathf.Max(.6f,characterCooldown*(1f-haste));
        power=characterPower/oldPower-1f; range=characterRange/oldRange-1f; haste=1f-characterCooldown/oldCooldown;
        if(HasBomb&&IsInnate(AbilityKind.Bomb)) { bombAbility.UpgradeDamage(power); bombAbility.UpgradeRadius(range); bombAbility.UpgradeCooldown(haste); }
        if(HasShield&&IsInnate(AbilityKind.Shield)) { shieldAbility.UpgradeDuration(power*.5f); shieldAbility.UpgradeCooldown(haste); }
    }
    public bool HasAny => HasBomb || HasShield || activeAbilities.Count > 0 || autoAbilities.Count > 0;
    public bool HasDamagingAbility => HasBomb || activeAbilities.Count > 0 || autoAbilities.Count > 0;
    public bool CanUnlock(AbilityKind kind) => SlotIndex(kind) >= 0 || FirstFreeSlot() >= 0;
    public int AutomaticCount => autoAbilities.Count;

    public bool HasBomb =>
        bombAbility != null && bombAbility.IsUnlocked;

    public bool HasShield =>
        shieldAbility != null && shieldAbility.IsUnlocked;

    public BombAbility Bomb => bombAbility;
    public ShieldAbility Shield => shieldAbility;

    internal void ResetForSession()
    {
        inputEnabled = false;
        System.Array.Clear(activeKinds,0,activeKinds.Length);
        autoAbilities.Clear(); activeAbilities.Clear(); abilityPower = abilityRange = abilityCooldown = 1f;
        characterPower=characterRange=characterCooldown=1f; innateActive=innateAuto=null;
        AbilityVfxBatch.Clear(); runMetrics = null;
        if (bombAbility != null) bombAbility.ResetForSession();
        if (shieldAbility != null) shieldAbility.ResetForSession();
    }

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        if (bombAbility == null)
            bombAbility = GetComponent<BombAbility>();

        if (shieldAbility == null)
            shieldAbility = GetComponent<ShieldAbility>();
    }

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void Start()
    {
        EnsureSubscribed();
    }

    private void EnsureSubscribed()
    {
        if (subscribed)
            return;

        if (GameStateManager.Instance == null)
            return;

        GameStateManager.Instance.OnGameStateChanged += HandleStateChanged;
        subscribed = true;

        inputEnabled =
            GameStateManager.Instance.CurrentState == GameState.Playing;
    }

    private void OnDisable()
    {
        AbilityVfxBatch.Clear();
        if (subscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;
            subscribed = false;
        }
    }

    private void HandleStateChanged(GameState state)
    {
        inputEnabled = state == GameState.Playing;
    }

    private void Update()
    {
        if (!CanUse) return;
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.qKey.wasPressedThisFrame) TryUseActive(0);
            if (keyboard.eKey.wasPressedThisFrame) TryUseActive(1);
        }
        float dt = Time.deltaTime;
        foreach (var runtime in activeAbilities) runtime.Left = Mathf.Max(0f, runtime.Left-dt);
        float orbitRadius = 0f; bool familiar = false;
        foreach (var runtime in autoAbilities)
        {
            runtime.Left = Mathf.Max(0f,runtime.Left-dt);
            if (runtime.Data.Kind == AbilityKind.OrbitBlades) orbitRadius = Mathf.Clamp(runtime.Data.Radius*abilityRange,.1f,9f);
            if (runtime.Data.Kind == AbilityKind.ShadowFamiliar) familiar = true;
            if (runtime.Left > 0f || Time.time < runtime.RetryAt) continue;
            runtime.RetryAt = Time.time + .20f;
            if (Cast(runtime)) runtime.Left = Duration(runtime);
        }
        AbilityVfxBatch.SetCompanions(transform,orbitRadius>0f,orbitRadius,familiar);
    }

    private bool CanUse => isActiveAndEnabled && inputEnabled && Time.timeScale > 0f &&
        !GameModeManager.IsPvp && !RamLiftArena.InTransit && !JourneyUI.IsBlocking;

    public bool TryUseBomb() => CanUse && HasBomb && bombAbility.TryActivate();
    public bool TryUseShield() => CanUse && HasShield && shieldAbility.TryActivate();

    private int SlotIndex(AbilityKind kind)
    {
        for (int i=0;i<activeKinds.Length;i++) if (activeKinds[i] == kind) return i;
        return -1;
    }
    private int FirstFreeSlot()
    {
        for (int i=0;i<activeKinds.Length;i++) if (!activeKinds[i].HasValue) return i;
        return -1;
    }
    private bool Reserve(AbilityKind kind, int preferred = -1)
    {
        if (SlotIndex(kind)>=0) return true;
        if(innateActive==kind) preferred=0;
        int slot = preferred >= 0 && preferred < activeKinds.Length && !activeKinds[preferred].HasValue ? preferred : FirstFreeSlot();
        if (slot < 0) return false;
        activeKinds[slot] = kind; return true;
    }
    private RuntimeAbility Find(AbilityKind kind)
    {
        foreach (var runtime in activeAbilities) if (runtime.Data.Kind == kind) return runtime;
        foreach (var runtime in autoAbilities) if (runtime.Data.Kind == kind) return runtime;
        return null;
    }
    public bool EquipAbility(AbilityData data)
    {
        if (data == null || GameModeManager.IsPvp) return false;
        if (data.Kind == AbilityKind.Bomb) return UnlockBomb();
        if (data.Kind == AbilityKind.Shield) return UnlockShield();
        if (Find(data.Kind) != null) return true;
        if (data.IsAutomatic)
        {
            if (autoAbilities.Count >= AbilityPresentation.AutomaticSlots) return false;
            autoAbilities.Add(new RuntimeAbility { Data=data, Left=data.Cooldown*.35f });
        }
        else
        {
            if (!Reserve(data.Kind)) return false;
            activeAbilities.Add(new RuntimeAbility {Data=data});
        }
        AbilityVfxBatch.Prepare();
        return true;
    }
    private float Duration(RuntimeAbility runtime) => Mathf.Max(.45f,runtime.Data.Cooldown*abilityCooldown*(IsInnate(runtime.Data.Kind)?characterCooldown:1f));
    private bool Cast(RuntimeAbility runtime)
    {
        bool cast = combat.Cast(runtime.Data,transform,
            abilityPower*(stats!=null?stats.DamageMultiplier:1f)*RunBuildMechanics.EffectFactor*(IsInnate(runtime.Data.Kind)?characterPower:1f),abilityRange*(IsInnate(runtime.Data.Kind)?characterRange:1f));
        if (cast) AudioManager.Instance?.SFXLibrary?.PlayAbility(runtime.Data.Kind, transform.position, runtime.Data.IsAutomatic);
        return cast;
    }
    public bool TryUseActive(int index)
    {
        if (!CanUse || index<0 || index>=activeKinds.Length || !activeKinds[index].HasValue) return false;
        AbilityKind kind=activeKinds[index].Value;
        if (kind==AbilityKind.Bomb) return TryUseBomb();
        if (kind==AbilityKind.Shield) return TryUseShield();
        RuntimeAbility runtime=Find(kind);
        if (runtime==null || runtime.Left>0f || !Cast(runtime)) return false;
        runtime.Left=Duration(runtime);
        if(runMetrics==null) runMetrics=FindAnyObjectByType<RunMetrics>();
        runMetrics?.OnAbilityUsed(); return true;
    }
    public SlotInfo GetActiveSlot(int index)
    {
        if(index<0||index>=activeKinds.Length||!activeKinds[index].HasValue) return default;
        AbilityKind kind=activeKinds[index].Value;
        if(kind==AbilityKind.Bomb && HasBomb) return new SlotInfo(kind,bombAbility.CooldownRemaining,bombAbility.Cooldown,bombAbility.IsReady);
        if(kind==AbilityKind.Shield && HasShield) return new SlotInfo(kind,shieldAbility.CooldownRemaining,shieldAbility.Cooldown,shieldAbility.IsReady&&!shieldAbility.IsActive);
        RuntimeAbility runtime=Find(kind);
        return runtime!=null?new SlotInfo(kind,runtime.Left,Duration(runtime),runtime.Left<=0f):default;
    }
    public SlotInfo GetAutomaticSlot(int index)
    {
        if(index<0||index>=autoAbilities.Count) return default;
        var runtime=autoAbilities[index];
        return new SlotInfo(runtime.Data.Kind,runtime.Left,Duration(runtime),runtime.Left<=0f);
    }
    public void UpgradePower(float percent)
    {
        float before=abilityPower; abilityPower=Mathf.Min(3f,abilityPower*(1f+Mathf.Max(0f,percent)));
        float applied=abilityPower/before-1f;
        if(HasBomb) bombAbility.UpgradeDamage(applied);
        if(HasShield) shieldAbility.UpgradeDuration(applied*.5f);
    }
    public void UpgradeRange(float percent)
    {
        float before=abilityRange; abilityRange=Mathf.Min(1.5f,abilityRange*(1f+Mathf.Max(0f,percent)));
        if(HasBomb) bombAbility.UpgradeRadius(abilityRange/before-1f);
    }
    public void ReduceCooldown(float percent)
    {
        float old=abilityCooldown; abilityCooldown=Mathf.Max(.50f,abilityCooldown*(1f-Mathf.Clamp01(percent)));
        float applied=1f-abilityCooldown/old;
        if(HasBomb) bombAbility.UpgradeCooldown(applied);
        if(HasShield) shieldAbility.UpgradeCooldown(applied);
    }

    public bool UnlockBomb()
    {
        if (HasBomb) return true;
        if (!Reserve(AbilityKind.Bomb,1)) return false;
        if (bombAbility == null)
        {
            bombAbility =
                gameObject.AddComponent<BombAbility>();
        }

        if (bombAbility != null)
        {
            bombAbility.IsUnlocked = true;
            bombAbility.UpgradeDamage(abilityPower-1f); bombAbility.UpgradeRadius(abilityRange-1f); bombAbility.UpgradeCooldown(1f-abilityCooldown);
        }

        return bombAbility != null;
    }

    public bool UnlockShield()
    {
        if (HasShield) return true;
        if (!Reserve(AbilityKind.Shield,0)) return false;
        if (shieldAbility == null)
        {
            shieldAbility =
                gameObject.AddComponent<ShieldAbility>();
        }

        if (shieldAbility != null)
        {
            shieldAbility.IsUnlocked = true;
            shieldAbility.UpgradeDuration((abilityPower-1f)*.5f); shieldAbility.UpgradeCooldown(1f-abilityCooldown);
        }

        return shieldAbility != null;
    }
}
