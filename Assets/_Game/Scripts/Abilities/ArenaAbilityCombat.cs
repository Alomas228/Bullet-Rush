using System.Collections.Generic;
using UnityEngine;

internal sealed class ArenaAbilityCombat
{
    private readonly List<Enemy> enemies = new(64);
    private readonly List<Enemy> visited = new(8);
    private static readonly Color Gold = new Color32(248,199,99,255), Ice = new Color32(129,216,244,255),
        Fire = new Color32(249,136,63,255), Purple = new Color32(184,138,239,255), Electric = new Color32(124,200,250,255);

    public bool Cast(AbilityData data, Transform player, float power, float radiusMultiplier)
    {
        if (data == null || player == null) return false;
        if (data.IsAutomatic && Enemy.AliveCount <= 0) return false;
        Vector3 origin = player.position, forward = player.forward; forward.y = 0; forward.Normalize();
        float range = Mathf.Clamp(data.Radius * radiusMultiplier, .1f, 9f);
        float damage = data.Damage * power;
        int max = data.MaxTargets, id = DamageEventId.Next();
        Enemy.CopyAliveTo(enemies);
        switch (data.Kind)
        {
            case AbilityKind.CrescentSlash:
                Area(origin, range, damage, max, id, forward, 1, 0f); AbilityVfxBatch.Arc(origin, forward, range, Gold); return true;
            case AbilityKind.ThunderSlam:
                Area(origin, range, damage, max, id, forward, 2, 0f); AbilityVfxBatch.Thunder(origin, range, Gold); return true;
            case AbilityKind.FrostNova:
                Area(origin, range, damage, max, id, forward, 3, 2.5f); AbilityVfxBatch.Frost(origin, range, Ice); return true;
            case AbilityKind.GravityWell:
                Vector3 center = origin + forward * 3.1f;
                Area(center, range, damage, max, id, forward, 4, 0f); AbilityVfxBatch.Gravity(center, range, Purple); return true;
            case AbilityKind.OrbitBlades:
                if (Area(origin, range, damage, max, id, forward, 0, 0f) == 0) return false;
                AbilityVfxBatch.Ring(origin, range, Gold, .24f); return true;
            case AbilityKind.ShadowFamiliar:
                Enemy target = Nearest(origin, range);
                if (target == null) return false;
                Vector3 targetPosition = target.transform.position;
                Hit(target, damage, id); AbilityVfxBatch.Beam(AbilityVfxBatch.FamiliarPosition(player),targetPosition,Purple); return true;
            case AbilityKind.LightningStorm:
                return Chain(origin, range, damage, max, id);
            case AbilityKind.FlameRing:
                if (Area(origin, range, damage, max, id, forward, 5, 2.5f) == 0) return false;
                AbilityVfxBatch.Flame(origin, range, Fire); return true;
            case AbilityKind.IceLance:
                Enemy closest = Nearest(origin, range);
                if (closest == null) return false;
                Vector3 direction = Flat(closest.transform.position - origin).normalized;
                if (direction.sqrMagnitude < .01f) direction = forward;
                Lance(origin,direction,range,damage,max,id);
                AbilityVfxBatch.Lance(origin,origin+direction*range,Ice); return true;
            case AbilityKind.FallingStar:
                Enemy group = Nearest(origin,range);
                if (group == null) return false;
                Vector3 point = group.transform.position;
                float impact = Mathf.Min(3.8f,2.65f*radiusMultiplier);
                Area(point,impact,damage,max,id,forward,0,0f); AbilityVfxBatch.Star(point,impact,Gold); return true;
            case AbilityKind.VampirePulse:
                int victims=Area(origin,range,damage,max,id,forward,0,0f);
                var vampire=player.GetComponent<PlayerHealth>();
                if(vampire!=null&&victims>0) vampire.Heal(vampire.MaxHealth*Mathf.Min(.06f,victims*.012f));
                AbilityVfxBatch.Thunder(origin,range,new Color32(225,80,99,255)); return true;
            case AbilityKind.BeeSwarm:
                visited.Clear(); bool stung=false;
                for(int i=0;i<max;i++)
                {
                    Enemy beeTarget=Nearest(origin,range,true); if(beeTarget==null) break;
                    visited.Add(beeTarget); Vector3 pos=beeTarget.transform.position;
                    Hit(beeTarget,damage,id); if(Valid(beeTarget)) beeTarget.ApplyBleeding(damage*.18f,2.0f,.5f);
                    AbilityVfxBatch.Beam(origin+Vector3.up*.6f,pos,Gold); stung=true;
                }
                return stung;
            case AbilityKind.RicochetVolley:
                visited.Clear(); Enemy bounce=Nearest(origin,range); if(bounce==null) return false;
                Vector3 from=origin;
                for(int i=0;i<max&&bounce!=null;i++)
                {
                    Vector3 pos=bounce.transform.position; visited.Add(bounce);
                    Hit(bounce,damage*Mathf.Pow(.82f,i),id); AbilityVfxBatch.Beam(from,pos,Gold);
                    from=pos; bounce=Nearest(pos,3.4f,true);
                }
                return true;
            case AbilityKind.GuardianAura:
                var health=player.GetComponent<PlayerHealth>();
                if(health==null||health.CurrentHealth>=health.MaxHealth-.01f) return false;
                health.Heal(Mathf.Min(damage,health.MaxHealth*.05f));
                AbilityVfxBatch.Ring(origin,range,new Color32(126,220,150,255),.42f); return true;
            default: return false;
        }
    }

