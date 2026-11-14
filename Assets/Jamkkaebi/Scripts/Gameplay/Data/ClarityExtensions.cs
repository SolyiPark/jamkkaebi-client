namespace Jamkkaebi.Scripts.Gameplay.Data
{
    public static class ClarityExtensions
    {
        /// <summary>선명도를 되돌리려면 다시 시작해야 하는 페이즈. 선명하면 오염 관리만 반복한다.</summary>
        public static Phase RestoreFrom(this Clarity clarity) => clarity switch
        {
            Clarity.Faded => Phase.Recall,
            Clarity.Resealed => Phase.Unseal,
            _ => Phase.Guard
        };
    }
}