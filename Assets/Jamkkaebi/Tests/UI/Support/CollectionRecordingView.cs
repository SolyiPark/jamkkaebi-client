using System.Collections.Generic;
using Jamkkaebi.Scripts.UI.Collection;
using Jamkkaebi.Scripts.UI.Shell;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Tests.UI.EditMode
{
    /// <summary>실제 UI 계층 없이 교체 View의 데이터·입력 계약을 검사합니다.</summary>
    /// <remarks>
    /// 화면 대신 마지막으로 받은 안내지와 갱신 횟수만 기록합니다. 디자인을 바꿔도 기능이 유지되는지 보기 위한 대역입니다.
    /// MonoBehaviour는 Editor 전용 어셈블리에서 오브젝트에 붙일 수 없으므로 별도 테스트 지원 어셈블리에 둡니다.
    /// 이 어셈블리는 UNITY_INCLUDE_TESTS 조건에서만 사용하고 일반 Player에는 포함하지 않습니다.
    /// </remarks>
    public sealed class CollectionRecordingView : CollectionView
    {
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        public CollectionViewData LastData { get; private set; }
        public int RenderCount { get; private set; }
        public override IReadOnlyDictionary<string, Button> EraButtons => _buttons;
        public override IReadOnlyDictionary<string, Button> RelicButtons => _buttons;

        public override void Initialize(MirrorSceneRoot root, Font font) { }

        public override void Render(CollectionViewData data, float scrollPosition)
        {
            LastData = data;
            RenderCount++;
        }

        public void SelectEra(string eraId) => RequestEra(eraId);
        public void SelectRelic(string relicId) => RequestRelic(relicId);
        public void Back() => RequestBack();
    }
}
