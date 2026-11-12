using Jamkkaebi.Scripts.UI.Common;
using Jamkkaebi.Scripts.UI.Minigame;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 미니게임 결과 창 프리팹을 시안대로 만들고, 씬에 창을 띄울 PopupRoot(UILoader)를 놓아 HUD와 연결하는 에디터 도구입니다.
/// </summary>
/// <remarks>
/// 프리팹이 이미 있으면 다시 만들지 물어봅니다. 프리팹에서 직접 고친 내용이 있으면 "그대로 쓰기"를 고릅니다.
/// 예전처럼 씬에 직접 들어 있던 결과 화면(Result)은 이 도구가 지웁니다.
/// 다른 팝업(도움말 등)도 <see cref="SavePrefab{T}"/>·<see cref="RegisterPopup"/>로 같은 PopupRoot에 등록합니다.
/// </remarks>
public static class ResultPopupSetup
{
    public const string PopupRootName = "PopupRoot";
    private const string PrefabFolder = "Assets/Jamkkaebi/Prefabs/UI";
    private const string PrefabPath = PrefabFolder + "/ResultPopup.prefab";
    private const string LegacyResultName = "Result";
    private const string SpriteFolder = "Assets/Jamkkaebi/Art/Common/ResultPanel";
    private const string ThreatIconPath = "Assets/Jamkkaebi/Art/Minigame/TileContents/TileIcon_Threat.png";
    private const string FontFolder = "Assets/Jamkkaebi/Fonts/TMP";
    // 팝업 제목은 국립박물관문화재단클래식체 Bold로 쓴다
    private const string TitleFontFile = "Assets/Jamkkaebi/Fonts/MuseumCulturalFoundationClassic/MuseumCulturalFoundationClassic-Bold.otf";
    private const string BodyFontPath = FontFolder + "/Pretendard-Medium SDF.asset";
    private const int UiLayer = 5;
    private static readonly Color BrownText = new Color32(71, 60, 60, 255);
    private static readonly Color FailOutline = new Color32(61, 59, 83, 255);
    private static readonly Color GreenLabel = new Color32(43, 128, 110, 255);
    private static readonly Color RedLabel = new Color32(184, 46, 53, 255);
    private static readonly Color SparkleColor = new Color(1f, 0.93f, 0.6f, 1f);
    // 버튼 줄의 위쪽 끝. 시안(1080×2400)에서 화면 가운데보다 374.5px 아래
    private const float ButtonsTop = -374.5f;

    [MenuItem("Jamkkaebi/Minigame/Setup Result Popup")]
    public static void SetupMenu()
    {
        var presenter = Object.FindFirstObjectByType<MinigameHudPresenter>();
        Canvas canvas = presenter != null ? presenter.GetComponentInParent<Canvas>() : null;
        if (presenter == null || canvas == null)
        {
            EditorUtility.DisplayDialog("Result Popup", "씬에서 MinigameHudPresenter와 Canvas를 찾지 못했습니다.", "확인");
            return;
        }
        Setup(presenter, canvas);
        EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
        Debug.Log($"[ResultPopupSetup] 결과 창을 연결했습니다. 프리팹: {PrefabPath}. 씬을 저장해주세요.");
    }

    /// <summary>프리팹을 준비하고 캔버스 맨 위에 PopupRoot를 둔 뒤 프레젠터에 UILoader를 연결한다.</summary>
    public static void Setup(MinigameHudPresenter presenter, Canvas canvas)
    {
        ResultPopup prefab = AssetDatabase.LoadAssetAtPath<ResultPopup>(PrefabPath);
        bool rebuild = prefab == null || EditorUtility.DisplayDialog("Result Popup",
            "결과 창 프리팹이 이미 있습니다. 시안대로 다시 만들까요?\n프리팹에서 직접 고친 내용은 사라집니다.", "다시 만들기", "그대로 쓰기");
        if (rebuild) prefab = CreatePrefab();

        Transform legacy = canvas.rootCanvas.transform.Find(LegacyResultName);
        if (legacy != null) Undo.DestroyObjectImmediate(legacy.gameObject);

        RegisterPopup(presenter, canvas, prefab);
    }

