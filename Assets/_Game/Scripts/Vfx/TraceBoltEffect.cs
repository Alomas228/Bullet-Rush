using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Procedural lightning / ricochet line effect.
/// Uses one shared material and a MaterialPropertyBlock so repeated effects
/// do not create a new Material for every hit.
/// </summary>
public class TraceBoltEffect : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private float lifetime = 0.25f;
    [SerializeField] private float startWidth = 0.12f;
    [SerializeField] private float endWidth = 0.05f;
    [SerializeField] private int segments = 14;
    [SerializeField] private float sparkRadius = 0.22f;

    private static Material sharedMaterial;
    private static MaterialPropertyBlock propertyBlock;
    private static bool materialHasBaseColor;
    private static bool materialHasColor;
    private static bool materialHasEmission;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private LineRenderer lineRenderer;
    private Renderer sparkRenderer;
    private Transform sparkTransform;
    private Color boltColor;
    private float targetSparkScale = 1f;
    private float elapsed;
    private bool initialized;

    public void Initialize(
        Vector3 from,
        Vector3 to,
        Color color,
        bool zigZag,
        bool endSpark)
    {
        boltColor = color;
        elapsed = 0f;
        initialized = true;

        BuildBolt(from, to, zigZag);

        if (endSpark)
            BuildSpark(to);

        ApplyAlpha(1f);
    }

    private void Update()
    {
        if (!initialized)
            return;

        elapsed += Time.deltaTime;

        float progress = Mathf.Clamp01(
            elapsed / Mathf.Max(lifetime, 0.01f)
        );

        float fade = 1f - progress;
        ApplyAlpha(fade);
        UpdateSpark(progress);

        if (progress >= 1f)
            Destroy(gameObject);
    }

    private void BuildBolt(Vector3 from, Vector3 to, bool zigZag)
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        lineRenderer.numCapVertices = 2;
        lineRenderer.positionCount = Mathf.Max(1, segments) + 1;
        lineRenderer.sharedMaterial = GetSharedMaterial();

        int count = lineRenderer.positionCount;
        Vector3 direction = to - from;
        float distance = direction.magnitude;

        if (distance < 0.001f)
        {
            direction = Vector3.forward;
            distance = 1f;
        }

        Vector3 axis = direction / distance;
        Vector3 perp = Vector3.Cross(axis, Vector3.up);

        if (perp.sqrMagnitude < 0.0001f)
            perp = Vector3.Cross(axis, Vector3.right);

        perp.Normalize();

        int innerCount = count - 1;
        for (int i = 0; i < count; i++)
        {
            float t = innerCount > 0 ? i / (float)innerCount : 0f;
            Vector3 point = Vector3.Lerp(from, to, t);

            if (zigZag && i != 0 && i != count - 1)
            {
                float jitter = distance * 0.1f * Random.Range(0.6f, 1.4f);
                point += perp * Random.Range(-jitter, jitter);
                point += Vector3.up * Random.Range(-jitter * 0.4f, jitter * 0.4f);
            }

            lineRenderer.SetPosition(i, point);
        }
    }

    private void BuildSpark(Vector3 position)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "RicochetSpark";
        sphere.transform.SetParent(transform, false);
        sphere.transform.position = position;

        Collider collider = sphere.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        targetSparkScale = sparkRadius * 2f;
        sphere.transform.localScale = Vector3.one * sparkRadius * 0.4f;

        sparkRenderer = sphere.GetComponent<MeshRenderer>();
        if (sparkRenderer == null)
            return;

        sparkRenderer.shadowCastingMode = ShadowCastingMode.Off;
        sparkRenderer.receiveShadows = false;
        sparkRenderer.sharedMaterial = GetSharedMaterial();
        sparkTransform = sphere.transform;
    }

    private static Material GetSharedMaterial()
    {
        if (sharedMaterial != null)
            return sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
            return null;

        sharedMaterial = new Material(shader)
        {
            name = "TraceBoltSharedMat"
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

    private void ApplyAlpha(float alpha)
    {
        if (lineRenderer == null && sparkRenderer == null)
            return;

        Material material = GetSharedMaterial();
        if (material == null)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        Color color = boltColor;
        color.a = Mathf.Clamp01(alpha * boltColor.a);

        propertyBlock.Clear();

        if (materialHasBaseColor)
            propertyBlock.SetColor(BaseColorId, color);

        if (materialHasColor)
            propertyBlock.SetColor(ColorId, color);

        if (materialHasEmission)
        {
            Color emission = new Color(
                boltColor.r * 2f,
                boltColor.g * 2f,
                boltColor.b * 2f,
                1f
            );
            propertyBlock.SetColor(EmissionColorId, emission);
        }

        if (lineRenderer != null)
            lineRenderer.SetPropertyBlock(propertyBlock);

        if (sparkRenderer != null)
            sparkRenderer.SetPropertyBlock(propertyBlock);
    }

    private void UpdateSpark(float progress)
    {
        if (sparkTransform == null)
            return;

        float eased = 1f - Mathf.Pow(1f - progress, 2f);
        float scale = Mathf.Lerp(
            sparkRadius * 0.4f,
            targetSparkScale,
            eased
        );

        sparkTransform.localScale = Vector3.one * scale;
    }
}
