using System;
using System.Collections.Generic;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Memory
{
    /// <summary>
    /// 대화 기억 파일의 모양. JsonUtility로 읽고 쓰려고 public 필드와 List만 쓴다. 필드 이름이 곧 JSON 키다.
    /// </summary>
    /// <remarks>recalled가 없는 예전 파일은 false로 읽힌다.</remarks>
    [Serializable]
    public sealed class NpcMemorySaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public List<Spirit> spirits = new List<Spirit>();

        [Serializable]
        public sealed class Spirit
        {
            public int relicId;
            public List<Entry> memories = new List<Entry>();
        }

        [Serializable]
        public sealed class Entry
        {
            /// <summary>기억을 남긴 날. "yyyy-MM-dd".</summary>
            public string savedOn;
            public string actionId;
            public string summary;
            public bool recalled;
        }
    }
}
