using Jamkkaebi.Scripts.Gameplay.Collection;

namespace MobilePrototype.Collection
{
    /// <summary>
    /// 확정 카탈로그를 사용하는 도감 화면 검토용 샘플 완료 기록입니다.
    /// 작업대 연결 시 이 초기 기록 주입을 실제 사용자 기록과 완료 이벤트로 교체합니다.
    /// </summary>
    public static class CollectionDemoData
    {
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
