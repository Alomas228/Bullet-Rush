using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldStructureGenerator : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("Центр арены. Если не назначен — используется позиция этого объекта.")]
    [SerializeField] private Transform center;

    [Tooltip("Игрок, в котором нельзя ставить кубы. Если не назначен — ищется по тегу Player.")]
    [SerializeField] private Transform player;

    [Tooltip("Запас безопасности: куб не ставятся ближе этого радиуса к игроку.")]
    [SerializeField] private float playerSafeRadius = 1.5f;

    [Header("Placement")]
    [SerializeField] private float arenaRadius = 30f;
    [SerializeField] private float minDistanceFromCenter = 4f;
    [SerializeField] private float minSpacing = 1f;
    [SerializeField] private int maxPlacementAttempts = 30;

    [Header("Count")]
    [SerializeField] private int minStructures = 14;
    [SerializeField] private int maxStructures = 20;

    [Header("Sizes")]
    [SerializeField] private float minWidth = 1.5f;
    [SerializeField] private float maxWidth = 4f;
    [SerializeField] private float minHeight = 1f;
    [SerializeField] private float maxHeight = 6f;

    [Header("Look")]
    [Tooltip("Готовые материалы. Если пусто — кубы красятся цветами из палитры.")]
    [SerializeField] private Material[] materials;
    [Tooltip("Палитра цветов для кубов, когда материалы не заданы.")]
    [SerializeField] private Color[] palette;

    [Tooltip("Базовое зерно генерации. 0 — случайное при запуске.")]
    [SerializeField] private int baseSeed;

    [Header("Spawn Animation")]
    [Tooltip("Пауза между появлением кубов — объекты вырастают цепочкой, как исчезали при возврате в меню.")]
    [SerializeField] private float spawnStagger = 0.05f;
    [Tooltip("Длительность «вырастания» одного куба.")]
    [SerializeField] private float scaleInDuration = 0.25f;

    private readonly List<GameObject> structures =
        new List<GameObject>();

    private readonly List<Vector2> placedPositions =
        new List<Vector2>();

    private readonly List<float> placedClearances =
        new List<float>();

    private Coroutine generateCoroutine;

    private float groundY;

    public bool IsGenerating =>
        generateCoroutine != null;

    private void Awake()
    {
        if (baseSeed == 0)
            baseSeed = Random.Range(1, int.MaxValue);

        EnsureOcclusionManager();
    }

    // Добавляет менеджер растворяющихся при заслонении блоков,
    // если его ещё нет в сцене (не требует ручной настройки).
    private void EnsureOcclusionManager()
    {
        if (FindAnyObjectByType<StructureOcclusionManager>() == null)
            gameObject.AddComponent<StructureOcclusionManager>();
    }

    public void GenerateForWave(int wave)
    {
        if (generateCoroutine != null)
            StopCoroutine(generateCoroutine);

        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }

        Vector3 centerPos =
            center != null
                ? center.position
                : transform.position;

        groundY = centerPos.y;

        System.Random rng =
            new System.Random(
                baseSeed * 31 + wave * 131
            );

        int count =
            rng.Next(
                minStructures,
                maxStructures + 1
            );

        generateCoroutine = StartCoroutine(
            TransitionToWaveRoutine(
                rng,
                centerPos,
                count
            )
        );
    }

    private IEnumerator TransitionToWaveRoutine(
        System.Random rng,
        Vector3 centerPos,
        int count)
    {
        if (structures.Count > 0)
        {
            List<GameObject> oldStructures =
                new List<GameObject>(structures);

            structures.Clear();
            placedPositions.Clear();
            placedClearances.Clear();

            for (int i = 0; i < oldStructures.Count; i++)
            {
                if (oldStructures[i] != null)
                    StartCoroutine(
                        FadeOutStructure(
                            oldStructures[i]
                        )
                    );

                yield return new WaitForSecondsRealtime(
                    spawnStagger
                );
            }

            yield return new WaitForSecondsRealtime(
                scaleInDuration
            );
        }

        for (int i = 0; i < count; i++)
        {
            TrySpawnStructure(rng, centerPos);

            yield return new WaitForSecondsRealtime(
                spawnStagger
            );
        }

        // Ждём роста последнего куба, чтобы генерация
        // считалась завершённой только когда всё выросло.
        yield return new WaitForSecondsRealtime(
            scaleInDuration
        );

        generateCoroutine = null;
    }

    private IEnumerator FadeOutStructure(
        GameObject cube)
    {
        if (cube == null)
            yield break;

        Collider collider =
            cube.GetComponent<Collider>();

        if (collider != null)
            collider.enabled = false;

        Vector3 startScale =
            cube.transform.localScale;

        float timer = 0f;

        while (timer < scaleInDuration)
        {
            if (cube == null)
                yield break;

            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / scaleInDuration
                );

            t = Mathf.SmoothStep(0f, 1f, t);

            cube.transform.localScale =
                Vector3.Lerp(
                    startScale,
                    Vector3.zero,
                    t
                );

            yield return null;
        }

        if (cube != null)
            Destroy(cube);
    }

    private IEnumerator ScaleInStructure(
        GameObject cube,
        Vector3 fullScale)
    {
        cube.transform.localScale = Vector3.zero;

        float timer = 0f;

        while (timer < scaleInDuration)
        {
            if (cube == null)
                yield break;

            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / scaleInDuration
                );

            t = Mathf.SmoothStep(0f, 1f, t);

            cube.transform.localScale =
                Vector3.Lerp(
                    Vector3.zero,
                    fullScale,
                    t
                );

            yield return null;
        }

        if (cube != null)
            cube.transform.localScale = fullScale;
    }

    public void Clear()
    {
        foreach (GameObject structure in structures)
        {
            if (structure != null)
                Destroy(structure);
        }

        structures.Clear();
        placedPositions.Clear();
        placedClearances.Clear();
    }

    private void TrySpawnStructure(
        System.Random rng,
        Vector3 centerPos)
    {
        for (int attempt = 0;
             attempt < maxPlacementAttempts;
             attempt++)
        {
            float width = NextFloat(rng, minWidth, maxWidth);
            float depth = NextFloat(rng, minWidth, maxWidth);
            float height = NextFloat(rng, minHeight, maxHeight);

            float clearance =
                Mathf.Max(width, depth) * 0.5f +
                minSpacing;

            float angle =
                (float)(rng.NextDouble() * Mathf.PI * 2.0);

            float distance =
                NextFloat(
                    rng,
                    minDistanceFromCenter,
                    arenaRadius
                );

            Vector2 offset =
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                ) *
                distance;

            Vector2 hPos =
                new Vector2(
                    centerPos.x,
                    centerPos.z
                ) +
                offset;

            if (IsTooCloseToPlayer(hPos, clearance) ||
                !IsPositionFree(hPos, clearance))
            {
                continue;
            }

            SpawnCube(
                centerPos,
                hPos,
                width,
                depth,
                height,
                rng
            );

            placedPositions.Add(hPos);
            placedClearances.Add(clearance);

            return;
        }
    }

    private bool IsPositionFree(
        Vector2 hPos,
        float clearance)
    {
        for (int i = 0; i < placedPositions.Count; i++)
        {
            float distance =
                (placedPositions[i] - hPos).magnitude;

            if (distance <
                placedClearances[i] + clearance)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsTooCloseToPlayer(
        Vector2 hPos,
        float clearance)
    {
        if (player == null)
            return false;

        Vector2 playerH =
            new Vector2(
                player.position.x,
                player.position.z
            );

        float distance =
            (playerH - hPos).magnitude;

        return distance < clearance + playerSafeRadius;
    }

    private void SpawnCube(
        Vector3 centerPos,
        Vector2 hPos,
        float width,
        float depth,
        float height,
        System.Random rng)
    {
        GameObject cube =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        cube.name = $"Structure_{structures.Count}";

        cube.AddComponent<WorldStructure>();

        cube.transform.SetParent(
            transform,
            false
        );

        cube.transform.position =
            new Vector3(
                hPos.x,
                groundY + height * 0.5f,
                hPos.y
            );

        cube.transform.localScale =
            new Vector3(
                width,
                height,
                depth
            );

        float rotation =
            (float)(rng.NextDouble() * 360.0);

        cube.transform.localRotation =
            Quaternion.Euler(
                0f,
                rotation,
                0f
            );

        MeshRenderer renderer =
            cube.GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            if (materials != null &&
                materials.Length > 0)
            {
                renderer.material =
                    materials[
                        rng.Next(0, materials.Length)
                    ];
            }
            else if (palette != null &&
                     palette.Length > 0)
            {
                renderer.material.color =
                    palette[
                        rng.Next(0, palette.Length)
                    ];
            }
        }

        structures.Add(cube);

        StartCoroutine(
            ScaleInStructure(
                cube,
                cube.transform.localScale
            )
        );
    }

    private static float NextFloat(
        System.Random rng,
        float min,
        float max)
    {
        return
            min +
            (max - min) *
            (float)rng.NextDouble();
    }

    private void OnDestroy()
    {
        Clear();
    }
}