    /// <summary>
    /// 캔버스 맨 위에 PopupRoot(UILoader)를 두고(없으면 만든다) 팝업 프리팹을 등록한 뒤, 프레젠터에 UILoader를 연결한다.
    /// </summary>
    public static void RegisterPopup(MinigameHudPresenter presenter, Canvas canvas, UIPopup prefab)
    {
        Transform root = canvas.rootCanvas.transform;
        Transform existing = root.Find(PopupRootName);
        RectTransform popupRoot;
        if (existing != null) popupRoot = (RectTransform)existing;
        else
        {
            popupRoot = CreateRect(PopupRootName, root);
            Undo.RegisterCreatedObjectUndo(popupRoot.gameObject, "Create Popup Root");
        }
        Stretch(popupRoot);
        popupRoot.localScale = Vector3.one;
        // 창은 HUD·족자·도움말보다 위에 떠야 하므로 캔버스의 맨 뒤 형제로 둔다
        popupRoot.SetAsLastSibling();

        UILoader loader = popupRoot.GetComponent<UILoader>();
        if (loader == null) loader = Undo.AddComponent<UILoader>(popupRoot.gameObject);
        var loaderSo = new SerializedObject(loader);
        loaderSo.FindProperty("_popupRoot").objectReferenceValue = popupRoot;
        SerializedProperty prefabs = loaderSo.FindProperty("_popupPrefabs");
        if (prefab != null && !Contains(prefabs, prefab))
        {
            prefabs.arraySize++;
            prefabs.GetArrayElementAtIndex(prefabs.arraySize - 1).objectReferenceValue = prefab;
        }
        loaderSo.ApplyModifiedProperties();

        var presenterSo = new SerializedObject(presenter);
        presenterSo.FindProperty("_uiLoader").objectReferenceValue = loader;
        presenterSo.ApplyModifiedProperties();
    }

