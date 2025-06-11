using System;
using System.Collections;
using UnityEngine;
using Unity.WebRTC;
using UnityVerseBridge.Core;

#if UNITY_ANDROID || UNITY_EDITOR
#if QUEST_SUPPORT
using OVRNamespace = global::OVR;
#endif
#endif

namespace UnityVerseBridge.Core.Extensions.Quest
{
    /// <summary>
    /// Quest VR에서 카메라 비디오를 스트리밍하는 확장 컴포넌트
    /// VideoStreamHandler를 보완하는 Quest 특화 기능을 제공합니다.
    /// </summary>
    public class QuestVideoExtension : MonoBehaviour
    {
        [Header("Camera Settings")]
        [SerializeField] private Camera streamCamera;
        [SerializeField] private RenderTexture renderTexture;
        
        [Header("Stream Settings")]
        [SerializeField] private Vector2Int streamResolution = new Vector2Int(1280, 720);
        [SerializeField] private bool autoCreateRenderTexture = true;
        
        [Header("Quest MR Settings")]
        [SerializeField] private bool capturePassthrough = true;
        [SerializeField] private LayerMask cullingMask = -1;
        
        [Header("Performance")]
        [SerializeField] private bool useAdaptiveResolution = true;
        [SerializeField] private Vector2Int minResolution = new Vector2Int(640, 360);
        [SerializeField] private bool adjustQualityByPeerCount = true;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        
        private UnityVerseBridgeManager bridgeManager;
        private WebRtcManager webRtcManager;
        private VideoStreamTrack videoStreamTrack;
        private bool isStreaming = false;
        private int currentPeerCount = 0;
        
#if UNITY_ANDROID && QUEST_SUPPORT
        private OVRNamespace.OVRPassthroughLayer passthroughLayer;
        private OVRNamespace.OVRCameraRig cameraRig;
#endif

        void Awake()
        {
            // Try to find manager in parent first, then in the scene
            bridgeManager = GetComponentInParent<UnityVerseBridgeManager>();
            if (bridgeManager == null)
            {
                bridgeManager = FindFirstObjectByType<UnityVerseBridgeManager>();
            }
            
            if (bridgeManager == null)
            {
                Debug.LogError("[QuestVideoExtension] UnityVerseBridgeManager not found in parent or scene!");
                enabled = false;
                return;
            }
        }

        void Start()
        {
            // Wait for initialization - check mode after initialization
            StartCoroutine(WaitForInitialization());

            // Use camera from UnityVerseBridgeManager if available
            if (streamCamera == null && bridgeManager.QuestStreamCamera != null)
            {
                streamCamera = bridgeManager.QuestStreamCamera;
                Debug.Log("[QuestVideoExtension] Using camera from UnityVerseBridgeManager");
            }
            
            // Find Quest camera if still not assigned
            if (streamCamera == null)
            {
#if UNITY_ANDROID && QUEST_SUPPORT
                cameraRig = FindFirstObjectByType<OVRNamespace.OVRCameraRig>();
                if (cameraRig != null)
                {
                    streamCamera = cameraRig.centerEyeAnchor.GetComponent<Camera>();
                    Debug.Log("[QuestVideoExtension] Found OVR camera");
                }
#endif
                
                if (streamCamera == null)
                {
                    streamCamera = Camera.main;
                }
            }

            if (streamCamera == null)
            {
                Debug.LogError("[QuestVideoExtension] No camera found for streaming!");
                enabled = false;
                return;
            }
            
            // Use RenderTexture from UnityVerseBridgeManager if available
            if (renderTexture == null && bridgeManager.QuestStreamTexture != null)
            {
                renderTexture = bridgeManager.QuestStreamTexture;
                autoCreateRenderTexture = false; // Don't auto-create if provided
                Debug.Log("[QuestVideoExtension] Using RenderTexture from UnityVerseBridgeManager");
            }

            SetupRenderTexture();
            SetupPassthrough();
        }

