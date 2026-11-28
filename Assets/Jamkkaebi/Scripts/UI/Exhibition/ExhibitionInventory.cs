using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Jamkkaebi.Scripts.UI.Shell;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Exhibition
{
    /// <summary>
    /// 전시관에 놓을 수 있는 보유 객체 목록. 도깨비는 셸 공유 기록의 작업대 조회(최초 정화를 마친 정령)에서 읽는다.
    /// 건물은 서버 연결 때 같은 목록에 더한다.
    /// </summary>
    /// <remarks>
    /// 조회에 실패해도 모의 데이터로 바꾸지 않고 이전 목록을 그대로 둔다. 숨겨지거나 다시 조회하면 앞선 조회의 늦은 응답은 버린다.
    /// </remarks>
    public sealed class ExhibitionInventory : MonoBehaviour
    {
        private readonly List<ExhibitionPlaceable> _items = new List<ExhibitionPlaceable>();
        private ReadOnlyCollection<ExhibitionPlaceable> _readOnly;
        private int _generation;

        public IReadOnlyList<ExhibitionPlaceable> Items => _readOnly ??= _items.AsReadOnly();

        /// <summary>조회가 진행 중인지.</summary>
        public bool Loading { get; private set; }

        /// <summary>마지막 조회가 실패했으면 그 사유. 성공했거나 조회한 적이 없으면 null.</summary>
        public string Error { get; private set; }

        /// <summary>목록이나 조회 상태가 바뀌었을 때.</summary>
        public event Action Changed;

        /// <summary>목록을 통째로 바꾼다. 같은 Key가 여러 번 오면 처음 것만 남긴다.</summary>
        /// <exception cref="ArgumentNullException">items가 null인 경우.</exception>
        public void Set(IEnumerable<ExhibitionPlaceable> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            // 먼저 새 목록을 다 만든 뒤 바꾼다. items가 Items나 그것을 걸러 낸 지연 열거라면 먼저 비우는 순간 내용이 사라진다.
            var keys = new HashSet<string>();
            var next = new List<ExhibitionPlaceable>();
            foreach (var item in items)
                if (item != null && keys.Add(item.Key)) next.Add(item);
            _items.Clear();
            _items.AddRange(next);
            Changed?.Invoke();
        }

        /// <summary>Key가 같은 보유 객체를 찾는다. 없으면 null.</summary>
        public ExhibitionPlaceable Find(string key)
        {
            foreach (var item in _items)
                if (item.Key == key) return item;
            return null;
        }

        /// <summary>
        /// 셸의 작업대 백엔드에서 정령 목록을 다시 읽는다. 셸 없이 이 씬만 열었으면 아무것도 하지 않는다.
        /// </summary>
        public void Refresh()
        {
            var records = SharedRecords.Current;
            if (records == null || records.Workbench == null || !isActiveAndEnabled) return;
            int generation = ++_generation;
            Loading = true; Error = null;
            Changed?.Invoke();
            StartCoroutine(records.Workbench.GetWorkbench(
                snapshot =>
                {
                    if (generation != _generation) return;
                    Loading = false;
                    var spirits = new List<ExhibitionPlaceable>();
                    foreach (var spirit in snapshot.Spirits)
                        spirits.Add(new ExhibitionPlaceable(ExhibitionPlaceable.SpiritType, spirit.RelicId, spirit.SpiritName));
                    Set(spirits);
                },
                error =>
                {
                    if (generation != _generation) return;
                    Loading = false; Error = error;
                    Changed?.Invoke();
                }));
        }

        private void OnDisable()
        {
            // 비활성화되면 코루틴이 멈춘다. 끝나지 못한 조회의 늦은 응답이 와도 버리도록 세대를 올린다.
            _generation++;
            Loading = false;
        }
    }
}
