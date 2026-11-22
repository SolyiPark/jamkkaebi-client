using System;
using System.Collections;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using Jamkkaebi.Scripts.Gameplay.Purification;
using Jamkkaebi.Scripts.UI.Minigame;
using Jamkkaebi.Scripts.UI.Shell;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Jamkkaebi.Scripts.UI.Workbench
{
    /// <summary>
    /// Minigame 씬을 셸 위에 추가 로드해 한 판을 돌리고, 보고와 함께 내린다.
    /// 로드한 씬의 EventSystem·AudioListener는 끄고(셸 것만 쓴다), 셸 화면은 가리고(CoverShell), 탭 이동은 잠근다.
    /// 탭 잠금은 풀지 않는다. 보고·결과 화면까지 잠가 둘지는 작업대 컨트롤러가 정한다.
    /// </summary>
    /// <remarks>
    /// 탭 씬과 달리 기본 물리 씬으로 로드한다. 미니게임 타일은 OnMouseDown으로 눌리는데, 이 입력은 기본 물리 씬에서만 동작한다.
    /// </remarks>
    public sealed class MinigameLauncher : MonoBehaviour
    {
        public const string ScenePath = "Assets/Jamkkaebi/Scenes/Minigame.unity";

        // 셸 카메라(depth 0)보다 뒤에 그려지도록. 셸 카메라는 가려진 동안 꺼지지만, 꺼지지 않아도 미니게임이 위에 보이게 한다.
        private const float CameraDepth = 10f;

        // 셸 Overlay 캔버스(정렬 0)보다 위.
        private const int OverlaySortingOrder = 100;

        private MirrorSceneRoot _root;
        private Scene _scene;
        private MinigameSessionAdapter _adapter;
        private MinigameHudPresenter _hud;
        private PlayReport _report;
        private bool _exitRequested;

        /// <summary>미니게임이 떠 있는지. 떠 있는 동안 다시 부르면 실패로 알린다.</summary>
        public bool IsRunning { get; private set; }

        public void Initialize(MirrorSceneRoot root)
        {
            _root = root ? root : throw new ArgumentNullException(nameof(root));
        }

        /// <summary>
        /// 한 판을 띄우고, 나가면 보고를 completed로 돌려준다. 보고는 나가기보다 먼저 오므로(HUD가 보장) 나갈 때 항상 있다.
        /// 띄우지 못하면 failed.
        /// </summary>
        public IEnumerator Run(PlaySetup setup, Action<PlayReport> completed, Action<string> failed)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (!_root) throw new InvalidOperationException("Initialize를 먼저 불러야 합니다.");
            if (IsRunning)
            {
                failed("미니게임이 이미 열려 있어요.");
                yield break;
            }

            IsRunning = true;
            _report = null;
            _exitRequested = false;
            _root.SetNavigationLocked(true);
            _root.SetCoverShell(true);

            AsyncOperation load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            if (load == null)
            {
                yield return Close();
                failed("미니게임을 불러오지 못했어요.");
                yield break;
            }
            yield return load;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            PrepareScene();
            if (!_adapter || !_hud)
            {
                Debug.LogError($"{nameof(MinigameLauncher)}: {ScenePath}에서 미니게임 어댑터나 HUD를 찾지 못했습니다.", this);
                yield return Close();
                failed("미니게임을 불러오지 못했어요.");
                yield break;
            }

            // 구독한 뒤에 연다. 여는 도중 일어난 알림도 놓치지 않는다.
            _adapter.PlayFinished += OnPlayFinished;
            _hud.ExitRequested += OnExitRequested;
            _adapter.Begin(setup);

            while (!_exitRequested) yield return null;

            PlayReport report = _report;
            yield return Close();

            if (report == null)
            {
                // HUD는 보고를 보낸 뒤에 나가기를 알리므로 정상 흐름에서는 오지 않는다.
                failed("미니게임 결과를 받지 못했어요.");
                yield break;
            }
            completed(report);
        }

        private void OnDestroy()
        {
            // 작업대 씬이 먼저 내려가면(복귀 재시작 등) 미니게임 씬과 셸 가리기를 남기지 않는다.
            Unsubscribe();
            if (IsRunning && _scene.IsValid() && _scene.isLoaded) SceneManager.UnloadSceneAsync(_scene);
            if (_root) _root.SetCoverShell(false);
        }

        /// <summary>로드한 씬에서 셸과 겹치는 것을 끄고, 어댑터와 HUD를 찾는다. 다른 씬은 건드리지 않는다.</summary>
        private void PrepareScene()
        {
            _adapter = null;
            _hud = null;
            if (!_scene.IsValid() || !_scene.isLoaded) return;

            foreach (GameObject obj in _scene.GetRootGameObjects())
            {
                foreach (EventSystem events in obj.GetComponentsInChildren<EventSystem>(true))
                    events.gameObject.SetActive(false);
                foreach (AudioListener listener in obj.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
                foreach (Camera sceneCamera in obj.GetComponentsInChildren<Camera>(true))
                    sceneCamera.depth = CameraDepth;
                // 셸의 Overlay 캔버스와 정렬 순서가 같으면 어느 쪽이 위에 그려질지 정해지지 않는다. 미니게임 HUD를 위로 올린다.
                foreach (Canvas canvas in obj.GetComponentsInChildren<Canvas>(true))
                    if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, OverlaySortingOrder);

                if (!_adapter) _adapter = obj.GetComponentInChildren<MinigameSessionAdapter>(true);
                if (!_hud) _hud = obj.GetComponentInChildren<MinigameHudPresenter>(true);
            }
        }

        /// <summary>구독을 풀고 씬을 내린 뒤 셸 화면을 되돌린다. 성공·실패 경로가 함께 쓴다.</summary>
        private IEnumerator Close()
        {
            // 언로드 중 어댑터의 OnDisable이 다시 알려도 받지 않도록 먼저 해제한다.
            Unsubscribe();
            if (_scene.IsValid() && _scene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(_scene);
                if (unload != null) yield return unload;
            }
            _scene = default;
            if (_root) _root.SetCoverShell(false);
            IsRunning = false;
        }

        private void Unsubscribe()
        {
            if (_adapter) _adapter.PlayFinished -= OnPlayFinished;
            if (_hud) _hud.ExitRequested -= OnExitRequested;
            _adapter = null;
            _hud = null;
        }

        private void OnPlayFinished(PlayReport report) => _report = report;
        private void OnExitRequested() => _exitRequested = true;
    }
}
