using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 타일 덮개가 깨진 순간의 정보. 깨짐 연출과 타일 종류별 연출이 쓴다.
    /// </summary>
    public readonly struct TileBreakInfo
    {
        /// <summary>깨진 타일. 연출 시점에는 이미 열린 그림(또는 강화가 벗겨진 덮개)으로 바뀌어 있다.</summary>
        public TileView View { get; }
        /// <summary>깨지기 직전 덮개 그림.</summary>
        public Sprite CoverSprite { get; }
        /// <summary>깨지기 직전 덮개 색. 그림이 없는 매개체는 이 색으로 덮개를 표시한다.</summary>
        public Color CoverColor { get; }
        /// <summary>true면 덮개가 완전히 깨져 내용물이 열렸고, false면 강화만 벗겨졌다.</summary>
        public bool IsFullBreak { get; }
        /// <summary>타일을 깬 도구.</summary>
        public ToolMode Tool { get; }
        /// <summary>타일 내용물. 강화만 벗겨졌을 때는 아직 보이지 않으므로 연출에 쓰지 않는다.</summary>
        public TileContent Content { get; }
        /// <summary>판이 끝난 뒤 정답 공개로 깨졌으면 true. 플레이어 행동이 아니므로 위협·보너스 연출을 하지 않는다.</summary>
        public bool IsEndReveal { get; }

        public TileBreakInfo(TileView view, Sprite coverSprite, Color coverColor, bool isFullBreak, ToolMode tool,
            TileContent content, bool isEndReveal)
        {
            View = view;
            CoverSprite = coverSprite;
            CoverColor = coverColor;
            IsFullBreak = isFullBreak;
            Tool = tool;
            Content = content;
            IsEndReveal = isEndReveal;
        }
    }
}
