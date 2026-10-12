using System;

namespace Jamkkaebi.Scripts.Gameplay.Npc.Interaction
{
    /// <summary>게임 로직이 사용 가능 여부를 판정한 입력 선택지입니다. 조건식은 이 데이터에 넣지 않습니다.</summary>
    public sealed class NpcInteractionChoice
    {
        /// <summary>입력 트리에서 정의한 행동 식별자입니다. 표시 이름과 구분합니다.</summary>
        public string ActionId { get; }
        /// <summary>선택지의 한 줄 표시 이름입니다. 최대 64 UTF-16 코드 단위입니다.</summary>
        public string Label { get; }
        /// <summary>게임이 이 선택지를 사용 가능하다고 판정했는지 나타냅니다.</summary>
        public bool IsEnabled { get; }
        /// <summary>비활성 선택지의 한 줄 불가 사유입니다. 활성 선택지에서는 null입니다.</summary>
        public string UnavailableReason { get; }

        /// <summary>라벨과 활성 여부에 따른 불가 사유의 일관성을 검사합니다.</summary>
        /// <param name="actionId">선택한 행동 ID입니다.</param>
        /// <param name="label">한 줄 선택지 이름입니다.</param>
        /// <param name="isEnabled">게임이 판정한 선택 가능 여부입니다.</param>
        /// <param name="unavailableReason">비활성일 때 필수인 한 줄 사유입니다. 활성일 때는 null이어야 합니다.</param>
        /// <exception cref="ArgumentException">ID, 라벨 또는 활성 여부와 불가 사유가 규약을 위반한 경우입니다.</exception>
        public NpcInteractionChoice(string actionId, string label, bool isEnabled = true,
            string unavailableReason = null)
        {
            ActionId = NpcInteractionContract.RequireId(actionId, nameof(actionId));
            Label = NpcInteractionContract.RequireText(label, nameof(label), NpcInteractionContract.MaxDisplayNameLength);
            if (isEnabled && unavailableReason != null)
                throw new ArgumentException("사용 가능한 선택지에는 불가 사유를 넣지 않습니다.", nameof(unavailableReason));
            UnavailableReason = isEnabled ? null : NpcInteractionContract.RequireText(unavailableReason,
                nameof(unavailableReason), NpcInteractionContract.MaxContextEntryLength);
            IsEnabled = isEnabled;
        }
    }
}
