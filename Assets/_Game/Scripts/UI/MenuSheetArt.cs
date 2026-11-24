using UnityEngine;

public static class MenuSheetArt
{
    private static readonly Sprite[] Tiles = new Sprite[9];

    public static Sprite Arena(GameMap map)
    {
        int index;
        switch (map.langKey)
        {
            case "les": index = 0; break;
            case "plyazh": index = 1; break;
            case "gory": index = 2; break;
            case "pustynya": index = 3; break;
            case "gorod": index = 4; break;
            case "kosmos": index = 5; break;
            default: return map.icon;
        }
        return Tile(index, 8 + index % 2 * 504, 8 + index / 2 * 242, 488, 226);
    }

    public static Sprite Item(EquipmentCardData item)
    {
        if (item.Clothing != null && item.Clothing.Preview != null)
            return item.Clothing.Preview;
        if (item.Ability != null) return AbilityPresentation.Icon(item.Ability.Kind);

        return Weapon(item.Weapon, item.IconKey);
    }

    public static Sprite Weapon(WeaponData weapon, string fallback)
    {
        if (weapon == null) return ArcadeKitAssets.Icon(fallback);
        if (weapon.Preview != null) return weapon.Preview;
        int family;
        switch (weapon.WeaponType)
        {
            case WeaponType.Rifle: family = 0; break;
            case WeaponType.Shotgun: family = 1; break;
            case WeaponType.SMG: family = 2; break;
            default: return ArcadeKitAssets.Icon(fallback);
        }
        return Tile(6 + family, 8 + family * 336, 784, 322, 138);
    }

    private static readonly Sprite[] icons = new Sprite[20];
    private static Texture2D atlas;

    public static Sprite Upgrade(UpgradeType type)
    {
        int tile = (int)type;
        if (atlas == null) atlas = Resources.Load<Texture2D>("ArcadeKit/wave_upgrades");
        if (atlas != null && tile >= 0 && tile < icons.Length)
        {
            if (icons[tile] == null)
            {
                const int side = 192;
                var region = new Rect(tile % 4 * side, atlas.height - (tile / 4 + 1) * side, side, side);
                icons[tile] = Sprite.Create(atlas, region, Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            }
            return icons[tile];
        }
        return ArcadeKitAssets.Icon("spark");
    }

    private static Sprite Tile(int index, int x, int top, int width, int height)
    {
        if (Tiles[index] != null) return Tiles[index];
        var atlas = Resources.Load<Texture2D>("ArcadeKit/armory_maps");
        if (atlas == null) return null;
        var rect = new Rect(x, atlas.height - top - height, width, height);
        return Tiles[index] = Sprite.Create(atlas, rect, Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
    }
}
