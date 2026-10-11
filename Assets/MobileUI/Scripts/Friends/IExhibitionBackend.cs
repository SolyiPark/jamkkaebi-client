using System;
using System.Collections;

namespace MobilePrototype.Friends
{
    /// <summary>모의·HTTP 공급자가 공유하는 전시 전체 조회와 별도 방문 보상 명령의 경계입니다.</summary>
    public interface IExhibitionBackend
    {
        IEnumerator GetExhibition(string friendCode, Action<ExhibitionSnapshot> completed, Action<string> failed);
        IEnumerator ClaimVisitReward(string friendCode, Action<int> completed, Action<string> failed);
    }
}
