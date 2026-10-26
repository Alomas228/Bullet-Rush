using UnityEngine;

public sealed class PvpAiController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float preferredDistance = 10f;
    [SerializeField] private float visionDistance = 22f;
    [SerializeField] private float strafeSpeed = 2.2f;
    [SerializeField] private float fireInterval = 0.42f;
    [SerializeField] private float projectileSpeed = 19f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private float aimLead = 0.18f;
    [SerializeField] private float projectileDangerRadius = 1.4f;

    private Transform target;
    private float fireTimer;
    private PvpCombatant combatant;
    private PlayerController targetController;
    private Collider ownCollider;
    private Transform firePoint;
    private WeaponData weaponData;
    private float strafeTimer;
    private int strafeDirection = 1;

    public void Configure(Transform player, WeaponData weapon)
    {
        target = player;
        targetController = player != null ? player.GetComponent<PlayerController>() : null;
        combatant = GetComponent<PvpCombatant>();
        ownCollider = GetComponent<Collider>();
        weaponData = weapon;

        if (weapon == null)
            return;

        fireInterval = Mathf.Max(1f / Mathf.Max(weapon.FireRate, 0.1f), 0.12f);
        projectileSpeed = weapon.ProjectileSpeed;
        damage = weapon.Damage;

        Transform weaponRoot = transform.Find("Weapon");
        if (weaponRoot != null)
        {
            if (weapon.WeaponPrefab != null)
            {
                GameObject visual = Instantiate(weapon.WeaponPrefab, weaponRoot);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
            }

            Transform[] children = weaponRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == "FirePoint")
                {
                    firePoint = children[i];
                    break;
                }
            }
        }
    }

    private void Update()
    {
        if (target == null || combatant == null || combatant.IsDead ||
            GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameState.Playing)
            return;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance <= 0.01f)
            return;

        Vector3 direction = toTarget / distance;
        if (targetController != null)
            moveSpeed = targetController.CurrentMoveSpeed;

        UpdateStrafe();

        Vector3 movement = GetCombatMovement(direction, distance);
        movement += GetProjectileEvasion();
        movement = Vector3.ClampMagnitude(movement, moveSpeed);
        Move(movement * Time.deltaTime);

        Vector3 aim = target.position + GetTargetVelocity() * aimLead;
        aim.y = transform.position.y;
        Vector3 lookDirection = aim - transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lookDirection);

        fireTimer -= Time.deltaTime;
        if (fireTimer <= 0f && distance <= visionDistance && HasLineOfSight(aim))
        {
            Fire(aim - transform.position);
            fireTimer = fireInterval;
        }
    }

    private void UpdateStrafe()
    {
        strafeTimer -= Time.deltaTime;
        if (strafeTimer > 0f)
            return;

        strafeTimer = Random.Range(0.8f, 1.8f);
        strafeDirection *= -1;
    }

    private Vector3 GetCombatMovement(Vector3 direction, float distance)
    {
        Vector3 strafe = Vector3.Cross(Vector3.up, direction) * strafeSpeed * strafeDirection;
        Vector3 cover = GetCoverMovement();
        if (cover.sqrMagnitude > 0.01f)
            return cover + strafe * 0.35f;

        if (distance > preferredDistance + 2f)
            return direction * moveSpeed + strafe;

        if (distance < preferredDistance - 2f)
            return -direction * moveSpeed + strafe;

        return strafe;
    }

    private Vector3 GetProjectileEvasion()
    {
        Vector3 evasion = Vector3.zero;
        for (int i = 0; i < Bullet.ActiveCount; i++)
        {
            Bullet projectile = Bullet.GetActive(i);
            if (projectile == null)
                continue;

            AccumulateEvasion(
                projectile.transform.position,
                projectile.TravelDirection,
                projectile.TravelSpeed,
                ref evasion
            );
        }

        for (int i = 0; i < PvpProjectile.ActiveCount; i++)
        {
            PvpProjectile projectile = PvpProjectile.GetActive(i);
            if (projectile == null || !projectile.IsPlayerOwned)
                continue;

            AccumulateEvasion(
                projectile.transform.position,
                projectile.Direction,
                projectile.Speed,
                ref evasion
            );
        }

        return evasion;
    }

    private void AccumulateEvasion(
        Vector3 projectilePosition,
        Vector3 projectileDirection,
        float projectileSpeed,
        ref Vector3 evasion)
    {
        Vector3 toAi = transform.position - projectilePosition;
        toAi.y = 0f;
        projectileDirection.y = 0f;
        if (projectileDirection.sqrMagnitude <= 0.001f || Vector3.Dot(projectileDirection, toAi) <= 0f)
            return;

        Vector3 shotDirection = projectileDirection.normalized;
        float alongDistance = Vector3.Dot(toAi, shotDirection);
        if (alongDistance / Mathf.Max(projectileSpeed, 0.1f) > 0.9f)
            return;

        Vector3 closest = projectilePosition + shotDirection * alongDistance;
        Vector3 miss = transform.position - closest;
        miss.y = 0f;
        float distance = miss.magnitude;
        if (distance > projectileDangerRadius)
            return;

        Vector3 side = miss.sqrMagnitude > 0.001f
            ? miss.normalized
            : Vector3.Cross(Vector3.up, shotDirection).normalized * strafeDirection;
        float urgency = 1f - Mathf.Clamp01(distance / projectileDangerRadius);
        evasion += side * (urgency * moveSpeed);
    }

    private Vector3 GetCoverMovement()
    {
        Vector3 origin = transform.position + Vector3.up * 0.35f;
        Vector3 toTarget = target.position + Vector3.up * 0.35f - origin;
        if (!Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, toTarget.magnitude))
            return Vector3.zero;

        if (!StructureQuery.IsWorldStructure(hit.collider))
            return Vector3.zero;

        Vector3 awayFromTarget = -toTarget.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, toTarget.normalized).normalized;
        Bounds bounds = hit.collider.bounds;
        float sideExtent = Mathf.Abs(side.x) * bounds.extents.x +
            Mathf.Abs(side.z) * bounds.extents.z;
        float depthExtent = Mathf.Abs(awayFromTarget.x) * bounds.extents.x +
            Mathf.Abs(awayFromTarget.z) * bounds.extents.z;
        Vector3 firstPeek = bounds.center + side * (sideExtent + 1.2f) + awayFromTarget * (depthExtent + 0.4f);
        Vector3 secondPeek = bounds.center - side * (sideExtent + 1.2f) + awayFromTarget * (depthExtent + 0.4f);
        firstPeek.y = transform.position.y;
        secondPeek.y = transform.position.y;

        bool firstVisible = HasLineOfSightFrom(firstPeek);
        bool secondVisible = HasLineOfSightFrom(secondPeek);
        Vector3 destination;
        if (firstVisible && secondVisible)
            destination = strafeDirection > 0 ? firstPeek : secondPeek;
        else if (firstVisible)
            destination = firstPeek;
        else if (secondVisible)
            destination = secondPeek;
        else
            destination = strafeDirection > 0 ? firstPeek : secondPeek;

        Vector3 movement = destination - transform.position;
        movement.y = 0f;
        return Vector3.ClampMagnitude(movement, moveSpeed);
    }

    private bool HasLineOfSightFrom(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * 0.35f;
        Vector3 targetPoint = target.position + Vector3.up * 0.35f;
        Vector3 direction = targetPoint - origin;
        if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit, direction.magnitude))
            return true;

        return hit.collider.GetComponentInParent<PvpCombatant>() != null ||
            hit.collider.CompareTag("Player");
    }

    private void Move(Vector3 delta)
    {
        if (delta.sqrMagnitude <= 0.0001f)
            return;

        Vector3 next = transform.position + delta;
        next.y = transform.position.y;
        Vector3 direction = next - transform.position;
        float distance = direction.magnitude;
        if (ownCollider != null && Physics.SphereCast(transform.position + Vector3.up * 0.5f,
                0.45f, direction.normalized, out RaycastHit hit, distance) &&
            StructureQuery.IsWorldStructure(hit.collider))
        {
            Vector3 slide = Vector3.ProjectOnPlane(direction, hit.normal);
            next = transform.position + slide;
            next.y = transform.position.y;
        }

        transform.position = next;
    }

    private Vector3 GetTargetVelocity()
    {
        PlayerController controller = target.GetComponent<PlayerController>();
        if (controller != null)
            return controller.PlanarVelocity;

        Rigidbody targetBody = target.GetComponent<Rigidbody>();
        return targetBody != null ? targetBody.linearVelocity : Vector3.zero;
    }

    private bool HasLineOfSight(Vector3 aim)
    {
        Vector3 origin = transform.position + Vector3.up * 0.35f;
        Vector3 direction = aim - origin;
        if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit, direction.magnitude))
            return true;

        return hit.collider.GetComponentInParent<PvpCombatant>() != null ||
            hit.collider.CompareTag("Player");
    }

    private void Fire(Vector3 direction)
    {
        Transform origin = firePoint != null ? firePoint : transform;
        int count = weaponData != null ? Mathf.Max(weaponData.ProjectileCount, 1) : 1;
        float spread = weaponData != null ? weaponData.SpreadAngle : 0f;

        for (int i = 0; i < count; i++)
        {
            Vector3 shotDirection = direction.normalized;
            if (spread > 0f)
            {
                Vector2 offset = Random.insideUnitCircle * spread;
                shotDirection = Quaternion.Euler(offset.y, offset.x, 0f) * shotDirection;
            }

            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "PvpProjectile_AI";
            projectile.transform.position = origin.position + shotDirection * 0.7f;
            projectile.transform.localScale = Vector3.one * 0.18f;

            PvpProjectile shot = projectile.AddComponent<PvpProjectile>();
            shot.Initialize(shotDirection, projectileSpeed, damage, false);
        }
    }
}
