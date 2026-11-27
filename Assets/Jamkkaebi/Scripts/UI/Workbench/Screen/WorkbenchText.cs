using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>작업대 화면에 보이는 고정 문구. enum 이름을 화면에 그대로 쓰지 않는다.</summary>
    public static class WorkbenchText
    {
        public const string UnknownRelic = "알 수 없는 유물";
        public const string UnknownEra = "시대 미상";
        public const string ChooseRelic = "복원을 진행할 유물을 선택하세요.";
        public const string RegisterNewRelic = "신규 미확인 유물을 등록하세요.";
        public const string WorkbenchFull = "작업대가 가득 찼어요. 진행 중인 유물을 먼저 정화하세요.";
        public const string PendingBox = "결과를 받지 못한 상자가 있어요. 등록 버튼을 눌러 결과를 확인하세요.";
        public const string RegisterButton = "신규 미확인\n유물 등록";
        public const string CompleteAllToDisplay = "모든 단계를 완료하면 전시관에 배치할 수 있습니다.";
        public const string PurifyButton = "유물 정화하기";
        public const string BuildingsComingSoon = "건물은 준비 중이에요.";

        /// <summary>페이즈 화면 이름. 3페이즈는 "오염 관리"(enum은 서버와 같은 Guard).</summary>
        public static string PhaseName(Phase phase) => phase switch
        {
            Phase.Unseal => "봉인 해제",
            Phase.Recall => "기억 회복",
            Phase.Guard => "오염 관리",
            _ => phase.ToString()
        };

        public static string ClarityName(Clarity clarity) => clarity switch
        {
            Clarity.Clear => "선명",
            Clarity.Faded => "흐려짐",
            Clarity.Resealed => "재봉인",
            _ => clarity.ToString()
        };

        public static string DifficultyName(MinigameDifficulty difficulty) => difficulty switch
        {
            MinigameDifficulty.Easy => "쉬움",
            MinigameDifficulty.Normal => "보통",
            MinigameDifficulty.Hard => "어려움",
            _ => difficulty.ToString()
        };

        public static string RestoreEffectText(RestoreEffect effect) => effect switch
        {
            RestoreEffect.RefreshOnly => "선명도만 회복",
            RestoreEffect.OneStep => "한 단계 복구",
            RestoreEffect.Full => "전체 복구",
            _ => effect.ToString()
        };

        /// <summary>"마지막 정화로부터 n일째". 같은 날(24시간 안)이면 "오늘 정화했어요".</summary>
        public static string DaysSincePurified(int days) =>
            days <= 0 ? "오늘 정화했어요" : $"마지막 정화로부터 {days}일째";

        public static string PurifyPhaseButton(Phase phase) => $"{PurifyButton} - {PhaseName(phase)}";

        /// <summary>선명까지 남은 복구 단계(보통 난이도로 한 판씩). 선명이면 null.</summary>
        public static string StepsToClear(Clarity clarity) => clarity switch
        {
            Clarity.Resealed => "선명까지 두 단계 남았어요",
            Clarity.Faded => "선명까지 한 단계 남았어요",
            _ => null
        };

        /// <summary>"n일 뒤 흐려짐 상태가 돼요". 하루가 안 남았으면(0) "하루 안에".</summary>
        public static string ClarityChangeIn(int days, Clarity next) =>
            days <= 0 ? $"하루 안에 {ClarityName(next)} 상태가 돼요" : $"{days}일 뒤 {ClarityName(next)} 상태가 돼요";
    }
}
