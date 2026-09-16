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

    public static void SpawnExplosion(
        Vector3 position,
        float radius)
    {
        GameObject effect =
            new GameObject("BulletExplosion");

        effect.transform.position = position;

        BombExplosionEffect visual =
            effect.AddComponent<BombExplosionEffect>();

        visual.Initialize(
            radius,
            ExplosionColor
        );
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