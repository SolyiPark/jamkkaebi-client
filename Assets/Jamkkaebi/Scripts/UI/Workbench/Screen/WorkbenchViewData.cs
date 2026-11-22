using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Data;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.Gameplay.Relic;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>3단계 진행 표시 한 칸의 상태.</summary>
    public enum PhaseStepState
    {
        Done = 0,    // 체크 표시
        Current = 1, // 이번에 할 페이즈
        Locked = 2   // 자물쇠
    }

    /// <summary>3단계 진행 표시 한 칸.</summary>
    public readonly struct PhaseStep
    {
        public Phase Phase { get; }
        public string Name { get; }
        public PhaseStepState State { get; }

        public PhaseStep(Phase phase, PhaseStepState state)
        {
            Phase = phase;
            Name = WorkbenchText.PhaseName(phase);
            State = state;
        }
    }

    /// <summary>정령 상세의 선명도 단계 한 칸(재봉인 → 흐려짐 → 선명). 지금 선명도가 현재, 그 아래는 완료, 위는 잠김.</summary>
    public readonly struct ClarityStep
    {
        public Clarity Clarity { get; }
        public string Name { get; }
        public PhaseStepState State { get; }

        public ClarityStep(Clarity clarity, PhaseStepState state)
        {
            Clarity = clarity;
            Name = WorkbenchText.ClarityName(clarity);
            State = state;
        }
    }

    /// <summary>홈 상단의 진행 중 유물 한 칸.</summary>
    public sealed class SlotCardData
    {
        public int RelicId { get; }
        public RevealLevel RevealLevel { get; }

        /// <summary>카드에 보일 이름. 기억 회복 전에는 별칭, 뒤에는 실제 이름.</summary>
        public string Title { get; }

        public string EraName { get; }
        public Phase NextPhase { get; }
        public bool IsKnown { get; }

        public SlotCardData(int relicId, RevealLevel revealLevel, string title, string eraName, Phase nextPhase, bool isKnown)
        {
            RelicId = relicId;
            RevealLevel = revealLevel;
            Title = title;
            EraName = eraName;
            NextPhase = nextPhase;
            IsKnown = isKnown;
        }
    }

    /// <summary>홈 하단 정령 목록의 카드 한 장.</summary>
    public sealed class SpiritCardData
    {
        public int RelicId { get; }
        public string SpiritName { get; }
        public string RelicName { get; }
        public Clarity Clarity { get; }
        public string ClarityName { get; }

        public SpiritCardData(int relicId, string spiritName, string relicName, Clarity clarity)
        {
            RelicId = relicId;
            SpiritName = spiritName;
            RelicName = relicName;
            Clarity = clarity;
            ClarityName = WorkbenchText.ClarityName(clarity);
        }
    }

    /// <summary>작업대 홈 화면.</summary>
    public sealed class WorkbenchHomeData
    {
        public IReadOnlyList<SlotCardData> Slots { get; }
        public IReadOnlyList<SpiritCardData> Spirits { get; }

        /// <summary>"신규 미확인 유물 등록" 버튼을 보일지(빈 슬롯이 있을 때만).</summary>
        public bool CanRegister { get; }

        public string Guide { get; }
        public int LockedCount { get; }

        public WorkbenchHomeData(IReadOnlyList<SlotCardData> slots, IReadOnlyList<SpiritCardData> spirits,
            bool canRegister, string guide, int lockedCount)
        {
            Slots = slots;
            Spirits = spirits;
            CanRegister = canRegister;
            Guide = guide;
            LockedCount = lockedCount;
        }
    }

    /// <summary>관리 플레이에서 고를 수 있는 난이도 하나.</summary>
    public readonly struct DifficultyOption
    {
        public MinigameDifficulty Difficulty { get; }
        public string Name { get; }
        public string EffectText { get; }

        public DifficultyOption(MinigameDifficulty difficulty, RestoreEffect effect)
        {
            Difficulty = difficulty;
            Name = WorkbenchText.DifficultyName(difficulty);
            EffectText = WorkbenchText.RestoreEffectText(effect);
        }
    }

    /// <summary>최초 정화 중인 유물의 상세.</summary>
    public sealed class RelicDetailData
    {
        public int RelicId { get; }
        public RevealLevel RevealLevel { get; }
        public string Title { get; }
        public string EraName { get; }
        public string Notice { get; }
        public IReadOnlyList<PhaseStep> Steps { get; }
        public string PurifyButton { get; }

        public RelicDetailData(int relicId, RevealLevel revealLevel, string title, string eraName,
            IReadOnlyList<PhaseStep> steps, string purifyButton)
        {
            RelicId = relicId;
            RevealLevel = revealLevel;
            Title = title;
            EraName = eraName;
            Notice = WorkbenchText.CompleteAllToDisplay;
            Steps = steps;
            PurifyButton = purifyButton;
        }
    }

    /// <summary>최초 정화를 마친 정령의 상세.</summary>
    public sealed class SpiritDetailData
    {
        public int RelicId { get; }
        public string SpiritName { get; }
        public string RelicName { get; }
        public string EraName { get; }
        public Clarity Clarity { get; }
        public string ClarityName { get; }
        public IntimacyGauge Intimacy { get; }

        /// <summary>복구 진행. 선명도 세 칸(재봉인 → 흐려짐 → 선명) 중 지금 선명도가 현재.</summary>
        public IReadOnlyList<ClarityStep> ClaritySteps { get; }

        /// <summary>선명까지 남은 단계와 다음에 선명도가 바뀌는 때. 예: "선명까지 한 단계 남았어요 · 7일 뒤 재봉인 상태가 돼요".</summary>
        public string ClarityNotice { get; }

        public string DaysSincePurified { get; }
        public string PurifyButton { get; }

        /// <summary>고를 수 있는 난이도. 하나뿐이면 화면은 고르지 않고 그 난이도로 시작한다.</summary>
        public IReadOnlyList<DifficultyOption> Difficulties { get; }

        public bool DifficultySelectable => Difficulties.Count > 1;

        public SpiritDetailData(int relicId, string spiritName, string relicName, string eraName, Clarity clarity,
            IntimacyGauge intimacy, IReadOnlyList<ClarityStep> claritySteps, string clarityNotice,
            string daysSincePurified, string purifyButton, IReadOnlyList<DifficultyOption> difficulties)
        {
            RelicId = relicId;
            SpiritName = spiritName;
            RelicName = relicName;
            EraName = eraName;
            Clarity = clarity;
            ClarityName = WorkbenchText.ClarityName(clarity);
            Intimacy = intimacy;
            ClaritySteps = claritySteps;
            ClarityNotice = clarityNotice;
            DaysSincePurified = daysSincePurified;
            PurifyButton = purifyButton;
            Difficulties = difficulties;
        }
    }

    /// <summary>정화 한 판의 정산 결과 화면.</summary>
    public sealed class SettlementData
    {
        public int RelicId { get; }
        public bool Succeeded { get; }
        public string Title { get; }
        public IReadOnlyList<string> Lines { get; }

        /// <summary>더 할 판이 없는지(최초 정화 완료 또는 관리 완료). 확인을 누르면 홈으로 간다.</summary>
        public bool Finished { get; }

        public SettlementData(int relicId, bool succeeded, string title, IReadOnlyList<string> lines, bool finished)
        {
            RelicId = relicId;
            Succeeded = succeeded;
            Title = title;
            Lines = lines;
            Finished = finished;
        }
    }
}
