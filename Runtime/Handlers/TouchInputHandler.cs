using System.Collections.Generic;
using UnityEngine;
using UnityVerseBridge.Core.DataChannel.Data;
using TouchPhase = UnityVerseBridge.Core.DataChannel.Data.TouchPhase;

// Only import InputSystem for Client mode
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

namespace UnityVerseBridge.Core
{
    /// <summary>
    /// 터치 입력을 처리하는 핸들러
    /// Host: 터치 수신 및 VR 환경에 반영
    /// Client: 터치 전송
    /// </summary>
    [RequireComponent(typeof(UnityVerseBridgeManager))]
    public class TouchInputHandler : MonoBehaviour
    {
        [Header("Client Settings (Touch Sender)")]
        [SerializeField] private float sendInterval = 0.016f; // 60fps
        
        [Header("Host Settings (Touch Receiver)")]
        [SerializeField] private Camera vrCamera;
        [SerializeField] private LayerMask touchableLayerMask = -1;
        [SerializeField] private Canvas touchCanvas; // 2D 터치 표시용 캔버스 - DEPRECATED, use TouchVisualizer component instead
        [SerializeField] private GameObject touchVisualizerPrefab;
        [SerializeField] private bool showTouchVisualizer = false; // Disabled by default - use TouchVisualizer component
        [SerializeField] private float visualizerSize = 50f; // UI 크기 (픽셀)
        
        [Header("Multi-Touch Support")]
        [SerializeField] private bool supportMultiTouch = true;
        [SerializeField] private int maxSimultaneousTouches = 5;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        
        private UnityVerseBridgeManager bridgeManager;
        private WebRtcManager webRtcManager;
        private UnityVerseBridgeManager.BridgeMode mode;
        
        // Client mode
        private float lastSendTime;
        #if UNITY_EDITOR || UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID
        private InputAction touchPressAction;
        private InputAction touchPositionAction;
        #endif
        
        // Host mode
        private Dictionary<int, GameObject> touchVisualizers = new Dictionary<int, GameObject>();
        private Dictionary<string, Dictionary<int, TouchData>> peerTouches = new Dictionary<string, Dictionary<int, TouchData>>();
        private bool useWorldSpaceCanvas = true; // World Space Canvas 사용 (VR 스트리밍에 포함)
        
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
            
            // Subscribe to events
            webRtcManager.OnDataChannelMessageReceived += OnDataChannelMessageReceived;
            
            // For multi-peer mode in WebRtcManager
            webRtcManager.OnMultiPeerDataChannelMessageReceived += OnMultiPeerDataChannelMessageReceived;
            
            isInitialized = true;
        }

        void OnEnable()
        {
            #if UNITY_EDITOR || UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID
            if (mode == UnityVerseBridgeManager.BridgeMode.Client)
            {
                // Enable Enhanced Touch Support
                EnhancedTouchSupport.Enable();
                
                // Enable input actions
                touchPressAction?.Enable();
                touchPositionAction?.Enable();
            }
            #endif
        }

        void OnDisable()
        {
            #if UNITY_EDITOR || UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID
            if (mode == UnityVerseBridgeManager.BridgeMode.Client)
            {
                // Disable Enhanced Touch Support
                EnhancedTouchSupport.Disable();
                
                // Disable input actions
                touchPressAction?.Disable();
                touchPositionAction?.Disable();
            }
            #endif
        }

        void OnDestroy()
        {
            Cleanup();
        }

        void Update()
        {
            if (!isInitialized) return;
            
            if (mode == UnityVerseBridgeManager.BridgeMode.Client)
            {
                UpdateClientMode();
            }
        }

