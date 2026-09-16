using UnityEngine;

[CreateAssetMenu(
    fileName = "Effect_Bleed_",
    menuName = "Arcade Survivor/Effects/Bleeding"
)]
public class BleedingEffectData : UpgradeEffectData
{
    [Header("Bleeding Parameters")]
    [Range(0f, 1f)]
    [SerializeField] private float chance = 0.20f;

    [SerializeField] private float damage = 2f;
    [SerializeField] private float duration = 3f;
    [SerializeField] private float tickInterval = 0.5f;

    public float Chance => chance;
    public float Damage => damage;
    public float Duration => duration;

    public float TickInterval =>
        Mathf.Max(tickInterval, 0.05f);
}
