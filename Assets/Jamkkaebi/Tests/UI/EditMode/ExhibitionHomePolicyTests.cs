using System.Reflection;
using Jamkkaebi.Scripts.UI.Exhibition;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jamkkaebi.Tests.UI
{
    public sealed class ExhibitionHomePolicyTests
    {
        [Test]
        public void ReadOnlyPolicyRejectsDirectEditingCommands()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Jamkkaebi/Scenes/Home.unity", OpenSceneMode.Additive);
            try
            {
                ExhibitionHousing housing = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.TryGetComponent(out ExhibitionHousing candidate)) housing = candidate;
                Assert.IsNotNull(housing);
                var data = new SerializedObject(housing);
                data.FindProperty("_policy").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ExhibitionBehaviorPolicy>(
                    "Assets/Jamkkaebi/Configuration/VisitExhibitionBehavior.asset");
                data.ApplyModifiedPropertiesWithoutUndo();
                bool requested = false; housing.PlacementRequested += () => requested = true;
                Assert.IsFalse(housing.CanEdit); Assert.IsFalse(housing.BeginPlacement(housing.DefaultItem));
                housing.RequestPlacement(); housing.ExecuteAction(1, HousingAction.Move);
                housing.ExecuteAction(1, HousingAction.Rotate); housing.ExecuteAction(1, HousingAction.Recall);
                housing.SelectCell(new Vector2Int(3, 3)); housing.RemoveSelected();
                Assert.IsFalse(requested); Assert.IsFalse(housing.IsEditingCell);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void MovementDisabledPreservesTraversalAndFixedAnchor()
        {
            var root = new GameObject("Movement"); var grid = ScriptableObject.CreateInstance<ExhibitionGrid>();
            var policy = ScriptableObject.CreateInstance<ExhibitionBehaviorPolicy>();
            try
            {
                var surface = root.AddComponent<ExhibitionSurface>();
                typeof(ExhibitionSurface).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(surface, grid);
                var layout = new HousingLayout(grid); var wanderer = root.AddComponent<DokkaebiWanderer>();
                var cell = new Vector2Int(1, 1); wanderer.Configure(layout, surface, cell, policy);
                JsonUtility.FromJsonOverwrite("{\"_enableAmbientMovement\":false}", policy);
                Vector3 position = root.transform.position;
                typeof(DokkaebiWanderer).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wanderer, null);
                Assert.AreEqual(position, root.transform.position); Assert.AreEqual(cell, wanderer.PlacementCell);
                Assert.AreEqual(cell, wanderer.CurrentCell); Assert.AreEqual(cell, wanderer.TargetCell);
                Assert.IsFalse(layout.CanPlace(cell, Vector2Int.one));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(grid); Object.DestroyImmediate(policy); }
        }
    }
}
