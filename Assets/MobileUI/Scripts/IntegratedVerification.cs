using System;
using MobilePrototype.Collection;
using MobilePrototype.Exhibition;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.Gameplay.Minigame.Core;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MobilePrototype
{
    /// <summary>
    /// 명시적 검증 옵션을 받은 개발 플레이어에서 실제 씬·입력·렌더링을 검사하고 결과를 저장합니다.
    /// 일반 플레이에서는 검증 흐름을 시작하지 않습니다.
    /// </summary>
    public class IntegratedVerification : MonoBehaviour
    {
        private readonly List<string> _results = new List<string>();
        private readonly List<string> _errors = new List<string>();
        private string _output;
        /// <summary>
        /// 개발 플레이어에 검증 출력 경로가 명시된 경우에만 검사를 실행하고, 오류를 수집해 결과 파일과 종료 코드로 전달합니다.
        /// </summary>
        /// <returns>중첩 검증 코루틴의 프레임 대기를 순서대로 실행하는 코루틴입니다.</returns>
        private IEnumerator Start()
        {
            string option = Environment.GetCommandLineArgs().FirstOrDefault(x => x.StartsWith("--verify-output="));
            if (option == null || !Debug.isDebugBuild) yield break;
            _output = option.Substring("--verify-output=".Length);
            Directory.CreateDirectory(_output);
            Application.logMessageReceived += RecordError;
            var routines = new Stack<IEnumerator>();
            routines.Push(Run());
            while (routines.Count > 0)
            {
                object next = null;
                bool more;
                var routine = routines.Peek();
                try { more = routine.MoveNext(); if (more) next = routine.Current; }
                catch (Exception ex) { _errors.Add(ex.ToString()); Finish(); yield break; }
                if (!more) { routines.Pop(); continue; }
                // Catch nested verification failures too, so the player always writes a report and exits.
                if (next is IEnumerator nested) { routines.Push(nested); continue; }
                yield return next;
            }
            Finish();
        }
        /// <summary>
        /// Unity 오류·예외·단언 실패 로그를 스택과 함께 수집해 실행 검증 실패로 반영합니다.
        /// </summary>
        /// <param name="message">Unity가 전달한 로그 메시지입니다.</param>
        /// <param name="stack">로그 발생 위치의 스택 정보입니다.</param>
        /// <param name="type">검증 실패로 포함할 오류 수준을 판단하는 로그 종류입니다.</param>
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
        /// <param name="condition">통과 여부를 나타내는 검증 조건입니다.</param>
        /// <param name="label">결과 파일과 실패 예외에 기록할 검사 이름입니다.</param>
        /// <exception cref="Exception">검증 조건이 거짓이면 발생합니다.</exception>
        private void Check(bool condition, string label)
        {
            if (!condition) throw new Exception(label);
            _results.Add("PASS " + label);
        }
        /// <summary>
        /// 활성 탭의 월드 위치를 카메라와 공통 뷰포트를 거쳐 실제 화면 좌표로 변환합니다.
        /// </summary>
        /// <param name="host">현재 탭 카메라와 공통 표시 영역을 제공하는 호스트입니다.</param>
        /// <param name="world">활성 콘텐츠 씬 안에서 입력할 월드 위치입니다.</param>
        /// <returns>공통 미러 입력에 전달할 화면 픽셀 좌표입니다.</returns>
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
        /// <param name="host">입력을 전달할 활성 탭의 호스트입니다.</param>
        /// <param name="world">렌더와 레이캐스트 준비가 끝난 클릭 대상의 월드 위치입니다.</param>
        private void Click(TabHost host, Vector3 world)
        {
            var pointer = new PointerEventData(EventSystem.current)
            { pointerId = -1, position = ScreenPoint(host, world), button = PointerEventData.InputButton.Left };
            host.input.OnPointerDown(pointer);
            host.input.OnPointerUp(pointer);
        }

        /// <summary>
        /// 배치물을 길게 눌러 로컬 메뉴를 열고, 지정 동작으로 드래그해 탭 전환 없이 한 번 실행되는지 검사합니다.
        /// </summary>
        /// <param name="host">전시관 입력을 전달할 호스트입니다.</param>
        /// <param name="housing">배치물 메뉴와 배치 상태를 제공하는 하우징입니다.</param>
        /// <param name="cell">길게 누를 배치물이 차지한 논리 격자 좌표입니다.</param>
        /// <param name="action">메뉴에서 선택할 이동·회전·회수 동작입니다.</param>
        /// <returns>길게 누르기와 화면 캡처 대기를 포함한 입력 검증 코루틴입니다.</returns>
        private IEnumerator HousingGesture(TabHost host, ExhibitionHousing housing, Vector2Int cell, HousingAction action)
        {
            var pointer = new PointerEventData(EventSystem.current)
            { pointerId = -1, position = ScreenPoint(host, housing.Surface.CellWorldPosition(cell) +
                housing.Surface.transform.TransformVector(Vector3.up * .16f)), button = PointerEventData.InputButton.Left };
            host.input.OnPointerDown(pointer);
            Check(!housing.Menu.IsVisible, "housing UI waits for long press " + action);
            yield return new WaitForSeconds(.55f);
            Check(housing.Menu.IsVisible && !housing.Menu.IsFloorMenu && housing.Menu.VisibleActionCount == 3,
                "object hold reveals exactly three local actions " + action);
            if (action == HousingAction.Move)
            {
                yield return new WaitForEndOfFrame();
                Capture("Housing_Hold_Menu.png");
            }
            pointer.position = ScreenPoint(host, housing.Menu.ActionWorldPosition(action));
            host.input.OnDrag(pointer);
            host.input.OnDrag(pointer);
            Check(host.ActiveIndex == 0 && !host.Transition.IsActive && housing.Menu.IsVisible,
                "radial action owns swipe until release " + action);
            host.input.OnPointerUp(pointer);
            Check(!housing.Menu.IsVisible, "all housing UI closes on release " + action);
        }

        /// <summary>
        /// 빈 바닥을 길게 눌러 배치 또는 취소를 선택하고, 메뉴 종료 후에도 선택한 배치 상태가 유지되는지 검사합니다.
        /// </summary>
        /// <param name="host">전시관 입력을 전달할 호스트입니다.</param>
        /// <param name="housing">빈 바닥 메뉴와 배치 대기 상태를 제공하는 하우징입니다.</param>
        /// <param name="cell">길게 누를 비어 있는 논리 격자 좌표입니다.</param>
        /// <param name="action">빈 바닥 메뉴에서 선택할 배치 또는 취소 동작입니다.</param>
        /// <returns>길게 누르기와 메뉴 표시 대기를 포함한 입력 검증 코루틴입니다.</returns>
        private IEnumerator BeginHousingPlacement(TabHost host, ExhibitionHousing housing, Vector2Int cell,
            HousingAction action = HousingAction.Place)
        {
            var pointer = new PointerEventData(EventSystem.current)
            { pointerId = -1, position = ScreenPoint(host, housing.Surface.CellWorldPosition(cell)),
                button = PointerEventData.InputButton.Left };
            host.input.OnPointerDown(pointer);
            yield return new WaitForSeconds(.55f);
            Check(housing.Menu.IsVisible && housing.Menu.IsFloorMenu && housing.Menu.VisibleActionCount == 2,
                "empty floor hold reveals exactly two local actions " + action);
            if (action == HousingAction.Place)
            {
                yield return new WaitForEndOfFrame();
                Capture("Housing_Empty_Menu.png");
            }
            pointer.position = ScreenPoint(host, housing.Menu.ActionWorldPosition(action));
            host.input.OnDrag(pointer);
            host.input.OnDrag(pointer);
            Check(housing.IsPlacing == (action == HousingAction.Place) && !host.Transition.IsActive,
                "empty floor swipe executes local action once " + action);
            host.input.OnPointerUp(pointer);
            Check(!housing.Menu.IsVisible && housing.IsPlacing == (action == HousingAction.Place),
                "empty floor menu hides while action state persists " + action);
        }
        /// <summary>
        /// 발굴 검사에 사용할 미공개·비강화 빈 타일을 찾습니다. 조건을 만족하는 타일이 없으면 실패합니다.
        /// </summary>
        /// <param name="adapter">활성 세션의 타일 모델과 표현을 제공하는 미니게임 어댑터입니다.</param>
        /// <returns>안전한 빈 타일 클릭 검사에 사용할 타일 표현입니다.</returns>
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
        /// <param name="host">렌더가 완료된 활성 탭의 카메라와 렌더 텍스처를 제공하는 호스트입니다.</param>
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
        /// <param name="filename">검증 출력 폴더 안에 저장할 PNG 파일 이름입니다.</param>
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
        /// <param name="host">공통 표시 영역을 제공하는 탭 호스트입니다.</param>
        /// <param name="uv">공통 표시 영역 안의 0~1 정규화 좌표입니다.</param>
        /// <returns>해당 화면 좌표에서 시작할 단일 왼쪽 포인터 이벤트입니다.</returns>
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
        /// <param name="host">탭 스와이프 입력을 전달할 호스트입니다.</param>
        /// <param name="direction">왼쪽은 음수, 오른쪽은 양수로 지정하는 이동 방향입니다.</param>
        /// <returns>스와이프 전달과 전환 정착 대기를 포함한 코루틴입니다.</returns>
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
        /// 도감의 실제 버튼·미러 입력으로 복원 기준 잠금, 상세 열람, 제스처 소유권과 탭 재방문 상태를 검증합니다.
        /// </summary>
        /// <param name="host">도감과 다른 탭의 전환·입력을 제공하는 통합 호스트입니다.</param>
        /// <returns>도감 화면 캡처, 기록 교체와 입력 처리를 순서대로 검증하는 코루틴입니다.</returns>
        private IEnumerator VerifyCollection(TabHost host)
        {
            host.SelectTab(2);
            yield return new WaitForSeconds(.35f);
            var root = host.GetRoot(2);
            var collection = root.GetComponent<CollectionPresenter>();
            Check(collection && collection.Catalog && collection.Progress != null,
                "collection catalog and dummy restoration progress initialized");
            Check(collection.Appearance, "collection scene references shared appearance asset");
            var expectedEraIds = new[] { "samguk", "goryeo", "joseon" };
            var expectedEraNames = new[] { "삼국", "고려", "조선" };
            var expectedRelicIds = new[]
            {
                "samguk-standing-buddha", "samguk-incense-burner", "samguk-crown",
                "goryeo-stone-buddha", "goryeo-celadon", "goryeo-lacquerware",
                "joseon-royal-seal", "joseon-horse-medallion", "joseon-cat-sparrow-painting"
            };
            var expectedRelicNames = new[]
            {
                "금동여래입상", "금동대향로", "금관", "석불", "고려청자", "나전칠기", "옥새", "마패", "묘작도"
            };
            Check(collection.Catalog.Eras.Select(era => era.Id).SequenceEqual(expectedEraIds) &&
                collection.Catalog.Eras.Select(era => era.Name).SequenceEqual(expectedEraNames),
                "collection era order and names match confirmed source sheet");
            Check(collection.Catalog.Relics.Select(relic => relic.Id).SequenceEqual(expectedRelicIds) &&
                collection.Catalog.Relics.Select(relic => relic.Name).SequenceEqual(expectedRelicNames) &&
                collection.Catalog.Relics.Select(relic => relic.EraId).SequenceEqual(
                    expectedEraIds.SelectMany(eraId => Enumerable.Repeat(eraId, 3))),
                "collection contains exactly nine confirmed relics in source order and eras");
            var removedRelicIds = new[]
            {
                "samguk-pottery", "goryeo-bronze-mirror", "goryeo-bell",
                "joseon-white-porcelain", "joseon-brush", "joseon-sundial"
            };
            Check(removedRelicIds.All(relicId => collection.Catalog.FindRelic(relicId) == null &&
                !collection.Progress.RegisterRestorationCompleted(relicId)) && collection.Progress.RestoredCount == 5,
                "removed provisional relics cannot be found or registered as restored");
            Check(!root.GetComponentInChildren<DemoCounter>(true) && !root.GetComponentInChildren<DemoDraggable>(true),
                "collection demo components replaced by collection screens");
            Check(root.GetComponentsInChildren<Transform>(true).All(child =>
                child.gameObject.scene == root.gameObject.scene && child.gameObject.layer == root.RenderLayer),
                "runtime collection UI belongs to collection scene and render layer");
            collection.ShowOverview();
            yield return new WaitForEndOfFrame();
            Check(collection.Page == CollectionPage.Overview && collection.EraButtons.Count == 3 &&
                collection.StatsText.text.Contains("5 / 9") && collection.StatsText.text.Contains("1 / 3") &&
                collection.StatsText.text.Contains("55%"),
                "collection overview counts restored relic types and excludes buildings from rate");
            Capture("Collection_Overview.png");

            var firstEra = collection.Catalog.Eras.First(era =>
                collection.Progress.RestoredInEra(era.Id) == 1);
            var restoredRelic = collection.Catalog.Relics.First(relic =>
                relic.EraId == firstEra.Id && collection.Progress.IsRestored(relic.Id));
            var lockedRelic = collection.Catalog.Relics.First(relic =>
                relic.EraId == firstEra.Id && !collection.Progress.IsRestored(relic.Id));
            Click(host, collection.EraButtons[firstEra.Id].transform.position);
            yield return new WaitForEndOfFrame();
            Check(collection.Page == CollectionPage.Era && collection.SelectedEraId == firstEra.Id &&
                collection.RelicButtons.Count == 3 && !collection.RelicButtons[lockedRelic.Id].interactable,
                "era card opens three relics with unrestored types locked");
            Check(collection.GetComponentsInChildren<Text>().Any(text => text.text.Contains("1 / 3")) &&
                !collection.BuildingStatusText.text.Contains("해금 완료"),
                "era shows restoration count and locked building");
            Click(host, collection.RelicButtons[lockedRelic.Id].transform.position);
            collection.ShowRelic(lockedRelic.Id);
            Check(collection.Page == CollectionPage.Era && collection.SelectedEraId == firstEra.Id,
                "locked relic cannot open through pointer or direct navigation");
            Capture("Collection_Era.png");

            Check(collection.Progress.IsUnread(restoredRelic.Id), "new restored relic begins unread");
            Click(host, collection.RelicButtons[restoredRelic.Id].transform.position);
            yield return new WaitForEndOfFrame();
            Check(collection.Page == CollectionPage.Relic && collection.SelectedRelicId == restoredRelic.Id &&
                !collection.Progress.IsUnread(restoredRelic.Id),
                "restored relic click opens detail and marks new record viewed");
            Check(collection.GetComponentsInChildren<Text>().Any(text => text.text.Contains(restoredRelic.Name)) &&
                collection.GetComponentsInChildren<Text>().Any(text => text.text.Contains(restoredRelic.SpiritName)),
                "relic detail displays relic and spirit identity");
            Capture("Collection_Relic.png");

            collection.Scroll.StopMovement();
            collection.Scroll.verticalNormalizedPosition = 1;
            Canvas.ForceUpdateCanvases();
            var scrollPointer = new PointerEventData(EventSystem.current)
            {
                pointerId = -1,
                position = ScreenPoint(host, collection.Scroll.viewport.TransformPoint(collection.Scroll.viewport.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            host.input.OnPointerDown(scrollPointer);
            scrollPointer.position += Vector2.up * 60;
            host.input.OnDrag(scrollPointer);
            scrollPointer.position += Vector2.up * 100;
            host.input.OnDrag(scrollPointer);
            host.input.OnPointerUp(scrollPointer);
            yield return null;
            collection.Scroll.StopMovement();
            Check(host.ActiveIndex == 2 && !host.IsBusy && collection.Page == CollectionPage.Relic &&
                collection.Scroll.verticalNormalizedPosition < .98f,
                "vertical collection drag scrolls content without changing tab or page");
            collection.Scroll.verticalNormalizedPosition = 0;
            yield return new WaitForEndOfFrame();
            Capture("Collection_Relic_Scrolled.png");
            float scrollPosition = collection.Scroll.verticalNormalizedPosition;
            host.SelectTab(3);
            yield return new WaitForSeconds(.35f);
            host.SelectTab(2);
            yield return new WaitForSeconds(.35f);
            Check(collection.Page == CollectionPage.Relic && collection.SelectedRelicId == restoredRelic.Id &&
                Mathf.Abs(collection.Scroll.verticalNormalizedPosition - scrollPosition) < .02f,
                "collection tab revisit preserves detail and scroll position");

            Click(host, collection.BackButton.transform.position);
            yield return new WaitForEndOfFrame();
            Check(collection.Page == CollectionPage.Era && collection.SelectedEraId == firstEra.Id &&
                !collection.RelicButtons[restoredRelic.Id].GetComponentsInChildren<Transform>(true).Any(child =>
                    child.name == "New" && child.gameObject.activeInHierarchy),
                "detail back restores era and removes viewed relic new badge");
            var cardSwipe = new PointerEventData(EventSystem.current)
            {
                pointerId = -1,
                position = ScreenPoint(host, collection.RelicButtons[restoredRelic.Id].transform.position),
                button = PointerEventData.InputButton.Left
            };
            host.input.OnPointerDown(cardSwipe);
            cardSwipe.position -= Vector2.right * Screen.width * .4f;
            host.input.OnDrag(cardSwipe);
            host.input.OnPointerUp(cardSwipe);
            yield return new WaitForSeconds(.35f);
            Check(host.ActiveIndex == 3 && collection.Page == CollectionPage.Era,
                "horizontal swipe on relic card changes tab and cancels card click");
            host.SelectTab(2);
            yield return new WaitForSeconds(.35f);
            Click(host, collection.BackButton.transform.position);
            yield return new WaitForEndOfFrame();
            Check(collection.Page == CollectionPage.Overview, "era back returns to collection overview");
            var completedEra = collection.Catalog.Eras.First(era => collection.Progress.IsBuildingUnlocked(era.Id));
            Click(host, collection.EraButtons[completedEra.Id].transform.position);
            yield return new WaitForEndOfFrame();
            Check(collection.Page == CollectionPage.Era && collection.BuildingStatusText.text.Contains("해금 완료"),
                "all restored relic types unlock era building");
            Capture("Collection_Era_Complete.png");

            collection.ShowEra(firstEra.Id);
            bool completionAccepted = collection.Progress.RegisterRestorationCompleted(lockedRelic.Id);
            yield return new WaitForEndOfFrame();
            Check(completionAccepted &&
                collection.RelicButtons[lockedRelic.Id].interactable,
                "restoration completion bridge refreshes visible locked relic");
            Capture("Collection_Era_Updated.png");
            collection.ShowRelic(lockedRelic.Id);
            yield return new WaitForEndOfFrame();
            Check(collection.GetComponentsInChildren<Text>().Any(text => text.text == lockedRelic.Name),
                "newly restored confirmed relic displays its exact source name in detail");
            Capture("Collection_Relic_Updated.png");
            collection.ShowOverview();
            Check(collection.StatsText.text.Contains("6 / 9") && collection.StatsText.text.Contains("66%") &&
                !collection.Progress.RegisterRestorationCompleted(lockedRelic.Id) && collection.Progress.RestoredCount == 6,
                "restoration completion updates collection count once per relic type");
            var originalProgress = collection.Progress;
            collection.ShowRelic(restoredRelic.Id);
            collection.BindProgress(new CollectionProgress(collection.Catalog));
            yield return new WaitForEndOfFrame();
            Check(collection.Page == CollectionPage.Era && collection.SelectedRelicId == null &&
                !collection.RelicButtons[restoredRelic.Id].interactable,
                "binding empty shared progress closes now-locked detail and locks its relic card");
            collection.BindProgress(originalProgress);
            yield return new WaitForEndOfFrame();
            collection.ShowOverview();
            Check(collection.StatsText.text.Contains("6 / 9") && collection.Progress == originalProgress,
                "rebinding original progress restores collection view without losing records");
        }

        /// <summary>
        /// 지정 계층에서 이름이 일치하는 활성 이미지를 찾고, 화면 재구성 뒤 제거 대기 중인 비활성 이미지는 제외합니다.
        /// </summary>
        /// <param name="scope">이미지를 검색할 부모 계층입니다.</param>
        /// <param name="name">검색할 이미지 오브젝트의 이름입니다.</param>
        /// <returns>일치하는 첫 활성 이미지이며, 없으면 null입니다.</returns>
        private Image ActiveImage(Transform scope, string name)
        {
            return scope.GetComponentsInChildren<Image>(true).FirstOrDefault(image =>
                image.name == name && image.gameObject.activeInHierarchy);
        }

        /// <summary>
        /// 활성 이미지의 스프라이트·원본 색·입력 설정을 확인하고, UI 바탕이면 9-slice와 늘림 설정도 검사합니다.
        /// </summary>
        /// <param name="scope">검사할 이미지가 속한 계층입니다.</param>
        /// <param name="name">검사할 이미지 오브젝트의 이름입니다.</param>
        /// <param name="sprite">이미지에 연결되어야 하는 스프라이트입니다.</param>
        /// <param name="surface">카드나 패널처럼 9-slice로 채워야 하는 UI 바탕인지 나타냅니다.</param>
        /// <param name="raycast">해당 이미지가 포인터 레이캐스트를 받아야 하는지 나타냅니다.</param>
        /// <returns>이미지가 존재하고 모든 표시·입력 조건이 일치하면 true입니다.</returns>
        private bool MatchesImage(Transform scope, string name, Sprite sprite, bool surface = false, bool raycast = false)
        {
            var image = ActiveImage(scope, name);
            return image && image.sprite == sprite && image.color == Color.white && image.raycastTarget == raycast &&
                (!surface || image.type == Image.Type.Sliced && !image.preserveAspect);
        }

        /// <summary>
        /// 이미지 슬롯이 비었을 때 지정 위치에 활성 벡터 대체 그림이 생성되었는지 확인합니다.
        /// </summary>
        /// <param name="scope">대체 그림을 검색할 부모 계층입니다.</param>
        /// <param name="name">대체 그림 오브젝트의 이름입니다.</param>
        /// <returns>이름이 일치하는 활성 벡터 그림이 있으면 true입니다.</returns>
        private bool HasVectorArtwork(Transform scope, string name)
        {
            return scope.GetComponentsInChildren<CollectionArtwork>(true).Any(artwork =>
                artwork.name == name && artwork.gameObject.activeInHierarchy);
        }

        /// <summary>
        /// Player 메모리의 직렬화 스프라이트 슬롯만 교체하고, finally에서 역순으로 실행할 원래 참조 복구 동작을 등록합니다.
        /// </summary>
        /// <param name="target">교체할 private 스프라이트 필드를 가진 정의 또는 외형 객체입니다.</param>
        /// <param name="fieldName">교체할 직렬화 필드의 정확한 이름입니다.</param>
        /// <param name="sprite">검사할 임시 스프라이트이며, null이면 비어 있는 슬롯을 검사합니다.</param>
        /// <param name="restore">교체 이전 참조를 복구할 동작을 누적하는 목록입니다.</param>
        /// <exception cref="Exception">지정 필드가 없거나 스프라이트 형식이 아니면 발생합니다.</exception>
        private void OverrideSprite(object target, string fieldName, Sprite sprite, List<Action> restore)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(Sprite))
                throw new Exception("Missing serialized sprite slot: " + target.GetType().Name + "." + fieldName);
            var previous = field.GetValue(target);
            restore.Add(() => field.SetValue(target, previous));
            field.SetValue(target, sprite);
        }

        /// <summary>
        /// 실제 렌더 색과 9-slice 검사를 위한 4×4 단색 스프라이트를 만들고 텍스처와 스프라이트를 정리 목록에 등록합니다.
        /// </summary>
        /// <param name="color">서로 다른 이미지 경로를 구분할 원본 픽셀 색입니다.</param>
        /// <param name="temporary">검사가 끝나면 제거할 Unity 객체 목록입니다.</param>
        /// <returns>네 방향에 1픽셀 테두리가 설정된 임시 스프라이트입니다.</returns>
        private Sprite VerificationSprite(Color color, List<UnityEngine.Object> temporary)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            temporary.Add(texture);
            texture.SetPixels(Enumerable.Repeat(color, 16).ToArray());
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f),
                100, 0, SpriteMeshType.FullRect, Vector4.one);
            temporary.Add(sprite);
            return sprite;
        }

        /// <summary>
        /// 실제 렌더 텍스처의 콘텐츠 바깥쪽 배경 픽셀을 읽어 선택된 페이지 배경의 주된 색 채널을 검사합니다.
        /// </summary>
        /// <param name="host">렌더가 완료된 활성 도감 탭을 제공하는 호스트입니다.</param>
        /// <param name="dominantChannel">우세해야 하는 색 채널 인덱스로, 빨강 0·초록 1·파랑 2입니다.</param>
        /// <param name="label">결과 파일에 기록할 배경 우선순위 검사 이름입니다.</param>
        private void CheckBackgroundRender(TabHost host, int dominantChannel, string label)
        {
            var target = host.ActiveRoot.sceneCamera.targetTexture;
            var previous = RenderTexture.active;
            var pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            Color rendered;
            try
            {
                RenderTexture.active = target;
                pixel.ReadPixels(new Rect(Mathf.FloorToInt(target.width * .02f), target.height / 2, 1, 1), 0, 0);
                pixel.Apply();
                rendered = pixel.GetPixel(0, 0);
            }
            finally
            {
                RenderTexture.active = previous;
                Destroy(pixel);
            }
            var channels = new[] { rendered.r, rendered.g, rendered.b };
            Check(channels[dominantChannel] > .1f && channels[dominantChannel] >
                channels.Where((value, index) => index != dominantChannel).Max() * 1.15f, label);
        }

        /// <summary>
        /// 기본 화면 캡처를 마친 뒤 실제 스프라이트로 표시 경로와 대체 순서를 검사하고 모든 임시 참조를 복원합니다.
        /// </summary>
        /// <param name="host">도감 카메라·미러 입력·실제 렌더 텍스처를 제공하는 통합 호스트입니다.</param>
        /// <returns>임시 이미지 주입, 화면 전환과 렌더 대기를 포함한 검증 코루틴입니다.</returns>
        private IEnumerator VerifyCollectionArtwork(TabHost host)
        {
            host.SelectTab(2);
            yield return new WaitForSeconds(.35f);
            var collection = host.GetRoot(2).GetComponent<CollectionPresenter>();
            var originalAppearance = collection.Appearance;
            var originalProgress = collection.Progress;
            int restoredCount = originalProgress.RestoredCount;
            var restoredIds = collection.Catalog.Relics.Where(relic => originalProgress.IsRestored(relic.Id))
                .Select(relic => relic.Id).ToArray();
            var unreadIds = collection.Catalog.Relics.Where(relic => originalProgress.IsUnread(relic.Id))
                .Select(relic => relic.Id).ToArray();
            var relicIds = collection.Catalog.Relics.Select(relic => relic.Id).ToArray();
            var restoredRelic = collection.Catalog.Relics.First(relic =>
                originalProgress.IsRestored(relic.Id) && !originalProgress.IsUnread(relic.Id));
            var era = collection.Catalog.FindEra(restoredRelic.EraId);
            var lockedRelic = collection.Catalog.Relics.First(relic =>
                relic.EraId == era.Id && !originalProgress.IsRestored(relic.Id));
            var completedEra = collection.Catalog.Eras.First(item => originalProgress.IsBuildingUnlocked(item.Id));
            var restore = new List<Action>();
            var temporary = new List<UnityEngine.Object>();
            try
            {
                var blue = VerificationSprite(new Color(.2f, .3f, .75f), temporary);
                var green = VerificationSprite(new Color(.2f, .7f, .3f), temporary);
                var red = VerificationSprite(new Color(.8f, .25f, .2f), temporary);
                var appearance = ScriptableObject.CreateInstance<CollectionAppearance>();
                temporary.Add(appearance);
                foreach (string field in new[]
                {
                    "_pageBackground", "_backButtonBackground", "_lockIllustration", "_progressTrack", "_spiritPlateBackground"
                }) OverrideSprite(appearance, field, blue, restore);
                foreach (string field in new[]
                {
                    "_eraCardBackground", "_restoredRelicCardBackground", "_buildingCardBackground", "_newBadgeIcon",
                    "_completionIcon", "_progressFill", "_accentStrip"
                }) OverrideSprite(appearance, field, green, restore);
                foreach (string field in new[]
                {
                    "_lockedRelicCardBackground", "_relicPlateBackground", "_newBadgeBackground", "_backIcon", "_divider"
                }) OverrideSprite(appearance, field, red, restore);
                OverrideSprite(era, "_overviewIllustration", green, restore);
                OverrideSprite(era, "_contextIllustration", blue, restore);
                OverrideSprite(era, "_lockedBuildingIllustration", red, restore);
                OverrideSprite(era, "_backgroundIllustration", green, restore);
                OverrideSprite(completedEra, "_buildingIllustration", red, restore);
                OverrideSprite(restoredRelic, "_illustration", blue, restore);
                OverrideSprite(restoredRelic, "_thumbnail", green, restore);
                OverrideSprite(restoredRelic, "_spiritIllustration", red, restore);
                OverrideSprite(restoredRelic, "_backgroundIllustration", red, restore);
                // A locked type deliberately also has restored artwork; the lock path must keep it hidden.
                OverrideSprite(lockedRelic, "_illustration", blue, restore);
                OverrideSprite(lockedRelic, "_thumbnail", green, restore);
                OverrideSprite(lockedRelic, "_lockedIllustration", red, restore);
                collection.BindAppearance(appearance);
                collection.ShowOverview();
                yield return new WaitForEndOfFrame();
                Check(MatchesImage(collection.transform, "Paper", blue, true) &&
                    MatchesImage(collection.EraButtons[era.Id].transform, "Era Emblem", green) &&
                    MatchesImage(collection.EraButtons[era.Id].transform, "Era_" + era.Id, green, true, true),
                    "assigned shared background and era artwork render without tint and with sliced card frames");
                Check(MatchesImage(collection.transform, "New Icon", green) &&
                    MatchesImage(collection.EraButtons[completedEra.Id].transform, "Complete Icon", green) &&
                    MatchesImage(collection.transform, "New", red, true) &&
                    MatchesImage(collection.transform, "Accent", green, true) &&
                    MatchesImage(collection.transform, "Stats Rule", red, true) &&
                    MatchesImage(collection.transform, "Progress Track", blue, true) &&
                    MatchesImage(collection.transform, "Progress Fill", green, true),
                    "shared new and completion icons and collection decorations use assigned images");
                CheckBackgroundRender(host, 2, "shared page sprite produces blue pixels in actual collection render");
                Click(host, collection.EraButtons[era.Id].transform.position);
                yield return new WaitForEndOfFrame();
                Check(collection.Page == CollectionPage.Era &&
                    MatchesImage(collection.RelicButtons[restoredRelic.Id].transform, "Relic Emblem", green) &&
                    MatchesImage(collection.RelicButtons[lockedRelic.Id].transform, "Relic Emblem", red) &&
                    MatchesImage(collection.RelicButtons[restoredRelic.Id].transform, "Relic_" + restoredRelic.Id, green, true, true) &&
                    MatchesImage(collection.RelicButtons[lockedRelic.Id].transform, "Relic_" + lockedRelic.Id, red, true, true),
                    "image-decorated era button accepts mirror input and relic cards separate thumbnails from locked artwork");
                Check(MatchesImage(collection.transform, "Context Emblem", blue) &&
                    MatchesImage(collection.transform, "Building Emblem", red) &&
                    MatchesImage(collection.transform, "Building Card", green, true) &&
                    MatchesImage(collection.transform, "Back Icon", red) &&
                    MatchesImage(collection.BackButton.transform, "Back", blue, true, true) &&
                    MatchesImage(collection.transform, "Header Rule", red, true),
                    "era context locked building and back control use their assigned images");
                CheckBackgroundRender(host, 1, "era background overrides shared page sprite in actual render");

                OverrideSprite(restoredRelic, "_thumbnail", null, restore);
                OverrideSprite(lockedRelic, "_lockedIllustration", null, restore);
                OverrideSprite(era, "_contextIllustration", null, restore);
                OverrideSprite(era, "_lockedBuildingIllustration", null, restore);
                collection.ShowEra(era.Id);
                yield return new WaitForEndOfFrame();
                Check(MatchesImage(collection.RelicButtons[restoredRelic.Id].transform, "Relic Emblem", blue) &&
                    MatchesImage(collection.RelicButtons[lockedRelic.Id].transform, "Relic Emblem", blue) &&
                    MatchesImage(collection.transform, "Context Emblem", green) &&
                    MatchesImage(collection.transform, "Building Emblem", blue),
                    "missing thumbnail context and individual locks fall back to detailed artwork era artwork and shared lock");
                OverrideSprite(appearance, "_lockIllustration", null, restore);
                collection.BindAppearance(appearance);
                Check(HasVectorArtwork(collection.RelicButtons[lockedRelic.Id].transform, "Relic Emblem") &&
                    HasVectorArtwork(collection.transform, "Building Emblem"),
                    "missing individual and shared locks retain vector locks without exposing restored artwork");
                OverrideSprite(appearance, "_lockIllustration", blue, restore);
                collection.BindAppearance(appearance);
                collection.ShowEra(completedEra.Id);
                yield return new WaitForEndOfFrame();
                Check(MatchesImage(collection.transform, "Building Emblem", red),
                    "unlocked building uses era building illustration");
                collection.ShowEra(era.Id);
                // This era may retain the bottom position from the preceding progress-binding checks.
                collection.Scroll.StopMovement();
                collection.Scroll.verticalNormalizedPosition = 1;
                yield return new WaitForEndOfFrame();
                Click(host, collection.RelicButtons[restoredRelic.Id].transform.position);
                yield return new WaitForEndOfFrame();
                Check(collection.Page == CollectionPage.Relic,
                    "image-decorated relic card accepts mirror input after canvas update");
                Check(MatchesImage(collection.transform, "Relic Illustration", blue) &&
                    MatchesImage(collection.transform, "Spirit Illustration", red) &&
                    MatchesImage(collection.transform, "Illustration Plate", red, true) &&
                    MatchesImage(collection.transform, "Spirit Plate", blue, true),
                    "image-decorated relic card opens distinct detailed relic and spirit images");
                Check(collection.GetComponentsInChildren<Image>(true).Where(image =>
                    image.gameObject.activeInHierarchy && image.sprite && !image.GetComponent<Button>())
                    .All(image => image.color == Color.white && !image.raycastTarget),
                    "all assigned decorative images preserve source color and leave input to controls");
                CheckBackgroundRender(host, 0, "relic background overrides era and shared page sprites in actual render");
                CheckRender(host);
                OverrideSprite(restoredRelic, "_backgroundIllustration", null, restore);
                collection.ShowRelic(restoredRelic.Id);
                yield return new WaitForEndOfFrame();
                CheckBackgroundRender(host, 1, "missing relic background falls back to era sprite in actual render");
                OverrideSprite(era, "_backgroundIllustration", null, restore);
                collection.ShowRelic(restoredRelic.Id);
                yield return new WaitForEndOfFrame();
                CheckBackgroundRender(host, 2, "missing relic and era backgrounds fall back to shared sprite in actual render");
                collection.BindAppearance(null);
                var paper = ActiveImage(collection.transform, "Paper");
                Check(collection.Appearance == null && paper && !paper.sprite && paper.type == Image.Type.Simple &&
                    !ActiveImage(collection.transform, "Back Icon") &&
                    collection.BackButton.GetComponentsInChildren<Text>().Any(text => text.text == "‹"),
                    "removing shared appearance restores default background and back label");
                collection.GoBack();
                Check(HasVectorArtwork(collection.RelicButtons[lockedRelic.Id].transform, "Relic Emblem"),
                    "removing shared appearance retains vector lock fallback");
                collection.BindAppearance(appearance);
                Check(collection.Appearance == appearance &&
                    MatchesImage(collection.transform, "Paper", blue, true) &&
                    MatchesImage(collection.RelicButtons[lockedRelic.Id].transform, "Relic Emblem", blue),
                    "rebinding shared appearance restores its background and lock images");
            }
            finally
            {
                for (int i = restore.Count - 1; i >= 0; i--) restore[i]();
                collection.BindAppearance(originalAppearance);
                collection.ShowOverview();
                for (int i = temporary.Count - 1; i >= 0; i--) Destroy(temporary[i]);
            }
            yield return new WaitForEndOfFrame();
            Check(collection.Appearance == originalAppearance && collection.Progress == originalProgress &&
                collection.Progress.RestoredCount == restoredCount &&
                collection.Catalog.Relics.Select(relic => relic.Id).SequenceEqual(relicIds) &&
                collection.Catalog.Relics.Where(relic => collection.Progress.IsRestored(relic.Id)).Select(relic => relic.Id).SequenceEqual(restoredIds) &&
                collection.Catalog.Relics.Where(relic => collection.Progress.IsUnread(relic.Id)).Select(relic => relic.Id).SequenceEqual(unreadIds),
                "temporary artwork verification restores original references without changing collection records");
        }
        /// <summary>
        /// 실제 씬과 입력 브리지로 초기 로딩 정책·전시관·탭 전환·발굴·일시정지·해상도 변경을 검증하고 화면을 저장합니다.
        /// </summary>
        /// <returns>통합 씬 로드부터 도감 이미지 참조 복구까지 순서대로 실행하는 전체 검증 코루틴입니다.</returns>
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
            var housing = exhibition.GetComponent<ExhibitionHousing>();
            Check(housing && housing.Layout != null && housing.Wanderer, "housing and dokkaebi initialized");
            Check(!housing.Menu.IsVisible && !housing.GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "Controls"),
                "home has no bottom housing panel or persistent instructions");
            yield return BeginHousingPlacement(host, housing, new Vector2Int(5, 5));
            yield return BeginHousingPlacement(host, housing, new Vector2Int(5, 5), HousingAction.Cancel);
            yield return BeginHousingPlacement(host, housing, new Vector2Int(5, 5));
            int initialCount = housing.Layout.Placements.Count;
            var newCell = new Vector2Int(4, 4);
            Click(host, surface.CellWorldPosition(newCell));
            Check(housing.Layout.Placements.Count == initialCount + 1 && housing.Layout.At(newCell) != null,
                "mirror UI button and floor click place a stand");
            var placement = housing.Layout.At(newCell);
            Click(host, surface.CellWorldPosition(newCell));
            Check(!housing.Menu.IsVisible && placement.Cell == newCell, "short tap does not open menu or move stand");
            var quickSwipe = new PointerEventData(EventSystem.current)
            { pointerId = -1, position = ScreenPoint(host, surface.CellWorldPosition(newCell)),
                button = PointerEventData.InputButton.Left };
            host.input.OnPointerDown(quickSwipe);
            quickSwipe.position -= Vector2.right * Screen.width * .12f;
            host.input.OnDrag(quickSwipe);
            yield return new WaitForSeconds(.55f);
            Check(!housing.Menu.IsVisible && host.Transition.IsActive, "swiping before hold keeps tab gesture ownership");
            host.input.OnPointerUp(quickSwipe);
            yield return new WaitForSeconds(.35f);
            yield return HousingGesture(host, housing, newCell, HousingAction.Move);
            var movedCell = new Vector2Int(5, 4);
            Click(host, surface.CellWorldPosition(movedCell));
            Check(placement.Cell == movedCell && housing.Layout.At(newCell) == null,
                "floor selection moves stand and releases old occupancy");
            yield return HousingGesture(host, housing, movedCell, HousingAction.Rotate);
            Check(placement.QuarterTurns == 1 && placement.Cell == movedCell,
                "rotation executes once per hold and preserves 1x1 anchor");
            var cancelHold = new PointerEventData(EventSystem.current)
            { pointerId = -1, position = ScreenPoint(host, surface.CellWorldPosition(movedCell)),
                button = PointerEventData.InputButton.Left };
            host.input.OnPointerDown(cancelHold);
            yield return new WaitForSeconds(.55f);
            Check(housing.Menu.IsVisible, "long press captures floor input");
            host.input.Cancel();
            Check(!housing.Menu.IsVisible && placement.QuarterTurns == 1,
                "input cancellation closes menu without another action");
            yield return HousingGesture(host, housing, movedCell, HousingAction.Recall);
            Check(housing.Layout.At(movedCell) == null && housing.Layout.Placements.Count == initialCount,
                "remove control releases occupied cell");
            yield return BeginHousingPlacement(host, housing, new Vector2Int(5, 5));
            Click(host, surface.CellWorldPosition(new Vector2Int(2, 2)));
            Check(housing.Layout.Placements.Count == initialCount, "occupied floor rejects another stand");
            var floorSwipe = new PointerEventData(EventSystem.current)
            { pointerId = -1, position = ScreenPoint(host, surface.CellWorldPosition(newCell)),
                button = PointerEventData.InputButton.Left };
            host.input.OnPointerDown(floorSwipe);
            floorSwipe.position -= Vector2.right * Screen.width * .12f;
            host.input.OnDrag(floorSwipe);
            yield return new WaitForSeconds(.15f);
            host.input.OnPointerUp(floorSwipe);
            yield return new WaitForSeconds(.35f);
            Check(host.ActiveIndex == 0 && housing.Layout.Placements.Count == initialCount,
                "floor swipe cancels click without accidental housing placement");
            housing.ResetSelection();
            var externalItem = Instantiate(housing.DefaultItem);
            JsonUtility.FromJsonOverwrite("{\"_itemId\":\"catalog-bridge-test\",\"_footprint\":{\"x\":2,\"y\":1}}", externalItem);
            HousingPlacement addedItem = null;
            HousingPlacement removedItem = null;
            System.Action requestItem = () => housing.BeginPlacement(externalItem);
            System.Action<HousingPlacement> onAdded = item => addedItem = item;
            System.Action<HousingPlacement> onRemoved = item => removedItem = item;
            housing.PlacementRequested += requestItem;
            housing.PlacementAdded += onAdded;
            housing.PlacementRemoved += onRemoved;
            housing.RequestPlacement();
            Check(housing.IsPlacing, "external catalog bridge starts item placement");
            var externalCell = new Vector2Int(4, 4);
            Check(housing.Layout.CanPlace(externalCell, externalItem.Footprint), "external item test footprint is free");
            housing.SelectCell(externalCell);
            Check(addedItem != null && addedItem.ItemId == externalItem.ItemId && addedItem.Size == new Vector2Int(2, 1),
                "external definition supplies item identity and footprint and emits placement event");
            housing.ExecuteAction(addedItem.Id, HousingAction.Recall);
            Check(removedItem == addedItem && housing.Layout.Placements.Count == initialCount,
                "recall emits item identity for catalog bridge");
            housing.PlacementRequested -= requestItem;
            housing.PlacementAdded -= onAdded;
            housing.PlacementRemoved -= onRemoved;
            Destroy(externalItem);
            Check(!housing.Layout.CanPlace(housing.Wanderer.CurrentCell, Vector2Int.one) &&
                !housing.Layout.CanPlace(housing.Wanderer.TargetCell, Vector2Int.one),
                "housing cannot cover dokkaebi traversal");
            int steps = housing.Wanderer.CompletedSteps;
            yield return new WaitForSeconds(2.5f);
            Check(housing.Wanderer.CompletedSteps > steps && housing.Layout.IsWalkable(housing.Wanderer.CurrentCell) &&
                housing.Layout.IsWalkable(housing.Wanderer.TargetCell), "dokkaebi wanders through free adjacent cells");
            Check(housing.Wanderer.gameObject.scene == host.ActiveRoot.gameObject.scene &&
                housing.Wanderer.GetComponentsInChildren<Transform>().All(child => child.gameObject.layer == host.ActiveRoot.RenderLayer),
                "runtime dummy stays in home scene and render layer");
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
                Check(host.profileBar.activeSelf == host.catalog.tabs[i].showProfile, "profile visibility " + i);
                yield return new WaitForEndOfFrame();
                CheckRender(host);
                Capture($"0{i+1}_{host.catalog.tabs[i].id}.png");
                yield return null;
            }
            yield return VerifyCollection(host);
            host.SelectTab(3); yield return new WaitForSeconds(.35f);
            var contentDrag = host.GetRoot(3).GetComponentInChildren<DemoDraggable>();
            var dragBefore = contentDrag.transform.position;
            var contentPointer = new PointerEventData(EventSystem.current)
            { pointerId = -1, position = ScreenPoint(host, dragBefore), button = PointerEventData.InputButton.Left };
            host.input.OnPointerDown(contentPointer);
            contentPointer.position += new Vector2(50, 30);
            host.input.OnDrag(contentPointer); host.input.OnPointerUp(contentPointer);
            Check(host.ActiveIndex == 3 && !host.IsBusy && Vector3.Distance(dragBefore, contentDrag.transform.position) > .1f,
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
                host.SelectTab(2); yield return new WaitForSeconds(.4f);
                host.GetRoot(2).GetComponent<CollectionPresenter>().ShowOverview();
                yield return new WaitForEndOfFrame();
                CheckRender(host);
                Capture($"Collection_Overview_{size.x}x{size.y}.png");
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
            yield return VerifyCollectionArtwork(host);
            Check(_errors.Count == 0, "no runtime errors during integration checks");
            yield return new WaitForSeconds(.3f);
        }
    }
}
