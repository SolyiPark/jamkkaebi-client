using System;
using Jamkkaebi.Scripts.UI.Friends;
using Jamkkaebi.Scripts.UI.Exhibition;
using Jamkkaebi.Scripts.UI.Shell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>기존 친구 더미 씬만 최초 한 번 PDF 기반 프론트엔드로 이관합니다.</summary>
public static class FriendsSetup
{
    private const string VisitPrefabPath = "Assets/Jamkkaebi/Prefabs/Friends/FriendExhibitionStage.prefab";
    private const string VisualCatalogPath = "Assets/Jamkkaebi/Configuration/ExhibitionVisualCatalog.asset";

    /// <summary>기존 씬을 재생성하지 않고 방문 자산·격리·필수 참조와 기존 회귀 검사를 확인합니다.</summary>
    public static void ValidateVisitStage()
    {
        EditorSceneManager.OpenScene("Assets/Jamkkaebi/Scenes/Friends.unity");
        var root = UnityEngine.Object.FindFirstObjectByType<MirrorSceneRoot>();
        var presenter = root.GetComponent<FriendsPresenter>();
        var rebuilder = root.GetComponent<FriendExhibitionRebuilder>();
        if (!presenter || !rebuilder || presenter.Rebuilder != rebuilder || !rebuilder.Surface ||
            rebuilder.StageActive || rebuilder.Surface.Grid.Size != new Vector2Int(7, 7))
            throw new InvalidOperationException("방문 스테이지의 초기 상태·격자·표시 연결이 올바르지 않습니다.");
        var data = new SerializedObject(rebuilder);
        foreach (string field in new[] { "_root", "_stage", "_surface", "_content", "_catalog", "_material" })
            if (!data.FindProperty(field).objectReferenceValue) throw new InvalidOperationException("방문 참조 누락: " + field);
        if (root.GetComponentsInChildren<ExhibitionLayer>(true).Length != 10 ||
            root.GetComponentsInChildren<ExhibitionIntro>(true).Length != 0 ||
            root.GetComponentsInChildren<ExhibitionHousing>(true).Length != 0)
            throw new InvalidOperationException("방문 배경은 홈의 등장·편집 컴포넌트와 분리되어야 합니다.");
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (child.gameObject.layer != 11 || child.gameObject.scene != root.gameObject.scene ||
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                throw new InvalidOperationException("방문 씬·레이어·스크립트 참조 오류: " + child.name);
        var catalog = AssetDatabase.LoadAssetAtPath<ExhibitionVisualCatalog>(VisualCatalogPath);
        catalog.Validate();
        foreach (int id in new[] { 2, 8 })
        {
            if (!catalog.TryGet(false, id, out var definition) || !definition.Prefab)
                throw new InvalidOperationException("실제 도깨비 표현 누락: " + id);
            foreach (var child in definition.Prefab.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                    throw new InvalidOperationException("도깨비 프리팹 스크립트 누락: " + child.name);
            foreach (var renderer in definition.Prefab.GetComponentsInChildren<SpriteRenderer>(true))
                if (!renderer.sprite) throw new InvalidOperationException("도깨비 Sprite 누락: " + renderer.name);
        }
        ExhibitionChecks.Run();
        ReviewRegressionChecks.Run();
        Debug.Log("FRIEND_VISIT_STAGE_VALIDATED");
    }

    /// <summary>기존 친구 화면을 보존하고 방문 스테이지·카탈로그만 한 번 추가합니다.</summary>
    public static void ApplyVisitStage()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("저장된 씬을 대상으로 batch mode에서 실행하세요.");
        var scene = EditorSceneManager.OpenScene("Assets/Jamkkaebi/Scenes/Friends.unity");
        var root = UnityEngine.Object.FindFirstObjectByType<MirrorSceneRoot>();
        if (!root || !root.sceneCamera || !root.GetComponent<FriendsPresenter>())
            throw new InvalidOperationException("기존 친구 화면과 카메라가 필요합니다.");
        if (root.GetComponent<FriendExhibitionRebuilder>() || AssetDatabase.LoadAssetAtPath<GameObject>(VisitPrefabPath))
            throw new InvalidOperationException("방문 스테이지는 이미 구성되어 있습니다. 기존 자산을 직접 편집하세요.");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Jamkkaebi/Materials/SpriteUnlit.mat");
        var grid = AssetDatabase.LoadAssetAtPath<ExhibitionGrid>("Assets/Jamkkaebi/Configuration/ExhibitionGrid.asset");
        if (!material || !grid || !grid.IsValid) throw new InvalidOperationException("공용 전시관 재질과 격자가 필요합니다.");
        var catalog = AssetDatabase.LoadAssetAtPath<ExhibitionVisualCatalog>(VisualCatalogPath);
        if (!catalog)
        {
            catalog = ScriptableObject.CreateInstance<ExhibitionVisualCatalog>();
            var relics = new ExhibitionVisualDefinition[9];
            for (int id = 1; id <= relics.Length; id++)
                relics[id - 1] = CreateVisual(id, id == 2 ? "Dokkaebi_A2" : id == 8 ? "Dokkaebi_C2" : null);
            var buildings = new ExhibitionVisualDefinition[3];
            for (int id = 1; id <= buildings.Length; id++) buildings[id - 1] = CreateVisual(id, null);
            catalog.Configure(relics, buildings);
            AssetDatabase.CreateAsset(catalog, VisualCatalogPath);
        }
        catalog.Validate();
        var stage = new GameObject("Friend Exhibition Stage");
        var layers = new ExhibitionLayer[10];
        for (int i = 1; i <= 10; i++)
        {
            var layer = new GameObject($"Artwork {i:00}", typeof(SpriteRenderer), typeof(ExhibitionLayer));
            layer.transform.SetParent(stage.transform, false);
            ExhibitionArtworkSetup.Configure(layer, i, material);
            layers[i - 1] = layer.GetComponent<ExhibitionLayer>();
            if (i == 5)
            {
                var surface = new SerializedObject(layer.AddComponent<ExhibitionSurface>());
                surface.FindProperty("_grid").objectReferenceValue = grid;
                surface.ApplyModifiedPropertiesWithoutUndo();
                var content = new GameObject("Visit Content");
                content.transform.SetParent(layer.transform, false);
            }
        }
        var input = new GameObject("Visit Input", typeof(BoxCollider2D), typeof(FriendExhibitionInput));
        input.transform.SetParent(stage.transform, false);
        input.GetComponent<BoxCollider2D>().size = new Vector2(40, 40);
        MirrorSceneRoot.SetLayer(stage.transform, 11);
        stage.SetActive(false);
        if (!AssetDatabase.IsValidFolder("Assets/Jamkkaebi/Prefabs/Friends"))
            AssetDatabase.CreateFolder("Assets/Jamkkaebi/Prefabs", "Friends");
        var prefab = PrefabUtility.SaveAsPrefabAsset(stage, VisitPrefabPath);
        UnityEngine.Object.DestroyImmediate(stage);
        stage = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        stage.transform.SetParent(root.transform, false);
        var stageSurface = stage.GetComponentInChildren<ExhibitionSurface>(true);
        var rebuilder = root.gameObject.AddComponent<FriendExhibitionRebuilder>();
        rebuilder.Configure(root, stage, stageSurface, stageSurface.transform.Find("Visit Content"),
            stage.GetComponentsInChildren<ExhibitionLayer>(true), catalog, material);
        var stageInput = stage.GetComponentInChildren<FriendExhibitionInput>(true);
        stageInput.Configure(rebuilder);
        PrefabUtility.RecordPrefabInstancePropertyModifications(stageInput);
        root.GetComponent<FriendsPresenter>().ConfigureVisit(rebuilder);
        EditorUtility.SetDirty(rebuilder);
        EditorUtility.SetDirty(root.GetComponent<FriendsPresenter>());
        MirrorSceneRoot.SetLayer(root.transform, 11);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(PrototypeSetup.Shell);
        Debug.Log("FRIEND_VISIT_STAGE_CONFIGURED");
    }

    private static ExhibitionVisualDefinition CreateVisual(int id, string prefabName)
    {
        var prefab = prefabName == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Jamkkaebi/Prefabs/Characters/{prefabName}.prefab");
        if (prefabName != null && !prefab) throw new InvalidOperationException($"도깨비 프리팹 {prefabName}이 없습니다.");
        Vector3 scale = Vector3.one;
        Vector2 footOffset = Vector2.zero;
        var touchRect = new Rect(-.2f, 0, .4f, .5f);
        if (prefab)
        {
            // 원본의 피벗과 크기를 그대로 보존하고 방문 표현 정의에서만 칸 크기에 맞춥니다.
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException($"{prefabName}의 SpriteRenderer가 없습니다.");
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float factor = .3f / Mathf.Max(bounds.size.x, .001f);
                scale = Vector3.one * factor;
                footOffset = new Vector2(-bounds.center.x * factor, -bounds.min.y * factor);
                touchRect = new Rect(-.18f, 0, .36f, Mathf.Max(.4f, bounds.size.y * factor));
                Debug.Log($"VISIT_VISUAL {prefabName}: bounds={bounds} scale={factor} offset={footOffset}");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        return new ExhibitionVisualDefinition(id, prefab, scale, footOffset, touchRect);
    }

    /// <summary>저장된 친구 더미 씬을 한 번만 이관하고 카메라·글꼴·방문 격자를 연결합니다.</summary>
    public static void Apply()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("저장된 씬을 대상으로 batch mode에서 실행하세요.");
        var scene = EditorSceneManager.OpenScene("Assets/Jamkkaebi/Scenes/Friends.unity");
        var root = UnityEngine.Object.FindFirstObjectByType<MirrorSceneRoot>();
        if (!root || !root.sceneCamera || root.GetComponent<FriendsPresenter>())
            throw new InvalidOperationException("기존 친구 더미 씬만 이관할 수 있습니다.");
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            if (root.transform.GetChild(i) != root.sceneCamera.transform)
                UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        root.name = "Friends Root"; root.sceneCamera.name = "Friends Camera";
        root.uiRaycasters = Array.Empty<UnityEngine.UI.GraphicRaycaster>();
        var presenter = new SerializedObject(root.gameObject.AddComponent<FriendsPresenter>());
        presenter.FindProperty("_font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Jamkkaebi/Fonts/NotoSansCJKkr-Regular.otf");
        presenter.ApplyModifiedPropertiesWithoutUndo();
        MirrorSceneRoot.SetLayer(root.transform, 11); root.sceneCamera.cullingMask = 1 << 11;
        EditorSceneManager.SaveScene(scene);
        ApplyVisitStage();
        Debug.Log("FRIENDS_SCENE_CONFIGURED");
    }
}
