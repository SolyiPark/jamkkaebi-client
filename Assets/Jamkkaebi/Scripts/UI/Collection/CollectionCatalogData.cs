using Jamkkaebi.Scripts.Gameplay.Collection;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Collection
{
    /// <summary>
    /// 2026-11-19 서버 시드 기준.
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
                        "유물 3종을 모아 삼국의 전시실을 채워 보세요.", "석굴암", samguk),
                    new CollectionEraDefinition("goryeo", "고려", "고려",
                        "유물 3종을 모아 고려의 전시실을 완성해 보세요.", "경천사지 십층석탑", goryeo),
                    new CollectionEraDefinition("joseon", "조선", "조선",
                        "유물 3종을 모아 조선의 전시실을 채워 보세요.", "경복궁", joseon)
                },
                new[]
                {
                    Relic("samguk-turtle-water-dropper", "samguk", "거북이 모양 연적", "거북이", samguk),
                    Relic("samguk-incense-burner", "samguk", "금동대향로", "향산이", samguk),
                    Relic("samguk-frog-figurine", "samguk", "개구리 모양 토우", "개굴이", samguk),
                    Relic("goryeo-pomegranate-water-dropper", "goryeo", "청자 석류 모양 연적", "석류동이", goryeo),
                    Relic("goryeo-celadon", "goryeo", "상감 청자 매병", "구름결", goryeo),
                    Relic("goryeo-lacquerware", "goryeo", "나전칠기", "밤바다", goryeo),
                    Relic("joseon-phoenix-hairpin", "joseon", "봉 뒤꽂이", "봉이", joseon),
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
