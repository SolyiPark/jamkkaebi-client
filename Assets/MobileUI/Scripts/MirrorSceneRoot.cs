using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MobilePrototype
{
    // Keep all scene content under this root. A pause deactivates this root only;
    // Time.timeScale and the other scenes are deliberately unaffected.
    public class MirrorSceneRoot : MonoBehaviour
    {
        public Camera sceneCamera;
        public GraphicRaycaster[] uiRaycasters;
        public int RenderLayer { get; private set; }
        public bool IsPaused => !gameObject.activeSelf;
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
