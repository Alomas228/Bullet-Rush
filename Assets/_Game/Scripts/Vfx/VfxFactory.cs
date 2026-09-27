using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Фабрика процедурных визуальных эффектов для апгрейдов забега.
/// Все эффекты строятся кодом без внешних префабов/спрайтов.
/// </summary>
public static class VfxFactory
{
    private static readonly Color ExplosionColor =
        new Color(1f, 0.55f, 0.1f, 0.8f);

    private static readonly Color LightningColor =
        new Color(0.45f, 0.8f, 1f, 0.95f);

    private static readonly Color RicochetColor =
        new Color(1f, 0.92f, 0.6f, 1f);

    // =========================================================
    // ТРАССЕРЫ И ПОПАДАНИЯ
    // =========================================================

    // Трассер стоит копейки (1 квад, 1 draw call), спавнится
    // без ограничений.
    //
    // Попадание дороже: вспышка + 3 осколка = 4 draw call.
    // При дробовике, попадающем в залп, за один кадр можно
    // получить 30+ попаданий в одну точку, и эффект превратится
    // в белое пятно. Поэтому на кадр есть жёсткий бюджет.
    private const int MaxImpactsPerFrame = 8;

    private static int impactFrame = -1;
    private static int impactsThisFrame;

    /// <summary>
    /// Пулевой трассер. Забирается из пула, тратит один
    /// квад и 0.075 секунды жизни.
    /// </summary>
    /// <param name="position">Точка выстрела.</param>
    /// <param name="direction">Направление полёта пули.</param>
    /// <param name="speed">Скорость пули: задаёт длину хвоста.</param>
    /// <param name="followTarget">Пуля, за которой идёт трассер.</param>
    /// <param name="prefab">Опциональный префаб вместо шаблона.</param>
    public static TracerEffect SpawnTracer(
        Vector3 position,
        Vector3 direction,
        float speed,
        Transform followTarget = null,
        GameObject prefab = null)
    {
        TracerEffect tracer =
            VfxPools.Tracers.Spawn(
                prefab,
                position,
                Quaternion.identity
            );

        if (tracer == null)
            return null;

        tracer.Play(
            direction,
            speed,
            followTarget
        );

        return tracer;
    }

    /// <summary>
    /// Красный трассер снаряда врага. Тот же эффект и тот же
    /// квад, что у пуль игрока, но из своего пула и с красным
    /// материалом - глаз отличает чужую пулю на лету.
    /// </summary>
    /// <param name="position">Точка выстрела.</param>
    /// <param name="direction">Направление полёта снаряда.</param>
    /// <param name="speed">Скорость снаряда: задаёт длину хвоста.</param>
    /// <param name="followTarget">Снаряд, за которым идёт трассер.</param>
    public static TracerEffect SpawnEnemyTracer(
        Vector3 position,
        Vector3 direction,
        float speed,
        Transform followTarget = null)
    {
        TracerEffect tracer =
            VfxPools.EnemyTracers.Spawn(
                null,
                position,
                Quaternion.identity
            );

        if (tracer == null)
            return null;

        tracer.Play(
            direction,
            speed,
            followTarget
        );

        return tracer;
    }

    /// <summary>
    /// Вспышка попадания с осколками.
    /// </summary>
    /// <param name="position">Точка попадания.</param>
    /// <param name="normal">Направление отдачи осколков.</param>
    /// <param name="prefab">Опциональный префаб вместо шаблона.</param>
    public static bool SpawnImpact(
        Vector3 position,
        Vector3 normal,
        GameObject prefab = null)
    {
        if (impactFrame != Time.frameCount)
        {
            impactFrame = Time.frameCount;
            impactsThisFrame = 0;
        }

        if (impactsThisFrame >= MaxImpactsPerFrame)
            return false;

        impactsThisFrame++;

        ImpactEffect impact =
            VfxPools.Impacts.Spawn(
                prefab,
                position,
                Quaternion.identity
            );

        if (impact == null)
            return false;

        impact.Play(position, normal);
        return true;
    }

    // Схлопывание взрывов: если в одном месте уже есть взрыв,
    // случившийся в коротком окне, новый не создаётся.
    // Иначе дробь с улучшением «взрыв при попадании» плодит
    // 6-7 наложенных взрывов.
    private const float ExplosionMergeRadiusFactor = 0.6f;
    private const float ExplosionMergeWindow = 0.2f;
    private const int MaxExplosionRecords = 24;

    private static readonly List<ExplosionRecord> recentExplosions =
        new List<ExplosionRecord>();

    private struct ExplosionRecord
    {
        public Vector3 Position;
        public float Radius;
        public float Time;
    }

    public static bool TrySpawnExplosion(
        Vector3 position,
        float radius)
    {
        if (IsDuplicatedExplosion(position, radius))
            return false;

        GameObject effect =
            new GameObject("BulletExplosion");

        effect.transform.position = position;

        BombExplosionEffect visual =
            effect.AddComponent<BombExplosionEffect>();

        visual.Initialize(
            radius,
            ExplosionColor
        );

        RecordExplosion(position, radius);

        return true;
    }

    private static bool IsDuplicatedExplosion(
        Vector3 position,
        float radius)
    {
        float now = Time.time;

        for (int i = recentExplosions.Count - 1; i >= 0; i--)
        {
            ExplosionRecord record =
                recentExplosions[i];

            if (now - record.Time > ExplosionMergeWindow)
            {
                recentExplosions.RemoveAt(i);
                continue;
            }

            float mergeRadius =
                Mathf.Max(radius, record.Radius) *
                ExplosionMergeRadiusFactor;

            float distanceSqr =
                (record.Position - position).sqrMagnitude;

            if (distanceSqr <=
                mergeRadius * mergeRadius)
            {
                return true;
            }
        }

        return false;
    }

    private static void RecordExplosion(
        Vector3 position,
        float radius)
    {
        if (recentExplosions.Count >=
            MaxExplosionRecords)
        {
            recentExplosions.RemoveAt(0);
        }

        recentExplosions.Add(
            new ExplosionRecord
            {
                Position = position,
                Radius = radius,
                Time = Time.time
            }
        );
    }

    // Схлопывание молний: за один кадр в одном месте разыгрывается
    // только один бросок шанса молнии. Залп дроби попадает за один
    // physics-шаг, поэтому без этого гейта шанс складывается 1-(1-p)^N.
    private const float LightningMergeRadius = 1.5f;

    private static int lastLightningFrame = -1;
    private static Vector3 lastLightningPosition;

    public static bool IsLightningMerged(
        Vector3 position)
    {
        if (lastLightningFrame != Time.frameCount)
            return false;

        float squareRadius =
            LightningMergeRadius *
            LightningMergeRadius;

        return
            (position - lastLightningPosition).sqrMagnitude <=
            squareRadius;
    }

    public static void MarkLightningAttempt(
        Vector3 position)
    {
        lastLightningFrame = Time.frameCount;
        lastLightningPosition = position;
    }

    public static void SpawnLightning(
        Vector3 from,
        Vector3 to)
    {
        GameObject effect =
            new GameObject("ChainLightning");

        TraceBoltEffect bolt =
            effect.AddComponent<TraceBoltEffect>();

        bolt.Initialize(
            from,
            to,
            LightningColor,
            true,
            true
        );
    }

    public static void SpawnRicochet(
        Vector3 from,
        Vector3 to)
    {
        GameObject effect =
            new GameObject("RicochetBolt");

        TraceBoltEffect bolt =
            effect.AddComponent<TraceBoltEffect>();

        bolt.Initialize(
            from,
            to,
            RicochetColor,
            false,
            true
        );
    }
}