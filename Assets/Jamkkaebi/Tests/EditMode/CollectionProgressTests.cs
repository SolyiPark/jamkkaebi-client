using System;
using System.Reflection;
using Jamkkaebi.Scripts.Gameplay.Collection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Jamkkaebi.Tests.EditMode
{
    public class CollectionProgressTests
    {
        private CollectionCatalog _catalog;
        private CollectionProgress _progress;

        /// <summary>시대 3개와 유물 3종으로 각 테스트에 사용할 빈 진행 기록을 구성합니다.</summary>
        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<CollectionCatalog>();
            _catalog.Configure(
                new[] { Era("era-a"), Era("era-b"), Era("empty-era") },
                new[] { Relic("relic-a1", "era-a"), Relic("relic-a2", "era-a"), Relic("relic-b1", "era-b") });
            _progress = new CollectionProgress(_catalog);
        }

        /// <summary>테스트에서 만든 메모리 카탈로그가 남아 있으면 파괴합니다.</summary>
        [TearDown]
        public void TearDown()
        {
            if (_catalog)
                UnityEngine.Object.DestroyImmediate(_catalog);
        }

        /// <summary>빈 기록은 유물만 전체 개수에 포함하고 빈 시대와 미완료 시대의 건물을 잠그는지 검사합니다.</summary>
        [Test]
        public void InitialRecord_OnlyRelicsContributeToTotalAndBuildingsStayLocked()
        {
            Assert.AreEqual(3, _progress.TotalCount);
            Assert.AreEqual(0, _progress.RestoredCount);
            Assert.AreEqual(0f, _progress.CollectionRate);
            Assert.AreEqual(0, _progress.CompletedBuildingCount);
            Assert.IsFalse(_progress.IsBuildingUnlocked("empty-era"));
            Assert.IsFalse(_progress.IsBuildingUnlocked("missing-era"));
            Assert.IsFalse(_progress.HasUnreadInEra("era-a"));
        }

        /// <summary>동일 종류의 중복 완료가 수집 개수·수집률·변경 알림을 한 번만 갱신하는지 검사합니다.</summary>
        [Test]
        public void RestorationCompleted_RepeatedNotificationCountsTypeOnlyOnce()
        {
            int notifications = 0;
            _progress.Changed += () => notifications++;

            Assert.IsTrue(_progress.RegisterRestorationCompleted("relic-a1"));
            Assert.IsFalse(_progress.RegisterRestorationCompleted("relic-a1"));

            Assert.AreEqual(1, _progress.RestoredCount);
            Assert.AreEqual(1f / 3f, _progress.CollectionRate, 0.0001f);
            Assert.AreEqual(1, notifications);
            Assert.IsTrue(_progress.IsRestored("relic-a1"));
            Assert.IsTrue(_progress.IsUnread("relic-a1"));
            Assert.IsFalse(_progress.IsBuildingUnlocked("era-a"));
        }

        /// <summary>유물 종류로 등록할 수 없는 ID가 기록과 신규 상태, 변경 알림을 만들지 않는지 검사합니다.</summary>
        /// <param name="relicId">null·빈 문자열·미등록 ID·시대 ID 중 하나인 거절 대상입니다.</param>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("missing-relic")]
        [TestCase("era-a")]
        public void UnknownRestoration_DoesNotChangeCountsOrNotify(string relicId)
        {
            int notifications = 0;
            _progress.Changed += () => notifications++;

            Assert.IsFalse(_progress.RegisterRestorationCompleted(relicId));
            Assert.IsFalse(_progress.MarkViewed(relicId));
            Assert.IsFalse(_progress.IsRestored(relicId));
            Assert.IsFalse(_progress.IsUnread(relicId));
            Assert.AreEqual(0, _progress.RestoredCount);
            Assert.AreEqual(0, notifications);
        }

        /// <summary>상세 확인은 신규 표시만 해제하고 중복 완료 후에도 복원 기록을 유지하는지 검사합니다.</summary>
        [Test]
        public void ViewingDetail_ClearsNewBadgeAndRetainsPermanentRestoration()
        {
            _progress.RegisterRestorationCompleted("relic-a1");
            int notifications = 0;
            _progress.Changed += () => notifications++;

            Assert.IsTrue(_progress.MarkViewed("relic-a1"));
            Assert.IsFalse(_progress.MarkViewed("relic-a1"));
            Assert.IsFalse(_progress.RegisterRestorationCompleted("relic-a1"));

            Assert.IsTrue(_progress.IsRestored("relic-a1"));
            Assert.IsFalse(_progress.IsUnread("relic-a1"));
            Assert.IsFalse(_progress.HasUnreadInEra("era-a"));
            Assert.AreEqual(1, _progress.RestoredCount);
            Assert.AreEqual(1, notifications);
        }

        /// <summary>시대의 마지막 유물 복원으로 해당 건물만 해금하고 건물을 수집률에서 제외하는지 검사합니다.</summary>
        [Test]
        public void LastRelicInEra_UnlocksOnlyThatEraBuilding()
        {
            _progress.RegisterRestorationCompleted("relic-a1");
            _progress.RegisterRestorationCompleted("relic-b1");
            Assert.AreEqual(1, _progress.CompletedBuildingCount);
            Assert.IsFalse(_progress.IsBuildingUnlocked("era-a"));
            Assert.IsTrue(_progress.IsBuildingUnlocked("era-b"));

            _progress.RegisterRestorationCompleted("relic-a2");

            Assert.AreEqual(2, _progress.RestoredInEra("era-a"));
            Assert.AreEqual(2, _progress.TotalInEra("era-a"));
            Assert.AreEqual(2, _progress.CompletedBuildingCount);
            Assert.AreEqual(3, _progress.TotalCount);
            Assert.AreEqual(1f, _progress.CollectionRate);
            Assert.IsTrue(_progress.IsBuildingUnlocked("era-a"));
            Assert.IsFalse(_progress.IsBuildingUnlocked("empty-era"));
        }

        /// <summary>모든 신규 유물 상세를 확인할 때까지 시대 신규 표시가 남는지 검사합니다.</summary>
        [Test]
        public void NewBadgeInEra_RemainsUntilEveryNewRelicHasBeenViewed()
        {
            _progress.RegisterRestorationCompleted("relic-a1");
            _progress.RegisterRestorationCompleted("relic-a2");
            _progress.MarkViewed("relic-a1");

            Assert.IsTrue(_progress.HasUnreadInEra("era-a"));
            Assert.IsFalse(_progress.HasUnreadInEra("era-b"));

            _progress.MarkViewed("relic-a2");
            Assert.IsFalse(_progress.HasUnreadInEra("era-a"));
            Assert.IsTrue(_progress.IsBuildingUnlocked("era-a"));
        }

        /// <summary>유물과 시대가 없는 유효한 카탈로그의 수집률과 해금 건물 수가 0인지 검사합니다.</summary>
        [Test]
        public void EmptyCatalog_HasZeroRateAndNoCompletedBuildings()
        {
            _catalog.Configure(Array.Empty<CollectionEraDefinition>(), Array.Empty<CollectionRelicDefinition>());
            CollectionProgress emptyProgress = new CollectionProgress(_catalog);

            Assert.AreEqual(0, emptyProgress.TotalCount);
            Assert.AreEqual(0f, emptyProgress.CollectionRate);
            Assert.AreEqual(0, emptyProgress.CompletedBuildingCount);
        }

        /// <summary>중복 ID와 없는 시대 참조로 구성을 요청해도 기존 유효한 카탈로그가 보존되는지 검사합니다.</summary>
        [Test]
        public void InvalidCatalog_DuplicateIdsOrUnknownEraCannotReplaceValidConfiguration()
        {
            Assert.Throws<ArgumentException>(() => _catalog.Configure(
                new[] { Era("same"), Era("same") }, Array.Empty<CollectionRelicDefinition>()));
            Assert.Throws<ArgumentException>(() => _catalog.Configure(
                new[] { Era("era-a") }, new[] { Relic("same", "era-a"), Relic("same", "era-a") }));
            Assert.Throws<ArgumentException>(() => _catalog.Configure(
                new[] { Era("era-a") }, new[] { Relic("orphan", "unknown") }));

            Assert.AreEqual(3, _catalog.Eras.Count);
            Assert.AreEqual(3, _catalog.Relics.Count);
            Assert.IsNotNull(_catalog.FindRelic("relic-a1"));
            Assert.IsNull(_catalog.FindRelic("orphan"));
        }

        /// <summary>직렬화 편집으로 저장한 잘못된 정의가 재로드 후 진행 기록 생성 단계에서도 거절되는지 검사합니다.</summary>
        /// <param name="invalidKind">시대 ID 중복, 유물 ID 중복 또는 없는 시대 참조의 오류 종류입니다.</param>
        [TestCase("duplicate-era")]
        [TestCase("duplicate-relic")]
        [TestCase("orphan-relic")]
        public void SavedCatalog_InvalidSerializedDefinitionsAreRejectedWhenProgressIsCreated(string invalidKind)
        {
            string assetPath = "Assets/CollectionCatalogTest_" + Guid.NewGuid().ToString("N") + ".asset";
            try
            {
                AssetDatabase.CreateAsset(_catalog, assetPath);
                using (SerializedObject serialized = new SerializedObject(_catalog))
                {
                    if (invalidKind == "duplicate-era")
                    {
                        serialized.FindProperty("_eras").GetArrayElementAtIndex(1)
                            .FindPropertyRelative("_id").stringValue = "era-a";
                    }
                    else if (invalidKind == "duplicate-relic")
                    {
                        serialized.FindProperty("_relics").GetArrayElementAtIndex(1)
                            .FindPropertyRelative("_id").stringValue = "relic-a1";
                    }
                    else
                    {
                        serialized.FindProperty("_relics").GetArrayElementAtIndex(0)
                            .FindPropertyRelative("_eraId").stringValue = "missing-era";
                    }

                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                EditorUtility.SetDirty(_catalog);
                AssetDatabase.SaveAssets();
                Resources.UnloadAsset(_catalog);
                _catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalog>(assetPath);

                Assert.IsNotNull(_catalog);
                Assert.Throws<ArgumentException>(() => new CollectionProgress(_catalog));
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                _catalog = null;
            }
        }

        /// <summary>유효한 카탈로그를 저장하고 재로드한 뒤에도 복원 등록과 시대 건물 해금이 가능한지 검사합니다.</summary>
        [Test]
        public void SavedCatalog_ValidDefinitionsRemainUsableAfterReload()
        {
            string assetPath = "Assets/CollectionCatalogTest_" + Guid.NewGuid().ToString("N") + ".asset";
            try
            {
                AssetDatabase.CreateAsset(_catalog, assetPath);
                AssetDatabase.SaveAssets();
                Resources.UnloadAsset(_catalog);
                _catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalog>(assetPath);

                CollectionProgress loadedProgress = new CollectionProgress(_catalog);
                Assert.AreEqual(3, loadedProgress.TotalCount);
                Assert.IsTrue(loadedProgress.RegisterRestorationCompleted("relic-b1"));
                Assert.IsTrue(loadedProgress.IsBuildingUnlocked("era-b"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                _catalog = null;
            }
        }

        /// <summary>직렬화 배열이 null이면 읽기 모델 사용 전에 명시적 정의 검증에서 거절하는지 검사합니다.</summary>
        /// <param name="fieldName">null로 바꿀 시대 또는 유물 목록의 직렬화 필드 이름입니다.</param>
        [TestCase("_eras")]
        [TestCase("_relics")]
        public void Catalog_NullSerializedArrayIsRejectedBeforeReadModelUsesIt(string fieldName)
        {
            typeof(CollectionCatalog).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_catalog, null);

            Assert.Throws<ArgumentException>(() => _catalog.Validate());
            Assert.Throws<ArgumentException>(() => new CollectionProgress(_catalog));
        }

        /// <summary>수집 규칙 테스트에 필요한 최소 시대 정의를 지정한 ID로 만듭니다.</summary>
        private static CollectionEraDefinition Era(string id)
        {
            return new CollectionEraDefinition(id, id, "시대", "설명", "건물", Color.white);
        }

        /// <summary>수집 규칙 테스트에 필요한 최소 유물 정의를 지정한 종류·시대 ID로 만듭니다.</summary>
        private static CollectionRelicDefinition Relic(string id, string eraId)
        {
            return new CollectionRelicDefinition(id, eraId, id, "정령", "설명", "복원 초안", Color.white);
        }
    }
}
