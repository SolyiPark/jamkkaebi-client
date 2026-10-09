using System;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Collection;
using MobilePrototype;
using MobilePrototype.Collection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 도감의 최초 씬 이관, 확정 카탈로그 동기화와 공통 이미지 자산 연결을 수행하는 에디터 도구입니다.
/// </summary>
public static class CollectionSetup
{
    private const string ScenePath = "Assets/MobileUI/Scenes/Collection.unity";
    private const string CatalogPath = "Assets/MobileUI/Configuration/CollectionCatalog.asset";
    private const string AppearancePath = "Assets/MobileUI/Configuration/CollectionAppearance.asset";

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
            AssetDatabase.CreateAsset(CollectionCatalogData.CreateCatalog(), CatalogPath);
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
            AssetDatabase.LoadAssetAtPath<Font>("Assets/MobileUI/Fonts/NotoSansCJKkr-Regular.otf");
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
    /// Play가 종료된 상태에서 확정 도감 정의를 저장 자산에 반영하고 기존 자산 GUID와 그림 참조를 유지합니다.
    /// </summary>
    /// <exception cref="InvalidOperationException">Play 중이거나 Play 진입이 예약되어 있으면 발생합니다.</exception>
    [MenuItem("Prototype/Collection/Sync Catalog From Confirmed Data")]
    public static void SyncCatalog()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play를 종료한 뒤 확정 도감 데이터를 갱신하세요.");
        SyncCatalogAsset(CatalogPath);
    }

    /// <summary>
    /// 지정 경로의 카탈로그를 생성하거나 갱신하고, 같은 시대·유물 ID의 모든 그림 참조를 보존한 뒤 저장합니다.
    /// </summary>
    /// <param name="catalogPath">생성하거나 갱신할 프로젝트 상대 카탈로그 자산 경로입니다.</param>
    private static void SyncCatalogAsset(string catalogPath)
    {
        CollectionCatalog current = AssetDatabase.LoadAssetAtPath<CollectionCatalog>(catalogPath);
        CollectionCatalog confirmed = CollectionCatalogData.CreateCatalog();
        try
        {
            if (!current)
            {
                AssetDatabase.CreateAsset(confirmed, catalogPath);
                confirmed = null;
            }
            else
            {
                CollectionEraDefinition[] eras = confirmed.Eras.Select(era =>
                {
                    CollectionEraDefinition previous = current.FindEra(era.Id);
                    return new CollectionEraDefinition(era.Id, era.Name, era.Period, era.Description,
                        era.BuildingName, era.Accent, previous?.OverviewIllustration,
                        previous?.ContextIllustration, previous?.BuildingIllustration,
                        previous?.LockedBuildingIllustration, previous?.BackgroundIllustration);
                }).ToArray();
                CollectionRelicDefinition[] relics = confirmed.Relics.Select(relic =>
                {
                    CollectionRelicDefinition previous = current.FindRelic(relic.Id);
                    return new CollectionRelicDefinition(relic.Id, relic.EraId, relic.Name, relic.SpiritName,
                        relic.Description, relic.ConservationNote, relic.Accent,
                        previous?.Illustration, previous?.SpiritIllustration, previous?.Thumbnail,
                        previous?.LockedIllustration, previous?.BackgroundIllustration);
                }).ToArray();
                Undo.RecordObject(current, "확정 도감 데이터 반영");
                current.Configure(eras, relics);
                EditorUtility.SetDirty(current);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("COLLECTION_CONFIRMED_CATALOG_SYNCED");
        }
        finally
        {
            if (confirmed) UnityEngine.Object.DestroyImmediate(confirmed);
        }
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
