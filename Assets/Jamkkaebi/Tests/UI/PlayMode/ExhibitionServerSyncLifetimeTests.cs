using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Jamkkaebi.Scripts.UI.Common;
using Jamkkaebi.Scripts.UI.Exhibition;
using Jamkkaebi.Scripts.UI.Shell;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Jamkkaebi.Tests.UI.PlayMode
{
    /// <summary>
    /// 홈 배치를 서버와 맞추는 컴포넌트가 꺼졌다 켜질 때의 수명을 실제 코루틴으로 확인한다.
    /// 전체 배치를 통째로 바꾸는 저장을 보내므로, 기다리는 도중에 꺼지면 저장이 나가지 않아야 하고
    /// 늦게 온 응답이 홈을 바꾸지 않아야 하며, 다시 켜면 정상으로 이어져야 한다.
    /// </summary>
    /// <remarks>
    /// EditMode 테스트는 불러오기·보내기를 끝까지 직접 돌려서 이 경로를 검증하지 못한다.
    /// 컴포넌트만 끄면 Unity가 코루틴을 멈추지 않는다는 동작은 Play에서만 확인된다.
    /// 전송은 API 클라이언트의 전송 경계를 바꿔 끼운 것이며 실제 HTTP가 아니다.
    /// </remarks>
    public sealed class ExhibitionServerSyncLifetimeTests
    {
        private const float Interval = .05f;
        private const float Timeout = 5f;
        private static readonly Vector2Int OldBuildingCell = new Vector2Int(5, 0);
        private static readonly Vector2Int NewBuildingCell = new Vector2Int(0, 0);

        private readonly List<(string method, string path, JObject body)> _sent = new List<(string, string, JObject)>();
        private readonly Queue<ServerApiResult> _responses = new Queue<ServerApiResult>();
        private bool _holding;
        private int _delivered;

        private GameObject _root;
        private ExhibitionGrid _grid;
        private ExhibitionBehaviorPolicy _policy;
        private ExhibitionHousing _housing;
        private ExhibitionServerSync _sync;

        private static JObject Response() => JObject.Parse(@"{
            'grid':{'width':7,'height':7},
            'spirits':[
                {'relicId':2,'spiritName':'달빛','displayed':true,'hidden':false,'position':{'x':1,'y':1}},
                {'relicId':8,'displayed':true,'hidden':true,'position':{'x':4,'y':4}}],
            'buildings':[{'buildingId':1,'displayed':true,'position':{'x':5,'y':0}}]}");

        /// <summary>다른 기기에서 건물을 옮긴 뒤의 전시관. 늦은 응답이 홈에 반영됐는지 건물 자리로 알아본다.</summary>
        private static JObject ResponseWithMovedBuilding()
        {
            var response = Response();
            response["buildings"][0]["position"] = new JObject { ["x"] = NewBuildingCell.x, ["y"] = NewBuildingCell.y };
            return response;
        }

        private static ServerApiResult Ok(JToken data) => new ServerApiResult(true, "OK", "성공", data, 200);

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        /// <summary>요청을 기록하고, 붙잡는 중이면 풀어 줄 때까지 응답을 주지 않는다.</summary>
        private IEnumerator Transport(string method, string path, JObject body, bool authenticated, Action<ServerApiResult> completed)
        {
            _sent.Add((method, path, body));
            var response = _responses.Dequeue();
            while (_holding) yield return null;
            _delivered++;
            completed(response);
        }

        private static IEnumerator WaitFor(Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + Timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("시간 안에 일어나지 않았습니다: " + what);
                yield return null;
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private int Puts => _sent.FindAll(request => request.method == "PUT").Count;

        /// <summary>홈을 세우고 맞춤 컴포넌트를 붙인다. 도깨비는 걷지 않게 해 배치가 테스트가 바꿀 때만 달라지게 한다.</summary>
        private IEnumerator BuildHome()
        {
            _sent.Clear(); _responses.Clear(); _holding = false; _delivered = 0;
            _grid = ScriptableObject.CreateInstance<ExhibitionGrid>();
            _policy = ScriptableObject.CreateInstance<ExhibitionBehaviorPolicy>();
            JsonUtility.FromJsonOverwrite("{\"_enableAmbientMovement\":false}", _policy);

            _root = new GameObject("Home");
            _root.SetActive(false);
            var cameraObject = new GameObject("Camera"); cameraObject.transform.SetParent(_root.transform, false);
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
            _root.AddComponent<MirrorSceneRoot>().sceneCamera = camera;
            var surfaceObject = new GameObject("Surface"); surfaceObject.transform.SetParent(_root.transform, false);
            var surface = surfaceObject.AddComponent<ExhibitionSurface>();
            SetField(surface, "_grid", _grid);
            var content = new GameObject("Content"); content.transform.SetParent(surfaceObject.transform, false);
            var intro = _root.AddComponent<ExhibitionIntro>();
            _housing = _root.AddComponent<ExhibitionHousing>();
            SetField(_housing, "_surface", surface);
            SetField(_housing, "_content", content.transform);
            SetField(_housing, "_policy", _policy);
            _root.SetActive(true);
            yield return null;
            intro.Complete();
            Assert.That(_housing.Layout, Is.Not.Null, "하우징이 시작돼야 한다");
            Assert.That(_housing.ServerSync, Is.Null, "셸이 없으므로 하우징이 스스로 붙이지 않는다");

            _sync = _root.AddComponent<ExhibitionServerSync>();
            _sync.Configure(_housing, new JamkkaebiApiClient("http://127.0.0.1:1", "access", Transport), () => true, Interval);
        }

        /// <summary>서버 배치를 불러온 홈. 2번은 1,1에, 8번은 4,4에 서 있고 건물은 5,0에 있다.</summary>
        private IEnumerator BuildLoadedHome()
        {
            yield return BuildHome();
            _responses.Enqueue(Ok(Response()));
            yield return WaitFor(() => _sync.Loaded, "서버 배치 불러오기");
            Assert.That(_housing.FindWanderer(2).PlacementCell, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(_housing.Layout.IsBlocked(OldBuildingCell), Is.True);
        }

        /// <summary>배치를 바꾸고, 저장용 조회가 나가 응답을 기다리는 상태까지 간다.</summary>
        private IEnumerator StartSaveAndHoldItsGet()
        {
            _housing.FindWanderer(2).TryMoveAnchor(new Vector2Int(0, 3));
            _holding = true;
            _responses.Enqueue(Ok(ResponseWithMovedBuilding()));
            yield return WaitFor(() => _sent.Count == 2, "저장용 조회");
            Assert.That(_sent[1].method, Is.EqualTo("GET"));
        }

        private void AssertSaveWasAbandoned()
        {
            Assert.That(Puts, Is.Zero, "꺼진 뒤에 서버 저장이 나가면 안 된다");
            Assert.That(_delivered, Is.EqualTo(1), "붙잡혀 있던 응답은 전달되지 않는다");
            Assert.That(_housing.Layout.IsBlocked(OldBuildingCell), Is.True, "늦은 응답이 홈의 건물 자리를 바꾸면 안 된다");
            Assert.That(_housing.Layout.IsBlocked(NewBuildingCell), Is.False);
            Assert.That(_housing.FindWanderer(2).PlacementCell, Is.EqualTo(new Vector2Int(0, 3)));
            Assert.That(_sync.SyncedSignature, Is.EqualTo("2@1,1;8@4,4;"), "저장하지 못했으므로 기준은 그대로다");
        }

        /// <summary>다시 켠 뒤 다음 차례에 조회와 저장이 정상으로 나가는지 본다.</summary>
        private IEnumerator AssertNextTurnSaves()
        {
            int before = _sent.Count;
            _responses.Enqueue(Ok(ResponseWithMovedBuilding()));
            var saved = ResponseWithMovedBuilding();
            saved["spirits"][0]["position"] = new JObject { ["x"] = 0, ["y"] = 3 };
            _responses.Enqueue(Ok(saved));
            yield return WaitFor(() => Puts == 1, "다시 켠 뒤의 저장");
            yield return WaitFor(() => _sync.SyncedSignature == "2@0,3;8@4,4;", "저장 결과 반영");

            Assert.That(_sent.Count, Is.EqualTo(before + 2), "조회 1번과 저장 1번");
            Assert.That(_sent[before].method, Is.EqualTo("GET"));
            Assert.That(_sent[before + 1].path, Is.EqualTo("/api/exhibition/layout"));
            Assert.That(_sync.LastError, Is.Null);
            Assert.That(_housing.Layout.IsBlocked(NewBuildingCell), Is.True, "이번에는 최신 건물 자리를 반영한다");
            Assert.That(_housing.Layout.IsBlocked(OldBuildingCell), Is.False);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _holding = false;
            if (_root) Object.Destroy(_root);
            if (_grid) Object.Destroy(_grid);
            if (_policy) Object.Destroy(_policy);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingOnlyTheComponentDuringSaveStopsThePutAndReenablingResumes()
        {
            yield return BuildLoadedHome();
            yield return StartSaveAndHoldItsGet();

            // 컴포넌트만 끈다. Unity는 이때 코루틴을 멈추지 않으므로 컴포넌트가 직접 멈춰야 한다.
            _sync.enabled = false;
            _holding = false;
            yield return Frames(10);
            AssertSaveWasAbandoned();

            _sync.enabled = true;
            yield return AssertNextTurnSaves();
        }

        [UnityTest]
        public IEnumerator DeactivatingTheObjectDuringSaveStopsThePutAndReactivatingResumes()
        {
            yield return BuildLoadedHome();
            yield return StartSaveAndHoldItsGet();

            _root.SetActive(false);
            _holding = false;
            yield return Frames(10);
            AssertSaveWasAbandoned();

            _root.SetActive(true);
            yield return AssertNextTurnSaves();
        }

        [UnityTest]
        public IEnumerator DisablingDuringLoadDropsTheLateResponseAndReenablingLoadsAgain()
        {
            yield return BuildHome();
            _holding = true;
            _responses.Enqueue(Ok(Response()));
            yield return WaitFor(() => _sent.Count == 1, "첫 조회");

            _sync.enabled = false;
            _holding = false;
            yield return Frames(10);
            Assert.That(_sync.Loaded, Is.False);
            Assert.That(_housing.Wanderers, Is.Empty, "늦은 응답으로 도깨비를 세우면 안 된다");
            Assert.That(_housing.Layout.IsBlocked(OldBuildingCell), Is.False);
            Assert.That(Puts, Is.Zero);

            _responses.Enqueue(Ok(Response()));
            _sync.enabled = true;
            yield return WaitFor(() => _sync.Loaded, "다시 켠 뒤의 불러오기");
            Assert.That(_housing.FindWanderer(2).PlacementCell, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(_housing.FindWanderer(8).PlacementCell, Is.EqualTo(new Vector2Int(4, 4)));
            Assert.That(Puts, Is.Zero, "불러오기만 했고 배치가 그대로라 보내지 않는다");
        }

        [UnityTest]
        public IEnumerator UninterruptedSaveStillGoesThroughWithRealCoroutines()
        {
            yield return BuildLoadedHome();
            _housing.FindWanderer(2).TryMoveAnchor(new Vector2Int(0, 3));
            yield return AssertNextTurnSaves();
        }
    }
}
