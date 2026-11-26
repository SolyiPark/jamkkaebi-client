using UnityEngine;
using UnityEngine.EventSystems;

namespace Jamkkaebi.Scripts.Gameplay.Minigame.Presentation
{
    /// <summary>
    /// 미니게임 씬을 단독으로 Play할 때만 씬의 EventSystem을 켠다.
    /// </summary>
    /// <remarks>
    /// 셸 위에 추가 로드될 때는 셸의 EventSystem 하나만 써야 한다. 씬의 EventSystem을 켠 채로 저장하면
    /// 로드 도중 OnEnable에서 "There can be only one active Event System." 경고가 나므로 꺼 두고 저장한다.
    /// 다른 EventSystem이 켜져 있지 않을 때(단독 Play)만 켠다.
    /// </remarks>
    public sealed class StandaloneEventSystem : MonoBehaviour
    {
        [Tooltip("씬에 꺼 둔 채로 저장한 EventSystem 오브젝트.")]
        [SerializeField] private GameObject _eventSystem;

        private void Awake()
        {
            if (_eventSystem == null)
            {
                Debug.LogWarning($"{nameof(StandaloneEventSystem)}: 켤 EventSystem이 연결되지 않았습니다.", this);
                return;
            }
            if (_eventSystem.activeSelf || HasOtherActiveEventSystem()) return;
            _eventSystem.SetActive(true);
        }

        /// <summary>이 씬 것이 아닌, 켜진 EventSystem이 이미 있는지.</summary>
        private bool HasOtherActiveEventSystem()
        {
            // 꺼진 오브젝트는 찾지 않는다. 컴포넌트만 꺼진 EventSystem은 동작하지 않으므로 없는 것으로 본다.
            foreach (EventSystem events in FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                if (events.enabled && events.gameObject != _eventSystem)
                    return true;
            return false;
        }
    }
}
