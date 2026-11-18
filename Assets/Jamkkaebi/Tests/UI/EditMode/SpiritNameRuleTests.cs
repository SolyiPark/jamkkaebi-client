using Jamkkaebi.Scripts.UI.Workbench;
using NUnit.Framework;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>정령 이름 규칙(서버 SpiritNamePolicy와 같음)을 검사합니다.</summary>
    public sealed class SpiritNameRuleTests
    {
        /// <summary>비우거나 공백뿐이면 기본 이름으로 되돌리는 것으로 통과하는지 검사합니다.</summary>
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\t\n")]
        public void Empty_ResetsToDefault(string input)
        {
            Assert.IsTrue(SpiritNameRule.TryNormalize(input, out string normalized, out string error));
            Assert.IsNull(normalized);
            Assert.IsNull(error);
        }

        /// <summary>앞뒤 공백을 자른 이름을 돌려주는지 검사합니다.</summary>
        [Test]
        public void Valid_Trims()
        {
            Assert.IsTrue(SpiritNameRule.TryNormalize("  깨 비  ", out string normalized, out string error));
            Assert.AreEqual("깨 비", normalized);
            Assert.IsNull(error);
        }

        /// <summary>2자와 10자는 통과하는지 검사합니다(경계값).</summary>
        [TestCase("깨비")]
        [TestCase("열글자정령이름이에요")]
        public void Boundary_Passes(string input)
        {
            Assert.IsTrue(SpiritNameRule.TryNormalize(input, out string normalized, out _));
            Assert.AreEqual(input, normalized);
        }

        /// <summary>1자와 11자는 실패하고 사용자용 문장을 주는지 검사합니다.</summary>
        [TestCase("깨")]
        [TestCase("열한글자정령이름이에요")]
        public void OutOfRange_Fails(string input)
        {
            Assert.IsFalse(SpiritNameRule.TryNormalize(input, out string normalized, out string error));
            Assert.IsNull(normalized);
            Assert.IsFalse(string.IsNullOrEmpty(error));
        }

        /// <summary>공백을 자른 뒤의 길이로 판단하는지 검사합니다.</summary>
        [Test]
        public void LengthCountedAfterTrim()
        {
            Assert.IsFalse(SpiritNameRule.TryNormalize("   깨   ", out _, out _));
        }

        /// <summary>이모지(서로게이트 쌍)를 한 글자로 세는지 검사합니다. string.Length로는 2입니다.</summary>
        [Test]
        public void CountCodePoints_CountsSurrogatePairAsOne()
        {
            string fire = char.ConvertFromUtf32(0x1F525);
            Assert.AreEqual(2, fire.Length);
            Assert.AreEqual(1, SpiritNameRule.CountCodePoints(fire));
            Assert.AreEqual(3, SpiritNameRule.CountCodePoints("깨비" + fire));
            Assert.AreEqual(0, SpiritNameRule.CountCodePoints(null));
        }

        /// <summary>이모지 10개는 통과하고 11개는 실패하는지 검사합니다(서버 codePointCount와 같음).</summary>
        [Test]
        public void Emoji_UsesCodePointLength()
        {
            string fire = char.ConvertFromUtf32(0x1F525);
            string ten = string.Concat(System.Linq.Enumerable.Repeat(fire, 10));

            Assert.IsTrue(SpiritNameRule.TryNormalize(ten, out _, out _));
            Assert.IsFalse(SpiritNameRule.TryNormalize(ten + fire, out _, out _));
        }
    }
}
