using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 프리텐다드의 여러 굵기를 TMP 폰트 에셋으로 만들고, 대표 에셋(Medium)의 Font Weights 표에 연결하는 에디터 도구입니다.
/// </summary>
/// <remarks>
/// 텍스트에는 대표 에셋 하나만 지정하고 Font Weight(Thin, SemiBold, Bold…)로 굵기를 고릅니다.
/// 대표 에셋의 Font Weights 표에 해당 굵기의 에셋이 연결되어 있으면 TMP가 그 진짜 글꼴로 그립니다.
/// 연결되어 있지 않은 굵기는 대표 에셋(Medium) 그대로 보입니다.
/// 새 에셋을 만들었으면 정적 아틀라스로 굽기까지 합니다.
/// </remarks>
public static class FontWeightSetup
{
    private const string PretendardFolder = "Assets/Jamkkaebi/Fonts/Pretendard";
    private const string FontAssetFolder = "Assets/Jamkkaebi/Fonts/TMP";
    private const string MainStyle = "Medium";

    // (파일 이름의 굵기, TMP 굵기). 표의 칸 번호는 굵기 ÷ 100이다(Thin 100 → 1번, SemiBold 600 → 6번)
    private static readonly (string Style, FontWeight Weight)[] Weights =
    {
        ("Thin", FontWeight.Thin),
        ("SemiBold", FontWeight.SemiBold),
        ("Bold", FontWeight.Bold),
    };

    [MenuItem("Jamkkaebi/Fonts/Setup Pretendard Weights")]
    public static void SetupMenu()
    {
        bool created = Setup();
        Debug.Log(created
            ? "[FontWeightSetup] 새 굵기 에셋을 만들고 구웠습니다."
            : "[FontWeightSetup] 굵기 에셋이 이미 있어 연결만 확인했습니다.");
    }

    /// <summary>굵기 에셋을 만들고 대표 에셋에 연결한다. 새로 만든 에셋이 있으면 굽는다.</summary>
    /// <returns>새로 만든 에셋이 있으면 true.</returns>
    public static bool Setup()
    {
        TMP_FontAsset main = MinigameHudSetup.GetOrCreateFontAsset(FontPath(MainStyle));
        if (main == null)
        {
            Debug.LogWarning($"[FontWeightSetup] 대표 글꼴을 찾지 못했습니다: {FontPath(MainStyle)}");
            return false;
        }

        bool created = false;
        foreach ((string style, FontWeight weight) in Weights)
        {
            bool existed = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath(style)) != null;
            TMP_FontAsset asset = MinigameHudSetup.GetOrCreateFontAsset(FontPath(style));
            if (asset == null)
            {
                Debug.LogWarning($"[FontWeightSetup] 글꼴 파일을 찾지 못했습니다: {FontPath(style)}");
                continue;
            }
            created |= !existed;
            Link(main, weight, asset);
        }

        if (created) StaticFontBaker.Bake();   // 굽기 도구가 마지막에 에셋을 저장한다
        else AssetDatabase.SaveAssets();
        return created;
    }

    private static void Link(TMP_FontAsset main, FontWeight weight, TMP_FontAsset asset)
    {
        TMP_FontWeightPair[] table = main.fontWeightTable;
        int index = (int)weight / 100;
        if (table == null || table.Length <= index || table[index].regularTypeface == asset) return;
        // 배열 칸을 직접 고치면 대표 에셋이 들고 있는 표가 바뀐다
        table[index].regularTypeface = asset;
        EditorUtility.SetDirty(main);
    }

    private static string FontPath(string style) => $"{PretendardFolder}/Pretendard-{style}.otf";
    private static string AssetPath(string style) => $"{FontAssetFolder}/Pretendard-{style} SDF.asset";
}
