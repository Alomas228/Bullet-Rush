using UnityEngine;

public sealed class JourneyCosmetic : MonoBehaviour
{
    private string equipped;
    private int rank = -1;
    private Transform ornament;
    private float nextCheck;
    private Weapon weapon;
    private WeaponData lastWeapon;
    private void Update()
    {
        if (ornament != null) ornament.localRotation = Quaternion.Euler(0, Time.time * 35, 0);
        if (Time.unscaledTime < nextCheck || GameplayJourney.Instance == null) return;
        nextCheck = Time.unscaledTime + 0.5f;
        var profile = GameplayJourney.Instance.Data;
        if (weapon == null) weapon = GetComponentInChildren<Weapon>();
        var currentWeapon = weapon != null ? weapon.Data : null;
        if (equipped == profile.equippedTrial && rank == profile.rank && currentWeapon == lastWeapon) return;
        equipped = profile.equippedTrial; rank = profile.rank; lastWeapon = currentWeapon;
        if (ornament != null) Destroy(ornament.gameObject);
        if (string.IsNullOrEmpty(equipped) && rank == 0) return;
        var root = new GameObject("JourneyInsignia"); root.transform.SetParent(transform, false); ornament = root.transform;
        Color color = string.IsNullOrEmpty(equipped) ? new Color(1, 0.78f, 0.3f) : JourneyVisuals.RewardColor(equipped);
        JourneyVisuals.Ring(ornament, "Insignia", color, 0.85f, 0.12f, 4 + rank * 2);
        if (weapon != null && !string.IsNullOrEmpty(equipped))
        {
            foreach (var renderer in weapon.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.sharedMaterial == null) continue;
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", Color.Lerp(Color.white, color, 0.5f));
                block.SetColor("_Color", Color.Lerp(Color.white, color, 0.5f)); renderer.SetPropertyBlock(block);
            }
        }
    }
}
