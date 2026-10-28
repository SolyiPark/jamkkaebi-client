using System;
using System.Collections;

namespace Jamkkaebi.Scripts.UI.Friends
{
    /// <summary>모의·HTTP 공급자가 공유하는 전시 전체 조회와 별도 방문 보상 명령의 경계입니다.</summary>
    public interface IExhibitionBackend
    {
        /// <summary>Returns a coroutine that retrieves a friend exhibition through completed or reports a failure through failed, separately from reward claiming.</summary>
        IEnumerator GetExhibition(string friendCode, Action<ExhibitionSnapshot> completed, Action<string> failed);
        /// <summary>Returns a coroutine that claims a visit reward and reports the gained amount through completed or a failure through failed.</summary>
        IEnumerator ClaimVisitReward(string friendCode, Action<int> completed, Action<string> failed);
    }
}
