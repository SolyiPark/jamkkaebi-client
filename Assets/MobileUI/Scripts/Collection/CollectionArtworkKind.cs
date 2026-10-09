namespace MobilePrototype.Collection
{
    /// <summary>실제 그림 자산이 없을 때 표시할 도감 임시 벡터 기호의 종류입니다.</summary>
    public enum CollectionArtworkKind
    {
        /// <summary>금관과 삼국의 대표 기호입니다.</summary>
        Crown,
        /// <summary>도자기 유물과 고려의 대표 기호입니다.</summary>
        Vessel,
        /// <summary>금동여래입상과 석불을 식별하는 공통 불상 기호입니다.</summary>
        Buddha,
        /// <summary>금동대향로를 식별하는 향로 기호입니다.</summary>
        IncenseBurner,
        /// <summary>나전칠기를 식별하는 장식 상자 기호입니다.</summary>
        Lacquerware,
        /// <summary>옥새와 조선의 대표 인장 기호입니다.</summary>
        RoyalSeal,
        /// <summary>마패를 식별하는 말 증표 기호입니다.</summary>
        HorseMedallion,
        /// <summary>묘작도를 식별하는 그림 기호입니다.</summary>
        Painting,
        /// <summary>해금된 건물이나 알려지지 않은 시대의 대표 기호입니다.</summary>
        Building,
        /// <summary>정령 그림이 없을 때 사용하는 공통 정령 기호입니다.</summary>
        Spirit,
        /// <summary>잠금 그림이 없는 미복원 유물이나 미해금 건물의 기호입니다.</summary>
        Lock
    }
}
