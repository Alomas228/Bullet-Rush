using System.Collections.Generic;
using UnityEngine;

public enum ColliderKind
{
    Other,
    WorldStructure,
    Player,
    Enemy
}

// Классификация коллайдера с кэшем. ResolveStructureOverlap зовёт её на
// каждом найденном коллайдере для каждого врага десятки раз в секунду,
// поэтому CompareTag/GetComponentInParent из горячего цикла убраны:
// CompareTag — нативный вызов, GetComponentInParent — обход иерархии с
// проверкой типа. Здесь всё заменено на сравнение ссылок и поиск по хешу.
//
// Враги регистрируются при спавне и снимаются при уничтожении, поэтому
// набор держит ровно живых врагов, а не всех за забег. Стены и игрок
// закэшированы в StructureQuery и здесь не дублируются.
public static class ColliderKindQuery
{
    private static readonly Dictionary<Collider, Enemy> EnemyColliders = new(128);
    private static readonly List<Collider> childColliderBuffer = new(8);

    private static Transform playerRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        EnemyColliders.Clear();
        playerRoot = null;
    }

    public static void RegisterEnemy(Transform enemyRoot)
    {
        if (enemyRoot == null)
            return;

        AddEnemyColliders(enemyRoot, true);
    }

    public static void UnregisterEnemy(Transform enemyRoot)
    {
        if (enemyRoot == null)
            return;

        AddEnemyColliders(enemyRoot, false);
    }

    // Источник истины — трансформ самого Enemy, а не selfCollider:
    // так набор совпадает с GetComponentInParent<Enemy>() даже если
    // у префаба коллайдер уедет на дочерний объект.
    private static void AddEnemyColliders(
        Transform enemyRoot,
        bool add)
    {
        Enemy enemy =
            enemyRoot.GetComponent<Enemy>();

        if (enemy == null)
            return;

        childColliderBuffer.Clear();
        enemyRoot.GetComponentsInChildren(false, childColliderBuffer);

        for (int i = 0; i < childColliderBuffer.Count; i++)
        {
            Collider child = childColliderBuffer[i];

            if (child == null)
                continue;

            if (add)
                EnemyColliders[child] = enemy;
            else
                EnemyColliders.Remove(child);
        }
    }

    public static void SetPlayerRoot(Transform root)
    {
        playerRoot = root;
    }

    /// <summary>
    /// Враг, которому принадлежит коллайдер, иначе null.
    /// Заменяет GetComponent/Find на горячем пути: это поиск по
    /// хешу вместо нативного вызова с обходом иерархии.
    /// </summary>
    public static Enemy GetEnemy(Collider collider)
    {
        if (collider == null)
            return null;

        return EnemyColliders.TryGetValue(
            collider,
            out Enemy enemy)
            ? enemy
            : null;
    }

    public static ColliderKind GetKind(Collider collider)
    {
        if (collider == null)
            return ColliderKind.Other;

        if (StructureQuery.IsWorldStructure(collider))
            return ColliderKind.WorldStructure;

        if (IsPlayerCollider(collider))
            return ColliderKind.Player;

        if (EnemyColliders.ContainsKey(collider))
            return ColliderKind.Enemy;

        return ColliderKind.Other;
    }

    private static bool IsPlayerCollider(Collider collider)
    {
        if (playerRoot == null)
            return false;

        return collider.transform.root == playerRoot;
    }
}
