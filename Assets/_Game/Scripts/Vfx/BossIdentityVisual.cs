using UnityEngine;
using UnityEngine.Rendering;

// Distinct silhouettes built from the game's existing geometric art style.
public sealed class BossIdentityVisual : MonoBehaviour
{
    public void Build(BossData data)
    {
        if (transform.childCount > 0) return;
        switch (data.AbilityPattern)
        {
            case BossAbilityPattern.TargetedStrike:
                Part(new Vector3(-0.7f, 1f, 0.6f), new Vector3(0.3f, 0.7f, 0.8f), data.BossTint);
                Part(new Vector3(0.7f, 1f, 0.6f), new Vector3(0.3f, 0.7f, 0.8f), data.BossTint);
                break;
            case BossAbilityPattern.MortarBarrage:
                for (int i = -1; i <= 1; i++)
                    Part(new Vector3(i * 0.55f, 1.25f, 0f), new Vector3(0.3f, 0.6f, 1.2f), data.BossTint);
                break;
            case BossAbilityPattern.RadialBarrage:
                Ring(8, 1.2f, 0.7f, data.BossTint);
                break;
            case BossAbilityPattern.SummoningRitual:
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * Mathf.PI * 2f / 3f;
                    Part(new Vector3(Mathf.Cos(angle), 1.1f, Mathf.Sin(angle)),
                        new Vector3(0.5f, 0.8f, 0.5f), data.BossTint);
                }
                break;
            case BossAbilityPattern.Singularity:
                Ring(12, 1.25f, 0.35f, data.BossTint);
                Ring(8, 0.8f, 1.5f, data.BossTint);
                break;
        }
    }

    private void Ring(int count, float radius, float height, Color color)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Part(new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius),
                Vector3.one * 0.22f, color);
        }
    }

    private void Part(Vector3 position, Vector3 scale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = "SignaturePart";
        Collider collider = part.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        part.transform.SetParent(transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = VfxSharedAssets.BulletMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_RimColor", color);
        properties.SetFloat("_Intensity", 0.9f);
        renderer.SetPropertyBlock(properties);
    }
}
