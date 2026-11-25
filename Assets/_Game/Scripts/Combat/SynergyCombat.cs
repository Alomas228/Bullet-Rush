using UnityEngine;
using static GameplayJourney;

public sealed class SynergyCombat : MonoBehaviour
{
    private static int pools;
    private float remaining = 3, nextTick;
    private float damage;
    private string weapon;
    public static string Description(string id)
    {
        switch (id)
        {
            case "Synergy_Firestorm": return T("Огненный разрыв: горящий враг оставляет огонь на 3 с. До 3 областей одновременно.", "Fire rupture: burning enemies leave a 3-second fire pool. Up to 3 pools.", "Ateş yarığı: yanan düşmanlar 3 saniyelik ateş bırakır. En fazla 3 alan.");
            case "Synergy_ChainReaction": return T("Электрический узел: каждые 3 попадания молнией по живой цели вызывают разряд по 3 соседям. Пауза 2 с.", "Electric node: every 3 lightning hits on a surviving target discharge into 3 neighbours. 2-second cooldown.", "Elektrik düğümü: yaşayan hedefe her 3 yıldırım, 3 komşuya boşalır. 2 saniye bekleme.");
            case "Synergy_BloodBomb": return T("Кровавый каскад: смерть с 3+ стаками переносит часть кровотечения 3 соседям. Перенос не запускает сам себя.", "Blood cascade: death with 3+ stacks transfers part of bleeding to 3 neighbours. Transfers cannot chain themselves.", "Kan çağlayanı: 3+ yükle ölüm, kanamanın bir kısmını 3 komşuya aktarır. Aktarımlar kendini zincirlemez.");
            default: return "";
        }
    }
    public static void FirePool(Vector3 position, float dps, string weaponId)
    {
        if (pools >= 3 || dps <= 0) return;
        var pool = new GameObject("SynergyFirePool").AddComponent<SynergyCombat>();
        pools++; pool.transform.position = position; pool.damage = dps * 0.6f; pool.weapon = weaponId;
        JourneyVisuals.Ring(pool.transform, "FireBoundary", new Color(1, 0.45f, 0.1f), 2.2f);
        VfxFactory.TrySpawnExplosion(position, 1.2f, new Color(1, 0.4f, 0.1f), soundKey: "fire_rupture");
    }
    private void Update()
    {
        if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing) { Destroy(gameObject); return; }
        remaining -= Time.deltaTime; nextTick -= Time.deltaTime;
        if (remaining <= 0) { Destroy(gameObject); return; }
        if (nextTick > 0 || Time.deltaTime <= 0) return;
        nextTick = 0.5f;
        foreach (var enemy in Enemy.SnapshotAlive())
            if (enemy != null && !enemy.IsDead && (enemy.transform.position - transform.position).sqrMagnitude <= 2.2f * 2.2f)
                enemy.TakeDamage(damage * 0.5f, false, DamageSource.Burn, 0, weapon, true);
    }
    private void OnDestroy() { pools = Mathf.Max(0, pools - 1); }
    public static void Discharge(Enemy source, float damage, string weapon)
    {
        int count = 0, id = DamageEventId.Next();
        foreach (var enemy in Enemy.SnapshotAlive())
        {
            if (count >= 3) break;
            if (enemy == null || enemy == source || enemy.IsDead || (enemy.transform.position - source.transform.position).sqrMagnitude > 16) continue;
            VfxFactory.SpawnLightning(source.transform.position, enemy.transform.position);
            enemy.TakeDamage(damage, false, DamageSource.ChainLightning, id, weapon, true); count++;
        }
        if (count > 0 && AudioManager.Instance != null && AudioManager.Instance.SFXLibrary != null)
            AudioManager.Instance.PlaySFXVariationAt(AudioManager.Instance.SFXLibrary.Lightning, source.transform.position, .65f, SfxPriority.Low);
    }
}
