using TMPro;
using UnityEngine;

/// <summary>
/// Одно число урона. Экземпляры создаёт DamageNumberSystem из
/// префаба и переиспользует из пула, поэтому на каждое попадание
/// больше не бывает ни Instantiate, ни Destroy.
///
/// Самого канваса у числа теперь нет: раньше каждый экземпляр
/// нёс свой world-space канвас, а это отдельный draw call и
/// отдельная перестройка вершин на каждое попадание. Теперь все
/// числа лежат под одним экранным канвасом системы и рисуются
/// одним батчем, потому что шрифт у них один и тот же. Тот же
/// приём, что у EnemyHealthBarSystem.
///
/// Число живёт в мире, а не на экране. Якорь - это точка мира, в
/// которой появилось попадание, дальше из неё число только
/// поднимается вверх и больше никуда не привязано. Проекция в
/// координаты канваса делается каждый кадр, поэтому сдвиг камеры
/// двигает число по экрану ровно так же, как это делал
/// world-space канвас. Сейчас число НЕ следует за мобом: якорь
/// ставится один раз на попадание и с мобом не двигается.
///
/// Размер - тоже функция расстояния до камеры, поэтому далёкий
/// моб показывает мелкое число, как и раньше. Размер задаётся
/// масштабом текста, а не fontSize: fontSize на каждый кадр
/// перестраивал бы меш текста, а масштаб трогает только
/// RectTransform, и перестройка у него общая на весь канвас.
///
/// Компонент - MonoBehaviour только ради спасения ссылок при
/// перезагрузке домена в редакторе, ровно как у
/// EnemyHealthBarView.
///
/// Иерархия:
///   DamageNumber  <- RectTransform здесь
///   └── Text      <- TextMeshProUGUI
/// </summary>
public class DamageNumber : MonoBehaviour
{
    [SerializeField] private RectTransform rect;
    [SerializeField] private RectTransform textRect;
    [SerializeField] private TMP_Text damageText;

    [Header("Settings")]
    [SerializeField] private float lifetime = 0.7f;
    [SerializeField] private float moveSpeed = 2f;

    [Header("Critical")]
    [SerializeField] private float criticalScale = 1.4f;

    // Служебное состояние. Пишет его только система. Показанная
    // позиция и масштаб запоминаются, чтобы не трогать
    // RectTransform, когда камера застыла (CameraFollow её
    // правда замораживает, когда игрок и мышь стоят) - тогда
    // число не меняет вид и канвас не перестраивается зря.
    private float age;
    private Vector3 worldPosition;
    private Vector2 shownPosition;

    // Пока координаты не записаны ни разу. Отдельный флаг, а не
    // NaN в shownPosition: сравнение с NaN всегда даёт false, и
    // первая же запись прошла бы мимо - число навсегда осталось
    // бы в (0, 0), то есть в центре канваса.
    private bool hasShownPosition;
    private float shownScale = -1f;
    private bool isCritical;

    /// <summary>
    /// Сколько времени число уже висит на экране. Система
    /// читает его, чтобы при переполнении пула вытеснить самое
    /// старое число, а не самое первое в списке.
    /// </summary>
    public float Age => age;

    /// <summary>
    /// Якорь в мире. Система проецирует его в координаты канваса
    /// каждый кадр - именно поэтому число уезжает по экрану
    /// вместе с камерой, а не стоит на месте.
    /// </summary>
    public Vector3 WorldPosition => worldPosition;

    /// <summary>
    /// Выдаёт число из пула: ставит текст и запоминает, где оно
    /// появилось в мире.
    /// </summary>
    public void Bind(
        float damage,
        bool isCritical,
        Vector3 worldPosition)
    {
        damageText.text =
            Mathf.RoundToInt(damage).ToString();

        if (isCritical)
            damageText.text = "CRIT " + damageText.text;

        this.isCritical = isCritical;

        this.worldPosition = worldPosition;

        // Форсированная перезапись: после возврата в пул
        // показанные значения сброшены, а первое же Bind обязано
        // поставить число на место, даже если оно окажется ровно
        // там же, где висело прошлое.
        hasShownPosition = false;
        shownScale = -1f;

        age = 0f;

        if (gameObject.activeSelf == false)
            gameObject.SetActive(true);
    }

    /// <summary>
    /// Поднимает якорь вверх. Возвращает false, когда пора
    /// вернуть число в пул.
    ///
    /// Подъём идёт в мировых единицах, как и раньше, поэтому на
    /// экране число уходит вверх ровно на ту же величину, что и
    /// при world-space канвасе.
    ///
    /// Время игровое (Time.deltaTime), а не реальное: на паузе
    /// timeScale = 0 и числа замирают вместе с боем.
    /// </summary>
    public bool Advance(float deltaTime)
    {
        age += deltaTime;

        if (age >= lifetime)
            return false;

        worldPosition.y +=
            moveSpeed * deltaTime;

        return true;
    }

    /// <summary>
    /// Ставит число в координаты канваса, посчитанные системой
    /// из якоря, и задаёт размер по дистанции до камеры.
    /// </summary>
    public void Show(Vector2 position, float sizeScale)
    {
        if (isCritical)
            sizeScale *= criticalScale;

        if (ScaleChanged(shownScale, sizeScale))
        {
            shownScale = sizeScale;

            textRect.localScale =
                Vector3.one * sizeScale;
        }

        if (!hasShownPosition || PositionChanged(shownPosition, position))
        {
            hasShownPosition = true;
            shownPosition = position;

            rect.anchoredPosition = position;
        }

        if (gameObject.activeSelf == false)
            gameObject.SetActive(true);
    }

    /// <summary>
    /// Якорь ушёл за спину камеры. Проецировать его нельзя: z
    /// отрицательный, и точка отразилась бы через центр экрана,
    /// то есть число моргнуло бы в другом месте. Оно само
    /// вернётся в кадр само, поэтому достаточно погасить.
    /// </summary>
    public void Hide()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    /// <summary>
    /// Сброс при возврате в пул.
    /// </summary>
    public void Unbind()
    {
        age = 0f;
        worldPosition = Vector3.zero;
        hasShownPosition = false;
        shownScale = -1f;
        isCritical = false;

        textRect.localScale = Vector3.one;

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    // Точка перестроения канваса не должна дрожать на последнем
    // разряде координат: сравнение круглое, а не точное.
    private const float Epsilon = 0.0001f;

    private static bool PositionChanged(
        Vector2 shown,
        Vector2 next)
    {
        return (shown - next).sqrMagnitude >= Epsilon;
    }

    private static bool ScaleChanged(
        float shown,
        float next)
    {
        // Отрицательный показанный размер - это его отсутствие:
        // в Bind и Unbind он сбрасывается в -1, чтобы первая же
        // запись прошла мимо сравнения.
        if (shown < 0f || next < 0f)
            return true;

        return Mathf.Abs(shown - next) >= Epsilon;
    }
}
