using System.Collections.Generic;
using UnityEngine;

public sealed partial class ArcadeMenuSkin
{
    public static readonly Vector3 CampCameraOffset = new Vector3(2.5f, 3.4f, 3.8f);

    private GameObject campScenery;
    private GameMap campSceneryMap;
    private readonly List<Material> campMaterials = new List<Material>();
    private readonly Dictionary<(int,float),Material> campMaterialCache = new Dictionary<(int,float),Material>();
    private readonly List<Mesh> campMeshes = new List<Mesh>();
    private string campClothingName;
    private string campWeaponName;
    private bool campAppearanceInitialized;
    private float nextCampAppearanceCheck;

    private void RefreshCampAppearance()
    {
        if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Menu)
        {
            campAppearanceInitialized = false;
            return;
        }
        if (Time.unscaledTime < nextCampAppearanceCheck || UpgradeManager.Instance == null) return;
        nextCampAppearanceCheck = Time.unscaledTime + .25f;
        string selected = EquipmentManager.EquippedClothingName;
        string selectedWeapon = EquipmentManager.EquippedWeaponName;
        if (campAppearanceInitialized && selected == campClothingName &&
            selectedWeapon == campWeaponName) return;
        var applier = FindAnyObjectByType<PlayerEquipmentApplier>(FindObjectsInactive.Include);
        if (applier == null) return;
        applier.RefreshMenuAppearance();
        applier.GetComponentInChildren<Weapon>(true)?.RefreshMenuWeapon();
        campClothingName = selected;
        campWeaponName = selectedWeapon;
        campAppearanceInitialized = true;
    }

    private void RefreshCampScene()
    {
        if (!CampMenuLayoutEnabled) return;
        GameMap map = environment != null ? environment.CurrentMap : null;
        RefreshCampMapIcon(map);
        bool menuActive = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Menu;
        bool pvpMap = map != null && map.langKey == "pvp.arena";
        if (!menuActive || map == null)
        {
            if (!menuActive) CloseCampExtras();
            DestroyCampScene();
            return;
        }

        if (environment != null && environment.IsSessionRefreshPending) return;

        if (campScenery != null && campSceneryMap == map)
        {
            SyncCampSceneryVisibility();
            return;
        }
        DestroyCampScene();
        PlayerController player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null || (!pvpMap && map.environmentTheme == null)) return;

        // The clearing is purely a menu decoration. It has no colliders, navigation,
        // progression or gameplay components, and is removed as soon as a run starts.
        campSceneryMap = map;
        campScenery = new GameObject("Camp Menu Scenery - " + map.langKey);
        Vector3 forward = new Vector3(-CampCameraOffset.x, 0, -CampCameraOffset.z).normalized;
        campScenery.transform.SetPositionAndRotation(new Vector3(player.transform.position.x, 0, player.transform.position.z),
            Quaternion.LookRotation(forward));

        EnvironmentTheme theme = map.environmentTheme;
        BuildHubScenery(map, theme);
        SyncCampSceneryVisibility();
    }

    private void SyncCampSceneryVisibility()
    {
        if (campScenery == null) return;
        bool visible = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Menu
            && environment != null && environment.CurrentMap == campSceneryMap;
        if (campScenery.activeSelf != visible) campScenery.SetActive(visible);
    }

    private void BuildForestCamp(EnvironmentTheme theme)
    {
        CampModel(theme, "Tree", -3.9f, 1.9f, 3.1f, 24);
        CampModel(theme, "Tree", -3.6f, 4.0f, 3.5f, 105);
        CampModel(theme, "Tree", -1.3f, 4.1f, 3.2f, 195);
        CampModel(theme, "Tree", 1.1f, 4.2f, 3.4f, 52);
        CampModel(theme, "Tree", 3.2f, 2.4f, 3.3f, 95);
        CampModel(theme, "Tree", -3.0f, -2.4f, 2.5f, 120);
        CampModel(theme, "Tree", 3.0f, -1.5f, 2.8f, 15);
        CampModel(theme, "Stone", 1.7f, 1.9f, .73f, 95);
        CampModel(theme, "Stone", 2.35f, -.8f, .72f, 30);
        CampModel(theme, "Stone", -1.2f, -2.1f, .62f, 70);
        CampModel(theme, "Stump", 1.9f, .95f, .68f, 72);
        CampModel(theme, "Stump", -.9f, -1.7f, .52f, 35);
        CampModel(theme, "Bush", 2.0f, 1.6f, .47f, 45);
        CampModel(theme, "Bush", 2.4f, -1.6f, .40f, 210);
        CampModel(theme, "Flowers", -.8f, -.75f, .19f, 65);
        CampModel(theme, "Flowers", 1.3f, .36f, .20f, 135);
        CampModel(theme, "Flowers", .6f, -1.9f, .20f, 12);
        CampModel(theme, "Flowers", 2.7f, 1.3f, .18f, 35);

        BuildCampTent(new Vector3(-2.15f, .015f, 1.1f));
        BuildCampCrate(new Vector3(-1.35f, .3f, .03f), new Vector3(.60f, .60f, .58f), false);
        BuildCampCrate(new Vector3(-2.02f, .23f, -.28f), new Vector3(.63f, .46f, .50f), true);
    }

    private void DestroyCampScene()
    {
        if (campScenery != null)
        {
            campScenery.SetActive(false);
            Destroy(campScenery);
        }
        campScenery = null;
        campSceneryMap = null;
        ClearHubSceneryReferences();
        foreach (Material mat in campMaterials) if (mat != null) Destroy(mat);
        foreach (Mesh mesh in campMeshes) if (mesh != null) Destroy(mesh);
        campMaterials.Clear();
        campMaterialCache.Clear();
        campMeshes.Clear();
    }

    private void CampModel(EnvironmentTheme theme, string key, float x, float z, float height, float angle,
        float maxSpan = float.PositiveInfinity)
    {
        if (theme == null) return;
        EnvironmentObjectDef def = FindCampModel(theme.insideObstacles, key) ?? FindCampModel(theme.insideDecor, key)
            ?? FindCampModel(theme.outsideObjects, key);
        if (def == null || def.model == null) return;
        var holder = new GameObject(key);
        holder.transform.SetParent(campScenery.transform, false);
        holder.transform.localPosition = new Vector3(x, .012f, z);
        holder.transform.localRotation = Quaternion.Euler(0, angle, 0);
        var instance = Instantiate(def.model, holder.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        // The source models have different pivot conventions; normalize each on its bounds.
        if (!TryCampBounds(instance, out Bounds bounds) || bounds.size.y < .001f) return;
        float scale = height / bounds.size.y;
        float horizontalSpan = Mathf.Max(bounds.size.x, bounds.size.z);
        if (horizontalSpan > .001f) scale = Mathf.Min(scale, maxSpan / horizontalSpan);
        instance.transform.localScale *= scale;
        if (!TryCampBounds(instance, out bounds)) return;
        Vector3 groundCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        instance.transform.position += holder.transform.position - groundCenter;
    }

    private static EnvironmentObjectDef FindCampModel(EnvironmentObjectDef[] definitions, string key)
    {
        if (definitions == null) return null;
        foreach (var def in definitions)
            if (def != null && def.name == key && def.model != null) return def;
        return null;
    }

    private static bool TryCampBounds(GameObject instance, out Bounds bounds)
    {
        bounds = default;
        bool initialized = false;
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            if (!initialized) { bounds = renderer.bounds; initialized = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return initialized;
    }

    private void BuildCampTent(Vector3 position, int lightFabric = 0x9B9B63, int darkFabric = 0x777D46,
        int backFabric = 0x545C37, int poleColor = 0xAE8252)
    {
        Transform tent = new GameObject("Field tent").transform;
        tent.SetParent(campScenery.transform, false);
        tent.localPosition = position;
        tent.localRotation = Quaternion.Euler(0, -12, 0);
        const float width = .91f;
        const float height = 1.38f;
        const float depth = .84f;
        float roofLength = Mathf.Sqrt(width * width + height * height);
        float angle = Mathf.Atan2(height, width) * Mathf.Rad2Deg;
        Material canvasA = CampMat(lightFabric);
        Material canvasB = CampMat(darkFabric);
        Material canvasBack = CampMat(backFabric);
        Material wood = CampMat(poleColor);

        // Two sloped fabric sides and a closed rear, leaving a dark triangular entrance.
        CampCube(tent, "Left canvas", new Vector3(-width / 2, height / 2, 0), new Vector3(roofLength, .055f, depth * 2),
            Quaternion.Euler(0, 0, angle), canvasA);
        CampCube(tent, "Right canvas", new Vector3(width / 2, height / 2, 0), new Vector3(roofLength, .055f, depth * 2),
            Quaternion.Euler(0, 0, -angle), canvasB);
        CampTriangle(tent, "Rear fabric", new Vector3(-width, 0, depth), new Vector3(width, 0, depth), new Vector3(0, height, depth), canvasBack);
        CampTriangle(tent, "Tent interior", new Vector3(-width * .79f, .015f, depth * .18f), new Vector3(width * .79f, .015f, depth * .18f),
            new Vector3(0, height * .85f, depth * .18f), CampMat(0x323B27));
        CampCube(tent, "Ridge pole", new Vector3(0, height, .015f), new Vector3(.065f, .065f, depth * 2.2f), Quaternion.identity, wood);
        CampCube(tent, "Left pole", new Vector3(-width / 2, height / 2, -depth - .025f), new Vector3(roofLength, .045f, .045f),
            Quaternion.Euler(0, 0, angle), wood);
        CampCube(tent, "Right pole", new Vector3(width / 2, height / 2, -depth - .025f), new Vector3(roofLength, .045f, .045f),
            Quaternion.Euler(0, 0, -angle), wood);
    }

    private void BuildCampCrate(Vector3 position, Vector3 size, bool ammo)
    {
        Transform box = new GameObject(ammo ? "Ammo case" : "Supply crate").transform;
        box.SetParent(campScenery.transform, false);
        box.localPosition = position;
        box.localRotation = Quaternion.Euler(0, ammo ? -12 : 13, 0);
        Material body = CampMat(ammo ? 0x666D3E : 0xA47747);
        Material edge = CampMat(ammo ? 0x4D5733 : 0xD1A06B);
        CampCube(box, "Body", Vector3.zero, size, Quaternion.identity, body);
        CampCube(box, "Lid", Vector3.up * (size.y * .5f), new Vector3(size.x * 1.04f, .07f, size.z * 1.04f), Quaternion.identity, edge);
        if (ammo)
        {
            Material brass = CampMat(0xD9BD77);
            for (int i = -1; i <= 1; i++)
                CampCube(box, "Ammo marking", new Vector3(i * .13f, -.015f, -size.z * .505f), new Vector3(.065f, .15f, .01f), Quaternion.identity, brass);
        }
        else
        {
            float face = -size.z * .516f;
            CampCube(box, "Top rail", new Vector3(0, size.y * .39f, face), new Vector3(size.x, .08f, .035f), Quaternion.identity, edge);
            CampCube(box, "Bottom rail", new Vector3(0, -size.y * .40f, face), new Vector3(size.x, .08f, .035f), Quaternion.identity, edge);
            CampCube(box, "Diagonal rail", new Vector3(0, 0, face), new Vector3(size.x * 1.10f, .075f, .035f), Quaternion.Euler(0, 0, 39), edge);
        }
    }

    private Material CampMat(int rgb, float emission = 0f)
    {
        if(campMaterialCache.TryGetValue((rgb,emission),out Material cached)) return cached;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = "Menu prop material", color = ArcadeUiTheme.Rgb(rgb) };
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0);
        if (emission > 0 && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", ArcadeUiTheme.Rgb(rgb) * emission);
        }
        campMaterials.Add(material);
        campMaterialCache[(rgb,emission)]=material;
        return material;
    }

    private void CampCube(Transform parent, string name, Vector3 position, Vector3 scale, Quaternion rotation, Material mat)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        cube.transform.localRotation = rotation;
        cube.GetComponent<Collider>().enabled = false;
        cube.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private void CampTriangle(Transform parent, string name, Vector3 a, Vector3 b, Vector3 c, Material mat)
    {
        GameObject surface = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        surface.transform.SetParent(parent, false);
        Mesh mesh = new Mesh { name = name, vertices = new[] { a, b, c, a, c, b }, triangles = new[] { 0, 1, 2, 3, 4, 5 } };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        campMeshes.Add(mesh);
        surface.GetComponent<MeshFilter>().sharedMesh = mesh;
        surface.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
