using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityVerseBridge.Core.DataChannel.Data;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityVerseBridge.Core.DataChannel.Data.TouchPhase;

namespace UnityVerseBridge.Core.Extensions.Mobile
{
    /// <summary>
    /// 모바일 터치 입력을 WebRTC로 전송하는 확장 컴포넌트
    /// TouchInputHandler를 보완하는 모바일 특화 기능을 제공합니다.
    /// </summary>
    [RequireComponent(typeof(UnityVerseBridgeManager))]
    public class MobileInputExtension : MonoBehaviour
    {
        [Header("Touch Settings")]
        [SerializeField] private float sendInterval = 0.016f; // 60fps
        [SerializeField] private bool enableMultiTouch = true;
        [SerializeField] private int maxTouchCount = 10;
        
        [Header("Touch Area")]
        [SerializeField] private RectTransform touchArea; // 터치 영역 제한 (optional)
        [SerializeField] private bool normalizeToTouchArea = true;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool showTouchVisualizer = false;
        
        private UnityVerseBridgeManager bridgeManager;
        private WebRtcManager webRtcManager;
        private float lastSendTime;
        private Camera uiCamera;
        
        // Touch visualizer
        private GameObject[] touchVisualizers;
        private readonly Color[] touchColors = new Color[] 
        { 
            Color.red, Color.green, Color.blue, Color.yellow, Color.magenta,
            Color.cyan, Color.white, Color.gray, new Color(1f, 0.5f, 0f), new Color(0.5f, 0f, 1f)
        };

        void Awake()
        {
            bridgeManager = GetComponent<UnityVerseBridgeManager>();
            if (bridgeManager == null)
            {
                Debug.LogError("[MobileInputExtension] UnityVerseBridgeManager not found!");
                enabled = false;
                return;
            }
        }

        void Start()
        {
            if (bridgeManager.bridgeMode != UnityVerseBridgeManager.BridgeMode.Client)
            {
                Debug.LogWarning("[MobileInputExtension] This component only works in Client mode. Disabling...");
                enabled = false;
                return;
            }
            
            webRtcManager = bridgeManager.WebRtcManager;
            if (webRtcManager == null)
            {
                Debug.LogError("[MobileInputExtension] WebRtcManager not found!");
                enabled = false;
                return;
            }

            // UI Camera 찾기
            if (touchArea != null)
            {
                Canvas canvas = touchArea.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    uiCamera = canvas.worldCamera;
                }
            }

            // Touch visualizer 초기화
            if (showTouchVisualizer)
            {
                InitializeTouchVisualizers();
            }

