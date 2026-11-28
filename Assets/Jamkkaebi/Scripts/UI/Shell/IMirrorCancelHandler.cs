namespace Jamkkaebi.Scripts.UI.Shell
{
    /// <summary>
    /// 누름을 받은 오브젝트가 구현하면, 손을 떼지 않았는데 입력이 끊길 때(포커스 상실, 탭 전환, 대상 교체 등)
    /// MirrorInput이 누름 종료(pointerUp)를 보내기 전에 먼저 알려 준다.
    /// 누름 종료만으로는 실제로 손을 뗀 것과 취소를 구분할 수 없어, 놓는 순간에 결과가 정해지는 제스처가 쓴다.
    /// </summary>
    public interface IMirrorCancelHandler
    {
        void OnMirrorCancel();
    }
}
