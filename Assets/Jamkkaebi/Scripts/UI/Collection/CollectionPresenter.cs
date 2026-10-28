using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Collection;
using UnityEngine;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Collection
{
    /// <summary>
    /// 복원 완료 기록을 시대 목록, 시대 상세와 유물 상세로 표시하고 도감 내부 탐색을 관리합니다.
    /// </summary>
    /// <remarks>
    /// Presenter는 '무슨 내용을 보여줄지, 누른 행동을 허용할지 결정하는 담당자'입니다.
    /// 카드의 좌표·색상·모양은 View에 맡깁니다. 디자인을 바꿀 때 이 클래스의 수집·잠금 로직까지 고치지 않기 위한 분리입니다.
    /// 흐름은 사용자 입력 → View의 요청 → 여기서 검사와 상태 변경 → 표시 데이터 복사 → View.Render 순서입니다.
    /// 기록 변경은 CollectionProgress가 맡고, 이 클래스는 그 변경 알림을 받아 화면을 갱신합니다.
    /// </remarks>
    [RequireComponent(typeof(MirrorSceneRoot))]
    public sealed class CollectionPresenter : MonoBehaviour
    {
        [SerializeField] private CollectionCatalog _catalog;
        [SerializeField] private Font _font;
        [SerializeField] private CollectionAppearance _appearance;
        // 선택 사항입니다. 새 디자인을 담은 프리팹의 '씬에 배치된 인스턴스'를 연결합니다.
        // 비워 두어도 기존 씬이 실행되도록 Awake에서 기존 디자인의 기본 View를 준비합니다.
        [SerializeField] private CollectionView _view;
        // 기록이 여러 번 바뀌어도 같은 프레임에서 UI 전체를 여러 번 만들지 않도록 갱신을 예약합니다.
        private bool _refreshRequested;
        // 예: 시대 목록에서 내려 본 위치와 삼국 상세에서 내려 본 위치를 따로 기억합니다.
        private readonly Dictionary<string, float> _scrollPositions = new Dictionary<string, float>();
        /// <summary>현재 표시 중인 도감 내부 화면입니다.</summary>
        public CollectionPage Page { get; private set; }
        /// <summary>시대와 유물의 고정 정보를 제공하는 카탈로그입니다.</summary>
        public CollectionCatalog Catalog => _catalog;
        /// <summary>현재 공통 UI 그림이며, null이면 기본 색상과 기호를 사용합니다.</summary>
        public CollectionAppearance Appearance => _appearance;
        /// <summary>현재 화면이 구독하는 복원 완료 및 신규 확인 기록입니다.</summary>
        public CollectionProgress Progress { get; private set; }
        /// <summary>상세 화면의 시대 ID이며, 시대 목록에서는 null입니다.</summary>
        public string SelectedEraId { get; private set; }
        /// <summary>유물 상세의 유물 ID이며, 다른 화면에서는 null입니다.</summary>
        public string SelectedRelicId { get; private set; }
        /// <summary>현재 페이지의 스크롤과 공통 탭 제스처 소유권을 처리합니다.</summary>
        public CollectionScrollRect Scroll => _view ? _view.Scroll : null;
        /// <summary>상위 도감 화면으로 이동하는 버튼이며, 시대 목록에서는 숨겨집니다.</summary>
        public Button BackButton => _view ? _view.BackButton : null;
        /// <summary>현재 생성된 시대 목록 버튼을 시대 ID로 조회합니다.</summary>
        public IReadOnlyDictionary<string, Button> EraButtons => _view.EraButtons;
        /// <summary>현재 생성된 유물 카드 버튼을 유물 ID로 조회합니다.</summary>
        public IReadOnlyDictionary<string, Button> RelicButtons => _view.RelicButtons;
        /// <summary>시대 목록의 전체 통계이며, 다른 화면에서는 null입니다.</summary>
        public Text StatsText => _view ? _view.StatsText : null;
        /// <summary>시대 상세의 건물 해금 상태이며, 다른 화면에서는 null입니다.</summary>
        public Text BuildingStatusText => _view ? _view.BuildingStatusText : null;

        /// <summary>필수 자산을 확인하고 샘플 기록, 공통 프레임과 첫 시대 목록을 생성합니다.</summary>
        private void Awake()
        {
            if (!_catalog || !_font)
            {
                Debug.LogError("도감 카탈로그와 글꼴을 연결하세요.", this);
                enabled = false;
                return;
            }
            // 현재는 서버 저장을 읽는 대신 샘플 기록으로 시작합니다. View를 바꿔도 이 초기 기록 정책은 그대로입니다.
            Progress = CollectionDemoData.CreateProgress(_catalog);
            // 명시적으로 연결한 View → 계층 안에 있는 View → 기존 디자인의 기본 View 순서입니다.
            // 새 디자인을 연결하려고 기존 Collection.unity나 일회성 이관 도구를 다시 만들 필요가 없습니다.
            if (!_view) _view = GetComponentInChildren<CollectionView>(true);
            if (!_view) _view = gameObject.AddComponent<CollectionDefaultView>();
            // View의 버튼은 상태를 직접 바꾸지 않습니다. 아래 함수로 요청을 보내 여기서 검사하게 합니다.
            // 이 연결은 View와 Presenter가 살아 있는 동안 유지하고 OnDestroy에서 해제합니다.
            _view.BackRequested += GoBack;
            _view.EraRequested += ShowEra;
            _view.RelicRequested += ShowRelic;
            _view.Initialize(GetComponent<MirrorSceneRoot>(), _font);
            RenderCurrent(1);
        }

        /// <summary>기록 변경 알림을 구독하고 활성화 후 최신 상태를 표시하도록 예약합니다.</summary>
        private void OnEnable()
        {
            if (Progress == null) return;
            Progress.Changed += RequestRefresh;
            // 탭이 숨겨진 사이에 기록이 달라졌을 수 있으므로, 다시 보일 때는 최신 복사본을 만들게 합니다.
            _refreshRequested = true;
        }

        /// <summary>비활성화 시 기록 변경 구독을 해제해 중복 알림을 방지합니다.</summary>
        private void OnDisable()
        {
            // 숨겨진 화면이 계속 변경 알림을 받지 않게 합니다. 재활성화 시 OnEnable에서 한 번만 다시 연결합니다.
            if (Progress != null) Progress.Changed -= RequestRefresh;
        }

        /// <summary>여러 기록 변경을 다음 프레임의 한 번의 화면 갱신으로 모읍니다.</summary>
        private void RequestRefresh() => _refreshRequested = true;

        /// <summary>예약된 기록 변경을 현재 페이지의 스크롤 위치를 유지하며 표시합니다.</summary>
        private void LateUpdate()
        {
            if (!_refreshRequested || !_view) return;
            _refreshRequested = false;
            // ScrollRect 자체가 없어도 View의 논리적 위치를 쓰므로, 스크롤 없는 새 디자인도 갱신할 수 있습니다.
            RenderCurrent(_view.ScrollPosition);
        }

        /// <summary>
        /// 연결 계층의 공유 기록으로 교체하고 구독과 현재 유물의 열람 가능 상태를 갱신합니다.
        /// 저장 수명은 기록을 제공하는 연결 계층에서 관리합니다.
        /// </summary>
        /// <param name="progress">이 도감과 같은 카탈로그를 사용하는 새 기록입니다.</param>
        /// <exception cref="System.ArgumentException">기록이 null이거나 카탈로그가 다르면 발생합니다.</exception>
        public void BindProgress(CollectionProgress progress)
        {
            if (progress == null || progress.Catalog != _catalog)
                throw new System.ArgumentException("도감과 같은 카탈로그의 기록을 전달하세요.", nameof(progress));
            if (Progress != null) Progress.Changed -= RequestRefresh;
            Progress = progress;
            // 계정·공유 기록을 바꿨는데 지금 보고 있는 유물이 새 기록에서는 잠겼다면 상세를 계속 보여주지 않습니다.
            if (Page == CollectionPage.Relic && !Progress.IsRestored(SelectedRelicId))
            {
                Page = CollectionPage.Era;
                SelectedRelicId = null;
            }
            else if (Page == CollectionPage.Relic) Progress.MarkViewed(SelectedRelicId);
            if (isActiveAndEnabled) Progress.Changed += RequestRefresh;
            RequestRefresh();
        }

        /// <summary>
        /// 공통 그림을 교체하고 현재 페이지와 스크롤 위치를 유지합니다. null은 기본 표현을 사용합니다.
        /// </summary>
        /// <param name="appearance">새 공통 UI 그림이며, null이면 기본 표현으로 돌아갑니다.</param>
        public void BindAppearance(CollectionAppearance appearance)
        {
            _appearance = appearance;
            if (_view && Progress != null)
            {
                RenderCurrent(_view.ScrollPosition);
                _refreshRequested = false;
            }
            else RequestRefresh();
        }

        /// <summary>현재 스크롤을 기억한 뒤 시대 목록과 해당 목록의 이전 스크롤 위치를 표시합니다.</summary>
        public void ShowOverview()
        {
            RememberScroll();
            Page = CollectionPage.Overview;
            SelectedEraId = null;
            SelectedRelicId = null;
            RenderCurrent(SavedScroll());
        }

        /// <summary>등록된 시대의 유물 목록, 건물 해금 상태와 설명을 표시합니다.</summary>
        /// <param name="eraId">열 시대 ID입니다. 카탈로그에 없으면 현재 화면을 유지합니다.</param>
        public void ShowEra(string eraId)
        {
            if (_catalog.FindEra(eraId) == null) return;
            RememberScroll();
            Page = CollectionPage.Era;
            SelectedEraId = eraId;
            SelectedRelicId = null;
            RenderCurrent(SavedScroll());
        }

        /// <summary>복원 완료한 유물의 상세를 열고 해당 기록의 신규 표시를 확인 처리합니다.</summary>
        /// <param name="relicId">열 유물 ID입니다. 미등록 또는 미복원 유물이면 현재 화면을 유지합니다.</param>
        public void ShowRelic(string relicId)
        {
            var relic = _catalog.FindRelic(relicId);
            // 기본 버튼의 interactable=false는 화면 표현일 뿐입니다.
            // 새 디자인이 잠긴 항목의 요청을 보내거나 다른 코드가 직접 호출해도 여기서 실제 기록으로 다시 검사합니다.
            if (relic == null || !Progress.IsRestored(relicId)) return;
            RememberScroll();
            Page = CollectionPage.Relic;
            SelectedEraId = relic.EraId;
            SelectedRelicId = relicId;
            // 신규 N 표시를 지우는 책임도 Presenter에 둡니다. 그림만 다시 그리는 View가 신규 기록을 지우면 안 됩니다.
            Progress.MarkViewed(relicId);
            RenderCurrent(SavedScroll());
            // MarkViewed의 변경 알림으로 예약된 갱신은 바로 위에서 이미 표시했으므로 중복 재생성을 막습니다.
            _refreshRequested = false;
        }

        /// <summary>유물 상세에서는 소속 시대로, 시대 상세에서는 시대 목록으로 이동합니다.</summary>
        public void GoBack()
        {
            if (Page == CollectionPage.Relic) ShowEra(SelectedEraId);
            else if (Page == CollectionPage.Era) ShowOverview();
        }

        /// <summary>화면 종류와 선택 항목을 조합한 스크롤 위치 보관 키입니다.</summary>
        private string ScrollKey => Page + ":" + (Page == CollectionPage.Relic ? SelectedRelicId : SelectedEraId);
        /// <summary>현재 화면의 정규화된 세로 위치를 화면별 기록에 보관합니다.</summary>
        private void RememberScroll()
        {
            if (_view) _scrollPositions[ScrollKey] = _view.ScrollPosition;
        }
        /// <summary>현재 화면에서 마지막으로 보던 위치를 조회합니다.</summary>
        /// <returns>저장된 정규화 위치이며, 첫 방문이면 상단을 나타내는 1입니다.</returns>
        private float SavedScroll() => _scrollPositions.TryGetValue(ScrollKey, out float position) ? position : 1;

        /// <summary>현재 상태를 표시 전용 복사본으로 만들어 View에 전달합니다.</summary>
        private void RenderCurrent(float scrollPosition)
        {
            // 원본 기록을 View에 넘기지 않고 지금 보여줄 수치·판정만 복사합니다.
            // Render는 '이 안내지대로 화면을 보여 달라'는 요청이며 복원·해금을 실행하는 명령이 아닙니다.
            _view.Render(new CollectionViewData(Progress, _appearance, Page, SelectedEraId, SelectedRelicId), scrollPosition);
        }

        /// <summary>View의 입력 알림 연결을 해제해 파괴된 Presenter를 다시 호출하지 않게 합니다.</summary>
        private void OnDestroy()
        {
            if (!_view) return;
            _view.BackRequested -= GoBack;
            _view.EraRequested -= ShowEra;
            _view.RelicRequested -= ShowRelic;
        }
    }
}
