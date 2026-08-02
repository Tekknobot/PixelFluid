#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class SurferSpecialAssetImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        bool surfer = path.EndsWith("Resources/Surfers/chuck_water_slash.png") ||
                      path.EndsWith("Resources/Surfers/chuck_flow_finish.png");
        bool slash = path.EndsWith("Resources/VFX/water_slash.png");
        if (!surfer && !slash) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 32f;
        int cell = surfer ? 32 : 64;
        int count = 16;
        SpriteMetaData[] sprites = new SpriteMetaData[count];
        for (int i = 0; i < count; i++)
        {
            sprites[i] = new SpriteMetaData
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path) + "_" + i,
                rect = new Rect(i * cell, 0, cell, cell),
                pivot = new Vector2(0.5f, 0.5f),
                alignment = (int)SpriteAlignment.Center
            };
        }
#pragma warning disable CS0618
        importer.spritesheet = sprites;
#pragma warning restore CS0618
    }
}
#endif
