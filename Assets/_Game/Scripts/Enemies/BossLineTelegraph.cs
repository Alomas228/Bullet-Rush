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
        Action onComplete,
        float halfWidth = 0f)
    {
        GameObject lineObject = new GameObject("BossLineTelegraph");
        BossLineTelegraph telegraph =
            lineObject.AddComponent<BossLineTelegraph>();

        telegraph.Initialize(
            start,
            end,
            duration,
            color,
            onComplete,
            halfWidth);

        return telegraph;
    }

    private void Initialize(
        Vector3 start,
        Vector3 end,
        float duration,
        Color color,
        Action onComplete,
        float halfWidth)
    {
        remaining = Mathf.Max(duration, 0f);
        completed = onComplete;
        baseColor = color;
        baseColor.a = Mathf.Max(baseColor.a, 0.75f);

        start.y = RamLiftArena.FloorY + 0.09f;
        end.y = start.y;
        if (RamLiftArena.Active != null)
            transform.SetParent(RamLiftArena.Active.transform, true);

        line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        if (halfWidth > 0f)
        {
            Vector3 side = Vector3.Cross(Vector3.up, (end - start).normalized) * halfWidth;
            line.loop = true;
            line.positionCount = 4;
            line.SetPositions(new[] { start + side, end + side, end - side, start - side });
        }
        else
        {
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }
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
