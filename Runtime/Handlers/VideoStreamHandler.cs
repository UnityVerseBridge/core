using UnityEngine;
using Unity.WebRTC;
using System.Collections;

namespace UnityVerseBridge.Core
{
    /// <summary>
    /// 비디오 스트리밍 기능을 처리하는 핸들러
    /// </summary>
    [RequireComponent(typeof(UnityVerseBridgeManager))]
    public class VideoStreamHandler : MonoBehaviour
    {
        [Header("Video Settings - Host")]
        [SerializeField] private Camera streamCamera;
        [SerializeField] private RenderTexture streamTexture;
        [SerializeField] private int videoWidth = 640;
        [SerializeField] private int videoHeight = 360;
        // [SerializeField] private int videoFramerate = 30; // Not used currently
        
        [Header("Video Settings - Client")]
        [SerializeField] private UnityEngine.UI.RawImage displayImage;
        [SerializeField] private RenderTexture receiveTexture;
        
        [Header("Advanced Settings")]
        [SerializeField] private bool autoSetupCamera = true;
        [SerializeField] private bool useMixedReality = false; // For Quest MR
        
        private UnityVerseBridgeManager bridgeManager;
        private WebRtcManager webRtcManager;
        private UnityVerseBridgeManager.BridgeMode mode;
        
        private VideoStreamTrack videoTrack;
        private VideoStreamTrack receivedVideoTrack;
        private bool isInitialized = false;

        public void Initialize(UnityVerseBridgeManager manager, WebRtcManager rtcManager, UnityVerseBridgeManager.BridgeMode bridgeMode)
        {
            bridgeManager = manager;
            webRtcManager = rtcManager;
            mode = bridgeMode;
            
            if (mode == UnityVerseBridgeManager.BridgeMode.Host)
            {
                SetupHostMode();
            }
            else
            {
                SetupClientMode();
            }
            
            isInitialized = true;
        }

        void OnDestroy()
        {
            Cleanup();
        }

