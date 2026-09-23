using System;
using MobilePrototype.Exhibition;
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
        /// <summary>
        /// 개발 플레이어에 검증 출력 경로가 명시된 경우에만 검사를 실행하고, 오류를 수집해 결과 파일과 종료 코드로 전달합니다.
        /// </summary>
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
        /// <summary>
        /// Unity 오류·예외·단언 실패 로그를 스택과 함께 수집해 실행 검증 실패로 반영합니다.
        /// </summary>
        private void RecordError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _errors.Add(message + "\n" + stack);
        }
        /// <summary>
        /// 로그 구독과 시간 배율을 정리하고 검증 결과를 저장한 뒤 성공은 0, 실패는 1로 플레이어를 종료합니다.
        /// </summary>
        private void Finish()
        {
            Application.logMessageReceived -= RecordError;
            Time.timeScale = 1;
            File.WriteAllLines(Path.Combine(_output, "verification.txt"),
                new[] { _errors.Count == 0 ? "ALL CHECKS PASSED" : "CHECKS FAILED" }.Concat(_results).Concat(_errors));
            Application.Quit(_errors.Count == 0 ? 0 : 1);
        }
        /// <summary>
        /// 조건이 참이면 통과 목록에 추가하고, 거짓이면 검사 이름을 담은 예외로 검증을 중단합니다.
        /// </summary>
        private void Check(bool condition, string label)
        {
            if (!condition) throw new Exception(label);
            _results.Add("PASS " + label);
        }
        /// <summary>
        /// 활성 탭의 월드 위치를 카메라와 공통 뷰포트를 거쳐 실제 화면 좌표로 변환합니다.
        /// </summary>
        private Vector2 ScreenPoint(TabHost host, Vector3 world)
        {
            var uv = host.ActiveRoot.sceneCamera.WorldToViewportPoint(world);
            var rect = host.viewport.rect;
            return RectTransformUtility.WorldToScreenPoint(null, host.viewport.TransformPoint(new Vector3(
                Mathf.Lerp(rect.xMin, rect.xMax, uv.x), Mathf.Lerp(rect.yMin, rect.yMax, uv.y))));
        }
        /// <summary>
        /// 월드 위치에 해당하는 화면 지점에서 입력 브리지의 누름·놓기를 호출해 실제 클릭 전달 경로를 검증합니다.
        /// </summary>
        private void Click(TabHost host, Vector3 world)
        {
            var pointer = new PointerEventData(EventSystem.current)
            { pointerId = -1, position = ScreenPoint(host, world), button = PointerEventData.InputButton.Left };
            host.input.OnPointerDown(pointer);
            host.input.OnPointerUp(pointer);
        }
        /// <summary>
        /// 발굴 검사에 사용할 미공개·비강화 빈 타일을 찾습니다. 조건을 만족하는 타일이 없으면 실패합니다.
        /// </summary>
        private TileView CoveredEmpty(MinigameSessionAdapter adapter)
        {
            return adapter.TileViews.Values.First(view =>
            {
                var tile = adapter.Session.Grid.GetTile(view.Coordinate.x, view.Coordinate.y);
                return !tile.IsRevealed && !tile.IsReinforced && tile.Content == TileContent.Empty;
            });
        }
        /// <summary>
        /// 활성 탭의 렌더 텍스처에서 콘텐츠 픽셀과 오류 셰이더 색상을 검사하고 읽기 전 렌더 타깃을 복원합니다.
        /// </summary>
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
        /// <summary>
        /// 현재 화면에 충분한 가시 픽셀이 있는지 확인한 뒤 출력 폴더에 PNG로 저장합니다. 렌더 완료 후 호출해야 합니다.
        /// </summary>
        private void Capture(string filename)
        {
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            if (!screenshot) throw new Exception("Screenshot capture failed: " + filename);
            Check(screenshot.GetPixels32().Count(c => c.r > 20 || c.g > 20 || c.b > 20) > screenshot.width * screenshot.height / 4,
                "screenshot contains visible rendered frame: " + filename);
            File.WriteAllBytes(Path.Combine(_output, filename), screenshot.EncodeToPNG());
            Destroy(screenshot);
        }
        /// <summary>
        /// 뷰포트 내 정규화 좌표를 화면 좌표로 바꿔 검증용 단일 왼쪽 포인터 이벤트를 생성합니다.
        /// </summary>
        private PointerEventData PointerAt(TabHost host, Vector2 uv)
        {
            var rect = host.viewport.rect;
            var screen = RectTransformUtility.WorldToScreenPoint(null, host.viewport.TransformPoint(new Vector3(
                Mathf.Lerp(rect.xMin, rect.xMax, uv.x), Mathf.Lerp(rect.yMin, rect.yMax, uv.y))));
            return new PointerEventData(EventSystem.current)
            { pointerId = -1, position = screen, button = PointerEventData.InputButton.Left };
        }
        /// <summary>
        /// 화면 폭의 40%만큼 지정 방향으로 포인터를 이동·해제하고 탭 전환 정착을 기다립니다. 음수 방향은 왼쪽입니다.
        /// </summary>
        private IEnumerator Swipe(TabHost host, float direction)
        {
            var pointer = PointerAt(host, new Vector2(.5f, .8f));
            host.input.OnPointerDown(pointer);
            pointer.position += Vector2.right * Screen.width * .4f * direction;
            host.input.OnDrag(pointer);
            host.input.OnPointerUp(pointer);
            yield return new WaitForSeconds(.35f);
        }
        /// <summary>
        /// 실제 씬과 입력 브리지로 초기 로딩 정책·전시관·탭 전환·발굴·일시정지·해상도 변경을 검증하고 화면을 저장합니다.
        /// </summary>
        private IEnumerator Run()
        {
            var host = FindFirstObjectByType<TabHost>();
            float deadline = Time.realtimeSinceStartup + 45;
            int observedHiddenRoots = 0;
            while (!host.IsReady && Time.realtimeSinceStartup < deadline)
            {
                for (int i = 0; i < host.catalog.tabs.Count; i++)
                {
                    var root = host.GetRoot(i);
                    if (!root || i == host.ActiveIndex) continue;
                    bool shouldPause = host.catalog.tabs[i].backgroundPolicy != BackgroundPolicy.ContinueRunning;
                    if (root.IsPaused != shouldPause)
                        throw new Exception("loaded hidden root violates background policy: " + i);
                    observedHiddenRoots++;
                }
                yield return null;
            }
            Check(host.IsReady && observedHiddenRoots > 0, "background policies hold while initial tabs load");
            // Establish the test viewport after a hidden process launch as well.
            Screen.SetResolution(541, 961, FullScreenMode.Windowed);
            yield return new WaitForSeconds(.2f);
            Screen.SetResolution(540, 960, FullScreenMode.Windowed);
            yield return new WaitForSeconds(.5f);
            Check(host.IsReady && SceneManager.sceneCount == 5, "shell and four content scenes load");
            Check(FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "one shared EventSystem");
            Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.enabled) == 1, "one enabled AudioListener");
            Check(host.ActiveIndex == 0 && host.profileBar.activeSelf, "home profile visible");
            var profile = FindFirstObjectByType<ProfileDataSource>();
            profile.SetProfile(new ProfileData { nickname = "통합 테스트", consecutiveDays = 24 });
            Check(host.profileBar.GetComponent<ProfileBar>().nickname.text == "통합 테스트", "profile updates from data source");
            profile.SetProfile(new ProfileData());
            yield return new WaitForSeconds(.4f);
            var exhibition = host.GetRoot(0).GetComponent<ExhibitionView>();
            Check(exhibition && exhibition.GetComponentsInChildren<ExhibitionLayer>().Length == 10, "home has ten independent artwork layers");
            Check(!host.GetRoot(0).GetComponentInChildren<DemoCounter>(), "home demo replaced by exhibition");
            yield return new WaitForSeconds(1.5f);
            Check(exhibition.Intro.IsComplete, "session introduction completes");
            var surface = exhibition.GetComponentInChildren<ExhibitionSurface>();
            Check(surface && surface.Grid && surface.Grid.Size == new Vector2Int(7, 7), "housing uses invisible 7x7 logical grid");
            var expectedCell = new Vector2Int(3, 4);
            foreach (float zoom in new[] { 1f, 1.4f, .8f })
            {
                exhibition.SetView(new Vector2(.1f, -.1f), zoom);
                yield return null;
                yield return null;
                var mapped = host.input.MapPosition(ScreenPoint(host, surface.CellWorldPosition(expectedCell)), null);
                Check(surface.TryGetCell(host.ActiveRoot.sceneCamera, mapped, out var cell) && cell == expectedCell,
                    "logical cell stable through screen mapping and zoom " + zoom);
            }
            exhibition.SetView(Vector2.zero, 1);
            yield return null;
            var dragPointer = PointerAt(host, new Vector2(.5f, .8f));
            host.input.OnPointerDown(dragPointer);
            dragPointer.position -= Vector2.right * Screen.width * .12f;
            host.input.OnDrag(dragPointer);
            yield return null;
            Check(host.Transition.IsActive && exhibition.TransitionOffset < 0 && exhibition.Intro.IsComplete,
                "swipe parallax operates independently after introduction");
            yield return new WaitForEndOfFrame();
            Capture("Home_Swipe_Preview.png");
            yield return new WaitForSeconds(.15f);
            host.input.OnPointerUp(dragPointer);
            yield return new WaitForSeconds(.35f);
            Check(host.ActiveIndex == 0 && !host.IsBusy && exhibition.TransitionOffset == 0,
                "short held swipe cancels and resets parallax");
            for (int i = 1; i < 4; i++)
            {
                yield return Swipe(host, -1);
                Check(host.ActiveIndex == i, "swipe left selects tab " + i);
            }
            yield return Swipe(host, -1);
            Check(host.ActiveIndex == 3, "last tab does not wrap");
            for (int i = 2; i >= 0; i--)
            {
                yield return Swipe(host, 1);
                Check(host.ActiveIndex == i, "swipe right selects tab " + i);
            }
            yield return Swipe(host, 1);
            Check(host.ActiveIndex == 0 && exhibition.Intro.IsComplete, "first tab does not wrap or replay intro");
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
            host.SelectTab(2); yield return new WaitForSeconds(.35f);
            var contentDrag = host.GetRoot(2).GetComponentInChildren<DemoDraggable>();
            var dragBefore = contentDrag.transform.position;
            var contentPointer = new PointerEventData(EventSystem.current)
            { pointerId = -1, position = ScreenPoint(host, dragBefore), button = PointerEventData.InputButton.Left };
            host.input.OnPointerDown(contentPointer);
            contentPointer.position += new Vector2(50, 30);
            host.input.OnDrag(contentPointer); host.input.OnPointerUp(contentPointer);
            Check(host.ActiveIndex == 2 && !host.IsBusy && Vector3.Distance(dragBefore, contentDrag.transform.position) > .1f,
                "dedicated object drag retains gesture instead of switching tab");
            host.SelectTab(1); yield return new WaitForSeconds(.35f);
            var adapter = host.GetRoot(1).GetComponentInChildren<MinigameSessionAdapter>();
            var presenter = host.GetRoot(1).GetComponent<WorkshopPresenter>();
            Check(adapter.Session != null && adapter.TileViews.Count == 56, "existing 7x8 excavation grid instantiated");
            Check(adapter.TileViews.Values.All(t => t.gameObject.layer == 9 && t.gameObject.scene == adapter.gameObject.scene && t.UsesPointerEvents), "spawned tiles belong to workshop scene and render layer");
            int beforeSwipeUses = adapter.Session.RemainingDestructiveToolUses;
            var tilePointer = new PointerEventData(EventSystem.current)
            { pointerId = -1, position = ScreenPoint(host, CoveredEmpty(adapter).transform.position), button = PointerEventData.InputButton.Left };
            host.input.OnPointerDown(tilePointer);
            tilePointer.position += Vector2.right * Screen.width * .4f;
            host.input.OnDrag(tilePointer); host.input.OnPointerUp(tilePointer);
            yield return new WaitForSeconds(.35f);
            Check(host.ActiveIndex == 0 && adapter.Session.RemainingDestructiveToolUses == beforeSwipeUses,
                "swiping from excavation tile does not click it");
            float hiddenSeconds = adapter.Session.RemainingSeconds;
            dragPointer = PointerAt(host, new Vector2(.5f, .8f));
            host.input.OnPointerDown(dragPointer);
            dragPointer.position -= Vector2.right * Screen.width * .1f;
            host.input.OnDrag(dragPointer);
            yield return new WaitForSeconds(.2f);
            Check(Mathf.Abs(adapter.Session.RemainingSeconds - hiddenSeconds) < .001f, "preview does not resume hidden workshop");
            host.input.Cancel();
            yield return null;
            Check(host.ActiveIndex == 0 && !host.IsBusy, "input cancellation restores tab");
            host.SelectTab(1); yield return new WaitForSeconds(.35f);
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
            host.SelectTab(0); yield return new WaitForSeconds(.35f);
            float remaining = adapter.Session.RemainingSeconds;
            yield return new WaitForSeconds(.15f);
            Check(host.catalog.tabs[1].backgroundPolicy == BackgroundPolicy.PauseWhileHidden &&
                Mathf.Abs(adapter.Session.RemainingSeconds - remaining) < .001f,
                "default hidden workshop freezes timer and retains session");
            adapter.SelectTool(ToolMode.AttackDestroy);
            Check(adapter.SelectedTool == ToolMode.Scout && !adapter.CanAcceptInput, "hidden workshop rejects input");
            Check(exhibition.Intro.IsComplete, "minigame input does not replay home intro");
            host.SelectTab(1); yield return new WaitForSeconds(.35f);
            host.catalog.tabs[1].backgroundPolicy = BackgroundPolicy.PauseWhileHidden;
            host.SelectTab(0); yield return new WaitForSeconds(.35f); remaining = adapter.Session.RemainingSeconds;
            yield return new WaitForSeconds(.35f);
            Check(Mathf.Abs(adapter.Session.RemainingSeconds - remaining) < .001f, "hidden pause freezes workshop timer");
            host.SelectTab(1); yield return new WaitForSeconds(.45f);
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
                host.SelectTab(0); yield return new WaitForSeconds(.4f);
                yield return new WaitForEndOfFrame();
                Capture($"Home_{size.x}x{size.y}.png");
                var mappedCell = host.input.MapPosition(ScreenPoint(host, surface.CellWorldPosition(expectedCell)), null);
                Check(surface.TryGetCell(host.ActiveRoot.sceneCamera, mappedCell, out var resizedCell) && resizedCell == expectedCell,
                    "grid mapping after viewport resize " + size);
                host.SelectTab(1); yield return new WaitForSeconds(.4f);
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
