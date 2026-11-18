using System;
using System.Collections;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>
    /// 작업대·상자·정화의 서버 경계. 모의 구현과 HTTP 구현이 같은 계약을 따른다.
    /// 메서드마다 서버 API 하나에 대응한다.
    /// </summary>
    public interface IWorkbenchBackend
    {
        /// <summary>GET /api/workbench</summary>
        IEnumerator GetWorkbench(Action<WorkbenchSnapshot> completed, Action<string> failed);

        /// <summary>GET /api/relics/{relicId}</summary>
        IEnumerator GetRelicDetail(int relicId, Action<RelicDetail> completed, Action<string> failed);

        /// <summary>POST /api/box/open. requestId가 같으면 서버는 다시 뽑지 않고 같은 결과를 돌려준다.</summary>
        IEnumerator OpenBox(BoxSource source, string requestId, Action<BoxOpenOutcome> completed,
            Action<string> failed);

        /// <summary>POST /api/purifications. difficulty는 관리 플레이에서 고른 값, 최초 정화면 null.</summary>
        IEnumerator StartPurification(int relicId, MinigameDifficulty? difficulty,
            Action<PurificationSession> completed, Action<string> failed);

        /// <summary>POST /api/purifications/{sessionToken}/result</summary>
        IEnumerator ReportPurification(string sessionToken, PlayResult result, Action<SettlementResult> completed,
            Action<string> failed);
    }
}