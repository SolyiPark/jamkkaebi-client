namespace Jamkkaebi.Scripts.Gameplay.Relic
{
    /// <summary>친밀도 게이지 표시값. 돌봄 횟수에서 계산하며 저장하지 않는다.</summary>
    public readonly struct IntimacyGauge
    {
        public int Level { get; }
        public int Current { get; }
        public int Max { get; }
        
        public bool IsMax => Max == 0;

        public float Fill => IsMax ? 1f : (float)Current / Max;

        public IntimacyGauge(int level, int current, int max)
        {
            Level = level;
            Current = current;
            Max = max;
        }
    }
}