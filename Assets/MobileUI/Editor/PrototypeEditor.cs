using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PrototypeEditor
{
    [MenuItem("Prototype/Open UI Prototype")]
    public static void Open()
    {
        EditorSceneManager.OpenScene(PrototypeSetup.Shell);
        // Use the editor's Game view size API to create a reusable portrait preset.
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance",BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
        var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes,new[] { Enum.Parse(groupType,"Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var kindType = assembly.GetType("UnityEditor.GameViewSizeType");
        var texts = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null);
        int index = Array.FindIndex(texts,x=>x.Contains("Prototype Portrait"));
        if (index < 0)
        {
            var preset = Activator.CreateInstance(sizeType, new object[] { Enum.Parse(kindType,"FixedResolution"),540,960,"Prototype Portrait" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[] { preset });
            index = ((string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null)).Length - 1;
        }
        var gameType = assembly.GetType("UnityEditor.GameView");
        var game = EditorWindow.GetWindow(gameType);
        gameType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(game,index);
        game.Focus();
        Selection.activeObject = UnityEngine.Object.FindFirstObjectByType<MobilePrototype.TabHost>().gameObject;
    }
    [MenuItem("Prototype/Save Preview Screenshot")]
    public static void Capture()
    {
        if (!EditorApplication.isPlaying) return;
        ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Screenshots/EditorPreview.png"));
    }
}
