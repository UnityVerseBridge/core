using UnityEngine;
using System.Collections;
#if UNITY_XR_MANAGEMENT
using UnityEngine.XR.Management;
#endif

namespace UnityVerseBridge.Core.Utils
{
    /// <summary>
    /// Monitors XR session state and prevents unexpected shutdowns in Unity Editor
    /// </summary>
    public class XRSessionMonitor : MonoBehaviour
    {
        #if UNITY_EDITOR && UNITY_XR_MANAGEMENT
        private bool wasXRActive = false;
        private float xrCheckInterval = 1f;
        #endif
        
        void Start()
        {
            #if UNITY_EDITOR && UNITY_XR_MANAGEMENT
            StartCoroutine(MonitorXRSession());
            #endif
        }
        
        #if UNITY_EDITOR && UNITY_XR_MANAGEMENT
        private IEnumerator MonitorXRSession()
        {
            while (true)
            {
                yield return new WaitForSeconds(xrCheckInterval);
                
                bool isXRActive = XRGeneralSettings.Instance != null && 
                                 XRGeneralSettings.Instance.Manager != null && 
                                 XRGeneralSettings.Instance.Manager.isInitializationComplete;
                
                if (wasXRActive && !isXRActive)
                {
                    Debug.LogWarning("[XRSessionMonitor] XR session ended unexpectedly. This might be a Meta XR Simulator issue.");
                    
                    // Try to restart XR if it was running
                    // Note: In Unity Editor with Meta XR Simulator, we don't try to restart
                    // as it might cause issues. Instead, we just log the event.
                    if (Application.isPlaying && !Application.isEditor)
                    {
                        Debug.Log("[XRSessionMonitor] Attempting to restart XR...");
                        yield return TryRestartXR();
                    }
                    else if (Application.isEditor)
                    {
                        Debug.LogWarning("[XRSessionMonitor] XR session ended in Editor. This is expected with Meta XR Simulator when not in VR preview mode.");
                        // Try to detect if Meta XR Simulator is being used
                        CheckMetaXRSimulator();
                    }
                }
                
                wasXRActive = isXRActive;
            }
        }
        
        private IEnumerator TryRestartXR()
        {
            if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
            {
                // Wait a bit before restarting
                yield return new WaitForSeconds(2f);
                
                try
                {
                    XRGeneralSettings.Instance.Manager.InitializeLoaderSync();
                    if (XRGeneralSettings.Instance.Manager.activeLoader != null)
                    {
                        XRGeneralSettings.Instance.Manager.StartSubsystems();
                        Debug.Log("[XRSessionMonitor] XR restarted successfully");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[XRSessionMonitor] Failed to restart XR: {e.Message}");
                }
            }
        }
        #endif
        
        void OnApplicationPause(bool pauseStatus)
        {
            #if UNITY_EDITOR
            if (pauseStatus)
            {
                Debug.Log("[XRSessionMonitor] Application paused - XR session might be affected");
            }
            #endif
        }
        
        void OnApplicationFocus(bool hasFocus)
        {
            #if UNITY_EDITOR
            if (!hasFocus)
            {
                Debug.Log("[XRSessionMonitor] Application lost focus - XR session might be affected");
            }
            #endif
        }
        
        #if UNITY_EDITOR
        private void CheckMetaXRSimulator()
        {
            // Check if Meta XR Simulator is available
            var metaSimType = System.Type.GetType("Meta.XR.Simulator.Editor.MetaXRSimulator, Meta.XR.Simulator.Editor");
            if (metaSimType != null)
            {
                Debug.Log("[XRSessionMonitor] Meta XR Simulator detected. Make sure to:");
                Debug.Log("  1. Open Window > Meta > XR Simulator");
                Debug.Log("  2. Enable 'Play Mode OpenXR Runtime' set to 'Unity Mock Runtime'");
                Debug.Log("  3. Check that OpenXR is enabled for PC platform");
            }
        }
        #endif
    }
}