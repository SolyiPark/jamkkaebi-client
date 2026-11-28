using System;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>놓을 대상을 고르는 목록의 한 줄. 목록은 Key와 Label만 알고 무엇을 놓는지는 모른다.</summary>
    public sealed class HousingPlacementOption
    {
        public string Key { get; }
        public string Label { get; }

        /// <exception cref="ArgumentException">key가 비어 있는 경우.</exception>
        public HousingPlacementOption(string key, string label)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("선택지 키가 필요합니다.", nameof(key));
            Key = key; Label = string.IsNullOrEmpty(label) ? key : label;
        }
    }
}
