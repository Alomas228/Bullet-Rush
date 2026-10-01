using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Плавно «растворяет» блок, когда он загораживает игрока от камеры,
// и возвращает непрозрачность обратно, когда игрок снова в поле зрения.
//
// Раньше в Awake на каждый MeshRenderer создавались две копии материала:
// обычная и «призрачная». Обычная копия тут же назначалась обратно
// через sharedMaterial и тем самым ломала SRP Batcher у всего блока,
// хотя её цвет никто не менял. Теперь в непрозрачном режиме работает
// исходный общий материал префаба, а призрачный материал кэшируется
// по исходному материалу и делится между всеми блоками. Цвет призрака
// едет через MaterialPropertyBlock — но только пока блок реально
// заслоняет игрока, то есть на единицах объектов, а не на всей карте.
public class StructureOcclusionFader : MonoBehaviour
{
    [Header("Occlusion Look")]
    [Tooltip("Минимальная альфа блока, пока он загораживает игрока.")]
    [SerializeField] private float occludedAlpha = 0.25f;
    [Tooltip("Скорость смены прозрачности.")]
    [SerializeField] private float fadeSpeed = 6f;
    [Tooltip("Задержка реакции на смену состояния — защита от мерцания.")]
    [SerializeField] private float responseDelay = 0.12f;
    [Tooltip("Холодный оттенок призрачного блока (эффект).")]
    [SerializeField] private Color occludedTint = new Color(0.75f, 0.9f, 1f, 1f);
    [Tooltip("Сила оттенка и лёгкого мерцания, пока блок скрывает игрока.")]
    [SerializeField] private float ghostEffectStrength = 0.35f;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    // Призрачный материал на каждый исходный материал, общий на всю
    // карту. Раньше на каждый рендерер каждого блока создавалась своя
    // копия — до двух материалов на меш в середине боя.
    private static readonly Dictionary<int, GhostEntry> ghostCache =
        new Dictionary<int, GhostEntry>(32);

    private sealed class GhostEntry
    {
        public Material material;
        public bool hasBaseColor;
        public bool hasColor;
    }

    // Рендереры ищутся во всём поддереве: у префабов-заготовок
    // меши часто висят на дочерних объектах, а не на корне.
    private MeshRenderer[] meshRenderers;

    private Material[] opaqueMaterials;
    private GhostEntry[] ghostEntries;
    private MaterialPropertyBlock[] ghostBlocks;
    private Color[] originalColors;

    private bool ghostActive;
    private bool requestBlocked;
    private float pendingTimer;
    private float currentAlpha = 1f;
    private float targetAlpha = 1f;

    private void Awake()
    {
        meshRenderers =
            GetComponentsInChildren<MeshRenderer>(true);

        if (meshRenderers == null ||
            meshRenderers.Length == 0)
        {
            enabled = false;
            return;
        }

        int count = meshRenderers.Length;

        opaqueMaterials = new Material[count];
        ghostEntries = new GhostEntry[count];
        ghostBlocks = new MaterialPropertyBlock[count];
        originalColors = new Color[count];

        for (int i = 0; i < count; i++)
        {
            // Именно sharedMaterial, а не material: общий материал
            // префаба не меняется, и копия не нужна.
            Material opaque = meshRenderers[i].sharedMaterial;

            if (opaque == null)
                continue;

            opaqueMaterials[i] = opaque;
            originalColors[i] = GetMaterialColor(opaque);
            ghostEntries[i] = GetGhostEntry(opaque);
        }
    }

