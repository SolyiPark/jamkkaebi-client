using System;
using System.Collections;
using System.Linq;
using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.UI.Common;
using Jamkkaebi.Scripts.UI.Workbench;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Shell
{
    /// <summary>
    /// 셸 수명의 공유 기록. 서버 API 클라이언트·작업대 백엔드·도감 기록을 하나씩 갖고, 탭 씬이 Current로 가져다 쓴다.
    /// 작업대는 숨기면 꺼지는 탭이라 공유 기록을 그 안에 두지 않는다.
    /// </summary>
    /// <remarks>
    /// 서버 주소가 있으면 ServerWorkbenchBackend, 없으면 MockWorkbenchBackend를 쓴다. 서버 실패 시 모의로 바꾸지 않는다.
    /// API 클라이언트는 여기 하나만 두고 친구 탭도 함께 쓴다. 탭마다 만들면 회전하는 refresh 토큰을 서로 소비해
    /// 재발급이 실패(A005)하기 때문이다. 작업대 HTTP 코루틴도 꺼지지 않는 이 오브젝트에서 돈다.
    /// </remarks>
    public sealed class SharedRecords : MonoBehaviour
    {
        [Tooltip("도감 씬과 같은 CollectionCatalog.asset. 다른 인스턴스면 도감이 기록을 거부한다.")]
        [SerializeField] private CollectionCatalog _collectionCatalog;

        [Tooltip("서버 기본 주소. 비우면 환경 변수 JAMKKAEBI_SERVER_URL을 쓰고, 그것도 없으면 모의 모드다. 토큰은 Inspector에 넣지 않는다.")]
        [SerializeField] private string _serverBaseUrl = "";

        /// <summary>셸에 있는 공유 기록. 탭 씬만 단독으로 열었으면 null이다.</summary>
        public static SharedRecords Current { get; private set; }

        public Func<DateTime> Clock { get; private set; }
        public IWorkbenchBackend Workbench { get; private set; }

        /// <summary>공용 서버 API 클라이언트. 모의 모드면 null.</summary>
        public JamkkaebiApiClient Api { get; private set; }

        public bool UsesServer => Api != null;
        public CollectionProgress Collection { get; private set; }
        public CollectionRecorder Recorder { get; private set; }

        private void Awake()
        {
            if (Current != null)
            {
                Debug.LogWarning("SharedRecords가 이미 있어 이 컴포넌트는 쓰지 않습니다.", this);
                Destroy(this);
                return;
            }

            if (_collectionCatalog == null)
            {
                Debug.LogError("collectionCatalog가 비어 있습니다.", this);
                Destroy(this);
                return;
            }

            Clock = () => DateTime.UtcNow;
            Api = ServerConnection.Create(_serverBaseUrl);
            Workbench = Api != null ? new ServerWorkbenchBackend(Api, this) : (IWorkbenchBackend)WorkbenchDemo.CreateBackend(Clock);
            Collection = new CollectionProgress(_collectionCatalog);
            Recorder = new CollectionRecorder(Collection, WorkbenchRelicSeed.Instance);

            Current = this;
        }

        /// <summary>시작할 때 이미 최초 정화를 마친 정령을 도감에 확인한 상태로 채운다(작업대 홈 조회가 먼저 왔으면 그쪽이 이미 채웠다).</summary>
        private IEnumerator Start()
        {
            yield return Workbench.GetWorkbench(
                snapshot => Recorder.SyncCompleted(snapshot.Spirits.Select(spirit => spirit.RelicId)),
                error => { Debug.LogWarning($"작업대를 조회하지 못했습니다. {error}", this); });
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }
        
        [ContextMenu("Dev/도감 기록 출력")]
        private void LogCollection()
        {
            if (Collection == null) return; // Awake에서 쓰지 않게 됐거나 Play 중이 아닐 때
            Debug.Log($"도감 등록 {Collection.RestoredCount}/{Collection.TotalCount}", this);
        }
    }
}