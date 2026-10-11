using System;
using UnityEngine;

/// <summary>Данные об убитом враге, которые важно знать достижениям.</summary>
public struct EnemyKillInfo
{
    public Enemy enemy;
    public bool isBoss;
    public string bossId;
    public DamageSource source;
    public int sourceEventId;
}

/// <summary>Данные об окончании забега.</summary>
public struct RunEndInfo
{
    public bool won;
    public string weaponName;
    public int waveReached;
}

/// <summary>
/// Единая точка, куда существующие системы шлют игровые события для
/// достижений. Раньше общего канала событий не было — убийства и урон
/// разлетались прямыми вызовами, и подключиться к ним было нечем.
///
/// Здесь намеренно статические события: подписчик один
/// (AchievementManager), а сцена перезагружается на рестарт/меню, так что
/// scene-local шина теряла бы подписку. Статика сбрасывается на
/// SubsystemRegistration, поэтому не течёт между запусками с выключенным
/// domain reload.
/// </summary>
public static class AchievementEvents
{
    public static event Action<EnemyKillInfo> EnemyKilled;
    public static event Action PlayerDamaged;
    public static event Action<int, bool> WaveCleared; // (номер волны, perfect)
    public static event Action RunStarted;
    public static event Action<RunEndInfo> RunEnded;

    public static void RaiseEnemyKilled(EnemyKillInfo info)
    {
        EnemyKilled?.Invoke(info);
    }

    public static void RaisePlayerDamaged()
    {
        PlayerDamaged?.Invoke();
    }

    public static void RaiseWaveCleared(int wave, bool wasPerfect)
    {
        WaveCleared?.Invoke(wave, wasPerfect);
    }

    public static void RaiseRunStarted()
    {
        RunStarted?.Invoke();
    }

    public static void RaiseRunEnded(RunEndInfo info)
    {
        RunEnded?.Invoke(info);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        EnemyKilled = null;
        PlayerDamaged = null;
        WaveCleared = null;
        RunStarted = null;
        RunEnded = null;
    }
}
