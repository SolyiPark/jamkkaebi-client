using System.Text.RegularExpressions;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using Jamkkaebi.Scripts.Gameplay.Purification;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Jamkkaebi.Tests.EditMode
{
    /// <summary>작업대가 외부 설정으로 판을 여는 어댑터 진입점(Begin)을 검사합니다.</summary>
    public class MinigameSessionAdapterTests
    {
        private GameObject _host;
        private MinigameSessionAdapter _adapter;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Adapter");
            _adapter = _host.AddComponent<MinigameSessionAdapter>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        /// <summary>
        /// 필수 참조가 빠져 판을 열지 못하면 false를 돌려주고 판·외부 설정 상태를 남기지 않는지 검사합니다.
        /// 실행기는 이 false를 보고 셸 가리기를 풀고 실패를 알린다(기다리기만 하면 포기·나가기가 오지 않는다).
        /// </summary>
        [Test]
        public void Begin_MissingReferences_ReturnsFalse()
        {
            LogAssert.Expect(LogType.Error, new Regex("연결되지 않았습니다"));

            bool begun = _adapter.Begin(new PlaySetup(2, Phase.Unseal, MinigameDifficulty.Normal, SealMedium.Fog, 1));

            Assert.IsFalse(begun);
            Assert.IsNull(_adapter.Session);
            Assert.IsFalse(_adapter.HasExternalSetup);
        }
    }
}
