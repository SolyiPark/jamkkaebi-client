using UnityEditor;
using UnityEngine;

public sealed class ExhibitionTextureImporter : AssetPostprocessor
{
    /// <summary>
    /// 전시관 이미지의 최초 임포트에만 사각 스프라이트 메시를 적용해 질감의 미세한 입자가 과도하게 삼각 분할되지 않도록 합니다.
    /// </summary>
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
