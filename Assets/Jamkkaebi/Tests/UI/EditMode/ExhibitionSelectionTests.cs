using Jamkkaebi.Scripts.UI.Exhibition;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.UI
{
    public sealed class ExhibitionSelectionTests
    {
        private GameObject _root;
        private GameObject _target;
        private ExhibitionSelectionController _selection;
        private ExhibitionCameraController _camera;
        private ExhibitionBehaviorPolicy _policy;
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Selection"); _target = new GameObject("Target", typeof(SpriteRenderer));
            _camera = _root.AddComponent<ExhibitionCameraController>(); _camera.Configure(_root.AddComponent<Camera>());
            _selection = _root.AddComponent<ExhibitionSelectionController>();
            _policy = ScriptableObject.CreateInstance<ExhibitionBehaviorPolicy>(); _selection.Configure(_policy, _camera);
        }
        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_root); Object.DestroyImmediate(_target); Object.DestroyImmediate(_policy); }
        private void Tick(string method) => typeof(ExhibitionSelectionController).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(_selection, null);
        [Test]
        public void ReselectRestoresOriginalAlphaAndEmitsClear()
        {
            var renderer = _target.GetComponent<SpriteRenderer>(); renderer.color = new Color(.2f, .4f, .6f, .4f);
            int events = 0; ExhibitionSelectionTarget changed = null;
            _selection.SelectionChanged += target => { events++; changed = target; };
            Assert.IsTrue(_selection.Select(new ExhibitionSelectionTarget("instance:1", _target.transform)));
            Assert.AreEqual(.3f, renderer.color.a, .0001f); Assert.IsNotNull(changed);
            Assert.IsFalse(_selection.Select(new ExhibitionSelectionTarget("instance:1", _target.transform)));
            Assert.AreEqual(.4f, renderer.color.a, .0001f); Assert.AreEqual(.2f, renderer.color.r, .0001f);
            Assert.IsNull(changed); Assert.AreEqual(2, events);
        }
        [Test]
        public void PolicyRevocationClearsFocusAndSelection()
        {
            JsonUtility.FromJsonOverwrite("{\"_enableSelectionFocus\":true}", _policy);
            _selection.Select(new ExhibitionSelectionTarget("instance:1", _target.transform)); Assert.IsTrue(_camera.HasFocus);
            JsonUtility.FromJsonOverwrite("{\"_allowObjectSelection\":false}", _policy); Tick("Update");
            Assert.IsNull(_selection.Current); Assert.IsFalse(_camera.HasFocus);
            Assert.IsFalse(_selection.Select(new ExhibitionSelectionTarget("instance:1", _target.transform)));
        }
        [Test]
        public void DestroyedTargetAndDisabledScreenClearSelection()
        {
            _selection.Select(new ExhibitionSelectionTarget("instance:1", _target.transform));
            _target.SetActive(false); Tick("Update"); Assert.IsNull(_selection.Current);
            _target.SetActive(true); _selection.Select(new ExhibitionSelectionTarget("instance:1", _target.transform));
            _selection.enabled = false; Tick("OnDisable"); Assert.IsNull(_selection.Current);
            _selection.enabled = true; _selection.Select(new ExhibitionSelectionTarget("instance:1", _target.transform));
            Object.DestroyImmediate(_target); Tick("Update"); Assert.IsNull(_selection.Current);
        }
        [Test]
        public void SharedPolicyDoesNotShareRuntimeSelection()
        {
            var other = new GameObject("Other");
            try
            {
                var selection = other.AddComponent<ExhibitionSelectionController>(); selection.Configure(_policy, null);
                _selection.Select(new ExhibitionSelectionTarget("instance:1", _target.transform)); selection.Clear();
                Assert.IsNull(selection.Current); Assert.IsNotNull(_selection.Current);
            }
            finally { Object.DestroyImmediate(other); }
        }
    }
}