    private static Vector3 Flat(Vector3 value) { value.y = 0; return value; }
    private static bool Valid(Enemy enemy) => enemy != null && !enemy.IsDead;
    private static void Hit(Enemy enemy, float damage, int id) => enemy.TakeDamage(damage, false, DamageSource.Ability, id);
    private int Area(Vector3 origin, float radius, float damage, int max, int id, Vector3 forward, int mode, float duration)
    {
        int hits = 0; float sq = radius * radius;
        foreach (Enemy enemy in enemies)
        {
            if (hits >= max) break;
            if (!Valid(enemy)) continue;
            Vector3 delta = Flat(enemy.transform.position - origin);
            float d2 = delta.sqrMagnitude;
            if (d2 > sq || (mode == 1 && d2 > .01f && Vector3.Dot(delta / Mathf.Sqrt(d2), forward) < .5f)) continue;
            Hit(enemy, damage, id); hits++;
            if (!Valid(enemy)) continue;
            switch (mode)
            {
                case 2: enemy.ApplyKnockback(delta.normalized, 4f); break;
                case 3: enemy.ApplyArenaSlow(duration); break;
                case 4: if (d2 > .04f) enemy.ApplyKnockback(-delta.normalized, 3.5f); break;
                case 5: enemy.ApplyBurn(damage * .22f,duration,.5f,null); break;
            }
        }
        return hits;
    }

    private Enemy Nearest(Vector3 origin, float radius, bool excludeVisited = false)
    {
        float best = radius * radius; Enemy result = null;
        foreach (Enemy enemy in enemies)
        {
            if (!Valid(enemy) || (excludeVisited && visited.Contains(enemy))) continue;
            float d = Flat(enemy.transform.position - origin).sqrMagnitude;
            if (d <= best) { best = d; result = enemy; }
        }
        return result;
    }
    private bool Chain(Vector3 origin, float radius, float damage, int max, int id)
    {
        visited.Clear(); Enemy next = Nearest(origin,radius);
        if (next == null) return false;
        Vector3 from = origin;
        if (next.IsWet) max += 2;
        for (int i = 0; i < max && next != null; i++)
        {
            Vector3 p = next.transform.position;
            bool wet = next.IsWet;
            visited.Add(next);
            next.TakeDamage(damage*Mathf.Pow(.85f,i), false, DamageSource.ChainLightning, id);
            AbilityVfxBatch.Lightning(from,p,Electric);
            from = p; next = Nearest(p,MapCombatRules.LightningRange(3.8f, wet),true);
        }
        return true;
    }
    private void Lance(Vector3 origin, Vector3 direction, float length, float damage, int max, int id)
    {
        int count = 0;
        foreach (Enemy enemy in enemies)
        {
            if (count >= max) break;
            if (!Valid(enemy)) continue;
            Vector3 delta = Flat(enemy.transform.position-origin);
            float along = Vector3.Dot(delta,direction);
            if (along < 0 || along > length || (delta-direction*along).sqrMagnitude > .81f) continue;
            Hit(enemy,damage,id); count++;
            if (Valid(enemy)) enemy.ApplyArenaSlow(1.6f);
        }
    }
}
