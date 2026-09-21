using System.IO;
using System.Linq;
using MobilePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PrototypeSetup
{
    private static readonly Color Ink = new Color(.10f,.17f,.17f);
    private static readonly Color Paper = new Color(.985f,.985f,.975f);
    private static readonly Color Line = new Color(.74f,.77f,.73f);
    private static Font font;
    private static Sprite square;
    public const string Shell = "Assets/Scenes/UIPrototype.unity";

    [MenuItem("Prototype/Generate Initial Demo (replaces demo scenes)")]
    public static void Create()
    {
        Directory.CreateDirectory("Assets/Configuration"); Directory.CreateDirectory("Assets/Art"); Directory.CreateDirectory("Assets/Scenes");
        AssetDatabase.Refresh();
        font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJKkr-Regular.otf");
        if (!font) throw new System.Exception("Korean font missing.");
        var pixels = new Texture2D(16,16);
        pixels.SetPixels(Enumerable.Repeat(Color.white,256).ToArray()); pixels.Apply();
        File.WriteAllBytes("Assets/Art/Square.png", pixels.EncodeToPNG()); Object.DestroyImmediate(pixels);
        AssetDatabase.ImportAsset("Assets/Art/Square.png");
        var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Art/Square.png");
        importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 16;
        importer.filterMode = FilterMode.Point; importer.SaveAndReimport();
        square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Square.png");
        var catalog = AssetDatabase.LoadAssetAtPath<TabCatalog>("Assets/Configuration/MainTabs.asset");
        if (!catalog) { catalog = ScriptableObject.CreateInstance<TabCatalog>(); AssetDatabase.CreateAsset(catalog, "Assets/Configuration/MainTabs.asset"); }
        catalog.hideFlags = HideFlags.DontUnloadUnusedAsset;
        catalog.font = font; catalog.surface = Paper; catalog.tabs.Clear();
        string[] ids = { "Home", "Workshop", "Collection", "Friends" };
        string[] names = { "홈", "작업대", "도감", "친구" };
        string[] headings = { "오늘의 전시", "나만의 작업대", "차곡차곡 모은 기록", "함께하는 공간" };
        Color[] accents = { new Color(.25f,.48f,.42f), new Color(.70f,.43f,.26f), new Color(.39f,.43f,.65f), new Color(.61f,.40f,.51f) };
        for (int i=0; i<4; i++)
        {
            string path = $"Assets/Scenes/{ids[i]}.unity";
            catalog.tabs.Add(new TabDefinition { id = ids[i], title = names[i], scenePath = path, showProfile = i == 0, markerColor = accents[i] });
            CreateDemo(path, headings[i], accents[i]);
        }
        catalog.hideFlags = HideFlags.None;
        EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets(); CreateShell(catalog);
        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
        PlayerSettings.productName = "Mobile UI Prototype"; PlayerSettings.companyName = "Prototype";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false; PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.defaultScreenWidth = 540; PlayerSettings.defaultScreenHeight = 960;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true; PlayerSettings.runInBackground = true;
        SyncBuildScenes(); AssetDatabase.SaveAssets(); Debug.Log("PROTOTYPE_GENERATED");
    }
    private static void CreateShell(TabCatalog catalog)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var backgroundCamera = new GameObject("Shell Camera",typeof(Camera)).GetComponent<Camera>();
        backgroundCamera.clearFlags = CameraClearFlags.SolidColor; backgroundCamera.backgroundColor = Paper;
        backgroundCamera.cullingMask = 0; backgroundCamera.gameObject.AddComponent<AudioListener>();
        new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
        var canvas = new GameObject("App UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080,1920); scaler.matchWidthOrHeight = 0;
        UIFactory.Image("Background",canvas.transform,Paper,Vector2.zero,Vector2.one);
        var safe = UIFactory.Rect("Safe Area",canvas.transform,Vector2.zero,Vector2.one); safe.gameObject.AddComponent<SafeArea>();
        var view = UIFactory.Rect("Scene Mirror",safe,Vector2.zero,Vector2.one,new Vector2(0,124),new Vector2(0,-178));
        var mirror = view.gameObject.AddComponent<RawImage>(); mirror.color = Color.white;
        var bridge = view.gameObject.AddComponent<MirrorInput>();
        var header = UIFactory.Image("Home Profile",safe,Paper,new Vector2(0,1),Vector2.one,new Vector2(0,-178),Vector2.zero);
        var source = new GameObject("Profile Data (dummy publisher)",typeof(ProfileDataSource)).GetComponent<ProfileDataSource>();
        var profile = header.gameObject.AddComponent<ProfileBar>(); profile.source = source;
        profile.avatar = UIFactory.Image("Avatar",header.transform,Line,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(40,-38),new Vector2(116,38));
        profile.avatar.preserveAspect = true;
        profile.nickname = UIFactory.Label("Nickname",header.transform,font,"닉네임",40,Ink,new Vector2(0,.5f),new Vector2(1,1),new Vector2(144,-5),new Vector2(-140,-22),TextAnchor.MiddleLeft);
        profile.streak = UIFactory.Label("Consecutive days",header.transform,font,"연속 12일",34,new Color(.68f,.26f,.19f),Vector2.zero,new Vector2(1,.5f),new Vector2(144,28),new Vector2(-140,4),TextAnchor.MiddleLeft);
        UIFactory.Label("Placeholder",header.transform,font,"*",36,new Color(.55f,.58f,.56f),new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-104,-30),new Vector2(-44,30));
        UIFactory.Image("Divider",header.transform,Line,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,3));
        var bar = UIFactory.Image("Bottom Toolbar",safe,Paper,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,124));
        var items = UIFactory.Rect("Tabs",bar.transform,Vector2.zero,Vector2.one);
        UIFactory.Image("Divider",bar.transform,Line,new Vector2(0,1),Vector2.one,new Vector2(0,-3),Vector2.zero);
        var status = UIFactory.Label("Loading Status",safe,font,"불러오는 중…",32,Ink,new Vector2(0,.45f),new Vector2(1,.55f),new Vector2(30,0),new Vector2(-30,0));
        var host = new GameObject("Tab Host",typeof(TabHost)).GetComponent<TabHost>();
        host.catalog = catalog; host.viewport = view; host.mirror = mirror; host.input = bridge;
        host.profileBar = header.gameObject; host.toolbar = items; host.status = status;
        new GameObject("Verification (command line only)",typeof(PrototypeVerification));
        EditorSceneManager.SaveScene(scene,Shell);
    }
    private static void CreateDemo(string path, string title, Color accent)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root = new GameObject("Mirror Scene Root",typeof(MirrorSceneRoot)).GetComponent<MirrorSceneRoot>();
        var camera = new GameObject("Mirror Camera",typeof(Camera)).GetComponent<Camera>();
        camera.transform.SetParent(root.transform); camera.transform.localPosition = new Vector3(0,0,-10);
        camera.orthographic = true; camera.orthographicSize = 5.5f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Paper;
        camera.enabled = false; root.sceneCamera = camera;
        var canvas = new GameObject("Scene UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.transform.SetParent(root.transform,false); canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera; canvas.planeDistance = 5;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080,1600); scaler.matchWidthOrHeight = 0;
        UIFactory.Label("Heading",canvas.transform,font,title,38,Ink,new Vector2(0,1),Vector2.one,new Vector2(64,-150),new Vector2(-64,-70),TextAnchor.MiddleLeft);
        UIFactory.Label("Description",canvas.transform,font,"작은 움직임이 모이는 곳",26,new Color(.55f,.58f,.56f),new Vector2(0,1),Vector2.one,new Vector2(64,-204),new Vector2(-64,-152),TextAnchor.MiddleLeft);
        UIFactory.Image("Accent",canvas.transform,accent,new Vector2(0,1),new Vector2(0,1),new Vector2(64,-236),new Vector2(124,-231));
        var moving = Square("Moving Square",root.transform,accent,new Vector3(0,1.2f,0),new Vector3(1.25f,1.25f,1)); moving.AddComponent<DemoMotion>();
        var drag = Square("Drag Square",root.transform,Color.Lerp(accent,Color.white,.40f),new Vector3(0,-.9f,0),new Vector3(1.1f,1.1f,1));
        drag.AddComponent<BoxCollider2D>(); drag.AddComponent<DemoDraggable>().sceneCamera = camera;
        UIFactory.Label("Drag hint",canvas.transform,font,"사각형을 움직여 보세요",26,new Color(.55f,.58f,.56f),new Vector2(0,0),new Vector2(1,0),new Vector2(40,244),new Vector2(-40,294));
        var buttonImage = UIFactory.Image("Counter Button",canvas.transform,accent,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-180,112),new Vector2(180,210));
        buttonImage.raycastTarget = true;
        var button = buttonImage.gameObject.AddComponent<Button>(); button.targetGraphic = buttonImage;
        var label = UIFactory.Label("Count",button.transform,font,"터치  0",32,Color.white,Vector2.zero,Vector2.one);
        var counter = button.gameObject.AddComponent<DemoCounter>(); counter.button = button; counter.label = label;
        root.uiRaycasters = new[] { canvas.GetComponent<GraphicRaycaster>() };
        MirrorSceneRoot.SetLayer(root.transform,8); camera.cullingMask = 1 << 8;
        EditorSceneManager.SaveScene(scene,path);
    }
    private static GameObject Square(string name, Transform parent, Color color, Vector3 position, Vector3 scale)
    {
        var obj = new GameObject(name,typeof(SpriteRenderer)); obj.transform.SetParent(parent,false);
        obj.transform.localPosition = position; obj.transform.localScale = scale;
        obj.GetComponent<SpriteRenderer>().sprite = square; obj.GetComponent<SpriteRenderer>().color = color; return obj;
    }
    [MenuItem("Prototype/Sync Build Scenes From Catalog")]
    public static void SyncBuildScenes()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<TabCatalog>("Assets/Configuration/MainTabs.asset");
        EditorBuildSettings.scenes = new[] { Shell }.Concat(catalog.tabs.Select(t=>t.scenePath)).Distinct().Select(path=>new EditorBuildSettingsScene(path,true)).ToArray();
    }
    public static void BuildPreview()
    {
        SyncBuildScenes(); Directory.CreateDirectory("Builds/Preview");
        var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, "Builds/Preview/MobileUIPrototype.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("Preview build failed: " + report.summary.result);
        Debug.Log("PROTOTYPE_BUILD_SUCCESS");
    }
}
