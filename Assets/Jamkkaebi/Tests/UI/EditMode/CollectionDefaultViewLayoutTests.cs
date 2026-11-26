using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.UI.Collection;
using Jamkkaebi.Scripts.UI.Shell;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>
    /// 시트에서 받은 긴 본문이 기본 도감 View에서 다음 요소와 겹치지 않고 스크롤 범위 안에 들어가는지 검사합니다.
    /// 배치 좌표만 확인하며 실제 렌더링 결과(글꼴 모양)는 에디터 Play로 확인합니다.
    /// </summary>
    public sealed class CollectionDefaultViewLayoutTests
    {
        private const string RelicId = "samguk-turtle-water-dropper";
        private const string EraId = "samguk";

        private GameObject _root;
        private CollectionCatalog _catalog;
        private CollectionDefaultView _view;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Collection layout root", typeof(Camera));
            MirrorSceneRoot sceneRoot = _root.AddComponent<MirrorSceneRoot>();
            sceneRoot.sceneCamera = _root.GetComponent<Camera>();
            _view = _root.AddComponent<CollectionDefaultView>();
            _view.Initialize(sceneRoot, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            if (_catalog) Object.DestroyImmediate(_catalog);
        }

        private static string Lines(int count) => string.Join("\n", Enumerable.Repeat("긴 본문 한 줄", count));

        /// <summary>구조 카탈로그에서 첫 시대 설명과 첫 유물의 설명·복원 기록만 바꾼 카탈로그로 화면을 그립니다.</summary>
        private void Render(CollectionPage page, string eraDescription, string description, string note)
        {
            CollectionCatalog structure = CollectionCatalogData.CreateCatalog();
            try
            {
                _catalog = ScriptableObject.CreateInstance<CollectionCatalog>();
                _catalog.Configure(
                    structure.Eras.Select(era => era.Id != EraId ? era : new CollectionEraDefinition(era.Id, era.Name,
                        "기간", eraDescription, era.BuildingName, era.Accent)),
                    structure.Relics.Select(relic => relic.Id != RelicId ? relic : new CollectionRelicDefinition(relic.Id,
                        relic.EraId, relic.Name, relic.SpiritName, description, note, relic.Accent)));
            }
            finally
            {
                Object.DestroyImmediate(structure);
            }

            CollectionProgress progress = new CollectionProgress(_catalog);
            progress.RegisterRestorationCompleted(RelicId);
            _view.Render(new CollectionViewData(progress, null, page, EraId, page == CollectionPage.Relic ? RelicId : null), 1f);
        }

        private RectTransform Find(string name)
        {
            Transform found = _view.Scroll.content.Find(name);
            Assert.That(found, Is.Not.Null, name);
            return (RectTransform)found;
        }

        // TopRect는 위쪽 기준이므로 offsetMax.y = -top, offsetMin.y = -bottom입니다.
        private static float Top(RectTransform rect) => -rect.offsetMax.y;
        private static float Bottom(RectTransform rect) => -rect.offsetMin.y;

        /// <summary>짧은 본문은 기존 디자인 좌표를 그대로 쓰는지 검사합니다.</summary>
        [Test]
        public void ShortRelicText_KeepsDesignPositions()
        {
            Render(CollectionPage.Relic, "설명", "짧은 설명", "짧은 기록");

            Assert.That(Bottom(Find("Description")), Is.EqualTo(1285).Within(.5f));
            Assert.That(Top(Find("Conservation")), Is.EqualTo(1445).Within(.5f));
            Assert.That(_view.Scroll.content.sizeDelta.y, Is.EqualTo(2020).Within(.5f));
        }

        /// <summary>긴 유물 설명·복원 기록이 아래 요소를 밀어내고 스크롤 높이도 함께 늘리는지 검사합니다.</summary>
        [Test]
        public void LongRelicText_PushesFollowingElementsAndScrollHeight()
        {
            Render(CollectionPage.Relic, "설명", Lines(30), Lines(30));

            RectTransform description = Find("Description");
            RectTransform conservation = Find("Conservation");
            RectTransform notice = Find("Draft Notice");
            Assert.That(Bottom(description), Is.GreaterThan(1285));
            Assert.That(Top(Find("Description Rule")), Is.GreaterThanOrEqualTo(Bottom(description)));
            Assert.That(Top(Find("Conservation Heading")), Is.GreaterThan(Bottom(description)));
            Assert.That(Bottom(conservation), Is.GreaterThan(Top(conservation) + 330));
            Assert.That(Top(notice), Is.GreaterThanOrEqualTo(Bottom(conservation)));
            Assert.That(_view.Scroll.content.sizeDelta.y, Is.GreaterThanOrEqualTo(Bottom(notice)));
        }

        /// <summary>긴 시대 설명이 아래 안내 문구를 밀어내고 스크롤 높이도 함께 늘리는지 검사합니다.</summary>
        [Test]
        public void LongEraDescription_PushesNoticeAndScrollHeight()
        {
            Render(CollectionPage.Era, Lines(30), "설명", "기록");

            RectTransform context = Find("Era Context");
            RectTransform notice = Find("Sample Notice");
            Assert.That(Top(notice), Is.GreaterThanOrEqualTo(Bottom(context)));
            Assert.That(_view.Scroll.content.sizeDelta.y, Is.GreaterThanOrEqualTo(Bottom(notice)));
        }
    }
}
