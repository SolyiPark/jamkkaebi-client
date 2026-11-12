using Jamkkaebi.Scripts.UI.Exhibition;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.UI
{
    public sealed class ExhibitionCameraTests
    {
        [TestCase(480f / 1040, 1f)]
        [TestCase(768f / 1024, 1f)]
        [TestCase(.5625f, 2f)]
        public void DefaultFramingMatchesExistingCalculation(float aspect, float zoom)
        {
            Assert.AreEqual(Mathf.Max(3.84f, 2.32f / Mathf.Max(.1f, aspect)) / zoom,
                ExhibitionCameraController.CalculateOrthographicSize(aspect, zoom), .00001f);
        }

        [Test]
        public void FocusTracksWorldContactAndClearingRestoresBaseline()
        {
            var root = new GameObject("Camera root"); var target = new GameObject("Target");
            try
            {
                var camera = root.AddComponent<Camera>();
                var controller = root.AddComponent<ExhibitionCameraController>(); controller.Configure(camera);
                JsonUtility.FromJsonOverwrite("{\"_transitionSeconds\":0}", controller);
                controller.SetView(new Vector2(.2f, -.1f), 1.2f); controller.Apply();
                Vector3 baseline = camera.transform.localPosition; float size = camera.orthographicSize;
                controller.SetFocus(target.transform); target.transform.position = new Vector3(2, 3, 0); controller.Apply();
                Assert.AreEqual(new Vector3(2, 3, -10), camera.transform.localPosition);
                Assert.Less(camera.orthographicSize, size);
                target.transform.position = Vector3.one; controller.Apply();
                Assert.AreEqual(new Vector3(1, 1, -10), camera.transform.localPosition);
                controller.ClearFocus(); controller.Apply();
                Assert.AreEqual(baseline, camera.transform.localPosition); Assert.AreEqual(size, camera.orthographicSize);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(target); }
        }

        [Test]
        public void RepeatedCaptureKeepsOriginalVisitCamera()
        {
            var root = new GameObject("Camera root");
            try
            {
                var camera = root.AddComponent<Camera>(); var controller = root.AddComponent<ExhibitionCameraController>();
                controller.Configure(camera); camera.transform.localPosition = new Vector3(2, 4, -12); camera.orthographicSize = 6;
                controller.CaptureView(); controller.ResetView(); controller.Apply(); controller.CaptureView(); controller.RestoreView();
                Assert.AreEqual(new Vector3(2, 4, -12), camera.transform.localPosition); Assert.AreEqual(6, camera.orthographicSize);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
