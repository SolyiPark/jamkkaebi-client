using MobilePrototype.Collection;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MobilePrototype.Tests.EditMode
{
    public sealed class CollectionScrollRectTests
    {
        private GameObject _root;
        private CollectionScrollRect _scroll;
        private RectTransform _content;
        private PointerEventData _pointer;

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

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

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

        private void ResizeViewport(float height)
        {
            _scroll.viewport.sizeDelta = new Vector2(400, height);
            Canvas.ForceUpdateCanvases();
            TickScroll();
        }

        private void TickScroll()
        {
            typeof(CollectionScrollRect).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_scroll, null);
        }
    }
}
