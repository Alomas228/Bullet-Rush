using UnityEngine;

/// <summary>
/// Единственный Update на все HP-бары забега.
///
/// N полос с собственными Update стоили бы N вызовов из движка
/// (нативный -> managed) каждый кадр. Здесь тикает один
/// компонент, а полосы лежат в статическом реестре
/// EnemyHealthBarSystem - так же, как это сделано для VFX в
/// VfxUpdater.
///
/// Когда показывать нечего (нет мобов или ни одна полоса не
/// занята), компонент выключает себя: Update в пустоту из движка
/// не вызывается вообще, а первый же зарегистрированный моб
/// включает его обратно.
/// </summary>
[DefaultExecutionOrder(210)]
public class EnemyHealthBarUpdater : MonoBehaviour
{
    private void Awake()
    {
        EnemyHealthBarSystem.HandleUpdaterAwake(this);
    }

    private void Update()
    {
        EnemyHealthBarSystem.Tick();

        if (EnemyHealthBarSystem.IsIdle)
            enabled = false;
    }

    private void OnDestroy()
    {
        EnemyHealthBarSystem.HandleUpdaterDestroyed(this);
    }
}
