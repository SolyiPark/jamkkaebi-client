using System;
using System.IO;
using MobilePrototype;
using MobilePrototype.Exhibition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class ExhibitionSetup
{
    private const string ArtPath = "Assets/MobileUI/Art/Exhibition";
    private const string GridPath = "Assets/MobileUI/Configuration/ExhibitionGrid.asset";

    /// <summary>
    /// 전시관 및 기존 회귀 검사를 수행한 뒤 Windows 개발 빌드를 생성합니다.
    /// </summary>
    public static void ValidateAndBuild()
    {
        ExhibitionChecks.Run();
        ReviewRegressionChecks.Run();
        IntegratedSetup.Build();
    }

    // Explicit batch migration only. Never regenerate the home automatically on editor startup.
    /// <summary>
    /// 배치 모드에서 배경 임포트 설정과 홈 씬을 생성하는 일회성 마이그레이션입니다. 기존 홈 씬을 덮어쓰므로 수동 편집 후 재실행하지 않습니다.
    /// </summary>
    public static void Prepare()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run the one-time home migration in batch mode.");
        Directory.CreateDirectory(ArtPath);
        for (int i = 1; i <= 10; i++)
        {
            string destination = $"{ArtPath}/Artwork{i:00}.png";
            if (!File.Exists(destination))
                File.Copy(Path.GetFullPath($"../잠깨비배경분리/제목_없는_아트워크-{i} 2.png"), destination);
        }
        AssetDatabase.Refresh();
        for (int i = 1; i <= 10; i++)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath($"{ArtPath}/Artwork{i:00}.png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1000;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            foreach (string platform in new[] { "Android", "iPhone" })
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                { name = platform, overridden = true, maxTextureSize = 2048, format = TextureImporterFormat.ASTC_6x6 });
            importer.SaveAndReimport();
        }
        var grid = AssetDatabase.LoadAssetAtPath<ExhibitionGrid>(GridPath);
        if (!grid) { grid = ScriptableObject.CreateInstance<ExhibitionGrid>(); AssetDatabase.CreateAsset(grid, GridPath); }
        AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // NewScene can unload editor-only references. Resolve the persistent asset in the new scene.
        grid = AssetDatabase.LoadAssetAtPath<ExhibitionGrid>(GridPath);
        var root = new GameObject("Home Exhibition", typeof(MirrorSceneRoot), typeof(ExhibitionIntro), typeof(ExhibitionView));
        var camera = new GameObject("Exhibition Camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.SetParent(root.transform, false);
        camera.transform.localPosition = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.orthographicSize = 3.84f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.94f, .95f, .97f);
        camera.enabled = false;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        var mirrorRoot = root.GetComponent<MirrorSceneRoot>();
        mirrorRoot.sceneCamera = camera;
        mirrorRoot.uiRaycasters = new UnityEngine.UI.GraphicRaycaster[0];
        var view = new SerializedObject(root.GetComponent<ExhibitionView>());
        view.FindProperty("_camera").objectReferenceValue = camera;
        view.ApplyModifiedPropertiesWithoutUndo();
        var world = new GameObject("Artwork space (4320 x 7680, center pivot, 1000 PPU)").transform;
        world.SetParent(root.transform, false);
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/MobileUI/Materials/SpriteUnlit.mat");
        float[] parallax = { .015f, .055f, .025f, .11f, .18f, .27f, .32f, .36f, .01f, 0 };
        int[] orders = { 0, 10, 20, 30, 100, 2000, 2010, 2020, 2100, 2110 };
        for (int i = 1; i <= 10; i++)
        {
            var layer = new GameObject($"Artwork {i:00}", typeof(SpriteRenderer), typeof(ExhibitionLayer));
            layer.transform.SetParent(world, false);
            var renderer = layer.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtPath}/Artwork{i:00}.png");
            renderer.sharedMaterial = material;
            renderer.sortingOrder = orders[i - 1];
            layer.GetComponent<ExhibitionLayer>().Configure(parallax[i - 1], (i - 1) * .09f,
                i == 10 ? .075f : i == 9 ? .22f : 1, i == 1, i == 2 || i == 4 || i == 6 || i == 7 || i == 8);
            if (i == 5)
            {
                var surface = layer.AddComponent<ExhibitionSurface>();
                var serialized = new SerializedObject(surface);
                serialized.FindProperty("_grid").objectReferenceValue = grid;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var housing = new GameObject("Housing Content (platform local coordinates)");
                housing.transform.SetParent(layer.transform, false);
            }
        }
        MirrorSceneRoot.SetLayer(root.transform, 8);
        camera.cullingMask = 1 << 8;
        EditorSceneManager.SaveScene(scene, "Assets/MobileUI/Scenes/Home.unity");
        EditorSceneManager.OpenScene(PrototypeSetup.Shell);
        AssetDatabase.SaveAssets();
        Debug.Log("EXHIBITION_PREPARED");
    }
}
