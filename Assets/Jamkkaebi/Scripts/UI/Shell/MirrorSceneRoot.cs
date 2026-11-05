using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Jamkkaebi.Scripts.UI.Shell
{
    // Keep all scene content under this root. A pause deactivates this root only;
    // Time.timeScale and the other scenes are deliberately unaffected.
    public class MirrorSceneRoot : MonoBehaviour
    {
        public Camera sceneCamera;
        public GraphicRaycaster[] uiRaycasters;
        public int RenderLayer { get; private set; }
        public bool IsPaused => !gameObject.activeSelf;
        public bool NavigationLocked { get; private set; }
        /// <summary>콘텐츠의 전용 화면이 열려 있는 동안 공통 탭 이동을 잠급니다.</summary>
        public void SetNavigationLocked(bool locked) => NavigationLocked = locked;
        public void Configure(int layer, RenderTexture target)
        {
            RenderLayer = layer;
            SetLayer(transform, layer);
            sceneCamera.cullingMask = 1 << layer;
            sceneCamera.targetTexture = target;
            sceneCamera.enabled = false;
            foreach (var listener in GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            uiRaycasters = GetComponentsInChildren<GraphicRaycaster>(true);
            // Only the mirror input bridge may raycast these canvases.
            foreach (var raycaster in uiRaycasters) raycaster.enabled = false;
        }
        public void RegisterSpawnedObject(GameObject obj)
        {
            obj.transform.SetParent(transform, true);
            SetLayer(obj.transform, RenderLayer);
        }
        public static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayer(child, layer);
        }
        public void SetPaused(bool paused) => gameObject.SetActive(!paused);
        public void Simulate(float step)
        {
            var physics = gameObject.scene.GetPhysicsScene2D();
            if (!IsPaused && physics.IsValid() && physics != Physics2D.defaultPhysicsScene) physics.Simulate(step);
        }
    }
}