    private void OnDestroy()
    {
        if (meshRenderers == null)
            return;

        // Снимаем PropertyBlock, чтобы кэшированный призрачный
        // материал не остался привязанным к уничтоженному рендереру.
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i] != null)
                meshRenderers[i].SetPropertyBlock(null);
        }
    }

    // Вызывает менеджер при заслонении/освобождении игрока.
    public void SetBlocked(bool blocked)
    {
        requestBlocked = blocked;
        pendingTimer = responseDelay;
    }

    public void ForceOpaque()
    {
        requestBlocked = false;
        pendingTimer = 0f;
        targetAlpha = 1f;
    }

    private void Update()
    {
        if (meshRenderers == null ||
            meshRenderers.Length == 0)
        {
            return;
        }

        if (pendingTimer > 0f)
        {
            pendingTimer -= Time.deltaTime;

            if (pendingTimer <= 0f)
            {
                targetAlpha =
                    requestBlocked
                        ? occludedAlpha
                        : 1f;
            }
        }

        currentAlpha =
            Mathf.MoveTowards(
                currentAlpha,
                targetAlpha,
                fadeSpeed * Time.deltaTime
            );

        bool shouldGhost =
            targetAlpha < 1f;

        if (shouldGhost && !ghostActive)
        {
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                if (opaqueMaterials[i] == null)
                    continue;

                meshRenderers[i].sharedMaterial =
                    ghostEntries[i].material;
            }

            ghostActive = true;
        }
        else if (
            !shouldGhost &&
            ghostActive &&
            currentAlpha >= 0.999f)
        {
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                if (opaqueMaterials[i] == null)
                    continue;

                meshRenderers[i].sharedMaterial =
                    opaqueMaterials[i];

                meshRenderers[i].SetPropertyBlock(null);
            }

            ghostActive = false;
        }

        if (ghostActive)
        {
            ApplyGhostColor();
        }
    }

    // Призрак: полупрозрачный цвет с холодным оттенком
    // и лёгким «дыханием», пока блок мешает обзору.
    private void ApplyGhostColor()
    {
        float progress =
            Mathf.Clamp01(
                (currentAlpha - occludedAlpha) /
                Mathf.Max(1f - occludedAlpha, 0.01f)
            );

        float shimmer =
            Mathf.Sin(Time.time * 8f) *
            0.04f *
            (1f - progress);

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (opaqueMaterials[i] == null)
                continue;

            Color color =
                Color.Lerp(
                    originalColors[i],
                    occludedTint,
                    ghostEffectStrength * (1f - progress)
                );

            color.r += shimmer;
            color.g += shimmer;
            color.b += shimmer * 1.5f;

            color.a = currentAlpha;

            GhostEntry entry = ghostEntries[i];

            if (ghostBlocks[i] == null)
                ghostBlocks[i] = new MaterialPropertyBlock();

            ghostBlocks[i].Clear();

            if (entry.hasBaseColor)
                ghostBlocks[i].SetColor(BaseColorId, color);

            if (entry.hasColor)
                ghostBlocks[i].SetColor(ColorId, color);

            meshRenderers[i].SetPropertyBlock(ghostBlocks[i]);
        }
    }

    private static GhostEntry GetGhostEntry(Material opaque)
    {
        int key = opaque.GetInstanceID();

        if (ghostCache.TryGetValue(key, out GhostEntry cached) &&
            cached != null &&
            cached.material != null)
        {
            return cached;
        }

        Material ghost =
            new Material(opaque)
            {
                name = opaque.name + " (Ghost)"
            };

        MakeTransparent(ghost);
        ghost.enableInstancing = true;

        GhostEntry entry = new GhostEntry
        {
            material = ghost,
            hasBaseColor = ghost.HasProperty(BaseColorId),
            hasColor = ghost.HasProperty(ColorId)
        };

        ghostCache[key] = entry;

        return entry;
    }

    private static Color GetMaterialColor(
        Material material)
    {
        if (material.HasProperty(BaseColorId))
            return material.GetColor(BaseColorId);

        if (material.HasProperty(ColorId))
            return material.GetColor(ColorId);

        return Color.white;
    }

    // Переключает копию материала в прозрачный режим.
    // Поддерживает URP Lit / Simple Lit и встроенный Standard.
    private static void MakeTransparent(Material material)
    {
        string shaderName =
            material.shader != null
                ? material.shader.name
                : string.Empty;

        material.SetFloat(
            "_ZWrite",
            0f
        );

        if (shaderName.Contains("Universal Render Pipeline"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat(
                "_SrcBlend",
                (float)BlendMode.SrcAlpha
            );
            material.SetFloat(
                "_DstBlend",
                (float)BlendMode.OneMinusSrcAlpha
            );
            material.SetFloat("_AlphaClip", 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        else if (shaderName == "Standard")
        {
            material.SetInt("_Mode", 3);
            material.SetInt(
                "_SrcBlend",
                (int)BlendMode.SrcAlpha
            );
            material.SetInt(
                "_DstBlend",
                (int)BlendMode.OneMinusSrcAlpha
            );

            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        material.renderQueue =
            (int)RenderQueue.Transparent;
    }
}
