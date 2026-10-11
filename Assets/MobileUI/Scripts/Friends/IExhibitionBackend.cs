using System;
using System.Collections;

namespace MobilePrototype.Friends
{
    /// <summary>전시 전체를 조회하는 경계. 현재 구현은 네트워크를 사용하지 않는 모의 공급자입니다.</summary>
    public interface IExhibitionBackend
    {
        /// <summary>Returns a coroutine that retrieves a friend exhibition through completed or reports a failure through failed, separately from reward claiming.</summary>
        IEnumerator GetExhibition(string friendCode, Action<ExhibitionSnapshot> completed, Action<string> failed);
        /// <summary>Returns a coroutine that claims a visit reward and reports the gained amount through completed or a failure through failed.</summary>
        IEnumerator ClaimVisitReward(string friendCode, Action<int> completed, Action<string> failed);
    }
}
