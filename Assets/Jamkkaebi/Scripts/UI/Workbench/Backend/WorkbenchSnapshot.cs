using System;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>작업대 슬롯 한 칸. 서버 SlotView.</summary>
    public sealed class WorkbenchSlot
    {
        public int RelicId { get; }
        public RevealLevel RevealLevel { get; }
        public Phase CurrentPhase { get; }

        public WorkbenchSlot(int relicId, RevealLevel revealLevel, Phase currentPhase)
        {
            if (relicId <= 0) throw new ArgumentOutOfRangeException(nameof(relicId));
            if (!Enum.IsDefined(typeof(RevealLevel), revealLevel))
                throw new ArgumentException($"정체 공개 단계 {revealLevel}은 정의되지 않았습니다.", nameof(revealLevel));
            if (!Enum.IsDefined(typeof(Phase), currentPhase))
                throw new ArgumentException($"유물 정화 페이즈 {currentPhase}는 정의되지 않았습니다.", nameof(currentPhase));

            RelicId = relicId;
            RevealLevel = revealLevel;
            CurrentPhase = currentPhase;
        }
    }

    /// <summary>작업대 정령 목록 한 줄. 서버 SpiritView.</summary>
    public sealed class WorkbenchSpirit
    {
        public int RelicId { get; }

        /// <summary>사용자가 지은 이름, 없으면 기본 이름. 서버가 풀어서 준다.</summary>
        public string SpiritName { get; }

        public Clarity Clarity { get; }
        public int CareCount { get; }

        public WorkbenchSpirit(int relicId, string spiritName, Clarity clarity, int careCount)
        {
            if (relicId <= 0) throw new ArgumentOutOfRangeException(nameof(relicId), "relicId는 1부터 시작합니다.");
            if (string.IsNullOrEmpty(spiritName))
                throw new ArgumentException("정령 이름이 비어 있습니다.", nameof(spiritName));
            if (!Enum.IsDefined(typeof(Clarity), clarity))
                throw new ArgumentException($"선명도 {clarity}는 정의되지 않았습니다.", nameof(clarity));
            if (careCount < 0) throw new ArgumentOutOfRangeException(nameof(careCount), "careCount는 0 이상이어야 합니다.");

            RelicId = relicId;
            SpiritName = spiritName;
            Clarity = clarity;
            CareCount = careCount;
        }
    }

    /// <summary>작업대 첫 화면. 서버 WorkbenchResponse.</summary>
    public sealed class WorkbenchSnapshot
    {
        public int Capacity { get; }

        /// <summary>최초 정화 중인 유물. 획득 순.</summary>
        public IReadOnlyList<WorkbenchSlot> Slots { get; }

        /// <summary>정령. 돌봐야 할 순서.</summary>
        public IReadOnlyList<WorkbenchSpirit> Spirits { get; }

        /// <summary>아직 뽑지 못한 유물 수.</summary>
        public int LockedCount { get; }

        public bool IsFull => Slots.Count >= Capacity;

        public WorkbenchSnapshot(int capacity, IEnumerable<WorkbenchSlot> slots,
            IEnumerable<WorkbenchSpirit> spirits, int lockedCount)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "작업대 용량은 1 이상이어야 합니다.");
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (spirits == null) throw new ArgumentNullException(nameof(spirits));
            if (lockedCount < 0) throw new ArgumentOutOfRangeException(nameof(lockedCount), "미획득 유물 수는 음수일 수 없습니다.");

            List<WorkbenchSlot> slotList = slots.ToList();
            if (slotList.Contains(null)) throw new ArgumentException("슬롯 목록에 null이 있습니다.", nameof(slots));

            List<WorkbenchSpirit> spiritList = spirits.ToList();
            if (spiritList.Contains(null)) throw new ArgumentException("도깨비 목록에 null이 있습니다.", nameof(spirits));

            Capacity = capacity;
            Slots = slotList.AsReadOnly();
            Spirits = spiritList.AsReadOnly();
            LockedCount = lockedCount;
        }
    }
}