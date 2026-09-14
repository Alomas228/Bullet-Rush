using System.Collections;
using UnityEngine;

public class ShieldAbility : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float baseDuration = 2.5f;
    [SerializeField] private float cooldown = 12f;

    [Header("Visual")]
    [SerializeField] private GameObject shieldVisual;

    private float cooldownTimer;
    private GameObject shieldInstance;

    public bool IsUnlocked { get; set; }

    public float Cooldown => cooldown;
    public float CooldownRemaining => cooldownTimer;
    public bool IsReady => cooldownTimer <= 0f;
    public bool IsActive { get; private set; }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    [ContextMenu("Activate Shield")]
    public bool TryActivate()
    {
        if (!IsUnlocked)
            return false;

        if (!IsReady)
            return false;

        if (IsActive)
            return false;

        cooldownTimer = cooldown;

        StartCoroutine(ShieldRoutine());

        return true;
    }

    private IEnumerator ShieldRoutine()
    {
        if (shieldInstance == null &&
            shieldVisual != null)
        {
            shieldInstance =
                Instantiate(
                    shieldVisual,
                    transform.position,
                    Quaternion.identity,
                    transform
                );
        }
        else if (shieldInstance == null)
        {
            shieldInstance =
                BuildProceduralShield();
        }

        IsActive = true;

        if (shieldInstance != null)
            shieldInstance.SetActive(true);

        yield return new WaitForSeconds(baseDuration);

        IsActive = false;

        if (shieldInstance != null)
            shieldInstance.SetActive(false);
    }

    public void UpgradeCooldown(float percent)
    {
        cooldown *= 1f - percent;
        cooldown = Mathf.Max(cooldown, 1f);
    }

    public void UpgradeDuration(float percent)
    {
        baseDuration *= 1f + percent;
    }

    private GameObject BuildProceduralShield()
    {
        GameObject sphere =
            GameObject.CreatePrimitive(PrimitiveType.Sphere);

        sphere.name = "ShieldBubble";

        sphere.transform.SetParent(
            transform,
            false
        );

        sphere.transform.localPosition =
            new Vector3(0f, 0.4f, 0f);

        sphere.transform.localScale =
            new Vector3(5f, 5f, 5f);

        Collider col = sphere.GetComponent<Collider>();

        if (col != null)
            Destroy(col);

        MeshRenderer renderer =
            sphere.GetComponent<MeshRenderer>();

        renderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        renderer.receiveShadows = false;

        Material mat =
            CreateTransparentMaterial();

        renderer.sharedMaterial = mat;

        return sphere;
    }

    private Material CreateTransparentMaterial()
    {
        Color shieldColor =
            new Color(0.3f, 0.7f, 1f, 0.35f);

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
            mat.SetColor("_BaseColor", shieldColor);

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", shieldColor);

        if (mat.HasProperty("_EmissionColor"))
        {
            Color emission =
                new Color(
                    shieldColor.r * 1.5f,
                    shieldColor.g * 1.5f,
                    shieldColor.b * 1.5f,
                    1f
                );

            mat.SetColor("_EmissionColor", emission);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        mat.name = "ShieldMat";

        return mat;
    }
}