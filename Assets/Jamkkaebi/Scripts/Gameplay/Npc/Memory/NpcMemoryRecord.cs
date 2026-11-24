using System;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Memory
{
    /// <summary>
    /// 기기에 저장하는 대화 기억 하나. 대사 패키지의 MemoryEntry에는 날짜 없이 "며칠 전"만 있어서,
    /// 저장한 날을 따로 남겨 두고 읽을 때마다 며칠 전인지 다시 센다.
    /// </summary>
    public sealed class NpcMemoryRecord
    {
        /// <summary>기억을 남긴 날(서버 today 기준, 시각 없음).</summary>
        public DateTime SavedOn { get; }

        /// <summary>그때 고른 행동. 대사 패키지의 NpcActions 값.</summary>
        public string ActionId { get; }

        /// <summary>대사 원문이 아닌 요약.</summary>
        public string Summary { get; }

        /// <summary>꺼냈음 표시. 한 번 true가 되면 되돌리지 않는다.</summary>
        public bool Recalled { get; private set; }

        public NpcMemoryRecord(DateTime savedOn, string actionId, string summary, bool recalled)
        {
            SavedOn = savedOn.Date;
            ActionId = actionId;
            Summary = summary;
            Recalled = recalled;
        }

        internal void MarkRecalled()
        {
            Recalled = true;
        }
    }
}
