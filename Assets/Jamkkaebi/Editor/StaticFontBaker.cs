using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 미니게임 TMP 폰트 에셋을 정적(Static) 아틀라스로 굽는 에디터 도구입니다.
/// </summary>
/// <remarks>
/// 동적(Dynamic) 폰트는 처음 쓰이는 글자를 그때그때 아틀라스에 추가하고, 에디터에서는 그 결과가 .asset 파일에 저장된다.
/// 그래서 Play만 해도 SDF 파일이 git 변경 사항에 올라온다. 쓰는 글자를 미리 모두 그려 두고 Static으로 바꾸면 파일이 더는 바뀌지 않는다.
///
/// 굽는 글자:
/// - 한글 완성형 2,350자(KS X 1001, <see cref="HangulCharsetPath"/>)와 한글 자모(ㄱ~ㅣ)
/// - 영문·숫자·기호(ASCII 0x20~0x7E)
/// - 미니게임 UI 코드(<see cref="UiCodeFolder"/>)의 문자열("..."). 로그·Tooltip·Header 문구는 화면에 나오지 않으므로 뺀다
/// - 미니게임 씬(<see cref="ScenePath"/>)의 TMP 텍스트(m_text)
/// - <see cref="ExtraCharactersPath"/>에 적어 둔 글자(서버에서 받는 이름처럼 코드에 없는 문구용). #으로 시작하는 줄은 설명이라 뺀다
///
/// 2,350자에 없는 글자(일부 기호, 드문 한글)를 새 문구에 넣었다면 이 메뉴를 다시 실행해야 보인다. 빠진 글자는 네모(□)로 표시된다.
///
/// 2,350자를 한 장의 아틀라스에 담기 위해 글자를 <see cref="SamplingPointSize"/>pt로 작게 그린다(처음 만들 때는 90pt였다).
/// SDF는 거리 값으로 글자 모양을 저장하므로, 그린 크기보다 2~3배 크게 보여도 가장자리가 깨끗하다.
/// 폰트 에셋 파일은 새로 만들지 않고 그 자리에서 다시 굽는다. 그래야 GUID와 머티리얼이 유지되어 씬의 글자 연결이 끊기지 않는다.
/// </remarks>
public static class StaticFontBaker
{
    private const string FontFolder = "Assets/Jamkkaebi/Fonts/TMP";
    private const string ExtraCharactersPath = FontFolder + "/StaticFontExtraCharacters.txt";
    private const string HangulCharsetPath = FontFolder + "/KSX1001_Hangul2350.txt";

    // 아틀라스 설정. 2048 한 장에 2,350자 + 영문·기호(약 2,450자)가 들어가도록 고른 값이다.
    // 글자 칸 ≈ 32pt + 여백 4×2 → 약 40px, 2048² ÷ 40² ≈ 2,600칸. 넘치면 다중 아틀라스로 한 장이 더 생긴다
    internal const int SamplingPointSize = 32;
    internal const int AtlasPadding = 4;
    internal const int AtlasSize = 2048;
    // 이 폰트들은 미니게임 HUD에서만 쓴다. 다른 화면 문구까지 모으면 아틀라스가 불필요하게 커진다
    private const string UiCodeFolder = "Assets/Jamkkaebi/Scripts/UI/Minigame";
    private const string ScenePath = "Assets/Jamkkaebi/Scenes/Minigame.unity";

