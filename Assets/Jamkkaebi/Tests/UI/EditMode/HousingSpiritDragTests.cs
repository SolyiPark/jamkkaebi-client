using System.Reflection;
using Jamkkaebi.Scripts.UI.Exhibition;
using Jamkkaebi.Scripts.UI.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>
    /// 하우징을 실제로 세워 도깨비 끌기의 시작·놓기·취소를 확인한다.
    /// 놓는 곳에 따라 자리·예약·정지 상태가 어떻게 되는지, 입력이 끊겼을 때 놓지 않고 되돌리는지 본다.
    /// </summary>
    public sealed class HousingSpiritDragTests
    {
        private static readonly Vector2Int Start = new Vector2Int(1, 1);
        private static readonly Vector2Int OtherSpirit = new Vector2Int(5, 5);
        private static readonly Vector2Int Item = new Vector2Int(3, 3);
        private static readonly Vector2Int Empty = new Vector2Int(2, 4);

        private GameObject _root;
        private ExhibitionGrid _grid;
        private ExhibitionSurface _surface;
        private ExhibitionHousing _housing;
        private Camera _camera;
        private DokkaebiWanderer _spirit;
        private DokkaebiWanderer _other;

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [SetUp]
        public void SetUp()
        {
            _grid = ScriptableObject.CreateInstance<ExhibitionGrid>();
            _root = new GameObject("Home");
            var cameraObject = new GameObject("Camera"); cameraObject.transform.SetParent(_root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0, -10);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true; _camera.orthographicSize = 5;
            _root.AddComponent<MirrorSceneRoot>().sceneCamera = _camera;

            var surfaceObject = new GameObject("Surface"); surfaceObject.transform.SetParent(_root.transform, false);
            _surface = surfaceObject.AddComponent<ExhibitionSurface>();
            SetField(_surface, "_grid", _grid);
            var content = new GameObject("Content"); content.transform.SetParent(surfaceObject.transform, false);

            _housing = _root.AddComponent<ExhibitionHousing>();
            SetField(_housing, "_surface", _surface);
            SetField(_housing, "_content", content.transform);
            SetField(_housing, "_spawnInitialItem", false);
            // 편집 모드에서는 Unity가 Start를 부르지 않는다.
            typeof(ExhibitionHousing).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_housing, null);

            Assert.That(_housing.TryAddSpirit(new ExhibitionPlaceable(ExhibitionPlaceable.SpiritType, 2, "하나"), Start), Is.True);
            Assert.That(_housing.TryAddSpirit(new ExhibitionPlaceable(ExhibitionPlaceable.SpiritType, 8, "둘"), OtherSpirit), Is.True);
            Assert.That(_housing.Layout.TryPlace("item", Item, Vector2Int.one, out _), Is.True);
            _spirit = _housing.FindWanderer(2); _other = _housing.FindWanderer(8);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_grid);
        }

        /// <summary>격자 칸의 중심이 화면에 찍히는 좌표. 하우징이 받는 RT 좌표와 같은 공간이다.</summary>
        private Vector2 PointOf(Vector2Int cell) => _camera.WorldToScreenPoint(_surface.CellWorldPosition(cell));

        private static void AssertNear(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.001f), $"{actual} != {expected}");

        [Test]
        public void DragPausesAndFollowsThePointerWithoutChangingTheCell()
        {
            Assert.That(_housing.BeginSpiritDrag(_spirit.WalkerId, PointOf(Start)), Is.True);
            Assert.That(_housing.IsDraggingSpirit, Is.True);
            Assert.That(_spirit.Paused, Is.True);

            _housing.DragSpiritTo(PointOf(Empty));
            AssertNear(_spirit.transform.position, _surface.CellWorldPosition(Empty));
            Assert.That(_spirit.PlacementCell, Is.EqualTo(Start), "놓기 전에는 자리가 바뀌지 않는다");
            Assert.That(_housing.Layout.CanPlace(Start, Vector2Int.one), Is.False, "놓기 전에는 원래 칸 예약이 남는다");
            Assert.That(_housing.Layout.CanPlace(Empty, Vector2Int.one), Is.True);
        }

        [Test]
        public void DropOnEmptyCellMovesTheSpiritAndResumesWandering()
        {
            _housing.BeginSpiritDrag(_spirit.WalkerId, PointOf(Start));
            _housing.DragSpiritTo(PointOf(Empty));

            Assert.That(_housing.EndSpiritDrag(PointOf(Empty)), Is.True);
            Assert.That(_housing.IsDraggingSpirit, Is.False);
            Assert.That(_spirit.Paused, Is.False);
            Assert.That(_spirit.PlacementCell, Is.EqualTo(Empty));
            Assert.That(_spirit.CurrentCell, Is.EqualTo(Empty));
            Assert.That(_spirit.TargetCell, Is.EqualTo(Empty));
            AssertNear(_spirit.transform.position, _surface.CellWorldPosition(Empty));
            Assert.That(_housing.Layout.CanPlace(Start, Vector2Int.one), Is.True, "떠난 칸은 풀린다");
            Assert.That(_housing.Layout.CanPlace(Empty, Vector2Int.one), Is.False, "새 칸을 예약한다");
        }

        [Test]
        public void DropOnBlockedOrOutsidePlaceReturnsTheSpirit()
        {
            Vector3 origin = _spirit.transform.position;
            var outside = new Vector2(-5000, -5000);
            foreach (var drop in new[] { PointOf(Item), PointOf(OtherSpirit), outside })
            {
                Assert.That(_housing.BeginSpiritDrag(_spirit.WalkerId, PointOf(Start)), Is.True);
                _housing.DragSpiritTo(drop);

                Assert.That(_housing.EndSpiritDrag(drop), Is.False, "막힌 칸과 상판 밖에는 놓이지 않는다");
                Assert.That(_housing.IsDraggingSpirit, Is.False);
                Assert.That(_spirit.Paused, Is.False);
                Assert.That(_spirit.PlacementCell, Is.EqualTo(Start));
                AssertNear(_spirit.transform.position, origin);
                Assert.That(_housing.Layout.CanPlace(Start, Vector2Int.one), Is.False);
            }
            Assert.That(_other.PlacementCell, Is.EqualTo(OtherSpirit), "다른 도깨비는 영향을 받지 않는다");
            Assert.That(_housing.Layout.At(Item), Is.Not.Null);
        }

        [Test]
        public void CancelReturnsTheSpiritAndIgnoresUnknownIds()
        {
            Vector3 origin = _spirit.transform.position;
            Assert.That(_housing.BeginSpiritDrag(999, PointOf(Start)), Is.False, "도깨비가 아니면 끌 수 없다");
            Assert.That(_housing.IsDraggingSpirit, Is.False);

            _housing.BeginSpiritDrag(_spirit.WalkerId, PointOf(Start));
            _housing.DragSpiritTo(PointOf(Empty));
            _housing.CancelSpiritDrag();
            Assert.That(_housing.IsDraggingSpirit, Is.False);
            Assert.That(_spirit.Paused, Is.False);
            Assert.That(_spirit.PlacementCell, Is.EqualTo(Start));
            AssertNear(_spirit.transform.position, origin);
            // 끄는 중이 아닐 때의 놓기와 취소는 아무것도 바꾸지 않는다.
            Assert.That(_housing.EndSpiritDrag(PointOf(Empty)), Is.False);
            _housing.CancelSpiritDrag();
            Assert.That(_spirit.PlacementCell, Is.EqualTo(Start));
        }

        [Test]
        public void StartingAnotherDragReturnsTheFirstSpirit()
        {
            Vector3 origin = _spirit.transform.position;
            _housing.BeginSpiritDrag(_spirit.WalkerId, PointOf(Start));
            _housing.DragSpiritTo(PointOf(Empty));

            Assert.That(_housing.BeginSpiritDrag(_other.WalkerId, PointOf(OtherSpirit)), Is.True);
            Assert.That(_spirit.Paused, Is.False, "먼저 끌던 도깨비가 멈춘 채 남으면 안 된다");
            AssertNear(_spirit.transform.position, origin);
            Assert.That(_other.Paused, Is.True);
            _housing.CancelSpiritDrag();
            Assert.That(_other.Paused, Is.False);
        }

        [Test]
        public void ReleaseThroughInputDropsTheSpirit()
        {
            var input = _root.GetComponent<HousingSurfaceInput>();
            _housing.BeginSpiritDrag(_spirit.WalkerId, PointOf(Start));
            SetField(input, "_dragging", true);

            input.OnPointerUp(new PointerEventData(null) { position = PointOf(Empty) });

            Assert.That(_spirit.PlacementCell, Is.EqualTo(Empty), "실제로 손을 떼면 그 칸에 놓인다");
            Assert.That(_spirit.Paused, Is.False);
        }

        [Test]
        public void NewPressWithoutReleaseReturnsTheDraggedSpirit()
        {
            Vector3 origin = _spirit.transform.position;
            var input = _root.GetComponent<HousingSurfaceInput>();
            _housing.BeginSpiritDrag(_spirit.WalkerId, PointOf(Start));
            _housing.DragSpiritTo(PointOf(Empty));
            SetField(input, "_dragging", true);

            // 손을 뗐다는 신호 없이 새 누름이 오면 앞선 끌기를 놓지 않고 되돌린다.
            input.OnPointerDown(new PointerEventData(null) { position = PointOf(Empty) });

            Assert.That(_housing.IsDraggingSpirit, Is.False);
            Assert.That(_spirit.Paused, Is.False);
            Assert.That(_spirit.PlacementCell, Is.EqualTo(Start));
            AssertNear(_spirit.transform.position, origin);
            // 이어지는 누름 종료도 자리를 바꾸지 않는다.
            input.OnPointerUp(new PointerEventData(null) { position = PointOf(Empty) });
            Assert.That(_spirit.PlacementCell, Is.EqualTo(Start));
        }

        [Test]
        public void InputCancelBeforeReleaseDoesNotCommitTheDrop()
        {
            Vector3 origin = _spirit.transform.position;
            var input = _root.GetComponent<HousingSurfaceInput>();
            _housing.BeginSpiritDrag(_spirit.WalkerId, PointOf(Start));
            _housing.DragSpiritTo(PointOf(Empty));
            SetField(input, "_dragging", true);

            // 셸의 입력 전달이 끊길 때의 순서 그대로: 취소 알림 뒤에 누름 종료가 온다.
            var mirror = new GameObject("Mirror");
            try
            {
                var bridge = mirror.AddComponent<MirrorInput>();
                var forwarded = new PointerEventData(null) { position = PointOf(Empty), eligibleForClick = true };
                SetField(bridge, "forwarded", forwarded);
                SetField(bridge, "pressed", _root);
                bridge.Cancel();
                // 편집 모드에서는 이벤트 전달이 생략될 수 있어 누름 종료를 직접 한 번 더 보낸다.
                input.OnPointerUp(forwarded);
            }
            finally { Object.DestroyImmediate(mirror); }

            Assert.That(_housing.IsDraggingSpirit, Is.False);
            Assert.That(_spirit.PlacementCell, Is.EqualTo(Start), "취소된 끌기는 마지막 손가락 위치에 놓이지 않는다");
            AssertNear(_spirit.transform.position, origin);
            Assert.That(_spirit.Paused, Is.False, "되돌린 뒤 다시 걷는다");
            Assert.That(_housing.Layout.CanPlace(Empty, Vector2Int.one), Is.True);
        }
    }
}