            Debug.Log("[MobileInputExtension] Initialized");
        }

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            
            if (touchVisualizers != null)
            {
                foreach (var visualizer in touchVisualizers)
                {
                    if (visualizer != null) Destroy(visualizer);
                }
            }
        }

        void Update()
        {
            if (!webRtcManager.IsWebRtcConnected) return;

            // Send interval throttling
            if (Time.time - lastSendTime < sendInterval) return;

            ProcessTouches();
            lastSendTime = Time.time;
        }

        private void ProcessTouches()
        {
            var activeTouches = Touch.activeTouches;
            if (activeTouches.Count == 0) return;

            int touchCount = enableMultiTouch ? Mathf.Min(activeTouches.Count, maxTouchCount) : 1;

            for (int i = 0; i < touchCount; i++)
            {
                var touch = activeTouches[i];
                Vector2 position = touch.screenPosition;

                // Touch area 제한 확인
                if (touchArea != null)
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(touchArea, position, uiCamera))
                    {
                        continue;
                    }

                    if (normalizeToTouchArea)
                    {
                        // Touch area 내에서의 상대 좌표로 변환
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            touchArea, position, uiCamera, out Vector2 localPoint);
                        
                        Rect rect = touchArea.rect;
                        position.x = (localPoint.x - rect.x) / rect.width;
                        position.y = (localPoint.y - rect.y) / rect.height;
                    }
                    else
                    {
                        // 전체 화면 기준 정규화
                        position.x /= Screen.width;
                        position.y /= Screen.height;
                    }
                }
                else
                {
                    // Touch area가 없으면 전체 화면 기준
                    position.x /= Screen.width;
                    position.y /= Screen.height;
                }

                // Clamp to valid range
                position.x = Mathf.Clamp01(position.x);
                position.y = Mathf.Clamp01(position.y);

                var touchData = new TouchData
                {
                    touchId = touch.touchId,
                    positionX = position.x,
                    positionY = position.y,
                    phase = ConvertTouchPhase(touch.phase)
                };

                if (debugMode)
                {
                    Debug.Log($"[MobileInputExtension] Sending touch {touchData.touchId}: " +
                             $"({touchData.positionX:F3}, {touchData.positionY:F3}) - {touchData.phase}");
                }

                webRtcManager.SendDataChannelMessage(touchData);

                // Update visualizer
                if (showTouchVisualizer && touchVisualizers != null && i < touchVisualizers.Length)
                {
                    UpdateTouchVisualizer(i, touch.screenPosition, touch.phase);
                }
            }

            // Hide unused visualizers
            if (showTouchVisualizer && touchVisualizers != null)
            {
                for (int i = touchCount; i < touchVisualizers.Length; i++)
                {
                    if (touchVisualizers[i] != null)
                        touchVisualizers[i].SetActive(false);
                }
            }
        }

        private TouchPhase ConvertTouchPhase(UnityEngine.InputSystem.TouchPhase inputPhase)
        {
            switch (inputPhase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    return TouchPhase.Began;
                case UnityEngine.InputSystem.TouchPhase.Moved:
                    return TouchPhase.Moved;
                case UnityEngine.InputSystem.TouchPhase.Stationary:
                    return TouchPhase.Moved; // Stationary를 Moved로 매핑
                case UnityEngine.InputSystem.TouchPhase.Ended:
                    return TouchPhase.Ended;
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    return TouchPhase.Canceled;
                default:
                    return TouchPhase.Canceled;
            }
        }

        private void InitializeTouchVisualizers()
        {
            touchVisualizers = new GameObject[maxTouchCount];
            
            // Create a canvas for touch visualization
            GameObject canvasGO = new GameObject("TouchVisualizerCanvas");
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // On top of everything
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            for (int i = 0; i < maxTouchCount; i++)
            {
                GameObject visualizer = new GameObject($"TouchVisualizer_{i}");
                visualizer.transform.SetParent(canvas.transform, false);
                
                var image = visualizer.AddComponent<UnityEngine.UI.Image>();
                image.sprite = CreateCircleSprite();
                image.color = touchColors[i % touchColors.Length];
                image.rectTransform.sizeDelta = new Vector2(100, 100);
                image.raycastTarget = false;
                
                visualizer.SetActive(false);
                touchVisualizers[i] = visualizer;
            }
        }

        private void UpdateTouchVisualizer(int index, Vector2 screenPosition, UnityEngine.InputSystem.TouchPhase phase)
        {
            if (touchVisualizers == null || index >= touchVisualizers.Length) return;
            
            GameObject visualizer = touchVisualizers[index];
            if (visualizer == null) return;
            
            visualizer.SetActive(true);
            visualizer.transform.position = screenPosition;
            
            // Scale based on phase
            float scale = phase == UnityEngine.InputSystem.TouchPhase.Began ? 1.2f : 1.0f;
            visualizer.transform.localScale = Vector3.one * scale;
            
            // Fade out on end
            if (phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                var image = visualizer.GetComponent<UnityEngine.UI.Image>();
                if (image != null)
                {
                    Color c = image.color;
                    c.a = 0.5f;
                    image.color = c;
                }
            }
        }

        private Sprite CreateCircleSprite()
        {
            // Create a simple circle texture
            int size = 64;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 2;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance <= radius)
                    {
                        float alpha = 1f - (distance / radius);
                        pixels[y * size + x] = new Color(1, 1, 1, alpha * alpha);
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // Public API
        public void SetTouchArea(RectTransform area)
        {
            touchArea = area;
            if (area != null)
            {
                Canvas canvas = area.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    uiCamera = canvas.worldCamera;
                }
            }
        }

        public void SetMultiTouchEnabled(bool enabled)
        {
            enableMultiTouch = enabled;
        }

        public void SetSendInterval(float interval)
        {
            sendInterval = Mathf.Max(0.001f, interval);
        }

        public void SetDebugMode(bool enabled)
        {
            debugMode = enabled;
            showTouchVisualizer = enabled;
            
            if (enabled && touchVisualizers == null)
            {
                InitializeTouchVisualizers();
            }
            else if (!enabled && touchVisualizers != null)
            {
                foreach (var visualizer in touchVisualizers)
                {
                    if (visualizer != null) Destroy(visualizer);
                }
                touchVisualizers = null;
            }
        }
    }
}