using System;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>
    /// 전시관에 놓을 수 있는 보유 객체 하나. 도깨비와 건물이 같은 모양을 쓴다.
    /// Type과 ServerId는 서버 배치 항목의 type·id와 같다.
    /// </summary>
    public sealed class ExhibitionPlaceable
    {
        public const string SpiritType = "SPIRIT";
        public const string BuildingType = "BUILDING";

        /// <summary><see cref="SpiritType"/> 또는 <see cref="BuildingType"/>.</summary>
        public string Type { get; }

        /// <summary>도깨비면 relicId, 건물이면 buildingId.</summary>
        public int ServerId { get; }

        public string DisplayName { get; }

        /// <summary>종류와 서버 ID를 합친 식별자. <see cref="ExhibitionObjectData.Key"/>와 같은 형식이다.</summary>
        public string Key => Type + ":" + ServerId;

        public bool IsSpirit => Type == SpiritType;

        /// <exception cref="ArgumentException">종류가 정해진 값이 아니거나 서버 ID가 1보다 작은 경우.</exception>
        public ExhibitionPlaceable(string type, int serverId, string displayName)
        {
            if (type != SpiritType && type != BuildingType)
                throw new ArgumentException("지원하지 않는 객체 종류입니다: " + type, nameof(type));
            if (serverId <= 0) throw new ArgumentException("서버 ID는 1부터 시작합니다.", nameof(serverId));
            Type = type; ServerId = serverId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Key : displayName;
        }
    }
}
