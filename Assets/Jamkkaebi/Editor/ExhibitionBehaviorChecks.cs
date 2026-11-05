using System;
using Jamkkaebi.Scripts.UI.Exhibition;
using Jamkkaebi.Scripts.UI.Friends;
using Jamkkaebi.Scripts.UI.Shell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>씬을 재생성하지 않고 공용 동작의 화면별 참조와 정책을 검사합니다.</summary>
public static class ExhibitionBehaviorChecks
{
    public static void Run()
    {
        ValidateScene("Home", "HomeExhibitionBehavior", true);
        ValidateScene("Friends", "VisitExhibitionBehavior", false);
        Debug.Log("EXHIBITION_BEHAVIOR_CHECKS_PASSED");
    }

    private static void ValidateScene(string name, string policyName, bool home)
    {
        EditorSceneManager.OpenScene($"Assets/Jamkkaebi/Scenes/{name}.unity");
        var root = UnityEngine.Object.FindFirstObjectByType<MirrorSceneRoot>();
        var camera = root.GetComponent<ExhibitionCameraController>();
        var selection = root.GetComponent<ExhibitionSelectionController>();
        var policy = AssetDatabase.LoadAssetAtPath<ExhibitionBehaviorPolicy>($"Assets/Jamkkaebi/Configuration/{policyName}.asset");
        Require(root.GetComponents<ExhibitionCameraController>().Length == 1 &&
            root.GetComponents<ExhibitionSelectionController>().Length == 1, name + " 독립 컴포넌트");
        Require(camera && camera.SceneCamera == root.sceneCamera && selection && selection.Policy == policy && policy,
            name + " 카메라·정책 참조");
        Require(new SerializedObject(selection).FindProperty("_cameraController").objectReferenceValue == camera, name + " 선택 포커스 카메라");
        if (home)
        {
            var housing = root.GetComponent<ExhibitionHousing>();
            var view = new SerializedObject(root.GetComponent<ExhibitionView>());
            Require(housing.BehaviorPolicy == policy && housing.SelectionController == selection &&
                view.FindProperty("_cameraController").objectReferenceValue == camera &&
                view.FindProperty("_policy").objectReferenceValue == policy, "홈 공용 동작 연결");
            Require(policy.AllowLayoutEditing && policy.PlayIntroduction && policy.EnableTabParallax, "홈 정책");
        }
        else
        {
            var visit = root.GetComponent<FriendExhibitionRebuilder>();
            Require(visit.Policy == policy && visit.CameraController == camera && visit.SelectionController == selection, "방문 공용 동작 연결");
            Require(!policy.AllowLayoutEditing && !policy.PlayIntroduction && !policy.EnableTabParallax &&
                policy.LockNavigationDuringVisit && !visit.Session.CanEdit && !root.GetComponent<ExhibitionHousing>() &&
                !root.GetComponent<ExhibitionIntro>(), "방문 읽기 전용 경계");
        }
        Require(policy.AllowObjectSelection && !policy.EnableSelectionFocus && policy.EnableAmbientMovement, name + " 기본 선택·이동 정책");
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, child.name + " 스크립트");
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        Debug.Log("PASS " + label);
    }
}
