using UnityEngine;

/// <summary>
/// Единственный Update на все числа урона за забега.
///
/// N чисел с собственными Update стоили бы N вызовов из движка
/// (нативный -> managed) каждый кадр. Здесь тикает один
/// компонент, а числа лежат в статическом реестре
/// DamageNumberSystem - так же, как это сделано для HP-баров в
/// EnemyHealthBarUpdater и для VFX в VfxUpdater.
///
/// Пока показывать нечего, компонент выключает себя: Update в
/// пустоту из движка не вызывается вообще, а первое же число
/// включает его обратно.
/// </summary>
[DefaultExecutionOrder(220)]
public class DamageNumberUpdater : MonoBehaviour
{
    private void Awake()
    {
        DamageNumberSystem.HandleUpdaterAwake(this);
    }

    private void Update()
    {
        DamageNumberSystem.Tick();

        if (DamageNumberSystem.IsIdle)
            enabled = false;
    }

    private void OnDestroy()
    {
        DamageNumberSystem.HandleUpdaterDestroyed(this);
    }
}
