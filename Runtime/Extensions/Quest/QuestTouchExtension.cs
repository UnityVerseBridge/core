using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityVerseBridge.Core.DataChannel.Data;

namespace UnityVerseBridge.Core.Extensions.Quest
{
    /// <summary>
    /// Quest VR에서 여러 모바일 기기로부터 터치 입력을 수신하고 시각화하는 확장 컴포넌트
    /// TouchInputHandler를 보완하는 Quest 특화 기능을 제공합니다.
    /// </summary>
    [RequireComponent(typeof(UnityVerseBridgeManager))]
    public class QuestTouchExtension : MonoBehaviour
    {
        [Header("Touch Display Settings")]
        [SerializeField] private Canvas touchCanvas;
        [SerializeField] private GameObject touchPointerPrefab;
        [SerializeField] private bool createDefaultCanvas = true;
        
        [Header("Visualization")]
        [SerializeField] private float pointerSize = 50f;
        [SerializeField] private bool animateOnTouch = true;
        [SerializeField] private bool showTouchTrail = true;
        [SerializeField] private bool showPeerLabel = true;
        
        [Header("Peer Colors")]
        [SerializeField] private Color[] peerColors = new Color[]
        {
            Color.red,
            Color.blue,
            Color.green,
            Color.yellow,
            Color.magenta,
            Color.cyan,
            new Color(1f, 0.5f, 0f), // Orange
            new Color(0.5f, 0f, 1f)  // Purple
        };

        [Header("Camera Reference")]
        [SerializeField] private Camera vrCamera;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        private UnityVerseBridgeManager bridgeManager;
        private WebRtcManager webRtcManager;
        private Dictionary<string, PeerTouchInfo> peerTouches = new Dictionary<string, PeerTouchInfo>();
        private Dictionary<string, Color> peerColorMap = new Dictionary<string, Color>();
        private int nextColorIndex = 0;

        private class PeerTouchInfo
        {
            public string PeerId { get; set; }
            public GameObject PointerObject { get; set; }
            public Image PointerImage { get; set; }
            public Text PeerLabel { get; set; }
            public TrailRenderer Trail { get; set; }
            public Vector2 CurrentPosition { get; set; }
            public bool IsActive { get; set; }
            public float LastTouchTime { get; set; }
            public int TouchId { get; set; }
        }

        void Awake()
        {
            bridgeManager = GetComponent<UnityVerseBridgeManager>();
            if (bridgeManager == null)
            {
                Debug.LogError("[QuestTouchExtension] UnityVerseBridgeManager not found!");
                enabled = false;
                return;
            }
        }

        void Start()
        {
            if (bridgeManager.Mode != UnityVerseBridgeManager.BridgeMode.Host)
            {
                Debug.LogWarning("[QuestTouchExtension] This component only works in Host mode. Disabling...");
                enabled = false;
                return;
            }
            
            webRtcManager = bridgeManager.WebRtcManager;
            if (webRtcManager == null)
            {
                Debug.LogError("[QuestTouchExtension] WebRtcManager not found!");
                enabled = false;
                return;
            }

            // Setup canvas
            if (createDefaultCanvas && touchCanvas == null)
            {
                CreateDefaultCanvas();
            }

            // Find VR camera
            if (vrCamera == null)
            {
#if UNITY_ANDROID && QUEST_SUPPORT
                var cameraRig = FindFirstObjectByType<OVRCameraRig>();
                if (cameraRig != null)
                {
                    vrCamera = cameraRig.centerEyeAnchor.GetComponent<Camera>();
                }
#endif
                if (vrCamera == null)
                {
                    vrCamera = Camera.main;
                }
            }

            // Create default pointer prefab if needed
            if (touchPointerPrefab == null)
            {
                CreateDefaultPointerPrefab();
            }

            // Subscribe to events
            webRtcManager.OnDataChannelMessageReceived += HandleDataChannelMessage;
            webRtcManager.OnMultiPeerDataChannelMessageReceived += HandleMultiPeerDataChannelMessage;
            webRtcManager.OnPeerDisconnected += HandlePeerDisconnected;
            
            Debug.Log("[QuestTouchExtension] Initialized");
        }

        void OnDestroy()
        {
            if (webRtcManager != null)
            {
                webRtcManager.OnDataChannelMessageReceived -= HandleDataChannelMessage;
                webRtcManager.OnMultiPeerDataChannelMessageReceived -= HandleMultiPeerDataChannelMessage;
                webRtcManager.OnPeerDisconnected -= HandlePeerDisconnected;
            }

            // Clean up all touch pointers
            foreach (var touchInfo in peerTouches.Values)
            {
                if (touchInfo.PointerObject != null)
                {
                    Destroy(touchInfo.PointerObject);
                }
            }
            peerTouches.Clear();
        }