        private System.Collections.IEnumerator WaitForInitialization()
        {
            // Wait for UnityVerseBridgeManager to be initialized
            while (!bridgeManager.IsInitialized)
            {
                yield return null;
            }
            
            // Check mode after initialization
            if (bridgeManager.Mode != UnityVerseBridgeManager.BridgeMode.Host)
            {
                Debug.LogWarning("[QuestVideoExtension] This component only works in Host mode. Disabling...");
                enabled = false;
                yield break;
            }
            
            // Get WebRtcManager
            webRtcManager = bridgeManager.WebRtcManager;
            if (webRtcManager == null)
            {
                Debug.LogError("[QuestVideoExtension] WebRtcManager not found after initialization!");
                enabled = false;
                yield break;
            }
            
            // Subscribe to events
            webRtcManager.OnSignalingConnected += StartStreaming;
            webRtcManager.OnSignalingDisconnected += StopStreaming;
            webRtcManager.OnPeerConnected += HandlePeerConnected;
            webRtcManager.OnPeerDisconnected += HandlePeerDisconnected;
            
            Debug.Log("[QuestVideoExtension] Initialized");
        }

        void OnDestroy()
        {
            StopStreaming();
            
            if (webRtcManager != null)
            {
                webRtcManager.OnSignalingConnected -= StartStreaming;
                webRtcManager.OnSignalingDisconnected -= StopStreaming;
                webRtcManager.OnPeerConnected -= HandlePeerConnected;
                webRtcManager.OnPeerDisconnected -= HandlePeerDisconnected;
            }

            if (autoCreateRenderTexture && renderTexture != null && !Application.isEditor)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }

        private void SetupRenderTexture()
        {
            if (autoCreateRenderTexture && renderTexture == null)
            {
                renderTexture = new RenderTexture(streamResolution.x, streamResolution.y, 24, RenderTextureFormat.BGRA32);
                renderTexture.name = "QuestStreamTexture";
                renderTexture.Create();
                Debug.Log($"[QuestVideoExtension] Created RenderTexture: {streamResolution.x}x{streamResolution.y}");
            }
        }

        private void SetupPassthrough()
        {
#if UNITY_ANDROID && QUEST_SUPPORT
            if (capturePassthrough)
            {
                // Find passthrough layer
                passthroughLayer = FindFirstObjectByType<OVRNamespace.OVRPassthroughLayer>();
                
                // Enable passthrough in OVRManager
                if (OVRNamespace.OVRManager.instance != null)
                {
                    OVRNamespace.OVRManager.instance.isInsightPassthroughEnabled = true;
                    Debug.Log("[QuestVideoExtension] Passthrough enabled");
                }
            }
#endif
        }

        private void StartStreaming()
        {
            if (isStreaming) return;

            try
            {
                // Configure camera
                if (streamCamera != null)
                {
                    streamCamera.cullingMask = cullingMask;
                }

#if UNITY_ANDROID && QUEST_SUPPORT
                // Enable passthrough layer
                if (capturePassthrough && passthroughLayer != null)
                {
                    passthroughLayer.enabled = true;
                }
#endif

                // Create video stream track
                videoStreamTrack = new VideoStreamTrack(renderTexture);
                webRtcManager.AddVideoTrack(videoStreamTrack);
                
                isStreaming = true;
                
                // Start capture coroutine
                StartCoroutine(CaptureFrames());
                
                Debug.Log("[QuestVideoExtension] Streaming started");
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestVideoExtension] Failed to start streaming: {e.Message}");
            }
        }

        private void StopStreaming()
        {
            if (!isStreaming) return;

            isStreaming = false;

            if (videoStreamTrack != null && webRtcManager != null)
            {
                webRtcManager.RemoveTrack(videoStreamTrack);
                videoStreamTrack.Dispose();
                videoStreamTrack = null;
            }

            Debug.Log("[QuestVideoExtension] Streaming stopped");
        }