    // C# 문자열 리터럴. 이스케이프된 따옴표(\")를 건너뛴다
    private static readonly Regex StringLiteral = new Regex("\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.Compiled);
    // 화면에 나오지 않는 문자열: 로그, Inspector 설명(Tooltip·Header), 주석
    private static readonly Regex NonDisplayCode = new Regex(
        @"Debug\.Log\w*\([^;]*\);|\[(?:Tooltip|Header)\([^\]]*\)\]|//[^\n]*", RegexOptions.Compiled);
    // 씬·프리팹 YAML의 TMP 텍스트 한 줄. 한글은 "손..."처럼 이스케이프되어 저장된다
    private static readonly Regex YamlText = new Regex(@"^\s*m_text:\s*(.*)$", RegexOptions.Compiled | RegexOptions.Multiline);

    [MenuItem("Jamkkaebi/Fonts/Bake Static Font Atlases")]
    public static void Bake()
    {
        string characters = CollectCharacters();
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontFolder });
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[StaticFontBaker] {FontFolder}에서 TMP 폰트 에셋을 찾지 못했습니다.");
            return;
        }

        var report = new StringBuilder();
        report.AppendLine($"[StaticFontBaker] 글자 {characters.Length}개를 폰트 {guids.Length}개에 구웠습니다.");

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar("Bake Static Font Atlases", Path.GetFileName(path), (float)i / guids.Length);

                var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (!ApplyAtlasSettings(fontAsset))
                {
                    report.AppendLine($"- {fontAsset.name}: 원본 폰트 파일이 연결되어 있지 않아 건너뜀");
                    continue;
                }
                // 글자를 추가하려면 동적이어야 한다. 이전 데이터를 비워 같은 글자 집합이면 항상 같은 결과가 나오게 한다
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                fontAsset.ClearFontAssetData(true);
                fontAsset.TryAddCharacters(characters, out string missing);
                // 다 그린 뒤 정적으로 바꿔, 실행 중에 아틀라스가 더 바뀌지 않게 한다
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
                EditorUtility.SetDirty(fontAsset);

                report.Append($"- {fontAsset.name}: 아틀라스 {fontAsset.atlasTextures.Length}장");
                report.AppendLine(string.IsNullOrEmpty(missing) ? "" : $", 폰트에 없는 글자: {Visible(missing)}");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        Debug.Log(report.ToString());
    }

    /// <summary>
    /// 그리는 크기·여백·아틀라스 크기를 바꾼다. 글자 높이·줄 간격 같은 수치(faceInfo)도 새 크기로 다시 읽어야
    /// 줄 간격이 어긋나지 않는다.
    /// </summary>
    /// <returns>원본 폰트 파일이 있어 설정을 바꿨으면 true.</returns>
    private static bool ApplyAtlasSettings(TMP_FontAsset fontAsset)
    {
        if (fontAsset.sourceFontFile == null) return false;

        // 원본 폰트를 새 크기로 열어 그 크기 기준의 글꼴 수치를 읽는다
        FontEngine.InitializeFontEngine();
        FontEngine.LoadFontFace(fontAsset.sourceFontFile, SamplingPointSize);
        fontAsset.faceInfo = FontEngine.GetFaceInfo();

        // 아틀라스 크기와 여백은 공개 설정 함수가 없어 직렬화된 값을 직접 바꾼다
        var so = new SerializedObject(fontAsset);
        so.FindProperty("m_AtlasWidth").intValue = AtlasSize;
        so.FindProperty("m_AtlasHeight").intValue = AtlasSize;
        so.FindProperty("m_AtlasPadding").intValue = AtlasPadding;
        so.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = true;
        // 나중에 Font Asset Creator로 다시 만들 때 같은 값이 나오도록 생성 기록도 맞춘다
        SetIfExists(so, "m_CreationSettings.pointSize", SamplingPointSize);
        SetIfExists(so, "m_CreationSettings.padding", AtlasPadding);
        SetIfExists(so, "m_CreationSettings.atlasWidth", AtlasSize);
        SetIfExists(so, "m_CreationSettings.atlasHeight", AtlasSize);
        so.ApplyModifiedPropertiesWithoutUndo();

        // SDF 셰이더는 여백 크기로 가장자리 두께를 계산한다(GradientScale = 여백 + 1)
        if (fontAsset.material != null)
        {
            fontAsset.material.SetFloat(ShaderUtilities.ID_GradientScale, AtlasPadding + 1);
            fontAsset.material.SetFloat(ShaderUtilities.ID_TextureWidth, AtlasSize);
            fontAsset.material.SetFloat(ShaderUtilities.ID_TextureHeight, AtlasSize);
            EditorUtility.SetDirty(fontAsset.material);
        }
        return true;
    }

    private static void SetIfExists(SerializedObject so, string path, int value)
    {
        SerializedProperty property = so.FindProperty(path);
        if (property != null) property.intValue = value;
    }

    /// <summary>
    /// 화면에 나올 수 있는 글자를 모은다. 같은 글자는 한 번만 넣고, 코드 포인트 순서로 정렬해 매번 같은 결과가 나오게 한다.
    /// </summary>
    private static string CollectCharacters()
    {
        var set = new SortedSet<char>();
        for (char c = (char)0x20; c <= 0x7E; c++) set.Add(c);
        for (char c = '\u3131'; c <= '\u3163'; c++) set.Add(c);   // 한글 자모 ㄱ~ㅣ

        if (File.Exists(HangulCharsetPath)) AddVisible(File.ReadAllText(HangulCharsetPath), set);
        else Debug.LogWarning($"[StaticFontBaker] {HangulCharsetPath}가 없어 한글 2,350자를 굽지 못했습니다.");

        if (Directory.Exists(UiCodeFolder))
        {
            foreach (string file in Directory.EnumerateFiles(UiCodeFolder, "*.cs", SearchOption.AllDirectories))
            {
                AddCodeStrings(File.ReadAllText(file), set);
            }
        }
        if (File.Exists(ScenePath)) AddYamlTexts(File.ReadAllText(ScenePath), set);

        if (File.Exists(ExtraCharactersPath))
        {
            foreach (string line in File.ReadAllLines(ExtraCharactersPath))
            {
                if (!line.TrimStart().StartsWith("#")) AddVisible(line, set);
            }
        }
        return new string(set.ToArray());
    }

    private static void AddCodeStrings(string source, SortedSet<char> set)
    {
        string displayCode = NonDisplayCode.Replace(source, "");
        foreach (Match match in StringLiteral.Matches(displayCode)) AddVisible(match.Value.Trim('"'), set);
    }

    private static void AddYamlTexts(string yaml, SortedSet<char> set)
    {
        foreach (Match match in YamlText.Matches(yaml))
        {
            string value = match.Groups[1].Value.Trim();
            // 따옴표로 감싼 값은 \uXXXX 같은 이스케이프를 풀어야 실제 글자가 된다
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                value = Regex.Unescape(value.Substring(1, value.Length - 2));
            }
            AddVisible(value, set);
        }
    }

    // 줄바꿈·탭 같은 제어 문자는 아틀라스에 그릴 필요가 없다
    private static void AddVisible(string text, SortedSet<char> set)
    {
        foreach (char c in text)
        {
            if (!char.IsControl(c) && !char.IsSurrogate(c)) set.Add(c);
        }
    }

    private static string Visible(string text) => string.Join(" ", text.Select(c => $"'{c}'(U+{(int)c:X4})"));
}
