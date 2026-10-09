using MobilePrototype.Collection;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MobilePrototype.Tests.EditMode
{
    /// <summary>
    /// 도감 스크롤의 탭 제스처 분리, 수명 정리와 뷰포트 변경 시 논리 위치 보존을 검증합니다.
    /// </summary>
    public sealed class CollectionScrollRectTests
    {
        private GameObject _root;
        private CollectionScrollRect _scroll;
        private RectTransform _content;
        private PointerEventData _pointer;

        /// <summary>독립 Canvas, 400 높이 뷰포트, 1000 높이 콘텐츠와 왼쪽 포인터를 생성합니다.</summary>
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CollectionScrollTest", typeof(RectTransform), typeof(Canvas));
            _root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(CollectionScrollRect));
            viewportObject.transform.SetParent(_root.transform, false);
            var viewport = viewportObject.GetComponent<RectTransform>();
            viewport.sizeDelta = new Vector2(400, 400);
            _scroll = viewportObject.GetComponent<CollectionScrollRect>();
            _scroll.viewport = viewport;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Unrestricted;
            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewport, false);
            _content = contentObject.GetComponent<RectTransform>();
            _content.anchorMin = _content.anchorMax = new Vector2(.5f, 1);
            _content.pivot = new Vector2(.5f, 1);
            _content.sizeDelta = new Vector2(400, 1000);
            _content.anchoredPosition = Vector2.zero;
            _scroll.content = _content;
            _pointer = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                pressPosition = Vector2.zero,
                position = Vector2.zero
            };
            Canvas.ForceUpdateCanvases();
            _scroll.OnInitializePotentialDrag(_pointer);
        }

        /// <summary>각 테스트의 Canvas와 모든 하위 UI 오브젝트를 즉시 제거합니다.</summary>
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        /// <summary>방향 조회 단계에서는 움직이지 않고 세로 드래그 시작 후에만 콘텐츠가 이동하는지 검증합니다.</summary>
        [Test]
        public void VerticalPreflightDoesNotScrollUntilBeginDrag()
        {
            _pointer.position = new Vector2(0, 20);
            _scroll.OnDrag(_pointer);
            Assert.That(_scroll.OwnsGesture, Is.True);
            Assert.That(_content.anchoredPosition, Is.EqualTo(Vector2.zero));

            _scroll.OnBeginDrag(_pointer);
            _pointer.position = new Vector2(0, 120);
            _scroll.OnDrag(_pointer);
            Assert.That(_content.anchoredPosition.y, Is.GreaterThan(0));
            Assert.That(_content.anchoredPosition.x, Is.Zero);
        }

        /// <summary>가로로 결정된 제스처를 이후 세로 이동이 스크롤로 빼앗지 않는지 검증합니다.</summary>
        [Test]
        public void HorizontalGestureStaysAvailableToNavigationAfterChangingDirection()
        {
            _pointer.position = new Vector2(20, 0);
            _scroll.OnDrag(_pointer);
            _scroll.OnBeginDrag(_pointer);
            _pointer.position = new Vector2(20, 200);
            _scroll.OnDrag(_pointer);
            Assert.That(_scroll.OwnsGesture, Is.False);
            Assert.That(_content.anchoredPosition, Is.EqualTo(Vector2.zero));
        }

        /// <summary>캡처한 세로 드래그가 뷰포트 밖으로 나가도 소유권과 세로 이동을 유지하는지 검증합니다.</summary>
        [Test]
        public void VerticalGestureRetainsOwnershipOutsideViewport()
        {
            _pointer.position = new Vector2(0, 20);
            _scroll.OnDrag(_pointer);
            _scroll.OnBeginDrag(_pointer);
            _pointer.position = new Vector2(1500, 2000);
            _scroll.OnDrag(_pointer);
            Assert.That(_scroll.OwnsGesture, Is.True);
            Assert.That(_content.anchoredPosition.y, Is.GreaterThan(0));
            Assert.That(_content.anchoredPosition.x, Is.Zero);
        }

        /// <summary>작은 이동은 방향을 확정하지 않으며 이후 가로 이동에도 콘텐츠가 움직이지 않는지 검증합니다.</summary>
        [Test]
        public void SmallMovementLeavesOwnershipUndecided()
        {
            _pointer.position = new Vector2(0, 3);
            _scroll.OnDrag(_pointer);
            Assert.That(_scroll.OwnsGesture, Is.False);
            _pointer.position = new Vector2(20, 0);
            _scroll.OnDrag(_pointer);
            Assert.That(_scroll.OwnsGesture, Is.False);
            Assert.That(_content.anchoredPosition, Is.EqualTo(Vector2.zero));
        }

        /// <summary>새 포인터 준비가 이전 방향과 관성을 초기화해 다시 세로 방향을 선택할 수 있는지 검증합니다.</summary>
        [Test]
        public void NewPotentialDragResetsDirectionAndStopsInertia()
        {
            _pointer.position = new Vector2(20, 0);
            _scroll.OnDrag(_pointer);
            _scroll.velocity = new Vector2(0, 120);
            _scroll.OnInitializePotentialDrag(_pointer);
            Assert.That(_scroll.velocity, Is.EqualTo(Vector2.zero));
            _pointer.position = new Vector2(0, 20);
            _scroll.OnDrag(_pointer);
            Assert.That(_scroll.OwnsGesture, Is.True);
        }

        /// <summary>드래그 종료와 오브젝트 비활성화가 각각 제스처 소유권을 해제하는지 검증합니다.</summary>
        [Test]
        public void EndAndDisableReleaseOwnership()
        {
            _pointer.position = new Vector2(0, 20);
            _scroll.OnDrag(_pointer);
            _scroll.OnBeginDrag(_pointer);
            _scroll.OnEndDrag(_pointer);
            Assert.That(_scroll.OwnsGesture, Is.False);

            _scroll.OnInitializePotentialDrag(_pointer);
            _scroll.OnDrag(_pointer);
            Assert.That(_scroll.OwnsGesture, Is.True);
            _root.SetActive(false);
            Assert.That(_scroll.OwnsGesture, Is.False);
        }

        /// <summary>뷰포트가 400에서 600으로 커졌다가 돌아와도 같은 정규화 위치를 유지하는지 검증합니다.</summary>
        /// <param name="position">리사이즈 전후에 유지해야 하는 정규화 세로 위치입니다.</param>
        [TestCase(0f)]
        [TestCase(.35f)]
        public void ViewportResizePreservesLogicalPosition(float position)
        {
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.verticalNormalizedPosition = position;
            TickScroll();
            ResizeViewport(600);
            Assert.That(_scroll.verticalNormalizedPosition, Is.EqualTo(position).Within(.001f));
            ResizeViewport(400);
            Assert.That(_scroll.verticalNormalizedPosition, Is.EqualTo(position).Within(.001f));
        }

        /// <summary>뷰포트가 잠시 콘텐츠 전체를 표시한 뒤 작아져도 이전 논리 위치를 복구하는지 검증합니다.</summary>
        [Test]
        public void ViewportLargerThanContentDoesNotForgetLogicalPosition()
        {
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.verticalNormalizedPosition = .35f;
            TickScroll();
            ResizeViewport(1200);
            TickScroll();
            ResizeViewport(400);
            Assert.That(_scroll.verticalNormalizedPosition, Is.EqualTo(.35f).Within(.001f));
        }

        /// <summary>리사이즈 후 명시적으로 설정한 새 위치가 다음 리사이즈에서도 유지되는지 검증합니다.</summary>
        [Test]
        public void ExplicitPositionAfterResizeReplacesRememberedPosition()
        {
            _scroll.verticalNormalizedPosition = 0;
            TickScroll();
            ResizeViewport(600);
            _scroll.verticalNormalizedPosition = .75f;
            ResizeViewport(400);
            Assert.That(_scroll.verticalNormalizedPosition, Is.EqualTo(.75f).Within(.001f));
        }

        /// <summary>콘텐츠 갱신과 함께 지정한 상단 위치를 이전 페이지의 위치가 덮어쓰지 않는지 검증합니다.</summary>
        [Test]
        public void ContentRefreshUsesItsExplicitPagePosition()
        {
            _scroll.verticalNormalizedPosition = 0;
            TickScroll();
            _content.sizeDelta = new Vector2(400, 1400);
            _scroll.viewport.sizeDelta = new Vector2(400, 600);
            _scroll.verticalNormalizedPosition = 1;
            Canvas.ForceUpdateCanvases();
            TickScroll();
            ResizeViewport(400);
            Assert.That(_scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.001f));
        }

        /// <summary>콘텐츠 자체의 크기가 바뀌면 이전 콘텐츠의 정규화 위치를 강제로 복원하지 않는지 검증합니다.</summary>
        [Test]
        public void ContentSizeChangeDoesNotRestoreThePreviousContentPosition()
        {
            _scroll.verticalNormalizedPosition = .35f;
            TickScroll();
            Vector2 pixelPosition = _content.anchoredPosition;
            _content.sizeDelta = new Vector2(400, 1400);
            ResizeViewport(600);
            Assert.That(_content.anchoredPosition, Is.EqualTo(pixelPosition));
            Assert.That(_scroll.verticalNormalizedPosition, Is.Not.EqualTo(.35f).Within(.001f));
        }

        /// <summary>뷰포트 높이를 바꾸고 Canvas 레이아웃과 한 번의 스크롤 갱신을 실행합니다.</summary>
        /// <param name="height">적용할 새 뷰포트 높이입니다.</param>
        private void ResizeViewport(float height)
        {
            _scroll.viewport.sizeDelta = new Vector2(400, height);
            Canvas.ForceUpdateCanvases();
            TickScroll();
        }

        /// <summary>Editor 프레임 타이밍에 의존하지 않고 보호된 LateUpdate를 직접 호출해 스크롤 한 프레임을 실행합니다.</summary>
        private void TickScroll()
        {
            typeof(CollectionScrollRect).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_scroll, null);
        }
    }
}
