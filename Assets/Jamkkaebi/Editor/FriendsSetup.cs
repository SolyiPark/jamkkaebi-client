using System;
using Jamkkaebi.Scripts.UI;
using Jamkkaebi.Scripts.UI.Friends;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>기존 친구 더미 씬만 최초 한 번 PDF 기반 프론트엔드로 이관합니다.</summary>
public static class FriendsSetup
{
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
        presenter.FindProperty("_visitGrid").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Jamkkaebi.Scripts.UI.Exhibition.ExhibitionGrid>("Assets/Jamkkaebi/Configuration/ExhibitionGrid.asset");
        presenter.ApplyModifiedPropertiesWithoutUndo();
        MirrorSceneRoot.SetLayer(root.transform, 11); root.sceneCamera.cullingMask = 1 << 11;
        EditorSceneManager.SaveScene(scene);
        Debug.Log("FRIENDS_SCENE_CONFIGURED");
    }
}
