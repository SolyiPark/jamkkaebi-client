using System;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Collection;
using MobilePrototype;
using MobilePrototype.Collection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class CollectionSetup
{
    private const string ScenePath = "Assets/MobileUI/Scenes/Collection.unity";
    private const string CatalogPath = "Assets/MobileUI/Configuration/CollectionCatalog.asset";
    private const string AppearancePath = "Assets/MobileUI/Configuration/CollectionAppearance.asset";

    // Explicit, one-time migration of this tab only. Builds never call this method.
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

    // Updates only the definitions; existing scene GUIDs and assigned artwork are retained.
    [MenuItem("Prototype/Collection/Sync Catalog From Confirmed Data")]
    public static void SyncCatalog()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play를 종료한 뒤 확정 도감 데이터를 갱신하세요.");
        SyncCatalogAsset(CatalogPath);
    }

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

    // Add image configuration to the saved tab without rebuilding its hierarchy.
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
