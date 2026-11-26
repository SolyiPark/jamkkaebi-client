using Jamkkaebi.Scripts.Gameplay.Collection;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Collection
{
    /// <summary>
    /// 도감의 구조(시대·유물 ID, 소속, 순서, 강조색)와 서버 시드(2026-11-19) 기준 이름을 만듭니다.
    /// 시대 기간 문구·시대 설명·유물 설명·복원 기록은 비워 두며, 에디터의 도감 텍스트 임포트가
    /// Configuration/Text의 CSV에서 채워 CollectionCatalog.asset에 저장합니다. 이 클래스에 화면 문구를 넣지 않습니다.
    /// 임시: 화면의 "도깨비" 표기와 도깨비 이름은 확정 전 임시 내용이며 코드 식별자(SpiritName 등)는 바꾸지 않았습니다.
    /// </summary>
    public static class CollectionCatalogData
    {
        /// <summary>서버 시드의 시대·유물 순서와 이름으로, 텍스트가 빈 새 메모리 카탈로그를 만듭니다.</summary>
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
                    Era("samguk", "삼국", "석굴암", samguk),
                    Era("goryeo", "고려", "경천사지 십층석탑", goryeo),
                    Era("joseon", "조선", "경복궁", joseon)
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

        /// <summary>서버 기준 시대·건물 이름으로 기간 문구와 설명이 빈 시대 정의를 만듭니다.</summary>
        private static CollectionEraDefinition Era(string id, string name, string buildingName, Color accent)
        {
            return new CollectionEraDefinition(id, name, string.Empty, string.Empty, buildingName, accent);
        }

        /// <summary>서버 기준 유물·도깨비 이름으로 설명과 복원 기록이 빈 유물 정의를 만듭니다.</summary>
        private static CollectionRelicDefinition Relic(string id, string eraId, string name, string spiritName, Color accent)
        {
            return new CollectionRelicDefinition(id, eraId, name, spiritName, string.Empty, string.Empty, accent);
        }
    }
}
