using System;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Collection
{
    [Serializable]
    public sealed class CollectionEraDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private string _period;
        [SerializeField] private string _description;
        [SerializeField] private string _buildingName;
        [SerializeField] private Color _accent;
        [Tooltip("시대 목록 카드의 그림입니다. 비어 있으면 기본 그림을 사용합니다.")]
        [SerializeField] private Sprite _overviewIllustration;
        [Tooltip("시대 설명 영역의 그림입니다. 비어 있으면 시대 목록 그림 또는 기본 그림을 사용합니다.")]
        [SerializeField] private Sprite _contextIllustration;
        [Tooltip("해금된 건물의 그림입니다. 비어 있으면 기본 건물 그림을 사용합니다.")]
        [SerializeField] private Sprite _buildingIllustration;
        [Tooltip("미해금 건물의 잠금 그림입니다. 비어 있으면 공통 잠금 그림 또는 기본 그림을 사용합니다.")]
        [SerializeField] private Sprite _lockedBuildingIllustration;
        [Tooltip("시대 화면과 해당 시대 유물 상세의 배경입니다. 비어 있으면 공통 배경을 사용합니다.")]
        [SerializeField] private Sprite _backgroundIllustration;

        /// <summary>유물의 시대 참조와 그림 보존에 사용하는 고정 시대 ID입니다.</summary>
        public string Id => _id;
        public string Name => _name;
        public string Period => _period;
        public string Description => _description;
        public string BuildingName => _buildingName;
        public Color Accent => _accent;
        /// <summary>시대 목록 카드에 표시할 그림이며 null이면 화면의 대체 그림을 사용합니다.</summary>
        public Sprite OverviewIllustration => _overviewIllustration;
        /// <summary>시대 설명 그림이며 null이면 목록 그림 또는 화면의 대체 그림을 사용합니다.</summary>
        public Sprite ContextIllustration => _contextIllustration;
        /// <summary>해금 건물에 표시할 선택적 그림입니다.</summary>
        public Sprite BuildingIllustration => _buildingIllustration;
        /// <summary>미해금 건물에 표시할 선택적 잠금 그림입니다.</summary>
        public Sprite LockedBuildingIllustration => _lockedBuildingIllustration;
        /// <summary>시대 화면과 소속 유물 상세에 적용할 선택적 배경입니다.</summary>
        public Sprite BackgroundIllustration => _backgroundIllustration;

        /// <summary>시대의 표시 정보와 선택적 그림 슬롯을 구성합니다. ID 검증은 카탈로그가 수행합니다.</summary>
        /// <param name="id">카탈로그 내에서 유일한 고정 시대 ID입니다.</param>
        /// <param name="name">화면에 표시할 시대 이름입니다.</param>
        /// <param name="period">카드에 표시할 시대 보조 문구입니다.</param>
        /// <param name="description">시대 상세에 표시할 설명입니다.</param>
        /// <param name="buildingName">모든 소속 유물 복원 후 표시할 건물 이름입니다.</param>
        /// <param name="accent">그림이 없는 화면의 대체 표현에 사용하는 강조색입니다.</param>
        /// <param name="overviewIllustration">시대 목록 카드의 선택적 그림입니다.</param>
        /// <param name="contextIllustration">시대 설명 영역의 선택적 그림입니다.</param>
        /// <param name="buildingIllustration">해금된 건물의 선택적 그림입니다.</param>
        /// <param name="lockedBuildingIllustration">미해금 건물의 선택적 잠금 그림입니다.</param>
        /// <param name="backgroundIllustration">시대와 소속 유물 상세의 선택적 배경입니다.</param>
        public CollectionEraDefinition(string id, string name, string period, string description,
            string buildingName, Color accent, Sprite overviewIllustration = null,
            Sprite contextIllustration = null, Sprite buildingIllustration = null,
            Sprite lockedBuildingIllustration = null, Sprite backgroundIllustration = null)
        {
            _id = id;
            _name = name;
            _period = period;
            _description = description;
            _buildingName = buildingName;
            _accent = accent;
            _overviewIllustration = overviewIllustration;
            _contextIllustration = contextIllustration;
            _buildingIllustration = buildingIllustration;
            _lockedBuildingIllustration = lockedBuildingIllustration;
            _backgroundIllustration = backgroundIllustration;
        }
    }
}
