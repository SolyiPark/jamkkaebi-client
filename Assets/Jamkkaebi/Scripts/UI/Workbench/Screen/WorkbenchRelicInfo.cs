using System;
using Jamkkaebi.Scripts.Gameplay.Data;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>작업대 화면에 쓰는 유물 하나의 고정 정보(서버 유물 마스터 데이터와 같은 내용).</summary>
    public sealed class WorkbenchRelicInfo
    {
        public int RelicId { get; }
        
        /// <summary>도감(CollectionCatalog)의 유물 종류 ID. 최초 정화를 마치면 이 ID로 도감에 등록한다.</summary>
        public string CollectionId { get; }

        /// <summary>시대 표시명. 예: "삼국".</summary>
        public string EraName { get; }

        /// <summary>정체가 드러나기 전에 보여 주는 시적 별칭. 예: "백제, 향이 스민 산맥".</summary>
        public string PoeticAlias { get; }

        /// <summary>실제 유물 이름. 기억 회복을 마친 뒤 공개한다.</summary>
        public string RealName { get; }

        /// <summary>사용자가 이름을 짓지 않았을 때의 정령 이름.</summary>
        public string DefaultSpiritName { get; }
        
        public SealMedium SealMedium { get; }

        public WorkbenchRelicInfo(int relicId, string collectionId, string eraName, string poeticAlias, string realName, string defaultSpiritName, SealMedium sealMedium)
        {
            if (relicId <= 0) throw new ArgumentOutOfRangeException(nameof(relicId), "relicId는 1부터 시작합니다.");
            if (string.IsNullOrEmpty(collectionId)) throw new ArgumentException("도감 ID가 비어 있습니다.", nameof(collectionId));
            if (string.IsNullOrEmpty(eraName)) throw new ArgumentException("시대 이름이 비어 있습니다.", nameof(eraName));
            if (string.IsNullOrEmpty(poeticAlias)) throw new ArgumentException("별칭이 비어 있습니다.", nameof(poeticAlias));
            if (string.IsNullOrEmpty(realName)) throw new ArgumentException("유물 이름이 비어 있습니다.", nameof(realName));
            if (string.IsNullOrEmpty(defaultSpiritName))
                throw new ArgumentException("기본 정령 이름이 비어 있습니다.", nameof(defaultSpiritName));
            if (!Enum.IsDefined(typeof(SealMedium), sealMedium))
                throw new ArgumentException($"SealMedium {sealMedium}은 정의되지 않았습니다.", nameof(sealMedium));

            RelicId = relicId;
            CollectionId = collectionId;
            EraName = eraName;
            PoeticAlias = poeticAlias;
            RealName = realName;
            DefaultSpiritName = defaultSpiritName;
            SealMedium = sealMedium;
        }
    }

    /// <summary>relicId로 유물 고정 정보를 찾는다. 모르는 유물이면 false.</summary>
    public interface IWorkbenchRelicCatalog
    {
        /// <summary>전체 유물 수. 미획득 수 계산과 뽑기 범위에 쓴다.</summary>
        int Count { get; }

        bool TryFind(int relicId, out WorkbenchRelicInfo info);
    }
}
