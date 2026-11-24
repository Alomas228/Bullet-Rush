using System.Collections.Generic;
using UnityEngine;

public partial class PlayerController
{
    [Header("Mobile Auto Aim")]
    [Tooltip("Автонаводка на ближайшего живого врага, пока правый стик не задаёт направление вручную.")]
    [SerializeField] private bool autoAimEnabled = true;

    [Tooltip("Максимальная дистанция поиска цели в метрах.")]
    [SerializeField] private float autoAimRange = 18f;

    [Tooltip("Сколько секунд после ручной наводки автонаводка не перебивает выбранное направление.")]
    [SerializeField] private float autoAimManualGrace = .55f;

    [Tooltip("Как часто искать цель и сравнивать её с текущей. Между поисками цель только проверяется.")]
    [SerializeField] private float autoAimSearchInterval = .12f;

    [Tooltip("Скорость доворота на цель в градусах в секунду. Ручная наводка по-прежнему мгновенная.")]
    [SerializeField] private float autoAimTurnSpeed = 1080f;

    [Tooltip("Насколько новая цель должна быть ближе текущей, чтобы автонаводка переключилась. 0.18 = на 18%.")]
    [Range(0f, .5f)]
    [SerializeField] private float autoAimSwitchAdvantage = .18f;

    [Tooltip("Высота точки прицеливания над землёй. По ней же проверяется линия огня.")]
    [SerializeField] private float autoAimSampleHeight = .85f;

    private static readonly List<Enemy> autoAimCandidates = new List<Enemy>(128);
    private static int autoAimWorldMask = -1;

    private Enemy autoAimTarget;
    private float autoAimSearchTimer;
    private float autoAimManualUntil;
    private int autoAimFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetAutoAimStatics()
    {
        autoAimCandidates.Clear();
        autoAimWorldMask = -1;
    }

    /// <summary>
    /// Наведение на телефоне. Пока игрок ведёт правый стик, направление
    /// полностью ручное. Стик отпущен — через короткую паузу включается
    /// автонаводка на ближайшего живого врага с чистой линией огня.
    /// </summary>
    internal void ApplyMobileAim()
    {
        // PlayerController.Update и Weapon.Update могут оба позвать метод
        // за кадр. Второй вызов пропускаем, чтобы поворот не считался дважды.
        if (autoAimFrame == Time.frameCount)
            return;

        autoAimFrame = Time.frameCount;

        Vector3 manualDirection =
            MobileControls.WorldDirection(
                MobileControls.AimInput,
                playerCamera
            );

        manualDirection.y = 0f;

        if (manualDirection.sqrMagnitude > .0004f)
        {
            autoAimManualUntil = Time.time + autoAimManualGrace;
            autoAimTarget = null;
            autoAimSearchTimer = 0f;

            transform.rotation =
                Quaternion.LookRotation(manualDirection.normalized);

            return;
        }

        if (!autoAimEnabled ||
            Time.time < autoAimManualUntil)
        {
            return;
        }

        if (!TryGetAutoAimDirection(out Vector3 direction))
            return;

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(direction),
                autoAimTurnSpeed * Time.deltaTime
            );
    }

    private bool TryGetAutoAimDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        bool currentValid =
            autoAimTarget != null &&
            IsAutoAimTargetValid(autoAimTarget);

        if (!currentValid)
        {
            autoAimTarget = null;

            // Потерянную цель заменяем сразу, не дожидаясь
            // очередного тика поиска.
            autoAimSearchTimer = 0f;
        }

        autoAimSearchTimer -= Time.deltaTime;

        if (autoAimSearchTimer <= 0f)
        {
            autoAimSearchTimer =
                Mathf.Max(autoAimSearchInterval, .03f);

            autoAimTarget =
                FindBestAutoAimTarget(
                    currentValid
                        ? autoAimTarget
                        : null
                );
        }

        if (autoAimTarget == null)
            return false;

        Vector3 toTarget =
            GetAutoAimPoint(autoAimTarget) -
            GetAutoAimOrigin();

        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= .0004f)
            return false;

        direction = toTarget.normalized;
        return true;
    }

    private Enemy FindBestAutoAimTarget(Enemy currentTarget)
    {
        Enemy.CopyAliveTo(autoAimCandidates);

        Enemy best = null;
        float bestScore = float.PositiveInfinity;
        float maxDistanceSqr = autoAimRange * autoAimRange;

        // Сравниваем квадраты дистанций: корень для выбора не нужен.
        // Текущая цель получает скидку, чтобы двое врагов рядом не
        // перетягивали наводку друг у друга каждый кадр.
        float stickiness =
            1f - Mathf.Clamp(autoAimSwitchAdvantage, 0f, .5f);

        Vector3 origin = GetAutoAimOrigin();

        for (int i = 0; i < autoAimCandidates.Count; i++)
        {
            Enemy enemy = autoAimCandidates[i];

            if (enemy == null ||
                enemy.IsDead ||
                !enemy.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 point = GetAutoAimPoint(enemy);

            Vector3 flat =
                point -
                origin;

            flat.y = 0f;

            float distanceSqr = flat.sqrMagnitude;

            if (distanceSqr > maxDistanceSqr)
                continue;

            float score = distanceSqr;

            if (enemy == currentTarget)
                score *= stickiness * stickiness;

            if (score >= bestScore)
                continue;

            if (!HasAutoAimLineOfSight(origin, point))
                continue;

            bestScore = score;
            best = enemy;
        }

        return best;
    }

    private bool IsAutoAimTargetValid(Enemy enemy)
    {
        if (enemy == null ||
            enemy.IsDead ||
            !enemy.isActiveAndEnabled)
        {
            return false;
        }

        Vector3 origin = GetAutoAimOrigin();
        Vector3 point = GetAutoAimPoint(enemy);

        Vector3 flat =
            point -
            origin;

        flat.y = 0f;

        if (flat.sqrMagnitude > autoAimRange * autoAimRange)
            return false;

        return HasAutoAimLineOfSight(origin, point);
    }

    private static bool HasAutoAimLineOfSight(
        Vector3 origin,
        Vector3 target)
    {
        if (autoAimWorldMask < 0)
        {
            int worldLayer =
                LayerMask.NameToLayer("World");

            autoAimWorldMask =
                worldLayer >= 0
                    ? 1 << worldLayer
                    : Physics.DefaultRaycastLayers;
        }

        return !Physics.Linecast(
            origin,
            target,
            autoAimWorldMask,
            QueryTriggerInteraction.Ignore
        );
    }

    private Vector3 GetAutoAimOrigin()
    {
        return
            transform.position +
            Vector3.up * autoAimSampleHeight;
    }

    private Vector3 GetAutoAimPoint(Enemy enemy)
    {
        return
            enemy.transform.position +
            Vector3.up * autoAimSampleHeight;
    }
}
