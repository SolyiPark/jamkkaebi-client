using System.Collections.Generic;
using System.Reflection;
using Jamkkaebi.Scripts.Gameplay.Minigame.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Jamkkaebi.Tests.EditMode
{
    public class StandaloneEventSystemTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _createdObjects)
            {
                if (o != null) Object.DestroyImmediate(o);
            }

            _createdObjects.Clear();
        }

        private GameObject CreateEventSystem(string name, bool active)
        {
            GameObject obj = new GameObject(name);
            obj.SetActive(active);
            obj.AddComponent<EventSystem>();
            _createdObjects.Add(obj);
            return obj;
        }

        private StandaloneEventSystem CreateBootstrap(GameObject eventSystem)
        {
            GameObject obj = new GameObject("StandaloneEventSystem");
            _createdObjects.Add(obj);
            StandaloneEventSystem bootstrap = obj.AddComponent<StandaloneEventSystem>();
            SetPrivateField(bootstrap, "_eventSystem", eventSystem);
            return bootstrap;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"{target.GetType().Name}에 '{fieldName}' 필드가 없습니다.");
            field.SetValue(target, value);
        }

        // EditMode에서는 Awake가 자동으로 불리지 않으므로 직접 부른다.
        private static void InvokeAwake(StandaloneEventSystem bootstrap)
        {
            MethodInfo awake = typeof(StandaloneEventSystem).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(awake, "StandaloneEventSystem에 Awake가 없습니다.");
            awake.Invoke(bootstrap, null);
        }

        [Test]
        public void Awake_NoOtherEventSystem_ActivatesSceneEventSystem()
        {
            // Arrange: 미니게임 씬 단독 Play
            GameObject sceneEvents = CreateEventSystem("MinigameEventSystem", false);
            StandaloneEventSystem bootstrap = CreateBootstrap(sceneEvents);

            // Act
            InvokeAwake(bootstrap);

            // Assert
            Assert.IsTrue(sceneEvents.activeSelf);
        }

        [Test]
        public void Awake_ShellEventSystemActive_KeepsSceneEventSystemInactive()
        {
            // Arrange: 셸 위에 추가 로드
            CreateEventSystem("ShellEventSystem", true);
            GameObject sceneEvents = CreateEventSystem("MinigameEventSystem", false);
            StandaloneEventSystem bootstrap = CreateBootstrap(sceneEvents);

            // Act
            InvokeAwake(bootstrap);

            // Assert
            Assert.IsFalse(sceneEvents.activeSelf);
        }

        [Test]
        public void Awake_OtherEventSystemOnInactiveObject_ActivatesSceneEventSystem()
        {
            // Arrange: 꺼진 오브젝트의 EventSystem은 입력을 받지 않으므로 없는 것으로 본다
            CreateEventSystem("InactiveEventSystem", false);
            GameObject sceneEvents = CreateEventSystem("MinigameEventSystem", false);
            StandaloneEventSystem bootstrap = CreateBootstrap(sceneEvents);

            // Act
            InvokeAwake(bootstrap);

            // Assert
            Assert.IsTrue(sceneEvents.activeSelf);
        }

        [Test]
        public void Awake_OtherEventSystemComponentDisabled_ActivatesSceneEventSystem()
        {
            // Arrange: 컴포넌트만 꺼진 EventSystem도 없는 것으로 본다
            GameObject other = CreateEventSystem("DisabledEventSystem", true);
            other.GetComponent<EventSystem>().enabled = false;
            GameObject sceneEvents = CreateEventSystem("MinigameEventSystem", false);
            StandaloneEventSystem bootstrap = CreateBootstrap(sceneEvents);

            // Act
            InvokeAwake(bootstrap);

            // Assert
            Assert.IsTrue(sceneEvents.activeSelf);
        }
    }
}
