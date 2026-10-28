using System;
using Jamkkaebi.Scripts.Gameplay.Data;
using NUnit.Framework;

namespace Jamkkaebi.Tests.EditMode
{
    public class SealMediumParserTests
    {
        [TestCase("RUST", SealMedium.Rust)]
        [TestCase("FOG", SealMedium.Fog)]
        [TestCase("SOOT", SealMedium.Soot)]
        [TestCase("ROOT", SealMedium.Root)]
        public void Parse_ServerValue_ReturnsMatchingSealMedium(string serverValue, SealMedium expected)
        {
            Assert.AreEqual(expected, SealMediumParser.Parse(serverValue));
        }

        [TestCase("rust")]      // 서버 표기와 대소문자가 다름
        [TestCase("2")]         // Enum.TryParse라면 통과해 버리는 숫자 문자열
        [TestCase("")]
        [TestCase("ICE")]
        public void Parse_UnknownValue_ThrowsArgumentException(string serverValue)
        {
            Assert.Throws<ArgumentException>(() => SealMediumParser.Parse(serverValue));
        }

        [Test]
        public void Parse_Null_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SealMediumParser.Parse(null));
        }
    }
}
