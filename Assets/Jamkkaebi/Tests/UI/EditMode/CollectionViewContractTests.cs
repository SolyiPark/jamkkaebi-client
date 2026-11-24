using System.Reflection;
using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.UI.Collection;
using Jamkkaebi.Scripts.UI.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>실제 디자인 대신 기록용 View를 연결해도 Presenter가 기능 규칙을 유지하는지 검사합니다.</summary>
    public sealed class CollectionViewContractTests
    {
        private GameObject _root;
        private CollectionCatalog _catalog;
        private CollectionPresenter _presenter;
        private CollectionRecordingView _view;
        private SharedRecords _shared;
        private CollectionCatalog _otherCatalog;

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
            // EditMode의 단위 검사이므로 준비한 참조를 넣은 뒤 초기화 함수(Awake)를 각 검사에서 직접 호출합니다.
            // 공유 기록 유무를 검사마다 다르게 준비해야 하므로 SetUp에서는 부르지 않습니다.
            // 실제 Play Mode의 씬 로드·입력·렌더링까지 검증했다는 의미는 아닙니다. 그 부분은 Player 검사로 확인합니다.
        }

        [TearDown]
        public void TearDown()
        {
            // EditMode에서 직접 부른 Awake는 OnDestroy가 따라 불리지 않으므로 공유 기록을 직접 비웁니다.
            if (_shared) Object.DestroyImmediate(_shared.gameObject);
            SetCurrentSharedRecords(null);
            if (_otherCatalog) Object.DestroyImmediate(_otherCatalog);
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_catalog);
        }

        /// <summary>화면 모양이 없어도 요청이 잠금 검사·신규 확인·뒤로가기를 거쳐 처리되는지 확인합니다.</summary>
        [Test]
        public void ReplacementViewRoutesInputThroughPresenterLockAndUnreadRules()
        {
            InvokeLifecycle("Awake");
            var progress = new CollectionProgress(_catalog);
            progress.RegisterRestorationCompleted("samguk-frog-figurine");
            _presenter.BindProgress(progress);
            Assert.That(_root.GetComponent<CollectionDefaultView>(), Is.Null);
            Assert.That(_view.LastData.Page, Is.EqualTo(CollectionPage.Overview));

            _view.SelectEra("samguk");
            _view.SelectRelic("samguk-turtle-water-dropper");
            Assert.That(_presenter.Page, Is.EqualTo(CollectionPage.Era));
            Assert.That(_presenter.Progress.RestoredCount, Is.EqualTo(1));

            _view.SelectRelic("samguk-frog-figurine");
            Assert.That(_view.LastData.Page, Is.EqualTo(CollectionPage.Relic));
            Assert.That(_view.LastData.SelectedRelicId, Is.EqualTo("samguk-frog-figurine"));
            Assert.That(_presenter.Progress.IsUnread("samguk-frog-figurine"), Is.False);
            _view.Back();
            _view.Back();
            Assert.That(_presenter.Page, Is.EqualTo(CollectionPage.Overview));
        }

        /// <summary>디자인에 ScrollRect가 없다는 이유로 기록이나 외형 갱신이 중단되지 않는지 확인합니다.</summary>
        [Test]
        public void ProgressAndAppearanceRefreshWorkWithAViewWithoutScrollRect()
        {
            InvokeLifecycle("Awake");
            Assert.That(_presenter.Scroll, Is.Null);
            var progress = new CollectionProgress(_catalog);
            _presenter.BindProgress(progress);
            InvokeLifecycle("LateUpdate");
            Assert.That(_view.LastData.RestoredCount, Is.Zero);

            _presenter.BindAppearance(null);
            Assert.That(_view.LastData.Appearance, Is.Null);
            Assert.That(_view.RenderCount, Is.EqualTo(3));
        }

        /// <summary>셸 없이 열리면(공유 기록 없음) 오류 없이 빈 기록으로 시작하는지 검사합니다.</summary>
        [Test]
        public void WithoutSharedRecords_StartsWithEmptyProgress()
        {
            InvokeLifecycle("Awake");

            Assert.That(_presenter.Progress, Is.Not.Null);
            Assert.That(_presenter.Progress.Catalog, Is.SameAs(_catalog));
            Assert.That(_presenter.Progress.RestoredCount, Is.Zero);
            Assert.That(_view.LastData.RestoredCount, Is.Zero);
        }

        /// <summary>셸의 공유 기록이 같은 카탈로그를 쓰면 새로 만들지 않고 그 기록을 그대로 보는지 검사합니다.</summary>
        [Test]
        public void WithSharedRecords_UsesSharedProgress()
        {
            CreateSharedRecords(_catalog);
            _shared.Collection.RegisterRestorationCompleted("samguk-frog-figurine");

            InvokeLifecycle("Awake");

            Assert.That(_presenter.Progress, Is.SameAs(_shared.Collection));
            Assert.That(_view.LastData.RestoredCount, Is.EqualTo(1));
        }

        /// <summary>
        /// 셸의 공유 기록이 다른 카탈로그 인스턴스를 쓰면 오류를 남기고, 그 기록을 쓰지 않고 빈 기록으로 시작하는지 검사합니다.
        /// (다른 카탈로그의 기록을 받으면 BindProgress처럼 같은 카탈로그 규칙이 깨진다)
        /// </summary>
        [Test]
        public void WithSharedRecordsOfOtherCatalog_LogsErrorAndStartsEmpty()
        {
            _otherCatalog = CollectionCatalogData.CreateCatalog();
            CreateSharedRecords(_otherCatalog);
            _shared.Collection.RegisterRestorationCompleted("samguk-frog-figurine");

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("카탈로그"));
            InvokeLifecycle("Awake");

            Assert.That(_presenter.Progress, Is.Not.SameAs(_shared.Collection));
            Assert.That(_presenter.Progress.Catalog, Is.SameAs(_catalog));
            Assert.That(_presenter.Progress.RestoredCount, Is.Zero);
        }

        /// <summary>셸의 공유 기록을 만들고 Awake를 직접 불러 Current로 등록합니다.</summary>
        private void CreateSharedRecords(CollectionCatalog catalog)
        {
            var shell = new GameObject("Shared Records");
            shell.SetActive(false);
            _shared = shell.AddComponent<SharedRecords>();
            typeof(SharedRecords).GetField("_collectionCatalog", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_shared, catalog);
            typeof(SharedRecords).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_shared, null);
            Assert.That(SharedRecords.Current, Is.SameAs(_shared));
        }

        private static void SetCurrentSharedRecords(SharedRecords value) => typeof(SharedRecords)
            .GetProperty(nameof(SharedRecords.Current), BindingFlags.Static | BindingFlags.Public)
            .SetValue(null, value);

        private void SetField(string name, object value) => typeof(CollectionPresenter)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_presenter, value);

        private void InvokeLifecycle(string name) => typeof(CollectionPresenter)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_presenter, null);
    }
}
