using System;
using UnityEngine;

/// <summary>
/// A short-lived, readable line warning for directional boss attacks.
/// The callback is fired only after the warning has finished.
/// </summary>
public sealed class BossLineTelegraph : MonoBehaviour
{
    private LineRenderer line;
    private float remaining;
    private Action completed;
    private Color baseColor;

    public static BossLineTelegraph Spawn(
        Vector3 start,
        Vector3 end,
        float duration,
        Color color,
        Action onComplete)
    {
        GameObject lineObject = new GameObject("BossLineTelegraph");
        BossLineTelegraph telegraph =
            lineObject.AddComponent<BossLineTelegraph>();

        telegraph.Initialize(
            start,
            end,
            duration,
            color,
            onComplete);

        return telegraph;
    }

    private void Initialize(
        Vector3 start,
        Vector3 end,
        float duration,
        Color color,
        Action onComplete)
    {
        remaining = Mathf.Max(duration, 0f);
        completed = onComplete;
        baseColor = color;
        baseColor.a = Mathf.Max(baseColor.a, 0.75f);

        start.y = 0.09f;
        end.y = 0.09f;

        line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = 0.16f;
        line.endWidth = 0.08f;
        line.material = VfxSharedAssets.TraceBoltMaterial;
        line.startColor = baseColor;
        line.endColor = baseColor;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    private void Update()
    {
        remaining -= Time.deltaTime;

        float pulse =
            0.65f + 0.35f * Mathf.Sin(Time.time * 12f);

        Color color = baseColor;
        color.a *= pulse;
        line.startColor = color;
        line.endColor = color;

        if (remaining > 0f)
            return;

        Action callback = completed;
        completed = null;
        callback?.Invoke();
        Destroy(gameObject);
    }
}
