using System;
using System.Collections;
using System.Linq;
using Jamkkaebi.Scripts.UI.Exhibition;
using Jamkkaebi.Scripts.UI.Friends;
using Jamkkaebi.Scripts.UI.Shell;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Dev
{
    /// <summary>실제 Player에서 화면 어댑터와 공용 선택·포커스·입력 변환을 함께 검사합니다.</summary>
    public static class SharedExhibitionVerification
    {
        public static IEnumerator Home(TabHost host, Action<Vector3> click, Func<Vector3, Vector2> map, Action<bool, string> check, Action<string> capture)
        {
            var housing = host.GetRoot(0).GetComponent<ExhibitionHousing>();
            var selection = housing.SelectionController;
            var camera = housing.GetComponent<ExhibitionCameraController>();
            check(selection && selection.Policy == housing.BehaviorPolicy && camera && !selection.Policy.EnableSelectionFocus,
                "home shared behavior references and focus off policy");
            var placement = housing.Layout.Placements[0];
            Vector3 point = housing.Surface.CellWorldPosition(placement.Cell) + Vector3.up * .3f;
            click(point);
            check(selection.Current?.Id == "housing:" + placement.Id && !camera.HasFocus, "home short mirrored touch selects placement without editing");
            click(point); check(selection.Current == null, "home reselect clears display selection");
            click(point); click(new Vector3(1.7f, 2, 0));
            check(selection.Current == null, "home empty background clears selection through mirror input");
            var npc = housing.Wanderer;
            npc.enabled = false;
            try
            {
                click(npc.transform.position + Vector3.up * .28f);
                check(selection.Current?.Root == npc.transform, "home NPC has an independent selectable instance");
                selection.Clear();
                yield return Focus(housing.Surface, selection, camera, npc.transform, map, check, capture, "home");
                var originalPolicy = housing.BehaviorPolicy;
                var readOnly = UnityEngine.Object.Instantiate(originalPolicy);
                try
                {
                    JsonUtility.FromJsonOverwrite("{\"_allowLayoutEditing\":false}", readOnly);
                    typeof(ExhibitionHousing).GetField("_policy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(housing, readOnly);
                    int count = housing.Layout.Placements.Count;
                    housing.ExecuteAction(placement.Id, HousingAction.Recall);
                    check(!housing.BeginPlacement(housing.DefaultItem) && housing.Layout.Placements.Count == count,
                        "home direct editing commands reject read only policy");
                }
                finally
                {
                    typeof(ExhibitionHousing).GetField("_policy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(housing, originalPolicy);
                    UnityEngine.Object.Destroy(readOnly);
                }
            }
            finally { selection.Clear(); npc.enabled = true; }
        }

        public static IEnumerator Visit(TabHost host, Action<Vector3> click, Func<Vector3, Vector2> map, Action<bool, string> check, Action<string> capture)
        {
            var visit = host.GetRoot(3).GetComponent<FriendExhibitionRebuilder>();
            var home = host.GetRoot(0).GetComponent<ExhibitionHousing>();
            var selection = visit.SelectionController;
            var target = visit.Views.First(pair => !pair.Key.Placement.IsBuilding);
            visit.enabled = false;
            try
            {
                selection.Clear();
                click(target.Value.transform.position + Vector3.up * .2f);
                check(selection.Current != null && !visit.CameraController.HasFocus, "visit mirrored short touch uses shared selection without default focus");
                click(target.Value.transform.position + Vector3.up * .2f);
                check(selection.Current == null, "visit reselect clears selection and detail");
                click(target.Value.transform.position + Vector3.up * .2f);
                click(new Vector3(1.7f, 1, 0));
                check(selection.Current == null, "visit empty background clears selection");
                yield return Focus(visit.Surface, selection, visit.CameraController, target.Value.transform, map, check, capture, "visit");
                check(home.SelectionController.Current == null &&
                    !ReferenceEquals(home.SelectionController, selection) &&
                    !ReferenceEquals(home.GetComponent<ExhibitionCameraController>(), visit.CameraController) && !visit.Session.CanEdit,
                    "visit selection camera and edit permissions remain independent from home");
            }
            finally { selection.Clear(); visit.enabled = true; }
        }

        private static IEnumerator Focus(ExhibitionSurface surface, ExhibitionSelectionController selection,
            ExhibitionCameraController camera, Transform target, Func<Vector3, Vector2> map, Action<bool, string> check, Action<string> capture, string label)
        {
            var original = selection.Policy;
            var policy = UnityEngine.Object.Instantiate(original);
            Vector3 targetPosition = target.position;
            camera.Apply();
            float size = camera.SceneCamera.orthographicSize;
            Vector3 position = camera.SceneCamera.transform.localPosition;
            try
            {
                JsonUtility.FromJsonOverwrite("{\"_enableSelectionFocus\":true}", policy);
                selection.Configure(policy, camera);
                selection.Select(new ExhibitionSelectionTarget("verification:npc", target));
                yield return new WaitForSecondsRealtime(.2f); camera.Apply();
                check(camera.HasFocus && camera.SceneCamera.orthographicSize < size, label + " selection focus remains applied across frames");
                var expected = new Vector2Int(3, 3);
                check(surface.TryGetCell(camera.SceneCamera, map(surface.CellWorldPosition(expected)), out var actual) && actual == expected,
                    label + " focused camera preserves screen RT world grid roundtrip");
                target.position += Vector3.right * .08f; camera.Apply();
                Vector3 expectedPosition = camera.SceneCamera.transform.parent.InverseTransformPoint(target.position);
                check(Vector2.Distance(camera.SceneCamera.transform.localPosition, expectedPosition) < .001f,
                    label + " focused camera follows moving contact without changing baseline");
                target.position = surface.CellWorldPosition(new Vector2Int(6, 0)); camera.Apply();
                var view = selection.GetComponent<ExhibitionView>();
                foreach (var layer in selection.GetComponentsInChildren<ExhibitionLayer>(true))
                    layer.Apply(view ? view.Intro.Elapsed : 100, 0, camera.SceneCamera);
                var paper = selection.GetComponentsInChildren<ExhibitionLayer>(true).First(layer =>
                    (bool)typeof(ExhibitionLayer).GetField("_coverBackground", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(layer));
                var bounds = paper.GetComponent<SpriteRenderer>().bounds;
                var lower = camera.SceneCamera.ViewportToWorldPoint(new Vector3(0, 0, 10));
                var upper = camera.SceneCamera.ViewportToWorldPoint(new Vector3(1, 1, 10));
                check(bounds.min.x <= lower.x && bounds.min.y <= lower.y && bounds.max.x >= upper.x && bounds.max.y >= upper.y,
                    label + " paper covers the viewport when focusing an edge cell");
                yield return new WaitForEndOfFrame(); capture(label + "_SelectionFocus.png");
                selection.Clear(); yield return new WaitForSecondsRealtime(.2f); camera.Apply();
                check(!camera.HasFocus && Vector3.Distance(camera.SceneCamera.transform.localPosition, position) < .001f &&
                    Mathf.Abs(camera.SceneCamera.orthographicSize - size) < .001f, label + " deselect restores original view");
                JsonUtility.FromJsonOverwrite("{\"_allowObjectSelection\":false}", policy);
                check(!selection.Select(new ExhibitionSelectionTarget("verification:blocked", target)) && !camera.HasFocus,
                    label + " selection policy blocks focus and selection");
                JsonUtility.FromJsonOverwrite("{\"_allowObjectSelection\":true}", policy);
                selection.Select(new ExhibitionSelectionTarget("verification:disable", target)); selection.enabled = false;
                check(selection.Current == null && !camera.HasFocus, label + " disabled selection component clears its runtime state");
                selection.enabled = true;
            }
            finally
            {
                target.position = targetPosition; selection.Configure(original, camera); selection.Clear();
                UnityEngine.Object.Destroy(policy);
            }
            yield return new WaitForSecondsRealtime(.2f);
            camera.Apply();
        }
    }
}
