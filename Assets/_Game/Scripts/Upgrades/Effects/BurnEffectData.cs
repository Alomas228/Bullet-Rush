using UnityEngine;

[CreateAssetMenu(
    fileName = "Effect_Burn_",
    menuName = "Arcade Survivor/Effects/Burn"
)]
public class BurnEffectData : UpgradeEffectData
{
    [Header("Burn Parameters")]
    [Range(0f, 1f)]
    [SerializeField] private float chance = 0.20f;

    [SerializeField] private float damage = 3f;
    [SerializeField] private float duration = 2f;
    [SerializeField] private float tickInterval = 0.5f;

    public float Chance => chance;
    public float Damage => damage;
    public float Duration => duration;

    public float TickInterval =>
        Mathf.Max(tickInterval, 0.05f);
}
