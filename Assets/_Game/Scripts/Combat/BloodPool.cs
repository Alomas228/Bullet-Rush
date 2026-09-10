using UnityEngine;

public class BloodPool : MonoBehaviour
{
    [Header("Main Pool")]
    [SerializeField] private float mainPoolSize = 1.2f;
    [SerializeField, Range(0f, 1f)] private float mainSizeRandomness = 0.2f;

    [Header("Splatters")]
    [SerializeField] private int splatterCount = 4;
    [SerializeField] private float minSplatterSize = 0.12f;
    [SerializeField] private float maxSplatterSize = 0.38f;
    [SerializeField] private float maxSplatterOffset = 1.1f;

    [Header("Lifetime")]
    [SerializeField] private float lifetime = 10f;

    private static Material cachedMainMaterial;
    private static Material cachedSplatterMaterial;

    private const float GroundY = 0.006f;

    private void Start()
    {
        SpawnMainPool();
        SpawnSplatters();

        Destroy(gameObject, lifetime);
    }

    public void Initialize(
        float size,
        int splatters,
        float maxOffset)
    {
        if (size > 0f)
            mainPoolSize = size;

        if (splatters >= 0)
            splatterCount = splatters;

        if (maxOffset > 0f)
            maxSplatterOffset = maxOffset;
    }

    public static void SpawnAt(
        Vector3 position,
        float poolSize = 1.2f,
        int splatters = 4,
        float maxOffset = 1.1f)
    {
        GameObject bloodObject =
            new GameObject("BloodPool");

        position.y = GroundY;

        bloodObject.transform.position =
            position;

        bloodObject.transform.rotation =
            Quaternion.identity;

        BloodPool pool =
            bloodObject.AddComponent<BloodPool>();

        pool.Initialize(
            poolSize,
            splatters,
            maxOffset
        );
    }

    private void SpawnMainPool()
    {
        float size =
            mainPoolSize *
            (1f - Random.value * mainSizeRandomness);

        CreateBloodQuad(
            new Vector3(
                Random.Range(-0.15f, 0.15f),
                GroundY,
                Random.Range(-0.15f, 0.15f)
            ),
            size,
            ref cachedMainMaterial,
            new Color(0.52f, 0.03f, 0.04f)
        );
    }

    private void SpawnSplatters()
    {
        for (int i = 0; i < splatterCount; i++)
        {
            float size =
                Random.Range(
                    minSplatterSize,
                    maxSplatterSize
                );

            Vector2 offset =
                Random.insideUnitCircle *
                maxSplatterOffset;

            Vector3 position =
                new Vector3(
                    offset.x,
                    GroundY,
                    offset.y
                );

            CreateBloodQuad(
                position,
                size,
                ref cachedSplatterMaterial,
                new Color(0.44f, 0.025f, 0.035f)
            );
        }
    }

    private void CreateBloodQuad(
        Vector3 position,
        float size,
        ref Material cachedMaterial,
        Color color)
    {
        GameObject quad =
            GameObject.CreatePrimitive(
                PrimitiveType.Quad
            );

        quad.name = "BloodQuad";

        quad.transform.SetParent(
            transform,
            true
        );

        Collider collider =
            quad.GetComponent<Collider>();

        if (collider != null)
            Destroy(collider);

        quad.transform.localPosition =
            position;

        float randomYaw =
            Random.Range(0f, 360f);

        quad.transform.localRotation =
            Quaternion.Euler(
                90f,
                randomYaw,
                0f
            );

        quad.transform.localScale =
            new Vector3(
                size,
                size,
                1f
            );

        Renderer renderer =
            quad.GetComponent<Renderer>();

        renderer.sharedMaterial =
            GetOrCreateMaterial(
                ref cachedMaterial,
                color
            );
    }

    private static Material GetOrCreateMaterial(
        ref Material cachedMaterial,
        Color color)
    {
        if (cachedMaterial != null)
            return cachedMaterial;

        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Unlit"
            );

        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        cachedMaterial =
            new Material(shader);

        cachedMaterial.color =
            color;

        return cachedMaterial;
    }
}