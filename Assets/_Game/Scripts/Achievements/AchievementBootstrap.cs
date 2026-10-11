using UnityEngine;

/// <summary>
/// Гарантирует, что AchievementManager существует ещё до загрузки
/// первой сцены. Благодаря этому прогресс читается из сохранения сразу,
/// а меню и игровой экран видят одно и то же состояние — не нужно
/// расставлять объект в каждой сцене руками.
/// </summary>
public static class AchievementBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        AchievementManager.EnsureExists();
    }
}
