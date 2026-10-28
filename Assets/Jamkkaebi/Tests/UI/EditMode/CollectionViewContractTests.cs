using System.Reflection;
using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.UI.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>실제 디자인 대신 기록용 View를 연결해도 Presenter가 기능 규칙을 유지하는지 검사합니다.</summary>
    public sealed class CollectionViewContractTests
    {
        private GameObject _root;
        private CollectionCatalog _catalog;
        private CollectionPresenter _presenter;
        private CollectionRecordingView _view;

        [SetUp]
        public void SetUp()
        {
            _catalog = CollectionCatalogData.CreateCatalog();
            _root = new GameObject("Collection contract root");
            _root.SetActive(false);
            _view = _root.AddComponent<CollectionRecordingView>();
            _presenter = _root.AddComponent<CollectionPresenter>();
            SetField("_catalog", _catalog);
            SetField("_font", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            SetField("_view", _view);
            // EditMode의 단위 검사이므로 준비한 참조를 넣은 뒤 초기화 함수를 직접 호출합니다.
            // 실제 Play Mode의 씬 로드·입력·렌더링까지 검증했다는 의미는 아닙니다. 그 부분은 Player 검사로 확인합니다.
            InvokeLifecycle("Awake");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_catalog);
        }

        /// <summary>화면 모양이 없어도 요청이 잠금 검사·신규 확인·뒤로가기를 거쳐 처리되는지 확인합니다.</summary>
        [Test]
        public void ReplacementViewRoutesInputThroughPresenterLockAndUnreadRules()
        {
            Assert.That(_root.GetComponent<CollectionDefaultView>(), Is.Null);
            Assert.That(_view.LastData.Page, Is.EqualTo(CollectionPage.Overview));

            _view.SelectEra("samguk");
            _view.SelectRelic("samguk-standing-buddha");
            Assert.That(_presenter.Page, Is.EqualTo(CollectionPage.Era));
            Assert.That(_presenter.Progress.RestoredCount, Is.EqualTo(5));

            _view.SelectRelic("samguk-crown");
            Assert.That(_view.LastData.Page, Is.EqualTo(CollectionPage.Relic));
            Assert.That(_view.LastData.SelectedRelicId, Is.EqualTo("samguk-crown"));
            Assert.That(_presenter.Progress.IsUnread("samguk-crown"), Is.False);
            _view.Back();
            _view.Back();
            Assert.That(_presenter.Page, Is.EqualTo(CollectionPage.Overview));
        }

        /// <summary>디자인에 ScrollRect가 없다는 이유로 기록이나 외형 갱신이 중단되지 않는지 확인합니다.</summary>
        [Test]
        public void ProgressAndAppearanceRefreshWorkWithAViewWithoutScrollRect()
        {
            Assert.That(_presenter.Scroll, Is.Null);
            var progress = new CollectionProgress(_catalog);
            _presenter.BindProgress(progress);
            InvokeLifecycle("LateUpdate");
            Assert.That(_view.LastData.RestoredCount, Is.Zero);

            _presenter.BindAppearance(null);
            Assert.That(_view.LastData.Appearance, Is.Null);
            Assert.That(_view.RenderCount, Is.EqualTo(3));
        }

        private void SetField(string name, object value) => typeof(CollectionPresenter)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_presenter, value);

        private void InvokeLifecycle(string name) => typeof(CollectionPresenter)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_presenter, null);
    }
}