        private void SetupHostMode()
        {
            Debug.Log("[TouchInputHandler] Setting up Host mode for touch reception");
            
            // Auto find VR camera if not assigned
            if (vrCamera == null)
            {
                #if UNITY_ANDROID && !UNITY_EDITOR
                // Use reflection to avoid direct OVRCameraRig dependency
                try
                {
                    var ovrCameraRigType = System.Type.GetType("OVRCameraRig, Oculus.VR");
                    if (ovrCameraRigType != null)
                    {
                        var cameraRig = FindFirstObjectByType(ovrCameraRigType);
                        if (cameraRig != null)
                        {
                            var centerEyeAnchorProp = ovrCameraRigType.GetProperty("centerEyeAnchor");
                            if (centerEyeAnchorProp != null)
                            {
                                var centerEyeTransform = centerEyeAnchorProp.GetValue(cameraRig) as Transform;
                                if (centerEyeTransform != null)
                                {
                                    vrCamera = centerEyeTransform.GetComponent<Camera>();
                                }
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[TouchInputHandler] Failed to find OVRCameraRig: {e.Message}");
                }
                #else
                vrCamera = Camera.main;
                #endif
            }
            
            if (vrCamera != null)
            {
                Debug.Log($"[TouchInputHandler] VR Camera found: {vrCamera.name}");
            }
            else
            {
                Debug.LogError("[TouchInputHandler] VR Camera not found! Touch handling will not work.");
            }
            
            // DEPRECATED: Touch visualization is now handled by TouchVisualizer component
            // Force disable visualization
            showTouchVisualizer = false;
            Debug.Log("[TouchInputHandler] Touch visualization is disabled. Use TouchVisualizer component instead.");
        }

        private void SetupClientMode()
        {
            if (debugMode)
            {
                Debug.Log("[TouchInputHandler] Setting up Client mode for touch sending");
            }
            
            #if UNITY_EDITOR || UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID
            // Create input actions for fallback (mouse in editor)
            var inputActionMap = new InputActionMap("TouchInput");
            
            // Mouse/Pointer press action
            touchPressAction = inputActionMap.AddAction("Press", binding: "<Mouse>/leftButton");
            touchPressAction.AddBinding("<Touchscreen>/primaryTouch/press");
            
            // Mouse/Pointer position action
            touchPositionAction = inputActionMap.AddAction("Position", binding: "<Mouse>/position");
            touchPositionAction.AddBinding("<Touchscreen>/primaryTouch/position");
            
            // Enable Enhanced Touch Support for mobile
            EnhancedTouchSupport.Enable();
            #else
            Debug.LogWarning("[TouchInputHandler] InputSystem not available on this platform for Client mode");
            #endif
        }

        private void CreateTouchCanvas()
        {
            GameObject canvasObj = new GameObject("TouchCanvas");
            touchCanvas = canvasObj.AddComponent<Canvas>();
            
            // VR 카메라 찾기
            if (vrCamera == null)
            {
                vrCamera = Camera.main;
            }
            
            if (useWorldSpaceCanvas && vrCamera != null)
            {
                // World Space Canvas - VR 스트리밍에 포함됨
                touchCanvas.renderMode = RenderMode.WorldSpace;
                touchCanvas.transform.position = vrCamera.transform.position + vrCamera.transform.forward * 2f;
                touchCanvas.transform.rotation = vrCamera.transform.rotation;
                
                // Canvas 크기 설정
                RectTransform canvasRect = touchCanvas.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(1920, 1080);
                canvasRect.localScale = Vector3.one * 0.001f; // 2미터 거리에서 적절한 크기
            }
            else
            {
                // Fallback to Overlay
                touchCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                touchCanvas.sortingOrder = 999;
            }
            
            // Canvas Scaler 추가
            var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            UnityEngine.Debug.Log($"[TouchInputHandler] Touch canvas created - Mode: {touchCanvas.renderMode}, Position: {touchCanvas.transform.position}");
        }
        
        private void CreateDefaultTouchVisualizer()
        {
            GameObject visualizer = new GameObject("TouchVisualizer");
            
            // UI Image 컴포넌트 추가
            var image = visualizer.AddComponent<UnityEngine.UI.Image>();
            
            // 빨간 원 텍스처 생성
            Texture2D circleTexture = new Texture2D(64, 64);
            Color[] pixels = new Color[64 * 64];
            Vector2 center = new Vector2(32, 32);
            
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance <= 30)
                    {
                        pixels[y * 64 + x] = new Color(1f, 0f, 0f, 1f); // 빨간색
                    }
                    else
                    {
                        pixels[y * 64 + x] = Color.clear;
                    }
                }
            }
            
            circleTexture.SetPixels(pixels);
            circleTexture.Apply();
            
            // Sprite 생성
            image.sprite = Sprite.Create(circleTexture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            image.raycastTarget = false;
            
            // RectTransform 설정
            RectTransform rectTransform = visualizer.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(visualizerSize, visualizerSize);
            
            visualizer.SetActive(false);
            touchVisualizerPrefab = visualizer;
            
            Debug.Log("[TouchInputHandler] Default 2D touch visualizer created with red circle");
        }

        private void UpdateClientMode()
        {
            // Check if we can send
            if (!CanSendData()) return;
            
            // Rate limiting
            if (Time.time - lastSendTime < sendInterval) return;
            
            #if UNITY_EDITOR || UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID
            // Handle touch input using new Input System
            var activeTouches = Touch.activeTouches;
            if (activeTouches.Count > 0)
            {
                int touchCount = supportMultiTouch ? 
                    Mathf.Min(activeTouches.Count, maxSimultaneousTouches) : 1;
                    
                for (int i = 0; i < touchCount; i++)
                {
                    SendTouchData(activeTouches[i]);
                }
                lastSendTime = Time.time;
            }
            #if UNITY_EDITOR || UNITY_STANDALONE
            // Fallback to mouse input in editor/standalone
            else if (touchPressAction != null && touchPressAction.IsPressed())
            {
                SendMouseAsTouch();
                lastSendTime = Time.time;
            }
            #endif
            #endif
        }

        private bool CanSendData()
        {
            if (webRtcManager is WebRtcManager concreteManager)
            {
                return concreteManager.IsDataChannelOpen;
            }
            return webRtcManager.IsWebRtcConnected;
        }

        private void SendMouseAsTouch()
        {
            #if UNITY_EDITOR || UNITY_STANDALONE
            Vector2 mousePos = touchPositionAction.ReadValue<Vector2>();
            float normalizedX = mousePos.x / Screen.width;
            float normalizedY = mousePos.y / Screen.height;
            
            // Determine phase based on press state changes
            TouchPhase phase = TouchPhase.Moved;
            if (touchPressAction.WasPressedThisFrame())
                phase = TouchPhase.Began;
            else if (touchPressAction.WasReleasedThisFrame())
                phase = TouchPhase.Ended;
            
            var touchData = new TouchData
            {
                type = "touch",
                touchId = 0,
                phase = phase,
                positionX = normalizedX,
                positionY = normalizedY
            };

            webRtcManager.SendDataChannelMessage(touchData);
            Debug.Log($"[TouchInputHandler] Sent mouse as touch: Phase={phase}, Pos=({normalizedX:F3}, {normalizedY:F3})");
            #endif
        }

        #if UNITY_EDITOR || UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID
        private void SendTouchData(Touch touch)
        {
            float normalizedX = touch.screenPosition.x / Screen.width;
            float normalizedY = touch.screenPosition.y / Screen.height;

            var phase = ConvertPhase(touch.phase);

            var touchData = new TouchData
            {
                type = "touch",
                touchId = touch.touchId,
                phase = phase,
                positionX = normalizedX,
                positionY = normalizedY
            };

            webRtcManager.SendDataChannelMessage(touchData);
            Debug.Log($"[TouchInputHandler] Sent touch: ID={touchData.touchId}, Phase={phase}, Pos=({normalizedX:F3}, {normalizedY:F3})");
        }
        #endif

        #if UNITY_EDITOR || UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID
        private TouchPhase ConvertPhase(UnityEngine.InputSystem.TouchPhase inputPhase)
        {
            return inputPhase switch
            {
                UnityEngine.InputSystem.TouchPhase.Began => TouchPhase.Began,
                UnityEngine.InputSystem.TouchPhase.Moved => TouchPhase.Moved,
                UnityEngine.InputSystem.TouchPhase.Stationary => TouchPhase.Moved,
                UnityEngine.InputSystem.TouchPhase.Ended => TouchPhase.Ended,
                UnityEngine.InputSystem.TouchPhase.Canceled => TouchPhase.Canceled,
                _ => TouchPhase.Canceled
            };
        }
        #endif

        private void OnDataChannelMessageReceived(string jsonData)
        {
            // Always log in Host mode for debugging
            if (mode == UnityVerseBridgeManager.BridgeMode.Host)
            {
                Debug.Log($"[TouchInputHandler] Data channel message received in Host mode. Data: {jsonData}");
            }
            else if (debugMode)
            {
                Debug.Log($"[TouchInputHandler] Data channel message received. Mode: {mode}");
            }
            
            if (mode != UnityVerseBridgeManager.BridgeMode.Host) 
            {
                if (debugMode)
                {
                    Debug.Log("[TouchInputHandler] Not in Host mode, ignoring touch data");
                }
                return;
            }
            
            HandleTouchMessage("default", jsonData);
        }

        private void OnMultiPeerDataChannelMessageReceived(string peerId, string jsonData)
        {
            if (mode != UnityVerseBridgeManager.BridgeMode.Host) return;
            
            HandleTouchMessage(peerId, jsonData);
        }

        private void HandleTouchMessage(string peerId, string jsonData)
        {
            try
            {
                var baseMsg = JsonUtility.FromJson<DataChannelMessageBase>(jsonData);
                if (baseMsg?.type == "touch")
                {
                    var touchData = JsonUtility.FromJson<TouchData>(jsonData);
                    ProcessTouchData(peerId, touchData);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TouchInputHandler] Failed to parse touch data: {e.Message}");
            }
        }

        private void ProcessTouchData(string peerId, TouchData touchData)
        {
            if (vrCamera == null)
            {
                Debug.LogError("[TouchInputHandler] VR Camera is null! Cannot process touch data.");
                return;
            }
            
            // Store touch data per peer
            if (!peerTouches.ContainsKey(peerId))
            {
                peerTouches[peerId] = new Dictionary<int, TouchData>();
            }
            peerTouches[peerId][touchData.touchId] = touchData;
            
            // Always log touch data for debugging
            Debug.Log($"[TouchInputHandler] Processing touch - PeerID: {peerId}, TouchID: {touchData.touchId}, Phase: {touchData.phase}, Pos: ({touchData.positionX:F3}, {touchData.positionY:F3})");
            
            // Debug coordinate information
            if (touchData.phase == TouchPhase.Began)
            {
                Debug.Log($"[TouchInputHandler] Screen Resolution: {Screen.width}x{Screen.height}");
                Debug.Log($"[TouchInputHandler] VR Camera: {vrCamera.name}, FOV: {vrCamera.fieldOfView}, Active: {vrCamera.gameObject.activeInHierarchy}");
                
                if (touchCanvas != null)
                {
                    Debug.Log($"[TouchInputHandler] Touch Canvas: {touchCanvas.name}, RenderMode: {touchCanvas.renderMode}, Active: {touchCanvas.gameObject.activeInHierarchy}");
                }
                else
                {
                    Debug.LogWarning("[TouchInputHandler] Touch Canvas is null!");
                }
            }
            
            // Calculate world position from normalized screen coordinates
            // 스트리밍 해상도 기준으로 고정 (1280x720)
            const float STREAM_WIDTH = 1280f;
            const float STREAM_HEIGHT = 720f;
            
            Vector3 screenPos = new Vector3(
                touchData.positionX * STREAM_WIDTH,
                touchData.positionY * STREAM_HEIGHT,
                0f
            );
            
            // 실제 화면 크기에 맞춰 스케일 조정
            float scaleX = Screen.width / STREAM_WIDTH;
            float scaleY = Screen.height / STREAM_HEIGHT;
            float scale = Mathf.Min(scaleX, scaleY); // 종횡비 유지
            
            // 중앙 정렬을 위한 오프셋
            float offsetX = (Screen.width - STREAM_WIDTH * scale) / 2f;
            float offsetY = (Screen.height - STREAM_HEIGHT * scale) / 2f;
            
            // 최종 스크린 좌표
            screenPos.x = screenPos.x * scale + offsetX;
            screenPos.y = screenPos.y * scale + offsetY;
            
            if (debugMode && touchData.phase == TouchPhase.Began)
            {
                Debug.Log($"[TouchInputHandler] Screen Position: {screenPos}");
                Debug.Log($"[TouchInputHandler] Camera: {vrCamera.name}, FOV: {vrCamera.fieldOfView}");
            }
            
            Ray ray = vrCamera.ScreenPointToRay(screenPos);
            
            // Visualization disabled - handled by TouchVisualizer component
            // UpdateTouchVisualizer(touchData.touchId, screenPos, touchData.phase);
            
            // Perform raycast for interaction
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, touchableLayerMask))
            {
                HandleTouchHit(peerId, touchData, hit);
            }
            
