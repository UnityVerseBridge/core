using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UnityVerseBridge.Core
{
    public class UnityVerseErrorHandler : MonoBehaviour
    {
        public enum ErrorType
        {
            Connection,
            Authentication,
            Network,
            Media,
            Configuration,
            Permission
        }
        
        public enum ErrorSeverity
        {
            Info,
            Warning,
            Error,
            Critical
        }
        
        [System.Serializable]
        public class ErrorEvent : UnityEvent<ErrorInfo> { }
        
        [System.Serializable]
        public class ErrorInfo
        {
            public ErrorType type;
            public ErrorSeverity severity;
            public string message;
            public string userMessage;
            public string details;
            public DateTime timestamp;
            public bool isRecoverable;
            
            public ErrorInfo(ErrorType type, ErrorSeverity severity, string message, string userMessage = null, bool isRecoverable = true)
            {
                this.type = type;
                this.severity = severity;
                this.message = message;
                this.userMessage = userMessage ?? GetDefaultUserMessage(type, message);
                this.details = "";
                this.timestamp = DateTime.Now;
                this.isRecoverable = isRecoverable;
            }
        }
        
        [Header("Error Events")]
        public ErrorEvent OnErrorOccurred = new ErrorEvent();
        public UnityEvent OnConnectionLost = new UnityEvent();
        public UnityEvent OnConnectionRestored = new UnityEvent();
        
        [Header("UI References")]
        [SerializeField] private GameObject errorPanel;
        [SerializeField] private TMPro.TextMeshProUGUI errorText;
        [SerializeField] private TMPro.TextMeshProUGUI errorDetailText;
        [SerializeField] private GameObject reconnectingIndicator;
        
        [Header("Settings")]
        [SerializeField] private bool showErrorUI = true;
        [SerializeField] private float errorDisplayDuration = 5f;
        [SerializeField] private bool autoRetry = true;
        [SerializeField] private float retryDelay = 2f;
        
        private static UnityVerseErrorHandler instance;
        private Queue<ErrorInfo> errorQueue = new Queue<ErrorInfo>();
        private Coroutine errorDisplayCoroutine;
        private int retryCount = 0;
        private bool isReconnecting = false;
        
        // Common error messages
        private static readonly Dictionary<string, string> ErrorMessages = new Dictionary<string, string>
        {
            {"connection_failed", "Unable to connect to server. Please check your network connection."},
            {"auth_failed", "Authentication failed. Please check your credentials."},
            {"room_full", "The room is full. Please try a different room."},
            {"peer_disconnected", "The other device disconnected."},
            {"network_timeout", "Connection timed out. Please try again."},
            {"permission_denied", "Camera/microphone permission denied."},
            {"invalid_config", "Invalid configuration. Please check your settings."},
            {"server_unreachable", "Cannot reach the server. Please check the server URL."},
            {"video_failed", "Failed to start video stream."},
            {"reconnecting", "Connection lost. Reconnecting..."},
            {"reconnect_failed", "Failed to reconnect after {0} attempts."}
        };
        
        public static UnityVerseErrorHandler Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<UnityVerseErrorHandler>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("UnityVerseErrorHandler");
                        instance = go.AddComponent<UnityVerseErrorHandler>();
                    }
                }
                return instance;
            }
        }
        
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        public void ReportError(ErrorType type, string message, Exception exception = null)
        {
            ErrorSeverity severity = DetermineSeverity(type, exception);
            string userMessage = GetUserFriendlyMessage(message);
            bool isRecoverable = IsErrorRecoverable(type, message);
            
            ErrorInfo error = new ErrorInfo(type, severity, message, userMessage, isRecoverable);
            if (exception != null)
            {
                error.details = exception.ToString();
            }
            
            HandleError(error);
        }
        
        public void ReportConnectionLost()
        {
            if (!isReconnecting)
            {
                isReconnecting = true;
                retryCount = 0;
                
                ReportError(ErrorType.Connection, "reconnecting");
                OnConnectionLost?.Invoke();
                
                if (showErrorUI && reconnectingIndicator != null)
                {
                    reconnectingIndicator.SetActive(true);
                }
                
                if (autoRetry)
                {
                    StartCoroutine(AutoReconnect());
                }
            }
        }
        
        public void ReportConnectionRestored()
        {
            isReconnecting = false;
            retryCount = 0;
            
            OnConnectionRestored?.Invoke();
            
            if (reconnectingIndicator != null)
            {
                reconnectingIndicator.SetActive(false);
            }
            
            ShowNotification("Connection restored!", ErrorSeverity.Info);
        }
        
        private void HandleError(ErrorInfo error)
        {
            // Log based on severity
            switch (error.severity)
            {
                case ErrorSeverity.Info:
                    Debug.Log($"[UnityVerse] {error.message}");
                    break;
                case ErrorSeverity.Warning:
                    Debug.LogWarning($"[UnityVerse] {error.message}");
                    break;
                case ErrorSeverity.Error:
                case ErrorSeverity.Critical:
                    Debug.LogError($"[UnityVerse] {error.message}\n{error.details}");
                    break;
            }
            
            // Invoke error event
            OnErrorOccurred?.Invoke(error);
            
            // Show UI if enabled
            if (showErrorUI)
            {
                errorQueue.Enqueue(error);
                if (errorDisplayCoroutine == null)
                {
                    errorDisplayCoroutine = StartCoroutine(DisplayErrors());
                }
            }
        }
        
        private System.Collections.IEnumerator DisplayErrors()
        {
            while (errorQueue.Count > 0)
            {
                ErrorInfo error = errorQueue.Dequeue();
                ShowErrorUI(error);
                yield return new WaitForSeconds(errorDisplayDuration);
            }
            
            HideErrorUI();
            errorDisplayCoroutine = null;
        }
        
        private void ShowErrorUI(ErrorInfo error)
        {
            if (errorPanel != null)
            {
                errorPanel.SetActive(true);
                
                if (errorText != null)
                {
                    errorText.text = error.userMessage;
                    errorText.color = GetSeverityColor(error.severity);
                }
                
                if (errorDetailText != null && !string.IsNullOrEmpty(error.details))
                {
                    errorDetailText.text = error.details;
                    errorDetailText.gameObject.SetActive(error.severity >= ErrorSeverity.Error);
                }
            }
        }
        
        private void HideErrorUI()
        {
            if (errorPanel != null)
            {
                errorPanel.SetActive(false);
            }
        }
        
        private void ShowNotification(string message, ErrorSeverity severity)
        {
            ErrorInfo notification = new ErrorInfo(ErrorType.Connection, severity, message, message);
            errorQueue.Enqueue(notification);
            
            if (errorDisplayCoroutine == null)
            {
                errorDisplayCoroutine = StartCoroutine(DisplayErrors());
            }
        }
        
        private System.Collections.IEnumerator AutoReconnect()
        {
            UnityVerseBridgeManager bridge = FindFirstObjectByType<UnityVerseBridgeManager>();
            if (bridge == null) yield break;
            
            UnityVerseConfig config = bridge.Configuration;
            int maxAttempts = config != null ? config.maxReconnectAttempts : 3;
            
            while (retryCount < maxAttempts && isReconnecting)
            {
                yield return new WaitForSeconds(retryDelay * (retryCount + 1)); // Exponential backoff
                
                retryCount++;
                Debug.Log($"[UnityVerse] Reconnection attempt {retryCount}/{maxAttempts}");
                
                // Try to reconnect
                bridge.Connect();
                
                // Wait for connection result
                yield return new WaitForSeconds(5f);
                
                if (bridge.IsConnected)
                {
                    ReportConnectionRestored();
                    yield break;
                }
            }
            
            // Max retries reached
            isReconnecting = false;
            if (reconnectingIndicator != null)
            {
                reconnectingIndicator.SetActive(false);
            }
            
            string message = string.Format(ErrorMessages["reconnect_failed"], maxAttempts);
            ReportError(ErrorType.Connection, "reconnect_failed", new Exception(message));
        }
        
        private static string GetUserFriendlyMessage(string technicalMessage)
        {
            // Check for known error keys
            foreach (var kvp in ErrorMessages)
            {
                if (technicalMessage.ToLower().Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }
            
            // Simplify technical messages
            if (technicalMessage.Contains("ECONNREFUSED"))
                return ErrorMessages["server_unreachable"];
            if (technicalMessage.Contains("timeout"))
                return ErrorMessages["network_timeout"];
            if (technicalMessage.Contains("auth") || technicalMessage.Contains("401"))
                return ErrorMessages["auth_failed"];
            
            // Default: make message more readable
            return technicalMessage.Replace("_", " ").Replace("Error:", "").Trim();
        }
        
        private static string GetDefaultUserMessage(ErrorType type, string message)
        {
            switch (type)
            {
                case ErrorType.Connection:
                    return "Connection issue occurred. Please check your network.";
                case ErrorType.Authentication:
                    return "Authentication failed. Please check your credentials.";
                case ErrorType.Network:
                    return "Network error. Please try again.";
                case ErrorType.Media:
                    return "Media streaming issue. Please check permissions.";
                case ErrorType.Configuration:
                    return "Configuration error. Please check settings.";
                case ErrorType.Permission:
                    return "Permission denied. Please grant required permissions.";
                default:
                    return "An error occurred. Please try again.";
            }
        }
        
        private static ErrorSeverity DetermineSeverity(ErrorType type, Exception exception)
        {
            if (exception != null)
            {
                if (exception is UnauthorizedAccessException)
                    return ErrorSeverity.Critical;
                if (exception is TimeoutException)
                    return ErrorSeverity.Warning;
            }
            
            switch (type)
            {
                case ErrorType.Configuration:
                case ErrorType.Permission:
                    return ErrorSeverity.Critical;
                case ErrorType.Authentication:
                case ErrorType.Media:
                    return ErrorSeverity.Error;
                case ErrorType.Connection:
                case ErrorType.Network:
                    return ErrorSeverity.Warning;
                default:
                    return ErrorSeverity.Info;
            }
        }
        
        private static bool IsErrorRecoverable(ErrorType type, string message)
        {
            // Non-recoverable errors
            if (type == ErrorType.Configuration || type == ErrorType.Permission)
                return false;
            
            if (message.Contains("invalid") || message.Contains("missing"))
                return false;
                
            return true;
        }
        
        private Color GetSeverityColor(ErrorSeverity severity)
        {
            switch (severity)
            {
                case ErrorSeverity.Info:
                    return Color.white;
                case ErrorSeverity.Warning:
                    return Color.yellow;
                case ErrorSeverity.Error:
                    return new Color(1f, 0.5f, 0f); // Orange
                case ErrorSeverity.Critical:
                    return Color.red;
                default:
                    return Color.white;
            }
        }
    }
}