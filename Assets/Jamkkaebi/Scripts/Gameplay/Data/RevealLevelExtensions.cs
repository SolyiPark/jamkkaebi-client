namespace Jamkkaebi.Scripts.Gameplay.Data
{
    public static class RevealLevelExtensions
    {
        /// <summary>기억 회복을 마쳐 실제 이름과 풀컬러를 보여도 되는지.</summary>
        public static bool ShowsRealName(this RevealLevel level) => level == RevealLevel.Named;

        /// <summary>진행도로 공개 단계를 정한다.</summary>
        /// <param name="status">완료 여부.</param>
        /// <param name="currentPhase">최초 정화에서 다음에 할 페이즈. Completed면 무시한다.</param>
        public static RevealLevel From(RelicStatus status, Phase currentPhase)
        {
            if (status == RelicStatus.Completed) return RevealLevel.Named;
            return currentPhase switch
            {
                Phase.Recall => RevealLevel.Form,  // 봉인 해제를 마침
                Phase.Guard => RevealLevel.Named,  // 기억 회복을 마침
                _ => RevealLevel.Silhouette
            };
        }
    }
}