namespace Jamkkaebi.Scripts.Gameplay.Data
{
    /// <summary>
    /// 정체 공개 단계. 페이즈를 클리어할 때마다 오르고, 한 번 오르면 선명도가 떨어져도 내려가지 않는다.
    /// 시적 별칭과 시대는 획득 직후부터 항상 공개한다.
    /// </summary>
    /// <remarks>
    /// 서버 RevealLevel(SILHOUETTE, ALIAS, FORM, NAMED)과 규칙이 다르다. 서버 수정 후 대응을 다시 맞춘다.
    /// 오염 관리 클리어의 보상(전시관 배치 가능)은 공개 단계가 아니라 <see cref="RelicStatus.Completed"/>가 나타낸다.
    /// </remarks>
    public enum RevealLevel
    {
        Silhouette = 0, // 획득 직후: 실루엣, 별칭, 시대
        Form = 1,       // 봉인 해제 클리어: 흑백 형태
        Named = 2       // 기억 회복 클리어: 실제 이름, 풀컬러
    }
}