namespace Jamkkaebi.Scripts.Gameplay.Npc.Interaction
{
    /// <summary>추론의 최종 결과입니다. 취소는 반응을 표시하지 않으며 다른 실패는 유효한 요청에서 기본 반응을 사용합니다.</summary>
    public enum NpcGenerationStatus
    {
        /// <summary>검증된 완성 대사입니다. 게임이 정한 분기와 표현을 유지합니다.</summary>
        Succeeded = 0,
        /// <summary>총 시간 예산 초과입니다. 현재 요청이면 기본 반응을 사용합니다.</summary>
        TimedOut = 1,
        /// <summary>모델 미설치 또는 미준비입니다. 현재 요청이면 기본 반응을 사용합니다.</summary>
        ModelUnavailable = 2,
        /// <summary>추론 실행 오류입니다. 현재 요청이면 기본 반응을 사용합니다.</summary>
        Failed = 3,
        /// <summary>출력 형식 또는 의미 검증 실패입니다. 현재 요청이면 기본 반응을 사용합니다.</summary>
        InvalidOutput = 4,
        /// <summary>공유 서비스의 작업 접수 한도 초과입니다.</summary>
        QueueFull = 5,
        /// <summary>창 종료나 대상 전환 등으로 취소되었습니다. 기본 반응도 새로 표시하지 않습니다.</summary>
        Cancelled = 6
    }
}