    /// <summary>
    /// 시안(Design/Jamkkaebi/UI/Common/buttonPanel의 result·resultFail)대로 결과 창 프리팹을 만들어 저장한다.
    /// </summary>
    /// <remarks>
    /// 좌표는 1080×2400 기준이고, 패널 가운데(= 화면 가운데)를 원점으로 위가 +y다.
    /// 성공·실패 내용은 같은 패널 위에 묶음 두 개로 겹쳐 두고, 결과에 따라 한쪽만 켠다.
    /// </remarks>
    private static ResultPopup CreatePrefab()
    {
        FontWeightSetup.Setup();   // 버튼 글자의 SemiBold 굵기가 대표 에셋에 연결되어 있어야 한다
        TMP_FontAsset titleFont = GetOrCreateTitleFont();
        var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        Material successTitleMaterial = GetOrCreateOutlineMaterial(titleFont, "Outline Light", Color.white);
        Material failTitleMaterial = GetOrCreateOutlineMaterial(titleFont, "Outline Dark", FailOutline);

        RectTransform popupRect = CreateRect("ResultPopup", null);
        Stretch(popupRect);
        var popup = popupRect.gameObject.AddComponent<ResultPopup>();

        // 화면 전체를 덮어 뒤의 HUD 버튼이 눌리지 않게 한다
        Image dim = CreateImage("Dim", popupRect, null);
        Stretch(dim.rectTransform);
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;

        // 열기 연출(팝)로 커졌다 돌아오는 묶음. 어두운 배경까지 커지면 어색하므로 따로 둔다
        RectTransform content = CreateRect("Content", popupRect);
        Stretch(content);

        Image panel = CreateImage("Panel", content, "ResultPanel");
        panel.SetNativeSize();
        RectTransform panelRect = panel.rectTransform;
        PlaceCenter(panelRect, 0f, 0f);
        Sprite failPanelSprite = LoadSprite("ResultPanelFail");

        // ── 성공 ──
        RectTransform success = CreateRect("SuccessContent", panelRect);
        Stretch(success);
        TMP_Text successTitle = CreateText("Title", success, titleFont, 88f, BrownText);
        if (successTitleMaterial != null) successTitle.fontSharedMaterial = successTitleMaterial;
        successTitle.text = "정화 성공";
        PlaceCenter(successTitle.rectTransform, 0f, 117.5f, new Vector2(800f, 120f));
        TMP_Text damage = CreateText("Damage", success, bodyFont, 42f, BrownText);
        damage.text = "손상도 0%";
        PlaceCenter(damage.rectTransform, 0f, 9f, new Vector2(700f, 60f));
        TMP_Text bonus = CreateText("Bonus", success, bodyFont, 42f, BrownText);
        bonus.text = "획득한 정성 0개";
        PlaceCenter(bonus.rectTransform, 0f, -55.5f, new Vector2(700f, 60f));
        Image jeongseong = CreateImage("Jeongseong", success, "ResultJeongseong");
        jeongseong.SetNativeSize();
        PlaceCenter(jeongseong.rectTransform, 0f, -130.5f);

        // 성공 반짝임: 패널 테두리에 걸쳐 앞에 그린다
        GameObject successSparkles = CreateSuccessSparkles(content);

        // ── 실패 ──
        RectTransform fail = CreateRect("FailContent", panelRect);
        Stretch(fail);
        TMP_Text failTitle = CreateText("Title", fail, titleFont, 88f, Color.white);
        if (failTitleMaterial != null) failTitle.fontSharedMaterial = failTitleMaterial;
        failTitle.text = "정화 실패";
        PlaceCenter(failTitle.rectTransform, 0f, 76f, new Vector2(800f, 120f));
        TMP_Text message = CreateText("Message", fail, bodyFont, 42f, Color.white);
        message.text = "유물이 더 상하기 전에 작업을 멈췄어요.\n획득한 추가 정성은 사라져요.";
        message.lineSpacing = 40f;   // 시안의 줄 간격(65px)에 맞춘 값. 글꼴을 바꾸면 다시 맞춘다
        PlaceCenter(message.rectTransform, 0f, -65f, new Vector2(800f, 140f));
        AddThreatIcon(fail, "ThreatTop", new Vector2(236f, 174f), 100f, 0f);
        AddThreatIcon(fail, "ThreatBottom", new Vector2(-231.5f, -177f), 75f, 0.4f);

        // ── 버튼 ──
        // 레이아웃 그룹은 켜진 버튼만 위에서부터 쌓으므로, 숨은 버튼 자리는 비지 않고 당겨진다
        RectTransform buttons = CreateRect("Buttons", content);
        buttons.anchorMin = buttons.anchorMax = new Vector2(0.5f, 0.5f);
        buttons.pivot = new Vector2(0.5f, 1f);
        buttons.anchoredPosition = new Vector2(0f, ButtonsTop);
        buttons.sizeDelta = new Vector2(325f, 260f);
        var layout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 32f;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        Button nextButton = CreateButton("NextButton", buttons, bodyFont, "다음 단계", GreenLabel);
        Button retryButton = CreateButton("RetryButton", buttons, bodyFont, "다시 하기", GreenLabel);
        Button exitButton = CreateButton("ExitButton", buttons, bodyFont, "나가기", RedLabel);

        var so = new SerializedObject(popup);
        so.FindProperty("_popTarget").objectReferenceValue = content;
        so.FindProperty("_panelImage").objectReferenceValue = panel;
        so.FindProperty("_successPanelSprite").objectReferenceValue = panel.sprite;
        so.FindProperty("_failPanelSprite").objectReferenceValue = failPanelSprite;
        so.FindProperty("_successContent").objectReferenceValue = success.gameObject;
        so.FindProperty("_failContent").objectReferenceValue = fail.gameObject;
        SerializedProperty effects = so.FindProperty("_successEffects");
        effects.arraySize = 1;
        effects.GetArrayElementAtIndex(0).objectReferenceValue = successSparkles;
        so.FindProperty("_damageText").objectReferenceValue = damage;
        so.FindProperty("_bonusText").objectReferenceValue = bonus;
        so.FindProperty("_nextButton").objectReferenceValue = nextButton;
        so.FindProperty("_retryButton").objectReferenceValue = retryButton;
        so.FindProperty("_exitButton").objectReferenceValue = exitButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        return SavePrefab<ResultPopup>(popupRect.gameObject, PrefabPath);
    }

