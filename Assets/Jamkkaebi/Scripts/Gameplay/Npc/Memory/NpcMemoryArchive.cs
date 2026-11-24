using System;
using System.Collections.Generic;
using System.Globalization;
using Jamkkaebi.Npc;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Memory
{
    /// <summary>
    /// 대화 기억을 글 한 덩이(JSON)로 남기고 되읽는다. 파일에 쓸 때는 대사 패키지의 NpcLineFileStore를 넘긴다
    /// (임시 파일에 다 쓴 뒤 바꿔 넣는다). 경로는 Application.persistentDataPath 아래로 준다.
    /// </summary>
    public sealed class NpcMemoryArchive
    {
        private const string DateFormat = "yyyy-MM-dd";

        private readonly INpcLineStore _file;

        /// <exception cref="ArgumentNullException">file이 null인 경우.</exception>
        public NpcMemoryArchive(INpcLineStore file)
        {
            _file = file ?? throw new ArgumentNullException(nameof(file));
        }

        /// <summary>남겨 둔 기억을 읽는다.</summary>
        /// <param name="store">읽은 저장소. 남긴 것이 없거나 읽지 못했으면 빈 저장소.</param>
        /// <returns>
        /// 남긴 것이 없거나 제대로 읽었으면 true. 글이 깨져 읽지 못했으면 false —
        /// 이때 빈 저장소를 그대로 <see cref="Save"/>하면 남아 있던 파일을 덮어쓴다.
        /// </returns>
        public bool TryLoad(out NpcMemoryStore store)
        {
            store = new NpcMemoryStore();
            string text = _file.Read();
            if (string.IsNullOrWhiteSpace(text)) return true;

            NpcMemorySaveData data;
            try
            {
                data = JsonUtility.FromJson<NpcMemorySaveData>(text);
            }
            catch (ArgumentException)
            {
                return false;
            }
            if (data == null || data.spirits == null) return false;

            foreach (NpcMemorySaveData.Spirit spirit in data.spirits)
            {
                if (spirit == null || spirit.memories == null) continue;
                foreach (NpcMemorySaveData.Entry entry in spirit.memories)
                {
                    // 날짜를 읽을 수 없는 기억은 며칠 전인지 셀 수 없어 버린다.
                    if (entry == null || !TryParseDate(entry.savedOn, out DateTime savedOn)) continue;
                    store.Append(spirit.relicId, new NpcMemoryRecord(savedOn, entry.actionId, entry.summary, entry.recalled));
                }
            }
            return true;
        }

        /// <summary>저장소 전체를 남긴다. 기억을 붙이거나 꺼냈음 표시를 바꾼 뒤에 부른다.</summary>
        /// <exception cref="ArgumentNullException">store가 null인 경우.</exception>
        public void Save(NpcMemoryStore store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));

            var relicIds = new List<int>(store.RelicIds);
            relicIds.Sort();

            var data = new NpcMemorySaveData();
            foreach (int relicId in relicIds)
            {
                var spirit = new NpcMemorySaveData.Spirit { relicId = relicId };
                foreach (NpcMemoryRecord record in store.RecordsOf(relicId))
                {
                    spirit.memories.Add(new NpcMemorySaveData.Entry
                    {
                        savedOn = record.SavedOn.ToString(DateFormat, CultureInfo.InvariantCulture),
                        actionId = record.ActionId,
                        summary = record.Summary,
                        recalled = record.Recalled
                    });
                }
                data.spirits.Add(spirit);
            }
            _file.Write(JsonUtility.ToJson(data));
        }

        private static bool TryParseDate(string text, out DateTime date)
        {
            return DateTime.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}
