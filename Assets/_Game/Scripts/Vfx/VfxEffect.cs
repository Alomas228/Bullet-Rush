using UnityEngine;

/// <summary>
/// База всех забираемых VFX-эффектов.
///
/// У эффекта НЕТ своего Update: тик вызывается из одного общего
/// VfxUpdater. При сотне одновременных трассеров это на порядок
/// дешевле, чем сотня вызовов Update из движка Unity.
///
/// Жизненный цикл: пул достаёт объект -> BeginPlay() -> эффект
/// крутится в общем тикере -> Finish() -> эффект возвращается в пул.
/// </summary>
public abstract class VfxEffect : MonoBehaviour
{
    /// <summary>
    /// Ключ пула (по префабу/шаблону). 0 = объект живёт вне пула.
    /// </summary>
    public EntityId? PoolKey { get; internal set; }

    public bool IsPlaying { get; private set; }

    // Индекс в плотном массиве VfxUpdater (swap-back).
    internal int RegistryIndex = -1;

    protected float TimeLeft;
    protected float Duration;

    internal void BeginPlay(float duration)
    {
        Duration = Mathf.Max(0.01f, duration);
        TimeLeft = Duration;
        IsPlaying = true;

        VfxUpdater.Register(this);
    }

    internal void TickInternal(float deltaTime)
    {
        Tick(deltaTime);
    }

    /// <summary>
    /// Кадр эффекта. Вызывается только пока IsPlaying == true.
    /// </summary>
    protected abstract void Tick(float deltaTime);

    /// <summary>
    /// Эффект доиграл: снимаем с тикера и возвращаем в пул.
    /// </summary>
    protected void Finish()
    {
        if (!IsPlaying)
            return;

        TimeLeft = 0f;
        IsPlaying = false;

        VfxUpdater.Unregister(this);
        ReturnToPool();
    }

    /// <summary>
    /// Принудительно прервать эффект (например, при смене сцены).
    /// </summary>
    public void Stop()
    {
        if (!IsPlaying)
            return;

        Finish();
    }

    /// <summary>
    /// Вернуть объект в конкретный пул. Реализуется наследником,
    /// чтобы база не знала про дженерики пула.
    /// </summary>
    protected abstract void ReturnToPool();
}