        private IEnumerator CaptureFrames()
        {
            var wait = new WaitForEndOfFrame();
            float lastQualityCheck = 0f;
            
            while (isStreaming)
            {
                yield return wait;

                if (streamCamera != null && renderTexture != null)
                {
                    // Backup current target
                    var previousTarget = streamCamera.targetTexture;
                    
                    // Render to texture
                    streamCamera.targetTexture = renderTexture;
                    streamCamera.Render();
                    
                    // Restore target
                    streamCamera.targetTexture = previousTarget;
                }

                // Adaptive quality adjustment
                if (useAdaptiveResolution && Time.time - lastQualityCheck > 2f)
                {
                    AdjustStreamingQuality();
                    lastQualityCheck = Time.time;
                }
            }
        }

        private void HandlePeerConnected(string peerId)
        {
            currentPeerCount = webRtcManager.ActiveConnectionsCount;
            Debug.Log($"[QuestVideoExtension] Peer connected: {peerId}, Total peers: {currentPeerCount}");
            
            if (adjustQualityByPeerCount)
            {
                AdjustStreamingQuality();
            }
        }

        private void HandlePeerDisconnected(string peerId)
        {
            currentPeerCount = webRtcManager.ActiveConnectionsCount;
            Debug.Log($"[QuestVideoExtension] Peer disconnected: {peerId}, Total peers: {currentPeerCount}");
            
            if (adjustQualityByPeerCount)
            {
                AdjustStreamingQuality();
            }
        }

        private void AdjustStreamingQuality()
        {
            if (!isStreaming || renderTexture == null) return;

            Vector2Int newResolution = streamResolution;
            
            // Adjust resolution based on peer count
            if (currentPeerCount > 3)
            {
                newResolution = new Vector2Int(
                    Mathf.Max(minResolution.x, streamResolution.x / 2),
                    Mathf.Max(minResolution.y, streamResolution.y / 2)
                );
            }
            else if (currentPeerCount > 5)
            {
                newResolution = minResolution;
            }

            // Recreate render texture if resolution changed
            if (newResolution.x != renderTexture.width || newResolution.y != renderTexture.height)
            {
                Debug.Log($"[QuestVideoExtension] Adjusting resolution: {newResolution.x}x{newResolution.y} for {currentPeerCount} peers");
                
                var oldTexture = renderTexture;
                renderTexture = new RenderTexture(newResolution.x, newResolution.y, 24, RenderTextureFormat.BGRA32);
                renderTexture.Create();
                
                // Note: In production, you'd need to handle video track recreation
                
                oldTexture.Release();
                Destroy(oldTexture);
            }
        }

        // Public API
        public void SetStreamCamera(Camera camera)
        {
            streamCamera = camera;
        }

        public void SetStreamResolution(int width, int height)
        {
            streamResolution = new Vector2Int(width, height);
            if (isStreaming)
            {
                StopStreaming();
                SetupRenderTexture();
                StartStreaming();
            }
        }

        public void SetCapturePassthrough(bool enable)
        {
            capturePassthrough = enable;
#if UNITY_ANDROID && QUEST_SUPPORT
            if (passthroughLayer != null)
            {
                passthroughLayer.enabled = enable && isStreaming;
            }
#endif
        }

        public void SetAdaptiveQuality(bool enable)
        {
            useAdaptiveResolution = enable;
        }

        void OnGUI()
        {
            if (!debugMode) return;

            GUI.Label(new Rect(10, 10, 300, 20), $"Quest Streaming: {(isStreaming ? "ON" : "OFF")}");
            GUI.Label(new Rect(10, 30, 300, 20), $"Connected Peers: {currentPeerCount}");
            GUI.Label(new Rect(10, 50, 300, 20), $"Resolution: {renderTexture?.width}x{renderTexture?.height}");
#if UNITY_ANDROID && QUEST_SUPPORT
            GUI.Label(new Rect(10, 70, 300, 20), $"Passthrough: {(capturePassthrough && passthroughLayer?.enabled == true ? "ON" : "OFF")}");
#endif
        }
    }
}