using System;
using System.Collections.Generic;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Interaction
{
    /// <summary>입력 화면에 전달하는 현재 노드·NPC 상태·선택지입니다. 목록이 비어 있으면 입력 대기가 없습니다.</summary>
    public sealed class NpcInteractionPage
    {
        /// <summary>화면을 열거나 대상을 전환할 때 새로 발급하는 상호작용 세션 식별자입니다.</summary>
        public string SessionId { get; }
        /// <summary>선택지를 제공한 입력 트리 노드 식별자입니다.</summary>
        public string NodeId { get; }
        /// <summary>동일 NPC 상태의 불변 스냅샷입니다. 생성 요청에서는 효과 적용 후 상태입니다.</summary>
        public NpcContextSnapshot Context { get; }
        /// <summary>게임이 판정한 현재 선택지의 읽기 전용 복사본입니다. 빈 목록은 입력 대기가 없음을 뜻합니다.</summary>
        public IReadOnlyList<NpcInteractionChoice> Choices { get; }

        /// <summary>세션과 노드 및 상태를 묶고 선택지를 읽기 전용으로 복사합니다.</summary>
        /// <param name="sessionId">현재 화면 세션 ID입니다.</param>
        /// <param name="nodeId">입력 트리 노드 ID입니다.</param>
        /// <param name="context">불변 NPC 상태입니다. 요청에서는 입력과 같은 NPC의 효과 적용 후 상태여야 합니다.</param>
        /// <param name="choices">현재 선택지입니다. null은 빈 목록이며 중복 행동 ID는 허용하지 않습니다.</param>
        /// <exception cref="ArgumentNullException">상태가 null인 경우입니다.</exception>
        /// <exception cref="ArgumentException">ID 또는 선택지 목록이 규약을 위반한 경우입니다.</exception>
        public NpcInteractionPage(string sessionId, string nodeId, NpcContextSnapshot context,
            IEnumerable<NpcInteractionChoice> choices)
        {
            SessionId = NpcInteractionContract.RequireId(sessionId, nameof(sessionId));
            NodeId = NpcInteractionContract.RequireId(nodeId, nameof(nodeId));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Choices = NpcInteractionContract.CopyChoices(choices, nameof(choices));
        }

        /// <summary>페이지 데이터와 일치하는 활성 선택인지 검사합니다. 실제 상태 재판정과 중복 입력 차단은 호출 측 책임입니다.</summary>
        /// <param name="selection">승인된 입력입니다. 입력 검증이나 효과 적용을 대신하지 않습니다.</param>
        /// <returns>페이지와 일치하는 활성 행동이면 true, null 또는 불일치 및 비활성이면 false입니다.</returns>
        public bool CanSelect(NpcInteractionSelection selection)
        {
            if (selection == null || selection.SessionId != SessionId || selection.NodeId != NodeId ||
                selection.NpcId != Context.NpcId || selection.ExpectedStateVersion != Context.StateVersion)
                return false;
            foreach (NpcInteractionChoice choice in Choices)
            {
                if (choice.ActionId == selection.ActionId) return choice.IsEnabled;
            }
            return false;
        }
    }
}
