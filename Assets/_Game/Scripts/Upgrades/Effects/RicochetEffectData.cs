using UnityEngine;

[CreateAssetMenu(
    fileName = "Effect_Ricochet_",
    menuName = "Arcade Survivor/Effects/Ricochet"
)]
public class RicochetEffectData : UpgradeEffectData
{
    [Header("Ricochet Parameters")]
    [Range(0f, 1f)]
    [SerializeField] private float chance = 0.25f;

    [SerializeField] private int maxBounces = 2;
    [SerializeField] private float searchRadius = 5f;
    [SerializeField] private float damageMultiplier = 0.75f;

    public float Chance => chance;
    public int MaxBounces => maxBounces;
    public float SearchRadius => searchRadius;
    public float DamageMultiplier => damageMultiplier;
}