        void Update()
        {
            // Update touch pointer positions and fade out
            foreach (var touchInfo in peerTouches.Values)
            {
                if (touchInfo.IsActive)
                {
                    UpdatePointerPosition(touchInfo);
                    
                    // Fade out after timeout
                    float timeSinceTouch = Time.time - touchInfo.LastTouchTime;
                    if (timeSinceTouch > 0.5f)
                    {
                        float alpha = Mathf.Lerp(1f, 0f, (timeSinceTouch - 0.5f) / 0.5f);
                        SetPointerAlpha(touchInfo, alpha);
                        
                        if (timeSinceTouch > 1f)
                        {
                            touchInfo.IsActive = false;
                            touchInfo.PointerObject.SetActive(false);
                        }
                    }
                }
            }
        }

        private void CreateDefaultCanvas()
        {
            GameObject canvasObject = new GameObject("Touch Display Canvas");
            touchCanvas = canvasObject.AddComponent<Canvas>();
            touchCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            touchCanvas.sortingOrder = 100;
            
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        private void CreateDefaultPointerPrefab()
        {
            touchPointerPrefab = new GameObject("Touch Pointer Prefab");
            
            // Pointer image
            Image pointerImage = touchPointerPrefab.AddComponent<Image>();
            pointerImage.sprite = CreateCircleSprite();
            pointerImage.color = Color.white;
            pointerImage.raycastTarget = false;
            
            RectTransform rectTransform = touchPointerPrefab.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(pointerSize, pointerSize);
            
            // Peer label
            GameObject labelObject = new GameObject("Peer Label");
            labelObject.transform.SetParent(touchPointerPrefab.transform);
            Text label = labelObject.AddComponent<Text>();
            label.text = "Peer";
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 14;
            label.color = Color.white;
            label.raycastTarget = false;
            
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchoredPosition = new Vector2(0, -pointerSize);
            labelRect.sizeDelta = new Vector2(100, 20);
            
            // Trail renderer (optional)
            if (showTouchTrail)
            {
                TrailRenderer trail = touchPointerPrefab.AddComponent<TrailRenderer>();
                trail.time = 0.5f;
                trail.startWidth = pointerSize * 0.5f;
                trail.endWidth = 0f;
                trail.material = new Material(Shader.Find("Sprites/Default"));
            }
            
            touchPointerPrefab.SetActive(false);
        }

        private Sprite CreateCircleSprite()
        {
            Texture2D texture = new Texture2D(64, 64);
            Color[] pixels = new Color[64 * 64];
            
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = x - 32;
                    float dy = y - 32;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    
                    if (distance < 30)
                    {
                        float alpha = 1f - (distance / 30f);
                        pixels[y * 64 + x] = new Color(1, 1, 1, alpha);
                    }
                    else
                    {
                        pixels[y * 64 + x] = Color.clear;
                    }
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            
            return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        }

        private void HandleDataChannelMessage(string jsonData)
        {
            ProcessTouchMessage("default", jsonData);
        }

        private void HandleMultiPeerDataChannelMessage(string peerId, string jsonData)
        {
            ProcessTouchMessage(peerId, jsonData);
        }

        private void ProcessTouchMessage(string peerId, string jsonData)
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
            catch (Exception e)
            {
                Debug.LogError($"[QuestTouchExtension] Failed to parse message from {peerId}: {e.Message}");
            }
        }

        private void ProcessTouchData(string peerId, TouchData touchData)
        {
            string touchKey = $"{peerId}_{touchData.touchId}";
            
            // Get or create touch info
            if (!peerTouches.TryGetValue(touchKey, out var touchInfo))
            {
                touchInfo = CreatePeerTouchInfo(peerId, touchData.touchId);
                peerTouches[touchKey] = touchInfo;
            }

            // Update touch position
            touchInfo.CurrentPosition = new Vector2(touchData.positionX, touchData.positionY);
            touchInfo.LastTouchTime = Time.time;
            touchInfo.IsActive = true;

            // Activate pointer
            if (!touchInfo.PointerObject.activeSelf)
            {
                touchInfo.PointerObject.SetActive(true);
            }

            // Handle touch phases
            switch (touchData.phase)
            {
                case UnityVerseBridge.Core.DataChannel.Data.TouchPhase.Began:
                    if (animateOnTouch)
                    {
                        AnimateTouchBegan(touchInfo);
                    }
                    if (debugMode)
                        Debug.Log($"[QuestTouchExtension] Touch began from {peerId} at ({touchData.positionX:F3}, {touchData.positionY:F3})");
                    break;
                    
                case UnityVerseBridge.Core.DataChannel.Data.TouchPhase.Moved:
                    // Update position is already done above
                    break;
                    
                case UnityVerseBridge.Core.DataChannel.Data.TouchPhase.Ended:
                case UnityVerseBridge.Core.DataChannel.Data.TouchPhase.Canceled:
                    if (animateOnTouch)
                    {
                        AnimateTouchEnded(touchInfo);
                    }
                    // Mark for removal after animation
                    touchInfo.LastTouchTime = Time.time - 0.5f;
                    break;
            }
        }

