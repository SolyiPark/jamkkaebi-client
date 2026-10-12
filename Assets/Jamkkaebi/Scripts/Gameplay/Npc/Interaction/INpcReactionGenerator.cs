using System.Threading;
using System.Threading.Tasks;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Interaction
{
    /// <summary>
    /// 실제·모의 생성기가 공통으로 구현할 비동기 경계입니다. NPC별 모델 생성 대신 하나의 공유 서비스로 연결합니다.
    /// 준비·대기·추론·검증을 시간 예산에 포함하고 취소된 작업의 결과는 적용하지 않습니다.
    /// </summary>
    public interface INpcReactionGenerator
    {
        /// <summary>공유 모델의 준비, 대기, 추론, 검증을 포함해 대사를 비동기로 생성합니다. 게임 스레드를 차단하지 않아야 합니다.</summary>
        /// <param name="request">대상 생성 요청입니다.</param>
        /// <param name="cancellationToken">창 종료나 대상 전환 등을 알리는 취소 토큰입니다.</param>
        /// <returns>요청 ID에 연결된 최종 결과의 Task입니다. 성공은 검증된 대사, 실패는 상태만 반환합니다.</returns>
        /// <exception cref="System.OperationCanceledException">구현이 취소 상태 반환 대신 Task 취소로 종료한 경우입니다. 호출 측은 표시 없이 처리해야 합니다.</exception>
        Task<NpcGenerationResult> GenerateAsync(NpcGenerationRequest request, CancellationToken cancellationToken);
    }
}
