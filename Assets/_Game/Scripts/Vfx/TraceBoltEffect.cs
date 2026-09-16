using System.Collections;
using UnityEngine;

/// <summary>
/// Процедурная «молния» / линия рикошета между двумя точками.
/// Рисуется через LineRenderer с зигзагом и плавным затуханием,
/// опционально добавляет сферу-искру в конечной точке.
/// Не требует внешних ассетов и уничтожается после показа.
/// </summary>
public class TraceBoltEffect : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private float lifetime = 0.25f;
    [SerializeField] private float startWidth = 0.12f;
    [SerializeField] private float endWidth = 0.05f;
    [SerializeField] private int segments = 14;
    [SerializeField] private float sparkRadius = 0.22f;

    private LineRenderer lineRenderer;
    private Material lineMaterial;
    private Material sparkMaterial;
    private Transform sparkTransform;
    private Color boltColor;
    private float targetSparkScale = 1f;

    public void Initialize(
        Vector3 from,
        Vector3 to,
        Color color,
        bool zigZag,
        bool endSpark)
    {
        boltColor = color;

        BuildBolt(from, to, zigZag);

        // Искра в конечной точке: она заметнее тонкого сегмента,
        // особенно при коротких молниях между близкими целями.
        if (endSpark)
            BuildSpark(to);

        StartCoroutine(AnimateRoutine());
    }

    private void BuildBolt(
        Vector3 from,
        Vector3 to,
        bool zigZag)
    {
        lineRenderer =
            gameObject.AddComponent<LineRenderer>();

        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        lineRenderer.numCapVertices = 4;
        lineRenderer.positionCount = segments + 1;

        lineRenderer.SetPositions(
            GeneratePoints(from, to, zigZag)
        );

        lineMaterial =
            CreateUnlitMaterial(boltColor);

        lineRenderer.sharedMaterial =
            lineMaterial;
    }

    private Vector3[] GeneratePoints(
        Vector3 from,
        Vector3 to,
        bool zigZag)
    {
        Vector3[] points =
            new Vector3[segments + 1];

        Vector3 direction =
            to - from;

        float distance =
            direction.magnitude;

        if (distance < 0.001f)
        {
            direction = Vector3.forward;
            distance = 1f;
        }

        for (int i = 0; i < points.Length; i++)
        {
            float t =
                i / (float)(points.Length - 1);

            points[i] =
                Vector3.Lerp(from, to, t);

            if (!zigZag ||
                i == 0 ||
                i == points.Length - 1)
            {
                continue;
            }

            Vector3 axis =
                direction / distance;

            Vector3 perp =
                Vector3.Cross(axis, Vector3.up);

            if (perp.sqrMagnitude < 0.0001f)
                perp = Vector3.Cross(axis, Vector3.right);

            perp.Normalize();

            float jitter =
                distance *
                0.1f *
                Random.Range(0.6f, 1.4f);

            points[i] +=
                perp *
                Random.Range(-jitter, jitter);

            points[i] +=
                Vector3.up *
                Random.Range(
                    -jitter * 0.4f,
                    jitter * 0.4f
                );
        }

        return points;
    }

    private void BuildSpark(
        Vector3 position)
    {
        GameObject sphere =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

        sphere.name = "RicochetSpark";

        sphere.transform.SetParent(
            transform,
            false
        );

        sphere.transform.position = position;

        Collider collider =
            sphere.GetComponent<Collider>();

        if (collider != null)
            Destroy(collider);

        targetSparkScale =
            sparkRadius * 2f;

        sphere.transform.localScale =
            Vector3.one *
            sparkRadius *
            0.4f;

        MeshRenderer renderer =
            sphere.GetComponent<MeshRenderer>();

        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        renderer.receiveShadows = false;

        sparkMaterial =
            CreateUnlitMaterial(boltColor);

        renderer.sharedMaterial =
            sparkMaterial;

        sparkTransform =
            sphere.transform;
    }

    private Material CreateUnlitMaterial(
        Color color)
    {
        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Unlit"
            );

        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat =
            new Material(shader);

        // Прозрачный поверхностный режим — иначе alpha-затухание
        // игнорируется и эффект выглядит мутным/мгновенно исчезает.
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

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);

        if (mat.HasProperty("_EmissionColor"))
        {
            Color emission =
                new Color(
                    color.r * 2f,
                    color.g * 2f,
                    color.b * 2f,
                    1f
                );

            mat.SetColor("_EmissionColor", emission);
            mat.EnableKeyword("_EMISSION");
        }

        return mat;
    }

    private IEnumerator AnimateRoutine()
    {
        float t = 0f;

        while (t < lifetime)
        {
            t += Time.deltaTime;

            float progress =
                Mathf.Clamp01(t / lifetime);

            float fade =
                1f - progress;

            SetAlpha(
                lineMaterial,
                fade * boltColor.a
            );

            UpdateSpark(progress);

            yield return null;
        }

        Destroy(gameObject);
    }

    private void UpdateSpark(float progress)
    {
        if (sparkTransform == null ||
            sparkMaterial == null)
        {
            return;
        }

        float eased =
            1f - Mathf.Pow(1f - progress, 2f);

        float scale =
            Mathf.Lerp(
                sparkRadius * 0.4f,
                targetSparkScale,
                eased
            );

        sparkTransform.localScale =
            Vector3.one * scale;

        SetAlpha(
            sparkMaterial,
            (1f - progress) * boltColor.a
        );
    }

    private void SetAlpha(
        Material mat,
        float alpha)
    {
        if (mat == null)
            return;

        if (mat.HasProperty("_BaseColor"))
        {
            Color color =
                mat.GetColor("_BaseColor");

            color.a =
                Mathf.Clamp01(alpha);

            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            Color color =
                mat.GetColor("_Color");

            color.a =
                Mathf.Clamp01(alpha);

            mat.SetColor("_Color", color);
        }
    }
}