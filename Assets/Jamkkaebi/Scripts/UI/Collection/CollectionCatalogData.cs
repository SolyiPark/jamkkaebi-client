using Jamkkaebi.Scripts.Gameplay.Collection;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Collection
{
    /// <summary>
    /// 2026-10-09 확인한 「깨비별 특성」 시트1 A2:C10의 유물·시대 순서에 서버 ContentSeedData(be72ae8)의 이름을 적용한 스냅샷.
    /// 설명과 복원 기록은 아직 확정되지 않은 화면용 콘텐츠입니다.
    /// 임시: 화면의 "도깨비" 표기와 도깨비 이름은 확정 전 임시 내용이며 코드 식별자(SpiritName 등)는 바꾸지 않았습니다.
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
                        "금동연가7년명여래입상, 금동대향로, 금관총 금관을 복원해 삼국의 전시실을 채워 보세요.", "석굴암", samguk),
                    new CollectionEraDefinition("goryeo", "고려", "불상과 공예의 전시실",
                        "논산 관촉사 석조미륵보살입상, 상감청자, 나전칠기를 모아 고려의 전시실을 완성해 보세요.", "경천사지 십층석탑", goryeo),
                    new CollectionEraDefinition("joseon", "조선", "인장과 증표, 그림의 전시실",
                        "유서지보, 마패, 묘작도를 복원하며 조선의 전시실을 채워 보세요.", "경복궁", joseon)
                },
                new[]
                {
                    // 임시: 네 번째 인자의 도깨비 이름은 서버 spiritDefaultName을 복사한 값으로, 확정되면 교체합니다.
                    Relic("samguk-standing-buddha", "samguk", "금동연가7년명여래입상", "불꽃걸음", samguk),
                    Relic("samguk-incense-burner", "samguk", "금동대향로", "향산이", samguk),
                    Relic("samguk-crown", "samguk", "금관총 금관", "금빛숲", samguk),
                    Relic("goryeo-stone-buddha", "goryeo", "논산 관촉사 석조미륵보살입상", "미륵이", goryeo),
                    Relic("goryeo-celadon", "goryeo", "상감청자", "구름결", goryeo),
                    Relic("goryeo-lacquerware", "goryeo", "나전칠기", "밤바다", goryeo),
                    Relic("joseon-royal-seal", "joseon", "유서지보", "붉은당부", joseon),
                    Relic("joseon-horse-medallion", "joseon", "마패", "말발굽", joseon),
                    Relic("joseon-cat-sparrow-painting", "joseon", "묘작도", "묘작이", joseon)
                });
            return catalog;
        }

        /// <summary>서버 기준 유물·도깨비 이름에 임시 설명·복원 기록을 붙여 정의를 만듭니다.</summary>
        private static CollectionRelicDefinition Relic(string id, string eraId, string name, string spiritName, Color accent)
        {
            return new CollectionRelicDefinition(id, eraId, name, spiritName,
                $"[임시 설명] {name}의 모습을 살펴보세요. 유물 해설과 도깨비 이야기는 추후 추가됩니다.",
                $"[임시 복원 기록] {name}의 복원을 마쳐 도감에 등록되었습니다. 발굴·보존 과정의 상세 이야기는 추후 추가됩니다.",
                accent);
        }
    }
}
