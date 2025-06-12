using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;

namespace UnityVerseBridge.Core.UI
{
    /// <summary>
    /// Centralized UI management system for UnityVerse
    /// Handles UI creation, tracking, and cleanup across all platforms
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private static UIManager instance;
        public static UIManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<UIManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("UIManager");
                        instance = go.AddComponent<UIManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }
        
        [Header("Configuration")]
        [SerializeField] private string uiTag = "UnityVerseUI";
        [SerializeField] private string uiLayer = "UI";
        [SerializeField] private int defaultSortingOrder = 100;
        
        [Header("UI Prefabs")]
        [SerializeField] private GameObject errorPanelPrefab;
        [SerializeField] private GameObject loadingPanelPrefab;
        [SerializeField] private GameObject connectionStatusPrefab;
        
        [Header("Tracked Objects")]
        [SerializeField] private List<GameObject> trackedObjects = new List<GameObject>();
        [SerializeField] private List<Canvas> trackedCanvases = new List<Canvas>();
        private Dictionary<string, GameObject> namedPanels = new Dictionary<string, GameObject>();
        
        // Common UI elements
        private GameObject errorPanel;
        private GameObject loadingPanel;
        private GameObject connectionStatusPanel;
        
        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Create default UI layer if it doesn't exist
            CreateLayerIfNeeded(uiLayer);
        }
        
        #region Canvas Management
        
        /// <summary>
        /// Create a tracked canvas with proper setup
        /// </summary>
        public Canvas CreateCanvas(string name = "UICanvas", CanvasType type = CanvasType.Overlay)
        {
            GameObject canvasObj = new GameObject(name);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            
            switch (type)
            {
                case CanvasType.Overlay:
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = defaultSortingOrder;
                    break;
                    
                case CanvasType.Camera:
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = Camera.main;
                    canvas.planeDistance = 1f;
                    break;
                    
                case CanvasType.World:
                    canvas.renderMode = RenderMode.WorldSpace;
                    break;
            }
            
            // Add CanvasScaler for proper UI scaling
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            // Add GraphicRaycaster for UI interaction
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Track the canvas
            TrackGameObject(canvasObj);
            
            return canvas;
        }
        
        /// <summary>
        /// Get or create the main UI canvas
        /// </summary>
        public Canvas GetMainCanvas()
        {
            Canvas mainCanvas = trackedCanvases.FirstOrDefault(c => c != null && c.name == "MainUICanvas");
            if (mainCanvas == null)
            {
                mainCanvas = CreateCanvas("MainUICanvas", CanvasType.Overlay);
            }
            return mainCanvas;
        }
        
        #endregion
        
        #region UI Creation
        
        /// <summary>
        /// Create a UI element from prefab
        /// </summary>
        public T CreateUI<T>(GameObject prefab, Transform parent = null, string panelName = null) where T : Component
        {
            if (prefab == null)
            {
                Debug.LogError("[UIManager] Prefab is null");
                return null;
            }
            
            if (parent == null)
            {
                parent = GetMainCanvas().transform;
            }
            
            GameObject instance = Instantiate(prefab, parent);
            TrackGameObject(instance);
            
            if (!string.IsNullOrEmpty(panelName))
            {
                namedPanels[panelName] = instance;
            }
            
            return instance.GetComponent<T>();
        }
        
        /// <summary>
        /// Create a simple panel with background
        /// </summary>
        public GameObject CreatePanel(string name, Transform parent = null, Color? backgroundColor = null)
        {
            if (parent == null)
            {
                parent = GetMainCanvas().transform;
            }
            
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            Image background = panel.AddComponent<Image>();
            background.color = backgroundColor ?? new Color(0, 0, 0, 0.8f);
            
            TrackGameObject(panel);
            namedPanels[name] = panel;
            
            return panel;
        }
        
        /// <summary>
        /// Create a text element
        /// </summary>
        public TextMeshProUGUI CreateText(string text, Transform parent, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(parent, false);
            
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(20, 20);
            rect.offsetMax = new Vector2(-20, -20);
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = alignment;
            tmp.fontSize = 24;
            
            TrackGameObject(textObj);
            
            return tmp;
        }
        
        /// <summary>
        /// Create a button
        /// </summary>
        public Button CreateButton(string text, Transform parent, System.Action onClick = null)
        {
            GameObject buttonObj = new GameObject("Button");
            buttonObj.transform.SetParent(parent, false);
            
            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 50);
            
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }
            
            // Add text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 18;
            
            TrackGameObject(buttonObj);
            
            return button;
        }
        
        #endregion
        
        #region Common UI Elements
        
        /// <summary>
        /// Show error message
        /// </summary>
        public void ShowError(string message, float duration = 5f)
        {
            if (errorPanel == null)
            {
                if (errorPanelPrefab != null)
                {
                    errorPanel = CreateUI<GameObject>(errorPanelPrefab, null, "ErrorPanel");
                }
                else
                {
                    // Create simple error panel
                    errorPanel = CreatePanel("ErrorPanel");
                    TextMeshProUGUI errorText = CreateText(message, errorPanel.transform);
                    errorText.color = Color.red;
                }
            }
            
            // Update message if using text component
            TextMeshProUGUI text = errorPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = message;
            }
            
            errorPanel.SetActive(true);
            
            if (duration > 0)
            {
                StartCoroutine(HideAfterDelay(errorPanel, duration));
            }
        }
        
        /// <summary>
        /// Show loading indicator
        /// </summary>
        public void ShowLoading(string message = "Loading...")
        {
            if (loadingPanel == null)
            {
                if (loadingPanelPrefab != null)
                {
                    loadingPanel = CreateUI<GameObject>(loadingPanelPrefab, null, "LoadingPanel");
                }
                else
                {
                    // Create simple loading panel
                    loadingPanel = CreatePanel("LoadingPanel");
                    TextMeshProUGUI loadingText = CreateText(message, loadingPanel.transform);
                }
            }
            
            // Update message if using text component
            TextMeshProUGUI text = loadingPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = message;
            }
            
            loadingPanel.SetActive(true);
        }
        
        /// <summary>
        /// Hide loading indicator
        /// </summary>
        public void HideLoading()
        {
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }
        }
        
        /// <summary>
        /// Update connection status display
        /// </summary>
        public void UpdateConnectionStatus(string status, Color statusColor)
        {
            if (connectionStatusPanel == null)
            {
                if (connectionStatusPrefab != null)
                {
                    connectionStatusPanel = CreateUI<GameObject>(connectionStatusPrefab, null, "ConnectionStatus");
                }
                else
                {
                    // Create simple status panel
                    Canvas canvas = GetMainCanvas();
                    connectionStatusPanel = CreatePanel("ConnectionStatus", canvas.transform);
                    
                    RectTransform rect = connectionStatusPanel.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0, 1);
                    rect.anchorMax = new Vector2(0, 1);
                    rect.pivot = new Vector2(0, 1);
                    rect.sizeDelta = new Vector2(300, 50);
                    rect.anchoredPosition = new Vector2(10, -10);
                    
                    TextMeshProUGUI statusText = CreateText(status, connectionStatusPanel.transform);
                    statusText.fontSize = 16;
                }
            }
            
            // Update status text
            TextMeshProUGUI text = connectionStatusPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = status;
                text.color = statusColor;
            }
            
            connectionStatusPanel.SetActive(true);
        }
        
        #endregion
        
        #region Tracking and Cleanup
        
        /// <summary>
        /// Track a GameObject for cleanup
        /// </summary>
        public void TrackGameObject(GameObject obj)
        {
            if (obj == null) return;
            
            // Add tag
            obj.tag = uiTag;
            
            // Set layer recursively
            SetLayerRecursively(obj, LayerMask.NameToLayer(uiLayer));
            
            // Track object
            if (!trackedObjects.Contains(obj))
            {
                trackedObjects.Add(obj);
            }
            
            // Track Canvas separately if it has one
            Canvas canvas = obj.GetComponent<Canvas>();
            if (canvas != null && !trackedCanvases.Contains(canvas))
            {
                trackedCanvases.Add(canvas);
            }
        }
        
        /// <summary>
        /// Untrack a GameObject
        /// </summary>
        public void UntrackGameObject(GameObject obj)
        {
            if (obj == null) return;
            
            trackedObjects.Remove(obj);
            
            Canvas canvas = obj.GetComponent<Canvas>();
            if (canvas != null)
            {
                trackedCanvases.Remove(canvas);
            }
            
            // Remove from named panels
            string panelName = namedPanels.FirstOrDefault(x => x.Value == obj).Key;
            if (!string.IsNullOrEmpty(panelName))
            {
                namedPanels.Remove(panelName);
            }
        }
        
        /// <summary>
        /// Clean up all tracked UI
        /// </summary>
        public void CleanupAll()
        {
            // Remove all event listeners
            RemoveAllEventListeners();
            
            // Destroy all tracked objects
            foreach (var obj in trackedObjects.ToList())
            {
                if (obj != null)
                {
                    DestroyImmediate(obj);
                }
            }
            
            trackedObjects.Clear();
            trackedCanvases.Clear();
            namedPanels.Clear();
            
            errorPanel = null;
            loadingPanel = null;
            connectionStatusPanel = null;
        }
        
        /// <summary>
        /// Get a named panel
        /// </summary>
        public GameObject GetPanel(string name)
        {
            namedPanels.TryGetValue(name, out GameObject panel);
            return panel;
        }
        
        /// <summary>
        /// Hide a named panel
        /// </summary>
        public void HidePanel(string name)
        {
            GameObject panel = GetPanel(name);
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }
        
        /// <summary>
        /// Show a named panel
        /// </summary>
        public void ShowPanel(string name)
        {
            GameObject panel = GetPanel(name);
            if (panel != null)
            {
                panel.SetActive(true);
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
        
        private void CreateLayerIfNeeded(string layerName)
        {
            // Layers must be created in Unity Editor
            // This is just a placeholder for documentation
        }
        
        private void RemoveAllEventListeners()
        {
            foreach (var obj in trackedObjects)
            {
                if (obj == null) continue;
                
                // Remove button listeners
                Button[] buttons = obj.GetComponentsInChildren<Button>(true);
                foreach (var button in buttons)
                {
                    button.onClick.RemoveAllListeners();
                }
                
                // Remove toggle listeners
                Toggle[] toggles = obj.GetComponentsInChildren<Toggle>(true);
                foreach (var toggle in toggles)
                {
                    toggle.onValueChanged.RemoveAllListeners();
                }
                
                // Remove input field listeners
                InputField[] inputFields = obj.GetComponentsInChildren<InputField>(true);
                foreach (var inputField in inputFields)
                {
                    inputField.onEndEdit.RemoveAllListeners();
                    inputField.onValueChanged.RemoveAllListeners();
                }
                
                // Remove TMP input field listeners
                TMP_InputField[] tmpInputFields = obj.GetComponentsInChildren<TMP_InputField>(true);
                foreach (var inputField in tmpInputFields)
                {
                    inputField.onEndEdit.RemoveAllListeners();
                    inputField.onValueChanged.RemoveAllListeners();
                }
            }
        }
        
        private System.Collections.IEnumerator HideAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
        
        #endregion
        
        public enum CanvasType
        {
            Overlay,
            Camera,
            World
        }
        
        void OnDestroy()
        {
            if (instance == this)
            {
                CleanupAll();
                instance = null;
            }
        }
    }
}