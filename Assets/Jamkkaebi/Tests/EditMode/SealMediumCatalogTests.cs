using System.Collections.Generic;
using System.Reflection;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.EditMode
{
    public class SealMediumCatalogTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _createdObjects)
            {
                Object.DestroyImmediate(o);
            }
            
            _createdObjects.Clear();
        }

        private SealMediumData CreateData(SealMedium sealMedium)
        {
            SealMediumData data = ScriptableObject.CreateInstance<SealMediumData>();
            SetPrivateField(data, "_sealMedium", sealMedium);
            _createdObjects.Add(data);
            return data;
        }

        private SealMediumCatalog CreateCatalog(params SealMediumData[] entries)
        {
            SealMediumCatalog catalog = ScriptableObject.CreateInstance<SealMediumCatalog>();
            SetPrivateField(catalog, "_sealMediums", entries);
            _createdObjects.Add(catalog);
            return catalog;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"{target.GetType().Name}에 '{fieldName}' 필드가 없습니다.");
            field.SetValue(target, value);
        }
        
        [TestCase(SealMedium.Rust)]
        [TestCase(SealMedium.Fog)]
        [TestCase(SealMedium.Soot)]
        [TestCase(SealMedium.Root)]
        public void GetSealMediumData_RegisterAllMediums_ReturnsMatchingData(SealMedium requested)
        {
            // Arrange: 봉인 매개체 4종 생성 및 등록
            SealMediumData rust = CreateData(SealMedium.Rust);
            SealMediumData fog = CreateData(SealMedium.Fog);
            SealMediumData soot = CreateData(SealMedium.Soot);
            SealMediumData root = CreateData(SealMedium.Root);
            SealMediumCatalog catalog = CreateCatalog(rust, fog, soot, root);
            
            // Act
            SealMediumData result = catalog.GetSealMediumData(requested);
            
            // Assert
            Assert.AreEqual(requested, result.SealMedium);
        }
    }
}