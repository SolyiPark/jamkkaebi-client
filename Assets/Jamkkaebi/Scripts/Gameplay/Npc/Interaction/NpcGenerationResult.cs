using System;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Interaction
{
    /// <summary>어댑터가 요청 ID에 연결한 추론 결과입니다. 모델 출력 자체에는 ID·게임 상태·다음 분기가 없습니다.</summary>
    public sealed class NpcGenerationResult
    {
        /// <summary>한 생성 시도의 식별자입니다. 어댑터가 연결하며 모델이 생성하지 않습니다.</summary>
        public string RequestId { get; }
        /// <summary>어댑터가 확정한 생성 완료 상태입니다. 취소와 유효한 실패를 구분합니다.</summary>
        public NpcGenerationStatus Status { get; }
        /// <summary>성공 시 검증된 한 줄 완성 대사이며, 다른 상태에서는 null입니다.</summary>
        public string Dialogue { get; }

        /// <summary>완료 상태를 검사하고 성공 결과에만 완성 대사를 보관합니다.</summary>
        /// <param name="requestId">어댑터가 부여한 생성 시도 ID입니다.</param>
        /// <param name="status">정의된 생성 완료 상태입니다.</param>
        /// <param name="dialogue">한 줄 완성 대사입니다. 결과에서는 성공일 때만 지정하며 실패일 때 null이어야 합니다.</param>
        /// <exception cref="ArgumentException">ID 또는 대사가 유효하지 않거나 실패 결과에 대사를 포함한 경우입니다.</exception>
        /// <exception cref="ArgumentOutOfRangeException">정의되지 않은 상태인 경우입니다.</exception>
        public NpcGenerationResult(string requestId, NpcGenerationStatus status, string dialogue = null)
        {
            RequestId = NpcInteractionContract.RequireId(requestId, nameof(requestId));
            if (!Enum.IsDefined(typeof(NpcGenerationStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            Status = status;
            if (status == NpcGenerationStatus.Succeeded)
                Dialogue = NpcInteractionContract.RequireText(dialogue, nameof(dialogue), NpcInteractionContract.MaxDialogueLength);
            else if (dialogue != null)
                throw new ArgumentException("미완성 대사는 실패 결과에 포함하지 않습니다.", nameof(dialogue));
        }
    }
}
