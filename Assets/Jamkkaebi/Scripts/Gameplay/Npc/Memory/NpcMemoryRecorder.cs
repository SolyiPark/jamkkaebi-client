using System;
using Jamkkaebi.Npc;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Memory
{
    /// <summary>
    /// 대사 요청 하나를 마무리하며 대화 기억에 반영할 것을 한 번에 처리한다.
    /// 요청마다 꼭 한 번, NpcRequestManager.RequestAsync의 결과를 받은 뒤에 부른다.
    /// </summary>
    public static class NpcMemoryRecorder
    {
        /// <summary>
        /// context.Finish(response)를 부르고, 반응을 띄웠으면 남길 기억을 맨 뒤에 붙이고
        /// context.MemoryToMark가 가리키는 기억에 꺼냈음 표시를 남긴다.
        /// 반응을 띄우지 못했으면(화면 종료, NPC 전환 등) 저장소를 바꾸지 않아 다음 안부에 그 기억을 다시 꺼낸다.
        /// </summary>
        /// <param name="response">요청 관리자의 결과. 요청을 보내지 못했으면 null.</param>
        /// <param name="today">서버 today. 요청을 만들 때 넘긴 값과 같아야 한다.</param>
        /// <returns>저장소가 바뀌었으면 true. 이때 파일에 다시 남긴다.</returns>
        /// <exception cref="ArgumentNullException">store나 context가 null인 경우.</exception>
        public static bool Record(NpcMemoryStore store, int relicId, NpcContext context, NpcResponse response, DateTime today)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (context == null) throw new ArgumentNullException(nameof(context));

            // 띄우지 못했으면 Finish가 MemoryToMark를 null로 비우고 null을 돌려준다.
            MemoryEntry memory = context.Finish(response);
            bool changed = false;

            // 새 기억을 붙이기 전에 표시한다. 표시할 기억은 요청을 만들 때 넘긴 목록 안에 있다.
            if (context.MemoryToMark != null)
            {
                changed |= store.MarkRecalled(relicId, context.MemoryToMark);
            }
            if (memory != null)
            {
                store.Add(relicId, memory, today);
                changed = true;
            }
            return changed;
        }
    }
}
