using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jamkkaebi.Scripts.Gameplay.Collection;
using MobilePrototype.Collection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MobilePrototype.Tests.EditMode
{
    public sealed class CollectionArtworkSyncTests
    {
        private string _temporaryAssetPath;
        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

        /// <summary>검사 중 실패한 경우에도 임시 자산과 남은 메모리 오브젝트를 정리합니다.</summary>
        [TearDown]
        public void TearDown() => Cleanup();

        /// <summary>순서와 이름을 갱신해도 종류 ID별 그림 10개와 자산 GUID가 저장·재로드 후 보존되는지 검사합니다.</summary>
        [Test]
        public void SyncCatalog_PreservesEveryArtworkSlotAndAssetIdentityByIdAfterReload()
        {
            _temporaryAssetPath = "Assets/CollectionArtworkSyncTest_" + Guid.NewGuid().ToString("N") + ".asset";
            try
            {
                CollectionCatalog catalog = CollectionCatalogData.CreateCatalog();
                _createdObjects.Add(catalog);
                AssetDatabase.CreateAsset(catalog, _temporaryAssetPath);
                Texture2D texture = new Texture2D(2, 2) { name = "Artwork Test Texture" };
                _createdObjects.Add(texture);
                AssetDatabase.AddObjectToAsset(texture, catalog);

                Sprite[] sprites = new Sprite[10];
                for (int i = 0; i < sprites.Length; i++)
                {
                    sprites[i] = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f));
                    sprites[i].name = "Artwork Slot " + i;
                    _createdObjects.Add(sprites[i]);
                    AssetDatabase.AddObjectToAsset(sprites[i], catalog);
                }

                List<CollectionEraDefinition> eras = catalog.Eras.ToList();
                List<CollectionRelicDefinition> relics = catalog.Relics.ToList();
                CollectionEraDefinition originalEra = eras[0];
                CollectionRelicDefinition originalRelic = relics[0];
                eras[0] = new CollectionEraDefinition(originalEra.Id, "갱신 전 시대명", originalEra.Period,
                    originalEra.Description, originalEra.BuildingName, originalEra.Accent,
                    sprites[0], sprites[1], sprites[2], sprites[3], sprites[4]);
                relics[0] = new CollectionRelicDefinition(originalRelic.Id, originalRelic.EraId, "갱신 전 유물명",
                    originalRelic.SpiritName, originalRelic.Description, originalRelic.ConservationNote,
                    originalRelic.Accent, sprites[5], sprites[6], sprites[7], sprites[8], sprites[9]);
                // 저장 순서를 바꿔도 그림은 위치가 아닌 종류 ID로 보존되어야 합니다.
                eras.Reverse();
                relics.Reverse();
                catalog.Configure(eras, relics);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();

                string catalogGuid = AssetDatabase.AssetPathToGUID(_temporaryAssetPath);
                string[] spriteGuids = new string[sprites.Length];
                long[] spriteFileIds = new long[sprites.Length];
                for (int i = 0; i < sprites.Length; i++)
                {
                    Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprites[i],
                        out spriteGuids[i], out spriteFileIds[i]), Is.True, "그림 슬롯 " + i);
                    Assert.That(spriteGuids[i], Is.EqualTo(catalogGuid));
                }
                Assert.That(spriteFileIds.Distinct().Count(), Is.EqualTo(sprites.Length));

                Type setupType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("CollectionSetup", false))
                    .FirstOrDefault(type => type != null);
                Assert.That(setupType, Is.Not.Null);
                MethodInfo syncMethod = setupType.GetMethod("SyncCatalogAsset", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(syncMethod, Is.Not.Null);
                syncMethod.Invoke(null, new object[] { _temporaryAssetPath });
                AssetDatabase.SaveAssets();
                Resources.UnloadAsset(catalog);
                CollectionCatalog reloaded = AssetDatabase.LoadAssetAtPath<CollectionCatalog>(_temporaryAssetPath);

                Assert.That(reloaded, Is.Not.Null);
                Assert.That(AssetDatabase.AssetPathToGUID(_temporaryAssetPath), Is.EqualTo(catalogGuid));
                Assert.That(reloaded.Eras[0].Id, Is.EqualTo(originalEra.Id));
                Assert.That(reloaded.Eras[0].Name, Is.EqualTo(originalEra.Name));
                Assert.That(reloaded.Relics[0].Id, Is.EqualTo(originalRelic.Id));
                Assert.That(reloaded.Relics[0].Name, Is.EqualTo(originalRelic.Name));

                CollectionEraDefinition era = reloaded.FindEra(originalEra.Id);
                CollectionRelicDefinition relic = reloaded.FindRelic(originalRelic.Id);
                Sprite[] actualSprites =
                {
                    era.OverviewIllustration, era.ContextIllustration, era.BuildingIllustration,
                    era.LockedBuildingIllustration, era.BackgroundIllustration,
                    relic.Illustration, relic.SpiritIllustration, relic.Thumbnail,
                    relic.LockedIllustration, relic.BackgroundIllustration
                };
                for (int i = 0; i < actualSprites.Length; i++)
                {
                    Assert.That(actualSprites[i], Is.Not.Null, "그림 슬롯 " + i);
                    Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(actualSprites[i],
                        out string actualGuid, out long actualFileId), Is.True, "그림 슬롯 " + i);
                    Assert.That(actualGuid, Is.EqualTo(spriteGuids[i]), "그림 슬롯 " + i);
                    Assert.That(actualFileId, Is.EqualTo(spriteFileIds[i]), "그림 슬롯 " + i);
                }
            }
            finally
            {
                Cleanup();
            }
        }

        /// <summary>운영 자산을 변경하지 않고 이 테스트가 생성한 임시 자산과 비영속 오브젝트만 제거합니다.</summary>
        private void Cleanup()
        {
            if (!string.IsNullOrEmpty(_temporaryAssetPath))
            {
                AssetDatabase.DeleteAsset(_temporaryAssetPath);
                _temporaryAssetPath = null;
            }
            foreach (UnityEngine.Object created in _createdObjects)
            {
                if (created && !AssetDatabase.Contains(created))
                    UnityEngine.Object.DestroyImmediate(created);
            }
            _createdObjects.Clear();
        }
    }
}
