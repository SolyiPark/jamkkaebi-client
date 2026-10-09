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

        public string Id => _id;
        public string EraId => _eraId;
        public string Name => _name;
        public string SpiritName => _spiritName;
        public string Description => _description;
        public string ConservationNote => _conservationNote;
        public Color Accent => _accent;
        public Sprite Illustration => _illustration;
        public Sprite SpiritIllustration => _spiritIllustration;
        public Sprite Thumbnail => _thumbnail;
        public Sprite LockedIllustration => _lockedIllustration;
        public Sprite BackgroundIllustration => _backgroundIllustration;

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
