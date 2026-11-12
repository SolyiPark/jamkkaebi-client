using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jamkkaebi.Tests.EditMode
{
    public class PhaseCatalogTests
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

        private PhaseData CreateData(int phase)
        {
            PhaseData data = ScriptableObject.CreateInstance<PhaseData>();
            SetPrivateField(data, "_phase", phase);
            _createdObjects.Add(data);
            return data;
        }

        private PhaseCatalog CreateCatalog(params PhaseData[] entries)
        {
            PhaseCatalog catalog = ScriptableObject.CreateInstance<PhaseCatalog>();
            SetPrivateField(catalog, "_phases", entries);
            _createdObjects.Add(catalog);
            return catalog;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"{target.GetType().Name}에 '{fieldName}' 필드가 없습니다.");
            field.SetValue(target, value);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void GetPhaseData_RegisteredPhase_ReturnsMatchingData(int requested)
        {
            // Arrange: 1~3페이즈를 순서와 다르게 등록
            PhaseCatalog catalog = CreateCatalog(CreateData(3), CreateData(1), CreateData(2));

            // Act
            PhaseData result = catalog.GetPhaseData(requested);

            // Assert
            Assert.NotNull(result);
            Assert.AreEqual(requested, result.Phase);
        }

        [Test]
        public void GetPhaseData_UnregisteredPhase_ReturnsNullAndWarns()
        {
            // Arrange: 3페이즈 없이 등록
            PhaseCatalog catalog = CreateCatalog(CreateData(1), CreateData(2));

            // Assert: 게임을 멈추지 않고 경고만 남긴다
            LogAssert.Expect(LogType.Warning, new Regex("3페이즈"));

            // Act
            PhaseData result = catalog.GetPhaseData(3);

            // Assert
            Assert.IsNull(result);
        }

        [Test]
        public void GetPhaseData_DuplicatePhase_ReturnsFirstEntry()
        {
            // Arrange: 2페이즈를 두 번 등록
            PhaseData first = CreateData(2);
            PhaseData second = CreateData(2);
            PhaseCatalog catalog = CreateCatalog(first, second);

            // Act
            PhaseData result = catalog.GetPhaseData(2);

            // Assert: 먼저 등록된 것을 쓴다
            Assert.AreSame(first, result);
        }

        [Test]
        public void GetPhaseData_NullEntry_IsSkipped()
        {
            // Arrange: 빈 칸이 섞인 목록
            PhaseData phase1 = CreateData(1);
            PhaseCatalog catalog = CreateCatalog(null, phase1, null);

            // Act
            PhaseData result = catalog.GetPhaseData(1);

            // Assert: 빈 칸 때문에 예외가 나지 않고 등록된 데이터를 찾는다
            Assert.AreSame(phase1, result);
        }

        [Test]
        public void GetPhaseData_NoEntries_ReturnsNullAndWarns()
        {
            // Arrange: 목록 자체가 비어 있음
            PhaseCatalog catalog = CreateCatalog();

            LogAssert.Expect(LogType.Warning, new Regex("1페이즈"));

            // Act
            PhaseData result = catalog.GetPhaseData(1);

            // Assert
            Assert.IsNull(result);
        }
    }
}
