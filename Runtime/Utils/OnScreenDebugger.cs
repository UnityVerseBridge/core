using UnityEngine;
using System.Collections.Generic;
using System.Text;

namespace UnityVerseBridge.Core.Utils
{
    /// <summary>
    /// On-screen debug log display for runtime debugging on devices
    /// Useful for debugging on devices without easy access to console logs
    /// </summary>
    public class OnScreenDebugger : MonoBehaviour
    {
        private static OnScreenDebugger instance;
        private List<LogEntry> logs = new List<LogEntry>();
        private GUIStyle logStyle;
        private bool showLogs = true;
        private Vector2 scrollPosition;
        
        [Header("Display Settings")]
        [SerializeField] private int maxLogs = 20;
        [SerializeField] private int fontSize = 18;
        [SerializeField] private Color logColor = Color.white;
        [SerializeField] private Color errorColor = Color.red;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private float backgroundAlpha = 0.8f;
        
        [Header("Filter Settings")]
        [SerializeField] private bool showTimestamps = true;
        [SerializeField] private string[] filterKeywords = new string[0];
        [SerializeField] private bool filterExclusive = false; // If true, only logs with keywords are shown
        
        private struct LogEntry
        {
            public string message;
            public LogType type;
            public float timestamp;
            public string formattedMessage;
        }
        
        public static OnScreenDebugger Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("OnScreenDebugger");
                    instance = go.AddComponent<OnScreenDebugger>();
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
            // Apply filtering
            if (filterKeywords != null && filterKeywords.Length > 0)
            {
                bool hasKeyword = false;
                foreach (var keyword in filterKeywords)
                {
                    if (!string.IsNullOrEmpty(keyword) && logString.Contains(keyword))
                    {
                        hasKeyword = true;
                        break;
                    }
                }
                
                if (filterExclusive && !hasKeyword) return;
                if (!filterExclusive && hasKeyword) return;
            }
            
            // Create log entry
            var entry = new LogEntry
            {
                message = logString,
                type = type,
                timestamp = Time.time
            };
            
            // Format the message
            entry.formattedMessage = FormatLogEntry(entry);
            
            // Add to list
            logs.Add(entry);
            
            // Maintain max logs
            while (logs.Count > maxLogs)
            {
                logs.RemoveAt(0);
            }
            
            // Auto-scroll to bottom
            scrollPosition.y = float.MaxValue;
        }
        
        private string FormatLogEntry(LogEntry entry)
        {
            Color color = logColor;
            string prefix = "";
            
            switch (entry.type)
            {
                case LogType.Error:
                case LogType.Exception:
                    color = errorColor;
                    prefix = "[ERROR] ";
                    break;
                case LogType.Warning:
                    color = warningColor;
                    prefix = "[WARN] ";
                    break;
                default:
                    prefix = "[INFO] ";
                    break;
            }
            
            string timestamp = showTimestamps ? $"[{entry.timestamp:F2}] " : "";
            string colorHex = ColorUtility.ToHtmlStringRGB(color);
            
            return $"<color=#{colorHex}>{timestamp}{prefix}{entry.message}</color>";
        }
        
        void OnGUI()
        {
            if (!showLogs) return;
            
            if (logStyle == null)
            {
                logStyle = new GUIStyle(GUI.skin.label);
                logStyle.fontSize = fontSize;
                logStyle.wordWrap = true;
                logStyle.richText = true;
                logStyle.alignment = TextAnchor.UpperLeft;
            }
            
            // Calculate dimensions
            float margin = 10f;
            float buttonHeight = 30f;
            float panelWidth = Screen.width - (margin * 2);
            float panelHeight = Screen.height * 0.4f; // 40% of screen height
            
            // Background
            Color bgColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0, 0, 0, backgroundAlpha);
            GUI.Box(new Rect(margin, margin, panelWidth, panelHeight), "");
            GUI.backgroundColor = bgColor;
            
            // Title bar
            GUI.Box(new Rect(margin, margin, panelWidth, buttonHeight), "Debug Console");
            
            // Toggle button
            float toggleX = Screen.width - margin - 60;
            if (GUI.Button(new Rect(toggleX, margin, 50, buttonHeight - 5), "Hide"))
            {
                showLogs = false;
            }
            
            // Clear button
            if (GUI.Button(new Rect(toggleX - 60, margin, 50, buttonHeight - 5), "Clear"))
            {
                logs.Clear();
            }
            
            // Scroll view for logs
            float scrollY = margin + buttonHeight + 5;
            float scrollHeight = panelHeight - buttonHeight - 10;
            
            GUILayout.BeginArea(new Rect(margin + 5, scrollY, panelWidth - 10, scrollHeight));
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            
            foreach (var log in logs)
            {
                GUILayout.Label(log.formattedMessage, logStyle);
            }
            
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        
        /// <summary>
        /// Toggle the visibility of the debug console
        /// </summary>
        public void ToggleVisibility()
        {
            showLogs = !showLogs;
        }
        
        /// <summary>
        /// Set the visibility of the debug console
        /// </summary>
        public void SetVisibility(bool visible)
        {
            showLogs = visible;
        }
        
        /// <summary>
        /// Clear all logs
        /// </summary>
        public void Clear()
        {
            logs.Clear();
        }
        
        /// <summary>
        /// Set filter keywords
        /// </summary>
        public void SetFilter(string[] keywords, bool exclusive = false)
        {
            filterKeywords = keywords;
            filterExclusive = exclusive;
        }
        
        /// <summary>
        /// Static helper methods for easy logging
        /// </summary>
        public static void Log(string message)
        {
            Debug.Log($"[OnScreenDebug] {message}");
        }
        
        public static void LogError(string message)
        {
            Debug.LogError($"[OnScreenDebug] {message}");
        }
        
        public static void LogWarning(string message)
        {
            Debug.LogWarning($"[OnScreenDebug] {message}");
        }
        
        /// <summary>
        /// Create and show the debugger if it doesn't exist
        /// </summary>
        public static void Show()
        {
            Instance.SetVisibility(true);
        }
        
        /// <summary>
        /// Hide the debugger
        /// </summary>
        public static void Hide()
        {
            if (instance != null)
            {
                instance.SetVisibility(false);
            }
        }
    }
}