using UnityEngine;
using UnityEngine.Rendering;

// Плавно «растворяет» блок, когда он загораживает игрока от камеры,
// и возвращает непрозрачность обратно, когда игрок снова в поле зрения.
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

    private MeshRenderer meshRenderer;

    private Material opaqueMaterial;
    private Material ghostMaterial;
    private Color originalColor;

    private bool ghostActive;
    private bool requestBlocked;
    private float pendingTimer;
    private float currentAlpha = 1f;
    private float targetAlpha = 1f;

    private void Awake()
    {
        meshRenderer =
            GetComponent<MeshRenderer>();

        if (meshRenderer == null)
        {
            enabled = false;
            return;
        }

        opaqueMaterial =
            meshRenderer.material;

        originalColor =
            GetMaterialColor(opaqueMaterial);

        ghostMaterial =
            new Material(opaqueMaterial)
            {
                name = opaqueMaterial.name + " (Ghost)"
            };

        MakeTransparent(ghostMaterial);

        SetMaterialColor(
            ghostMaterial,
            originalColor
        );
    }

    private void OnDestroy()
    {
        if (ghostMaterial != null)
            Destroy(ghostMaterial);
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
        if (meshRenderer == null)
            return;

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
            meshRenderer.material =
                ghostMaterial;

            ghostActive = true;
        }
        else if (
            !shouldGhost &&
            ghostActive &&
            currentAlpha >= 0.999f)
        {
            meshRenderer.material =
                opaqueMaterial;

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

        Color color =
            Color.Lerp(
                originalColor,
                occludedTint,
                ghostEffectStrength * (1f - progress)
            );

        float shimmer =
            Mathf.Sin(Time.time * 8f) *
            0.04f *
            (1f - progress);

        color.r += shimmer;
        color.g += shimmer;
        color.b += shimmer * 1.5f;

        color.a = currentAlpha;

        SetMaterialColor(ghostMaterial, color);
    }

    private static Color GetMaterialColor(
        Material material)
    {
        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");

        if (material.HasProperty("_Color"))
            return material.GetColor("_Color");

        return Color.white;
    }

    private static void SetMaterialColor(
        Material material,
        Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
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