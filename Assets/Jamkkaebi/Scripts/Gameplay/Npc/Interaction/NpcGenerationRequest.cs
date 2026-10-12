using System;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Interaction
{
    /// <summary>공유 추론 서비스에 보내는 완전한 요청입니다. Context는 게임 효과 적용 후의 스냅샷입니다.</summary>
    public sealed class NpcGenerationRequest
    {
        /// <summary>요청이 사용하는 공통 데이터 규약 버전입니다.</summary>
        public int ContractVersion => NpcInteractionContract.Version;
        /// <summary>한 생성 시도의 식별자입니다. 어댑터가 연결하며 모델이 생성하지 않습니다.</summary>
        public string RequestId { get; }
        /// <summary>게임에서 승인한 입력입니다. 기대 버전은 효과 적용 전 상태를 가리킵니다.</summary>
        public NpcInteractionSelection Selection { get; }
        /// <summary>동일 NPC 상태의 불변 스냅샷입니다. 생성 요청에서는 효과 적용 후 상태입니다.</summary>
        public NpcContextSnapshot Context { get; }
        /// <summary>게임이 효과 후 상태에서 선택한 출력 분기와 기본 반응 계획입니다.</summary>
        public NpcResponsePlan Plan { get; }
        /// <summary>서비스 접수부터 준비, 대기, 추론, 검증까지의 총 예산입니다. 실행 기한은 서비스가 단조 시간으로 관리합니다.</summary>
        public int TimeBudgetMilliseconds { get; }

        /// <summary>같은 NPC의 효과 후 상태와 양의 시간 예산을 갖는 생성 요청을 만듭니다.</summary>
        /// <param name="requestId">어댑터가 부여한 생성 시도 ID입니다.</param>
        /// <param name="selection">승인된 입력입니다. 입력 검증이나 효과 적용을 대신하지 않습니다.</param>
        /// <param name="context">불변 NPC 상태입니다. 요청에서는 입력과 같은 NPC의 효과 적용 후 상태여야 합니다.</param>
        /// <param name="plan">같은 효과 후 상태에서 선택한 출력 계획입니다.</param>
        /// <param name="timeBudgetMilliseconds">서비스 접수부터 적용할 양의 총 시간 예산입니다.</param>
        /// <exception cref="ArgumentNullException">선택, 상태 또는 계획이 null인 경우입니다.</exception>
        /// <exception cref="ArgumentException">ID가 유효하지 않거나 선택과 상태의 NPC 또는 버전이 맞지 않는 경우입니다.</exception>
        /// <exception cref="ArgumentOutOfRangeException">총 시간 예산이 양수가 아닌 경우입니다.</exception>
        public NpcGenerationRequest(string requestId, NpcInteractionSelection selection,
            NpcContextSnapshot context, NpcResponsePlan plan, int timeBudgetMilliseconds)
        {
            RequestId = NpcInteractionContract.RequireId(requestId, nameof(requestId));
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            if (selection.NpcId != context.NpcId || context.StateVersion < selection.ExpectedStateVersion)
                throw new ArgumentException("선택과 같은 NPC의 효과 적용 후 상태가 필요합니다.", nameof(context));
            if (timeBudgetMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(timeBudgetMilliseconds));
            TimeBudgetMilliseconds = timeBudgetMilliseconds;
        }

        /// <summary>세션, 입력 의도, NPC, 상태 버전의 일치를 검사합니다. 기한과 완료 여부 및 활성 요청 ID는 별도 검사합니다.</summary>
        /// <param name="sessionId">현재 화면 세션 ID입니다.</param>
        /// <param name="interactionId">한 입력 의도의 ID입니다.</param>
        /// <param name="npcId">도깨비 인스턴스 ID입니다.</param>
        /// <param name="stateVersion">비교할 상호작용 의미 상태 버전입니다.</param>
        /// <returns>모든 식별자와 상태 버전이 일치하면 true입니다.</returns>
        public bool IsCurrent(string sessionId, string interactionId, string npcId, long stateVersion)
        {
            return Selection.SessionId == sessionId && Selection.InteractionId == interactionId &&
                Context.NpcId == npcId && Context.StateVersion == stateVersion;
        }
    }
}
