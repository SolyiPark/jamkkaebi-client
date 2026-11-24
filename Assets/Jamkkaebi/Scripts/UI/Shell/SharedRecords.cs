using System;
using System.Collections;
using Jamkkaebi.Scripts.Gameplay.Collection;
using Jamkkaebi.Scripts.UI.Workbench;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Shell
{
    /// <summary>
    /// 셸 수명의 공유 기록. 작업대 백엔드와 도감 기록을 하나씩 갖고, 탭 씬이 Current로 가져다 쓴다.
    /// 작업대는 숨기면 꺼지는 탭이라 공유 기록을 그 안에 두지 않는다.
    /// </summary>
    public sealed class SharedRecords : MonoBehaviour
    {
        [Tooltip("도감 씬과 같은 CollectionCatalog.asset. 다른 인스턴스면 도감이 기록을 거부한다.")]
        [SerializeField] private CollectionCatalog _collectionCatalog;

        /// <summary>셸에 있는 공유 기록. 탭 씬만 단독으로 열었으면 null이다.</summary>
        public static SharedRecords Current { get; private set; }

        public Func<DateTime> Clock { get; private set; }
        public IWorkbenchBackend Workbench { get; private set; }
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
            Workbench = WorkbenchDemo.CreateBackend(Clock);
            Collection = new CollectionProgress(_collectionCatalog);
            Recorder = new CollectionRecorder(Collection, WorkbenchRelicSeed.Instance);

            Current = this;
        }

        /// <summary>시작할 때 이미 최초 정화를 마친 정령을 도감에 확인한 상태로 채운다.</summary>
        private IEnumerator Start()
        {
            yield return Workbench.GetWorkbench(
                snapshot =>
                {
                    foreach (WorkbenchSpirit spirit in snapshot.Spirits)
                    {
                        Recorder.RegisterCompleted(spirit.RelicId, true);
                    }
                },
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