using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 폴리오미노 한 종류의 그림 묶음. 형상 전체 미리보기와 칸별 조각으로 이루어진다.
    /// </summary>
    /// <remarks>
    /// <see cref="Cells"/>의 i번째 스프라이트는 <see cref="PolyominoShapes.Definitions"/>에서 같은 형상의 i번째 칸에 대응한다.
    /// 회전해도 칸 인덱스는 유지되므로, 조각은 기본 방향으로 그리고 표시할 때 회전시킨다.
    /// </remarks>
    [Serializable]
    public class PolyominoSpriteSet
    {
        [SerializeField] private PolyominoShape _shape;
        [Tooltip("형상 전체 그림. 찾아야 할 형상 목록 등 UI 미리보기에 쓴다.")]
        [SerializeField] private Sprite _preview;
        [Tooltip("칸별 조각. 순서는 PolyominoShapes에 정의된 칸 인덱스와 같아야 한다.")]
        [SerializeField] private Sprite[] _cells;

        public PolyominoShape Shape => _shape;
        public Sprite Preview => _preview;
        public IReadOnlyList<Sprite> Cells => _cells ?? Array.Empty<Sprite>();
    }
}
