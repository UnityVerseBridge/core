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
        [SerializeField] private Vector2Int streamResolution = new Vector2Int(640, 360);
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
        
        // Debug mode from UnityVerseBridgeManager
        private bool IsDebugEnabled => debugMode || (bridgeManager != null && bridgeManager.ShowDebugUI);
        
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
            Debug.Log("[QuestVideoExtension] Awake called");
            
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
            
            Debug.Log($"[QuestVideoExtension] Found UnityVerseBridgeManager: {bridgeManager.name}");
        }

        void Start()
        {
            Debug.Log("[QuestVideoExtension] Start called");
            
            // Wait for initialization - check mode after initialization
            StartCoroutine(WaitForInitialization());

            // Use camera from UnityVerseBridgeManager if available
            if (streamCamera == null && bridgeManager.QuestStreamCamera != null)
            {
                streamCamera = bridgeManager.QuestStreamCamera;
                if (IsDebugEnabled) Debug.Log("[QuestVideoExtension] Using camera from UnityVerseBridgeManager");
            }
            
            // Find Quest camera if still not assigned
            if (streamCamera == null)
            {
#if UNITY_ANDROID && QUEST_SUPPORT && !UNITY_EDITOR
                cameraRig = FindFirstObjectByType<OVRNamespace.OVRCameraRig>();
                if (cameraRig != null)
                {
                    streamCamera = cameraRig.centerEyeAnchor.GetComponent<Camera>();
                    if (IsDebugEnabled) Debug.Log("[QuestVideoExtension] Found OVR camera");
                }
#endif
                
                if (streamCamera == null)
                {
                    streamCamera = Camera.main;
                    
#if UNITY_EDITOR
                    // In Editor, create a camera if none exists
                    if (streamCamera == null)
                    {
                        GameObject cameraObj = new GameObject("Editor Stream Camera");
                        streamCamera = cameraObj.AddComponent<Camera>();
                        streamCamera.tag = "MainCamera";
                        cameraObj.transform.position = new Vector3(0, 1.6f, 0);
                        if (IsDebugEnabled) Debug.Log("[QuestVideoExtension] Created editor camera for streaming");
                        
                        // Configure camera for VR-like view
                        streamCamera.fieldOfView = 90f;
                        streamCamera.nearClipPlane = 0.01f;
                        streamCamera.farClipPlane = 1000f;
                        
                        // Add simple camera controller for testing
                        // Note: SimpleEditorCameraController is in Editor assembly, so we try to add it dynamically
                        try
                        {
                            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                            foreach (var assembly in assemblies)
                            {
                                var controllerType = assembly.GetType("UnityVerseBridge.Core.Utils.SimpleEditorCameraController");
                                if (controllerType != null)
                                {
                                    cameraObj.AddComponent(controllerType);
                                    if (IsDebugEnabled) Debug.Log("[QuestVideoExtension] Added SimpleEditorCameraController for Editor testing");
                                    break;
                                }
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"[QuestVideoExtension] Could not add camera controller: {e.Message}");
                        }
                    }
#endif
                }
            }

            // Emergency fallback for Quest builds
            if (streamCamera == null && Application.platform == RuntimePlatform.Android)
            {
                if (IsDebugEnabled) Debug.LogWarning("[QuestVideoExtension] No camera found! Attempting emergency fallback...");
                
                // Try to find any active camera
                Camera[] allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if (IsDebugEnabled) Debug.Log($"[QuestVideoExtension] Found {allCameras.Length} cameras in scene");
                
                foreach (var cam in allCameras)
                {
                    if (cam.enabled && cam.gameObject.activeInHierarchy)
                    {
                        streamCamera = cam;
                        if (IsDebugEnabled) Debug.LogWarning($"[QuestVideoExtension] Using emergency fallback camera: {cam.name} (tag: {cam.tag})");
                        break;
                    }
                }
            }
            
            if (streamCamera == null)
            {
                Debug.LogError("[QuestVideoExtension] CRITICAL: No camera found for streaming! Check your scene setup.");
                Debug.LogError("[QuestVideoExtension] Make sure you have either:");
                Debug.LogError("  1. OVRCameraRig in your scene (for Quest)");
                Debug.LogError("  2. A Camera tagged as 'MainCamera'");
                Debug.LogError("  3. Any active Camera component");
                enabled = false;
                return;
            }
            
            // Use RenderTexture from UnityVerseBridgeManager if available
            if (renderTexture == null && bridgeManager.QuestStreamTexture != null)
            {
                renderTexture = bridgeManager.QuestStreamTexture;
                autoCreateRenderTexture = false; // Don't auto-create if provided
                if (IsDebugEnabled) Debug.Log("[QuestVideoExtension] Using RenderTexture from UnityVerseBridgeManager");
            }

            SetupRenderTexture();
            SetupPassthrough();
        }

        private System.Collections.IEnumerator WaitForInitialization()
        {
            Debug.Log("[QuestVideoExtension] WaitForInitialization started");
            
            // Wait for UnityVerseBridgeManager to be initialized
            int waitFrames = 0;
            while (!bridgeManager.IsInitialized)
            {
                if (waitFrames % 60 == 0) // Log every second
                {
                    Debug.Log($"[QuestVideoExtension] Waiting for initialization... (frame {waitFrames})");
                }
                waitFrames++;
                yield return null;
            }
            
            Debug.Log($"[QuestVideoExtension] Manager initialized. Mode: {bridgeManager.Mode}, Role: {bridgeManager.Role}");
            
            // Check mode after initialization
            if (bridgeManager.Mode != UnityVerseBridgeManager.BridgeMode.Host)
            {
                Debug.LogWarning($"[QuestVideoExtension] This component only works in Host mode. Current mode: {bridgeManager.Mode}. Disabling...");
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
            // Don't start streaming immediately on signaling connected
            // Wait for PeerConnection to be properly initialized
            webRtcManager.OnSignalingConnected += OnSignalingConnectedHandler;
            webRtcManager.OnSignalingDisconnected += StopStreaming;
            webRtcManager.OnPeerConnected += HandlePeerConnected;
            webRtcManager.OnPeerDisconnected += HandlePeerDisconnected;
            
            Debug.Log("[QuestVideoExtension] Initialized successfully");
        }

        void OnDestroy()
        {
            StopStreaming();
            
            if (webRtcManager != null)
            {
                webRtcManager.OnSignalingConnected -= OnSignalingConnectedHandler;
                webRtcManager.OnSignalingDisconnected -= StopStreaming;
                webRtcManager.OnPeerConnected -= HandlePeerConnected;
                webRtcManager.OnPeerDisconnected -= HandlePeerDisconnected;
                webRtcManager.OnWebRtcConnected -= OnWebRtcConnectedAddTrack;
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
                
                if (IsDebugEnabled)
                {
                    Debug.Log($"[QuestVideoExtension] Created RenderTexture: {streamResolution.x}x{streamResolution.y}, Format: {renderTexture.format}");
                    
                    // Log VR camera info
                    if (streamCamera != null)
                    {
                        Debug.Log($"[QuestVideoExtension] Stream Camera: {streamCamera.name}");
                        Debug.Log($"[QuestVideoExtension] Camera Target Display: {streamCamera.targetDisplay}");
                        Debug.Log($"[QuestVideoExtension] Camera Target Texture: {streamCamera.targetTexture}");
                        Debug.Log($"[QuestVideoExtension] Camera Pixel Rect: {streamCamera.pixelRect}");
                        
                        // Check if this is a VR camera
                        if (streamCamera.stereoEnabled)
                        {
                            Debug.Log($"[QuestVideoExtension] VR Stereo Mode: {streamCamera.stereoTargetEye}");
                            Debug.Log($"[QuestVideoExtension] Stereo Separation: {streamCamera.stereoSeparation}");
                        }
                    }
                }
            }
        }

        private void SetupPassthrough()
        {
#if UNITY_ANDROID && QUEST_SUPPORT && !UNITY_EDITOR
            if (capturePassthrough)
            {
                // Find passthrough layer
                passthroughLayer = FindFirstObjectByType<OVRNamespace.OVRPassthroughLayer>();
                
                // Enable passthrough in OVRManager
                if (OVRNamespace.OVRManager.instance != null)
                {
                    OVRNamespace.OVRManager.instance.isInsightPassthroughEnabled = true;
                    if (IsDebugEnabled) Debug.Log("[QuestVideoExtension] Passthrough enabled");
                }
            }
#elif UNITY_EDITOR
            if (capturePassthrough)
            {
                if (IsDebugEnabled) Debug.Log("[QuestVideoExtension] Passthrough not available in Unity Editor");
            }
#endif
        }

        private void OnSignalingConnectedHandler()
        {
            // Start video streaming immediately so it's ready when peer connects
            Debug.Log("[QuestVideoExtension] Signaling connected, preparing video stream...");
            StartCoroutine(PrepareVideoStream());
        }
        
        private IEnumerator PrepareVideoStream()
        {
            // Wait a bit to ensure render texture and camera are ready
            yield return new WaitForSeconds(0.5f);
            
            // Start streaming immediately to prepare the video track
            StartStreaming();
            
            // The video track will be available when PeerConnection is created
            Debug.Log("[QuestVideoExtension] Video stream prepared and ready for peer connections");
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

#if UNITY_ANDROID && QUEST_SUPPORT && !UNITY_EDITOR
                // Enable passthrough layer
                if (capturePassthrough && passthroughLayer != null)
                {
                    passthroughLayer.enabled = true;
                }
#endif

                // Create video stream track
                videoStreamTrack = new VideoStreamTrack(renderTexture);
                Debug.Log($"[QuestVideoExtension] Created VideoStreamTrack - Enabled: {videoStreamTrack.Enabled}, ID: {videoStreamTrack.Id}");
                if (IsDebugEnabled)
                {
                    Debug.Log($"[QuestVideoExtension] RenderTexture Resolution: {renderTexture.width}x{renderTexture.height}");
                    Debug.Log($"[QuestVideoExtension] RenderTexture Format: {renderTexture.format}");
                    Debug.Log($"[QuestVideoExtension] RenderTexture Depth: {renderTexture.depth}");
                }
                
                // Store the track for later use when peer connection is ready
                var peerState = webRtcManager.GetPeerConnectionState();
                if (IsDebugEnabled) Debug.Log($"[QuestVideoExtension] Current PeerConnection state: {peerState}");
                
                if (peerState == Unity.WebRTC.RTCPeerConnectionState.Closed || peerState == Unity.WebRTC.RTCPeerConnectionState.New)
                {
                    if (IsDebugEnabled) Debug.Log("[QuestVideoExtension] PeerConnection not ready yet, storing video track for later");
                    // Subscribe to WebRTC connected event to add track when ready
                    webRtcManager.OnWebRtcConnected += OnWebRtcConnectedAddTrack;
                }
                else
                {
                    // Add track immediately if peer connection exists
                    webRtcManager.AddVideoTrack(videoStreamTrack);
                    if (IsDebugEnabled) Debug.Log("[QuestVideoExtension] Added video track to existing PeerConnection");
                }
                
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
        
        private void OnWebRtcConnectedAddTrack()
        {
            if (videoStreamTrack != null && webRtcManager != null)
            {
                if (IsDebugEnabled) Debug.Log("[QuestVideoExtension] WebRTC connected, will add video track after a short delay");
                StartCoroutine(AddVideoTrackWithDelay());
                // Unsubscribe after adding
                webRtcManager.OnWebRtcConnected -= OnWebRtcConnectedAddTrack;
            }
        }
        
        private IEnumerator AddVideoTrackWithDelay()
        {
            // Wait a bit to ensure the initial connection is fully established
            yield return new WaitForSeconds(1.0f);
            
            if (videoStreamTrack != null && webRtcManager != null)
            {
                if (IsDebugEnabled) Debug.Log("[QuestVideoExtension] Adding video track to PeerConnection now");
                webRtcManager.AddVideoTrack(videoStreamTrack);
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

            if (IsDebugEnabled) Debug.Log("[QuestVideoExtension] Streaming stopped");
        }

        private IEnumerator CaptureFrames()
        {
            var wait = new WaitForEndOfFrame();
            float lastQualityCheck = 0f;
            int frameCount = 0;
            
            if (IsDebugEnabled)
            {
                Debug.Log($"[QuestVideoExtension] Starting capture - Camera: {streamCamera?.name}, RT: {renderTexture}");
                if (streamCamera != null)
                {
                    Debug.Log($"[QuestVideoExtension] Camera Resolution: {streamCamera.pixelWidth}x{streamCamera.pixelHeight}");
                    Debug.Log($"[QuestVideoExtension] Camera FOV: {streamCamera.fieldOfView}");
                    Debug.Log($"[QuestVideoExtension] Camera Near/Far: {streamCamera.nearClipPlane}/{streamCamera.farClipPlane}");
                }
            }
            
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
                    
                    // Log periodically when debug is enabled
                    if (IsDebugEnabled && frameCount % 60 == 0)
                    {
                        Debug.Log($"[QuestVideoExtension] Capturing frame {frameCount}, VideoTrack enabled: {videoStreamTrack?.Enabled}");
                        // Log texture scaling info
                        if (streamCamera != null && renderTexture != null)
                        {
                            float cameraAspect = (float)streamCamera.pixelWidth / streamCamera.pixelHeight;
                            float textureAspect = (float)renderTexture.width / renderTexture.height;
                            Debug.Log($"[QuestVideoExtension] Camera Aspect: {cameraAspect:F2}, Texture Aspect: {textureAspect:F2}");
                        }
                    }
                    frameCount++;
                }
                else
                {
                    if (IsDebugEnabled) Debug.LogWarning($"[QuestVideoExtension] Missing capture components - Camera: {streamCamera != null}, RT: {renderTexture != null}");
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
            if (IsDebugEnabled) Debug.Log($"[QuestVideoExtension] Peer connected: {peerId}, Total peers: {currentPeerCount}");
            
            if (adjustQualityByPeerCount)
            {
                AdjustStreamingQuality();
            }
        }

        private void HandlePeerDisconnected(string peerId)
        {
            currentPeerCount = webRtcManager.ActiveConnectionsCount;
            if (IsDebugEnabled) Debug.Log($"[QuestVideoExtension] Peer disconnected: {peerId}, Total peers: {currentPeerCount}");
            
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
                if (IsDebugEnabled) Debug.Log($"[QuestVideoExtension] Adjusting resolution: {newResolution.x}x{newResolution.y} for {currentPeerCount} peers");
                
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
#if UNITY_ANDROID && QUEST_SUPPORT && !UNITY_EDITOR
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
            if (!IsDebugEnabled) return;

            GUI.Label(new Rect(10, 10, 300, 20), $"Quest Streaming: {(isStreaming ? "ON" : "OFF")}");
            GUI.Label(new Rect(10, 30, 300, 20), $"Connected Peers: {currentPeerCount}");
            GUI.Label(new Rect(10, 50, 300, 20), $"Resolution: {renderTexture?.width}x{renderTexture?.height}");
#if UNITY_ANDROID && QUEST_SUPPORT && !UNITY_EDITOR
            GUI.Label(new Rect(10, 70, 300, 20), $"Passthrough: {(capturePassthrough && passthroughLayer?.enabled == true ? "ON" : "OFF")}");
#elif UNITY_EDITOR
            GUI.Label(new Rect(10, 70, 300, 20), "Platform: Unity Editor (XR Disabled)");
#endif
        }
    }
}