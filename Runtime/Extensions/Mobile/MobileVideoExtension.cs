using UnityEngine;
using UnityEngine.UI;
using Unity.WebRTC;
using System.Collections;

namespace UnityVerseBridge.Core.Extensions.Mobile
{
    /// <summary>
    /// Quest 앱으로부터 비디오 스트림을 수신하여 화면에 표시하는 모바일 특화 확장 컴포넌트
    /// VideoStreamHandler를 보완하는 추가 기능을 제공합니다.
    /// </summary>
    [RequireComponent(typeof(UnityVerseBridgeManager))]
    public class MobileVideoExtension : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private RawImage displayImage; // 비디오를 표시할 UI RawImage
        [SerializeField] private RenderTexture receiveTexture; // Inspector에서 할당 가능
        [SerializeField] private bool autoCreateTexture = true;
        [SerializeField] private int textureWidth = 1280;
        [SerializeField] private int textureHeight = 720;
        
        [Header("Aspect Ratio")]
        [SerializeField] private bool maintainAspectRatio = true;
        [SerializeField] private AspectRatioFitter.AspectMode aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool showDebugInfo = false;
        
        private UnityVerseBridgeManager bridgeManager;
        private WebRtcManager webRtcManager;
        private VideoStreamTrack receivedVideoTrack;
        private bool isReceiving = false;
        private Coroutine updateCoroutine;
        private AspectRatioFitter aspectRatioFitter;
        
        // Debug info
        private int frameCount = 0;
        private float lastFrameTime = 0f;
        private float fps = 0f;

        void Awake()
        {
            bridgeManager = GetComponent<UnityVerseBridgeManager>();
            if (bridgeManager == null)
            {
                Debug.LogError("[MobileVideoExtension] UnityVerseBridgeManager not found!");
                enabled = false;
                return;
            }
        }

        void Start()
        {
            if (bridgeManager.BridgeMode != UnityVerseBridgeManager.BridgeMode.Client)
            {
                Debug.LogWarning("[MobileVideoExtension] This component only works in Client mode. Disabling...");
                enabled = false;
                return;
            }
            
            webRtcManager = bridgeManager.WebRtcManager;
            if (webRtcManager == null)
            {
                Debug.LogError("[MobileVideoExtension] WebRtcManager not found!");
                enabled = false;
                return;
            }

            if (displayImage == null)
            {
                Debug.LogError("[MobileVideoExtension] Display RawImage not assigned!");
                enabled = false;
                return;
            }

            // Aspect Ratio Fitter 설정
            if (maintainAspectRatio)
            {
                aspectRatioFitter = displayImage.GetComponent<AspectRatioFitter>();
                if (aspectRatioFitter == null)
                {
                    aspectRatioFitter = displayImage.gameObject.AddComponent<AspectRatioFitter>();
                }
                aspectRatioFitter.aspectMode = aspectMode;
            }

            // RenderTexture 생성 또는 확인
            if (autoCreateTexture && receiveTexture == null)
            {
                Debug.Log("[MobileVideoExtension] Creating RenderTexture for receiving video...");
                receiveTexture = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.BGRA32, RenderTextureReadWrite.sRGB);
                receiveTexture.name = "MobileReceiveTexture";
                receiveTexture.Create();
            }

            // 비디오 트랙 수신 이벤트 구독
            webRtcManager.OnVideoTrackReceived += HandleVideoTrackReceived;
            webRtcManager.OnMultiPeerVideoTrackReceived += HandleMultiPeerVideoTrackReceived;
            
