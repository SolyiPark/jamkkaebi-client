using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jamkkaebi.Scripts.UI.Common
{
    /// <summary>
    /// 등록된 창 프리팹을 처음 쓸 때 만들어 두고, 이후에는 만든 창을 다시 꺼내 쓴다.
    /// </summary>
    /// <remarks>
    /// 창을 씬에 미리 넣어 두지 않으므로 씬이 가벼워지고, 창의 생김새는 프리팹 하나만 고치면 된다.
    /// 창은 스크립트 종류로 찾는다. 예를 들어 <c>Get&lt;ResultPopup&gt;()</c>은 등록된 프리팹 중 ResultPopup이 붙은 것을 쓴다.
    /// 그래서 같은 종류의 창 프리팹은 하나만 등록한다.
    /// </remarks>
    public class UILoader : MonoBehaviour
    {
        [Tooltip("창을 만들어 넣을 곳. 화면 전체를 덮는 RectTransform이어야 한다. 비우면 이 오브젝트 아래에 만든다.")]
        [SerializeField] private RectTransform _popupRoot;
        [Tooltip("띄울 수 있는 창 프리팹. 창 종류(스크립트)마다 하나씩 넣는다.")]
        [SerializeField] private UIPopup[] _popupPrefabs = Array.Empty<UIPopup>();

        // 창 종류 → 만든 창. 한 번 만든 창은 지우지 않고 껐다 켜서 다시 쓴다
        private readonly Dictionary<Type, UIPopup> _instances = new Dictionary<Type, UIPopup>();

        /// <summary>
        /// T 종류의 창을 돌려준다. 아직 없으면 닫힌 상태로 만든다. 프리팹이 등록되어 있지 않으면 null.
        /// </summary>
        /// <remarks>내용을 채운 뒤 직접 <see cref="UIPopup.Open"/>을 부르고 싶을 때 쓴다.</remarks>
        public T Get<T>() where T : UIPopup
        {
            if (_instances.TryGetValue(typeof(T), out UIPopup cached) && cached != null) return (T)cached;

            T prefab = FindPrefab<T>();
            if (prefab == null)
            {
                Debug.LogError($"{nameof(UILoader)}: {typeof(T).Name} 프리팹이 등록되지 않았습니다.", this);
                return null;
            }

            Transform parent = _popupRoot != null ? _popupRoot : transform;
            T instance = Instantiate(prefab, parent, false);
            instance.name = prefab.name;   // 이름 뒤에 붙는 "(Clone)"을 뗀다
            instance.gameObject.SetActive(false);
            _instances[typeof(T)] = instance;
            return instance;
        }

        /// <summary>T 종류의 창을 바로 연다. 프리팹이 등록되어 있지 않으면 null.</summary>
        public T Open<T>() where T : UIPopup
        {
            T popup = Get<T>();
            if (popup != null) popup.Open();
            return popup;
        }

        /// <summary>T 종류의 창이 만들어져 있으면 닫는다.</summary>
        public void Close<T>() where T : UIPopup
        {
            if (_instances.TryGetValue(typeof(T), out UIPopup popup) && popup != null) popup.Close();
        }

        private T FindPrefab<T>() where T : UIPopup
        {
            foreach (UIPopup prefab in _popupPrefabs)
            {
                // is 패턴: prefab이 T 종류이면 T로 바꾼 값을 match에 담는다
                if (prefab is T match) return match;
            }
            return null;
        }
    }
}