        private PeerTouchInfo CreatePeerTouchInfo(string peerId, int touchId)
        {
            // Assign color
            if (!peerColorMap.ContainsKey(peerId))
            {
                peerColorMap[peerId] = peerColors[nextColorIndex % peerColors.Length];
                nextColorIndex++;
            }

            // Create pointer object
            GameObject pointerObject = Instantiate(touchPointerPrefab, touchCanvas.transform);
            pointerObject.name = $"Touch_{peerId}_{touchId}";
            pointerObject.SetActive(false);

            var touchInfo = new PeerTouchInfo
            {
                PeerId = peerId,
                TouchId = touchId,
                PointerObject = pointerObject,
                PointerImage = pointerObject.GetComponent<Image>(),
                PeerLabel = pointerObject.GetComponentInChildren<Text>(),
                Trail = pointerObject.GetComponent<TrailRenderer>()
            };

            // Set color
            Color peerColor = peerColorMap[peerId];
            touchInfo.PointerImage.color = peerColor;
            
            if (touchInfo.Trail != null)
            {
                touchInfo.Trail.startColor = peerColor;
                touchInfo.Trail.endColor = new Color(peerColor.r, peerColor.g, peerColor.b, 0f);
            }

            // Set label
            if (touchInfo.PeerLabel != null)
            {
                touchInfo.PeerLabel.text = showPeerLabel ? $"Player {GetPeerIndex(peerId) + 1}" : "";
                touchInfo.PeerLabel.gameObject.SetActive(showPeerLabel);
            }

            return touchInfo;
        }

        private int GetPeerIndex(string peerId)
        {
            int index = 0;
            foreach (var kvp in peerColorMap)
            {
                if (kvp.Key == peerId)
                    return index;
                index++;
            }
            return 0;
        }

        private void UpdatePointerPosition(PeerTouchInfo touchInfo)
        {
            if (touchCanvas == null || touchInfo.PointerObject == null) return;

            // Convert normalized coordinates to screen position
            Vector2 screenPosition = new Vector2(
                touchInfo.CurrentPosition.x * Screen.width,
                touchInfo.CurrentPosition.y * Screen.height
            );

            RectTransform rectTransform = touchInfo.PointerObject.GetComponent<RectTransform>();
            rectTransform.position = screenPosition;
        }

        private void SetPointerAlpha(PeerTouchInfo touchInfo, float alpha)
        {
            if (touchInfo.PointerImage != null)
            {
                Color color = touchInfo.PointerImage.color;
                color.a = alpha;
                touchInfo.PointerImage.color = color;
            }

            if (touchInfo.PeerLabel != null)
            {
                Color labelColor = touchInfo.PeerLabel.color;
                labelColor.a = alpha;
                touchInfo.PeerLabel.color = labelColor;
            }
        }

        private void AnimateTouchBegan(PeerTouchInfo touchInfo)
        {
            if (touchInfo.PointerObject == null) return;
            StopAllCoroutines();
            StartCoroutine(AnimateScale(touchInfo.PointerObject, Vector3.one * 1.2f, 0.1f));
        }

        private void AnimateTouchEnded(PeerTouchInfo touchInfo)
        {
            if (touchInfo.PointerObject == null) return;
            StartCoroutine(AnimateScale(touchInfo.PointerObject, Vector3.one, 0.1f));
        }

        private System.Collections.IEnumerator AnimateScale(GameObject target, Vector3 targetScale, float duration)
        {
            if (target == null) yield break;
            
            RectTransform rectTransform = target.GetComponent<RectTransform>();
            Vector3 startScale = rectTransform.localScale;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / duration;
                t = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic
                
                rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            rectTransform.localScale = targetScale;
        }

        private void HandlePeerDisconnected(string peerId)
        {
            // Remove all touches from this peer
            var keysToRemove = new List<string>();
            foreach (var kvp in peerTouches)
            {
                if (kvp.Value.PeerId == peerId)
                {
                    if (kvp.Value.PointerObject != null)
                    {
                        Destroy(kvp.Value.PointerObject);
                    }
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                peerTouches.Remove(key);
            }
        }

        // Public API
        public void SetPeerColor(string peerId, Color color)
        {
            peerColorMap[peerId] = color;
            
            foreach (var touchInfo in peerTouches.Values)
            {
                if (touchInfo.PeerId == peerId)
                {
                    touchInfo.PointerImage.color = color;
                    if (touchInfo.Trail != null)
                    {
                        touchInfo.Trail.startColor = color;
                        touchInfo.Trail.endColor = new Color(color.r, color.g, color.b, 0f);
                    }
                }
            }
        }

        public void ClearAllTouches()
        {
            foreach (var touchInfo in peerTouches.Values)
            {
                touchInfo.IsActive = false;
                touchInfo.PointerObject.SetActive(false);
            }
        }

        public Dictionary<string, Vector2> GetActiveTouches()
        {
            var activeTouches = new Dictionary<string, Vector2>();
            foreach (var kvp in peerTouches)
            {
                if (kvp.Value.IsActive)
                {
                    activeTouches[kvp.Key] = kvp.Value.CurrentPosition;
                }
            }
            return activeTouches;
        }
    }
}