using System;
using System.Collections.Generic;
using Jamkkaebi.Scripts.Gameplay.Collection;

namespace MobilePrototype.Collection
{
    /// <summary>한 번의 화면 갱신에 사용할 수집 판정의 복사본입니다. View에는 변경 가능한 진행 모델을 전달하지 않습니다.</summary>
    /// <remarks>
    /// 원본 장부(CollectionProgress)를 화면에 맡기는 대신, 지금 보여줄 내용만 적은 안내지를 만드는 역할입니다.
    /// 예를 들어 복원 5종일 때 만든 안내지는 원본이 6종으로 바뀌어도 5종을 유지합니다.
    /// Presenter가 변경 알림을 받아 새 안내지를 만들어 주면 그때 화면도 6종으로 갱신됩니다.
    /// 복원·신규·해금 판정은 복사하지만 그림·설명 자산은 공유합니다. 자산까지 모두 복제한 저장 파일은 아닙니다.
    /// </remarks>
    public sealed class CollectionViewData
    {
        // HashSet은 '이 ID가 목록 안에 있는가'를 빠르게 확인하기 위한 자료구조입니다.
        // 모든 목록은 이 객체가 직접 소유하고 밖으로 내주지 않으므로 View가 판정을 수정할 수 없습니다.
        private readonly HashSet<string> _restoredIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _unreadIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _unlockedEraIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _unreadEraIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _restoredInEra = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _totalInEra = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>시대·유물의 이름과 그림이 담긴 고정 정의 자산입니다. 표시 중에는 읽는 용도로 사용합니다.</summary>
        public CollectionCatalog Catalog { get; }
        /// <summary>공통 배경·카드·아이콘 자산입니다. null이면 기본 색상과 임시 기호를 사용합니다.</summary>
        public CollectionAppearance Appearance { get; }
        /// <summary>이번에 보여줄 화면 종류입니다. View가 아니라 Presenter가 결정합니다.</summary>
        public CollectionPage Page { get; }
        /// <summary>선택된 시대의 ID입니다. 시대 목록에서는 null입니다.</summary>
        public string SelectedEraId { get; }
        /// <summary>선택된 유물의 ID입니다. 유물 상세 이외에서는 null입니다.</summary>
        public string SelectedRelicId { get; }
        /// <summary>이 복사본을 만든 시점의 복원 완료 유물 종류 수입니다.</summary>
        public int RestoredCount { get; }
        /// <summary>수집 대상 유물 종류의 전체 수입니다. 건물 수는 포함하지 않습니다.</summary>
        public int TotalCount { get; }
        /// <summary>기존 수집 규칙이 해금 완료로 판정한 건물 수입니다.</summary>
        public int CompletedBuildingCount { get; }
        /// <summary>0~1 사이 수집 비율입니다. 화면에 55%처럼 쓰는 모양은 View가 정합니다.</summary>
        public float CollectionRate { get; }

        /// <summary>원본 진행 기록을 바꾸지 않고, 표시할 판정과 수치를 한 번 복사합니다.</summary>
        /// <param name="progress">현재 사용자 진행 기록입니다. 생성 중 읽기만 하고 필드에 보관하지 않습니다.</param>
        /// <param name="appearance">공통 그림 자산 또는 기본 표현을 뜻하는 null입니다.</param>
        /// <param name="page">Presenter가 선택한 화면 종류입니다.</param>
        /// <param name="selectedEraId">선택 시대의 ID 또는 시대 목록일 때 null입니다.</param>
        /// <param name="selectedRelicId">선택 유물의 ID 또는 유물 상세가 아닐 때 null입니다.</param>
        /// <exception cref="ArgumentNullException">복사할 원본 기록이 없는 경우입니다.</exception>
        public CollectionViewData(CollectionProgress progress, CollectionAppearance appearance,
            CollectionPage page, string selectedEraId, string selectedRelicId)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            Catalog = progress.Catalog;
            Appearance = appearance;
            Page = page;
            SelectedEraId = selectedEraId;
            SelectedRelicId = selectedRelicId;
            RestoredCount = progress.RestoredCount;
            TotalCount = progress.TotalCount;
            CompletedBuildingCount = progress.CompletedBuildingCount;
            CollectionRate = progress.CollectionRate;
            // '복원했다'와 '아직 상세를 읽지 않았다'는 다른 판정입니다. 신규 N 표시를 위해 별도로 복사합니다.
            foreach (var relic in Catalog.Relics)
            {
                if (progress.IsRestored(relic.Id)) _restoredIds.Add(relic.Id);
                if (progress.IsUnread(relic.Id)) _unreadIds.Add(relic.Id);
            }
            // View가 '전부 모았으면 건물 해금' 규칙을 다시 만들지 않도록 도메인 모델의 판정을 가져옵니다.
            // 디자인을 바꿀 때 게임 규칙까지 복제했다가 서로 달라지는 문제를 줄이기 위한 경계입니다.
            foreach (var era in Catalog.Eras)
            {
                _restoredInEra.Add(era.Id, progress.RestoredInEra(era.Id));
                _totalInEra.Add(era.Id, progress.TotalInEra(era.Id));
                if (progress.IsBuildingUnlocked(era.Id)) _unlockedEraIds.Add(era.Id);
                if (progress.HasUnreadInEra(era.Id)) _unreadEraIds.Add(era.Id);
            }
        }

        /// <summary>복사 시점에 복원한 유물인지 확인합니다. null·미등록 ID는 false입니다.</summary>
        public bool IsRestored(string relicId) => relicId != null && _restoredIds.Contains(relicId);
        /// <summary>복사 시점에 아직 상세를 읽지 않은 유물인지 확인합니다. 조회해도 신규 표시는 지워지지 않습니다.</summary>
        public bool IsUnread(string relicId) => relicId != null && _unreadIds.Contains(relicId);
        /// <summary>복사 시점에 해당 시대 건물이 해금됐는지 확인합니다. 해금 효과를 실행하는 함수가 아닙니다.</summary>
        public bool IsBuildingUnlocked(string eraId) => eraId != null && _unlockedEraIds.Contains(eraId);
        /// <summary>시대 카드에 신규 배지를 표시할지 확인합니다. 해당 시대에 미열람 유물이 있으면 true입니다.</summary>
        public bool HasUnreadInEra(string eraId) => eraId != null && _unreadEraIds.Contains(eraId);
        /// <summary>복사 시점에 해당 시대에서 복원한 종류 수입니다. null·미등록 ID는 0입니다.</summary>
        public int RestoredInEra(string eraId) => eraId != null && _restoredInEra.TryGetValue(eraId, out var count) ? count : 0;
        /// <summary>해당 시대의 전체 수집 대상 종류 수입니다. null·미등록 ID는 0입니다.</summary>
        public int TotalInEra(string eraId) => eraId != null && _totalInEra.TryGetValue(eraId, out var count) ? count : 0;
    }
}