    /// <summary>
    /// 메모리에서 만든 창 오브젝트를 프리팹으로 저장(같은 경로면 덮어쓰기)하고 원래 오브젝트는 지운다.
    /// </summary>
    /// <returns>저장된 프리팹에 붙은 T 컴포넌트.</returns>
    public static T SavePrefab<T>(GameObject source, string path) where T : UIPopup
    {
        EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(source, path);
        Object.DestroyImmediate(source);
        return saved.GetComponent<T>();
    }

    /// <summary>
    /// 제목 글꼴(국립박물관문화재단클래식체 Bold)의 TMP 폰트 에셋을 돌려준다. 없으면 만들고 정적 아틀라스로 굽는다.
    /// </summary>
    /// <remarks>
    /// 이 프로젝트의 TMP 폰트는 실행 중에 바뀌지 않도록 모두 정적(Static)으로 구워 둔다.
    /// 굽기 도구는 폴더의 모든 폰트를 다시 구우므로 처음 한 번은 시간이 조금 걸린다.
    /// </remarks>
    private static TMP_FontAsset GetOrCreateTitleFont()
    {
        string assetPath = $"{FontFolder}/{System.IO.Path.GetFileNameWithoutExtension(TitleFontFile)} SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        TMP_FontAsset created = MinigameHudSetup.GetOrCreateFontAsset(TitleFontFile);
        if (created == null)
        {
            Debug.LogWarning($"[ResultPopupSetup] 제목 글꼴을 찾지 못했습니다: {TitleFontFile}");
            return null;
        }
        StaticFontBaker.Bake();
        return created;
    }

