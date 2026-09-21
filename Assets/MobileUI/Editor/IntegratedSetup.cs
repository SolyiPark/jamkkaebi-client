using System;
using System.IO;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using MobilePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class IntegratedSetup
{
    public const string Workshop = "Assets/MobileUI/Scenes/Workshop.unity";
    private static readonly Color Paper = new Color(.985f, .985f, .975f);
    private static readonly Color Ink = new Color(.10f, .17f, .17f);

    public static void Prepare()
    {
        // Build a connected workshop from the existing game's scene and prefab.
        var scene = EditorSceneManager.OpenScene("Assets/Jamkkaebi/Scenes/Minigame.unity");
        var adapter = Object.FindFirstObjectByType<MinigameSessionAdapter>();
        var camera = Object.FindFirstObjectByType<Camera>();
        foreach (var events in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
            Object.DestroyImmediate(events.gameObject);
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            Object.DestroyImmediate(canvas.gameObject);
        var root = new GameObject("Workshop Root", typeof(MirrorSceneRoot)).GetComponent<MirrorSceneRoot>();
        foreach (var obj in scene.GetRootGameObjects())
            if (obj != root.gameObject) obj.transform.SetParent(root.transform, true);
        root.sceneCamera = camera;
        camera.tag = "Untagged";
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Paper;
        camera.orthographic = true;
        camera.enabled = false;
        foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(listener);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

        Directory.CreateDirectory("Assets/MobileUI/Prefabs");
        Directory.CreateDirectory("Assets/MobileUI/Materials");
        AssetDatabase.Refresh();
        const string materialPath = "Assets/MobileUI/Materials/SpriteUnlit.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Jamkkaebi/Prefabs/TileView.prefab");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/MobileUI/Art/Square.png");
        instance.GetComponent<SpriteRenderer>().sharedMaterial = material;
        var connectedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, "Assets/MobileUI/Prefabs/WorkshopTile.prefab");
        Object.DestroyImmediate(instance);
        var serialized = new SerializedObject(adapter);
        serialized.FindProperty("_tilePrefab").objectReferenceValue = connectedPrefab;
        serialized.FindProperty("_inputCamera").objectReferenceValue = camera;
        serialized.FindProperty("_usePointerEvents").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MobileUI/Fonts/NotoSansCJKkr-Regular.otf");
        var ui = new GameObject("Workshop UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        ui.transform.SetParent(root.transform, false);
        ui.renderMode = RenderMode.ScreenSpaceCamera;
        ui.worldCamera = camera;
        ui.planeDistance = 5;
        var scaler = ui.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1600);
        scaler.matchWidthOrHeight = 0;
        var presenter = root.gameObject.AddComponent<WorkshopPresenter>();
        presenter.adapter = adapter;
        presenter.sceneCamera = camera;
        UIFactory.Label("Heading", ui.transform, font, "나만의 작업대", 40, Ink,
            new Vector2(0, .94f), Vector2.one, new Vector2(35, 0), new Vector2(-35, -12));
        presenter.summary = UIFactory.Label("Session", ui.transform, font, "발굴 준비 중", 30, Ink,
            new Vector2(0, .81f), new Vector2(1, .94f));
        presenter.feedback = UIFactory.Label("Feedback", ui.transform, font, "타일을 선택해 발굴하세요.", 26, Ink,
            new Vector2(0, .14f), new Vector2(1, .20f));
        presenter.toolButtons = new Button[3];
        string[] names = { "안전", "공격", "정찰" };
        for (int i = 0; i < 3; i++)
            presenter.toolButtons[i] = Button("Tool_" + i, names[i], ui.transform, font,
                new Vector2(.06f + i * .30f, .065f), new Vector2(.34f + i * .30f, .135f));
        presenter.restartButton = Button("Restart", "새 발굴", ui.transform, font,
            new Vector2(.35f, .007f), new Vector2(.65f, .057f));
        MirrorSceneRoot.SetLayer(root.transform, 9);
        camera.cullingMask = 1 << 9;
        root.uiRaycasters = new[] { ui.GetComponent<GraphicRaycaster>() };
        EditorSceneManager.SaveScene(scene, Workshop);

        // Preserve the UI asset GUIDs, while converting its sprite material to URP.
        foreach (string path in new[] { "Home", "Collection", "Friends" })
        {
            var content = EditorSceneManager.OpenScene($"Assets/MobileUI/Scenes/{path}.unity");
            foreach (var renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)) renderer.sharedMaterial = material;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            EditorSceneManager.SaveScene(content);
        }
        var shell = EditorSceneManager.OpenScene(PrototypeSetup.Shell);
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        EnsureSingleVerification(shell);
        EditorSceneManager.SaveScene(shell);
        PrototypeSetup.SyncBuildScenes();
        PlayerSettings.defaultScreenWidth = 540;
        PlayerSettings.defaultScreenHeight = 960;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        AssetDatabase.SaveAssets();
        Debug.Log("INTEGRATION_PREPARED");
    }

    internal static void EnsureSingleVerification(UnityEngine.SceneManagement.Scene scene)
    {
        var verifications = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<IntegratedVerification>(true)).ToArray();
        if (verifications.Length == 0)
        {
            var verification = new GameObject("Integrated Verification (opt-in)", typeof(IntegratedVerification));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(verification, scene);
            return;
        }

        // Remove only duplicate components; their objects may contain other scene content.
        foreach (var duplicate in verifications.Skip(1)) Object.DestroyImmediate(duplicate);
    }

    private static Button Button(string name, string label, Transform parent, Font font, Vector2 min, Vector2 max)
    {
        var image = UIFactory.Image(name, parent, new Color(.70f, .43f, .26f), min, max);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        UIFactory.Label("Label", image.transform, font, label, 28, Color.white, Vector2.zero, Vector2.one);
        return button;
    }

    public static void Build()
    {
        PrototypeSetup.SyncBuildScenes();
        ValidateScenes();
        Directory.CreateDirectory("Builds/Integrated");
        var result = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, "Builds/Integrated/Jamkkaebi.exe",
            BuildTarget.StandaloneWindows64, BuildOptions.Development);
        if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception("Integrated build failed: " + result.summary.result);
        Debug.Log("INTEGRATED_BUILD_SUCCESS");
    }

    public static void ValidateScenes()
    {
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var scene = EditorSceneManager.OpenScene(entry.path);
            foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    throw new Exception("Missing script in " + entry.path + ": " + transform.name);
                foreach (var component in transform.GetComponents<Component>())
                {
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.NextVisible(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                            throw new Exception("Missing reference in " + entry.path + ": " + transform.name + "/" + property.propertyPath);
                }
            }
        }
        EditorSceneManager.OpenScene(PrototypeSetup.Shell);
        Debug.Log("INTEGRATED_SCENE_REFERENCES_VALID");
    }
}
