using UnityEngine;

namespace MobilePrototype.Collection
{
    /// <summary>
    /// 도감 공통 UI 그림입니다. 비어 있는 슬롯은 기존 색상과 벡터 기호를 사용합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CollectionAppearance", menuName = "Mobile Prototype/Collection Appearance")]
    public sealed class CollectionAppearance : ScriptableObject
    {
        [SerializeField, Tooltip("시대 또는 유물별 배경이 없을 때 사용하는 공통 페이지 배경입니다.")]
        private Sprite _pageBackground;
        [SerializeField, Tooltip("시대 목록 카드의 배경입니다.")]
        private Sprite _eraCardBackground;
        [SerializeField, Tooltip("복원 완료 유물 카드의 배경입니다.")]
        private Sprite _restoredRelicCardBackground;
        [SerializeField, Tooltip("미복원 유물 카드의 배경입니다.")]
        private Sprite _lockedRelicCardBackground;
        [SerializeField, Tooltip("시대별 건물 카드의 배경입니다.")]
        private Sprite _buildingCardBackground;
        [SerializeField, Tooltip("유물 상세 화면의 일러스트 상판 배경입니다.")]
        private Sprite _relicPlateBackground;
        [SerializeField, Tooltip("유물 상세 화면의 정령 일러스트 상판 배경입니다.")]
        private Sprite _spiritPlateBackground;
        [SerializeField, Tooltip("아직 읽지 않은 기록의 신규 배지 배경입니다.")]
        private Sprite _newBadgeBackground;
        [SerializeField, Tooltip("신규 배지의 N 글자를 대신하는 그림입니다.")]
        private Sprite _newBadgeIcon;
        [SerializeField, Tooltip("뒤로 가기 버튼의 배경입니다.")]
        private Sprite _backButtonBackground;
        [SerializeField, Tooltip("뒤로 가기 버튼의 화살표 글자를 대신하는 그림입니다.")]
        private Sprite _backIcon;
        [SerializeField, Tooltip("유물 또는 시대별 잠금 그림이 없을 때 사용하는 공통 잠금 그림입니다.")]
        private Sprite _lockIllustration;
        [SerializeField, Tooltip("수집률 진행 막대의 전체 길이 배경입니다.")]
        private Sprite _progressTrack;
        [SerializeField, Tooltip("수집률 진행 막대의 채워진 부분입니다.")]
        private Sprite _progressFill;
        [SerializeField, Tooltip("머리글과 본문을 구분하는 선입니다.")]
        private Sprite _divider;
        [SerializeField, Tooltip("시대 목록 카드 왼쪽의 강조 띠입니다.")]
        private Sprite _accentStrip;
        [SerializeField, Tooltip("시대 수집 완료 표시의 체크 글자를 대신하는 그림입니다.")]
        private Sprite _completionIcon;

        /// <summary>유물과 시대의 전용 배경이 없을 때 사용하는 공통 페이지 배경입니다.</summary>
        public Sprite PageBackground => _pageBackground;
        /// <summary>시대 목록 카드의 공통 배경이며, 미지정이면 시대 색상을 사용합니다.</summary>
        public Sprite EraCardBackground => _eraCardBackground;
        /// <summary>복원 완료한 유물 카드의 공통 배경입니다.</summary>
        public Sprite RestoredRelicCardBackground => _restoredRelicCardBackground;
        /// <summary>미복원 유물 카드의 공통 배경입니다.</summary>
        public Sprite LockedRelicCardBackground => _lockedRelicCardBackground;
        /// <summary>건물 해금 상태를 표시하는 카드의 공통 배경입니다.</summary>
        public Sprite BuildingCardBackground => _buildingCardBackground;
        /// <summary>유물 상세의 큰 일러스트 상판에 사용하는 배경입니다.</summary>
        public Sprite RelicPlateBackground => _relicPlateBackground;
        /// <summary>유물 상세의 정령 일러스트 상판에 사용하는 배경입니다.</summary>
        public Sprite SpiritPlateBackground => _spiritPlateBackground;
        /// <summary>미열람 기록의 신규 배지 표면에 사용하는 배경입니다.</summary>
        public Sprite NewBadgeBackground => _newBadgeBackground;
        /// <summary>신규 배지의 N 글자를 대체하는 그림이며, 미지정이면 글자를 표시합니다.</summary>
        public Sprite NewBadgeIcon => _newBadgeIcon;
        /// <summary>뒤로 가기 버튼의 공통 표면 그림입니다.</summary>
        public Sprite BackButtonBackground => _backButtonBackground;
        /// <summary>뒤로 가기 화살표 글자를 대체하는 그림이며, 미지정이면 글자를 표시합니다.</summary>
        public Sprite BackIcon => _backIcon;
        /// <summary>유물별 또는 시대별 잠금 그림이 없을 때 사용하는 공통 잠금 그림입니다.</summary>
        public Sprite LockIllustration => _lockIllustration;
        /// <summary>수집률 진행 막대의 전체 길이 배경입니다.</summary>
        public Sprite ProgressTrack => _progressTrack;
        /// <summary>수집률만큼 너비를 채워 표시하는 진행 막대 그림입니다.</summary>
        public Sprite ProgressFill => _progressFill;
        /// <summary>머리글과 본문 구간을 나누는 공통 구분선 그림입니다.</summary>
        public Sprite Divider => _divider;
        /// <summary>시대 목록 카드 왼쪽의 강조 띠를 대체하는 그림입니다.</summary>
        public Sprite AccentStrip => _accentStrip;
        /// <summary>시대 수집 완료의 체크 글자를 대체하는 그림이며, 미지정이면 글자를 표시합니다.</summary>
        public Sprite CompletionIcon => _completionIcon;
    }
}
