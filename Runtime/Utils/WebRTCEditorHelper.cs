using UnityEngine;
using Unity.WebRTC;
using System.Collections;

namespace UnityVerseBridge.Core.Utils
{
    /// <summary>
    /// Unity Editor에서 WebRTC 실행을 돕는 헬퍼 클래스
    /// </summary>
    public class WebRTCEditorHelper : MonoBehaviour
    {
        private static WebRTCEditorHelper instance;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Initialize()
        {
            if (instance != null) return;
            
            GameObject helperObject = new GameObject("[WebRTCEditorHelper]");
            DontDestroyOnLoad(helperObject);
            instance = helperObject.AddComponent<WebRTCEditorHelper>();
        }
        
        void Start()
        {
            // Ensure WebRTC is properly initialized in Editor
            if (Application.isEditor)
            {
                StartCoroutine(EnsureWebRTCInitialized());
            }
        }
        
        private IEnumerator EnsureWebRTCInitialized()
        {
            Debug.Log("[WebRTCEditorHelper] Ensuring WebRTC is initialized in Unity Editor...");
            
            // Wait a frame to ensure all systems are ready
            yield return null;
            
            // Start WebRTC Update coroutine if not already running
            StartCoroutine(WebRTC.Update());
            
            Debug.Log("[WebRTCEditorHelper] WebRTC initialization helper started");
            
            // Additional diagnostics in Editor
            #if UNITY_EDITOR
            yield return new WaitForSeconds(1f);
            
            // Log WebRTC capabilities
            try
            {
                // GetEncoderType may not be available in all Unity WebRTC versions
                var encoderTypeMethod = typeof(WebRTC).GetMethod("GetEncoderType");
                if (encoderTypeMethod != null)
                {
                    var encoderType = encoderTypeMethod.Invoke(null, null);
                    Debug.Log($"[WebRTCEditorHelper] WebRTC Encoder Type: {encoderType}");
                }
                else
                {
                    Debug.Log("[WebRTCEditorHelper] WebRTC encoder type info not available in this version");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[WebRTCEditorHelper] Could not get encoder type: {e.Message}");
            }
            
            // Check if hardware encoding is available
            try
            {
                var capabilities = RTCRtpSender.GetCapabilities(TrackKind.Video);
                if (capabilities != null && capabilities.codecs != null)
                {
                    Debug.Log($"[WebRTCEditorHelper] Available video codecs: {capabilities.codecs.Length}");
                    foreach (var codec in capabilities.codecs)
                    {
                        Debug.Log($"  - {codec.mimeType}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[WebRTCEditorHelper] Could not get codec capabilities: {e.Message}");
            }
            #endif
        }
        
        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}