using UnityEngine;

/// <summary>
/// Единственный источник Update для всех VFX забега.
///
/// N объектов с собственным Update стоят N вызовов из движка
/// (нативный -> managed) каждый кадр. Здесь все активные эффекты
/// лежат в плотном массиве, тикает один MonoBehaviour, а удаление
/// идёт через swap-back без сдвига массива и без аллокаций.
///
/// Когда активных эффектов нет, компонент сам выключает себя:
/// Update в пустоту не вызывается вообще.
/// </summary>
[DefaultExecutionOrder(200)]
public class VfxUpdater : MonoBehaviour
{
    // Трассеры живут до конца жизни пули, при fireRate 28 и
    // lifetime 3 с это до ~84 активных эффектов только от
    // минигана. Запас берём сразу, чтобы массив не рос
    // (аллокация) посреди боя.
    private const int InitialCapacity = 256;

    private static VfxUpdater instance;
    private static VfxEffect[] effects =
        new VfxEffect[InitialCapacity];

    private static int count;

    public static int ActiveCount
    {
        get { return count; }
    }

    public static void Register(VfxEffect effect)
    {
        if (effect == null)
            return;

        if (effect.RegistryIndex >= 0)
            return;

        EnsureExists();

        if (count == effects.Length)
            Grow(count * 2);

        effect.RegistryIndex = count;
        effects[count] = effect;
        count++;

        if (!instance.enabled)
            instance.enabled = true;
    }

    public static void Unregister(VfxEffect effect)
    {
        if (effect == null)
            return;

        int index = effect.RegistryIndex;

        if (index < 0)
            return;

        effect.RegistryIndex = -1;
        RemoveAt(index);
    }

    private static void RemoveAt(int index)
    {
        int last = count - 1;
        VfxEffect moved = effects[last];

        effects[last] = null;
        count = last;

        if (index == last)
            return;

        effects[index] = moved;

        if (moved != null)
            moved.RegistryIndex = index;
    }

    private static void Grow(int capacity)
    {
        VfxEffect[] grown = new VfxEffect[capacity];

        for (int i = 0; i < count; i++)
            grown[i] = effects[i];

        effects = grown;
    }

    private static void EnsureExists()
    {
        // == null срабатывает и на уничтоженный объект,
        // поэтому updater сам себя восстанавливает после смены сцены.
        if (instance != null)
            return;

        GameObject updaterObject =
            new GameObject("VfxUpdater");

        instance =
            updaterObject.AddComponent<VfxUpdater>();

        Object.DontDestroyOnLoad(updaterObject);
    }

    private void Awake()
    {
        instance = this;

        // Включит Register, когда появится первый эффект.
        enabled = false;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        // Обратный обход + swap-back: элемент, переехавший в i,
        // уже был обработан выше по индексу.
        for (int i = count - 1; i >= 0; i--)
        {
            VfxEffect effect = effects[i];

            if (effect == null)
            {
                // Эффект уничтожен сменой сцены.
                RemoveAt(i);
                continue;
            }

            effect.TickInternal(deltaTime);
        }

        if (count == 0)
            enabled = false;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        count = 0;
        effects = new VfxEffect[InitialCapacity];

        VfxPools.ClearAll();
        VfxSharedAssets.ResetStatics();

        DeactivateStrays();
    }

    /// <summary>
    /// Гасит эффекты, оставшиеся от прошлого домен-релоада.
    ///
    /// Перекомпиляция скриптов прямо в play mode НЕ убивает
    /// GameObject'ы пулов: они живут в DontDestroyOnLoad, поэтому
    /// Unity их пересоздаёт и восстанавливает из сериализованных
    /// полей. А статический реестр при этом обнуляется, и IsPlaying
    /// не сериализуется - тоже становится false.
    ///
    /// Итог: трассер, который в момент релоада был в фазе Follow,
    /// остаётся активным навсегда. Его больше никто не тикает и не
    /// гасит, но MeshRenderer продолжает рисовать яркий аддитивный
    /// квад. В иерархии его не видно (HideFlags.HideInHierarchy), в
    /// ActiveCount его нет, и счёт растёт от релоада к релоаду -
    /// ровно тот случай, когда редактор внезапно теряет кадры, а
    /// перезапуск всё чинит.
    ///
    /// После обнуления реестра играть больше некому (тикер пуст),
    /// поэтому любой активный эффект - сирота. Гасим их разом.
    /// Один FindObjectsByType на релоад, не на кадр.
    /// </summary>
    private static void DeactivateStrays()
    {
        // В edit mode SetActive пометил бы сцену изменённой.
        if (!Application.isPlaying)
            return;

        VfxEffect[] strays =
            Object.FindObjectsByType<VfxEffect>(
                FindObjectsInactive.Include);

        for (int i = 0; i < strays.Length; i++)
        {
            VfxEffect stray = strays[i];

            if (stray == null)
                continue;

            if (stray.gameObject.activeSelf)
                stray.gameObject.SetActive(false);
        }
    }
}
