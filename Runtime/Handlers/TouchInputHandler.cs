using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityVerseBridge.Core.DataChannel.Data;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityVerseBridge.Core.DataChannel.Data.TouchPhase;

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
        [SerializeField] private GameObject touchVisualizerPrefab;
        [SerializeField] private bool showTouchVisualizer = true;
        
        [Header("Multi-Touch Support")]
        [SerializeField] private bool supportMultiTouch = true;
        [SerializeField] private int maxSimultaneousTouches = 5;
        
        private UnityVerseBridgeManager bridgeManager;
        private WebRtcManager webRtcManager;
        private UnityVerseBridgeManager.BridgeMode mode;
        
        // Client mode
        private float lastSendTime;
        private InputAction touchPressAction;
        private InputAction touchPositionAction;
        
        // Host mode
        private Dictionary<int, GameObject> touchVisualizers = new Dictionary<int, GameObject>();
        private Dictionary<string, Dictionary<int, TouchData>> peerTouches = new Dictionary<string, Dictionary<int, TouchData>>();
        
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
            if (mode == UnityVerseBridgeManager.BridgeMode.Client)
            {
                // Enable Enhanced Touch Support
                EnhancedTouchSupport.Enable();
                
                // Enable input actions
                touchPressAction?.Enable();
                touchPositionAction?.Enable();
            }
        }

        void OnDisable()
        {
            if (mode == UnityVerseBridgeManager.BridgeMode.Client)
            {
                // Disable Enhanced Touch Support
                EnhancedTouchSupport.Disable();
                
                // Disable input actions
                touchPressAction?.Disable();
                touchPositionAction?.Disable();
            }
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
            // Auto find VR camera if not assigned
            if (vrCamera == null)
            {
                #if UNITY_ANDROID && !UNITY_EDITOR
                var cameraRig = FindFirstObjectByType<OVRCameraRig>();
                if (cameraRig != null)
                {
                    vrCamera = cameraRig.centerEyeAnchor.GetComponent<Camera>();
                }
                #else
                vrCamera = Camera.main;
                #endif
            }
            
            // Create default touch visualizer if not assigned
            if (touchVisualizerPrefab == null && showTouchVisualizer)
            {
                CreateDefaultTouchVisualizer();
            }
        }

        private void SetupClientMode()
        {
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
        }

        private void CreateDefaultTouchVisualizer()
        {
            GameObject visualizer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualizer.name = "TouchVisualizer";
            visualizer.transform.localScale = Vector3.one * 0.1f;
            
            // Make it red and semi-transparent
            var renderer = visualizer.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                renderer.material.color = new Color(1f, 0f, 0f, 0.5f);
            }
            
            // Remove collider
            var collider = visualizer.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
            
            visualizer.SetActive(false);
            touchVisualizerPrefab = visualizer;
        }

        private void UpdateClientMode()
        {
            // Check if we can send
            if (!CanSendData()) return;
            
            // Rate limiting
            if (Time.time - lastSendTime < sendInterval) return;
            
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
        }

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

        private void OnDataChannelMessageReceived(string jsonData)
        {
            if (mode != UnityVerseBridgeManager.BridgeMode.Host) return;
            
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
            if (vrCamera == null) return;
            
            // Store touch data per peer
            if (!peerTouches.ContainsKey(peerId))
            {
                peerTouches[peerId] = new Dictionary<int, TouchData>();
            }
            peerTouches[peerId][touchData.touchId] = touchData;
            
            // Calculate world position from normalized screen coordinates
            Vector3 screenPos = new Vector3(
                touchData.positionX * Screen.width,
                touchData.positionY * Screen.height,
                0f
            );
            
            Ray ray = vrCamera.ScreenPointToRay(screenPos);
            
            // Perform raycast
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, touchableLayerMask))
            {
                HandleTouchHit(peerId, touchData, hit);
                UpdateTouchVisualizer(touchData.touchId, hit.point, touchData.phase);
            }
            else
            {
                // No hit - project to far plane
                Vector3 worldPos = ray.GetPoint(10f);
                UpdateTouchVisualizer(touchData.touchId, worldPos, touchData.phase);
            }
            
            // Clean up ended touches
            if (touchData.phase == TouchPhase.Ended || touchData.phase == TouchPhase.Canceled)
            {
                peerTouches[peerId].Remove(touchData.touchId);
                HideTouchVisualizer(touchData.touchId);
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
            
            Debug.Log($"[TouchInputHandler] Touch from {peerId} hit: {hit.collider.name} at {hit.point}");
        }

        private void UpdateTouchVisualizer(int touchId, Vector3 position, TouchPhase phase)
        {
            if (!showTouchVisualizer || touchVisualizerPrefab == null) return;
            
            if (!touchVisualizers.TryGetValue(touchId, out GameObject visualizer))
            {
                visualizer = Instantiate(touchVisualizerPrefab);
                touchVisualizers[touchId] = visualizer;
            }
            
            visualizer.SetActive(true);
            visualizer.transform.position = position;
            
            // Scale based on touch phase
            float scale = phase == TouchPhase.Began ? 0.15f : 0.1f;
            visualizer.transform.localScale = Vector3.one * scale;
        }

        private void HideTouchVisualizer(int touchId)
        {
            if (touchVisualizers.TryGetValue(touchId, out GameObject visualizer))
            {
                visualizer.SetActive(false);
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
            touchPressAction?.Dispose();
            touchPositionAction?.Dispose();
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