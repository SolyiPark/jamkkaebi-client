using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    public static class ExhibitionLayoutPackage
    {
        /// <summary>전체 교체 PUT를 위해 관리하지 않는 서버 배치를 보존한다.</summary>
        /// <param name="draft">All managed objects, including unplaced objects to remove from the server layout.</param>
        /// <param name="serverExhibition">Current grid and owned spirit/building lists, including displayed positions to preserve for unmanaged objects.</param>
        /// <returns>An items payload for full replacement, with only placed managed objects and displayed unmanaged objects.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="draft"/> is null.</exception>
        /// <exception cref="FormatException">Required grid, ownership, or position fields are missing or invalid, or server IDs repeat.</exception>
        /// <exception cref="InvalidOperationException">Grids differ or are nonpositive; managed identities are invalid, unsupported, duplicated, or unowned; placed objects are rotated or not 1×1; or final cells overlap or lie outside the grid.</exception>
        /// <remarks>JSON access and conversion errors propagate, including overflow when integers exceed Int32 range.</remarks>
        public static JObject Build(ExhibitionLayoutDraft draft, JToken serverExhibition)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            var grid = serverExhibition?["grid"];
            if (grid == null || grid["width"]?.Type != JTokenType.Integer || grid["height"]?.Type != JTokenType.Integer)
                throw new FormatException("서버 격자 응답이 올바르지 않습니다.");
            var size = new Vector2Int((int)grid["width"], (int)grid["height"]);
            if (size != draft.GridSize || size.x <= 0 || size.y <= 0)
                throw new InvalidOperationException($"격자 불일치: 클라이언트 {draft.GridSize.x}×{draft.GridSize.y}, 서버 {size.x}×{size.y}");
            var owned = new HashSet<string>();
            var preserved = new Dictionary<string, JObject>();
            ReadOwned(serverExhibition["spirits"], "SPIRIT", "relicId", owned, preserved);
            ReadOwned(serverExhibition["buildings"], "BUILDING", "buildingId", owned, preserved);
            var managed = new HashSet<string>();
            var items = new JArray();
            foreach (var obj in draft.Objects)
            {
                if (obj == null || obj.ServerId <= 0) throw new InvalidOperationException("서버 ID가 없는 더미 객체는 저장할 수 없습니다.");
                if (obj.Type != "SPIRIT" && obj.Type != "BUILDING") throw new InvalidOperationException("지원하지 않는 객체 종류입니다.");
                if (!managed.Add(obj.Key)) throw new InvalidOperationException("동일 서버 객체를 중복 배치할 수 없습니다: " + obj.Key);
                if (!owned.Contains(obj.Key)) throw new InvalidOperationException("보유 목록에 없는 객체입니다: " + obj.Key);
                preserved.Remove(obj.Key);
                if (!obj.Placed) continue;
                if (obj.Footprint != Vector2Int.one || obj.QuarterTurns != 0)
                    throw new InvalidOperationException("현재 서버는 1×1, 회전 0의 배치만 보존할 수 있습니다.");
                items.Add(Item(obj.Type, obj.ServerId, obj.Cell));
            }
            foreach (var row in preserved.Values) items.Add(row);
            var cells = new HashSet<Vector2Int>();
            foreach (JObject item in items)
            {
                var cell = new Vector2Int((int)item["x"], (int)item["y"]);
                if (cell.x < 0 || cell.y < 0 || cell.x >= size.x || cell.y >= size.y)
                    throw new InvalidOperationException("배치가 격자 밖에 있습니다.");
                if (!cells.Add(cell)) throw new InvalidOperationException("보존할 서버 배치 또는 다른 객체와 위치가 겹칩니다.");
            }
            return new JObject { ["items"] = items };
        }

        /// <summary>서버 배치 PUT에 허용된 type·id·x·y 필드만 포함한 항목을 만듭니다.</summary>
        private static JObject Item(string type, int id, Vector2Int cell) =>
            new JObject { ["type"] = type, ["id"] = id, ["x"] = cell.x, ["y"] = cell.y };

        /// <summary>서버 보유 목록을 검증하고 현재 표시 중인 배치를 보존 목록으로 수집합니다.</summary>
        private static void ReadOwned(JToken rows, string type, string idField,
            HashSet<string> owned, Dictionary<string, JObject> preserved)
        {
            if (!(rows is JArray)) throw new FormatException("서버 보유 목록이 없습니다.");
            foreach (var row in rows)
            {
                if (row[idField]?.Type != JTokenType.Integer || row["displayed"]?.Type != JTokenType.Boolean)
                    throw new FormatException("서버 객체 응답이 올바르지 않습니다.");
                int id = (int)row[idField]; string key = type + ":" + id;
                if (id <= 0 || !owned.Add(key)) throw new FormatException("서버 객체 ID가 중복되거나 잘못되었습니다.");
                if (!(bool)row["displayed"]) continue;
                var pos = row["position"];
                if (pos?["x"]?.Type != JTokenType.Integer || pos["y"]?.Type != JTokenType.Integer)
                    throw new FormatException("서버 배치 좌표가 없습니다.");
                preserved.Add(key, Item(type, id, new Vector2Int((int)pos["x"], (int)pos["y"])));
            }
        }
    }
}
