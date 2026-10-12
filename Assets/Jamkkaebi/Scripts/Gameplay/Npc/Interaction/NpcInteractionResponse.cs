using System;
using System.Collections.Generic;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Interaction
{
    /// <summary>프론트엔드에 전달할 최종 반응입니다. 게임 상태 변경은 포함하지 않습니다.</summary>
    public sealed class NpcInteractionResponse
    {
        /// <summary>한 생성 시도의 식별자입니다. 어댑터가 연결하며 모델이 생성하지 않습니다.</summary>
        public string RequestId { get; }
        /// <summary>화면을 열거나 대상을 전환할 때 새로 발급하는 상호작용 세션 식별자입니다.</summary>
        public string SessionId { get; }
        /// <summary>한 입력 의도의 식별자입니다. 같은 입력을 재전달할 때 유지합니다.</summary>
        public string InteractionId { get; }
        /// <summary>도깨비 인스턴스 식별자입니다. 종류 식별자와 구분하며 대소문자를 보존합니다.</summary>
        public string NpcId { get; }
        /// <summary>스냅샷의 상호작용 의미 상태 버전입니다. 관련 상태가 바뀔 때 소유자가 갱신합니다.</summary>
        public long StateVersion { get; }
        /// <summary>게임 출력 트리가 선택한 반응 분기 식별자입니다. 모델은 변경하지 않습니다.</summary>
        public string BranchId { get; }
        /// <summary>생성 대사 또는 기본 대사와 게임이 정한 감정 및 동작을 결합한 최종 반응입니다.</summary>
        public NpcReaction Reaction { get; }
        /// <summary>최종 대사가 검증된 생성 결과인지 게임의 기본 반응인지 나타냅니다.</summary>
        public NpcReactionSource Source { get; }
        /// <summary>최종 반응을 구성한 생성 결과 상태입니다.</summary>
        public NpcGenerationStatus GenerationStatus { get; }
        /// <summary>다음 입력 노드 식별자입니다. 종료 반응에서는 null입니다.</summary>
        public string NextNodeId { get; }
        /// <summary>다음 노드의 읽기 전용 선택지입니다. 종료 반응에서는 빈 목록입니다.</summary>
        public IReadOnlyList<NpcInteractionChoice> NextChoices { get; }

        /// <summary>일치 검사에 통과한 결과에서 대사만 교체하고 게임이 정한 분기와 표현을 보존합니다.</summary>
        /// <param name="request">대상 생성 요청입니다.</param>
        /// <param name="result">어댑터가 요청 ID에 연결한 완료 결과입니다.</param>
        private NpcInteractionResponse(NpcGenerationRequest request, NpcGenerationResult result)
        {
            RequestId = request.RequestId;
            SessionId = request.Selection.SessionId;
            InteractionId = request.Selection.InteractionId;
            NpcId = request.Context.NpcId;
            StateVersion = request.Context.StateVersion;
            BranchId = request.Plan.BranchId;
            GenerationStatus = result.Status;
            Source = result.Status == NpcGenerationStatus.Succeeded ? NpcReactionSource.Generated : NpcReactionSource.Fallback;
            NpcReaction fallback = request.Plan.FallbackReaction;
            Reaction = Source == NpcReactionSource.Generated
                ? new NpcReaction(result.Dialogue, fallback.EmotionId, fallback.GestureId) : fallback;
            NextNodeId = request.Plan.NextNodeId;
            NextChoices = request.Plan.NextChoices;
        }

        /// <summary>현재 요청의 성공 또는 기본 반응을 구성합니다. 취소와 오래된 결과는 거부하며 기한과 완료 잠금은 호출 측이 검사합니다.</summary>
        /// <param name="request">대상 생성 요청입니다.</param>
        /// <param name="result">어댑터가 요청 ID에 연결한 완료 결과입니다.</param>
        /// <param name="activeRequestId">현재 활성 생성 시도 ID입니다. 종료 또는 교체된 요청을 차단합니다.</param>
        /// <param name="activeSessionId">현재 표시 대상 세션 ID입니다.</param>
        /// <param name="activeInteractionId">현재 접수된 입력 의도 ID입니다.</param>
        /// <param name="activeNpcId">현재 표시 대상 도깨비 인스턴스 ID입니다.</param>
        /// <param name="currentStateVersion">현재 게임의 효과 적용 후 의미 상태 버전입니다.</param>
        /// <param name="response">검사 통과 시 최종 반응이며, 거부 시 null입니다.</param>
        /// <returns>현재 요청의 취소되지 않은 결과를 조립했으면 true, 오래되거나 취소된 결과이면 false입니다.</returns>
        /// <exception cref="ArgumentNullException">요청 또는 결과가 null인 경우입니다.</exception>
        public static bool TryResolve(NpcGenerationRequest request, NpcGenerationResult result,
            string activeRequestId, string activeSessionId, string activeInteractionId, string activeNpcId, long currentStateVersion,
            out NpcInteractionResponse response)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (result == null) throw new ArgumentNullException(nameof(result));
            response = null;
            if (result.RequestId != request.RequestId || request.RequestId != activeRequestId ||
                result.Status == NpcGenerationStatus.Cancelled ||
                !request.IsCurrent(activeSessionId, activeInteractionId, activeNpcId, currentStateVersion))
                return false;
            response = new NpcInteractionResponse(request, result);
            return true;
        }
    }
}
