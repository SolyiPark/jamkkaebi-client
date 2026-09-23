using UnityEditor;
using UnityEngine;

public sealed class ExhibitionTextureImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/MobileUI/Art/Exhibition/")) return;
        var importer = (TextureImporter)assetImporter;
        if (!importer.importSettingsMissing) return;
        // The project's sprite preset otherwise triangulates every tiny grain in artwork 9.
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 1000;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
    }
}
