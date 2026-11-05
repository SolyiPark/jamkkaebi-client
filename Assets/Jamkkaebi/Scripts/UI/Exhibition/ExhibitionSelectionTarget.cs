using System;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>화면 어댑터가 전달하는 배치 인스턴스 식별자와 바닥 접점입니다.</summary>
    public sealed class ExhibitionSelectionTarget
    {
        public string Id { get; }
        public Transform Root { get; }
        public bool IsAlive => Root && Root.gameObject.activeInHierarchy;
        public ExhibitionSelectionTarget(string id, Transform root)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("배치 인스턴스 ID가 필요합니다.", nameof(id));
            Id = id; Root = root;
        }
    }
}
