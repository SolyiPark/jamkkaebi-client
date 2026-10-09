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

        public string Id => _id;
        public string Name => _name;
        public string Period => _period;
        public string Description => _description;
        public string BuildingName => _buildingName;
        public Color Accent => _accent;
        public Sprite OverviewIllustration => _overviewIllustration;
        public Sprite ContextIllustration => _contextIllustration;
        public Sprite BuildingIllustration => _buildingIllustration;
        public Sprite LockedBuildingIllustration => _lockedBuildingIllustration;
        public Sprite BackgroundIllustration => _backgroundIllustration;

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
