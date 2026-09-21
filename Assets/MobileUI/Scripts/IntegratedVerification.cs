using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MobilePrototype
{
    // Only enabled explicitly in a development player; no automatic gameplay changes.
    public class IntegratedVerification : MonoBehaviour
    {
        private readonly List<string> _results = new List<string>();
        private readonly List<string> _errors = new List<string>();
        private string _output;
        private IEnumerator Start()
        {
            string option = Environment.GetCommandLineArgs().FirstOrDefault(x => x.StartsWith("--verify-output="));
            if (option == null || !Debug.isDebugBuild) yield break;
            _output = option.Substring("--verify-output=".Length);
            Directory.CreateDirectory(_output);
            Application.logMessageReceived += RecordError;
            var routine = Run();
            while (true)
            {
                object next = null;
                bool more;
                try { more = routine.MoveNext(); if (more) next = routine.Current; }
                catch (Exception ex) { _errors.Add(ex.ToString()); Finish(); yield break; }
                if (!more) break;
                yield return next;
            }
            Finish();
        }
        private void RecordError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _errors.Add(message + "\n" + stack);
        }
        private void Finish()
        {
            Application.logMessageReceived -= RecordError;
            Time.timeScale = 1;
            File.WriteAllLines(Path.Combine(_output, "verification.txt"),
                new[] { _errors.Count == 0 ? "ALL CHECKS PASSED" : "CHECKS FAILED" }.Concat(_results).Concat(_errors));
            Application.Quit(_errors.Count == 0 ? 0 : 1);
        }
        private void Check(bool condition, string label)
        {
            if (!condition) throw new Exception(label);
            _results.Add("PASS " + label);
        }
        private Vector2 ScreenPoint(TabHost host, Vector3 world)
        {
            var uv = host.ActiveRoot.sceneCamera.WorldToViewportPoint(world);
            var rect = host.viewport.rect;
            return RectTransformUtility.WorldToScreenPoint(null, host.viewport.TransformPoint(new Vector3(
                Mathf.Lerp(rect.xMin, rect.xMax, uv.x), Mathf.Lerp(rect.yMin, rect.yMax, uv.y))));
        }
        private void Click(TabHost host, Vector3 world)
        {
            var pointer = new PointerEventData(EventSystem.current)
            { pointerId = -1, position = ScreenPoint(host, world), button = PointerEventData.InputButton.Left };
            host.input.OnPointerDown(pointer);
            host.input.OnPointerUp(pointer);
        }
        private TileView CoveredEmpty(MinigameSessionAdapter adapter)
        {
            return adapter.TileViews.Values.First(view =>
            {
                var tile = adapter.Session.Grid.GetTile(view.Coordinate.x, view.Coordinate.y);
                return !tile.IsRevealed && !tile.IsReinforced && tile.Content == TileContent.Empty;
            });
        }
        private void CheckRender(TabHost host)
        {
            var previous = RenderTexture.active;
            var target = host.ActiveRoot.sceneCamera.targetTexture;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            var colors = pixels.GetPixels32();
            int dark = colors.Count(c => c.r < 210 && c.g < 210 && c.b < 210);
            int magenta = colors.Count(c => c.r > 220 && c.g < 40 && c.b > 220);
            Destroy(pixels);
            RenderTexture.active = previous;
            Check(dark > colors.Length / 100 && magenta < colors.Length / 100, "tab renders content without error-shader pixels: " + host.ActiveIndex);
        }
        private void Capture(string filename)
        {
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            if (!screenshot) throw new Exception("Screenshot capture failed: " + filename);
            File.WriteAllBytes(Path.Combine(_output, filename), screenshot.EncodeToPNG());
            Destroy(screenshot);
        }
        private IEnumerator Run()
        {
            // Establish the test viewport after a hidden process launch as well.
            Screen.SetResolution(540, 960, FullScreenMode.Windowed);
            yield return new WaitForSeconds(.5f);
            var host = FindFirstObjectByType<TabHost>();
            float deadline = Time.realtimeSinceStartup + 45;
            while (!host.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Check(host.IsReady && SceneManager.sceneCount == 5, "shell and four content scenes load");
            Check(FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "one shared EventSystem");
            Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.enabled) == 1, "one enabled AudioListener");
            Check(host.ActiveIndex == 0 && host.profileBar.activeSelf, "home profile visible");
            var profile = FindFirstObjectByType<ProfileDataSource>();
            profile.SetProfile(new ProfileData { nickname = "통합 테스트", consecutiveDays = 24 });
            Check(host.profileBar.GetComponent<ProfileBar>().nickname.text == "통합 테스트", "profile updates from data source");
            profile.SetProfile(new ProfileData());
            yield return new WaitForSeconds(.4f);
            var homeCounter = host.GetRoot(0).GetComponentInChildren<DemoCounter>();
            Click(host, homeCounter.button.transform.position);
            Check(homeCounter.count == 1, "home mirrored button click");
            var drag = host.GetRoot(0).GetComponentInChildren<DemoDraggable>();
            var before = drag.transform.position;
            var pointer = new PointerEventData(EventSystem.current)
            { pointerId = -1, position = ScreenPoint(host, before), button = PointerEventData.InputButton.Left };
            host.input.OnPointerDown(pointer);
            pointer.position += new Vector2(50, 30);
            host.input.OnDrag(pointer); host.input.OnPointerUp(pointer);
            Check(Vector3.Distance(before, drag.transform.position) > .1f, "home mirrored collider drag");
            for (int i = 0; i < 4; i++)
            {
                var button = host.toolbar.GetChild(i).GetComponent<Button>();
                ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                yield return new WaitForSeconds(.3f);
                Check(host.ActiveIndex == i, "bottom tab button selects " + i);
                Check(host.GetRoot(i).sceneCamera.cullingMask == 1 << (8 + i), "isolated camera layer " + i);
                Check(host.profileBar.activeSelf == (i == 0), "profile visibility " + i);
                yield return new WaitForEndOfFrame();
                CheckRender(host);
                Capture($"0{i+1}_{host.catalog.tabs[i].id}.png");
                yield return null;
            }
            host.SelectTab(1); yield return new WaitForSeconds(.3f);
            var adapter = host.GetRoot(1).GetComponentInChildren<MinigameSessionAdapter>();
            var presenter = host.GetRoot(1).GetComponent<WorkshopPresenter>();
            Check(adapter.Session != null && adapter.TileViews.Count == 56, "existing 7x8 excavation grid instantiated");
            Check(adapter.TileViews.Values.All(t => t.gameObject.layer == 9 && t.gameObject.scene == adapter.gameObject.scene && t.UsesPointerEvents), "spawned tiles belong to workshop scene and render layer");
            var tileView = CoveredEmpty(adapter);
            int uses = adapter.Session.RemainingDestructiveToolUses;
            Click(host, tileView.transform.position);
            Check(adapter.Session.RemainingDestructiveToolUses == uses - 1, "mirrored tile click consumes exactly one tool use");
            Check(adapter.Session.Grid.GetTile(tileView.Coordinate.x, tileView.Coordinate.y).IsRevealed, "tile click updates existing core model");
            Check(tileView.GetComponent<SpriteRenderer>().color == Color.white, "tile model event updates sprite");
            Click(host, presenter.toolButtons[2].transform.position);
            Check(adapter.SelectedTool == ToolMode.Scout, "touch tool selection chooses scout");
            int scouts = adapter.Session.RemainingScoutToolUses;
            Click(host, CoveredEmpty(adapter).transform.position);
            Check(adapter.Session.RemainingScoutToolUses == scouts - 1 && presenter.feedback.text.StartsWith("정찰:"), "scout updates count and UI feedback");
            Time.timeScale = 0;
            adapter.SelectTool(ToolMode.AttackDestroy);
            Check(adapter.SelectedTool == ToolMode.Scout, "global pause blocks tool selection");
            int pausedUses = adapter.Session.RemainingScoutToolUses;
            Click(host, CoveredEmpty(adapter).transform.position);
            Check(adapter.Session.RemainingScoutToolUses == pausedUses, "global pause blocks tile input");
            Time.timeScale = 1;
            float remaining = adapter.Session.RemainingSeconds;
            host.SelectTab(0); yield return new WaitForSeconds(.35f);
            Check(host.catalog.tabs[1].backgroundPolicy == BackgroundPolicy.PauseWhileHidden &&
                Mathf.Abs(adapter.Session.RemainingSeconds - remaining) < .001f,
                "default hidden workshop freezes timer and retains session");
            adapter.SelectTool(ToolMode.AttackDestroy);
            Check(adapter.SelectedTool == ToolMode.Scout && !adapter.CanAcceptInput, "hidden workshop rejects input");
            Check(homeCounter.count == 1, "minigame input does not change home");
            host.SelectTab(1); yield return null;
            host.catalog.tabs[1].backgroundPolicy = BackgroundPolicy.PauseWhileHidden;
            host.SelectTab(0); remaining = adapter.Session.RemainingSeconds;
            yield return new WaitForSeconds(.35f);
            Check(Mathf.Abs(adapter.Session.RemainingSeconds - remaining) < .001f, "hidden pause freezes workshop timer");
            host.SelectTab(1); yield return new WaitForSeconds(.25f);
            Check(adapter.Session.RemainingSeconds < remaining && adapter.Session.RemainingDestructiveToolUses == uses - 1, "resume retains state and resumes timer");
            Click(host, presenter.toolButtons[0].transform.position);
            tileView = CoveredEmpty(adapter); Click(host, tileView.transform.position);
            Check(tileView.GetComponent<SpriteRenderer>().color == Color.white, "tile event subscription restored after pause");
            var previousSession = adapter.Session;
            host.catalog.tabs[1].backgroundPolicy = BackgroundPolicy.RestartOnReturn;
            host.SelectTab(0); host.SelectTab(1);
            deadline = Time.realtimeSinceStartup + 20;
            while (host.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSeconds(.3f);
            adapter = host.GetRoot(1).GetComponentInChildren<MinigameSessionAdapter>();
            presenter = host.GetRoot(1).GetComponent<WorkshopPresenter>();
            Check(!host.IsBusy && adapter.Session != previousSession && adapter.Session.RemainingDestructiveToolUses == 30, "return restart creates fresh minigame session");
            host.catalog.tabs[1].backgroundPolicy = BackgroundPolicy.PauseWhileHidden;
            Click(host, CoveredEmpty(adapter).transform.position);
            Click(host, presenter.restartButton.transform.position);
            yield return null;
            Check(adapter.Session.RemainingDestructiveToolUses == 30 && adapter.TileViews.Count == 56, "new excavation resets session without duplicate tiles");
            foreach (var size in new[] { new Vector2Int(480, 1040), new Vector2Int(768, 1024) })
            {
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                yield return new WaitForSeconds(.6f);
                var texture = host.ActiveRoot.sceneCamera.targetTexture;
                Check(Mathf.Abs((float)texture.width / texture.height - host.viewport.rect.width / host.viewport.rect.height) < .01f, "render texture matches viewport " + size);
                var corners = new[] { new Vector2Int(0, 0), new Vector2Int(6, 7) };
                Check(corners.All(c => { var uv = host.ActiveRoot.sceneCamera.WorldToViewportPoint(adapter.TileViews[c].transform.position); return uv.x > .03f && uv.x < .97f && uv.y > .20f && uv.y < .80f; }), "grid stays between controls " + size);
                uses = adapter.Session.RemainingDestructiveToolUses;
                Click(host, CoveredEmpty(adapter).transform.position);
                Check(adapter.Session.RemainingDestructiveToolUses == uses - 1, "tile mapping after resize " + size);
                yield return new WaitForEndOfFrame(); CheckRender(host);
                Capture($"Workshop_{size.x}x{size.y}.png");
                yield return null;
            }
            Check(_errors.Count == 0, "no runtime errors during integration checks");
            yield return new WaitForSeconds(.3f);
        }
    }
}
