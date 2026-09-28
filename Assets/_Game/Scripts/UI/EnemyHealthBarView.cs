using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Одна полоса HP: корень со Slider, подложка, зона заливки и сама
/// заливка. Полосы создаёт EnemyHealthBarSystem, они живут под её
/// канвасом и переиспользуются между мобами из пула.
///
/// Компонент - MonoBehaviour только ради спасения ссылок при
/// перезагрузке домена в редакторе: после релоада Unity
/// восстановит сериализованные поля, и полоса останется живой.
/// Ровно так же устроен VfxEffect.
///
/// Иерархия:
///   EnemyHealthBar        <- Slider здесь
///   ├── Background        <- Image, тёмная подложка
///   └── FillArea
///       └── Fill          <- Image, это slider.fillRect
/// </summary>
public class EnemyHealthBarView : MonoBehaviour
{
    [SerializeField] private RectTransform rect;
    [SerializeField] private RectTransform fillArea;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private Slider slider;

    // Служебное состояние. Работает с ним только система: привязанный
    // моб и последнее реально отрисованное значение. Кэш нужен, чтобы
    // не дёргать RectTransform и Slider на кадре, когда ничего не
    // изменилось (моб стоит, здоровье не падает).
    internal Enemy Owner;
    internal float ShownValue = -1f;
    internal Vector2 ShownPosition;
    internal bool Claimed;
    internal bool HasPosition;

    /// <summary>
    /// Создаёт полосу под канвасом системы. Один раз на моб, дальше
    /// она живёт из пула.
    /// </summary>
    public static EnemyHealthBarView Create(RectTransform parent)
    {
        GameObject rootObject =
            new GameObject("EnemyHealthBar", typeof(RectTransform));

        rootObject.layer = parent.gameObject.layer;

        RectTransform rootRect = (RectTransform)rootObject.transform;
        rootRect.SetParent(parent, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);

        Image background = CreateImage("Background", rootRect);

        GameObject fillAreaObject =
            new GameObject("FillArea", typeof(RectTransform));

        fillAreaObject.layer = rootObject.layer;

        RectTransform fillAreaRect =
            (RectTransform)fillAreaObject.transform;

        fillAreaRect.SetParent(rootRect, false);
        Stretch(fillAreaRect);

        Image fill = CreateImage("Fill", fillAreaRect);

        // Полоса - это индикатор, а не элемент управления: ни ввода,
        // ни рейкаста, ни анимаций перехода. Transition.None и
        // interactable = false убирают из неё всю эту работу, а
        // targetGraphic не задаётся вовсе, чтобы Slider не искал
        // Graphic для подсветки.
        Slider sliderComponent = rootObject.AddComponent<Slider>();
        sliderComponent.transition = Selectable.Transition.None;
        sliderComponent.interactable = false;
        sliderComponent.navigation = new Navigation
        {
            mode = Navigation.Mode.None
        };
        sliderComponent.minValue = 0f;
        sliderComponent.maxValue = 1f;
        sliderComponent.wholeNumbers = false;
        sliderComponent.direction = Slider.Direction.LeftToRight;
        sliderComponent.handleRect = null;
        sliderComponent.targetGraphic = null;
        sliderComponent.fillRect = fill.rectTransform;

        EnemyHealthBarView view =
            rootObject.AddComponent<EnemyHealthBarView>();

        view.rect = rootRect;
        view.fillArea = fillAreaRect;
        view.backgroundImage = background;
        view.fillImage = fill;
        view.slider = sliderComponent;

        // Полоса рождается скрытой и уходит в пул. Пока она лежит
        // в пуле, её не видно и она не участвует в перестройке
        // канваса.
        rootObject.SetActive(false);

        return view;
    }

    private static Image CreateImage(
        string name,
        RectTransform parent)
    {
        GameObject imageObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));

        RectTransform imageRect = (RectTransform)imageObject.transform;

        imageRect.SetParent(parent, false);
        Stretch(imageRect);

        imageObject.layer = parent.gameObject.layer;

        Image image = imageObject.GetComponent<Image>();

        // Полоса ничего не принимает: без GraphicRaycaster это
        // ничего не даёт, но оставлять луч в графике включённым
        // нельзя - он попадёт в GraphicRegistry и в любые
        // рукастные скрипты выбора объекта.
        image.raycastTarget = false;

        return image;
    }

    private static void Stretch(RectTransform target)
    {
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.pivot = new Vector2(0.5f, 0.5f);
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Переносит вид полосы с префаба на конкретную полосу из пула.
    /// Зовется только в момент привязки полосы к мобу, а не каждый
    /// кадр: иначе правки, сделанные руками в инспекторе прямо на
    /// полосе, затирались бы собственными же значениями.
    /// </summary>
    public void ApplySettings(EnemyHealthBarSettings settings)
    {
        if (settings == null)
            return;

        float padding = settings.FillPadding;

        rect.sizeDelta = settings.BarSize;

        // Размер живёт на корне, а подложка и зона заливки
        // растянуты по нему, поэтому отступ заливки задаётся
        // только здесь.
        fillArea.offsetMin = new Vector2(padding, padding);
        fillArea.offsetMax = new Vector2(-padding, -padding);

        backgroundImage.color = settings.BackgroundColor;
        fillImage.color = settings.FillColor;

        ShownValue = slider.value;
    }

    /// <summary>
    /// Перемещает полосу в координатах канваса. Пишет в
    /// RectTransform только при реальном сдвиге: пока моб стоит
    /// (пауза, застрял в углу) канвас не перестраивается зря.
    /// </summary>
    public void SetPosition(Vector2 position)
    {
        if (HasPosition &&
            (position - ShownPosition).sqrMagnitude < 0.0001f)
        {
            return;
        }

        HasPosition = true;
        ShownPosition = position;

        rect.anchoredPosition = position;
    }

    /// <summary>
    /// Заливает шкалу. Slider внутри сам сравнивает новое значение со
    /// старым, но лишний вызов через нативную границу на каждом
    /// раненом мобе каждый кадр всё равно не нужен.
    /// </summary>
    public void SetHealth(float value01)
    {
        if (Mathf.Approximately(value01, ShownValue))
            return;

        ShownValue = value01;

        slider.SetValueWithoutNotify(value01);
    }

    /// <summary>
    /// Прячет полосу и отвязывает её от моба.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    /// <summary>
    /// Сброс при возврате в пул.
    /// </summary>
    public void Unbind()
    {
        Owner = null;
        Claimed = false;
        HasPosition = false;
        ShownValue = -1f;

        SetVisible(false);
    }
}
