namespace Jamkkaebi.Scripts.Gameplay.Data
{
    public static class ClarityExtensions
    {
        /// <summary>
        /// 이 선명도에서 관리 플레이로 하는 페이즈. 재봉인은 봉인 해제, 흐려짐은 기억 회복, 선명은 오염 관리.
        /// </summary>
        public static Phase RestoreFrom(this Clarity clarity) => clarity switch
        {
            Clarity.Faded => Phase.Recall,
            Clarity.Resealed => Phase.Unseal,
            _ => Phase.Guard
        };

        /// <summary>복구 한 단계로 오르는 선명도. 재봉인 → 흐려짐 → 선명, 선명은 그대로.</summary>
        public static Clarity StepUp(this Clarity clarity) => clarity switch
        {
            Clarity.Resealed => Clarity.Faded,
            _ => Clarity.Clear
        };

        /// <summary>시간이 지나 다음에 떨어질 선명도. 선명 → 흐려짐 → 재봉인, 재봉인은 그대로.</summary>
        public static Clarity StepDown(this Clarity clarity) => clarity switch
        {
            Clarity.Clear => Clarity.Faded,
            _ => Clarity.Resealed
        };
    }
}
