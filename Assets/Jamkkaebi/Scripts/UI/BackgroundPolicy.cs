namespace Jamkkaebi.Scripts.UI
{
    /// <summary>탭이 화면에서 숨겨졌을 때 그 탭 씬을 어떻게 다룰지 정한다.</summary>
    public enum BackgroundPolicy
    {
        ContinueRunning,
        PauseWhileHidden,
        RestartOnReturn
    }
}
