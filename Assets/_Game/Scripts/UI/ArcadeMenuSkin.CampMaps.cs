using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class ArcadeMenuSkin
{
    // Coordinates are relative to the player and camera, not to the arena map.
    // Keep the central character area open and frame it with props on the sides and behind.
    private void BuildBeachCamp(EnvironmentTheme theme)
    {
        // The shoreline is close enough to be visible from the menu camera. These are
        // decorative meshes only, leaving the actual beach's water and ground untouched.
        float[] shore = { 3.8f, 4.0f, 3.6f, 3.25f, 3.4f, 3.75f, 3.35f, 3.05f, 3.45f };
        CampWaterRibbon("Wet sand", shore, -.36f, .05f, .009f, CampMat(0xBFA579));
        CampWaterRibbon("Sea foam", shore, .05f, .19f, .012f, CampMat(0xD7E5DC));
        CampWaterRibbon("Shallows", shore, .19f, .76f, .010f, CampMat(0x79BABA));
        CampWaterRibbon("Ocean", shore, .76f, 12f, .009f, CampMat(0x4B909F));

        CampModel(theme, "Palm", -3.55f, 1.7f, 3.0f, 42, 2.25f);
        CampModel(theme, "Palm", -1.8f, 3.0f, 2.55f, 127, 1.9f);
        CampModel(theme, "Palm", 1.9f, 2.6f, 2.8f, 235, 2.1f);
        CampModel(theme, "CornerPalm", 3.45f, 1.0f, 3.1f, 85, 2.2f);
        CampModel(theme, "BigPalm", -3.1f, -2.3f, 2.65f, 160, 2.2f);
        CampModel(theme, "Tower", -3.15f, 2.75f, 2.2f, 10, 1.7f);
        CampModel(theme, "BeachRock", 1.95f, .95f, .68f, 55, 1.15f);
        CampModel(theme, "BeachRock", 2.45f, -1.35f, .76f, 15, 1.35f);
        CampModel(theme, "Driftwood", -1.1f, -1.85f, .38f, 25, 1.15f);
        CampModel(theme, "Driftwood", 1.55f, 2.65f, .32f, 91, 1.1f);
        CampModel(theme, "Crate", -1.5f, 1.05f, .66f, 17, .8f);
        CampModel(theme, "Shell", -.92f, -.64f, .13f, 85, .28f);
        CampModel(theme, "Shell", 1.1f, -1.45f, .13f, 16, .25f);
        CampModel(theme, "Shell", 1.35f, .78f, .11f, 100, .24f);
        CampModel(theme, "LeafTrash", -1.15f, 2.4f, .13f, 48, .45f);
        CampModel(theme, "Dune", 2.7f, 2.0f, .28f, 32, 1.2f);

        BuildBeachParasol(new Vector3(-2.0f, .018f, .47f));
        BuildBeachTowel(new Vector3(-1.65f, .026f, -.63f));
    }

    private void BuildMountainCamp(EnvironmentTheme theme)
    {
        CampModel(theme, "SnowyPeak", -3.1f, 6.7f, 4.8f, 16, 4.7f);
        CampModel(theme, "BigPeak", .1f, 7.7f, 4.5f, 75, 4.2f);
        CampModel(theme, "SnowyPeak", 3.5f, 6.3f, 4.6f, 143, 4.3f);
        CampModel(theme, "CaveCliff", -3.5f, 3.3f, 2.8f, 12, 3.1f);
        CampModel(theme, "RockFormation", 2.85f, 3.4f, 2.1f, 85, 2.25f);
        CampModel(theme, "Fir", -1.6f, 3.75f, 2.55f, 110, 1.3f);
        CampModel(theme, "BigFir", 1.15f, 4.3f, 2.8f, 45, 1.5f);
        CampModel(theme, "Fir", 3.2f, 1.95f, 2.35f, 210, 1.4f);
        CampModel(theme, "Boulder", -3.1f, -1.7f, 1.25f, 190, 1.65f);
        CampModel(theme, "Boulder", 2.25f, -.9f, .9f, 25, 1.25f);
        CampModel(theme, "Boulder", 1.45f, 1.7f, .74f, 62, .95f);
        CampModel(theme, "Pebble", -.95f, -.9f, .18f, 69, .42f);
        CampModel(theme, "Pebble", 1.1f, -1.5f, .16f, 153, .42f);
        CampModel(theme, "Pebble", 2.0f, .1f, .18f, 30, .48f);
        CampModel(theme, "GrassClump", 1.15f, .7f, .23f, 157, .45f);
        CampModel(theme, "GrassClump", -1.15f, 2.2f, .22f, 30, .42f);

        BuildCampTent(new Vector3(-2.05f, .015f, 1.1f), 0x929FA3, 0x657B81, 0x455D63, 0x77706A);
        BuildCampCrate(new Vector3(-1.45f, .29f, -.1f), new Vector3(.56f, .58f, .55f), false);
        BuildCampCrate(new Vector3(-2.08f, .23f, -.46f), new Vector3(.58f, .45f, .46f), true);
        BuildCampTrailSign(new Vector3(1.45f, .01f, 2.35f), 0x7C7266, 0xB3A795);
    }

    private void BuildDesertCamp(EnvironmentTheme theme)
    {
        CampModel(theme, "Mesa", -3.35f, 6.2f, 3.85f, 20, 4.7f);
        CampModel(theme, "RedButte", 1.55f, 6.8f, 3.7f, 117, 4.1f);
        CampModel(theme, "CornerButte", 3.45f, 4.25f, 2.65f, 5, 3.2f);
        CampModel(theme, "MesaMini", -3.55f, 2.7f, 1.95f, 66, 2.5f);
        CampModel(theme, "Cactus", -1.45f, 3.1f, 1.9f, 65, .95f);
        CampModel(theme, "Cactus", 1.8f, 2.45f, 1.8f, 127, .95f);
        CampModel(theme, "Cactus", 3.1f, .3f, 1.55f, 205, .95f);
        CampModel(theme, "Cactus", -3.12f, -1.75f, 1.6f, 35, .95f);
        CampModel(theme, "Stone", 2.35f, -1.05f, .61f, 80, .95f);
        CampModel(theme, "Stone", -1.25f, 2.15f, .45f, 120, .8f);
        CampModel(theme, "DriedBush", 1.4f, -.38f, .37f, 45, .54f);
        CampModel(theme, "DriedBush", -1.0f, -1.67f, .34f, 90, .5f);
        CampModel(theme, "Dune", 2.6f, 1.3f, .27f, 40, 1.5f);
        CampModel(theme, "Dune", -2.4f, 3.35f, .30f, 95, 1.7f);
        CampModel(theme, "Bones", 1.42f, 1.12f, .22f, 75, .47f);
        CampModel(theme, "StoneSmall", -.92f, -.72f, .14f, 130, .3f);
        CampModel(theme, "StoneSmall", 1.1f, -1.4f, .17f, 10, .35f);

        BuildCampTent(new Vector3(-2.15f, .015f, 1.1f), 0xD7BD8B, 0xB49360, 0x8B724B, 0x9A7149);
        BuildCampCrate(new Vector3(-1.37f, .29f, -.02f), new Vector3(.60f, .58f, .56f), false);
        BuildCampSupplyCase(new Vector3(-2.03f, .02f, -.43f), new Vector3(.67f, .42f, .5f), 0x9B8054, 0x655741, 10);
        BuildCampTrailSign(new Vector3(1.75f, .01f, 1.75f), 0x8C6845, 0xBB9D72);
    }

    private void BuildCityCamp(EnvironmentTheme theme)
    {
        // Building facades establish a short city street without enclosing the player.
        CampModel(theme, "OfficeBuilding", -2.75f, 5.35f, 3.7f, 0, 2.65f);
        CampModel(theme, "TowerBlock", .25f, 6.0f, 4.7f, 0, 3.0f);
        CampModel(theme, "CornerSkyscraper", 3.1f, 4.3f, 3.85f, 0, 2.65f);
        CampModel(theme, "Container", -3.0f, 2.4f, 1.55f, 5, 2.6f);
        CampModel(theme, "Car", -1.9f, 1.1f, .80f, 80, 1.9f);
        CampModel(theme, "LowWall", 2.45f, 2.1f, .76f, 8, 1.55f);
        CampModel(theme, "StreetLamp", 1.95f, 1.8f, 2.15f, 25, .9f);
        CampModel(theme, "StreetLamp", -2.3f, 3.7f, 2.3f, 190, .9f);
        CampModel(theme, "Bench", 2.2f, 1.0f, .62f, 8, 1.1f);
        CampModel(theme, "Hydrant", 1.42f, -.45f, .46f, 25, .43f);
        CampModel(theme, "Bush", 2.95f, .12f, .64f, 8, .91f);
        CampModel(theme, "Bush", -3.0f, -1.65f, .66f, 120, 1.0f);

        BuildCityMarkings();
        BuildCampSupplyCase(new Vector3(-1.62f, .02f, -.48f), new Vector3(.66f, .44f, .5f), 0x68736C, 0x404B49, -10);
        BuildCityBarrier(new Vector3(-2.58f, .01f, -.52f), 13);
    }

    private void BuildSpaceCamp(EnvironmentTheme theme)
    {
        CampModel(theme, "StationWreck", -3.1f, 3.9f, 2.8f, 20, 3.6f);
        CampModel(theme, "CornerAsteroid", 2.8f, 4.45f, 2.75f, 102, 3.0f);
        CampModel(theme, "DarkRock", -.95f, 4.9f, 1.8f, 35, 2.3f);
        CampModel(theme, "Asteroid", 2.45f, .9f, 1.1f, 70, 1.55f);
        CampModel(theme, "Asteroid", -3.15f, -1.85f, 1.15f, 170, 1.6f);
        CampModel(theme, "CrystalShard", 1.4f, 2.3f, 1.25f, 20, .95f);
        CampModel(theme, "CrystalShard", 2.35f, -1.25f, .88f, 115, .85f);
        CampModel(theme, "BrokenPanel", -2.15f, 2.1f, 1.2f, 85, 1.55f);
        CampModel(theme, "Debris", 1.6f, .6f, .40f, 10, .72f);
        CampModel(theme, "Debris", 1.05f, -1.4f, .34f, 122, .65f);
        CampModel(theme, "SparkleShard", -.86f, -.74f, .25f, 37, .4f);
        CampModel(theme, "SparkleShard", -.85f, 2.0f, .30f, 90, .45f);
        CampModel(theme, "PipeGlow", 2.75f, 2.3f, .38f, 30, 1.15f);

        BuildSpaceTerminal(new Vector3(-2.05f, .018f, .83f));
        BuildCampSupplyCase(new Vector3(-1.45f, .018f, -.25f), new Vector3(.70f, .47f, .55f), 0x4F5F70, 0x303C4A, 14, 0x73B5BB);
        BuildCampSupplyCase(new Vector3(-2.17f, .018f, -.52f), new Vector3(.61f, .36f, .49f), 0x68727D, 0x3C4650, -7, 0x73B5BB);
    }

    private void BuildArenaCamp()
    {
        // PvP is a runtime map with no environment theme; use the same minimal game
        // materials instead of depending on one of the six PvE map assets.
        Transform stage = campScenery.transform;
        Material baseMat = CampMat(0x484F58);
        Material wallMat = CampMat(0x7C8793);
        Material slate = CampMat(0x343C46);
        Material gold = CampMat(0xD4AE68);
        Material blue = CampMat(0x7197B3);
        Material red = CampMat(0xB07870);

        CampCube(stage, "Back arena platform", new Vector3(0, .13f, 3.65f), new Vector3(6.4f, .26f, 1.0f), Quaternion.identity, baseMat);
        for (int side = -1; side <= 1; side += 2)
        {
            Material accent = side < 0 ? red : blue;
            float x = side * 2.65f;
            CampCube(stage, "Arena corner", new Vector3(x, .65f, 2.6f), new Vector3(1.02f, 1.3f, 1.0f), Quaternion.identity, wallMat);
            CampCube(stage, "Corner stripe", new Vector3(x, .9f, 2.09f), new Vector3(.89f, .21f, .035f), Quaternion.identity, accent);
            CampCube(stage, "Low arena barrier", new Vector3(side * 2.45f, .35f, .55f), new Vector3(.72f, .7f, 1.2f), Quaternion.identity, slate);
            CampCube(stage, "Barrier trim", new Vector3(side * 2.45f, .72f, .55f), new Vector3(.77f, .075f, 1.25f), Quaternion.identity, accent);
            for (int i = 0; i < 3; i++)
                CampCube(stage, "Lane marking", new Vector3(side * 1.65f, .019f, -1.25f + i * .54f),
                    new Vector3(.065f, .025f, .31f), Quaternion.identity, gold);
        }

        CampCube(stage, "Back arena wall", new Vector3(0, .86f, 4.1f), new Vector3(3.75f, 1.7f, .3f), Quaternion.identity, wallMat);
        CampCube(stage, "Arena center panel", new Vector3(0, 1.0f, 3.93f), new Vector3(1.6f, .7f, .04f), Quaternion.identity, slate);
        CampCube(stage, "Arena panel trim", new Vector3(0, 1.38f, 3.89f), new Vector3(1.65f, .09f, .05f), Quaternion.identity, gold);
        BuildCampSupplyCase(new Vector3(-1.85f, .025f, -.65f), new Vector3(.67f, .42f, .48f), 0x697582, 0x3C4955, -9);
        BuildCampSupplyCase(new Vector3(1.9f, .025f, -1.25f), new Vector3(.60f, .40f, .5f), 0x697582, 0x3C4955, 14);
    }

    private void CampWaterRibbon(string name, float[] shore, float frontOffset, float rearOffset, float y, Material mat)
    {
        int count = shore.Length;
        Vector3[] vertices = new Vector3[count * 2];
        int[] triangles = new int[(count - 1) * 6];
        for (int i = 0; i < count; i++)
        {
            float x = Mathf.Lerp(-8f, 8f, (float)i / (count - 1));
            vertices[i * 2] = new Vector3(x, y, shore[i] + frontOffset);
            vertices[i * 2 + 1] = new Vector3(x, y, shore[i] + rearOffset);
            if (i >= count - 1) continue;
            int t = i * 6;
            int v = i * 2;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v + 2; triangles[t + 4] = v + 1; triangles[t + 5] = v + 3;
        }
        var water = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        water.transform.SetParent(campScenery.transform, false);
        var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        campMeshes.Add(mesh);
        water.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = water.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private void BuildBeachParasol(Vector3 position)
    {
        Transform umbrella = new GameObject("Beach parasol").transform;
        umbrella.SetParent(campScenery.transform, false);
        umbrella.localPosition = position;
        Material wood = CampMat(0xAE865B);
        Material cream = CampMat(0xE8D9AD);
        Material coral = CampMat(0xC98676);
        CampCube(umbrella, "Parasol pole", new Vector3(0, .77f, 0), new Vector3(.06f, 1.54f, .06f), Quaternion.identity, wood);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            float b = (i + 1) * Mathf.PI / 4f;
            Vector3 p = new Vector3(Mathf.Cos(a) * .83f, 1.35f, Mathf.Sin(a) * .83f);
            Vector3 q = new Vector3(Mathf.Cos(b) * .83f, 1.35f, Mathf.Sin(b) * .83f);
            CampTriangle(umbrella, "Canvas segment", new Vector3(0, 1.67f, 0), p, q, i % 2 == 0 ? cream : coral);
        }
        CampCube(umbrella, "Finial", new Vector3(0, 1.69f, 0), new Vector3(.09f, .12f, .09f), Quaternion.identity, wood);
    }

    private void BuildBeachTowel(Vector3 position)
    {
        Transform towel = new GameObject("Beach towel").transform;
        towel.SetParent(campScenery.transform, false);
        towel.localPosition = position;
        towel.localRotation = Quaternion.Euler(0, -20, 0);
        Material linen = CampMat(0xE5D9B5);
        Material stripe = CampMat(0x719EA2);
        CampCube(towel, "Towel", Vector3.zero, new Vector3(.65f, .025f, .92f), Quaternion.identity, linen);
        for (int i = -1; i <= 1; i++)
            CampCube(towel, "Towel stripe", new Vector3(0, .017f, i * .25f), new Vector3(.66f, .014f, .075f), Quaternion.identity, stripe);
    }

    private void BuildCampTrailSign(Vector3 position, int postColor, int boardColor)
    {
        Transform sign = new GameObject("Trail sign").transform;
        sign.SetParent(campScenery.transform, false);
        sign.localPosition = position;
        sign.localRotation = Quaternion.Euler(0, -16, 0);
        Material post = CampMat(postColor);
        Material board = CampMat(boardColor);
        CampCube(sign, "Post", new Vector3(0, .47f, 0), new Vector3(.085f, .94f, .085f), Quaternion.identity, post);
        CampCube(sign, "Board", new Vector3(0, .81f, -.045f), new Vector3(.52f, .20f, .068f), Quaternion.identity, board);
        CampCube(sign, "Lower board", new Vector3(.07f, .55f, -.045f), new Vector3(.39f, .15f, .068f), Quaternion.Euler(0, 0, -4), board);
    }

    private void BuildCityMarkings()
    {
        Transform stage = campScenery.transform;
        Material stone = CampMat(0x9EABA9);
        Material curb = CampMat(0x778580);
        Material paint = CampMat(0xDEDDD0);
        CampCube(stage, "Sidewalk", new Vector3(2.38f, .045f, 1.18f), new Vector3(1.6f, .08f, 4.4f), Quaternion.identity, stone);
        CampCube(stage, "Sidewalk curb", new Vector3(1.58f, .062f, 1.18f), new Vector3(.065f, .11f, 4.4f), Quaternion.identity, curb);
        for (int i = 0; i < 5; i++)
            CampCube(stage, "Crosswalk", new Vector3(1.01f + i * .26f, .021f, -1.65f),
                new Vector3(.17f, .025f, .67f), Quaternion.identity, paint);
    }

    private void BuildCityBarrier(Vector3 position, float angle)
    {
        Transform barrier = new GameObject("Street barricade").transform;
        barrier.SetParent(campScenery.transform, false);
        barrier.localPosition = position;
        barrier.localRotation = Quaternion.Euler(0, angle, 0);
        Material feet = CampMat(0x626760);
        Material body = CampMat(0xD4A76E);
        Material dark = CampMat(0x5D6159);
        CampCube(barrier, "Barrier board", new Vector3(0, .45f, 0), new Vector3(.8f, .27f, .10f), Quaternion.identity, body);
        for (int i = -1; i <= 1; i += 2)
            CampCube(barrier, "Barrier foot", new Vector3(i * .27f, .20f, 0), new Vector3(.085f, .4f, .25f), Quaternion.identity, feet);
        for (int i = -1; i <= 1; i++)
            CampCube(barrier, "Warning stripe", new Vector3(i * .23f, .45f, -.056f),
                new Vector3(.085f, .27f, .014f), Quaternion.Euler(0, 0, -24), dark);
    }

    private void BuildSpaceTerminal(Vector3 position)
    {
        Transform terminal = new GameObject("Salvaged station terminal").transform;
        terminal.SetParent(campScenery.transform, false);
        terminal.localPosition = position;
        terminal.localRotation = Quaternion.Euler(0, -14, 0);
        Material frame = CampMat(0x596D81);
        Material housing = CampMat(0x3A4859);
        Material panel = CampMat(0x222E40);
        Material light = CampMat(0x78B9C1, .55f);
        CampCube(terminal, "Base plate", new Vector3(0, .055f, 0), new Vector3(.94f, .11f, .68f), Quaternion.identity, housing);
        CampCube(terminal, "Pedestal", new Vector3(0, .43f, 0), new Vector3(.40f, .77f, .44f), Quaternion.identity, frame);
        CampCube(terminal, "Monitor housing", new Vector3(0, .98f, 0), new Vector3(.77f, .46f, .25f), Quaternion.Euler(12, 0, 0), housing);
        CampCube(terminal, "Display", new Vector3(0, 1.01f, -.139f), new Vector3(.62f, .31f, .014f), Quaternion.Euler(12, 0, 0), panel);
        for (int i = 0; i < 3; i++)
            CampCube(terminal, "Screen indicator", new Vector3(-.16f, 1.09f - i * .073f, -.165f),
                new Vector3(.29f - i * .055f, .023f, .015f), Quaternion.Euler(12, 0, 0), light);
        CampCube(terminal, "Console status", new Vector3(0, .63f, -.23f), new Vector3(.26f, .05f, .02f), Quaternion.identity, light);
    }

    private void BuildCampSupplyCase(Vector3 position, Vector3 size, int bodyColor, int trimColor, float angle, int accentColor = -1)
    {
        Transform box = new GameObject("Supply case").transform;
        box.SetParent(campScenery.transform, false);
        box.localPosition = position;
        box.localRotation = Quaternion.Euler(0, angle, 0);
        Material body = CampMat(bodyColor);
        Material trim = CampMat(trimColor);
        CampCube(box, "Body", new Vector3(0, size.y * .5f, 0), size, Quaternion.identity, body);
        CampCube(box, "Lid", new Vector3(0, size.y, 0), new Vector3(size.x * 1.04f, .065f, size.z * 1.04f), Quaternion.identity, trim);
        for (int side = -1; side <= 1; side += 2)
            CampCube(box, "Case rib", new Vector3(side * size.x * .32f, size.y * .48f, 0),
                new Vector3(.075f, .98f * size.y, size.z * 1.04f), Quaternion.identity, trim);
        if (accentColor >= 0)
            CampCube(box, "Case status", new Vector3(0, .57f * size.y, -size.z * .517f),
                new Vector3(.2f, .044f, .015f), Quaternion.identity, CampMat(accentColor, .35f));
    }
}
