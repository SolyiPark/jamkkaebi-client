using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>
    /// 내 전시관 조회(GET /api/exhibition) 응답에서 홈이 쓰는 부분: 보유 도깨비, 놓인 도깨비의 자리, 놓인 건물이 차지하는 칸.
    /// </summary>
    /// <remarks>
    /// 숨긴 도깨비도 주인의 홈에는 놓인 것으로 읽는다. 숨김은 친구에게 보이지 않게 할 뿐 배치를 유지하기 때문이다.
    /// 서버 좌표는 차지하는 칸의 왼쪽 위 칸 기준이고 격자 칸으로 그대로 쓴다. 다른 크기의 격자로 줄이거나 보정하지 않는다.
    /// </remarks>
    public sealed class ExhibitionServerSnapshot
    {
        /// <summary>서버가 정한 건물 한 변의 칸 수(2×2).</summary>
        public const int BuildingSize = 2;

        /// <summary>보유한 도깨비 전부. 놓이지 않은 것도 들어 있다.</summary>
        public IReadOnlyList<ExhibitionPlaceable> Spirits { get; }

        /// <summary>놓인 도깨비의 relicId와 자리.</summary>
        public IReadOnlyDictionary<int, Vector2Int> SpiritCells { get; }

        /// <summary>놓인 건물들이 차지하는 칸 전부.</summary>
        public IReadOnlyCollection<Vector2Int> BuildingCells { get; }

        private ExhibitionServerSnapshot(List<ExhibitionPlaceable> spirits, Dictionary<int, Vector2Int> spiritCells,
            HashSet<Vector2Int> buildingCells)
        {
            Spirits = spirits.AsReadOnly(); SpiritCells = spiritCells; BuildingCells = buildingCells;
        }

        /// <summary>응답의 data를 읽는다.</summary>
        /// <param name="gridSize">홈의 격자 크기. 서버 격자와 같아야 한다.</param>
        /// <exception cref="FormatException">격자·보유 목록·좌표가 없거나 형식이 다른 경우, relicId가 겹치는 경우, 좌표가 격자 밖인 경우.</exception>
        /// <exception cref="InvalidOperationException">서버 격자가 홈 격자와 다른 경우.</exception>
        public static ExhibitionServerSnapshot Parse(JToken data, Vector2Int gridSize)
        {
            var grid = data?["grid"];
            if (grid == null || grid["width"]?.Type != JTokenType.Integer || grid["height"]?.Type != JTokenType.Integer)
                throw new FormatException("서버 격자 응답이 올바르지 않습니다.");
            var size = new Vector2Int((int)grid["width"], (int)grid["height"]);
            if (size != gridSize)
                throw new InvalidOperationException($"격자 불일치: 클라이언트 {gridSize.x}×{gridSize.y}, 서버 {size.x}×{size.y}");
            if (!(data["spirits"] is JArray spiritRows) || !(data["buildings"] is JArray buildingRows))
                throw new FormatException("서버 보유 목록이 없습니다.");

            var spirits = new List<ExhibitionPlaceable>();
            var spiritCells = new Dictionary<int, Vector2Int>();
            var seen = new HashSet<int>();
            foreach (var row in spiritRows)
            {
                if (row["relicId"]?.Type != JTokenType.Integer || row["displayed"]?.Type != JTokenType.Boolean)
                    throw new FormatException("서버 도깨비 응답이 올바르지 않습니다.");
                int relicId = (int)row["relicId"];
                if (relicId <= 0 || !seen.Add(relicId)) throw new FormatException("서버 도깨비 ID가 중복되거나 잘못되었습니다.");
                spirits.Add(new ExhibitionPlaceable(ExhibitionPlaceable.SpiritType, relicId, Text(row["spiritName"])));
                if (!(bool)row["displayed"]) continue;
                var cell = Position(row["position"]);
                if (cell.x < 0 || cell.y < 0 || cell.x >= size.x || cell.y >= size.y)
                    throw new FormatException("서버 도깨비 배치가 격자 밖에 있습니다.");
                spiritCells.Add(relicId, cell);
            }

            var buildingCells = new HashSet<Vector2Int>();
            foreach (var row in buildingRows)
            {
                if (row["buildingId"]?.Type != JTokenType.Integer || row["displayed"]?.Type != JTokenType.Boolean)
                    throw new FormatException("서버 건물 응답이 올바르지 않습니다.");
                if (!(bool)row["displayed"]) continue;
                var corner = Position(row["position"]);
                if (corner.x < 0 || corner.y < 0 || corner.x + BuildingSize > size.x || corner.y + BuildingSize > size.y)
                    throw new FormatException("서버 건물 배치가 격자 밖에 있습니다.");
                for (int x = 0; x < BuildingSize; x++)
                    for (int y = 0; y < BuildingSize; y++) buildingCells.Add(corner + new Vector2Int(x, y));
            }
            return new ExhibitionServerSnapshot(spirits, spiritCells, buildingCells);
        }

        private static Vector2Int Position(JToken position)
        {
            if (position?["x"]?.Type != JTokenType.Integer || position["y"]?.Type != JTokenType.Integer)
                throw new FormatException("서버 배치 좌표가 없습니다.");
            return new Vector2Int((int)position["x"], (int)position["y"]);
        }

        private static string Text(JToken token) => token?.Type == JTokenType.String ? (string)token : null;
    }
}
