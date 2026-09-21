#if UNITY_STANDALONE_WIN && !UNITY_EDITOR && SPRITE_FALLBACK_CLEANUP_13_0_6
using System;
using System.Reflection;
using UnityEngine;

namespace MobilePrototype
{
    internal static class SpriteFallbackBufferCleanup
    {
        private static Action _clearFallbackBuffer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            // 2D Animation 13.0.6 creates this buffer even without SpriteSkin, but
            // normally releases it only through DeformationManager.Cleanup.
            // Use its own idempotent cleanup at player exit; never touch active rendering.
            // Keep the package method in link.xml for managed stripping / IL2CPP.
            var type = Type.GetType("UnityEngine.U2D.Animation.GpuDeformationSystem, Unity.2D.Animation.Runtime");
            var method = type?.GetMethod("ClearFallbackBuffer", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                Debug.LogWarning("Sprite fallback buffer cleanup is unavailable; recheck the 2D Animation workaround.");
                return;
            }

            if (_clearFallbackBuffer != null) Application.quitting -= _clearFallbackBuffer;
            _clearFallbackBuffer = (Action)Delegate.CreateDelegate(typeof(Action), method);
            Application.quitting += _clearFallbackBuffer;
        }
    }
}
#endif