    /// <summary>패널 테두리 근처에서 차례로 나타나 반짝이는 크고 작은 별 일곱 개.</summary>
    /// <remarks>
    /// 배치는 프리팹에서 직접 바꾸면 된다(SuccessSparkles 아래 Sparkle 오브젝트를 옮기거나, 복제해서 늘리거나, 지운다).
    /// 이 값은 프리팹을 처음 만들 때의 기본 배치일 뿐이다.
    /// </remarks>
    private static GameObject CreateSuccessSparkles(Transform parent)
    {
        RectTransform group = CreateRect("SuccessSparkles", parent);
        Stretch(group);
        // (가운데 기준 위치, 크기, 주기, 박자, 나타나는 시각). 큰 별은 느리게, 작은 별은 빠르게 반짝인다
        var sparkles = new (Vector2 Position, float Size, float Period, float Phase, float Delay)[]
        {
            (new Vector2(-430f, 290f), 140f, 2.4f, 0.00f, 0.15f),
            (new Vector2(-330f, 345f), 50f, 1.4f, 0.50f, 0.25f),
            (new Vector2(300f, 335f), 80f, 1.9f, 0.35f, 0.35f),
            (new Vector2(460f, 200f), 110f, 2.1f, 0.60f, 0.45f),
            (new Vector2(-465f, -170f), 65f, 1.6f, 0.25f, 0.55f),
            (new Vector2(410f, -300f), 125f, 2.3f, 0.80f, 0.65f),
            (new Vector2(-250f, -335f), 40f, 1.3f, 0.10f, 0.75f),
        };
        for (int i = 0; i < sparkles.Length; i++)
        {
            var s = sparkles[i];
            Image sparkle = CreateImage($"Sparkle{i + 1}", group, "ResultSparkle");
            sparkle.color = SparkleColor;
            PlaceCenter(sparkle.rectTransform, s.Position.x, s.Position.y, new Vector2(s.Size, s.Size));
            var pulse = sparkle.gameObject.AddComponent<PulseEffect>();
            var so = new SerializedObject(pulse);
            so.FindProperty("_alphaRange").vector2Value = new Vector2(0.15f, 1f);
            so.FindProperty("_scaleRange").vector2Value = new Vector2(0.45f, 1f);
            so.FindProperty("_period").floatValue = s.Period;
            so.FindProperty("_phaseOffset").floatValue = s.Phase;
            so.FindProperty("_fadeInDuration").floatValue = 0.25f;
            so.FindProperty("_startDelay").floatValue = s.Delay;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return group.gameObject;
    }

    private static void AddThreatIcon(Transform parent, string name, Vector2 center, float size, float phaseOffset)
    {
        Image icon = CreateImage(name, parent, null);
        icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ThreatIconPath);
        icon.preserveAspect = true;
        PlaceCenter(icon.rectTransform, center.x, center.y, new Vector2(size, size));
        var bob = icon.gameObject.AddComponent<FloatingBob>();
        var so = new SerializedObject(bob);
        so.FindProperty("_phaseOffset").floatValue = phaseOffset;   // 두 아이콘이 엇박자로 움직이게 한다
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Button CreateButton(string name, Transform parent, TMP_FontAsset font, string label, Color labelColor)
    {
        Image image = CreateImage(name, parent, "ResultButton");
        image.SetNativeSize();
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText("Label", image.rectTransform, font, 56f, labelColor);
        text.fontWeight = FontWeight.SemiBold;   // 대표 에셋(Medium)의 Font Weights 표에서 SemiBold 에셋을 찾아 그린다
        Stretch(text.rectTransform);
        text.text = label;
        return button;
    }

    /// <summary>
    /// 제목용 외곽선 머티리얼을 글꼴 폴더에 만든다. 이미 있으면 그대로 쓴다.
    /// </summary>
    /// <remarks>
    /// TMP의 외곽선은 머티리얼 값이다. 글꼴 기본 머티리얼을 고치면 그 글꼴을 쓰는 모든 글자에 외곽선이 생기므로,
    /// 복사본(머티리얼 프리셋)을 따로 만들어 제목에만 쓴다. 두께는 만든 머티리얼의 Outline Thickness로 조절한다.
    /// </remarks>
    private static Material GetOrCreateOutlineMaterial(TMP_FontAsset font, string suffix, Color outlineColor)
    {
        if (font == null) return null;
        string path = $"{FontFolder}/{font.name} {suffix}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            // 폰트를 다시 구우면 아틀라스가 바뀌므로, 두께·색은 그대로 두고 아틀라스 관련 값만 폰트에 맞춘다
            existing.SetTexture(ShaderUtilities.ID_MainTex, font.material.GetTexture(ShaderUtilities.ID_MainTex));
            existing.SetFloat(ShaderUtilities.ID_GradientScale, font.material.GetFloat(ShaderUtilities.ID_GradientScale));
            existing.SetFloat(ShaderUtilities.ID_TextureWidth, font.material.GetFloat(ShaderUtilities.ID_TextureWidth));
            existing.SetFloat(ShaderUtilities.ID_TextureHeight, font.material.GetFloat(ShaderUtilities.ID_TextureHeight));
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var material = new Material(font.material);
        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        material.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.35f);
        material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.35f);   // 외곽선이 글자 안쪽을 덜 먹도록 글자를 살짝 키운다
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Sprite LoadSprite(string spriteName)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{spriteName}.png");
        if (sprite == null) Debug.LogWarning($"[ResultPopupSetup] 스프라이트를 찾지 못했습니다: {spriteName}");
        return sprite;
    }

    private static bool Contains(SerializedProperty array, Object target)
    {
        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == target) return true;
        }
        return false;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent != null ? parent.gameObject.layer : UiLayer;
        var rect = (RectTransform)go.transform;
        if (parent != null) rect.SetParent(parent, false);
        return rect;
    }

    private static Image CreateImage(string name, Transform parent, string spriteName)
    {
        var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        if (spriteName != null) image.sprite = LoadSprite(spriteName);
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, float size, Color color)
    {
        var text = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>부모의 가운데에서 (x, y)만큼 떨어진 곳에 가운데를 맞춰 놓는다. size를 주지 않으면 지금 크기를 쓴다.</summary>
    private static void PlaceCenter(RectTransform rect, float x, float y, Vector2? size = null)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        if (size.HasValue) rect.sizeDelta = size.Value;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
