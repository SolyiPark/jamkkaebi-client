using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MobilePrototype.Collection
{
    /// <summary>도감 디자인을 교체할 수 있는 표시 경계입니다. 입력 의도만 알리고 수집 기록은 변경하지 않습니다.</summary>
    /// <remarks>
    /// View는 여기서 '보이는 화면을 만드는 담당자'라는 뜻입니다.
    /// 새 디자인은 이 클래스를 상속하고 Initialize와 Render를 구현하면 같은 Presenter에 연결할 수 있습니다.
    /// 상속은 약속된 사용법을 이어받아 실제 만드는 방법을 채우는 것입니다.
    /// 버튼을 눌러도 View가 직접 복원 기록이나 현재 페이지를 바꾸지 않고 요청 이벤트를 보냅니다.
    /// Presenter가 그 요청을 검사하고, 처리한 결과를 다시 Render로 전달합니다.
    /// </remarks>
    public abstract class CollectionView : MonoBehaviour
    {
        /// <summary>뒤로 가고 싶다는 알림입니다. 어느 화면으로 갈지는 Presenter가 결정합니다.</summary>
        public event Action BackRequested;
        /// <summary>선택한 시대의 ID를 알립니다. 카드의 위치·표시 이름 대신 고정 ID를 전달합니다.</summary>
        public event Action<string> EraRequested;
        /// <summary>선택한 유물의 ID를 알립니다. 복원 여부를 다시 검사하고 상세를 여는 책임은 Presenter에 있습니다.</summary>
        public event Action<string> RelicRequested;

        // 아래 UI 참조는 기존 입력·통합 검증 코드가 현재 화면을 확인할 때 사용하는 창구입니다.
        // Presenter가 이 참조를 전달한다고 해서 버튼의 모양·위치까지 Presenter가 만들지는 않습니다.
        // protected set은 '이 View 또는 이를 상속한 표시 코드에서만 참조를 지정한다'는 뜻입니다.
        /// <summary>기본 도감의 세로 스크롤입니다. 스크롤 없는 디자인은 null이어도 됩니다.</summary>
        public CollectionScrollRect Scroll { get; protected set; }
        /// <summary>표시 구현이 만든 뒤로가기 버튼입니다. 기본 View는 시대 목록에서 숨깁니다.</summary>
        public Button BackButton { get; protected set; }
        /// <summary>현재 통계 문구입니다. 기본 View에서는 시대 목록 이외의 화면에서 null입니다.</summary>
        public Text StatsText { get; protected set; }
        /// <summary>현재 건물 해금 안내 문구입니다. 기본 View에서는 시대 상세 이외의 화면에서 null입니다.</summary>
        public Text BuildingStatusText { get; protected set; }
        /// <summary>화면의 현재 세로 위치입니다. 1은 맨 위, 0은 맨 아래이며 스크롤이 없으면 1입니다.</summary>
        /// <remarks>다른 종류의 스크롤을 쓰는 새 View는 이 속성을 재정의해 같은 0~1 의미로 연결합니다.</remarks>
        public virtual float ScrollPosition => Scroll ? Scroll.verticalNormalizedPosition : 1f;
        /// <summary>시대 ID로 현재 카드 버튼을 찾는 읽기 전용 목록입니다. 기본 UI와 기존 검증의 연결을 유지합니다.</summary>
        public abstract IReadOnlyDictionary<string, Button> EraButtons { get; }
        /// <summary>유물 ID로 현재 카드 버튼을 찾는 읽기 전용 목록입니다. 버튼 생성·잠금 표현은 View가 맡습니다.</summary>
        public abstract IReadOnlyDictionary<string, Button> RelicButtons { get; }

        /// <summary>씬 카메라·입력 연결과 표시 자산을 준비합니다. 생성 방법은 View가 소유합니다.</summary>
        /// <param name="root">도감 탭의 공통 부모입니다. 이 부모의 카메라와 레이어·입력 경계를 따릅니다.</param>
        /// <param name="font">기존 씬이 제공하는 글꼴입니다. 기본 View의 텍스트 생성에 사용합니다.</param>
        /// <remarks>Presenter가 처음 한 번 호출한 뒤 Render를 호출합니다. 프리팹을 쓰는 View는 여기서 표시 부품을 연결할 수 있습니다.</remarks>
        public abstract void Initialize(MirrorSceneRoot root, Font font);

        /// <summary>읽기 전용 표시 상태를 적용하고 지정한 스크롤 위치를 복원합니다.</summary>
        /// <param name="data">이 갱신에서 보여줄 값입니다. 원본 수집 기록의 변경 기능은 들어 있지 않습니다.</param>
        /// <param name="scrollPosition">표시 후 돌아갈 세로 위치입니다. 1은 맨 위, 0은 맨 아래입니다.</param>
        /// <remarks>기본 View는 UI를 다시 만들지만 새 View가 같은 생성 방식을 따를 필요는 없습니다. 기존 프리팹의 글자만 바꿔도 됩니다.</remarks>
        public abstract void Render(CollectionViewData data, float scrollPosition);

        /// <summary>뒤로가기 입력을 전달합니다. View 안에서 페이지를 직접 바꾸는 대신 호출합니다.</summary>
        protected void RequestBack() => BackRequested?.Invoke();
        /// <summary>시대 선택 입력을 전달합니다. 실제 시대 존재 여부는 Presenter가 확인합니다.</summary>
        /// <param name="eraId">선택한 시대의 고정 ID입니다.</param>
        protected void RequestEra(string eraId) => EraRequested?.Invoke(eraId);
        /// <summary>유물 선택 입력을 전달합니다. 실제 복원 여부는 Presenter가 다시 확인합니다.</summary>
        /// <param name="relicId">선택한 유물 종류의 고정 ID입니다.</param>
        protected void RequestRelic(string relicId) => RelicRequested?.Invoke(relicId);
    }
}
