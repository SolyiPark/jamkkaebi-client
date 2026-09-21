using System;
using System.IO;
using System.Linq;
using MobilePrototype;
using UnityEditor;
using UnityEngine;

public static class PrototypeSetup
{
    public const string Shell = "Assets/MobileUI/Scenes/UIPrototype.unity";
    public const string CatalogPath = "Assets/MobileUI/Configuration/MainTabs.asset";

    [MenuItem("Prototype/Sync Build Scenes From Catalog")]
    public static void SyncBuildScenes()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<TabCatalog>(CatalogPath);
        if (!catalog) throw new InvalidOperationException("MainTabs catalog is missing.");
        var existingStates = EditorBuildSettings.scenes
            .GroupBy(scene => scene.path)
            .ToDictionary(group => group.Key, group => group.First().enabled);
        var paths = new[] { Shell }.Concat(catalog.tabs.Select(t => t.scenePath))
            .Concat(new[] { "Assets/Jamkkaebi/Scenes/Minigame.unity" })
            .Concat(EditorBuildSettings.scenes.Where(s => File.Exists(s.path)).Select(s => s.path))
            .Distinct().ToArray();
        foreach (string path in paths)
            if (!File.Exists(path)) throw new InvalidOperationException("Missing build scene: " + path);
        EditorBuildSettings.scenes = paths.Select(path => new EditorBuildSettingsScene(path,
            !existingStates.TryGetValue(path, out bool enabled) || enabled)).ToArray();
    }

    public static void BuildPreview() => IntegratedSetup.Build();
}
