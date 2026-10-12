namespace Jamkkaebi.Scripts.Gameplay.Npc.Interaction
{
    /// <summary>표시용 대사와 게임이 선택한 감정·동작 ID입니다. 게임 효과나 이동 명령은 포함하지 않습니다.</summary>
    public sealed class NpcReaction
    {
        /// <summary>화면에 일반 텍스트로 표시할 한 줄 대사입니다. 최대 160 UTF-16 코드 단위입니다.</summary>
        public string Dialogue { get; }
        /// <summary>게임이 선택한 감정 표현 식별자입니다. 모델 출력으로 변경하지 않습니다.</summary>
        public string EmotionId { get; }
        /// <summary>게임이 선택한 동작 표현 식별자입니다. 게임 효과 또는 이동 명령이 아닙니다.</summary>
        public string GestureId { get; }

        /// <summary>한 줄 대사와 게임이 선택한 감정 및 동작 ID를 검사하여 반응을 만듭니다.</summary>
        /// <param name="dialogue">한 줄 완성 대사입니다. 결과에서는 성공일 때만 지정하며 실패일 때 null이어야 합니다.</param>
        /// <param name="emotionId">게임 콘텐츠의 감정 표현 ID입니다.</param>
        /// <param name="gestureId">게임 콘텐츠의 동작 표현 ID입니다.</param>
        /// <exception cref="System.ArgumentException">대사 또는 표현 ID가 규약을 위반한 경우입니다.</exception>
        public NpcReaction(string dialogue, string emotionId, string gestureId)
        {
            Dialogue = NpcInteractionContract.RequireText(dialogue, nameof(dialogue),
                NpcInteractionContract.MaxDialogueLength);
            EmotionId = NpcInteractionContract.RequireId(emotionId, nameof(emotionId));
            GestureId = NpcInteractionContract.RequireId(gestureId, nameof(gestureId));
        }
    }
}
