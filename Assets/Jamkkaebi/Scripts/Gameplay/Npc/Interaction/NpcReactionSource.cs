namespace Jamkkaebi.Scripts.Gameplay.Npc.Interaction
{
    /// <summary>최종 대사가 생성 결과인지 게임 작성 기본 대사인지 구분합니다.</summary>
    public enum NpcReactionSource
    {
        /// <summary>검증을 통과한 생성 대사를 사용하는 반응입니다.</summary>
        Generated = 0,
        /// <summary>게임이 선택한 같은 분기의 기본 대사를 사용하는 반응입니다.</summary>
        Fallback = 1
    }
}
