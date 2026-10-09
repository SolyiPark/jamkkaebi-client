using Jamkkaebi.Scripts.Gameplay.Collection;
using UnityEngine;

namespace MobilePrototype.Collection
{
    /// <summary>
    /// 2026-10-09 확인한 「깨비별 특성」 시트1 A2:C10의 유물·시대 스냅샷.
    /// 정령·건물 이름, 설명과 복원 기록은 아직 확정되지 않은 화면용 콘텐츠입니다.
    /// </summary>
    public static class CollectionCatalogData
    {
        /// <summary>확정 시트의 시대·유물 순서와 미정 콘텐츠 표시로 새 메모리 카탈로그를 만듭니다.</summary>
        /// <returns>저장되지 않은 카탈로그이며 호출 측이 자산으로 저장하거나 사용 후 파괴합니다.</returns>
        public static CollectionCatalog CreateCatalog()
        {
            Color samguk = new Color32(194, 132, 77, 255);
            Color goryeo = new Color32(94, 157, 137, 255);
            Color joseon = new Color32(114, 141, 171, 255);
            CollectionCatalog catalog = ScriptableObject.CreateInstance<CollectionCatalog>();
            catalog.name = "CollectionCatalog";
            catalog.Configure(
                new[]
                {
                    new CollectionEraDefinition("samguk", "삼국", "고구려 · 백제 · 신라",
                        "금동여래입상, 금동대향로, 금관을 복원해 삼국의 전시실을 채워 보세요.", "삼국 전시각", samguk),
                    new CollectionEraDefinition("goryeo", "고려", "불상과 공예의 전시실",
                        "석불, 고려청자, 나전칠기를 모아 고려의 전시실을 완성해 보세요.", "고려 전시각", goryeo),
                    new CollectionEraDefinition("joseon", "조선", "인장과 증표, 그림의 전시실",
                        "옥새, 마패, 묘작도를 복원하며 조선의 전시실을 채워 보세요.", "조선 전시각", joseon)
                },
                new[]
                {
                    Relic("samguk-standing-buddha", "samguk", "금동여래입상", samguk),
                    Relic("samguk-incense-burner", "samguk", "금동대향로", samguk),
                    Relic("samguk-crown", "samguk", "금관", samguk),
                    Relic("goryeo-stone-buddha", "goryeo", "석불", goryeo),
                    Relic("goryeo-celadon", "goryeo", "고려청자", goryeo),
                    Relic("goryeo-lacquerware", "goryeo", "나전칠기", goryeo),
                    Relic("joseon-royal-seal", "joseon", "옥새", joseon),
                    Relic("joseon-horse-medallion", "joseon", "마패", joseon),
                    Relic("joseon-cat-sparrow-painting", "joseon", "묘작도", joseon)
                });
            return catalog;
        }

        /// <summary>확정 유물 이름에 정령 미정 안내와 임시 설명·복원 기록을 붙여 정의를 만듭니다.</summary>
        private static CollectionRelicDefinition Relic(string id, string eraId, string name, Color accent)
        {
            return new CollectionRelicDefinition(id, eraId, name, "정령 이름 미정",
                $"[임시 설명] {name}의 모습을 살펴보세요. 유물 해설과 정령 이야기는 추후 추가됩니다.",
                $"[임시 복원 기록] {name}의 복원을 마쳐 도감에 등록되었습니다. 발굴·보존 과정의 상세 이야기는 추후 추가됩니다.",
                accent);
        }
    }
}
