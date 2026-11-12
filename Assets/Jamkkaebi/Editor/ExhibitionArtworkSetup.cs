using Jamkkaebi.Scripts.UI.Exhibition;
using UnityEditor;
using UnityEngine;

/// <summary>홈과 방문 스테이지에서 공유하는 배경 레이어의 정렬·합성 설정입니다.</summary>
public static class ExhibitionArtworkSetup
{
    private static readonly float[] Parallax = { .015f, .055f, .025f, .11f, .18f, .27f, .32f, .36f, .01f, 0 };
    private static readonly int[] Orders = { 0, 10, 20, 30, 100, 2000, 2010, 2020, 2100, 2110 };

    /// <summary>1~10번 배경의 원본 Sprite와 홈 기준 정렬·불투명도·크기 정책을 연결합니다.</summary>
    public static void Configure(GameObject layer, int number, Material material)
    {
        var renderer = layer.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Jamkkaebi/Art/Exhibition/Artwork{number:00}.png");
        if (!renderer.sprite) throw new System.InvalidOperationException($"배경 {number:00}의 Sprite가 없습니다.");
        renderer.sharedMaterial = material;
        renderer.sortingOrder = Orders[number - 1];
        layer.GetComponent<ExhibitionLayer>().Configure(Parallax[number - 1], (number - 1) * .09f,
            number == 10 ? .075f : number == 9 ? .22f : 1, number == 1,
            number == 2 || number == 4 || number == 6 || number == 7 || number == 8);
    }
}
