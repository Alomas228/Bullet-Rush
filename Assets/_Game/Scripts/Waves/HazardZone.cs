using System.Collections;
using UnityEngine;

// Временная зона урона: сначала пульсирующее предупреждение, потом
// расширяющийся круг, который жжёт игрока, пока он внутри. Игрок
// вынужден покинуть зону — наказание за стояние на месте.
public class HazardZone : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float warningDuration = 1.4f;
    [SerializeField] private float radius = 3f;
    [SerializeField] private float damagePerSecond = 3f;
    [SerializeField] private float duration = 4f;
    [SerializeField] private float growMultiplier = 1.8f;
    [SerializeField] private float damageTickInterval = 0.5f;

    [Header("Visual")]
    [SerializeField] private Color dangerColor =
        new Color(1f, 0.6f, 0.05f, 0.45f);

    private bool activated;
    private float timer;
    private float phaseTime;
    private float currentRadius;
    private float damageTickTimer;

    private Material fillMaterial;
    private Material borderMaterial;
    private Material flashMaterial;

    private Transform fillDisc;
    private Transform borderParent;
    private Transform flashSphere;

    private PlayerHealth playerHealth;

    public void Initialize(
        float newWarningDuration,
        float newRadius,
        float newDamagePerSecond,
        float newDuration,
        float newGrowMultiplier)
    {
        warningDuration = newWarningDuration;
        radius = newRadius;
        damagePerSecond = newDamagePerSecond;
        duration = newDuration;
        growMultiplier = newGrowMultiplier;

        timer = 0f;
        phaseTime = 0f;
        currentRadius = radius;
        damageTickTimer = 0f;
        activated = false;

        transform.localScale = Vector3.one;

        ResizeVisual(currentRadius);

        if (flashSphere != null)
        {
            flashSphere.localScale = Vector3.zero;
            flashSphere.gameObject.SetActive(false);
        }

        PlayWarningSound();
    }

    private void Awake()
    {
        HideOriginalChildren();
        BuildVisual();

        playerHealth = PlayerHealth.Instance;
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

        borderMaterial =
            CreateTransparentMaterial(
                new Color(
                    dangerColor.r,
                    dangerColor.g,
                    dangerColor.b,
                    0.9f
                )
            );

        flashMaterial =
            CreateTransparentMaterial(
                dangerColor
            );

        fillDisc =
            CreatePrimitive(
                PrimitiveType.Cylinder,
                "HazardFillDisc"
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

        borderParent =
            new GameObject("HazardBorder").transform;

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
                    "HazardBorderCube"
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

        flashSphere =
            CreatePrimitive(
                PrimitiveType.Sphere,
                "HazardFlash"
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

        mat.name = "HazardMat";

        return mat;
    }

    private void ResizeVisual(float newRadius)
    {
        if (fillDisc != null)
        {
            fillDisc.localScale =
                new Vector3(
                    newRadius * 2f,
                    0.03f,
                    newRadius * 2f
                );
        }

        if (borderParent == null)
            return;

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
        timer += Time.deltaTime;

        if (!activated)
        {
            if (timer >= warningDuration)
            {
                Activate();
                return;
            }

            float pulse =
                Mathf.Sin(timer * 8f) * 0.5f + 0.5f;

            UpdateFillPulse(pulse);

            if (borderParent != null)
                borderParent.Rotate(
                    0f,
                    90f * Time.deltaTime,
                    0f
                );

            return;
        }

        phaseTime += Time.deltaTime;

        float progress =
            Mathf.Clamp01(phaseTime / duration);

        float eased =
            progress * progress;

        currentRadius =
            Mathf.Lerp(
                radius,
                radius * Mathf.Max(growMultiplier, 1f),
                eased
            );

        ResizeVisual(currentRadius);

        TickDamage();

        if (phaseTime >= duration)
            Destroy(gameObject);
    }

    private void UpdateFillPulse(float pulse)
    {
        if (fillMaterial == null)
            return;

        float alpha =
            Mathf.Lerp(0.15f, 0.45f, pulse);

        Color c = dangerColor;
        c.a = alpha;

        SetMaterialColor(fillMaterial, c);
    }

    private void TickDamage()
    {
        damageTickTimer -= Time.deltaTime;

        if (damageTickTimer > 0f)
            return;

        damageTickTimer = damageTickInterval;

        if (playerHealth == null)
            playerHealth = PlayerHealth.Instance;

        if (playerHealth == null ||
            playerHealth.CurrentHealth <= 0f)
        {
            return;
        }

        Vector3 playerPosition =
            playerHealth.transform.position;

        Vector3 zonePosition =
            transform.position;

        float horizontalDistance =
            Vector3.Distance(
                new Vector3(
                    playerPosition.x,
                    0f,
                    playerPosition.z
                ),
                new Vector3(
                    zonePosition.x,
                    0f,
                    zonePosition.z
                )
            );

        if (horizontalDistance > currentRadius)
            return;

        playerHealth.TakeDamage(
            damagePerSecond * damageTickInterval
        );
    }

    private void Activate()
    {
        activated = true;

        PlayActivateSound();

        ResizeVisual(currentRadius);

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

    private IEnumerator FlashRoutine()
    {
        float t = 0f;

        while (t < 0.35f)
        {
            t += Time.deltaTime;

            float progress =
                Mathf.Clamp01(t / 0.35f);

            float eased =
                1f - Mathf.Pow(1f - progress, 3f);

            float diameter =
                Mathf.Lerp(
                    0.2f,
                    currentRadius * 1.6f,
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

                Color c = dangerColor;
                c.a = 1f - progress;

                SetMaterialColor(flashMaterial, c);
            }

            yield return null;
        }

        if (flashSphere != null)
            flashSphere.gameObject.SetActive(false);
    }

    private void SetMaterialColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }

    private void PlayWarningSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx =
            AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.BossAbility);
    }

    private void PlayActivateSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx =
            AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.BossAoeExplode);
    }
}