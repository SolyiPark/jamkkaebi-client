using System;
using Jamkkaebi.Scripts.Gameplay.Data;

namespace Jamkkaebi.Scripts.Gameplay.Relic
{
    /// <summary>
    /// 보유 유물 한 종에 대한 내 상태. 서버 UserRelic과 같은 규칙이다.
    /// 유물 종류 정보(그림·봉인 매개체)는 RelicData가 갖고, 여기서는 RelicId로만 연결한다.
    /// </summary>
    /// <remarks>모든 시각은 UTC다.</remarks>
    public sealed class OwnedRelic
    {
        public int RelicId { get; }
        public DateTime AcquiredAt { get; }

        public RelicStatus Status { get; private set; }

        /// <summary>최초 정화에서 다음에 할 페이즈. 완료 후에는 Guard에 머문다.</summary>
        public Phase CurrentPhase { get; private set; }

        /// <summary>마지막으로 선명도가 회복된 시각. 완료 전에는 null.</summary>
        public DateTime? LastPurifiedAt { get; private set; }

        /// <summary>이번 복구 순서에서 마지막으로 클리어한 페이즈. 복구 중이 아니면 null.</summary>
        public Phase? RestorePhase { get; private set; }
        
        /// <summary>이번 복구를 시작할 때의 선명도. RestorePhase와 함께 존재하고, 복구 중이 아니면 null.</summary>
        public Clarity? RestoreClarity { get; private set; }

        /// <summary>관리 플레이 성공 횟수. 친밀도 계산에 쓴다.</summary>
        public int CareCount { get; private set; }

        public bool IsCompleted => Status == RelicStatus.Completed;
        
        public RevealLevel RevealLevel => RevealLevelExtensions.From(Status, CurrentPhase);

        public bool ShowsSpiritName => IsCompleted;

        /// <summary>저장된 상태(모의 백엔드, 서버 응답)로 다시 만든다.</summary>
        /// <exception cref="ArgumentOutOfRangeException">relicId가 1 미만이거나 careCount가 음수인 경우.</exception>
        /// <exception cref="ArgumentException">클라이언트에 정의되지 않은 열거형이 입력되거나, 상태 조합이 맞지 않는 경우.</exception>
        public OwnedRelic(int relicId, DateTime acquiredAt, RelicStatus status, Phase currentPhase,
                          DateTime? lastPurifiedAt, Phase? restorePhase, Clarity? restoreClarity, int careCount)
        {
            if (relicId <= 0) throw new ArgumentOutOfRangeException(nameof(relicId));
            if (!Enum.IsDefined(typeof(RelicStatus), status)) throw new ArgumentException("존재하지 않는 status 종류입니다.", nameof(status));
            if (!Enum.IsDefined(typeof(Phase), currentPhase)) throw new ArgumentException("존재하지 않는 phase 종류입니다.", nameof(currentPhase));
            if (restorePhase.HasValue && !Enum.IsDefined(typeof(Phase), restorePhase.Value))
                throw new ArgumentException("존재하지 않는 복구 페이즈 종류입니다.", nameof(restorePhase));
            if (restoreClarity.HasValue && !Enum.IsDefined(typeof(Clarity), restoreClarity.Value))
                throw new ArgumentException("존재하지 않는 복구 시작 선명도 종류입니다.", nameof(restoreClarity));
            if (careCount < 0) throw new ArgumentOutOfRangeException(nameof(careCount));

            if (status == RelicStatus.InProgress)
            {
                if (restorePhase.HasValue) throw new ArgumentException("최초 정화 진행 중인 유물에는 복원 페이즈가 존재하지 않습니다.", nameof(restorePhase));
                if (lastPurifiedAt.HasValue) throw new ArgumentException("최초 정화 진행 중인 유물에는 마지막 정화 시각이 존재하지 않습니다.", nameof(lastPurifiedAt));
                if (careCount > 0) throw new ArgumentException("최초 정화 진행 중인 유물에는 관리 성공 횟수가 존재하지 않습니다.", nameof(careCount));
            }
            else
            {
                if (currentPhase != Phase.Guard) throw new ArgumentException("최초 정화가 완료된 유물의 진행 단계는 오염 관리보다 낮을 수 없습니다.", nameof(currentPhase));
                if (!lastPurifiedAt.HasValue) throw new ArgumentException("최초 정화가 완료된 유물은 마지막 정화 시각이 있어야 합니다.", nameof(lastPurifiedAt));
                if (restorePhase == Phase.Guard) throw new ArgumentException("마지막 단계(오염 관리)는 복구 페이즈일 수 없습니다.", nameof(restorePhase));
            }

            if (restorePhase.HasValue ^ restoreClarity.HasValue)
                throw new ArgumentException("마지막 복원 페이즈와 복원 시작 선명도는 함께 존재하거나 존재하지 않아야 합니다. 현재 한 쪽만 존재합니다.");
            if (restoreClarity == Clarity.Clear) throw new ArgumentException("선명한 상태에서는 복구를 진행 할 수 없습니다.", nameof(restoreClarity));
            if (restorePhase.HasValue && !IsMiddleOfRestore(restorePhase.Value, restoreClarity.Value))
                throw new ArgumentException("복구 시작 선명도의 복구 순서에 없는 페이즈입니다.", nameof(restorePhase));

            RelicId = relicId;
            AcquiredAt = acquiredAt;
            Status = status;
            CurrentPhase = currentPhase;
            LastPurifiedAt = lastPurifiedAt;
            RestorePhase = restorePhase;
            RestoreClarity = restoreClarity;
            CareCount = careCount;
        }

