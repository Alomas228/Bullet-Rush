using System.Collections;
using UnityEngine;

public class BombExplosionEffect : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Color explosionColor =
        new Color(1f, 0.55f, 0.1f, 0.8f);

    [SerializeField] private float ringDuration = 0.4f;
    [SerializeField] private float flashDuration = 0.22f;
    [SerializeField] private float ringHeight = 0.04f;

    private Material ringMaterial;
    private Material flashMaterial;
    private Transform ringTransform;
    private Transform flashTransform;
    private float targetRadius = 5f;

    private void Start()
    {
        BuildVisual();

        StartCoroutine(AnimateRoutine());
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

        ringMaterial =
            CreateTransparentMaterial();

        ringTransform.GetComponent<MeshRenderer>().sharedMaterial =
            ringMaterial;

        flashTransform =
            CreatePrimitive(
                PrimitiveType.Sphere,
                "ExplosionFlash"
            ).transform;

        flashTransform.localPosition =
            new Vector3(0f, 0.4f, 0f);

        flashMaterial =
            CreateTransparentMaterial();

        flashTransform.GetComponent<MeshRenderer>().sharedMaterial =
            flashMaterial;
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

    private Material CreateTransparentMaterial()
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);

        mat.SetOverrideTag("RenderType", "Transparent");

        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f);

        if (mat.HasProperty("_Blend"))
            mat.SetFloat("_Blend", 0f);

        if (mat.HasProperty("_SrcBlend"))
            mat.SetFloat(
                "_SrcBlend",
                (int)UnityEngine.Rendering.BlendMode.SrcAlpha
            );

        if (mat.HasProperty("_DstBlend"))
            mat.SetFloat(
                "_DstBlend",
                (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
            );

        if (mat.HasProperty("_ZWrite"))
            mat.SetFloat("_ZWrite", 0f);

        mat.renderQueue = 3000;

        // URP читает цвет из _BaseColor, но _Color есть как legacy-поле.
        // Ставим оба — иначе поверхность остаётся белой.
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", explosionColor);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", explosionColor);

        // Эмиссия: чтобы эффект светился и был виден при любом освещении.
        if (mat.HasProperty("_EmissionColor"))
        {
            Color emission =
                new Color(
                    explosionColor.r * 2f,
                    explosionColor.g * 2f,
                    explosionColor.b * 2f,
                    1f
                );

            mat.SetColor("_EmissionColor", emission);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        mat.name = "ExplosionMat";

        return mat;
    }

    private IEnumerator AnimateRoutine()
    {
        float seconds =
            Mathf.Max(flashDuration, ringDuration);

        float t = 0f;

        while (t < seconds)
        {
            t += Time.deltaTime;

            UpdateRing(t);
            UpdateFlash(t);

            yield return null;
        }

        Destroy(gameObject);
    }

    private void UpdateRing(float t)
    {
        if (ringTransform == null ||
            ringMaterial == null)
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

        SetMaterialAlpha(ringMaterial, 1f - progress);
    }

    private void UpdateFlash(float t)
    {
        if (flashTransform == null ||
            flashMaterial == null)
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

        SetMaterialAlpha(flashMaterial, 1f - progress);
    }

    private void SetMaterialAlpha(
        Material mat,
        float alpha)
    {
        if (mat.HasProperty("_BaseColor"))
        {
            Color c = mat.GetColor("_BaseColor");
            c.a = Mathf.Clamp01(alpha);
            mat.SetColor("_BaseColor", c);
        }
        else if (mat.HasProperty("_Color"))
        {
            Color c = mat.GetColor("_Color");
            c.a = Mathf.Clamp01(alpha);
            mat.SetColor("_Color", c);
        }
    }
}