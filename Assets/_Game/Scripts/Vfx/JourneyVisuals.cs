using UnityEngine;
using UnityEngine.Rendering;

public static class JourneyVisuals
{
    public static LineRenderer Line(Transform parent, string name, Color color, float width = 0.06f)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = VfxSharedAssets.TracerMaterial;
        var block = new MaterialPropertyBlock(); block.SetColor("_GlowColor", color); block.SetColor("_CoreColor", color);
        block.SetFloat("_Intensity", 1f); block.SetFloat("_TailPower", 0f); line.SetPropertyBlock(block);
        line.startColor = line.endColor = color; line.widthMultiplier = width;
        line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false; line.useWorldSpace = false;
        return line;
    }
    public static LineRenderer Ring(Transform parent, string name, Color color, float radius, float height = 0.1f, int segments = 20)
    {
        var line = Line(parent, name, color); line.loop = true; line.positionCount = segments;
        for (int i = 0; i < segments; i++)
        { float a = i * Mathf.PI * 2 / segments; line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius)); }
        return line;
    }
    public static Color RewardColor(string id)
    {
        uint hash = 2166136261; foreach (char c in id ?? "") hash = (hash ^ c) * 16777619;
        return Color.HSVToRGB((hash % 360) / 360f, 0.65f, 1f);
    }
}
