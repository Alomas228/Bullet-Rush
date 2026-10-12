using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Затемнение экрана во время вспышек песчаной бури (Пустыня).
///
/// Один постоянный RawImage поверх всего UI: когда буря бьёт зонами,
/// экран ненадолго темнеет и прозрачность понимается — песок
/// «реально» закрывает обзор. Полное полуслепое состояние не нужнo,
/// поэтому альфа держится на уровне интенсивности бури.
///
/// RawImage без текстуры рисует сплошной четырехугольник цветом
/// <c>color</c>, отдельный спрайт не нужен.
/// </summary>
public class MapStormOverlay : MonoBehaviour
{
    private static MapStormOverlay instance;

    private RawImage overlay;
    private Color baseColor =
        new Color(0.18f, 0.14f, 0.08f, 0f);

    /// <summary>
    /// Вспышка бури: плавно затемняем, держим, плавно убираем.
    /// Повторный вызов мягко перезапускает текущий цикл с нового
    /// максимума, а не наслаивает ещё один.
    /// </summary>
    public static void Pulse(
        float intensity,
        float fadeIn,
        float hold,
        float fadeOut)
    {
        if (instance == null)
            Create();

        if (instance != null)
            instance.StartPulse(
                Mathf.Clamp01(intensity),
                Mathf.Max(fadeIn, 0.05f),
                Mathf.Max(hold, 0f),
                Mathf.Max(fadeOut, 0.05f)
            );
    }

    private static void Create()
    {
        GameObject host = new GameObject("MapStormOverlay");

        MapStormOverlay overlay = host.AddComponent<MapStormOverlay>();

        instance = overlay;

        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;

        overlay = gameObject.AddComponent<RawImage>();

        overlay.raycastTarget = false;
        overlay.color = baseColor;
    }

    private void StartPulse(
        float intensity,
        float fadeIn,
        float hold,
        float fadeOut)
    {
        StopAllCoroutines();
        StartCoroutine(PulseRoutine(
            intensity,
            fadeIn,
            hold,
            fadeOut
        ));
    }

    private IEnumerator PulseRoutine(
        float intensity,
        float fadeIn,
        float hold,
        float fadeOut)
    {
        Color target = baseColor;
        target.a = intensity;

        float elapsed = 0f;

        while (elapsed < fadeIn)
        {
            elapsed += Time.deltaTime;

            overlay.color = Color.Lerp(
                baseColor,
                target,
                elapsed / fadeIn);

            yield return null;
        }

        overlay.color = target;

        yield return new WaitForSeconds(hold);

        elapsed = 0f;

        while (elapsed < fadeOut)
        {
            elapsed += Time.deltaTime;

            overlay.color = Color.Lerp(
                target,
                baseColor,
                elapsed / fadeOut);

            yield return null;
        }

        overlay.color = baseColor;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}