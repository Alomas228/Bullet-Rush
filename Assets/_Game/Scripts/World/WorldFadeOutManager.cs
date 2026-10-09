using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldFadeOutManager : MonoBehaviour
{
    [Tooltip("Максимальная длительность сжатия одного объекта. Реальная берётся из задержки, чтобы объекты исчезали по очереди.")]
    [SerializeField] private float maxFadeDuration = 0.25f;

    private bool fading;

    public bool IsFading => fading;

    public void FadeOutEverything()
    {
        if (fading)
            return;

        fading = true;

        // Числа урона лежат в общем пуле под своим канвасом, а не
        // в сцене по одному объекту на попадание, поэтому их
        // снятие - один вызов. Раньше здесь стоял
        // FindObjectsByType<DamageNumber>, который на каждый
        // переход в меню выделял массив по всем живым числам.
        DamageNumberSystem.HideAll();

        float totalFade = EstimateCameraFlightTime() + 0.5f;

        // Кровь больше не по объекту на лужу, а один общий батчер:
        // масштабирование отдельных GameObject'ов её не задело бы.
        // Поэтому все лужи затухают разом за всё время перелёта.
        BloodPoolBatcher.FadeOut(totalFade);

        List<GameObject> objects = CollectObjects();

        float stagger =
            objects.Count > 0 ? totalFade / objects.Count : 0f;

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
            Mathf.Min(
                Mathf.Clamp(
                    stagger * 0.75f,
                    0.05f,
                    maxFadeDuration
                ),
                stagger
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

            if (stagger > 0f)
                yield return new WaitForSecondsRealtime(stagger);
        }

        // Кровь гасится своим батчером, а не объектами в списке,
        // поэтому ждём его отдельно, чтобы сцена не перезагрузилась
        // посреди затухания.
        while (BloodPoolBatcher.IsFading)
            yield return null;

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