using UnityEngine;

[CreateAssetMenu(
    fileName = "Effect_Lifesteal_",
    menuName = "Arcade Survivor/Effects/Lifesteal"
)]
public class LifestealEffectData : UpgradeEffectData
{
    [Header("Lifesteal Parameters")]
    [Range(0f, 1f)]
    [SerializeField] private float percentage = 0.10f;

    public float Percentage => percentage;
}
