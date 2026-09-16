using UnityEngine;

[CreateAssetMenu(
    fileName = "Effect_ChainLightning_",
    menuName = "Arcade Survivor/Effects/Chain Lightning"
)]
public class ChainLightningEffectData : UpgradeEffectData
{
    [Header("Chain Lightning Parameters")]
    [Range(0f, 1f)]
    [SerializeField] private float chance = 0.20f;

    [SerializeField] private float damage = 5f;
    [SerializeField] private float radius = 4f;
    [SerializeField] private int maxTargets = 2;

    public float Chance => chance;
    public float Damage => damage;
    public float Radius => radius;
    public int MaxTargets => maxTargets;
}
