using Jamkkaebi.Scripts.Gameplay.Collection;

namespace MobilePrototype.Collection
{
    /// <summary>
    /// 확정 카탈로그를 사용하는 도감 화면 검토용 샘플 완료 기록입니다.
    /// 작업대 연결 시 이 초기 기록 주입을 실제 사용자 기록과 완료 이벤트로 교체합니다.
    /// </summary>
    public static class CollectionDemoData
    {
        /// <summary>확정 유물 5종을 복원하고 석불·나전칠기의 신규 표시를 해제한 화면 검토용 기록을 만듭니다.</summary>
        /// <param name="catalog">샘플 유물 종류가 정의된 도감 자산입니다.</param>
        /// <returns>카탈로그를 공유하는 새 메모리 기록이며 저장·작업대 연결은 수행하지 않습니다.</returns>
        /// <exception cref="System.ArgumentNullException">카탈로그가 null이거나 파괴된 경우입니다.</exception>
        /// <exception cref="System.ArgumentException">카탈로그의 정의가 유효하지 않은 경우입니다.</exception>
        public static CollectionProgress CreateProgress(CollectionCatalog catalog)
        {
            CollectionProgress progress = new CollectionProgress(catalog);
            progress.RegisterRestorationCompleted("samguk-crown");
            progress.RegisterRestorationCompleted("goryeo-celadon");
            progress.RegisterRestorationCompleted("goryeo-stone-buddha");
            progress.RegisterRestorationCompleted("goryeo-lacquerware");
            progress.RegisterRestorationCompleted("joseon-royal-seal");
            progress.MarkViewed("goryeo-stone-buddha");
            progress.MarkViewed("goryeo-lacquerware");
            return progress;
        }
    }
}
