using UnityEngine;

[CreateAssetMenu(
    fileName = "Effect_Explosion_",
    menuName = "Arcade Survivor/Effects/Explosion"
)]
public class ExplosionEffectData : UpgradeEffectData
{
    [Header("Explosion Parameters")]
    [SerializeField] private float radius = 3f;
    [SerializeField] private float damage = 5f;

    public float Radius => radius;
    public float Damage => damage;
}
