using System;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Interaction
{
    /// <summary>프론트엔드가 보낸 제한된 입력입니다. 게임은 접수 시 다시 검증하고 효과를 한 번만 적용합니다.</summary>
    public sealed class NpcInteractionSelection
    {
        /// <summary>화면을 열거나 대상을 전환할 때 새로 발급하는 상호작용 세션 식별자입니다.</summary>
        public string SessionId { get; }
        /// <summary>한 입력 의도의 식별자입니다. 같은 입력을 재전달할 때 유지합니다.</summary>
        public string InteractionId { get; }
        /// <summary>도깨비 인스턴스 식별자입니다. 종류 식별자와 구분하며 대소문자를 보존합니다.</summary>
        public string NpcId { get; }
        /// <summary>선택지를 제공한 입력 트리 노드 식별자입니다.</summary>
        public string NodeId { get; }
        /// <summary>입력 트리에서 정의한 행동 식별자입니다. 표시 이름과 구분합니다.</summary>
        public string ActionId { get; }
        /// <summary>화면이 선택지를 표시할 때 본 효과 적용 전 상태 버전입니다.</summary>
        public long ExpectedStateVersion { get; }

        /// <summary>식별자와 효과 전 기대 버전을 검사하여 제한된 입력을 만듭니다.</summary>
        /// <param name="sessionId">현재 화면 세션 ID입니다.</param>
        /// <param name="interactionId">한 입력 의도의 ID입니다.</param>
        /// <param name="npcId">도깨비 인스턴스 ID입니다.</param>
        /// <param name="nodeId">입력 트리 노드 ID입니다.</param>
        /// <param name="actionId">선택한 행동 ID입니다.</param>
        /// <param name="expectedStateVersion">화면이 본 효과 적용 전의 0 이상 상태 버전입니다.</param>
        /// <exception cref="ArgumentException">ID가 규약을 위반한 경우입니다.</exception>
        /// <exception cref="ArgumentOutOfRangeException">기대 버전이 음수인 경우입니다.</exception>
        public NpcInteractionSelection(string sessionId, string interactionId, string npcId,
            string nodeId, string actionId, long expectedStateVersion)
        {
            SessionId = NpcInteractionContract.RequireId(sessionId, nameof(sessionId));
            InteractionId = NpcInteractionContract.RequireId(interactionId, nameof(interactionId));
            NpcId = NpcInteractionContract.RequireId(npcId, nameof(npcId));
            NodeId = NpcInteractionContract.RequireId(nodeId, nameof(nodeId));
            ActionId = NpcInteractionContract.RequireId(actionId, nameof(actionId));
            if (expectedStateVersion < 0) throw new ArgumentOutOfRangeException(nameof(expectedStateVersion));
            ExpectedStateVersion = expectedStateVersion;
        }
    }
}
