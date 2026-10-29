using UnityEngine;
using UnityEngine.Rendering;

// Two lightweight, collider-free markers using an existing shared VFX material.
// Wet: a blue ground ring. Ice: a pale faceted crown that thins as ice breaks.
public sealed class EnemyMapStatusVisual : MonoBehaviour
{
    private Enemy enemy;
    private LineRenderer wet;
    private LineRenderer ice;
    private const int Segments = 16;

    public void Bind(Enemy owner)
    {
        enemy = owner;
        if (wet == null && owner.IsWet)
            wet = Build("WetMarker", new Color(0.15f, 0.6f, 1f), false);
        if (ice == null && owner.IceHealth > 0f)
            ice = Build("IceCoating", new Color(0.65f, 0.95f, 1f), true);
        enabled = true;
        Refresh();
    }

    private LineRenderer Build(string markerName, Color color, bool crystalline)
    {
        var host = new GameObject(markerName);
        host.transform.SetParent(transform, false);
        var line = host.AddComponent<LineRenderer>();
        line.sharedMaterial = VfxSharedAssets.TracerMaterial;
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_GlowColor", color);
        properties.SetColor("_CoreColor", color);
        properties.SetFloat("_Intensity", 1f);
        properties.SetFloat("_TailPower", 0f);
        line.SetPropertyBlock(properties);
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Segments;
        line.widthMultiplier = crystalline ? 0.07f : 0.045f;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            float radius = crystalline ? (i % 2 == 0 ? 0.78f : 0.65f) : 0.85f;
            float height = crystalline ? (i % 2 == 0 ? 1.1f : 0.35f) : 0.08f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
        }
        return line;
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        bool alive = enemy != null && !enemy.IsDead;
        if (wet != null) wet.enabled = alive && enemy.IsWet;
        if (ice != null)
        {
            ice.enabled = alive && enemy.IceHealth > 0f;
            if (ice.enabled)
                ice.widthMultiplier = Mathf.Lerp(0.02f, 0.07f, enemy.IceFraction);
        }
        if ((!alive || !enemy.IsWet) && (!alive || enemy.IceHealth <= 0f))
            enabled = false;
    }

    private void OnDisable()
    {
        if (wet != null) wet.enabled = false;
        if (ice != null) ice.enabled = false;
    }
}
