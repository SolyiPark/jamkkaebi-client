using System;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Friends
{
    /// <summary>서버 종류 ID에 대응하는 표현 프리팹과 바닥 접점·터치 영역입니다.</summary>
    [Serializable]
    public sealed class ExhibitionVisualDefinition
    {
        [SerializeField] private int _id;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private Vector3 _scale = Vector3.one;
        [SerializeField] private Vector2 _footOffset;
        [SerializeField] private Rect _touchRect = new Rect(-.2f, 0, .4f, .5f);
        public int Id => _id;
        public GameObject Prefab => _prefab;
        public Vector3 Scale => _scale;
        public Vector2 FootOffset => _footOffset;
        public Rect TouchRect => _touchRect;

        /// <summary>바닥 접점 기준 표현을 정의합니다. 프리팹이 없으면 공용 더미를 사용합니다.</summary>
        public ExhibitionVisualDefinition(int id, GameObject prefab, Vector3 scale, Vector2 footOffset, Rect touchRect)
        { _id = id; _prefab = prefab; _scale = scale; _footOffset = footOffset; _touchRect = touchRect; }
    }
}
