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

        private readonly IWorkbenchRelicCatalog _catalog;
        private readonly IntimacyPolicy _intimacy;
        private readonly RestorePolicy _restore;

        public WorkbenchViewDataBuilder(IWorkbenchRelicCatalog catalog, IntimacyPolicy intimacy, RestorePolicy restore)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _intimacy = intimacy ?? throw new ArgumentNullException(nameof(intimacy));
            _restore = restore ?? throw new ArgumentNullException(nameof(restore));
        }

        public WorkbenchHomeData Home(WorkbenchSnapshot snapshot)
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

            string guide = snapshot.IsFull ? WorkbenchText.WorkbenchFull
                : slots.Count > 0 ? WorkbenchText.ChooseRelic
                : WorkbenchText.RegisterNewRelic;

            return new WorkbenchHomeData(slots.AsReadOnly(), spirits.AsReadOnly(), !snapshot.IsFull, guide,
                snapshot.LockedCount);
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

            List<DifficultyOption> difficulties = plan.AllowedDifficulties
                .Select(d => new DifficultyOption(d, _restore.EffectOf(d)))
                .ToList();

            return new SpiritDetailData(detail.RelicId, detail.SpiritName, info?.RealName ?? WorkbenchText.UnknownRelic,
                info?.EraName ?? WorkbenchText.UnknownEra, detail.Clarity.Value, _intimacy.GaugeOf(detail.CareCount),
                RestoreSteps(plan.RemainingPhases), WorkbenchText.DaysSincePurified(days),
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

        /// <summary>관리 복구 진행: 남은 순서에 없으면 완료, 남은 순서의 첫 페이즈는 현재, 나머지는 잠김.</summary>
        public static IReadOnlyList<PhaseStep> RestoreSteps(IReadOnlyList<Phase> remaining)
        {
            if (remaining == null) throw new ArgumentNullException(nameof(remaining));

            return AllPhases.Select(phase => new PhaseStep(phase,
                    !remaining.Contains(phase) ? PhaseStepState.Done
                    : phase == remaining[0] ? PhaseStepState.Current
                    : PhaseStepState.Locked))
                .ToList().AsReadOnly();
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
