using System;
using Jamkkaebi.Scripts.UI.Friends;
using NUnit.Framework;
using UnityEngine;

namespace Jamkkaebi.Tests.UI
{
    /// <summary>프리팹 부재와 미등록 ID를 구분하고 잘못된 카탈로그가 적용되지 않는지 검사합니다.</summary>
    public sealed class ExhibitionVisualCatalogTests
    {
        private static ExhibitionVisualDefinition Definition(int id, Vector3? scale = null) =>
            new ExhibitionVisualDefinition(id, null, scale ?? Vector3.one, Vector2.zero, new Rect(-.2f, 0, .4f, .5f));

        [Test]
        public void RegisteredPlaceholderIsKnownAndNamespacesRemainIndependent()
        {
            var catalog = ScriptableObject.CreateInstance<ExhibitionVisualCatalog>();
            try
            {
                catalog.Configure(new[] { Definition(8) }, new[] { Definition(1) });
                Assert.IsTrue(catalog.IsKnown(false, 8));
                Assert.IsFalse(catalog.IsKnown(true, 8));
                Assert.IsTrue(catalog.TryGet(true, 1, out var building));
                Assert.IsNull(building.Prefab);
                Assert.IsFalse(catalog.TryGet(false, 999, out _));
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void InvalidReplacementPreservesPreviousCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<ExhibitionVisualCatalog>();
            try
            {
                catalog.Configure(new[] { Definition(2) }, Array.Empty<ExhibitionVisualDefinition>());
                Assert.Throws<InvalidOperationException>(() => catalog.Configure(
                    new[] { Definition(8), Definition(8) }, Array.Empty<ExhibitionVisualDefinition>()));
                Assert.IsTrue(catalog.IsKnown(false, 2));
                Assert.IsFalse(catalog.IsKnown(false, 8));
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidScaleIsRejected(float scale)
        {
            var catalog = ScriptableObject.CreateInstance<ExhibitionVisualCatalog>();
            try
            {
                Assert.Throws<InvalidOperationException>(() => catalog.Configure(
                    new[] { Definition(2, new Vector3(scale, 1, 1)) }, Array.Empty<ExhibitionVisualDefinition>()));
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }

        [Test]
        public void InputArrayCannotReplaceRegisteredEntriesAfterConfiguration()
        {
            var catalog = ScriptableObject.CreateInstance<ExhibitionVisualCatalog>();
            try
            {
                var entries = new[] { Definition(2) };
                catalog.Configure(entries, Array.Empty<ExhibitionVisualDefinition>());
                entries[0] = Definition(8);
                Assert.IsTrue(catalog.IsKnown(false, 2));
                Assert.IsFalse(catalog.IsKnown(false, 8));
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }
    }
}
