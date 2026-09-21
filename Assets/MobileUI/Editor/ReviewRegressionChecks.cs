using System;
using System.Collections.Generic;
using System.Linq;
using MobilePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run in batch mode with -executeMethod ReviewRegressionChecks.Run.
public static class ReviewRegressionChecks
{
    public static void Run()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run these checks in batch mode to protect unsaved editor scenes.");
        EditorSceneManager.OpenScene(PrototypeSetup.Shell);
        var originalScenes = EditorBuildSettings.scenes;
        var originalActiveScene = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var errors = new List<string>();
        void RecordError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(message);
        }
        Application.logMessageReceived += RecordError;
        try
        {
            IntegratedSetup.EnsureSingleVerification(scene);
            IntegratedSetup.EnsureSingleVerification(scene);
            Check(Verifications(scene).Length == 1, "repeated setup keeps one verifier");
            var duplicate = new GameObject("Inactive duplicate", typeof(IntegratedVerification));
            duplicate.SetActive(false);
            var retainedTransform = duplicate.transform;
            IntegratedSetup.EnsureSingleVerification(scene);
            Check(Verifications(scene).Length == 1, "inactive duplicate verifier removed");
            Check(retainedTransform != null, "duplicate owner and other components preserved");

            const string originalMinigame = "Assets/Jamkkaebi/Scenes/Minigame.unity";
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(PrototypeSetup.Shell, true),
                new EditorBuildSettingsScene(originalMinigame, false),
                new EditorBuildSettingsScene(originalMinigame, false)
            };
            PrototypeSetup.SyncBuildScenes();
            PrototypeSetup.SyncBuildScenes();
            var synced = EditorBuildSettings.scenes;
            Check(synced.Count(entry => entry.path == originalMinigame) == 1,
                "scene synchronization removes duplicate paths");
            Check(!synced.Single(entry => entry.path == originalMinigame).enabled,
                "disabled scene remains disabled after repeated synchronization");
            Check(synced[0].path == PrototypeSetup.Shell && synced[0].enabled,
                "shell remains enabled and first");
            var catalog = AssetDatabase.LoadAssetAtPath<TabCatalog>(PrototypeSetup.CatalogPath);
            Check(catalog.tabs.All(tab => synced.Any(entry => entry.path == tab.scenePath && entry.enabled)),
                "new catalog scenes included and enabled");

            var safeAreaObject = new GameObject("Safe area requirement");
            var safeArea = safeAreaObject.AddComponent<MobilePrototype.SafeArea>();
            Check(safeAreaObject.GetComponent<RectTransform>() != null && safeArea.enabled,
                "SafeArea adds required RectTransform to an ordinary object");
            safeAreaObject.SetActive(false);
            safeAreaObject.SetActive(true);

            // This is the exact active-AddComponent ordering questioned in the review.
            // WorkshopPresenter has no ExecuteAlways: edit-time creation must not subscribe.
            var presenterObject = new GameObject("Presenter editor lifecycle");
            var presenter = presenterObject.AddComponent<WorkshopPresenter>();
            Check(presenter.adapter == null, "presenter created before assigning adapter in EditMode");
            Object.DestroyImmediate(presenterObject);
            Check(errors.Count == 0, "no errors during editor creation, cleanup and SafeArea reactivation: " +
                string.Join("; ", errors));
            Debug.Log("REVIEW_REGRESSION_CHECKS_PASSED");
        }
        finally
        {
            Application.logMessageReceived -= RecordError;
            EditorBuildSettings.scenes = originalScenes;
            EditorSceneManager.CloseScene(scene, true);
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
                SceneManager.SetActiveScene(originalActiveScene);
        }
    }

    private static IntegratedVerification[] Verifications(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<IntegratedVerification>(true)).ToArray();

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Debug.Log("PASS " + message);
    }
}
