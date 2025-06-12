using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using TMPro;

namespace UnityVerseBridge.Core.Utils
{
    /// <summary>
    /// On-screen debug logger for Unity applications
    /// Works on all platforms including VR/AR devices
    /// </summary>
    public class OnScreenDebugLogger : MonoBehaviour
    {
        private static OnScreenDebugLogger instance;
        
        [Header("Display Settings")]
        [SerializeField] private bool enableOnScreenLog = true;
        [SerializeField] private int maxLogCount = 20;
        [SerializeField] private int fontSize = 14;
        [SerializeField] private float logLifetime = 0f; // 0 = permanent
        
        [Header("Filter Settings")]
        [SerializeField] private bool showInfo = true;
        [SerializeField] private bool showWarnings = true;
        [SerializeField] private bool showErrors = true;
        [SerializeField] private string[] filterKeywords = new string[0];
        [SerializeField] private bool filterExclude = false; // false = include only, true = exclude
        
        [Header("Colors")]
        [SerializeField] private Color infoColor = Color.white;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color errorColor = Color.red;
        [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.8f);
        
        [Header("UI Mode")]
        [SerializeField] private DisplayMode displayMode = DisplayMode.GUI;
        [SerializeField] private Canvas uiCanvas; // For UI mode
        [SerializeField] private TextMeshProUGUI logText; // For UI mode
        [SerializeField] private GameObject logPanel; // For UI mode
        
        private List<LogEntry> logs = new List<LogEntry>();
        private GUIStyle logStyle;
        private GUIStyle backgroundStyle;
        private Vector2 scrollPosition;
        private bool isVisible = true;
        
        private class LogEntry
        {
            public string message;
            public LogType type;
            public float timestamp;
            public Color color;
            
            public LogEntry(string message, LogType type, Color color)
            {
                this.message = message;
                this.type = type;
                this.color = color;
                this.timestamp = Time.time;
            }
        }
        
        public enum DisplayMode
        {
            GUI,        // OnGUI (works everywhere including VR)
            UI,         // UI Canvas (better for mobile/AR)
            Both        // Show in both
        }
        
        public static OnScreenDebugLogger Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("OnScreenDebugLogger");
                    instance = go.AddComponent<OnScreenDebugLogger>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }
        
        void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                Application.logMessageReceived += HandleLog;
                
