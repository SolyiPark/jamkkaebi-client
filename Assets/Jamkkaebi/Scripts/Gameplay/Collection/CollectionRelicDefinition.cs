using System;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Collection
{
    [Serializable]
    public sealed class CollectionRelicDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _eraId;
        [SerializeField] private string _name;
        [SerializeField] private string _spiritName;
        [SerializeField] private string _description;
        [SerializeField] private string _conservationNote;
        [SerializeField] private Color _accent;
        [Tooltip("유물 상세의 그림입니다. 비어 있으면 기본 유물 그림을 사용합니다.")]
        [SerializeField] private Sprite _illustration;
        [Tooltip("유물 상세의 정령 그림입니다. 비어 있으면 기본 정령 그림을 사용합니다.")]
        [SerializeField] private Sprite _spiritIllustration;
        [Tooltip("유물 목록 카드의 그림입니다. 비어 있으면 상세 그림 또는 기본 그림을 사용합니다.")]
        [SerializeField] private Sprite _thumbnail;
        [Tooltip("미복원 유물 카드의 잠금 그림입니다. 비어 있으면 공통 잠금 그림 또는 기본 그림을 사용합니다.")]
        [SerializeField] private Sprite _lockedIllustration;
        [Tooltip("유물 상세의 배경 그림입니다. 비어 있으면 시대 배경 또는 공통 배경을 사용합니다.")]
        [SerializeField] private Sprite _backgroundIllustration;

        /// <summary>개별 인스턴스와 구분되는 유물 종류의 고정 ID입니다.</summary>
        public string Id => _id;
        /// <summary>이 유물 종류가 소속된 시대의 고정 ID입니다.</summary>
        public string EraId => _eraId;
        public string Name => _name;
        public string SpiritName => _spiritName;
        public string Description => _description;
        public string ConservationNote => _conservationNote;
        public Color Accent => _accent;
        /// <summary>유물 상세 그림이며 썸네일이 없으면 목록 카드에도 사용합니다.</summary>
        public Sprite Illustration => _illustration;
        /// <summary>복원 후 함께 표시할 정령의 선택적 상세 그림입니다.</summary>
        public Sprite SpiritIllustration => _spiritIllustration;
        /// <summary>목록 카드 전용 그림이며 null이면 유물 상세 그림 또는 대체 그림을 사용합니다.</summary>
        public Sprite Thumbnail => _thumbnail;
        /// <summary>미복원 카드에 사용할 선택적 잠금 그림입니다.</summary>
        public Sprite LockedIllustration => _lockedIllustration;
        /// <summary>유물 상세 배경이며 null이면 시대 배경 또는 공통 배경을 사용합니다.</summary>
        public Sprite BackgroundIllustration => _backgroundIllustration;

        /// <summary>유물 종류의 표시 정보와 선택적 그림을 구성합니다. ID와 시대 참조는 카탈로그가 검증합니다.</summary>
        /// <param name="id">카탈로그 내에서 유일한 유물 종류 ID입니다.</param>
        /// <param name="eraId">카탈로그에 정의된 소속 시대 ID입니다.</param>
        /// <param name="name">복원 완료 후 표시할 유물 이름입니다.</param>
        /// <param name="spiritName">상세와 목록에 표시할 정령 이름 또는 미정 안내입니다.</param>
        /// <param name="description">유물 상세 설명입니다.</param>
        /// <param name="conservationNote">상세에 표시할 발굴·복원·보존 이야기입니다.</param>
        /// <param name="accent">그림이 없는 화면의 대체 표현에 사용하는 강조색입니다.</param>
        /// <param name="illustration">유물 상세의 선택적 그림입니다.</param>
        /// <param name="spiritIllustration">정령 상세의 선택적 그림입니다.</param>
        /// <param name="thumbnail">유물 목록 카드의 선택적 전용 그림입니다.</param>
        /// <param name="lockedIllustration">미복원 카드의 선택적 잠금 그림입니다.</param>
        /// <param name="backgroundIllustration">유물 상세의 선택적 배경입니다.</param>
        public CollectionRelicDefinition(string id, string eraId, string name, string spiritName,
            string description, string conservationNote, Color accent, Sprite illustration = null,
            Sprite spiritIllustration = null, Sprite thumbnail = null, Sprite lockedIllustration = null,
            Sprite backgroundIllustration = null)
        {
            _id = id;
            _eraId = eraId;
            _name = name;
            _spiritName = spiritName;
            _description = description;
            _conservationNote = conservationNote;
            _accent = accent;
            _illustration = illustration;
            _spiritIllustration = spiritIllustration;
            _thumbnail = thumbnail;
            _lockedIllustration = lockedIllustration;
            _backgroundIllustration = backgroundIllustration;
        }
    }
}
