using System;
using System.IO;
using System.Text;
using Jamkkaebi.Editor.TextTables;
using Jamkkaebi.Editor.TextTables.Collection;
using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.UI.Collection;
using Jamkkaebi.Scripts.UI.Shell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 도감의 최초 씬 이관, 확정 카탈로그 동기화와 공통 이미지 자산 연결을 수행하는 에디터 도구입니다.
/// </summary>
public static class CollectionSetup
{
    private const string ScenePath = "Assets/Jamkkaebi/Scenes/Collection.unity";
    private const string CatalogPath = "Assets/Jamkkaebi/Configuration/CollectionCatalog.asset";
    private const string AppearancePath = "Assets/Jamkkaebi/Configuration/CollectionAppearance.asset";

    /// <summary>
    /// batch mode에서 기존 도감 더미를 최초 한 번 이관하고 카메라·카탈로그·이미지 참조와 탭 설정을 저장합니다.
    /// 일반 실행이나 빌드 준비를 위해 재호출하지 않습니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// batch mode가 아니거나 기존 도감 루트·카메라가 없거나 이미 이관된 씬이면 발생합니다.
    /// </exception>
    public static void Apply()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("도감 씬 이관은 저장된 씬을 대상으로 batch mode에서 실행하세요.");
        if (AssetDatabase.LoadAssetAtPath<Jamkkaebi.Scripts.Gameplay.Collection.CollectionCatalog>(CatalogPath) == null)
            SyncCatalogAsset(CatalogPath);
        AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var root = UnityEngine.Object.FindFirstObjectByType<MirrorSceneRoot>();
        if (!root || !root.sceneCamera) throw new InvalidOperationException("기존 도감 루트와 카메라가 필요합니다.");
        var existing = root.GetComponent<CollectionPresenter>();
        if (existing) throw new InvalidOperationException("이미 도감 씬이 이관되었습니다. 일반 빌드에 Apply를 재실행하지 마세요.");
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            var child = root.transform.GetChild(i);
            if (child != root.sceneCamera.transform) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        root.name = "Collection Root";
        var camera = root.sceneCamera;
        camera.name = "Collection Camera";
        camera.tag = "Untagged";
        camera.enabled = false;
        camera.orthographic = true;
        camera.backgroundColor = new Color(.97f, .96f, .93f);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) UnityEngine.Object.DestroyImmediate(listener);
        root.uiRaycasters = Array.Empty<UnityEngine.UI.GraphicRaycaster>();
        var presenter = new SerializedObject(root.gameObject.AddComponent<CollectionPresenter>());
        presenter.FindProperty("_catalog").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Jamkkaebi.Scripts.Gameplay.Collection.CollectionCatalog>(CatalogPath);
        presenter.FindProperty("_font").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Font>("Assets/Jamkkaebi/Fonts/NotoSansCJKkr-Regular.otf");
        presenter.FindProperty("_appearance").objectReferenceValue = EnsureAppearanceAsset();
        presenter.ApplyModifiedPropertiesWithoutUndo();
        MirrorSceneRoot.SetLayer(root.transform, 10);
        camera.cullingMask = 1 << 10;
        EditorSceneManager.SaveScene(scene);

        var tabs = AssetDatabase.LoadAssetAtPath<TabCatalog>(PrototypeSetup.CatalogPath);
        foreach (var tab in tabs.tabs)
            if (tab.id == "Collection") tab.showProfile = true;
        EditorUtility.SetDirty(tabs);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(PrototypeSetup.Shell);
        Debug.Log("COLLECTION_SCENE_CONFIGURED");
    }

    /// <summary>
    /// Play가 종료된 상태에서 도감 구조(CollectionCatalogData)와 텍스트 CSV(Configuration/Text)를 저장 자산에 반영하고
    /// 기존 자산 GUID와 그림 참조를 유지합니다. 결과(바뀐 행 수, 경고, 오류)를 로그로 남기고, 에디터에서는 대화 상자로도 보여 줍니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">Play 중이거나 Play 진입이 예약되어 있거나 CSV 검증에 실패하면 발생합니다.</exception>
    [MenuItem("Jamkkaebi/Collection/Import Text CSV")]
    [MenuItem("Prototype/Collection/Sync Catalog From Confirmed Data")]
    public static void SyncCatalog()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play를 종료한 뒤 도감 데이터를 갱신하세요.");
        string summary;
        try
        {
            summary = SyncCatalogAsset(CatalogPath);
        }
        catch (InvalidOperationException exception)
        {
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("도감 텍스트 임포트 실패", exception.Message, "확인");
            throw;
        }
        if (!Application.isBatchMode)
            EditorUtility.DisplayDialog("도감 텍스트 임포트", summary, "확인");
    }

    /// <summary>
    /// 지정 경로의 카탈로그를 생성하거나 갱신합니다. 구조와 이름은 CollectionCatalogData, 설명류 텍스트는 CSV를 쓰며
    /// 같은 시대·유물 ID의 모든 그림 참조를 보존한 뒤 저장합니다. CSV에 오류가 있으면 자산을 바꾸지 않습니다.
    /// </summary>
    /// <param name="catalogPath">생성하거나 갱신할 프로젝트 상대 카탈로그 자산 경로입니다.</param>
    /// <returns>바뀐 행 수와 경고 수를 담은 결과 요약입니다.</returns>
    /// <exception cref="InvalidOperationException">CSV 파일이 없거나 검증 오류가 있으면 발생합니다. 메시지에 모든 오류를 넣습니다.</exception>
    private static string SyncCatalogAsset(string catalogPath)
    {
        CollectionCatalog current = AssetDatabase.LoadAssetAtPath<CollectionCatalog>(catalogPath);
        CollectionCatalog confirmed = CollectionCatalogData.CreateCatalog();
        try
        {
            TextTableReport report = new TextTableReport();
            CollectionTextImportResult result = CollectionTextImport.Build(confirmed, current,
                ReadCsv(CollectionTextImport.ErasCsvPath), ReadCsv(CollectionTextImport.RelicsCsvPath), report);
            foreach (string warning in report.Warnings)
                Debug.LogWarning("도감 텍스트 임포트 경고: " + warning);
            if (result == null)
            {
                foreach (string error in report.Errors)
                    Debug.LogError("도감 텍스트 임포트 오류: " + error);
                throw new InvalidOperationException(
                    $"도감 CSV에 오류가 {report.Errors.Count}개 있어 카탈로그를 바꾸지 않았습니다.\n" + report.Format());
            }

            if (!current)
            {
                confirmed.Configure(result.Eras, result.Relics);
                AssetDatabase.CreateAsset(confirmed, catalogPath);
                confirmed = null;
            }
            else
            {
                Undo.RecordObject(current, "도감 텍스트 CSV 반영");
                current.Configure(result.Eras, result.Relics);
                EditorUtility.SetDirty(current);
            }
            AssetDatabase.SaveAssets();

            string summary = $"시대 {result.Eras.Count}행 중 {result.ChangedEraCount}행, " +
                $"유물 {result.Relics.Count}행 중 {result.ChangedRelicCount}행이 바뀌었습니다. 경고 {report.Warnings.Count}개.";
            if (report.Warnings.Count > 0)
                summary += "\n" + report.Format();
            Debug.Log("COLLECTION_CONFIRMED_CATALOG_SYNCED " + summary);
            return summary;
        }
        finally
        {
            if (confirmed) UnityEngine.Object.DestroyImmediate(confirmed);
        }
    }

    /// <summary>프로젝트 상대 경로의 CSV를 UTF-8로 읽습니다.</summary>
    /// <exception cref="InvalidOperationException">파일이 없는 경우입니다.</exception>
    private static string ReadCsv(string assetPath)
    {
        string fullPath = Path.GetFullPath(assetPath);
        if (!File.Exists(fullPath))
            throw new InvalidOperationException($"도감 텍스트 CSV가 없습니다: {assetPath}");
        return File.ReadAllText(fullPath, Encoding.UTF8);
    }

    /// <summary>
    /// batch mode에서 카탈로그를 동기화하고, 기존 씬 계층과 이미 연결된 외형 자산을 유지하며 누락된 이미지 설정을 연결합니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// batch mode가 아니거나 도감 표시 컴포넌트가 없거나 공통 이미지 자산 연결에 실패하면 발생합니다.
    /// </exception>
    public static void ConfigureImages()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("도감 이미지 구성은 저장된 씬을 대상으로 batch mode에서 실행하세요.");
        SyncCatalog();
        var scene = EditorSceneManager.OpenScene(ScenePath);
        CollectionAppearance appearance = EnsureAppearanceAsset();
        var root = UnityEngine.Object.FindFirstObjectByType<MirrorSceneRoot>();
        var component = root ? root.GetComponent<CollectionPresenter>() : null;
        if (!component) throw new InvalidOperationException("도감 씬에 CollectionPresenter가 필요합니다.");
        var presenter = new SerializedObject(component);
        var property = presenter.FindProperty("_appearance");
        if (!property.objectReferenceValue)
        {
            property.objectReferenceValue = appearance;
            presenter.ApplyModifiedPropertiesWithoutUndo();
            if (!property.objectReferenceValue)
                throw new InvalidOperationException("도감 공통 이미지 자산을 연결하지 못했습니다.");
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("COLLECTION_IMAGE_SLOTS_CONFIGURED");
    }

    /// <summary>
    /// 저장된 공통 도감 외형 자산을 반환하고, 자산이 없으면 빈 이미지 슬롯으로 생성해 저장합니다.
    /// </summary>
    /// <returns>도감 씬에서 공유할 기존 또는 새 외형 자산입니다.</returns>
    private static CollectionAppearance EnsureAppearanceAsset()
    {
        CollectionAppearance appearance = AssetDatabase.LoadAssetAtPath<CollectionAppearance>(AppearancePath);
        if (!appearance)
        {
            appearance = ScriptableObject.CreateInstance<CollectionAppearance>();
            AssetDatabase.CreateAsset(appearance, AppearancePath);
            AssetDatabase.SaveAssets();
        }
        return appearance;
    }
}
