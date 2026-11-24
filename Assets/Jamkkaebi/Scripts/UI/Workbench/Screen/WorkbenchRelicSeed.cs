using System.Collections.Generic;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Data;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>
    /// 서버 ContentSeedData(jamkkaebi-server, 2026-11-19 확인)의 유물 9종을 옮긴 임시 목록.
    /// 도감 카탈로그(CollectionCatalog)는 relicId 대응과 별칭이 없고 1·3·4·7번이 서버와 달라 쓰지 않는다.
    /// 서버를 연결하면 이름은 응답에 담겨 오므로, 이 목록은 모의 백엔드와 화면 미리보기용이다.
    /// </summary>
    public sealed class WorkbenchRelicSeed : IWorkbenchRelicCatalog
    {
        public static WorkbenchRelicSeed Instance { get; } = new WorkbenchRelicSeed();

        private readonly Dictionary<int, WorkbenchRelicInfo> _relics = new WorkbenchRelicInfo[]
        {
            new(1, "samguk-turtle-water-dropper", "삼국", "고구려, 벼루 곁에 엎드린 거북", "거북이 모양 연적", "거북이", SealMedium.Root),
            new(2, "samguk-incense-burner", "삼국", "백제, 향이 스민 산맥", "금동대향로", "향산이", SealMedium.Soot),
            new(3, "samguk-frog-figurine", "삼국", "신라, 흙에서 깨어난 개구리", "개구리 모양 토우", "개굴이", SealMedium.Root),
            new(4, "goryeo-pomegranate-water-dropper", "고려", "고려, 석류를 끌어안은 원숭이", "청자 석류 모양 연적", "석류동이", SealMedium.Root),
            new(5, "goryeo-celadon", "고려", "고려, 살결에 새긴 구름", "상감 청자 매병", "구름결", SealMedium.Fog),
            new(6, "goryeo-lacquerware", "고려", "고려, 바다를 두른 검은 밤", "나전칠기", "밤바다", SealMedium.Fog),
            new(7, "joseon-phoenix-hairpin", "조선", "조선, 쪽머리에 내려앉은 봉황", "봉 뒤꽂이", "봉이", SealMedium.Rust),
            new(8, "joseon-horse-medallion", "조선", "조선, 밤길을 여는 말굽", "마패", "말발굽", SealMedium.Rust),
            new(9, "joseon-cat-sparrow-painting", "조선", "조선, 참새를 올려다보는 눈", "묘작도", "묘작이", SealMedium.Soot)
        }.ToDictionary(info => info.RelicId);

        private WorkbenchRelicSeed()
        {
        }

        public int Count => _relics.Count;

        public bool TryFind(int relicId, out WorkbenchRelicInfo info) => _relics.TryGetValue(relicId, out info);
    }
}
