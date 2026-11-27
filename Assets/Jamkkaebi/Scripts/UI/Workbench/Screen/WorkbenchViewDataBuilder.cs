using System;
using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>
    /// 백엔드 응답을 화면에 그릴 값으로 바꾼다. 순수 계산이라 EditMode 테스트 대상이다.
    /// 모르는 relicId는 예외 없이 "알 수 없는 유물"로 보여 준다(서버에 새 유물이 먼저 생긴 경우).
    /// </summary>
    public sealed class WorkbenchViewDataBuilder
    {
        private static readonly Phase[] AllPhases = { Phase.Unseal, Phase.Recall, Phase.Guard };
        private static readonly Clarity[] AllClarities = { Clarity.Resealed, Clarity.Faded, Clarity.Clear };

        private readonly IWorkbenchRelicCatalog _catalog;
        private readonly IntimacyPolicy _intimacy;
        private readonly RestorePolicy _restore;

        public WorkbenchViewDataBuilder(IWorkbenchRelicCatalog catalog, IntimacyPolicy intimacy, RestorePolicy restore)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _intimacy = intimacy ?? throw new ArgumentNullException(nameof(intimacy));
            _restore = restore ?? throw new ArgumentNullException(nameof(restore));
        }

        /// <param name="hasPendingBox">결과를 받지 못한 상자 열기 요청이 있는지. 있으면 작업대가 가득 차도 등록 버튼을 남겨
        /// 같은 요청 번호로 결과를 확인하게 한다(서버는 같은 번호면 가득 참 검사 전에 처음 결과를 돌려준다).</param>
        public WorkbenchHomeData Home(WorkbenchSnapshot snapshot, bool hasPendingBox = false)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            List<SlotCardData> slots = snapshot.Slots.Select(slot =>
            {
                bool known = _catalog.TryFind(slot.RelicId, out WorkbenchRelicInfo info);
                return new SlotCardData(slot.RelicId, slot.RevealLevel, TitleOf(info, slot.RevealLevel),
                    known ? info.EraName : WorkbenchText.UnknownEra, slot.CurrentPhase, known);
            }).ToList();

            List<SpiritCardData> spirits = snapshot.Spirits.Select(spirit =>
                new SpiritCardData(spirit.RelicId, spirit.SpiritName, RealNameOf(spirit.RelicId), spirit.Clarity)).ToList();

            string guide = hasPendingBox ? WorkbenchText.PendingBox
                : snapshot.IsFull ? WorkbenchText.WorkbenchFull
                : slots.Count > 0 ? WorkbenchText.ChooseRelic
                : WorkbenchText.RegisterNewRelic;

            return new WorkbenchHomeData(slots.AsReadOnly(), spirits.AsReadOnly(), !snapshot.IsFull || hasPendingBox, guide,
                snapshot.LockedCount);
        }

        /// <summary>
        /// 상자 결과 한 줄. 새 유물은 정체가 드러나기 전이라 별칭을, 중복은 받은 재화를 보인다.
        /// </summary>
        public string BoxResult(BoxOpenOutcome outcome)
        {
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));

            string spent = outcome.JeongseongSpent > 0 ? $" · 정성 -{outcome.JeongseongSpent}" : "";
            if (outcome.Result == AcquireResult.New)
            {
                string alias = _catalog.TryFind(outcome.RelicId, out WorkbenchRelicInfo info)
                    ? info.PoeticAlias : WorkbenchText.UnknownRelic;
                return $"새 유물을 등록했어요 · {alias}{spent}";
            }
            return $"이미 가진 유물이 나왔어요 · 정성 +{outcome.JeongseongGained} · 결정 +{outcome.CrystalGained}{spent}";
        }

        /// <summary>정산 결과를 결과 화면 문장으로 바꾼다. 다음 페이즈는 정산이 새로 만든 계획을 따른다.</summary>
        public SettlementData Settlement(int relicId, SettlementResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            bool succeeded = result.Result == PlayResult.Success;
            List<string> lines = new List<string>();
            _catalog.TryFind(relicId, out WorkbenchRelicInfo info);

            if (result.Revealed == RevealLevel.Form) lines.Add("유물의 형태가 드러났어요.");
            if (result.Revealed == RevealLevel.Named)
                lines.Add($"정체가 드러났어요 · {info?.RealName ?? WorkbenchText.UnknownRelic}");

            if (result.CompletedNow)
            {
                lines.Add("최초 정화를 모두 마쳤어요. 정령이 깨어났어요!");
                lines.Add("이제 전시관에 배치할 수 있어요.");
            }
            else
            {
                switch (result.Next)
                {
                    case NextAction.NextPhase:
                        lines.Add($"다음 단계 · {WorkbenchText.PhaseName(result.NextPlan.Phase)}");
                        break;
                    case NextAction.ContinueRestore:
                        // 관리 계획에는 선명도가 있다. 이번 판으로 오른 선명도다.
                        lines.Add($"{WorkbenchText.ClarityName(result.NextPlan.Clarity.Value)}까지 회복했어요.");
                        lines.Add($"남은 복구 · {WorkbenchText.PhaseName(result.NextPlan.Phase)}");
                        break;
                    case NextAction.Retry:
                        lines.Add($"다시 도전하세요 · {WorkbenchText.PhaseName(result.NextPlan.Phase)}");
                        break;
                    case NextAction.Done:
                        lines.Add("정령이 다시 선명해졌어요.");
                        break;
                }
            }

            return new SettlementData(relicId, succeeded, succeeded ? "정화 성공" : "정화 실패", lines.AsReadOnly(),
                result.NextPlan == null);
        }

        /// <summary>최초 정화 중인 유물의 상세. 완료한 정령은 SpiritDetail을 쓴다.</summary>
        public RelicDetailData RelicDetail(RelicDetail detail)
        {
            if (detail == null) throw new ArgumentNullException(nameof(detail));
            if (detail.Status != RelicStatus.InProgress)
                throw new ArgumentException("최초 정화 중인 유물이 아닙니다. SpiritDetail을 쓰세요.", nameof(detail));

            // 진행 중이면 다음 계획의 페이즈가 곧 다음에 할 페이즈다.
            Phase current = detail.NextPlay.Phase;
            RevealLevel reveal = RevealLevelExtensions.From(detail.Status, current);
            _catalog.TryFind(detail.RelicId, out WorkbenchRelicInfo info);

            return new RelicDetailData(detail.RelicId, reveal, TitleOf(info, reveal),
                info?.EraName ?? WorkbenchText.UnknownEra, FirstRouteSteps(current), WorkbenchText.PurifyButton);
        }

        /// <summary>최초 정화를 마친 정령의 상세.</summary>
        public SpiritDetailData SpiritDetail(RelicDetail detail, DateTime now)
        {
            if (detail == null) throw new ArgumentNullException(nameof(detail));
            if (detail.Status != RelicStatus.Completed)
                throw new ArgumentException("최초 정화를 마친 정령이 아닙니다. RelicDetail을 쓰세요.", nameof(detail));

            _catalog.TryFind(detail.RelicId, out WorkbenchRelicInfo info);
            PlayPlan plan = detail.NextPlay;

            // 완료한 정령은 RelicDetail 생성자가 선명도와 마지막 정화 시각을 보장한다.
            int days = DaysBetween(detail.LastPurifiedAt.Value, now);
            Clarity clarity = detail.Clarity.Value;

            List<DifficultyOption> difficulties = plan.AllowedDifficulties
                .Select(d => new DifficultyOption(d, _restore.EffectOf(d)))
                .ToList();

            return new SpiritDetailData(detail.RelicId, detail.SpiritName, info?.RealName ?? WorkbenchText.UnknownRelic,
                info?.EraName ?? WorkbenchText.UnknownEra, clarity, _intimacy.GaugeOf(detail.CareCount),
                ClaritySteps(clarity), ClarityNotice(clarity, detail.ClarityChangesAt, now), WorkbenchText.DaysSincePurified(days),
                WorkbenchText.PurifyPhaseButton(plan.Phase), difficulties.AsReadOnly());
        }

        /// <summary>최초 정화 진행: 지금 페이즈 앞은 완료, 지금은 현재, 뒤는 잠김.</summary>
        public static IReadOnlyList<PhaseStep> FirstRouteSteps(Phase current)
        {
            return AllPhases.Select(phase => new PhaseStep(phase,
                    phase < current ? PhaseStepState.Done
                    : phase == current ? PhaseStepState.Current
                    : PhaseStepState.Locked))
                .ToList().AsReadOnly();
        }

        /// <summary>복구 진행: 재봉인 → 흐려짐 → 선명 중 지금 선명도가 현재, 그보다 나쁜 칸은 완료, 좋은 칸은 잠김.</summary>
        public static IReadOnlyList<ClarityStep> ClaritySteps(Clarity current)
        {
            // Clarity 값은 선명 0 → 재봉인 2라 클수록 나쁘다. 화면은 나쁜 쪽부터 보인다.
            return AllClarities.Select(clarity => new ClarityStep(clarity,
                    clarity > current ? PhaseStepState.Done
                    : clarity == current ? PhaseStepState.Current
                    : PhaseStepState.Locked))
                .ToList().AsReadOnly();
        }

        /// <summary>
        /// 선명까지 남은 단계와 다음 변화 안내. 재봉인은 더 떨어지지 않으므로 남은 단계만 알린다.
        /// 남은 날은 24시간 단위 올림이다(방금 오른 정령은 7일 - 몇 초가 남아도 "7일 뒤"). 하루가 안 남았으면 "하루 안에"라고 쓴다.
        /// </summary>
        public static string ClarityNotice(Clarity clarity, DateTime? changesAt, DateTime now)
        {
            string remaining = WorkbenchText.StepsToClear(clarity);
            if (clarity == Clarity.Resealed || !changesAt.HasValue) return remaining;

            double days = (changesAt.Value - now).TotalDays;
            string change = WorkbenchText.ClarityChangeIn(days < 1 ? 0 : (int)Math.Ceiling(days), clarity.StepDown());
            return remaining == null ? change : $"{remaining} · {change}";
        }

        /// <summary>마지막 정화부터 지난 날수(24시간 단위, 내림). 기기 시각이 앞서 있으면 0.</summary>
        public static int DaysBetween(DateTime lastPurifiedAt, DateTime now)
        {
            double days = (now - lastPurifiedAt).TotalDays;
            return days <= 0 ? 0 : (int)Math.Floor(days);
        }

        private static string TitleOf(WorkbenchRelicInfo info, RevealLevel reveal)
        {
            if (info == null) return WorkbenchText.UnknownRelic;
            return reveal.ShowsRealName() ? info.RealName : info.PoeticAlias;
        }

        private string RealNameOf(int relicId) =>
            _catalog.TryFind(relicId, out WorkbenchRelicInfo info) ? info.RealName : WorkbenchText.UnknownRelic;
    }
}
