using UnityEngine;
using UnityEngine.Rendering;
public class BombExplosionEffect : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Color explosionColor =
        new Color(1f, 0.55f, 0.1f, 0.8f);

    [SerializeField] private float ringDuration = 0.4f;
    [SerializeField] private float flashDuration = 0.22f;
    [SerializeField] private float ringHeight = 0.04f;

    private static Material sharedMaterial;
    private static MaterialPropertyBlock propertyBlock;
    private static bool materialHasBaseColor;
    private static bool materialHasColor;
    private static bool materialHasEmission;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Renderer ringRenderer;
    private Renderer flashRenderer;
    private Transform ringTransform;
    private Transform flashTransform;
    private float targetRadius = 5f;
    private float elapsed;
    private float totalDuration;
    private bool initialized;

    private void Start()
    {
        BuildVisual();
        elapsed = 0f;
        totalDuration = Mathf.Max(flashDuration, ringDuration);
        initialized = true;
        ApplyAlpha(1f);
    }

    private void Update()
    {
        if (!initialized)
            return;

        elapsed += Time.deltaTime;
        UpdateRing(elapsed);
        UpdateFlash(elapsed);

        if (elapsed >= totalDuration)
            Destroy(gameObject);
    }

    public void Initialize(float radius, Color color)
    {
        targetRadius = Mathf.Max(radius, 0.1f);
        explosionColor = color;
    }

    private void BuildVisual()
    {
        ringTransform =
            CreatePrimitive(
                PrimitiveType.Cylinder,
                "ExplosionRing"
            ).transform;

        ringTransform.localPosition =
            new Vector3(0f, ringHeight, 0f);

        ringRenderer = ringTransform.GetComponent<MeshRenderer>();
        if (ringRenderer != null)
            ringRenderer.sharedMaterial = GetSharedMaterial();

        flashTransform =
            CreatePrimitive(
                PrimitiveType.Sphere,
                "ExplosionFlash"
            ).transform;

        flashTransform.localPosition =
            new Vector3(0f, 0.4f, 0f);

        flashRenderer = flashTransform.GetComponent<MeshRenderer>();
        if (flashRenderer != null)
            flashRenderer.sharedMaterial = GetSharedMaterial();
    }

    private GameObject CreatePrimitive(
        PrimitiveType type,
        string name)
    {
        GameObject go =
            GameObject.CreatePrimitive(type);

        go.name = name;

        go.transform.SetParent(
            transform,
            false
        );

        Collider col = go.GetComponent<Collider>();

        if (col != null)
            Destroy(col);

        MeshRenderer renderer =
            go.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            renderer.receiveShadows = false;
        }

        return go;
    }

    private static Material GetSharedMaterial()
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        sharedMaterial = new Material(shader)
        {
            name = "ExplosionSharedMat"
        };

        sharedMaterial.SetOverrideTag("RenderType", "Transparent");
        if (sharedMaterial.HasProperty("_Surface"))
            sharedMaterial.SetFloat("_Surface", 1f);
        if (sharedMaterial.HasProperty("_Blend"))
            sharedMaterial.SetFloat("_Blend", 0f);
        if (sharedMaterial.HasProperty("_SrcBlend"))
            sharedMaterial.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (sharedMaterial.HasProperty("_DstBlend"))
            sharedMaterial.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (sharedMaterial.HasProperty("_ZWrite"))
            sharedMaterial.SetFloat("_ZWrite", 0f);

        sharedMaterial.renderQueue = (int)RenderQueue.Transparent;
        materialHasBaseColor = sharedMaterial.HasProperty("_BaseColor");
        materialHasColor = sharedMaterial.HasProperty("_Color");
        materialHasEmission = sharedMaterial.HasProperty("_EmissionColor");

        if (materialHasEmission)
            sharedMaterial.EnableKeyword("_EMISSION");

        propertyBlock = new MaterialPropertyBlock();
        return sharedMaterial;
    }

    private void UpdateRing(float t)
    {
        if (ringTransform == null)
        {
            return;
        }

        float progress =
            Mathf.Clamp01(t / ringDuration);

        float eased =
            1f - Mathf.Pow(1f - progress, 3f);

        // Cylinder primitive: radius 0.5, height 2.
        // scale 2x => radius == target.
        float diameter =
            Mathf.Lerp(0.2f, targetRadius * 2f, eased);

        ringTransform.localScale =
            new Vector3(diameter, 1f, diameter);

        SetRendererAlpha(ringRenderer, 1f - progress);
    }

    private void UpdateFlash(float t)
    {
        if (flashTransform == null)
        {
            return;
        }

        float progress =
            Mathf.Clamp01(t / flashDuration);

        float eased =
            1f - Mathf.Pow(1f - progress, 3f);

        // Sphere primitive: radius 0.5 -> scale 2x = radius 1.
        float diameter =
            Mathf.Lerp(
                0.2f,
                targetRadius * 1.6f,
                eased
            );

        flashTransform.localScale =
            new Vector3(diameter, diameter, diameter);

        SetRendererAlpha(flashRenderer, 1f - progress);
    }

    private void ApplyAlpha(float alpha)
    {
        SetRendererAlpha(ringRenderer, alpha);
        SetRendererAlpha(flashRenderer, alpha);
    }

    private void SetRendererAlpha(Renderer renderer, float alpha)
    {
        if (renderer == null)
            return;

        Material material = GetSharedMaterial();
        if (material == null)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        propertyBlock.Clear();

        Color color = new Color(
            explosionColor.r,
            explosionColor.g,
            explosionColor.b,
            Mathf.Clamp01(alpha * explosionColor.a)
        );

        if (materialHasBaseColor)
            propertyBlock.SetColor(BaseColorId, color);
        if (materialHasColor)
            propertyBlock.SetColor(ColorId, color);
        if (materialHasEmission)
        {
            Color emission = new Color(
                explosionColor.r * 2f,
                explosionColor.g * 2f,
                explosionColor.b * 2f,
                1f
            );
            propertyBlock.SetColor(EmissionColorId, emission);
        }

        renderer.SetPropertyBlock(propertyBlock);
    }

}
