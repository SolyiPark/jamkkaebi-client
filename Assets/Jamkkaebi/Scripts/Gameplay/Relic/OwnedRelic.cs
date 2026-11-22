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

        /// <summary>마지막으로 정화에 성공한 시각("마지막 정화로부터 n일째"). 완료 전에는 null.</summary>
        public DateTime? LastPurifiedAt { get; private set; }

        /// <summary>선명도를 계산하는 기준 시각. 복구 단계를 깨면 선명도가 한 칸 오르도록 당겨진다. 완료 전에는 null.</summary>
        /// <remarks>마지막 정화 시각보다 늦지 않다(흐려짐으로 오르면 마지막 정화 - 흐려짐 시간).</remarks>
        public DateTime? ClarityAnchorAt { get; private set; }

        /// <summary>관리 플레이 성공 횟수. 친밀도 계산에 쓴다.</summary>
        public int CareCount { get; private set; }

        public bool IsCompleted => Status == RelicStatus.Completed;
        
        public RevealLevel RevealLevel => RevealLevelExtensions.From(Status, CurrentPhase);

        public bool ShowsSpiritName => IsCompleted;

        /// <summary>저장된 상태(모의 백엔드, 서버 응답)로 다시 만든다.</summary>
        /// <exception cref="ArgumentOutOfRangeException">relicId가 1 미만이거나 careCount가 음수인 경우.</exception>
        /// <exception cref="ArgumentException">클라이언트에 정의되지 않은 열거형이 입력되거나, 상태 조합이 맞지 않는 경우.</exception>
        public OwnedRelic(int relicId, DateTime acquiredAt, RelicStatus status, Phase currentPhase,
                          DateTime? lastPurifiedAt, DateTime? clarityAnchorAt, int careCount)
        {
            if (relicId <= 0) throw new ArgumentOutOfRangeException(nameof(relicId));
            if (!Enum.IsDefined(typeof(RelicStatus), status)) throw new ArgumentException("존재하지 않는 status 종류입니다.", nameof(status));
            if (!Enum.IsDefined(typeof(Phase), currentPhase)) throw new ArgumentException("존재하지 않는 phase 종류입니다.", nameof(currentPhase));
            if (careCount < 0) throw new ArgumentOutOfRangeException(nameof(careCount));

            if (status == RelicStatus.InProgress)
            {
                if (lastPurifiedAt.HasValue) throw new ArgumentException("최초 정화 진행 중인 유물에는 마지막 정화 시각이 존재하지 않습니다.", nameof(lastPurifiedAt));
                if (clarityAnchorAt.HasValue) throw new ArgumentException("최초 정화 진행 중인 유물에는 선명도 기준 시각이 존재하지 않습니다.", nameof(clarityAnchorAt));
                if (careCount > 0) throw new ArgumentException("최초 정화 진행 중인 유물에는 관리 성공 횟수가 존재하지 않습니다.", nameof(careCount));
            }
            else
            {
                if (currentPhase != Phase.Guard) throw new ArgumentException("최초 정화가 완료된 유물의 진행 단계는 오염 관리보다 낮을 수 없습니다.", nameof(currentPhase));
                if (!lastPurifiedAt.HasValue) throw new ArgumentException("최초 정화가 완료된 유물은 마지막 정화 시각이 있어야 합니다.", nameof(lastPurifiedAt));
                if (!clarityAnchorAt.HasValue) throw new ArgumentException("최초 정화가 완료된 유물은 선명도 기준 시각이 있어야 합니다.", nameof(clarityAnchorAt));
                if (clarityAnchorAt.Value > lastPurifiedAt.Value)
                    throw new ArgumentException("선명도 기준 시각은 마지막 정화 시각보다 늦을 수 없습니다.", nameof(clarityAnchorAt));
            }

            RelicId = relicId;
            AcquiredAt = acquiredAt;
            Status = status;
            CurrentPhase = currentPhase;
            LastPurifiedAt = lastPurifiedAt;
            ClarityAnchorAt = clarityAnchorAt;
            CareCount = careCount;
        }

        /// <summary>상자에서 새로 획득한 유물. 봉인 해제부터 시작한다.</summary>
        public static OwnedRelic Acquired(int relicId, DateTime acquiredAt)
        {
            return new OwnedRelic(relicId, acquiredAt, RelicStatus.InProgress, Phase.Unseal, null, null, 0);
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
            ClarityAnchorAt = now;
            return true;
        }

        /// <summary>
        /// 관리 플레이 한 판 성공. 돌봄 횟수를 하나 올리고, 마지막 정화 시각과 선명도 기준 시각을 새로 정한다.
        /// 몇 칸 오를지(기준 시각)는 정산이 난이도와 선명도 정책으로 정해서 넘긴다.
        /// </summary>
        /// <param name="clarityAnchorAt">이번 판으로 정해진 선명도 기준 시각(<see cref="ClarityPolicy.AnchorFor"/>).</param>
        /// <exception cref="InvalidOperationException">최초 정화가 완료되지 않은 경우.</exception>
        /// <exception cref="ArgumentOutOfRangeException">기준 시각이 지금보다 늦은 경우.</exception>
        public void ClearCarePlay(DateTime clarityAnchorAt, DateTime now)
        {
            // 상태를 바꾸기 전에 모두 검사한다. 저장 후 생성자가 거부할 상태를 만들지 않는다.
            RequireCompleted();
            if (clarityAnchorAt > now)
                throw new ArgumentOutOfRangeException(nameof(clarityAnchorAt), "선명도 기준 시각은 지금보다 늦을 수 없습니다.");

            CareCount++;
            LastPurifiedAt = now;
            ClarityAnchorAt = clarityAnchorAt;
        }

        private void RequireCompleted()
        {
            if (!IsCompleted) throw new InvalidOperationException("최초 정화가 완료되지 않은 유물은 관리할 수 없습니다.");
        }
    }
}