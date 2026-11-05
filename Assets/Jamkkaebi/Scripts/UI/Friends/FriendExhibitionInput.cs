using UnityEngine;
using UnityEngine.EventSystems;

namespace Jamkkaebi.Scripts.UI.Friends
{
    /// <summary>미러 입력의 RT 픽셀 좌표를 방문 전시물 선택으로 전달합니다.</summary>
    public sealed class FriendExhibitionInput : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private FriendExhibitionRebuilder _rebuilder;
        public void Configure(FriendExhibitionRebuilder rebuilder) => _rebuilder = rebuilder;
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_rebuilder) _rebuilder.SelectAtTexturePoint(eventData.position);
        }
    }
}
