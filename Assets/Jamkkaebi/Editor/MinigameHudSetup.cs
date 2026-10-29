using System.IO;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using Jamkkaebi.Scripts.UI.Common;
using Jamkkaebi.Scripts.UI.Minigame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// 미니게임 HUD를 피그마 시안(360×800 @3x = 1080×2400) 배치대로 Minigame 씬에 만드는 에디터 도구입니다.
/// </summary>
/// <remarks>
/// 한 번 만든 뒤에는 씬에서 직접 수정합니다. 다시 실행하면 기존 HUD를 지우고 새로 만들므로 수정 내용이 사라집니다.
/// 좌표는 모두 1080×2400 기준 픽셀이며, 위쪽 요소는 SafeArea 위에, 도구 버튼은 아래에 붙입니다.
/// </remarks>
public static class MinigameHudSetup
{
    private const string ScenePath = "Assets/Jamkkaebi/Scenes/Minigame.unity";
    private const string SpriteFolder = "Assets/Jamkkaebi/Art/Minigame/UI";
    private const string PretendardFolder = "Assets/Jamkkaebi/Fonts/Pretendard";
    private const string SejongFolder = "Assets/Jamkkaebi/Fonts/KingSejongInstitute";
    private const string FontAssetFolder = "Assets/Jamkkaebi/Fonts/TMP";
    private const string HudName = "MinigameHud";

    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 2400f);
    private static readonly Color DarkText = new Color(0.2f, 0.2f, 0.2f);
    private static readonly Color DamageRed = new Color(0.61f, 0.25f, 0.25f);

    [MenuItem("Jamkkaebi/Minigame/Build HUD")]
    public static void Build()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            EditorUtility.DisplayDialog("Minigame HUD", $"{ScenePath}를 열고 실행해주세요.", "확인");
            return;
        }
        if (Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
        {
            EditorUtility.DisplayDialog("Minigame HUD",
                "TMP 기본 리소스가 없습니다.\nWindow > TextMeshPro > Import TMP Essential Resources를 먼저 실행해주세요.", "확인");
            return;
        }

        var adapter = Object.FindFirstObjectByType<MinigameSessionAdapter>();
        var safeArea = Object.FindFirstObjectByType<SafeArea>();
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (adapter == null || safeArea == null || canvas == null)
        {
            EditorUtility.DisplayDialog("Minigame HUD", "씬에서 MinigameSessionAdapter, SafeArea, Canvas를 모두 찾지 못했습니다.", "확인");
            return;
        }

        Transform existing = safeArea.transform.Find(HudName);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Minigame HUD", "이미 HUD가 있습니다. 지우고 다시 만들까요?\n씬에서 수정한 내용은 사라집니다.", "다시 만들기", "취소"))
                return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        // 시안 기준: 숫자는 프리텐다드 Medium, 제목·라벨은 세종학당체(굵기별), 결과 설명은 프리텐다드 Regular
        TMP_FontAsset numberFont = GetOrCreateFontAsset(FindFontFile(PretendardFolder, "Medium"));
        TMP_FontAsset bodyFont = GetOrCreateFontAsset(FindFontFile(PretendardFolder, "Regular")) ?? numberFont;
        // 세종학당체가 없으면 프리텐다드로 대신한다
        TMP_FontAsset titleFont = GetOrCreateFontAsset(FindFontFile(SejongFolder, "Bold"))
            ?? GetOrCreateFontAsset(FindFontFile(PretendardFolder, "Bold")) ?? numberFont;
        TMP_FontAsset captionFont = GetOrCreateFontAsset(FindFontFile(SejongFolder, "Regular")) ?? bodyFont;

        ConfigureCanvasScaler(canvas);

        RectTransform hud = CreateRect(HudName, safeArea.transform);
        Stretch(hud);
        Undo.RegisterCreatedObjectUndo(hud.gameObject, "Build Minigame HUD");

        var presenter = hud.gameObject.AddComponent<MinigameHudPresenter>();
        var so = new SerializedObject(presenter);
        so.FindProperty("_adapter").objectReferenceValue = adapter;

        // ── 상단: 도구 남은 횟수, 포기, 도움말 ─────────────────────────────
        Image destructiveCount = CreateImage("DestructiveCount", hud, "DestructiveCount");
        PlaceTopLeft(destructiveCount.rectTransform, 44f, 48f);
        TMP_Text destructiveText = CreateText("Count", destructiveCount.rectTransform, numberFont, 44f, Color.white, TextAlignmentOptions.Center);
        PlaceInside(destructiveText.rectTransform, 110f, 0f, 135f, 106f);
        destructiveText.text = "0";

        Image scoutCount = CreateImage("ScoutCount", hud, "ScoutCount");
        PlaceTopLeft(scoutCount.rectTransform, 324f, 48f);
        TMP_Text scoutText = CreateText("Count", scoutCount.rectTransform, numberFont, 44f, Color.white, TextAlignmentOptions.Center);
        PlaceInside(scoutText.rectTransform, 95f, 0f, 115f, 106f);
        scoutText.text = "0";

        Button quitButton = CreateButton("QuitButton", hud, "QuitButton");
        PlaceTopLeft((RectTransform)quitButton.transform, 698f, 66f);

        Button infoButton = CreateButton("InfoButton", hud, "InfoButton");
        PlaceTopLeft((RectTransform)infoButton.transform, 956f, 65f);

        // ── 손상도 게이지 ──────────────────────────────────────────────
        Image gaugeBackground = CreateImage("DamageGauge", hud, "DamageGaugeBG");
        PlaceTopLeft(gaugeBackground.rectTransform, 47f, 300f);

        Image gaugeFill = CreateImage("Fill", gaugeBackground.rectTransform, "DamageGauge");
        gaugeFill.type = Image.Type.Sliced;   // 좌우 둥근 끝을 유지한 채 가로로만 늘린다
        RectTransform fillRect = gaugeFill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(0f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(0f, gaugeBackground.rectTransform.sizeDelta.y);

        TMP_Text damageLabel = CreateText("Label", gaugeBackground.rectTransform, titleFont, 36f, Color.white, TextAlignmentOptions.Left);
        PlaceInside(damageLabel.rectTransform, 32f, 0f, 200f, 66f);
        damageLabel.text = "손상도";

        TMP_Text damagePercent = CreateText("Percent", gaugeBackground.rectTransform, numberFont, 36f, DamageRed, TextAlignmentOptions.Right);
        PlaceInside(damagePercent.rectTransform, 987f - 32f - 200f, 0f, 200f, 66f);
        damagePercent.text = "0%";

        // ── 찾을 형상 패널 ─────────────────────────────────────────────
        Image panel = CreateImage("ShapePanel", hud, "panel");
        PlaceTopLeft(panel.rectTransform, 47f, 392f);
        panel.raycastTarget = false;

        TMP_Text panelTitle = CreateText("Title", panel.rectTransform, captionFont, 40f, DarkText, TextAlignmentOptions.Center);
        PlaceInside(panelTitle.rectTransform, 0f, 26f, 987f, 64f);
        panelTitle.text = "아래 모양을 찾으세요";

        RectTransform previews = CreateRect("Previews", panel.rectTransform);
        PlaceInside(previews, 60f, 110f, 867f, 210f);
        var layout = previews.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 100f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // ── 하단 도구 버튼 (ToolMode 순서: 안전=곡괭이, 공격=망치, 정찰=돋보기) ──────
        string[] toolSprites = { "Destructive_SafeButton", "Destructive_AttackButton", "scoutButton" };
        float[] toolCenters = { 207f, 540f, 873f };
        SerializedProperty toolButtons = so.FindProperty("_toolButtons");
        toolButtons.arraySize = toolSprites.Length;
        for (int i = 0; i < toolSprites.Length; i++)
        {
            Button button = CreateButton(toolSprites[i], hud, toolSprites[i]);
            button.transition = Selectable.Transition.None;   // 선택·비활성 색은 프레젠터가 직접 칠한다
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(toolCenters[i], 216f);
            toolButtons.GetArrayElementAtIndex(i).objectReferenceValue = button;
        }

        // ── 결과 화면 ────────────────────────────────────────────────
        Image dim = CreateImage("Result", hud, null);
        Stretch(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        Button restartButton = dim.gameObject.AddComponent<Button>();
        restartButton.transition = Selectable.Transition.None;

        Image resultPanel = CreateImage("Panel", dim.rectTransform, "panel");
        RectTransform resultRect = resultPanel.rectTransform;
        resultRect.anchorMin = resultRect.anchorMax = resultRect.pivot = new Vector2(0.5f, 0.5f);
        resultRect.anchoredPosition = Vector2.zero;
        resultPanel.raycastTarget = false;

        TMP_Text resultTitle = CreateText("Title", resultRect, titleFont, 64f, DarkText, TextAlignmentOptions.Center);
        PlaceInside(resultTitle.rectTransform, 0f, 50f, 987f, 100f);
        resultTitle.text = "정화 성공";
        TMP_Text resultBody = CreateText("Body", resultRect, bodyFont, 36f, DarkText, TextAlignmentOptions.Center);
        PlaceInside(resultBody.rectTransform, 40f, 160f, 907f, 150f);
        resultBody.text = "";
        dim.gameObject.SetActive(false);

        // ── 프레젠터 연결 ─────────────────────────────────────────────
        so.FindProperty("_destructiveCountText").objectReferenceValue = destructiveText;
        so.FindProperty("_scoutCountText").objectReferenceValue = scoutText;
        so.FindProperty("_damageGaugeBackground").objectReferenceValue = gaugeBackground.rectTransform;
        so.FindProperty("_damageGaugeFill").objectReferenceValue = fillRect;
        so.FindProperty("_damageLabelText").objectReferenceValue = damageLabel;
        so.FindProperty("_damagePercentText").objectReferenceValue = damagePercent;
        so.FindProperty("_shapePreviewContainer").objectReferenceValue = previews;
        so.FindProperty("_previewSpacing").floatValue = layout.spacing;
        so.FindProperty("_quitButton").objectReferenceValue = quitButton;
        so.FindProperty("_infoButton").objectReferenceValue = infoButton;
        so.FindProperty("_resultRoot").objectReferenceValue = dim.gameObject;
        so.FindProperty("_resultTitleText").objectReferenceValue = resultTitle;
        so.FindProperty("_resultBodyText").objectReferenceValue = resultBody;
        so.FindProperty("_restartButton").objectReferenceValue = restartButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 보드가 패널 아래(≈811px)와 도구 버튼 위(≈1951px) 사이에 오도록 카메라 여백을 맞춘다
        var fitter = Object.FindFirstObjectByType<BoardCameraFitter>();
        if (fitter != null)
        {
            var fitterSo = new SerializedObject(fitter);
            fitterSo.FindProperty("_topReservedRatio").floatValue = 811f / ReferenceResolution.y;
            fitterSo.FindProperty("_bottomReservedRatio").floatValue = (ReferenceResolution.y - 1951f) / ReferenceResolution.y;
            // 시안은 패널과 도구 버튼 사이를 보드가 꽉 채운다. 바깥 격자 선 두께만큼만 남긴다
            fitterSo.FindProperty("_marginInTiles").floatValue = 0.05f;
            fitterSo.ApplyModifiedProperties();
        }

        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        Selection.activeGameObject = hud.gameObject;
        Debug.Log($"[MinigameHudSetup] HUD를 만들었습니다. 제목 글꼴: {(titleFont != null ? titleFont.name : "없음")}. 씬을 저장해주세요.");
    }

    private static void ConfigureCanvasScaler(Canvas canvas)
    {
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) return;
        Undo.RecordObject(scaler, "Configure Canvas Scaler");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;   // 가로를 고정하고, 기기마다 세로 길이만 달라진다
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image CreateImage(string name, Transform parent, string spriteName)
    {
        RectTransform rect = CreateRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        if (spriteName != null)
        {
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{spriteName}.png");
            if (image.sprite == null) Debug.LogWarning($"[MinigameHudSetup] 스프라이트를 찾지 못했습니다: {spriteName}");
            image.SetNativeSize();   // 에셋이 1080×2400 기준으로 내보내져 있어 원래 크기가 곧 디자인 크기다
        }
        return image;
    }

    private static Button CreateButton(string name, Transform parent, string spriteName)
    {
        Image image = CreateImage(name, parent, spriteName);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>부모의 왼쪽 위를 기준으로 (x, y)에 놓는다. 크기는 이미 정해진 값을 쓴다.</summary>
    private static void PlaceTopLeft(RectTransform rect, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
    }

    /// <summary>부모의 왼쪽 위를 기준으로 (x, y, 폭, 높이) 영역에 놓는다.</summary>
    private static void PlaceInside(RectTransform rect, float x, float y, float width, float height)
    {
        PlaceTopLeft(rect, x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>폴더에서 이름에 keyword가 들어간 글꼴 파일을 찾는다. 없으면 폴더의 첫 글꼴을 쓴다.</summary>
    private static string FindFontFile(string folder, string keyword)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return null;
        string[] paths = AssetDatabase.FindAssets("t:Font", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p)
            .ToArray();
        return paths.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).Contains(keyword)) ?? paths.FirstOrDefault();
    }

    /// <summary>글꼴 파일로 동적 TMP 글꼴 에셋을 만든다. 이미 있으면 그것을 쓴다.</summary>
    /// <remarks>동적 모드라 쓰는 글자만 실행 중에 아틀라스에 추가되어 한글 전체를 미리 굽지 않아도 된다.</remarks>
    private static TMP_FontAsset GetOrCreateFontAsset(string fontPath)
    {
        if (string.IsNullOrEmpty(fontPath)) return null;

        string assetPath = $"{FontAssetFolder}/{Path.GetFileNameWithoutExtension(fontPath)} SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (font == null) return null;

        if (!AssetDatabase.IsValidFolder(FontAssetFolder))
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(FontAssetFolder).Replace('\\', '/'), Path.GetFileName(FontAssetFolder));
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        if (fontAsset == null) return null;

        AssetDatabase.CreateAsset(fontAsset, assetPath);
        // 아틀라스와 머티리얼을 같은 에셋 안에 넣어야 에디터를 다시 열어도 사라지지 않는다
        fontAsset.atlasTextures[0].name = $"{font.name} Atlas";
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
        fontAsset.material.name = $"{font.name} Material";
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        AssetDatabase.SaveAssets();
        return fontAsset;
    }
}
