using UnityEngine;

/// <summary>
/// Гарантирует, что DailyMissionManager существует ещё до загрузки
/// первой сцены. Это нужно, чтобы прогресс читался из сохранения сразу,
/// а меню и игровой экран видели один и тот же набор дня — объект не
/// требуется расставлять по сценам руками (как и у достижений).
/// </summary>
public static class DailyMissionBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        DailyMissionManager.EnsureExists();
    }
}
