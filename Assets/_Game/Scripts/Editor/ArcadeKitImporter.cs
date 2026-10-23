using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public sealed class ArcadeKitImporter : AssetPostprocessor
{
    private const string Folder = "/UI/ArcadeKit/Resources/ArcadeKit/";

    static ArcadeKitImporter()
    {
        EditorApplication.delayCall += ReimportIfNeeded;
    }

    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains(Folder))
            return;

        Configure((TextureImporter)assetImporter);
    }

    private static void ReimportIfNeeded()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:Texture2D",
            new[] { "Assets/_Game/UI/ArcadeKit/Resources/ArcadeKit" }
        );

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                continue;

            if (importer.textureCompression == TextureImporterCompression.Uncompressed &&
                !importer.mipmapEnabled &&
                importer.wrapMode == TextureWrapMode.Clamp &&
                importer.maxTextureSize == 2048)
            {
                continue;
            }

            Configure(importer);
            importer.SaveAndReimport();
        }
    }

    private static void Configure(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Default;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = false;
    }
}