        /// <summary>상자에서 새로 획득한 유물. 봉인 해제부터 시작한다.</summary>
        public static OwnedRelic Acquired(int relicId, DateTime acquiredAt)
        {
            return new OwnedRelic(relicId, acquiredAt, RelicStatus.InProgress, Phase.Unseal, null, null, null, 0);
        }

        /// <summary>최초 정화 한 판 성공. 마지막 페이즈였으면 완료한다.</summary>
        /// <remarks>서버 UserRelic.clearFirstPhase에 해당한다.</remarks>
        /// <returns>이번 성공으로 완료됐으면 true.</returns>
        public bool ClearFirstPlay(DateTime now)
        {
            if (Status != RelicStatus.InProgress) throw new InvalidOperationException("최초 정화가 완료된 유물은 다시 완료할 수 없습니다.");

            if (CurrentPhase.Next() is Phase next)
            {
                CurrentPhase = next;
                return false;
            }
            
            Status = RelicStatus.Completed;
            LastPurifiedAt = now;
            return true;
        }

        /// <summary>관리 플레이 한 판 성공으로 한 단계 복구한다. 순서의 마지막 페이즈면 선명도를 회복한다.</summary>
        /// <param name="phase">이 판에서 클리어한 페이즈.</param>
        /// <param name="clarity">이 판을 플레이한 선명도.</param>
        /// <exception cref="InvalidOperationException">최초 정화가 완료되지 않은 경우.</exception>
        /// <exception cref="ArgumentException">
        /// 정의되지 않은 열거형이거나, 중간 페이즈가 그 선명도의 복구 순서에 없는 경우(선명에서 중간 페이즈, 흐려짐에서 봉인 해제 등).
        /// </exception>
        public void ClearCarePlay(Phase phase, Clarity clarity, DateTime now)
        {
            // 상태를 바꾸기 전에 모두 검사한다. 저장 후 생성자가 거부할 상태를 만들지 않는다.
            RequireCompleted();
            if (!Enum.IsDefined(typeof(Phase), phase)) throw new ArgumentException("존재하지 않는 phase 종류입니다.", nameof(phase));
            if (!Enum.IsDefined(typeof(Clarity), clarity)) throw new ArgumentException("존재하지 않는 선명도 종류입니다.", nameof(clarity));
            if (!phase.IsLast() && !IsMiddleOfRestore(phase, clarity))
                throw new ArgumentException("이 선명도의 복구 순서에 없는 페이즈입니다.", nameof(phase));

            CareCount++;

            if (phase.IsLast())
            {
                Refresh(now);
                return;
            }

            RestorePhase = phase;
            RestoreClarity = clarity;
        }

        /// <summary>
        /// 지금 선명도로 복구할 때 이미 클리어했다고 인정하는 마지막 페이즈. 인정할 진행이 없으면 null.
        /// </summary>
        /// <remarks>
        /// 복구를 시작한 선명도와 지금 선명도가 같을 때만 저장된 진행을 이어 간다.
        /// 다르면(복구 도중 더 흐려졌거나 재봉인된 경우) 지난 진행을 버리고 null을 돌려준다.
        /// null은 "지금 선명도가 정한 순서의 첫 판부터"라는 뜻이며, 몇 판인지는 선명도가 정한다
        /// (선명 1판, 흐려짐 2판, 재봉인 3판. <see cref="ClarityPolicy.RemainingPhases"/> 참고).
        /// </remarks>
        /// <param name="current">지금 선명도.</param>
        /// <returns><see cref="ClarityPolicy.RemainingPhases"/>의 lastClearedPhase로 넘길 값.</returns>
        public Phase? RestorePhaseFor(Clarity current)
        {
            return RestoreClarity == current ? RestorePhase : null;
        }

        /// <summary>관리 플레이 한 판 성공으로 바로 선명까지 복구한다.</summary>
        /// <exception cref="InvalidOperationException">최초 정화가 완료되지 않은 경우.</exception>
        public void RestoreFully(DateTime now)
        {
            RequireCompleted();
            CareCount++;
            Refresh(now);
        }

        private void RequireCompleted()
        {
            if (!IsCompleted) throw new InvalidOperationException("최초 정화가 완료되지 않은 유물은 관리할 수 없습니다.");
        }

        // 그 선명도의 복구 순서에서 마지막(오염 관리)이 아닌 페이즈인지. 선명이면 중간 페이즈가 없다.
        private static bool IsMiddleOfRestore(Phase phase, Clarity clarity)
        {
            return clarity != Clarity.Clear
                   && !phase.IsLast()
                   && phase.Order() >= clarity.RestoreFrom().Order();
        }

        // 선명도 회복: 마지막 정화 시각 갱신, 복구 진행 비우기
        private void Refresh(DateTime now)
        {
            LastPurifiedAt = now;
            RestorePhase = null;
            RestoreClarity = null;
        }
    }
}