namespace Jamkkaebi.Scripts.UI.Shell
{
    // 조건부 제스처 소유자입니다. 명시적으로 입력을 잡기 전까지 MirrorInput은 드래그 시작을 보류합니다.
    public interface IMirrorGestureOwner
    {
        bool OwnsGesture { get; }
    }
}
