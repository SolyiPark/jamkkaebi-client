using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class MinigameRulesTests
    {
        [Test]
        public void ShapePool_ExcludesDot()
        {
            CollectionAssert.DoesNotContain(MinigameRules.ShapePool, PolyominoShape.Dot);
        }

        [Test]
        public void ShapePool_HasNoDuplicates()
        {
            CollectionAssert.AllItemsAreUnique(MinigameRules.ShapePool);
        }

        [Test]
        public void ShapePool_EveryShapeHasDefinition()
        {
            Assert.IsTrue(MinigameRules.ShapePool.All(s => PolyominoShapes.Definitions.ContainsKey(s)));
        }
    }
}
