using System;
using System.Collections.Generic;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Interaction
{
    /// <summary>출력 트리에서 게임이 선택한 반응 계획입니다. 모델은 분기·표현 ID·다음 선택지를 바꿀 수 없습니다.</summary>
    public sealed class NpcResponsePlan
    {
        /// <summary>게임 출력 트리가 선택한 반응 분기 식별자입니다. 모델은 변경하지 않습니다.</summary>
        public string BranchId { get; }
        /// <summary>모델이 표현해야 할 게임의 반응 의도입니다. 한 줄로 전달합니다.</summary>
        public string Intent { get; }
        /// <summary>게임이 확정한 사실의 읽기 전용 복사본입니다. 검증되지 않은 모델 출력을 사실로 넣지 않습니다.</summary>
        public IReadOnlyList<string> Facts { get; }
        /// <summary>현재 요청의 유효한 생성 실패에 사용할 게임 작성 기본 반응입니다.</summary>
        public NpcReaction FallbackReaction { get; }
        /// <summary>다음 입력 노드 식별자입니다. 종료 반응에서는 null입니다.</summary>
        public string NextNodeId { get; }
        /// <summary>다음 노드의 읽기 전용 선택지입니다. 종료 반응에서는 빈 목록입니다.</summary>
        public IReadOnlyList<NpcInteractionChoice> NextChoices { get; }
        /// <summary>다음 노드가 없어 이 반응으로 현재 상호작용 흐름이 종료되는지 나타냅니다.</summary>
        public bool IsTerminal => NextNodeId == null;

        /// <summary>출력 의미와 기본 반응을 묶고 사실 및 다음 선택지를 복사합니다.</summary>
        /// <param name="branchId">게임이 결정한 출력 분기 ID입니다.</param>
        /// <param name="intent">게임이 정한 한 줄 반응 의도입니다.</param>
        /// <param name="fallbackReaction">게임이 작성한 기본 대사와 감정 및 동작입니다.</param>
        /// <param name="facts">확정 사실 목록입니다. null은 빈 목록입니다.</param>
        /// <param name="nextNodeId">다음 입력 노드 ID입니다. 종료 시 null이며 다음 선택지와 함께 지정합니다.</param>
        /// <param name="nextChoices">다음 선택지 목록입니다. 다음 노드가 없으면 비어 있어야 합니다.</param>
        /// <exception cref="ArgumentNullException">기본 반응이 null인 경우입니다.</exception>
        /// <exception cref="ArgumentException">ID, 텍스트, 목록 또는 다음 노드와 선택지의 조합이 유효하지 않은 경우입니다.</exception>
        public NpcResponsePlan(string branchId, string intent, NpcReaction fallbackReaction,
            IEnumerable<string> facts = null, string nextNodeId = null,
            IEnumerable<NpcInteractionChoice> nextChoices = null)
        {
            BranchId = NpcInteractionContract.RequireId(branchId, nameof(branchId));
            Intent = NpcInteractionContract.RequireText(intent, nameof(intent), NpcInteractionContract.MaxContextEntryLength);
            FallbackReaction = fallbackReaction ?? throw new ArgumentNullException(nameof(fallbackReaction));
            Facts = NpcInteractionContract.CopyText(facts, nameof(facts),
                NpcInteractionContract.MaxFacts, NpcInteractionContract.MaxContextEntryLength);
            NextChoices = NpcInteractionContract.CopyChoices(nextChoices, nameof(nextChoices));
            if ((nextNodeId == null) != (NextChoices.Count == 0))
                throw new ArgumentException("다음 노드와 선택지는 함께 지정하거나 함께 비워야 합니다.", nameof(nextNodeId));
            NextNodeId = nextNodeId == null ? null : NpcInteractionContract.RequireId(nextNodeId, nameof(nextNodeId));
        }
    }
}
