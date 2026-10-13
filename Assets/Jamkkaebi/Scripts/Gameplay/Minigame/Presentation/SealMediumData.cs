using Jamkkaebi.Scripts.Gameplay.Data;
using UnityEngine;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    [CreateAssetMenu(fileName = "SealMediumData", menuName = "Jamkkaebi/Seal Medium Data")]
    public class SealMediumData : ScriptableObject
    {
        [Header("봉인 컨셉")]
        [SerializeField] private SealMedium _sealMedium;
        
        // Color는 아트 에셋 제작 이후 Sprite 타입으로 변경
        [Header("타일")]
        [SerializeField] private Color _tileColor;
        [SerializeField] private Color _reinforcedTileColor;
        
        [Header("배경")]
        [SerializeField] private Color _backgroundColor;
        
        public SealMedium SealMedium => _sealMedium;
        public Color TileColor => _tileColor;
        public Color ReinforcedTileColor => _reinforcedTileColor;
        public Color BackgroundColor => _backgroundColor;
    }
}