        private void SetupHostMode()
        {
            // Auto setup camera if needed
            if (autoSetupCamera && streamCamera == null)
            {
                #if UNITY_ANDROID && !UNITY_EDITOR
                // Quest platform - find OVR camera using reflection
                try
                {
                    var ovrCameraRigType = System.Type.GetType("OVRCameraRig, Oculus.VR");
                    if (ovrCameraRigType != null)
                    {
                        var cameraRig = FindFirstObjectByType(ovrCameraRigType);
                        if (cameraRig != null)
                        {
                            if (useMixedReality)
                            {
                                var trackingSpaceProp = ovrCameraRigType.GetProperty("trackingSpace");
                                if (trackingSpaceProp != null)
                                {
                                    var trackingSpace = trackingSpaceProp.GetValue(cameraRig) as Transform;
                                    if (trackingSpace != null)
                                    {
                                        streamCamera = trackingSpace.GetComponentInChildren<Camera>();
                                    }
                                }
                            }
                            
                            if (streamCamera == null)
                            {
                                var centerEyeAnchorProp = ovrCameraRigType.GetProperty("centerEyeAnchor");
                                if (centerEyeAnchorProp != null)
                                {
                                    var centerEyeTransform = centerEyeAnchorProp.GetValue(cameraRig) as Transform;
                                    if (centerEyeTransform != null)
                                    {
                                        streamCamera = centerEyeTransform.GetComponent<Camera>();
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[VideoStreamHandler] Failed to find OVRCameraRig: {e.Message}");
                }
                #else
                streamCamera = Camera.main;
                #endif
            }

            // Create render texture if not assigned
            if (streamTexture == null)
            {
                streamTexture = new RenderTexture(videoWidth, videoHeight, 24, RenderTextureFormat.BGRA32);
                streamTexture.name = "UnityVerseBridge_StreamTexture";
                streamTexture.Create();
            }

            // Assign render texture to camera
            if (streamCamera != null && streamCamera.targetTexture == null)
            {
                streamCamera.targetTexture = streamTexture;
            }

            // Subscribe to events
            webRtcManager.OnWebRtcConnected += OnHostConnected;
            webRtcManager.OnWebRtcDisconnected += OnHostDisconnected;
        }

        private void SetupClientMode()
        {
            // Create receive texture if not assigned
            if (receiveTexture == null)
            {
                receiveTexture = new RenderTexture(videoWidth, videoHeight, 24, RenderTextureFormat.BGRA32);
                receiveTexture.name = "UnityVerseBridge_ReceiveTexture";
                receiveTexture.Create();
            }

            // Subscribe to events
            webRtcManager.OnVideoTrackReceived += OnVideoTrackReceived;
            webRtcManager.OnWebRtcDisconnected += OnClientDisconnected;
            
            // For multi-peer mode
            webRtcManager.OnMultiPeerVideoTrackReceived += OnMultiPeerVideoTrackReceived;
        }

        private void OnHostConnected()
        {
            if (streamTexture != null)
            {
                StartCoroutine(CreateAndAddVideoTrack());
            }
        }

        private IEnumerator CreateAndAddVideoTrack()
        {
            // Wait a frame to ensure texture is ready
            yield return new WaitForEndOfFrame();
            
            // Create video track from render texture
            videoTrack = new VideoStreamTrack(streamTexture);
            videoTrack.Enabled = true;
            
            // Add to WebRTC manager
            webRtcManager.AddVideoTrack(videoTrack);
            
            Debug.Log("[VideoStreamHandler] Video track added to stream");
        }

        private void OnVideoTrackReceived(MediaStreamTrack track)
        {
            var videoTrack = track as VideoStreamTrack;
            if (videoTrack == null) return;
            
            HandleReceivedVideoTrack(videoTrack);
        }

        private void OnMultiPeerVideoTrackReceived(string peerId, MediaStreamTrack track)
        {
            var videoTrack = track as VideoStreamTrack;
            if (videoTrack == null) return;
            
            Debug.Log($"[VideoStreamHandler] Video track received from peer: {peerId}");
            HandleReceivedVideoTrack(videoTrack);
        }

        private void HandleReceivedVideoTrack(VideoStreamTrack videoTrack)
        {
            receivedVideoTrack = videoTrack;
            receivedVideoTrack.Enabled = true;
            
            // Setup video display
            if (displayImage != null)
            {
                StartCoroutine(UpdateVideoDisplay());
            }
            
            Debug.Log("[VideoStreamHandler] Video track received and enabled");
        }

        private IEnumerator UpdateVideoDisplay()
        {
            // Wait for decoder initialization
            yield return new WaitForSeconds(0.5f);
            
            while (receivedVideoTrack != null && receivedVideoTrack.ReadyState == TrackState.Live)
            {
                if (receivedVideoTrack.Texture != null)
                {
                    if (receiveTexture != null)
                    {
                        Graphics.Blit(receivedVideoTrack.Texture, receiveTexture);
                        displayImage.texture = receiveTexture;
                    }
                    else
                    {
                        displayImage.texture = receivedVideoTrack.Texture;
                    }
                }
                
                yield return new WaitForEndOfFrame();
            }
        }

        private void OnHostDisconnected()
        {
            if (videoTrack != null)
            {
                videoTrack.Dispose();
                videoTrack = null;
            }
        }

        private void OnClientDisconnected()
        {
            if (receivedVideoTrack != null)
            {
                receivedVideoTrack.Dispose();
                receivedVideoTrack = null;
            }
            
            if (displayImage != null)
            {
                displayImage.texture = null;
            }
        }

        private void Cleanup()
        {
            if (!isInitialized) return;
            
            // Unsubscribe events
            if (webRtcManager != null)
            {
                webRtcManager.OnWebRtcConnected -= OnHostConnected;
                webRtcManager.OnWebRtcDisconnected -= OnHostDisconnected;
                webRtcManager.OnVideoTrackReceived -= OnVideoTrackReceived;
                webRtcManager.OnWebRtcDisconnected -= OnClientDisconnected;
            }
            
            if (webRtcManager != null)
            {
                webRtcManager.OnMultiPeerVideoTrackReceived -= OnMultiPeerVideoTrackReceived;
            }
            
            // Cleanup resources
            if (videoTrack != null)
            {
                videoTrack.Dispose();
                videoTrack = null;
            }
            
            if (receivedVideoTrack != null)
            {
                receivedVideoTrack.Dispose();
                receivedVideoTrack = null;
            }
            
            // Don't destroy render textures if they were assigned in inspector
            if (mode == UnityVerseBridgeManager.BridgeMode.Host && streamCamera != null)
            {
                streamCamera.targetTexture = null;
            }
        }

        #region Public API
        /// <summary>
        /// 스트림 카메라 변경
        /// </summary>
        public void SetStreamCamera(Camera camera)
        {
            if (mode != UnityVerseBridgeManager.BridgeMode.Host)
            {
                Debug.LogWarning("[VideoStreamHandler] SetStreamCamera is only available in Host mode");
                return;
            }
            
            streamCamera = camera;
            if (streamTexture != null)
            {
                streamCamera.targetTexture = streamTexture;
            }
        }

        /// <summary>
        /// 비디오 해상도 변경
        /// </summary>
        public void SetVideoResolution(int width, int height)
        {
            videoWidth = width;
            videoHeight = height;
            
            // Recreate textures with new resolution
            // TODO: Implement texture recreation
        }

        /// <summary>
        /// Mixed Reality 모드 토글
        /// </summary>
        public void SetMixedRealityMode(bool enabled)
        {
            if (mode != UnityVerseBridgeManager.BridgeMode.Host)
            {
                Debug.LogWarning("[VideoStreamHandler] SetMixedRealityMode is only available in Host mode");
                return;
            }
            
            useMixedReality = enabled;
            // TODO: Switch camera based on MR mode
        }
        #endregion
    }
}