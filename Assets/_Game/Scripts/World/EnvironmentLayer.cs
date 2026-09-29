using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Один живой слой карты: визуальная подложка + декор + окружение.
/// Физический пол остаётся у статичной арены в сцене, поэтому во
/// время слайда слои свободно проезжают друг сквозь друга и по игроку.
/// Препятствия процедурного окружения (from EnvironmentTheme) намеренно
/// имеют коллайдеры и классифицируются как структуры — во время игры
/// они блокируют игрока, врагов и пули, как обычные структуры арены.
/// </summary>
public class EnvironmentLayer : MonoBehaviour
{
    /// <summary>
    /// Высота верхней грани подложки над уровнем арены (y = 0).
    /// Небольшой подъём убирает z-fighting с статичным полом арены,
    /// но остаётся ниже крови (BloodPool GroundY = 0.006) и структур.
    /// </summary>
    public const float GroundHeight = 0.003f;

    /// <summary>Толщина визуального «слоя» подложки.</summary>
    public const float SlabThickness = 0.05f;

    public GameMap Map { get; private set; }

    private readonly List<Collider> stampedColliders =
        new List<Collider>();

    /// <summary>
    /// Создаёт слой карты по адресу map. Позиция не задаётся —
    /// расстановкой занимается контроллер (transform.position = 0).
    /// </summary>
    public static EnvironmentLayer Create(
        GameMap map,
        string rootName,
        float groundSize)
    {
        GameObject root = new GameObject(rootName);

        EnvironmentLayer layer =
            root.AddComponent<EnvironmentLayer>();

        layer.Build(map, groundSize);

        return layer;
    }

    private void Build(GameMap map, float groundSize)
    {
        Map = map;

        BuildGround(map, groundSize);

        if (map == null)
            return;

        // Процедурное окружение карты: препятствия с коллайдерами
        // внутри арены, мелкая декорация на подложке и фон за её
        // пределами. Строится по данным темы, уезжает вместе со слоем.
        if (map.environmentTheme != null && map.buildThemeEnvironment)
            EnvironmentBuilder.Build(
                map.environmentTheme,
                transform,
                GroundHeight
            );

        if (map.groundDecorPrefab != null)
            Stamp(map.groundDecorPrefab);

        if (map.environmentPrefab != null)
            Stamp(map.environmentPrefab);

        // Ручной фиксированный дизайн карты. Объекты размещаются по
        // авторским позициям и остаются на своих местах независимо
        // от процедурной генерации.
        if (map.fixedDecor != null)
        {
            for (int i = 0; i < map.fixedDecor.Length; i++)
                StampFixed(map.fixedDecor[i]);
        }
    }

    /// <summary>
    /// Плоский «слой»-подложка во всю арену. Коллайдер примитива
    /// удаляется: пол карты не должен участвовать в физике.
    /// </summary>
    private void BuildGround(
        GameMap map,
        float groundSize)
    {
        GameObject slab =
            GameObject.CreatePrimitive(PrimitiveType.Cube);

        slab.name = "Ground";

        Collider[] colliders =
            slab.GetComponents<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
            Destroy(colliders[i]);
        }

        slab.transform.SetParent(transform, false);

        slab.transform.localScale =
            new Vector3(
                Mathf.Max(groundSize, 1f),
                SlabThickness,
                Mathf.Max(groundSize, 1f)
            );

        // Верхняя грань плиты должна быть на GroundHeight.
        slab.transform.localPosition =
            new Vector3(
                0f,
                GroundHeight - SlabThickness * 0.5f,
                0f
            );

        Renderer renderer =
            slab.GetComponent<Renderer>();

        if (renderer != null)
            renderer.sharedMaterial = GetGroundMaterial(map);
    }

    private static Material fallbackGroundMaterial;

    private static Material GetGroundMaterial(GameMap map)
    {
        if (map != null && map.groundMaterial != null)
            return map.groundMaterial;

        if (fallbackGroundMaterial != null)
            return fallbackGroundMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);

        material.name = "Fallback Ground";

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", new Color(0.65f, 0.65f, 0.65f));
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", new Color(0.65f, 0.65f, 0.65f));

        fallbackGroundMaterial = material;

        return material;
    }

    /// <summary>
    /// Инстанцирует префаб как дочерний объект слоя и отключает все
    /// коллайдеры внутри. Декор не должен блокировать игрока, пули
    /// и не должен перехватывать лучи StructureOcclusionManager.
    /// </summary>
    private void Stamp(GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, transform);

        stampedColliders.Clear();

        instance.GetComponentsInChildren(true, stampedColliders);

        for (int i = 0; i < stampedColliders.Count; i++)
        {
            if (stampedColliders[i] != null)
                stampedColliders[i].enabled = false;
        }
    }

    /// <summary>
    /// Ставит ручной объект фиксированного дизайна по сохранённой
    /// позиции/повороту/масштабу относительно центра арены.
    /// Коллайдеры отключаются, если в пункте не запрошен keepColliders.
    /// </summary>
    private void StampFixed(FixedDecorItem item)
    {
        if (item == null || item.prefab == null)
            return;

        GameObject instance = Instantiate(item.prefab, transform);

        instance.name = item.prefab.name + " (Fixed)";

        instance.transform.localPosition = item.position;
        instance.transform.localEulerAngles = item.rotation;
        instance.transform.localScale = item.scale;

        if (item.keepColliders)
            return;

        stampedColliders.Clear();

        instance.GetComponentsInChildren(true, stampedColliders);

        for (int i = 0; i < stampedColliders.Count; i++)
        {
            if (stampedColliders[i] != null)
                stampedColliders[i].enabled = false;
        }
    }
}