            Debug.Log("[MobileVideoExtension] Initialized and waiting for video stream...");
        }

        void OnDestroy()
        {
            if (updateCoroutine != null)
            {
                StopCoroutine(updateCoroutine);
            }

            if (webRtcManager != null)
            {
                webRtcManager.OnVideoTrackReceived -= HandleVideoTrackReceived;
                webRtcManager.OnMultiPeerVideoTrackReceived -= HandleMultiPeerVideoTrackReceived;
            }

            if (receivedVideoTrack != null)
            {
                receivedVideoTrack.OnVideoReceived -= OnVideoFrameReceived;
                receivedVideoTrack.Dispose();
                receivedVideoTrack = null;
            }

            if (autoCreateTexture && receiveTexture != null && !Application.isEditor)
            {
                receiveTexture.Release();
                Destroy(receiveTexture);
            }
        }

        void OnGUI()
        {
            if (showDebugInfo && isReceiving)
            {
                GUI.Label(new Rect(10, 10, 300, 100), 
                    $"Video Stream Debug:\n" +
                    $"FPS: {fps:F1}\n" +
                    $"Resolution: {(receivedVideoTrack?.Texture != null ? $"{receivedVideoTrack.Texture.width}x{receivedVideoTrack.Texture.height}" : "N/A")}\n" +
                    $"Track State: {receivedVideoTrack?.ReadyState}");
            }
        }

        private void HandleVideoTrackReceived(MediaStreamTrack track)
        {
            var videoTrack = track as VideoStreamTrack;
            if (videoTrack == null)
            {
                Debug.LogError("[MobileVideoExtension] Received track is not a video track");
                return;
            }
            
            Debug.Log($"[MobileVideoExtension] Video track received: {videoTrack.Id}");
            SetupVideoTrack(videoTrack);
        }

        private void HandleMultiPeerVideoTrackReceived(string peerId, MediaStreamTrack track)
        {
            var videoTrack = track as VideoStreamTrack;
            if (videoTrack == null)
            {
                Debug.LogError("[MobileVideoExtension] Received track is not a video track");
                return;
            }
            
            Debug.Log($"[MobileVideoExtension] Video track received from peer {peerId}: {videoTrack.Id}");
            SetupVideoTrack(videoTrack);
        }

        private void SetupVideoTrack(VideoStreamTrack videoTrack)
        {
            receivedVideoTrack = videoTrack;
            
            // 트랙이 활성화되어 있는지 확인
            if (!receivedVideoTrack.Enabled)
            {
                receivedVideoTrack.Enabled = true;
                Debug.Log("[MobileVideoExtension] Enabled video track");
            }
            
            // Check decoder initialization status
            StartCoroutine(WaitForDecoder());
        }
        
        private IEnumerator WaitForDecoder()
        {
            Debug.Log("[MobileVideoExtension] Waiting for decoder to be ready...");
            
            // Wait a bit for decoder to initialize internally
            yield return new WaitForSeconds(0.5f);
            
            // Check if track is ready by checking ReadyState
            if (receivedVideoTrack != null && receivedVideoTrack.ReadyState == TrackState.Live)
            {
                Debug.Log("[MobileVideoExtension] Track is live and ready");
                
                // Primary method: Use OnVideoReceived event (recommended)
                receivedVideoTrack.OnVideoReceived += OnVideoFrameReceived;
                
                isReceiving = true;
                
                // Fallback method: polling (for edge cases)
                if (updateCoroutine != null)
                {
                    StopCoroutine(updateCoroutine);
                }
                updateCoroutine = StartCoroutine(UpdateVideoTexture());
            }
            else
            {
                Debug.LogError($"[MobileVideoExtension] Track not ready after wait. State: {receivedVideoTrack?.ReadyState}");
            }
        }

        private void OnVideoFrameReceived(Texture texture)
        {
            // Texture is guaranteed to be ready here
            if (texture != null)
            {
                if (debugMode)
                    Debug.Log($"[MobileVideoExtension] Video received via OnVideoReceived: {texture.width}x{texture.height}, Type: {texture.GetType().Name}");
                
                // Stop polling if it's running
                if (updateCoroutine != null)
                {
                    StopCoroutine(updateCoroutine);
                    updateCoroutine = null;
                }
                
                // Platform-specific handling
                #if UNITY_ANDROID
                // Android may need texture alignment
                if (texture.width % 16 != 0 || texture.height % 16 != 0)
                {
                    if (debugMode)
                        Debug.LogWarning("[MobileVideoExtension] Android texture alignment issue detected");
                }
                #endif
                
                // Update display
                UpdateDisplay(texture);
                
                // Update debug info
                UpdateDebugInfo();
            }
            else
            {
                Debug.LogWarning("[MobileVideoExtension] OnVideoFrameReceived called with null texture");
            }
        }

        private void UpdateDisplay(Texture texture)
        {
            // Try direct assignment first
            displayImage.texture = texture;
            
            // Update aspect ratio
            if (maintainAspectRatio && aspectRatioFitter != null)
            {
                float aspectRatio = (float)texture.width / texture.height;
                aspectRatioFitter.aspectRatio = aspectRatio;
            }
        }

        private void UpdateDebugInfo()
        {
            frameCount++;
            float currentTime = Time.time;
            float deltaTime = currentTime - lastFrameTime;
            
            if (deltaTime >= 1.0f)
            {
                fps = frameCount / deltaTime;
                frameCount = 0;
                lastFrameTime = currentTime;
            }
        }
        
        /// <summary>
        /// 폴링 방식으로 비디오 텍스처를 확인하는 폴백 메커니즘입니다.
        /// OnVideoReceived 이벤트가 발생하지 않는 경우를 대비한 보호 로직입니다.
        /// </summary>
        private IEnumerator UpdateVideoTexture()
        {
            Debug.Log("[MobileVideoExtension] Starting video texture update coroutine (fallback)...");
            
            // 초기 대기: 디코더 초기화를 위한 시간
            yield return new WaitForSeconds(0.5f);
            
            // 최대 5초 동안 텍스처 생성을 기다림
            float waitTime = 0f;
            const float maxWaitTime = 5f;
            
            while (receivedVideoTrack != null && waitTime < maxWaitTime)
            {
                if (receivedVideoTrack.Texture != null)
                {
                    Debug.Log($"[MobileVideoExtension] Video texture ready via polling! Size: {receivedVideoTrack.Texture.width}x{receivedVideoTrack.Texture.height}");
                    break;
                }
                
                waitTime += Time.deltaTime;
                yield return null;
            }
            
            if (receivedVideoTrack == null || receivedVideoTrack.Texture == null)
            {
                Debug.LogError("[MobileVideoExtension] Failed to get video texture after waiting");
                yield break;
            }
            
            // 폴링 기반 텍스처 업데이트 루프
            while (isReceiving && receivedVideoTrack != null && receivedVideoTrack.ReadyState == TrackState.Live)
            {
                if (receivedVideoTrack.Texture != null)
                {
                    if (receiveTexture != null)
                    {
                        // Graphics.Blit: GPU에서 텍스처를 효율적으로 복사
                        Graphics.Blit(receivedVideoTrack.Texture, receiveTexture);
                        displayImage.texture = receiveTexture;
                    }
                    else
                    {
                        displayImage.texture = receivedVideoTrack.Texture;
                    }
                    
                    UpdateDebugInfo();
                }
                
                yield return new WaitForEndOfFrame();
            }
            
            Debug.Log("[MobileVideoExtension] Video texture update coroutine ended");
        }

        // Public API
        public void SetDisplayImage(RawImage image)
        {
            displayImage = image;
            if (aspectRatioFitter != null)
            {
                aspectRatioFitter = displayImage.GetComponent<AspectRatioFitter>();
            }
        }

        public void SetMaintainAspectRatio(bool maintain)
        {
            maintainAspectRatio = maintain;
            if (aspectRatioFitter != null)
            {
                aspectRatioFitter.enabled = maintain;
            }
        }

        public void SetDebugMode(bool enable)
        {
            debugMode = enable;
            showDebugInfo = enable;
        }
    }
}