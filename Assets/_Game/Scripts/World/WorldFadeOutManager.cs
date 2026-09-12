using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldFadeOutManager : MonoBehaviour
{
    [Tooltip("Максимальная длительность сжатия одного объекта. Реальная берётся из задержки, чтобы объекты исчезали по очереди.")]
    [SerializeField] private float maxFadeDuration = 0.25f;

    private bool fading;

    public void FadeOutEverything()
    {
        if (fading)
            return;

        fading = true;

        List<GameObject> objects = CollectObjects();

        if (objects.Count == 0)
        {
            fading = false;
            return;
        }

        float stagger = EstimateCameraFlightTime() / objects.Count;

        StartCoroutine(FadeOutRoutine(objects, stagger));
    }

    private float EstimateCameraFlightTime()
    {
        CameraFollow camera =
            FindAnyObjectByType<CameraFollow>();

        if (camera == null)
            return 1.5f;

        float estimate =
            camera.EstimateMenuTransitionTime();

        if (estimate < 0.1f)
            return 1.5f;

        return estimate;
    }

    private List<GameObject> CollectObjects()
    {
        List<GameObject> result = new List<GameObject>();

        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player != null && !result.Contains(player))
            result.Add(player);

        CollectFrom(result, FindObjectsByType<Enemy>());
        CollectFrom(result, FindObjectsByType<EnemyProjectile>());
        CollectFrom(result, FindObjectsByType<Bullet>());
        CollectFrom(result, FindObjectsByType<WorldStructure>());
        CollectFrom(result, FindObjectsByType<BloodPool>());
        CollectFrom(result, FindObjectsByType<DamageNumber>());

        return result;
    }

    private static void CollectFrom(
        List<GameObject> result,
        Component[] components)
    {
        foreach (Component component in components)
        {
            if (component == null)
                continue;

            GameObject target = component.gameObject;

            if (!result.Contains(target))
                result.Add(target);
        }
    }

    private IEnumerator FadeOutRoutine(
        List<GameObject> objects,
        float stagger)
    {
        float effectiveFade =
            Mathf.Clamp(
                stagger * 0.75f,
                0.05f,
                maxFadeDuration
            );

        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i] != null)
                StartCoroutine(
                    FadeOutObject(
                        objects[i],
                        effectiveFade
                    )
                );

            yield return new WaitForSecondsRealtime(
                stagger
            );
        }

        fading = false;
    }

    private IEnumerator FadeOutObject(
        GameObject target,
        float duration)
    {
        Collider[] colliders =
            target.GetComponentsInChildren<Collider>(true);

        foreach (Collider collider in colliders)
        {
            if (collider != null)
                collider.enabled = false;
        }

        Vector3 startScale =
            target.transform.localScale;

        float timer = 0f;

        while (timer < duration)
        {
            if (target == null)
                yield break;

            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / duration
                );

            t = Mathf.SmoothStep(0f, 1f, t);

            target.transform.localScale =
                Vector3.Lerp(
                    startScale,
                    Vector3.zero,
                    t
                );

            yield return null;
        }

        if (target != null)
            target.SetActive(false);
    }
}