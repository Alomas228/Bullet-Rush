using UnityEngine;

public class BossAttackZone : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float warningDuration = 2f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float radius = 5f;

    [Header("Visual")]
    [SerializeField] private Color dangerColor =
        new Color(1f, 0.15f, 0.1f, 0.45f);

    [SerializeField] private float flashDuration = 0.35f;

    private float timer;
    private bool activated;

    private Material fillMaterial;
    private Material flashMaterial;
    private Material borderMaterial;

    private Transform fillDisc;
    private Transform flashSphere;
    private Transform borderParent;

    public void Initialize(
        float newDamage,
        float newRadius,
        float newWarningDuration)
    {
        damage = newDamage;
        radius = newRadius;
        warningDuration = newWarningDuration;

        timer = 0f;
        activated = false;

        transform.localScale = Vector3.one;

        if (fillDisc != null)
        {
            float diameter = radius * 2f;

            fillDisc.localScale =
                new Vector3(diameter, 0.03f, diameter);
        }

        if (borderParent != null)
        {
            borderParent.localRotation = Quaternion.identity;

            SetBorderRadius(radius);
        }

        if (flashSphere != null)
        {
            flashSphere.localScale = Vector3.zero;
            flashSphere.gameObject.SetActive(false);
        }
    }

    private void Awake()
    {
        HideOriginalChildren();

        BuildVisual();
    }

    private void HideOriginalChildren()
    {
        MeshRenderer[] renderers =
            GetComponentsInChildren<MeshRenderer>(true);

        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer.transform == transform)
                continue;

            renderer.enabled = false;
        }
    }

    private void BuildVisual()
    {
        fillMaterial =
            CreateTransparentMaterial(
                dangerColor
            );

        flashMaterial =
            CreateTransparentMaterial(
                dangerColor
            );

        borderMaterial =
            CreateTransparentMaterial(
                new Color(
                    dangerColor.r,
                    dangerColor.g,
                    dangerColor.b,
                    0.9f
                )
            );

        // Заполняющий круг опасности.
        fillDisc =
            CreatePrimitive(
                PrimitiveType.Cylinder,
                "WarnFillDisc"
            ).transform;

        fillDisc.localPosition =
            new Vector3(0f, 0f, 0f);

        fillDisc.localScale =
            new Vector3(
                radius * 2f,
                0.03f,
                radius * 2f
            );

        fillDisc.GetComponent<MeshRenderer>().sharedMaterial =
            fillMaterial;

        // Вращающийся "забор" из кубиков по периметру.
        borderParent =
            new GameObject("WarnBorder").transform;

        borderParent.SetParent(transform, false);

        int count = 16;

        for (int i = 0; i < count; i++)
        {
            float angle =
                (float)i / count * Mathf.PI * 2f;

            Vector3 offset =
                new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle)
                );

            Transform cube =
                CreatePrimitive(
                    PrimitiveType.Cube,
                    "WarnBorderCube"
                ).transform;

            cube.SetParent(borderParent, false);

            cube.localPosition =
                new Vector3(
                    offset.x * (radius * 0.95f),
                    0.02f,
                    offset.z * (radius * 0.95f)
                );

            cube.localScale =
                new Vector3(0.45f, 0.05f, 0.45f);

            cube.GetComponent<MeshRenderer>().sharedMaterial =
                borderMaterial;
        }

        // Вспышка в момент удара.
        flashSphere =
            CreatePrimitive(
                PrimitiveType.Sphere,
                "WarnFlash"
            ).transform;

        flashSphere.localPosition =
            new Vector3(0f, 0.15f, 0f);

        flashSphere.localScale = Vector3.zero;

        flashSphere.GetComponent<MeshRenderer>().sharedMaterial =
            flashMaterial;

        flashSphere.gameObject.SetActive(false);
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

    private Material CreateTransparentMaterial(Color color)
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

        mat.name = "WarnMat";

        return mat;
    }

    private void SetBorderRadius(float newRadius)
    {
        for (int i = 0; i < borderParent.childCount; i++)
        {
            Transform cube = borderParent.GetChild(i);

            Vector3 local = cube.localPosition;

            float distance =
                newRadius * 0.95f;

            if (local.magnitude > 0.001f)
            {
                local =
                    local.normalized *
                    distance;
            }
            else
            {
                local = new Vector3(distance, local.y, 0f);
            }

            cube.localPosition = local;
        }
    }

    private void Update()
    {
        if (activated)
            return;

        timer += Time.deltaTime;

        float pulse =
            Mathf.Sin(timer * 8f) * 0.5f + 0.5f;

        UpdateFillPulse(pulse);

        RotateBorder();

        if (timer >= warningDuration)
        {
            Activate();
        }
    }

    private void UpdateFillPulse(float pulse)
    {
        if (fillMaterial == null)
            return;

        float alpha =
            Mathf.Lerp(0.15f, 0.5f, pulse);

        Color c = dangerColor;
        c.a = alpha;

        SetMaterialColor(fillMaterial, c);
    }

    private void RotateBorder()
    {
        if (borderParent == null)
            return;

        borderParent.Rotate(
            0f,
            90f * Time.deltaTime,
            0f
        );
    }

    private void Activate()
    {
        activated = true;

        PlayAoeExplodeSound();

        Collider[] targets =
            Physics.OverlapSphere(
                transform.position,
                radius
            );

        foreach (Collider target in targets)
        {
            if (!target.CompareTag("Player"))
                continue;

            PlayerHealth playerHealth =
                target.GetComponent<PlayerHealth>();

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }
        }

        if (flashSphere != null)
        {
            flashSphere.gameObject.SetActive(true);

            StartCoroutine(FlashRoutine());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private System.Collections.IEnumerator FlashRoutine()
    {
        float t = 0f;

        while (t < flashDuration)
        {
            t += Time.deltaTime;

            float progress =
                Mathf.Clamp01(t / flashDuration);

            float eased =
                1f - Mathf.Pow(1f - progress, 3f);

            float diameter =
                Mathf.Lerp(
                    0.2f,
                    radius * 1.7f,
                    eased
                );

            if (flashSphere != null)
            {
                flashSphere.localScale =
                    new Vector3(
                        diameter,
                        diameter * 0.5f,
                        diameter
                    );

                float alpha =
                    (1f - progress);

                Color c = dangerColor;
                c.a = alpha;

                SetMaterialColor(flashMaterial, c);
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void SetMaterialColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }

    private void PlayAoeExplodeSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.BossAoeExplode);
    }
}