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

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<CollectionCatalog>();
            _catalog.Configure(
                new[] { Era("era-a"), Era("era-b"), Era("empty-era") },
                new[] { Relic("relic-a1", "era-a"), Relic("relic-a2", "era-a"), Relic("relic-b1", "era-b") });
            _progress = new CollectionProgress(_catalog);
        }

        [TearDown]
        public void TearDown()
        {
            if (_catalog)
                UnityEngine.Object.DestroyImmediate(_catalog);
        }

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

        [Test]
        public void EmptyCatalog_HasZeroRateAndNoCompletedBuildings()
        {
            _catalog.Configure(Array.Empty<CollectionEraDefinition>(), Array.Empty<CollectionRelicDefinition>());
            CollectionProgress emptyProgress = new CollectionProgress(_catalog);

            Assert.AreEqual(0, emptyProgress.TotalCount);
            Assert.AreEqual(0f, emptyProgress.CollectionRate);
            Assert.AreEqual(0, emptyProgress.CompletedBuildingCount);
        }

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

        [TestCase("_eras")]
        [TestCase("_relics")]
        public void Catalog_NullSerializedArrayIsRejectedBeforeReadModelUsesIt(string fieldName)
        {
            typeof(CollectionCatalog).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_catalog, null);

            Assert.Throws<ArgumentException>(() => _catalog.Validate());
            Assert.Throws<ArgumentException>(() => new CollectionProgress(_catalog));
        }

        private static CollectionEraDefinition Era(string id)
        {
            return new CollectionEraDefinition(id, id, "시대", "설명", "건물", Color.white);
        }

        private static CollectionRelicDefinition Relic(string id, string eraId)
        {
            return new CollectionRelicDefinition(id, eraId, id, "정령", "설명", "복원 초안", Color.white);
        }
    }
}
