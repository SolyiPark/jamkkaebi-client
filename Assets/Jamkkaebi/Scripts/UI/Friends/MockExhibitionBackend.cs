using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Friends
{
    /// <summary>독립 모의 구현의 응답 시나리오를 Unity에서 재현합니다. 숨김·미배치 콘텐츠는 패키지에서 제외합니다.</summary>
    public sealed class MockExhibitionBackend : IExhibitionBackend
    {
        private readonly ExhibitionPreviewScenario _scenario;
        private readonly FriendsPreviewState _state;
        /// <summary>모의 전시 시나리오와 보상 판정용 로컬 상태를 지정합니다.</summary>
        public MockExhibitionBackend(ExhibitionPreviewScenario scenario, FriendsPreviewState state = null) { _scenario = scenario; _state = state; }
        /// <summary>전시 조회와 분리한 방문 보상 명령으로 수령 결과를 전달합니다.</summary>
        public IEnumerator ClaimVisitReward(string code, Action<int> completed, Action<string> failed)
        {
            yield return new WaitForSecondsRealtime(.3f);
            var profile = _state?.Friends.Find(p => p.Code == code);
            if (profile == null) { failed("방문 보상을 받을 수 없어요."); yield break; }
            completed(_state.ClaimVisit(profile) ? 5 : 0);
        }
        /// <summary>대상 친구의 전시 스냅샷을 조회하고 성공 또는 오류 콜백으로 전달합니다.</summary>
        public IEnumerator GetExhibition(string code, Action<ExhibitionSnapshot> completed, Action<string> failed)
        {
            yield return new WaitForSecondsRealtime(_scenario == ExhibitionPreviewScenario.Slow ? 1.8f : .3f);
            if (_scenario == ExhibitionPreviewScenario.Denied) { failed("친구 전시를 방문할 수 없어요."); yield break; }
            if (_scenario == ExhibitionPreviewScenario.Error) { failed("전시를 불러오지 못했어요. 다시 시도해 주세요."); yield break; }
            if (_scenario == ExhibitionPreviewScenario.Malformed) { failed("전시 정보가 올바르지 않아요."); yield break; }
            completed(CreateSnapshot(code, _scenario));
        }
        /// <summary>방문 예외 시나리오에 맞는 모의 스냅샷과 임시 배치 목록을 만듭니다.</summary>
        public static ExhibitionSnapshot CreateSnapshot(string code, ExhibitionPreviewScenario scenario)
        {
            var rows = new List<SnapshotPlacement>();
            if (scenario == ExhibitionPreviewScenario.Empty) return new ExhibitionSnapshot(code, rows, false, capturedAt: null);
            rows.Add(new SnapshotPlacement(false, 1, "미소", new Vector2Int(1, 3)));
            rows.Add(new SnapshotPlacement(false, 2, "달이", new Vector2Int(5, 2), "FULLY_AWAKENED"));
            rows.Add(new SnapshotPlacement(false, 3, "푸른결", new Vector2Int(0, 1), "AWAKENED_PLUS"));
            rows.Add(new SnapshotPlacement(true, 1, "불국사 다보탑", new Vector2Int(3, 4)));
            rows.Add(new SnapshotPlacement(true, 2, "경복궁 근정전", new Vector2Int(6, 6), level: 2));
            if (scenario == ExhibitionPreviewScenario.Unknown) rows.Add(new SnapshotPlacement(true, 999, "새 건물", new Vector2Int(6, 0)));
            if (scenario == ExhibitionPreviewScenario.Invalid) rows.Add(new SnapshotPlacement(false, 4, "잘못된 배치", new Vector2Int(8, -1)));
            if (scenario == ExhibitionPreviewScenario.Duplicate) rows.Add(rows[0]);
            return new ExhibitionSnapshot(code, rows, width: scenario == ExhibitionPreviewScenario.Mismatch ? 20 : 7,
                height: scenario == ExhibitionPreviewScenario.Mismatch ? 20 : 7);
        }
    }
}
