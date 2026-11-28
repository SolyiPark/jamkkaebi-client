using System.Collections.Generic;
using System.Reflection;
using Jamkkaebi.Scripts.UI.Exhibition;
using Jamkkaebi.Scripts.UI.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>손을 떼지 않고 끊긴 입력을 누름 종료와 구분해 알리는지 확인한다.</summary>
    public sealed class MirrorInputCancelTests
    {
        private sealed class Recorder : MonoBehaviour, IMirrorCancelHandler, IPointerUpHandler
        {
            public readonly List<string> Events = new List<string>();
            public void OnMirrorCancel() => Events.Add("cancel");
            public void OnPointerUp(PointerEventData eventData) => Events.Add("up");
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [Test]
        public void CancelTellsThePressedObjectBeforeEndingThePress()
        {
            var bridge = new GameObject("Bridge"); var target = new GameObject("Pressed");
            try
            {
                var input = bridge.AddComponent<MirrorInput>();
                var recorder = target.AddComponent<Recorder>();
                SetField(input, "forwarded", new PointerEventData(null) { eligibleForClick = true });
                SetField(input, "pressed", target);

                input.Cancel();

                Assert.That(recorder.Events, Does.Contain("cancel"));
                Assert.That(recorder.Events[0], Is.EqualTo("cancel"), "취소 알림이 누름 종료보다 먼저 와야 한다");
                Assert.That(recorder.Events.FindAll(name => name == "cancel"), Has.Count.EqualTo(1));

                // 누름이 없을 때의 취소는 아무것도 알리지 않는다.
                recorder.Events.Clear();
                input.Cancel();
                Assert.That(recorder.Events, Is.Empty);
            }
            finally { Object.DestroyImmediate(bridge); Object.DestroyImmediate(target); }
        }

        [Test]
        public void HousingInputTreatsCancelAsItsOwnSignal()
        {
            // 전시관 입력은 취소 알림을 받는다. 받지 않으면 끌던 도깨비가 마지막 손가락 위치에 놓인다.
            Assert.That(typeof(IMirrorCancelHandler).IsAssignableFrom(typeof(HousingSurfaceInput)), Is.True);
            var root = new GameObject("Input");
            try
            {
                var input = root.AddComponent<HousingSurfaceInput>();
                // 하우징 연결 전이나 누름이 없을 때 불려도 예외가 없어야 한다.
                Assert.DoesNotThrow(() => input.OnMirrorCancel());
                Assert.That(input.OwnsGesture, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
