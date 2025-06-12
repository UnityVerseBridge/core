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
            Debug.Log("[MobileVideoExtension] Awake called");
            
            // Try to find manager in parent first (for child components)
            bridgeManager = GetComponentInParent<UnityVerseBridgeManager>();
            
            // If not found in parent, search the scene
            if (bridgeManager == null)
            {
                bridgeManager = FindFirstObjectByType<UnityVerseBridgeManager>();
            }
            
            if (bridgeManager == null)
            {
                Debug.LogError("[MobileVideoExtension] UnityVerseBridgeManager not found in parent or scene!");
                enabled = false;
                return;
            }
            
            Debug.Log($"[MobileVideoExtension] Found UnityVerseBridgeManager: {bridgeManager.name}");
            
            if (bridgeManager == null)
            {
                Debug.LogError("[MobileVideoExtension] UnityVerseBridgeManager not found in parent or scene!");
                enabled = false;
                return;
            }
        }

        void Start()
        {
            StartCoroutine(WaitForInitialization());
        }

        private IEnumerator WaitForInitialization()
        {
            // Wait for UnityVerseBridgeManager to be initialized
            while (!bridgeManager.IsInitialized)
            {
                yield return null;
            }

            // Check mode after initialization
            if (bridgeManager.Mode != UnityVerseBridgeManager.BridgeMode.Client)
            {
                Debug.LogWarning("[MobileVideoExtension] This component only works in Client mode. Disabling...");
                enabled = false;
                yield break;
            }
            
            webRtcManager = bridgeManager.WebRtcManager;
            if (webRtcManager == null)
            {
                Debug.LogError("[MobileVideoExtension] WebRtcManager not found!");
                enabled = false;
                yield break;
            }

            // Use display image from UnityVerseBridgeManager if available
            if (displayImage == null && bridgeManager.MobileVideoDisplay != null)
            {
                displayImage = bridgeManager.MobileVideoDisplay;
                Debug.Log("[MobileVideoExtension] Using Video Display from UnityVerseBridgeManager");
            }
            
            if (displayImage == null)
            {
                Debug.LogError("[MobileVideoExtension] Display RawImage not assigned!");
                enabled = false;
                yield break;
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
                
                // Ensure the RawImage fills the parent completely
                RectTransform rect = displayImage.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    Debug.Log("[MobileVideoExtension] Set RawImage to fill parent container");
                }
            }

            // RenderTexture 생성 또는 확인
            if (autoCreateTexture && receiveTexture == null)
            {
                Debug.Log("[MobileVideoExtension] Creating RenderTexture for receiving video...");
                
                // Try multiple formats for better compatibility
                RenderTextureFormat[] formats = new RenderTextureFormat[] 
                {
                    RenderTextureFormat.BGRA32,
                    RenderTextureFormat.ARGB32,
                    RenderTextureFormat.RGB565,
                    RenderTextureFormat.Default
                };
                
                RenderTextureFormat selectedFormat = RenderTextureFormat.Default;
                
                foreach (var format in formats)
                {
                    if (SystemInfo.SupportsRenderTextureFormat(format))
                    {
                        selectedFormat = format;
                        Debug.Log($"[MobileVideoExtension] Selected texture format: {format}");
                        break;
                    }
                }
                
                // Create with depth buffer 0 for better mobile compatibility
                receiveTexture = new RenderTexture(textureWidth, textureHeight, 0, selectedFormat);
                receiveTexture.name = "MobileReceiveTexture";
                receiveTexture.useMipMap = false;
                receiveTexture.autoGenerateMips = false;
                receiveTexture.filterMode = FilterMode.Bilinear;
                receiveTexture.wrapMode = TextureWrapMode.Clamp;
                receiveTexture.Create();
                
                Debug.Log($"[MobileVideoExtension] RenderTexture created - Format: {selectedFormat}, Size: {textureWidth}x{textureHeight}, IsCreated: {receiveTexture.IsCreated()}");
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
            
            // 비디오 트랙 정보 로그
            Debug.Log($"[MobileVideoExtension] Video track setup - ID: {videoTrack.Id}, Enabled: {videoTrack.Enabled}, ReadyState: {videoTrack.ReadyState}");
            
            // 모바일 플랫폼에서 추가 디코더 초기화 시도
            #if UNITY_ANDROID || UNITY_IOS
            StartCoroutine(ForceDecoderInitialization());
            #else
            StartCoroutine(WaitForDecoder());
            #endif
        }
        
        private IEnumerator ForceDecoderInitialization()
        {
            Debug.Log("[MobileVideoExtension] Starting forced decoder initialization for mobile platform");
            
            // 모바일에서는 디코더 초기화를 위해 트랙을 재활성화
            receivedVideoTrack.Enabled = false;
            yield return null;
            receivedVideoTrack.Enabled = true;
            
            Debug.Log("[MobileVideoExtension] Toggled track enable state to force decoder init");
            
            // 이후 일반 디코더 대기 프로세스 진행
            yield return StartCoroutine(WaitForDecoder());
        }
        
        private IEnumerator WaitForDecoder()
        {
            Debug.Log("[MobileVideoExtension] Waiting for decoder to be ready...");
            
            // Force enable the track immediately
            if (receivedVideoTrack != null && !receivedVideoTrack.Enabled)
            {
                receivedVideoTrack.Enabled = true;
                Debug.Log("[MobileVideoExtension] Forced video track to be enabled");
            }
            
            // Try multiple decoder initialization attempts
            int maxAttempts = 5;
            float waitBetweenAttempts = 0.5f;
            
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Debug.Log($"[MobileVideoExtension] Decoder initialization attempt {attempt + 1}/{maxAttempts}");
                
                // Wait before checking
                yield return new WaitForSeconds(waitBetweenAttempts);
                
                // Force a frame update to trigger decoder
                if (receivedVideoTrack != null)
                {
                    // Try to access the texture to force initialization
                    var testTexture = receivedVideoTrack.Texture;
                    if (testTexture != null)
                    {
                        Debug.Log($"[MobileVideoExtension] Decoder initialized on attempt {attempt + 1}! Texture: {testTexture.width}x{testTexture.height}");
                        break;
                    }
                }
                
                // Increase wait time for next attempt
                waitBetweenAttempts = Mathf.Min(waitBetweenAttempts * 1.5f, 2.0f);
            }
            
            // Check if track is ready
            if (receivedVideoTrack != null && receivedVideoTrack.ReadyState == TrackState.Live)
            {
                Debug.Log($"[MobileVideoExtension] Track is live and ready - Enabled: {receivedVideoTrack.Enabled}");
                
                // Subscribe to OnVideoReceived event
                receivedVideoTrack.OnVideoReceived += OnVideoFrameReceived;
                Debug.Log("[MobileVideoExtension] Subscribed to OnVideoReceived event");
                
                isReceiving = true;
                
                // Start polling coroutine as primary method since OnVideoReceived might not fire
                if (updateCoroutine != null)
                {
                    StopCoroutine(updateCoroutine);
                }
                updateCoroutine = StartCoroutine(UpdateVideoTexture());
                Debug.Log("[MobileVideoExtension] Started texture update coroutine");
            }
            else
            {
                Debug.LogError($"[MobileVideoExtension] Track initialization failed after {maxAttempts} attempts. State: {receivedVideoTrack?.ReadyState}");
                
                // Try one more time with forced polling
                if (receivedVideoTrack != null)
                {
                    Debug.LogWarning("[MobileVideoExtension] Forcing polling mode despite track state");
                    isReceiving = true;
                    updateCoroutine = StartCoroutine(UpdateVideoTexture());
                }
            }
        }

        private void OnVideoFrameReceived(Texture texture)
        {
            // Texture is guaranteed to be ready here
            if (texture != null)
            {
                // Enhanced logging for debugging
                Debug.Log($"[MobileVideoExtension] Video received via OnVideoReceived: {texture.width}x{texture.height}, Type: {texture.GetType().Name}, " +
                    $"Format: {texture.graphicsFormat}, FilterMode: {texture.filterMode}, " +
                    $"Dimension: {texture.dimension}, MipMapBias: {texture.mipMapBias}");
                
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
                    Debug.LogWarning($"[MobileVideoExtension] Android texture alignment issue detected: {texture.width}x{texture.height}");
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
            Debug.Log($"[MobileVideoExtension] UpdateDisplay called - RenderTexture exists: {receiveTexture != null}, " +
                $"DisplayImage exists: {displayImage != null}, AutoCreateTexture: {autoCreateTexture}");
            
            // Check if we should use RenderTexture or direct assignment
            if (receiveTexture != null)
            {
                // Ensure RenderTexture is created
                if (!receiveTexture.IsCreated())
                {
                    Debug.Log("[MobileVideoExtension] RenderTexture not created, creating now...");
                    receiveTexture.Create();
                }
                
                // Use Graphics.Blit to copy texture to RenderTexture
                try
                {
                    Graphics.Blit(texture, receiveTexture);
                    displayImage.texture = receiveTexture;
                    Debug.Log($"[MobileVideoExtension] Successfully blitted to RenderTexture and assigned to display");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[MobileVideoExtension] Error during Graphics.Blit: {e.Message}");
                    // Fallback to direct assignment
                    displayImage.texture = texture;
                }
            }
            else
            {
                // Direct assignment
                displayImage.texture = texture;
                Debug.Log($"[MobileVideoExtension] Direct texture assignment to display (no RenderTexture)");
            }
            
            // Force UI update
            displayImage.SetMaterialDirty();
            
            // Ensure the display image is visible
            if (!displayImage.gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"[MobileVideoExtension] Display image GameObject is not active! Path: {GetGameObjectPath(displayImage.gameObject)}");
            }
            
            // Ensure the image has proper color (white for proper video display)
            if (displayImage.color != Color.white)
            {
                Debug.Log($"[MobileVideoExtension] Setting display image color to white (was: {displayImage.color})");
                displayImage.color = Color.white;
            }
            
            // Update aspect ratio
            if (maintainAspectRatio && aspectRatioFitter != null)
            {
                float aspectRatio = (float)texture.width / texture.height;
                aspectRatioFitter.aspectRatio = aspectRatio;
                Debug.Log($"[MobileVideoExtension] Updated aspect ratio to: {aspectRatio}");
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
            Debug.Log("[MobileVideoExtension] Starting video texture update coroutine...");
            
            // 초기 대기를 짧게 설정
            yield return new WaitForSeconds(0.2f);
            
            // 텍스처 폴링 시도
            float waitTime = 0f;
            const float maxWaitTime = 10f; // 최대 대기 시간 증가
            int frameCheckInterval = 30; // 0.5초마다 체크 (60fps 기준)
            int frameCounter = 0;
            
            Debug.Log($"[MobileVideoExtension] Starting texture polling - Track Enabled: {receivedVideoTrack?.Enabled}, ReadyState: {receivedVideoTrack?.ReadyState}");
            
            while (receivedVideoTrack != null && waitTime < maxWaitTime)
            {
                // 매 프레임마다 텍스처 체크
                var texture = receivedVideoTrack.Texture;
                if (texture != null)
                {
                    Debug.Log($"[MobileVideoExtension] Video texture detected! Size: {texture.width}x{texture.height}, Format: {texture.graphicsFormat}");
                    break;
                }
                
                // 주기적으로 트랙 상태 강제 업데이트
                if (frameCounter % frameCheckInterval == 0)
                {
                    // 트랙 활성화 재시도
                    if (!receivedVideoTrack.Enabled)
                    {
                        receivedVideoTrack.Enabled = true;
                        Debug.Log("[MobileVideoExtension] Re-enabled video track");
                    }
                    
                    Debug.Log($"[MobileVideoExtension] Still waiting for texture... {waitTime:F1}s / {maxWaitTime}s - ReadyState: {receivedVideoTrack.ReadyState}");
                }
                
                frameCounter++;
                waitTime += Time.deltaTime;
                yield return null;
            }
            
            if (receivedVideoTrack == null || receivedVideoTrack.Texture == null)
            {
                Debug.LogError("[MobileVideoExtension] Failed to get video texture after extended wait");
                Debug.LogError($"[MobileVideoExtension] Final state - Track exists: {receivedVideoTrack != null}, Texture exists: {receivedVideoTrack?.Texture != null}, ReadyState: {receivedVideoTrack?.ReadyState}");
                
                // 마지막 시도: 직접 텍스처 생성 시도
                if (receivedVideoTrack != null && receivedVideoTrack.ReadyState == TrackState.Live)
                {
                    Debug.LogWarning("[MobileVideoExtension] Attempting forced texture update despite null texture");
                    // 계속 진행하여 폴링 루프 시작
                }
                else
                {
                    yield break;
                }
            }
            
            // 폴링 기반 텍스처 업데이트 루프
            bool textureFound = false;
            while (isReceiving && receivedVideoTrack != null && receivedVideoTrack.ReadyState == TrackState.Live)
            {
                var currentTexture = receivedVideoTrack.Texture;
                
                if (currentTexture != null)
                {
                    if (!textureFound)
                    {
                        textureFound = true;
                        Debug.Log($"[MobileVideoExtension] First texture frame received! Size: {currentTexture.width}x{currentTexture.height}");
                    }
                    
                    // Log texture info periodically (every 60 frames) - only in debug mode
                    if (debugMode && Time.frameCount % 60 == 0)
                    {
                        Debug.Log($"[MobileVideoExtension] Polling: Texture - {currentTexture.width}x{currentTexture.height}, " +
                            $"Format: {currentTexture.graphicsFormat}, Type: {currentTexture.GetType().Name}");
                    }
                    
                    try
                    {
                        if (receiveTexture != null)
                        {
                            // Ensure RenderTexture is created
                            if (!receiveTexture.IsCreated())
                            {
                                receiveTexture.Create();
                                Debug.Log("[MobileVideoExtension] Recreated RenderTexture during update");
                            }
                            
                            // Graphics.Blit: GPU에서 텍스처를 효율적으로 복사
                            Graphics.Blit(currentTexture, receiveTexture);
                            displayImage.texture = receiveTexture;
                        }
                        else
                        {
                            // 직접 할당
                            displayImage.texture = currentTexture;
                        }
                        
                        // UI 강제 업데이트
                        if (displayImage != null)
                        {
                            displayImage.SetMaterialDirty();
                            displayImage.SetVerticesDirty();
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[MobileVideoExtension] Error updating display: {e.Message}");
                    }
                    
                    UpdateDebugInfo();
                }
                else if (Time.frameCount % 120 == 0) // 디버그 로그 빈도 감소
                {
                    Debug.LogWarning($"[MobileVideoExtension] Texture is null in update loop - Track state: {receivedVideoTrack.ReadyState}");
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
        
        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }
    }
}