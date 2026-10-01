using System.Collections.Generic;
using UnityEngine;

// Общий прозрачный материал для опасных зон: HazardZone (каст
// элиты, модификатор волны) и BossAttackZone (аое босса).
//
// Раньше каждая зона создавала в Awake три новых материала через
// new Material(...) и Destroy-ила их в OnDestroy. На волне с
// DangerZone это десятки аллокаций материалов за забег, плюс
// зоны не могли батчиться друг с другом: у каждой был свой
// материал.
//
// Теперь материал кэшируется по цвету и делится всеми зонами
// этого цвета. Цвет задаётся в самом материале, а не в
// MaterialPropertyBlock, чтобы зоны оставались в SRP Batcher.
// Мигание и пульсация идут через альфу в PropertyBlock —
// это 2 объекта на зону, а не 18 кубиков бордюра.
public static class DangerZoneMaterials
{
    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    // Ключ — квантованный цвет. Ближайшие оттенки делят один
    // материал: глазу разница не видна, а материалов меньше.
    private static readonly Dictionary<int, Material> cache =
        new Dictionary<int, Material>(8);

    public static Material Get(Color color)
    {
        int key = KeyOf(color);

        if (cache.TryGetValue(key, out Material cached) &&
            cached != null)
        {
            return cached;
        }

        Shader shader =
            Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = "DangerZone_" + key
        };

        material.SetOverrideTag("RenderType", "Transparent");

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, color);

        if (material.HasProperty(ColorId))
            material.SetColor(ColorId, color);

        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor(
                "_EmissionColor",
                new Color(color.r * 2f, color.g * 2f, color.b * 2f, 1f)
            );

            material.EnableKeyword("_EMISSION");
        }

        material.enableInstancing = true;

        cache[key] = material;

        return material;
    }

    // Цвет, который реально поддерживает шейдер зоны. Используется
    // для PropertyBlock: писать в свойство, которого в шейдере нет,
    // бессмысленно и только раздувает батч.
    public static void ApplyAlpha(
        Renderer renderer,
        MaterialPropertyBlock block,
        Color baseColor,
        float alpha)
    {
        if (renderer == null)
            return;

        if (block == null)
            return;

        Color color = baseColor;
        color.a = alpha;

        Material material = renderer.sharedMaterial;

        block.Clear();

        if (material != null && material.HasProperty(BaseColorId))
            block.SetColor(BaseColorId, color);

        if (material != null && material.HasProperty(ColorId))
            block.SetColor(ColorId, color);

        renderer.SetPropertyBlock(block);
    }

    private static int KeyOf(Color color)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(color.r * 64f), 0, 64);
        int g = Mathf.Clamp(Mathf.RoundToInt(color.g * 64f), 0, 64);
        int b = Mathf.Clamp(Mathf.RoundToInt(color.b * 64f), 0, 64);

        return (r * 64 + g) * 64 + b;
    }
}
