using UnityEngine;
using UnityEngine.UI;
using UnityVerseBridge.Core.Configuration;
using UnityVerseBridge.Core.DataChannel.Data;
using System.Collections.Generic;
using TouchPhase = UnityVerseBridge.Core.DataChannel.Data.TouchPhase;

namespace UnityVerseBridge.Core.Visualization
{
    /// <summary>
    /// Integrated touch visualization system for UnityVerseBridge
    /// </summary>
    public class TouchVisualizer : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TouchVisualizationConfig config;
        
        private Dictionary<int, TouchVisualization> touches = new Dictionary<int, TouchVisualization>();
        private WebRtcManager webRtcManager;
        private UnityVerseBridgeManager bridgeManager;
        private Camera mainCamera;
        private GameObject screenPlane;
        private Canvas touchCanvas;
        private GameObject canvasIndicatorPrefab;
        
        private class TouchVisualization
        {
            public Vector2 normalizedPos;
            public Vector2 screenPos;
            public GameObject cube3D;
            public GameObject planeDot;
            public GameObject canvasIndicator;
            public RectTransform canvasRectTransform;
            public Image canvasImage;
            public Text canvasCoordinateText;
            public TextMesh coordinateText;
            public float createTime;
        }
        
        public void Initialize(UnityVerseBridgeManager manager, TouchVisualizationConfig visualConfig)
        {
            bridgeManager = manager;
            config = visualConfig;
            webRtcManager = manager.WebRtcManager;
            
            if (config == null || !config.ShouldEnableForMode(manager.Mode))
            {
                UnityEngine.Debug.Log($"[TouchVisualizer] Disabled for mode: {manager.Mode}");
                enabled = false;
                return;
            }
            
            mainCamera = Camera.main;
            if (mainCamera == null && manager.Mode == UnityVerseBridgeManager.BridgeMode.Host)
            {
                // Try to find VR camera
                mainCamera = manager.QuestStreamCamera;
            }
            
            if (mainCamera == null)
            {
                UnityEngine.Debug.LogError("[TouchVisualizer] No camera found!");
                enabled = false;
                return;
            }
            
            // Subscribe to events
            webRtcManager.OnDataChannelMessageReceived += OnDataChannelMessageReceived;
            webRtcManager.OnMultiPeerDataChannelMessageReceived += OnMultiPeerDataChannelMessageReceived;
            
            // Create screen plane if needed
            if (config.mode == TouchVisualizationConfig.VisualizationMode.ScreenPlane || 
                config.mode == TouchVisualizationConfig.VisualizationMode.Both)
            {
                CreateScreenPlane();
            }
            
            // Create canvas if needed
            if (config.mode == TouchVisualizationConfig.VisualizationMode.Canvas)
            {
                CreateTouchCanvas();
            }
            
            UnityEngine.Debug.Log($"[TouchVisualizer] Initialized with mode: {config.mode}");
        }
        
        void CreateScreenPlane()
        {
            screenPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screenPlane.name = "TouchScreenPlane";
            screenPlane.transform.SetParent(transform);
            
            Destroy(screenPlane.GetComponent<Collider>());
            
            var renderer = screenPlane.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Unlit/Transparent"));
            material.color = new Color(0, 0, 0, 0.05f);
            renderer.material = material;
            
            UpdateScreenPlane();
        }
        
        void UpdateScreenPlane()
        {
            if (screenPlane == null || mainCamera == null) return;
            
            float distance = config.planeDistance;
            float height = 2f * distance * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float width = height * mainCamera.aspect;
            
            screenPlane.transform.position = mainCamera.transform.position + mainCamera.transform.forward * distance;
            screenPlane.transform.rotation = mainCamera.transform.rotation;
            screenPlane.transform.localScale = new Vector3(width, height, 1f);
        }
        
        void CreateTouchCanvas()
        {
            // Create canvas GameObject
            GameObject canvasGO = new GameObject("TouchVisualizationCanvas");
            canvasGO.transform.SetParent(transform);
            
            touchCanvas = canvasGO.AddComponent<Canvas>();
            touchCanvas.renderMode = config.canvasRenderMode;
            touchCanvas.sortingOrder = config.canvasSortingOrder;
            
            // Add CanvasScaler for resolution independence
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            // Add GraphicRaycaster (though we don't need input)
            canvasGO.AddComponent<GraphicRaycaster>();
            
            // Start with canvas hidden
            canvasGO.SetActive(false);
            
            // Create canvas indicator prefab if not provided
            if (config.canvasIndicatorPrefab == null)
            {
                CreateDefaultCanvasIndicatorPrefab();
            }
            else
            {
                canvasIndicatorPrefab = config.canvasIndicatorPrefab;
            }
        }
        
