using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MobilePrototype.Friends
{
    /// <summary>독립 모의 구현의 응답 시나리오를 Unity에서 재현합니다. 숨김·미배치 콘텐츠는 패키지에서 제외합니다.</summary>
    public sealed class MockExhibitionBackend : IExhibitionBackend
    {
        private readonly ExhibitionPreviewScenario _scenario;
        private readonly FriendsPreviewState _state;
        /// <summary>Selects a snapshot scenario and optional local state used for reward claims.</summary>
        public MockExhibitionBackend(ExhibitionPreviewScenario scenario, FriendsPreviewState state = null) { _scenario = scenario; _state = state; }
        /// <summary>Waits 0.3 real-time seconds, then reports five for the first local claim or zero for a repeated claim; missing state or friends invoke failed.</summary>
        public IEnumerator ClaimVisitReward(string code, Action<int> completed, Action<string> failed)
        {
            yield return new WaitForSecondsRealtime(.3f);
            var profile = _state?.Friends.Find(p => p.Code == code);
            if (profile == null) { failed("방문 보상을 받을 수 없어요."); yield break; }
            completed(_state.ClaimVisit(profile) ? 5 : 0);
        }
        /// <summary>Returns the selected synthetic snapshot after 0.3 real-time seconds, or 1.8 for Slow; Denied, Error, and Malformed invoke failed without claiming rewards.</summary>
        public IEnumerator GetExhibition(string code, Action<ExhibitionSnapshot> completed, Action<string> failed)
        {
            yield return new WaitForSecondsRealtime(_scenario == ExhibitionPreviewScenario.Slow ? 1.8f : .3f);
            if (_scenario == ExhibitionPreviewScenario.Denied) { failed("친구 전시를 방문할 수 없어요."); yield break; }
            if (_scenario == ExhibitionPreviewScenario.Error) { failed("전시를 불러오지 못했어요. 다시 시도해 주세요."); yield break; }
            if (_scenario == ExhibitionPreviewScenario.Malformed) { failed("전시 정보가 올바르지 않아요."); yield break; }
            completed(CreateSnapshot(code, _scenario));
        }
        /// <summary>Creates synthetic placements for the selected scenario, including empty, invalid, duplicate, unknown, or mismatched-grid data for preview handling.</summary>
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