            // Clean up ended touches
            if (touchData.phase == TouchPhase.Ended || touchData.phase == TouchPhase.Canceled)
            {
                peerTouches[peerId].Remove(touchData.touchId);
                // HideTouchVisualizer(touchData.touchId);
            }
        }

        private void HandleTouchHit(string peerId, TouchData touchData, RaycastHit hit)
        {
            // Send touch event to hit object
            var touchable = hit.collider.GetComponent<ITouchable>();
            if (touchable != null)
            {
                switch (touchData.phase)
                {
                    case TouchPhase.Began:
                        touchable.OnTouchBegan(hit.point);
                        break;
                    case TouchPhase.Moved:
                        touchable.OnTouchMoved(hit.point);
                        break;
                    case TouchPhase.Ended:
                        touchable.OnTouchEnded(hit.point);
                        break;
                }
            }
            
            // Also support SendMessage for compatibility with VRClickHandler
            if (touchData.phase == TouchPhase.Began)
            {
                hit.collider.SendMessage("OnVRClick", SendMessageOptions.DontRequireReceiver);
                
                // Try to find VRClickHandler component directly
                var clickHandler = hit.collider.GetComponent("VRClickHandler");
                if (clickHandler != null)
                {
                    // Use reflection to call OnVRClick if component exists
                    var method = clickHandler.GetType().GetMethod("OnVRClick");
                    if (method != null)
                    {
                        method.Invoke(clickHandler, null);
                    }
                }
            }
            
            if (debugMode)
            {
                Debug.Log($"[TouchInputHandler] Touch from {peerId} hit: {hit.collider.name} at {hit.point}");
            }
        }

        private void UpdateTouchVisualizer(int touchId, Vector3 screenPosition, TouchPhase phase)
        {
            // Completely disabled - use TouchVisualizer component instead
            return;
            
            // Canvas와 prefab 확인
            if (touchCanvas == null)
            {
                CreateTouchCanvas();
            }
            
            if (touchVisualizerPrefab == null)
            {
                CreateDefaultTouchVisualizer();
            }
            
            if (!touchVisualizers.TryGetValue(touchId, out GameObject visualizer))
            {
                // Create new visualizer instance
                visualizer = Instantiate(touchVisualizerPrefab, touchCanvas.transform);
                visualizer.name = $"TouchVisualizer_{touchId}";
                touchVisualizers[touchId] = visualizer;
                Debug.Log($"[TouchInputHandler] Created new touch visualizer for ID: {touchId}");
            }
            
            visualizer.SetActive(true);
            
            // RectTransform 가져오기
            RectTransform rectTransform = visualizer.GetComponent<RectTransform>();
            
            // Canvas RectTransform 정보
            RectTransform canvasRect = touchCanvas.GetComponent<RectTransform>();
            
            // Canvas 모드에 따라 위치 설정
            if (touchCanvas.renderMode == RenderMode.WorldSpace)
            {
                // World Space에서는 Canvas 내부 좌표로 변환
                float canvasWidth = canvasRect.rect.width;
                float canvasHeight = canvasRect.rect.height;
                
                // 정규화된 좌표(0-1)를 Canvas 좌표(-0.5 ~ 0.5)로 변환
                float x = (screenPosition.x / Screen.width - 0.5f) * canvasWidth;
                float y = (screenPosition.y / Screen.height - 0.5f) * canvasHeight;
                
                rectTransform.anchoredPosition = new Vector2(x, y);
                Debug.Log($"[TouchInputHandler] World Space - Canvas pos: ({x}, {y})");
            }
            else if (touchCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                rectTransform.position = screenPosition;
                Debug.Log($"[TouchInputHandler] Overlay mode - Direct position: {screenPosition}");
            }
            else
            {
                // Screen Space - Camera
                rectTransform.position = screenPosition;
            }
            
            // 터치 시작 시 애니메이션
            if (phase == TouchPhase.Began)
            {
                rectTransform.localScale = Vector3.one * 1.2f;
            }
            else
            {
                rectTransform.localScale = Vector3.one;
            }
            
            // Detailed debugging
            Debug.Log($"[TouchInputHandler] Touch visualizer updated:");
            Debug.Log($"  - ID: {touchId}");
            Debug.Log($"  - Screen Position: {screenPosition}");
            Debug.Log($"  - Canvas Mode: {touchCanvas.renderMode}");
            Debug.Log($"  - Canvas Size: {canvasRect.rect.width}x{canvasRect.rect.height}");
            Debug.Log($"  - RectTransform Position: {rectTransform.position}");
            Debug.Log($"  - RectTransform Anchored Position: {rectTransform.anchoredPosition}");
            Debug.Log($"  - Active: {visualizer.activeSelf}");
        }

        private void HideTouchVisualizer(int touchId)
        {
            if (touchVisualizers.TryGetValue(touchId, out GameObject visualizer))
            {
                if (visualizer != null)
                {
                    visualizer.SetActive(false);
                }
            }
        }

        private void Cleanup()
        {
            if (!isInitialized) return;
            
            // Unsubscribe events
            if (webRtcManager != null)
            {
                webRtcManager.OnDataChannelMessageReceived -= OnDataChannelMessageReceived;
            }
            
            webRtcManager.OnMultiPeerDataChannelMessageReceived -= OnMultiPeerDataChannelMessageReceived;
            
            // Clean up visualizers
            foreach (var visualizer in touchVisualizers.Values)
            {
                if (visualizer != null)
                {
                    Destroy(visualizer);
                }
            }
            touchVisualizers.Clear();
            
            // Dispose input actions
            #if UNITY_EDITOR || UNITY_STANDALONE || UNITY_IOS || UNITY_ANDROID
            touchPressAction?.Dispose();
            touchPositionAction?.Dispose();
            #endif
        }
    }

    /// <summary>
    /// 터치 가능한 오브젝트를 위한 인터페이스
    /// </summary>
    public interface ITouchable
    {
        void OnTouchBegan(Vector3 worldPosition);
        void OnTouchMoved(Vector3 worldPosition);
        void OnTouchEnded(Vector3 worldPosition);
    }
}