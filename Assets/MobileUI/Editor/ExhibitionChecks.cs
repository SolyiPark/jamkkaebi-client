using System;
using MobilePrototype.Exhibition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ExhibitionChecks
{
    /// <summary>
    /// 격자 좌표 왕복·경계·배치 범위·표시 순서와 홈 씬 참조를 검사하며, 실패하면 예외를 발생시킵니다.
    /// </summary>
    public static void Run()
    {
        var grid = AssetDatabase.LoadAssetAtPath<ExhibitionGrid>("Assets/MobileUI/Configuration/ExhibitionGrid.asset");
        Check(grid && grid.IsValid && grid.Size == new Vector2Int(7, 7), "7 by 7 grid asset");
        for (int x = 0; x < 7; x++)
        for (int y = 0; y < 7; y++)
        {
            var expected = new Vector2Int(x, y);
            Check(grid.TryGetCell(grid.CellCenter(expected), out var actual) && actual == expected, "cell roundtrip " + expected);
        }
        Check(!grid.TryGetCell(grid.ToLocal(new Vector2(-.01f, 1)), out _), "negative coordinates rejected");
        Check(!grid.TryGetCell(grid.ToLocal(new Vector2(7.01f, 1)), out _), "outside upper edge rejected");
        Check(!grid.TryGetCell(new Vector2(0, -3), out _), "platform side is not housing floor");
        Check(grid.ContainsFootprint(new Vector2Int(5, 5), new Vector2Int(2, 2)), "multi-cell footprint inside");
        Check(!grid.ContainsFootprint(new Vector2Int(6, 6), new Vector2Int(2, 2)), "multi-cell footprint outside");
        Check(ExhibitionDepth.OrderFor(-1) > ExhibitionDepth.OrderFor(0), "front ground anchor covers rear");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MobileUI/Art/Exhibition/Artwork05.png");
        Check(sprite && Mathf.Abs(sprite.bounds.size.x - 4.32f) < .02f && Mathf.Abs(sprite.bounds.size.y - 7.68f) < .02f,
            "texture downscaling preserves original artwork space: " + (sprite ? sprite.bounds.size.ToString() : "missing"));
        EditorSceneManager.OpenScene("Assets/MobileUI/Scenes/Home.unity");
        var surface = UnityEngine.Object.FindFirstObjectByType<ExhibitionSurface>();
        Check(surface && surface.Grid && surface.Grid.Size == new Vector2Int(7, 7), "home surface references persistent grid asset");
        IntegratedSetup.ValidateScenes();
        Debug.Log("EXHIBITION_CHECKS_PASSED");
    }
    /// <summary>
    /// 조건이 거짓이면 검사 이름을 담은 예외를 발생시키고, 참이면 통과 기록을 출력합니다.
    /// </summary>
    private static void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); Debug.Log("PASS " + label); }
}