                if (displayMode != DisplayMode.GUI)
                {
                    SetupUIMode();
                }
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }
        
        void OnDestroy()
        {
            if (instance == this)
            {
                Application.logMessageReceived -= HandleLog;
                instance = null;
            }
        }
        
        void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (!enableOnScreenLog) return;
            
            // Filter by type
            if (!ShouldShowLogType(type)) return;
            
            // Filter by keywords
            if (!PassesKeywordFilter(logString)) return;
            
            // Add to log
            Color color = GetLogColor(type);
            LogEntry entry = new LogEntry(FormatLogMessage(logString, type), type, color);
            
            logs.Add(entry);
            
            // Remove old logs
            while (logs.Count > maxLogCount)
            {
                logs.RemoveAt(0);
            }
            
            // Update UI if needed
            if (displayMode != DisplayMode.GUI)
            {
                UpdateUIDisplay();
            }
        }
        
        void Update()
        {
            // Remove expired logs
            if (logLifetime > 0)
            {
                logs.RemoveAll(log => Time.time - log.timestamp > logLifetime);
                
                if (displayMode != DisplayMode.GUI)
                {
                    UpdateUIDisplay();
                }
            }
            
            // Toggle visibility with keyboard shortcut (F12)
            if (Input.GetKeyDown(KeyCode.F12))
            {
                ToggleVisibility();
            }
        }
        
        void OnGUI()
        {
            if (!isVisible || displayMode == DisplayMode.UI) return;
            
            SetupGUIStyles();
            
            // Calculate dimensions
            float width = Screen.width * 0.9f;
            float height = Screen.height * 0.4f;
            float x = Screen.width * 0.05f;
            float y = 10;
            
            // Background
            GUI.Box(new Rect(x - 5, y - 5, width + 10, height + 10), "", backgroundStyle);
            
            // Title bar
            GUI.Box(new Rect(x, y, width, 30), $"Debug Log ({logs.Count})", GUI.skin.box);
            
            // Toggle button
            if (GUI.Button(new Rect(x + width - 60, y + 5, 50, 20), "Hide"))
            {
                isVisible = false;
            }
            
            // Clear button
            if (GUI.Button(new Rect(x + width - 120, y + 5, 50, 20), "Clear"))
            {
                logs.Clear();
            }
            
            // Log area
            GUILayout.BeginArea(new Rect(x, y + 35, width, height - 35));
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            
            foreach (LogEntry log in logs)
            {
                GUI.contentColor = log.color;
                GUILayout.Label(log.message, logStyle);
            }
            
            GUI.contentColor = Color.white;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        
        private void SetupGUIStyles()
        {
            if (logStyle == null)
            {
                logStyle = new GUIStyle(GUI.skin.label);
                logStyle.fontSize = fontSize;
                logStyle.wordWrap = true;
                logStyle.richText = true;
            }
            
            if (backgroundStyle == null)
            {
                backgroundStyle = new GUIStyle(GUI.skin.box);
                Texture2D bgTexture = new Texture2D(1, 1);
                bgTexture.SetPixel(0, 0, backgroundColor);
                bgTexture.Apply();
                backgroundStyle.normal.background = bgTexture;
            }
        }
        
        private void SetupUIMode()
        {
            if (uiCanvas == null)
            {
                // Create canvas
                GameObject canvasGO = new GameObject("DebugLogCanvas");
                canvasGO.transform.SetParent(transform);
                uiCanvas = canvasGO.AddComponent<Canvas>();
                uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                uiCanvas.sortingOrder = 9999;
                
                // Add canvas scaler
                var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                
                // Add raycaster
                canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            
            if (logPanel == null)
            {
                // Create panel
                logPanel = new GameObject("LogPanel");
                logPanel.transform.SetParent(uiCanvas.transform, false);
                
                var rect = logPanel.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.05f, 0.6f);
                rect.anchorMax = new Vector2(0.95f, 0.95f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                
                // Add background
                var image = logPanel.AddComponent<UnityEngine.UI.Image>();
                image.color = backgroundColor;
                
                // Add scroll view
                var scrollView = new GameObject("ScrollView");
                scrollView.transform.SetParent(logPanel.transform, false);
                var scrollRect = scrollView.AddComponent<UnityEngine.UI.ScrollRect>();
                
                var viewport = new GameObject("Viewport");
                viewport.transform.SetParent(scrollView.transform, false);
                viewport.AddComponent<UnityEngine.UI.RectMask2D>();
                
                var content = new GameObject("Content");
                content.transform.SetParent(viewport.transform, false);
                
                // Create text
                logText = content.AddComponent<TextMeshProUGUI>();
                logText.fontSize = fontSize;
                logText.color = infoColor;
                
                // Setup scroll rect
                scrollRect.content = content.GetComponent<RectTransform>();
                scrollRect.viewport = viewport.GetComponent<RectTransform>();
            }
        }
        
        private void UpdateUIDisplay()
        {
            if (logText == null) return;
            
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (LogEntry log in logs)
            {
                string colorHex = ColorUtility.ToHtmlStringRGB(log.color);
                sb.AppendLine($"<color=#{colorHex}>{log.message}</color>");
            }
            
            logText.text = sb.ToString();
        }
        
        private bool ShouldShowLogType(LogType type)
        {
            switch (type)
            {
                case LogType.Log:
                    return showInfo;
                case LogType.Warning:
                    return showWarnings;
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return showErrors;
                default:
                    return true;
            }
        }
        
        private bool PassesKeywordFilter(string message)
        {
            if (filterKeywords == null || filterKeywords.Length == 0)
                return true;
            
            bool hasKeyword = filterKeywords.Any(keyword => 
                !string.IsNullOrEmpty(keyword) && message.ToLower().Contains(keyword.ToLower()));
            
            return filterExclude ? !hasKeyword : hasKeyword;
        }
        
        private Color GetLogColor(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return warningColor;
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return errorColor;
                default:
                    return infoColor;
            }
        }
        
        private string FormatLogMessage(string message, LogType type)
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            string prefix = type switch
            {
                LogType.Warning => "[WARN]",
                LogType.Error => "[ERROR]",
                LogType.Exception => "[EXCEPTION]",
                LogType.Assert => "[ASSERT]",
                _ => "[INFO]"
            };
            
            return $"{timestamp} {prefix} {message}";
        }
        
        public void ToggleVisibility()
        {
            isVisible = !isVisible;
            
            if (logPanel != null)
            {
                logPanel.SetActive(isVisible);
            }
        }
        
        public void Clear()
        {
            logs.Clear();
            UpdateUIDisplay();
        }
        
        public static void Log(string message)
        {
            Debug.Log($"[OnScreen] {message}");
        }
        
        public static void LogWarning(string message)
        {
            Debug.LogWarning($"[OnScreen] {message}");
        }
        
        public static void LogError(string message)
        {
            Debug.LogError($"[OnScreen] {message}");
        }
    }
}