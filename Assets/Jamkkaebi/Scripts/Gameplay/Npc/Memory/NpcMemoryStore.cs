using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Jamkkaebi.Npc;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Memory
{
    /// <summary>
    /// 정령(relicId)마다의 대화 기억. 저장한 순서(오래된 것 → 새 것)를 그대로 지킨다.
    /// 대사 패키지의 컨텍스트 구성기가 이 순서로 꺼낼 기억을 정한다.
    /// </summary>
    /// <remarks>
    /// 파일 저장은 <see cref="NpcMemoryArchive"/>가 맡는다. 7일이 지난 기억도 지우지 않는다(구성기가 7일 안의 것만 본다).
    /// </remarks>
    public sealed class NpcMemoryStore
    {
        private static readonly IReadOnlyList<NpcMemoryRecord> NoRecords = new List<NpcMemoryRecord>().AsReadOnly();

        private readonly Dictionary<int, List<NpcMemoryRecord>> _records = new Dictionary<int, List<NpcMemoryRecord>>();

        // For가 내준 MemoryEntry가 어느 저장 기억인지. 구성기가 돌려주는 MemoryToMark는 내준 그 객체라서 참조로 찾는다.
        // 내준 객체가 버려지면 항목도 함께 사라진다.
        private readonly ConditionalWeakTable<MemoryEntry, NpcMemoryRecord> _handedOut =
            new ConditionalWeakTable<MemoryEntry, NpcMemoryRecord>();

        /// <summary>기억이 있는 정령의 relicId. 순서는 정하지 않는다.</summary>
        public IReadOnlyCollection<int> RelicIds => _records.Keys;

        /// <summary>이 정령의 저장 기억. 저장한 순서 그대로.</summary>
        public IReadOnlyList<NpcMemoryRecord> RecordsOf(int relicId)
        {
            return _records.TryGetValue(relicId, out List<NpcMemoryRecord> list) ? list.AsReadOnly() : NoRecords;
        }

        /// <summary>
        /// NpcContextInput.Memory에 넘길 목록. 저장한 순서 그대로이고 꺼냈음 표시를 담는다.
        /// </summary>
        /// <param name="today">서버 today. 며칠 전인지 세는 기준이다. 기기 시계를 쓰지 않는다.</param>
        /// <returns>부를 때마다 새로 만든 목록. 저장한 날이 today보다 뒤면 오늘(0일 전)로 센다.</returns>
        public List<MemoryEntry> For(int relicId, DateTime today)
        {
            var result = new List<MemoryEntry>();
            if (!_records.TryGetValue(relicId, out List<NpcMemoryRecord> list)) return result;

            foreach (NpcMemoryRecord record in list)
            {
                int daysAgo = Math.Max(0, (today.Date - record.SavedOn).Days);
                var entry = new MemoryEntry(daysAgo, record.ActionId, record.Summary) { Recalled = record.Recalled };
                _handedOut.Add(entry, record);
                result.Add(entry);
            }
            return result;
        }

        /// <summary>새 기억을 이 정령의 목록 맨 뒤에 붙인다. 꺼냈음 표시는 받은 값 그대로 저장한다(구성기가 주는 기억은 false).</summary>
        /// <param name="memory">NpcContext.Finish가 돌려준 기억.</param>
        /// <param name="today">서버 today. 저장한 날은 여기서 memory.DaysAgo만큼 앞선 날이다.</param>
        /// <exception cref="ArgumentNullException">memory가 null인 경우.</exception>
        public void Add(int relicId, MemoryEntry memory, DateTime today)
        {
            if (memory == null) throw new ArgumentNullException(nameof(memory));

            DateTime savedOn = today.Date.AddDays(-Math.Max(0, memory.DaysAgo));
            Append(relicId, new NpcMemoryRecord(savedOn, memory.ActionId, memory.Summary, memory.Recalled));
        }

        /// <summary>저장 기억에 꺼냈음 표시를 남긴다.</summary>
        /// <param name="memory">NpcContext.MemoryToMark. <see cref="For"/>가 내준 목록 안의 객체여야 한다.</param>
        /// <returns>이 정령의 기억을 찾아 표시했거나 이미 표시돼 있으면 true. 이 저장소가 내준 객체가 아니면 false.</returns>
        public bool MarkRecalled(int relicId, MemoryEntry memory)
        {
            if (memory == null) return false;
            if (!_handedOut.TryGetValue(memory, out NpcMemoryRecord record)) return false;
            if (!_records.TryGetValue(relicId, out List<NpcMemoryRecord> list) || !list.Contains(record)) return false;

            record.MarkRecalled();
            return true;
        }

        /// <summary>파일에서 읽은 기억을 저장돼 있던 순서대로 되살릴 때 쓴다.</summary>
        internal void Append(int relicId, NpcMemoryRecord record)
        {
            if (!_records.TryGetValue(relicId, out List<NpcMemoryRecord> list))
            {
                list = new List<NpcMemoryRecord>();
                _records.Add(relicId, list);
            }
            list.Add(record);
        }
    }
}
