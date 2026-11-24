using UnityEngine;

// The journey reward is applied only to the equipped weapon.
public sealed class JourneyCosmetic : MonoBehaviour
{
    private string equipped;
    private float nextCheck;
    private Weapon weapon;
    private WeaponData lastWeapon;
    private MaterialPropertyBlock paint;

    private void Update()
    {
        if (Time.unscaledTime < nextCheck || GameplayJourney.Instance == null) return;
        nextCheck = Time.unscaledTime + .5f;
        if (weapon == null) weapon = GetComponentInChildren<Weapon>();
        if (weapon == null) return;

        string trial = GameplayJourney.Instance.Data.equippedTrial;
        if (equipped == trial && lastWeapon == weapon.Data) return;
        lastWeapon = weapon.Data;
        equipped = trial;
        if (string.IsNullOrEmpty(equipped)) return;

        Color color = Color.Lerp(Color.white, JourneyVisuals.RewardColor(equipped), .5f);
        paint ??= new MaterialPropertyBlock();
        foreach (var renderer in weapon.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.sharedMaterial == null) continue;
            renderer.GetPropertyBlock(paint);
            paint.SetColor("_BaseColor", color);
            paint.SetColor("_Color", color);
            renderer.SetPropertyBlock(paint);
        }
    }
}
