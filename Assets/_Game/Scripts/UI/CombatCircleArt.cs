using UnityEngine;

/// <summary>Shared smooth disc/ring sprites for touch controls and status timers.</summary>
internal static class CombatCircleArt
{
    private static Sprite disc, ring;
    public static Sprite Disc => disc != null ? disc : disc = Create(false);
    public static Sprite Ring => ring != null ? ring : ring = Create(true);

    private static Sprite Create(bool hollow)
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size,size,TextureFormat.RGBA32,false);
        texture.name = hollow ? "Combat timer ring" : "Combat disc";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[size*size];
        for (int y=0;y<size;y++) for (int x=0;x<size;x++)
        {
            float dx=x+.5f-size*.5f, dy=y+.5f-size*.5f;
            float r=Mathf.Sqrt(dx*dx+dy*dy);
            float a=Mathf.Clamp01(63f-r);
            if (hollow) a*=Mathf.Clamp01(r-52f);
            pixels[y*size+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(a*255f));
        }
        texture.SetPixels32(pixels); texture.Apply(false,true);
        return Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);
    }
}
