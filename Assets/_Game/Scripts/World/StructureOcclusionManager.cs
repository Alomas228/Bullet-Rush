using System.Collections.Generic;
using UnityEngine;

// Каждые LateUpdate бросает лучи из камеры по центру и углам
// хитбокса игрока. Все блоки WorldStructure, попавшие между камерой
// и игроком, помечаются как «перекрывающие» — они растворяются.
public class StructureOcclusionManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Камера, из которой определяется заслонённость. Пусто — Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Игрок, которого нужно раскрывать. Пусто — ищется по тегу Player.")]
    [SerializeField] private Transform target;

    [Header("Probing")]
    [Tooltip("Дополнительные лучи по углам хитбокса игрока (помимо центрального).")]
    [SerializeField] private bool probeCorners = true;

    [Tooltip("Интервал обновления проверки заслонения. 0.05 = 20 проверок в секунду.")]
    [SerializeField] private float updateInterval = 0.05f;

    [Tooltip("Слои, которые проверяются лучами. Назначь только слой World, чтобы не тратить лучи на игроков/врагов/пули. Пусто — все слои.")]
    [SerializeField] private LayerMask occlusionMask = -1;

    private static readonly Vector3[] CornerOffsets =
    {
        new Vector3(-1f, -1f, -1f),
        new Vector3( 1f, -1f, -1f),
        new Vector3(-1f,  1f, -1f),
        new Vector3( 1f,  1f, -1f),
        new Vector3(-1f, -1f,  1f),
        new Vector3( 1f, -1f,  1f),
        new Vector3(-1f,  1f,  1f),
        new Vector3( 1f,  1f,  1f)
    };

    private readonly RaycastHit[] probeHits = new RaycastHit[32];
    private readonly HashSet<WorldStructure> blockedSet =
        new HashSet<WorldStructure>();

    // Переиспользуется каждый кадр вместо аллокации нового HashSet.
    private readonly HashSet<WorldStructure> currentSet =
        new HashSet<WorldStructure>();

    private Collider targetCollider;
    private float updateTimer;

    private bool wasActive;

    private void LateUpdate()
    {
        EnsureReferences();

        if (!IsPlayActive())
        {
            ResetAll();

            return;
        }

        if (targetCamera == null || target == null)
        {
            ResetAll();
            return;
        }

        if (updateTimer > 0f)
        {
            updateTimer -= Time.unscaledDeltaTime;
            return;
        }

        updateTimer = Mathf.Max(updateInterval, 0.02f);
        wasActive = true;

        HashSet<WorldStructure> current =
            CollectOccludingStructures();

        foreach (WorldStructure structure in blockedSet)
        {
            if (structure == null)
                continue;

            if (!current.Contains(structure))
                GetFader(structure).SetBlocked(false);
        }

        foreach (WorldStructure structure in current)
        {
            if (structure == null)
                continue;

            if (!blockedSet.Contains(structure))
                GetFader(structure).SetBlocked(true);
        }

        blockedSet.Clear();

        foreach (WorldStructure structure in current)
        {
            if (structure != null)
                blockedSet.Add(structure);
        }
    }

    private void EnsureReferences()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (target == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                target = playerObject.transform;
        }
    }

    private bool IsPlayActive()
    {
        GameStateManager manager =
            GameStateManager.Instance;

        return
            manager != null &&
            manager.CurrentState == GameState.Playing;
    }

    private void ResetAll()
    {
        if (!wasActive)
            return;

        foreach (WorldStructure structure in blockedSet)
        {
            if (structure == null)
                continue;

            StructureOcclusionFader fader =
                structure.GetComponentInParent<StructureOcclusionFader>();

            if (fader != null)
                fader.ForceOpaque();
        }

        blockedSet.Clear();
        wasActive = false;
        updateTimer = 0f;
    }

    private HashSet<WorldStructure> CollectOccludingStructures()
    {
        HashSet<WorldStructure> result = currentSet;

        result.Clear();

        if (targetCollider == null)
            targetCollider =
                target.GetComponentInChildren<Collider>();

        if (targetCollider == null)
            return result;

        Bounds bounds = targetCollider.bounds;
        Vector3 center = bounds.center;

        Vector3 cameraPosition =
            targetCamera.transform.position;

        ProbePoint(center, cameraPosition, result);

        if (probeCorners)
        {
            Vector3 extents = bounds.extents;

            for (int i = 0; i < CornerOffsets.Length; i++)
            {
                ProbePoint(
                    center + Vector3.Scale(CornerOffsets[i], extents),
                    cameraPosition,
                    result
                );
            }
        }

        return result;
    }

    private void ProbePoint(
        Vector3 probePosition,
        Vector3 cameraPosition,
        HashSet<WorldStructure> result)
    {
        Vector3 direction =
            probePosition - cameraPosition;

        float maxDistance =
            direction.magnitude;

        if (maxDistance <= 0.01f)
            return;

        int hitCount =
            Physics.RaycastNonAlloc(
                cameraPosition,
                direction.normalized,
                probeHits,
                maxDistance,
                occlusionMask,
                QueryTriggerInteraction.Ignore
            );

        for (int i = 0; i < hitCount; i++)
        {
            if (!StructureQuery.TryGetWorldStructure(
                probeHits[i].collider,
                out WorldStructure structure))
            {
                continue;
            }

            result.Add(structure);
        }
    }

    private static StructureOcclusionFader GetFader(
        WorldStructure structure)
    {
        StructureOcclusionFader fader =
            structure.GetComponentInParent<StructureOcclusionFader>();

        if (fader == null)
        {
            fader =
                structure.gameObject
                    .AddComponent<StructureOcclusionFader>();
        }

        return fader;
    }
}