        void CreateDefaultCanvasIndicatorPrefab()
        {
            canvasIndicatorPrefab = new GameObject("TouchIndicator");
            canvasIndicatorPrefab.SetActive(false);
            
            // Add RectTransform
            RectTransform rect = canvasIndicatorPrefab.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(config.canvasIndicatorSize, config.canvasIndicatorSize);
            
            // Add Image for visual indicator
            Image image = canvasIndicatorPrefab.AddComponent<Image>();
            image.color = config.canvasIndicatorColor;
            
            // Create a simple circle sprite
            Texture2D circleTexture = CreateCircleTexture(64);
            image.sprite = Sprite.Create(circleTexture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            
            // Add text for coordinates
            GameObject textGO = new GameObject("CoordinateText");
            textGO.transform.SetParent(canvasIndicatorPrefab.transform, false);
            
            RectTransform textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(config.canvasIndicatorSize * 0.5f + 10, -10);
            textRect.offsetMax = new Vector2(200, 10);
            
            Text text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = config.coordinateTextColor;
            text.alignment = TextAnchor.MiddleLeft;
            
            // Add outline for better visibility
            Outline outline = textGO.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1, -1);
        }
        
        Texture2D CreateCircleTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size);
            float center = size / 2f;
            float radius = size / 2f - 1;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (distance <= radius)
                    {
                        float alpha = 1f - (distance / radius) * 0.3f; // Slight fade at edges
                        texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.clear);
                    }
                }
            }
            
            texture.Apply();
            return texture;
        }
        
        void OnDataChannelMessageReceived(string jsonData)
        {
            ProcessTouchMessage("default", jsonData);
        }
        
        void OnMultiPeerDataChannelMessageReceived(string peerId, string jsonData)
        {
            ProcessTouchMessage(peerId, jsonData);
        }
        
        void ProcessTouchMessage(string peerId, string jsonData)
        {
            try
            {
                if (jsonData.Contains("\"type\":\"touch\""))
                {
                    var touch = JsonUtility.FromJson<TouchData>(jsonData);
                    ProcessTouch(touch);
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[TouchVisualizer] Error processing touch: {e.Message}");
            }
        }
        
        void ProcessTouch(TouchData touch)
        {
            Vector2 normalizedPos = new Vector2(touch.positionX, touch.positionY);
            
            // Calculate screen position with streaming resolution
            const float STREAM_WIDTH = 1280f;
            const float STREAM_HEIGHT = 720f;
            
            Vector2 screenPos = new Vector2(
                touch.positionX * STREAM_WIDTH,
                touch.positionY * STREAM_HEIGHT
            );
            
            // Scale to actual screen
            float scaleX = Screen.width / STREAM_WIDTH;
            float scaleY = Screen.height / STREAM_HEIGHT;
            float scale = Mathf.Min(scaleX, scaleY);
            
            float offsetX = (Screen.width - STREAM_WIDTH * scale) / 2f;
            float offsetY = (Screen.height - STREAM_HEIGHT * scale) / 2f;
            
            screenPos.x = screenPos.x * scale + offsetX;
            screenPos.y = screenPos.y * scale + offsetY;
            
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                RemoveTouch(touch.touchId);
            }
            else
            {
                UpdateTouch(touch.touchId, normalizedPos, screenPos);
            }
        }
        
        void UpdateTouch(int touchId, Vector2 normalizedPos, Vector2 screenPos)
        {
            // Check max touches limit
            if (!touches.ContainsKey(touchId) && touches.Count >= config.maxTouchesDisplay)
            {
                // Remove oldest touch
                int oldestId = -1;
                float oldestTime = float.MaxValue;
                foreach (var kvp in touches)
                {
                    if (kvp.Value.createTime < oldestTime)
                    {
                        oldestTime = kvp.Value.createTime;
                        oldestId = kvp.Key;
                    }
                }
                if (oldestId >= 0) RemoveTouch(oldestId);
            }
            
            if (!touches.TryGetValue(touchId, out TouchVisualization viz))
            {
                viz = new TouchVisualization();
                viz.createTime = Time.time;
                
                // Show canvas when first touch starts
                if (config.mode == TouchVisualizationConfig.VisualizationMode.Canvas && touchCanvas != null)
                {
                    touchCanvas.gameObject.SetActive(true);
                }
                
                // Create 3D cube
                if (config.mode == TouchVisualizationConfig.VisualizationMode.Cube3D || 
                    config.mode == TouchVisualizationConfig.VisualizationMode.Both)
                {
                    viz.cube3D = CreateCube(touchId);
                }
                
                // Create screen plane dot
                if (config.mode == TouchVisualizationConfig.VisualizationMode.ScreenPlane || 
                    config.mode == TouchVisualizationConfig.VisualizationMode.Both)
                {
                    viz.planeDot = CreatePlaneDot(touchId);
                }
                
                // Create canvas indicator
                if (config.mode == TouchVisualizationConfig.VisualizationMode.Canvas)
                {
                    viz.canvasIndicator = CreateCanvasIndicator(touchId);
                    viz.canvasRectTransform = viz.canvasIndicator.GetComponent<RectTransform>();
                    viz.canvasImage = viz.canvasIndicator.GetComponent<Image>();
                    viz.canvasCoordinateText = viz.canvasIndicator.GetComponentInChildren<Text>();
                }
                
                // Create coordinate text (for 3D modes)
                if (config.showCoordinates && config.mode != TouchVisualizationConfig.VisualizationMode.Canvas)
                {
                    viz.coordinateText = CreateCoordinateText(touchId);
                }
                
                touches[touchId] = viz;
            }
            
            viz.normalizedPos = normalizedPos;
            viz.screenPos = screenPos;
            
            UpdateVisualizationPositions(viz);
        }
        
        GameObject CreateCube(int touchId)
        {
            GameObject cube;
            if (config.cubePrefab != null)
            {
                cube = Instantiate(config.cubePrefab, transform);
            }
            else
            {
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(transform);
                Destroy(cube.GetComponent<Collider>());
                
                var renderer = cube.GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = config.cubeColor;
            }
            
            cube.name = $"TouchCube_{touchId}";
            cube.transform.localScale = Vector3.one * config.cubeSize;
            return cube;
        }
        
        GameObject CreatePlaneDot(int touchId)
        {
            GameObject dot;
            if (config.planeDotPrefab != null)
            {
                dot = Instantiate(config.planeDotPrefab, screenPlane.transform);
            }
            else
            {
                dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.transform.SetParent(screenPlane.transform);
                Destroy(dot.GetComponent<Collider>());
                
                var renderer = dot.GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Unlit/Color"));
                renderer.material.color = config.dotColor;
            }
            
            dot.name = $"PlaneDot_{touchId}";
            dot.transform.localScale = Vector3.one * config.dotSize;
            return dot;
        }
        
        TextMesh CreateCoordinateText(int touchId)
        {
            GameObject textObj = new GameObject($"TouchText_{touchId}");
            textObj.transform.SetParent(transform);
            
            var text = textObj.AddComponent<TextMesh>();
            text.fontSize = config.coordinateFontSize;
            text.color = config.coordinateTextColor;
            text.anchor = TextAnchor.MiddleLeft;
            text.characterSize = 0.01f;
            
            return text;
        }
        
        GameObject CreateCanvasIndicator(int touchId)
        {
            GameObject indicator = Instantiate(canvasIndicatorPrefab, touchCanvas.transform);
            indicator.name = $"CanvasIndicator_{touchId}";
            indicator.SetActive(true);
            
            // Apply current settings
            var image = indicator.GetComponent<Image>();
            if (image != null)
            {
                image.color = config.canvasIndicatorColor;
            }
            
            var rectTransform = indicator.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(config.canvasIndicatorSize, config.canvasIndicatorSize);
            }
            
            var text = indicator.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.gameObject.SetActive(config.showCoordinates);
            }
            
            return indicator;
        }
        
        void UpdateVisualizationPositions(TouchVisualization viz)
        {
            // Update 3D cube position
            if (viz.cube3D != null)
            {
                Vector3 viewportPos = new Vector3(viz.normalizedPos.x, viz.normalizedPos.y, config.cubeDistance);
                Vector3 worldPos = mainCamera.ViewportToWorldPoint(viewportPos);
                viz.cube3D.transform.position = worldPos;
                viz.cube3D.transform.LookAt(mainCamera.transform);
            }
            
            // Update screen plane dot position
            if (viz.planeDot != null)
            {
                float x = (viz.normalizedPos.x - 0.5f);
                float y = (viz.normalizedPos.y - 0.5f);
                viz.planeDot.transform.localPosition = new Vector3(x, y, -0.01f);
            }
            
            // Update coordinate text
            if (viz.coordinateText != null)
            {
                Vector3 textPos = Vector3.zero;
                if (viz.cube3D != null)
                {
                    textPos = viz.cube3D.transform.position + Vector3.up * (config.cubeSize * 0.7f);
                }
                else if (viz.planeDot != null)
                {
                    textPos = viz.planeDot.transform.position + Vector3.up * (config.dotSize * 2f);
                }
                
                viz.coordinateText.transform.position = textPos;
                viz.coordinateText.transform.rotation = mainCamera.transform.rotation;
                viz.coordinateText.text = $"({viz.normalizedPos.x:F2}, {viz.normalizedPos.y:F2})\n" +
                                         $"({viz.screenPos.x:F0}, {viz.screenPos.y:F0})";
            }
            
            // Update canvas indicator position
            if (viz.canvasIndicator != null && viz.canvasRectTransform != null)
            {
                viz.canvasRectTransform.position = viz.screenPos;
                
                // Update canvas coordinate text
                if (viz.canvasCoordinateText != null && config.showCoordinates)
                {
                    if (config.canvasShowBothCoordinates)
                    {
                        viz.canvasCoordinateText.text = $"Abs: ({viz.screenPos.x:F0}, {viz.screenPos.y:F0})\n" +
                                                        $"Rel: ({viz.normalizedPos.x:F2}, {viz.normalizedPos.y:F2})";
                    }
                    else
                    {
                        viz.canvasCoordinateText.text = $"({viz.screenPos.x:F0}, {viz.screenPos.y:F0})";
                    }
                }
            }
        }
        
        void RemoveTouch(int touchId)
        {
            if (touches.TryGetValue(touchId, out TouchVisualization viz))
            {
                if (viz.cube3D != null) Destroy(viz.cube3D);
                if (viz.planeDot != null) Destroy(viz.planeDot);
                if (viz.canvasIndicator != null) Destroy(viz.canvasIndicator);
                if (viz.coordinateText != null) Destroy(viz.coordinateText.gameObject);
                touches.Remove(touchId);
                
                // Hide canvas when no touches remain
                if (config.mode == TouchVisualizationConfig.VisualizationMode.Canvas && touches.Count == 0 && touchCanvas != null)
                {
                    touchCanvas.gameObject.SetActive(false);
                }
            }
        }
        
        void Update()
        {
            // Update screen plane position
            if (screenPlane != null)
            {
                UpdateScreenPlane();
            }
            
            // Update all visualization positions
            foreach (var viz in touches.Values)
            {
                UpdateVisualizationPositions(viz);
            }
            
            // Check for fade timeout
            if (config.touchFadeTime > 0)
            {
                List<int> toRemove = new List<int>();
                foreach (var kvp in touches)
                {
                    if (Time.time - kvp.Value.createTime > config.touchFadeTime)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
                foreach (int id in toRemove)
                {
                    RemoveTouch(id);
                }
            }
        }
        
        void OnDestroy()
        {
            if (webRtcManager != null)
            {
                webRtcManager.OnDataChannelMessageReceived -= OnDataChannelMessageReceived;
                webRtcManager.OnMultiPeerDataChannelMessageReceived -= OnMultiPeerDataChannelMessageReceived;
            }
            
            // Clean up all touches
            foreach (var id in new List<int>(touches.Keys))
            {
                RemoveTouch(id);
            }
            
            if (screenPlane != null) Destroy(screenPlane);
            if (touchCanvas != null) Destroy(touchCanvas.gameObject);
            if (canvasIndicatorPrefab != null) Destroy(canvasIndicatorPrefab);
        }
    }
}