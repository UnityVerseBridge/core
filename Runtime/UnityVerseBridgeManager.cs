using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityVerseBridge.Core.Signaling;
using UnityVerseBridge.Core.UI;
using UnityVerseBridge.Core.Utils;

namespace UnityVerseBridge.Core
{
    /// <summary>
    /// Unified manager for UnityVerseBridge with automatic platform detection
    /// </summary>
    [AddComponentMenu("UnityVerseBridge/UnityVerseBridge Manager")]
    public class UnityVerseBridgeManager : MonoBehaviour
    {
        /// <summary>
        /// Bridge mode for UnityVerseBridge
        /// </summary>
        public enum BridgeMode
        {
            Host,    // Quest/VR - sends video, receives touch
            Client   // Mobile - receives video, sends touch
        }
        #region Editor Tools
        #if UNITY_EDITOR
        [ContextMenu("Remove This Bridge Instance")]
        private void RemoveThisBridge()
        {
            if (UnityEditor.EditorUtility.DisplayDialog("Remove UnityVerseBridge", 
                "Remove this UnityVerseBridge instance and its children?", 
                "Yes", "No"))
            {
                UnityEditor.Undo.DestroyObjectImmediate(gameObject);
            }
        }
        
        [ContextMenu("Run Platform Debugger")]
        private void RunPlatformDebugger()
        {
            var debugger = GetComponent<PlatformDebugger>();
            if (debugger == null)
            {
                debugger = gameObject.AddComponent<PlatformDebugger>();
            }
            debugger.LogPlatformInfo();
        }
        #endif
        #endregion
        
        [Header("Configuration")]
        [SerializeField] private UnityVerseConfig unityVerseConfig;
        [SerializeField] private ConnectionConfig legacyConfig; // For backward compatibility
        
        [Header("Platform-Specific References")]
        [SerializeField] private Camera vrCamera; // Quest only
        [SerializeField] private RawImage videoDisplay; // Mobile only
        [SerializeField] private Canvas questTouchCanvas; // Quest only - for touch visualization
        [SerializeField] private RectTransform mobileTouchArea; // Mobile only - touch input area
        [SerializeField] private GameObject mobileTouchFeedbackLayer; // Mobile only - visual feedback
        
        [Header("Debug")]
        [SerializeField] private bool showDebugUI = true;
        [SerializeField] private OnScreenDebugLogger.DisplayMode debugDisplayMode = OnScreenDebugLogger.DisplayMode.GUI;
        [SerializeField] private bool enableAutoConnect = true; // Missing field for auto connection
        
        // Core components
        private WebRtcManager webRtcManager;
        private UnityVerseErrorHandler errorHandler;
        private UIManager uiManager;
        private OnScreenDebugLogger debugLogger;
        
        // State
        private bool isInitialized = false;
        private bool isConnecting = false;
        private PeerRole detectedRole;
        
        // Events
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;
        
        // Properties
        public UnityVerseConfig Configuration => unityVerseConfig;
        public ConnectionConfig ConnectionConfig => legacyConfig; // Backward compatibility
        public bool IsConnected => webRtcManager != null && webRtcManager.IsSignalingConnected;
        public PeerRole Role => detectedRole;
        public bool IsInitialized => isInitialized;
        public BridgeMode Mode => detectedRole == PeerRole.Host ? BridgeMode.Host : BridgeMode.Client;
        public WebRtcManager WebRtcManager => webRtcManager;
        public Camera QuestStreamCamera => vrCamera;
        public RenderTexture QuestStreamTexture => null; // This should be managed elsewhere or added as a field
        public RawImage MobileVideoDisplay => videoDisplay;
        public Canvas QuestTouchCanvas => questTouchCanvas;
        public RectTransform MobileTouchArea => mobileTouchArea;
        public GameObject MobileTouchFeedbackLayer => mobileTouchFeedbackLayer;
        public bool ShowDebugUI => showDebugUI;
        
        #region Unity Lifecycle
        
        void Reset()
        {
            // Called when component is first added or reset
            #if UNITY_EDITOR
            // Try to find existing config based on platform
            string configPath = "";
            if (UnityEngine.XR.XRSettings.enabled || gameObject.name.ToLower().Contains("quest"))
            {
                configPath = "Assets/UnityVerseBridge/QuestConfig.asset";
            }
            else if (gameObject.name.ToLower().Contains("mobile"))
            {
                configPath = "Assets/UnityVerseBridge/MobileConfig.asset";
            }
            
            if (!string.IsNullOrEmpty(configPath))
            {
                unityVerseConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityVerseConfig>(configPath);
                if (unityVerseConfig != null)
                {
                    Debug.Log($"[UnityVerseBridge] Auto-assigned configuration: {configPath}");
                }
            }
            
            // Clear legacy config reference
            legacyConfig = null;
            #endif
        }
        
        void Awake()
        {
            // Ensure we have a configuration
            if (unityVerseConfig == null && legacyConfig != null)
            {
                // Convert legacy config to new format
                ConvertLegacyConfig();
            }
            
            if (unityVerseConfig == null)
            {
                Debug.LogError("[UnityVerseBridge] No configuration assigned!");
                return;
            }
            
            // Initialize core components
            InitializeComponents();
            
            // Detect role
            detectedRole = unityVerseConfig.DetectedRole;
            LogDebug($"Detected role: {detectedRole}");
        }
        
        void Start()
        {
            if (unityVerseConfig != null && unityVerseConfig.autoConnect)
            {
                StartCoroutine(DelayedConnect());
            }
        }
        
        void OnDestroy()
        {
            // Clean up debug UI if it was created
            if (debugLogger != null && !showDebugUI)
            {
                Destroy(debugLogger);
            }
            
            Disconnect();
        }
        
        void OnApplicationPause(bool pauseStatus)
        {
            #if UNITY_EDITOR
            // In Editor, XR session pause might trigger application exit
            if (pauseStatus && UnityEngine.XR.XRSettings.enabled)
            {
                LogDebug("Application paused with XR enabled in Editor");
            }
            #endif
        }
        
        void OnApplicationFocus(bool hasFocus)
        {
            #if UNITY_EDITOR
            // In Editor, losing focus with XR might cause issues
            if (!hasFocus && UnityEngine.XR.XRSettings.enabled)
            {
                LogDebug("Application lost focus with XR enabled in Editor");
                // Don't disconnect or stop operations - Meta XR Simulator needs this
            }
            #endif
        }
        
        #endregion
        
        #region Initialization
        
        private void InitializeComponents()
        {
            // Error Handler
            errorHandler = UnityVerseErrorHandler.Instance;
            errorHandler.OnConnectionLost.AddListener(HandleConnectionLost);
            errorHandler.OnConnectionRestored.AddListener(HandleConnectionRestored);
            
            // UI Manager - only create instance if showDebugUI is true
            if (showDebugUI)
            {
                uiManager = UIManager.Instance;
                uiManager.UpdateConnectionStatus("Initializing...", Color.yellow);
            }
            
            // Debug Logger - only create if showDebugUI is true
            if (showDebugUI)
            {
                debugLogger = gameObject.AddComponent<OnScreenDebugLogger>();
                debugLogger.DisplayModeProperty = debugDisplayMode;
                debugLogger.FilterKeywords = new string[] { "UnityVerse", "WebRTC", detectedRole.ToString() };
            }
            
            // WebRTC Manager
            webRtcManager = GetComponent<WebRtcManager>();
            if (webRtcManager == null)
            {
                webRtcManager = gameObject.AddComponent<WebRtcManager>();
            }
            
            // Subscribe to WebRTC events
            webRtcManager.OnSignalingConnected += HandleSignalingConnected;
            webRtcManager.OnSignalingDisconnected += HandleSignalingDisconnected;
            webRtcManager.OnSignalingError += HandleSignalingError;
            webRtcManager.OnPeerConnectionEstablished += HandlePeerConnected;
            webRtcManager.OnPeerConnectionFailed += HandlePeerConnectionFailed;
            
            // Initialize platform-specific components
            StartCoroutine(InitializePlatformComponents());
            
            isInitialized = true;
        }
        
        private IEnumerator InitializePlatformComponents()
        {
            // Wait for platform detection
            yield return new WaitForSeconds(0.5f);
            
            switch (detectedRole)
            {
                case PeerRole.Host:
                    InitializeHostComponents();
                    break;
                    
                case PeerRole.Client:
                    InitializeClientComponents();
                    break;
            }
            
            LogDebug($"{detectedRole} components initialized");
        }
        
        private void InitializeHostComponents()
        {
            // Find VR camera if not assigned
            if (vrCamera == null)
            {
                vrCamera = FindVRCamera();
                if (vrCamera == null)
                {
                    vrCamera = Camera.main;
                    LogWarning("VR camera not found, using main camera");
                }
            }
            
            // Add Quest-specific extensions
            if (GetComponent<Extensions.Quest.QuestVideoExtension>() == null)
            {
                gameObject.AddComponent<Extensions.Quest.QuestVideoExtension>();
            }
            
            if (GetComponent<Extensions.Quest.QuestTouchExtension>() == null)
            {
                gameObject.AddComponent<Extensions.Quest.QuestTouchExtension>();
            }
            
            if (GetComponent<Extensions.Quest.QuestHapticExtension>() == null)
            {
                gameObject.AddComponent<Extensions.Quest.QuestHapticExtension>();
            }
        }
        
        private void InitializeClientComponents()
        {
            // Find or create video display
            if (videoDisplay == null)
            {
                videoDisplay = FindVideoDisplay();
                if (videoDisplay == null)
                {
                    LogWarning("Video display not found, creating one");
                    CreateVideoDisplay();
                }
            }
            
            // Add Mobile-specific extensions
            if (GetComponent<Extensions.Mobile.MobileVideoExtension>() == null)
            {
                gameObject.AddComponent<Extensions.Mobile.MobileVideoExtension>();
            }
            
            if (GetComponent<Extensions.Mobile.MobileInputExtension>() == null)
            {
                gameObject.AddComponent<Extensions.Mobile.MobileInputExtension>();
            }
        }
        
        #endregion
        
        #region Connection Management
        
        public void Connect()
        {
            if (!isInitialized)
            {
                LogError("UnityVerseBridge not initialized");
                return;
            }
            
            if (isConnecting || IsConnected)
            {
                LogWarning("Already connecting or connected");
                return;
            }
            
            StartCoroutine(ConnectCoroutine());
        }
        
        public void Disconnect()
        {
            if (webRtcManager != null)
            {
                webRtcManager.Disconnect();
            }
            
            isConnecting = false;
            
            if (showDebugUI)
            {
                uiManager.UpdateConnectionStatus("Disconnected", Color.red);
            }
        }
        
        private IEnumerator ConnectCoroutine()
        {
            isConnecting = true;
            
            if (showDebugUI)
            {
                uiManager.ShowLoading("Connecting...");
                uiManager.UpdateConnectionStatus("Connecting...", Color.yellow);
            }
            
            // Configure WebRTC
            ConfigureWebRtc();
            
            // Get authentication token if required
            if (unityVerseConfig.requireAuthentication)
            {
                yield return StartCoroutine(AuthenticateCoroutine());
            }
            
            // Connect to signaling server
            string url = BuildConnectionUrl();
            LogDebug($"Connecting to: {url}");
            
            // Create appropriate WebSocket adapter
            IWebSocketClient adapter = CreateWebSocketAdapter();
            
            var connectTask = webRtcManager.ConnectToSignaling(adapter, url);
            yield return new WaitUntil(() => connectTask.IsCompleted);
            
            if (connectTask.Exception != null)
            {
                HandleConnectionError(connectTask.Exception);
                isConnecting = false;
                yield break;
            }
            
            // Register with server
            yield return StartCoroutine(RegisterWithServer());
            
            isConnecting = false;
            
            if (showDebugUI)
            {
                uiManager.HideLoading();
            }
        }
        
        private IEnumerator DelayedConnect()
        {
            yield return new WaitForSeconds(1f);
            Connect();
        }
        
        #endregion
        
        #region Configuration
        
        private void ConvertLegacyConfig()
        {
            if (legacyConfig == null) return;
            
            // Create new config from legacy
            unityVerseConfig = ScriptableObject.CreateInstance<UnityVerseConfig>();
            unityVerseConfig.signalingUrl = legacyConfig.signalingServerUrl;
            unityVerseConfig.roomId = legacyConfig.GetRoomId();
            unityVerseConfig.requireAuthentication = legacyConfig.requireAuthentication;
            unityVerseConfig.authKey = legacyConfig.authKey;
            unityVerseConfig.connectionTimeout = legacyConfig.connectionTimeout;
            unityVerseConfig.maxReconnectAttempts = legacyConfig.maxReconnectAttempts;
            unityVerseConfig.enableDebugLogging = legacyConfig.enableDetailedLogging;
            unityVerseConfig.autoConnect = enableAutoConnect;
            
            // Set role based on legacy client type
            if (legacyConfig.clientType == ClientType.Quest)
            {
                unityVerseConfig.roleDetection = RoleDetectionMode.Manual;
                unityVerseConfig.manualRole = PeerRole.Host;
            }
            else if (legacyConfig.clientType == ClientType.Mobile)
            {
                unityVerseConfig.roleDetection = RoleDetectionMode.Manual;
                unityVerseConfig.manualRole = PeerRole.Client;
            }
            
            LogDebug("Converted legacy ConnectionConfig to UnityVerseConfig");
        }
        
        private void ConfigureWebRtc()
        {
            if (webRtcManager == null) return;
            
            // Create WebRtcConfiguration from UnityVerseConfig
            var webRtcConfig = ScriptableObject.CreateInstance<WebRtcConfiguration>();
            
            // Configure WebRTC settings
            webRtcConfig.dataChannelLabel = "data"; // Default data channel label
            webRtcConfig.iceServerUrls = new List<string> { "stun:stun.l.google.com:19302" };
            
            // Set the configuration
            webRtcManager.SetConfiguration(webRtcConfig);
            
            // Set connection config for backward compatibility
            if (legacyConfig != null)
            {
                webRtcManager.SetConnectionConfig(legacyConfig);
            }
            
            // Configure based on new config
            webRtcManager.SetDebugMode(unityVerseConfig.enableDebugLogging);
            
            // Set role
            webRtcManager.SetPeerRole(detectedRole == PeerRole.Host);
        }
        
        private string BuildConnectionUrl()
        {
            string baseUrl = unityVerseConfig.signalingUrl;
            string roomId = unityVerseConfig.roomId;
            
            if (unityVerseConfig.autoGenerateRoomId)
            {
                roomId = GenerateRoomId();
            }
            
            // Add query parameters
            string url = $"{baseUrl}?clientType={detectedRole.ToString().ToLower()}&roomId={roomId}";
            
            // Add token if we have one
            if (!string.IsNullOrEmpty(authToken))
            {
                url += $"&token={authToken}";
            }
            
            return url;
        }
        
        private IWebSocketClient CreateWebSocketAdapter()
        {
            // Use platform-appropriate adapter
            #if UNITY_WEBGL
                return new Signaling.Adapters.WebGLWebSocketAdapter();
            #else
                return new Signaling.Adapters.SystemWebSocketAdapter();
            #endif
        }
        
        #endregion
        
        #region Authentication
        
        private string authToken;
        
        private IEnumerator AuthenticateCoroutine()
        {
            string authUrl = unityVerseConfig.signalingUrl
                .Replace("ws://", "http://")
                .Replace("wss://", "https://") + "/auth";
            
            var authRequest = new AuthRequest
            {
                clientId = GenerateClientId(),
                clientType = detectedRole.ToString().ToLower(),
                authKey = unityVerseConfig.authKey
            };
            
            string jsonPayload = JsonUtility.ToJson(authRequest);
            
            using (var request = new UnityEngine.Networking.UnityWebRequest(authUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
                request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                
                yield return request.SendWebRequest();
                
                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                    authToken = response.token;
                    LogDebug("Authentication successful");
                }
                else
                {
                    string error = $"Authentication failed: {request.error}";
                    LogError(error);
                    errorHandler.ReportError(UnityVerseErrorHandler.ErrorType.Authentication, error);
                    
                    if (showDebugUI)
                    {
                        uiManager.ShowError("Authentication failed. Please check your credentials.", 5f);
                    }
                }
            }
        }
        
        #endregion
        
        #region Server Registration
        
        private IEnumerator RegisterWithServer()
        {
            string peerId = GenerateClientId();
            
            var registerMessage = new Signaling.Messages.RegisterMessage
            {
                peerId = peerId,
                clientType = detectedRole.ToString().ToLower(),
                roomId = unityVerseConfig.roomId
            };
            
            webRtcManager.SendSignalingMessage(registerMessage);
            
            LogDebug($"Registered as {detectedRole} with ID: {peerId}");
            
            // For clients, send ready message after a short delay
            if (detectedRole == PeerRole.Client)
            {
                yield return new WaitForSeconds(0.5f);
                
                var readyMessage = new Signaling.Data.ClientReadyMessage
                {
                    type = "client-ready",
                    peerId = peerId
                };
                
                webRtcManager.SendSignalingMessage(readyMessage);
                LogDebug("Sent client-ready message");
            }
            
            yield return null;
        }
        
        #endregion
        
        #region Event Handlers
        
        private void HandleSignalingConnected()
        {
            LogDebug("Signaling connected");
            
            if (showDebugUI && uiManager != null)
            {
                uiManager.UpdateConnectionStatus($"Connected ({detectedRole})", Color.green);
            }
            
            OnConnected?.Invoke();
        }
        
        private void HandleSignalingDisconnected()
        {
            LogWarning("Signaling disconnected");
            
            if (showDebugUI && uiManager != null)
            {
                uiManager.UpdateConnectionStatus("Disconnected", Color.red);
            }
            
            OnDisconnected?.Invoke();
            errorHandler.ReportConnectionLost();
        }
        
        private void HandleSignalingError(string error)
        {
            LogError($"Signaling error: {error}");
            OnError?.Invoke(error);
            
            errorHandler.ReportError(UnityVerseErrorHandler.ErrorType.Network, error);
        }
        
        private void HandlePeerConnected()
        {
            LogDebug("Peer connection established");
            
            if (showDebugUI && uiManager != null)
            {
                uiManager.UpdateConnectionStatus($"Streaming ({detectedRole})", Color.green);
            }
        }
        
        private void HandlePeerConnectionFailed(string error)
        {
            LogError($"Peer connection failed: {error}");
            
            errorHandler.ReportError(UnityVerseErrorHandler.ErrorType.Connection, error);
        }
        
        private void HandleConnectionLost()
        {
            if (unityVerseConfig.maxReconnectAttempts > 0)
            {
                LogDebug("Connection lost, attempting to reconnect...");
            }
        }
        
        private void HandleConnectionRestored()
        {
            LogDebug("Connection restored");
            
            if (showDebugUI && uiManager != null)
            {
                uiManager.UpdateConnectionStatus($"Connected ({detectedRole})", Color.green);
            }
        }
        
        private void HandleConnectionError(Exception exception)
        {
            string error = exception.InnerException?.Message ?? exception.Message;
            LogError($"Connection failed: {error}");
            
            errorHandler.ReportError(UnityVerseErrorHandler.ErrorType.Connection, error, exception);
            
            if (showDebugUI && uiManager != null)
            {
                uiManager.HideLoading();
                uiManager.ShowError($"Connection failed: {error}", 5f);
                uiManager.UpdateConnectionStatus("Connection Failed", Color.red);
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private Camera FindVRCamera()
        {
            // Look for common VR camera setups
            string[] vrCameraNames = { "CenterEyeAnchor", "Main Camera", "Head", "Camera (eye)" };
            
            foreach (string name in vrCameraNames)
            {
                GameObject cameraObj = GameObject.Find(name);
                if (cameraObj != null)
                {
                    Camera cam = cameraObj.GetComponent<Camera>();
                    if (cam != null && cam.stereoEnabled)
                    {
                        return cam;
                    }
                }
            }
            
            // Check for OVRCameraRig
            try
            {
                var ovrCameraRigType = System.Type.GetType("OVRCameraRig, Oculus.VR");
                if (ovrCameraRigType != null)
                {
                    var rig = FindFirstObjectByType(ovrCameraRigType);
                    if (rig != null)
                    {
                        Camera[] cameras = (rig as Component).GetComponentsInChildren<Camera>();
                        if (cameras.Length > 0)
                        {
                            return cameras[0];
                        }
                    }
                }
            }
            catch { }
            
            return null;
        }
        
        private RawImage FindVideoDisplay()
        {
            // Look for existing video display
            RawImage[] rawImages = FindObjectsByType<RawImage>(FindObjectsSortMode.None);
            foreach (var img in rawImages)
            {
                if (img.name.ToLower().Contains("video") || img.name.ToLower().Contains("stream"))
                {
                    return img;
                }
            }
            
            return null;
        }
        
        private void CreateVideoDisplay()
        {
            Canvas canvas = uiManager.GetMainCanvas();
            
            GameObject displayObj = new GameObject("VideoDisplay");
            displayObj.transform.SetParent(canvas.transform, false);
            
            RectTransform rect = displayObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            videoDisplay = displayObj.AddComponent<RawImage>();
            videoDisplay.color = Color.black;
            
            uiManager.TrackGameObject(displayObj);
        }
        
        private string GenerateRoomId()
        {
            return $"{detectedRole.ToString().ToLower()}-{UnityEngine.Random.Range(1000, 9999)}";
        }
        
        private string GenerateClientId()
        {
            return $"{detectedRole.ToString().ToLower()}_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}_{UnityEngine.Random.Range(1000, 9999):x}";
        }
        
        #endregion
        
        #region Public API
        
        public void SetRoomId(string roomId)
        {
            if (unityVerseConfig != null)
            {
                unityVerseConfig.roomId = roomId;
                unityVerseConfig.autoGenerateRoomId = false;
            }
            
            if (legacyConfig != null)
            {
                legacyConfig.roomId = roomId;
                legacyConfig.useSessionRoomId = false;
            }
        }
        
        public void SetCamera(Camera camera)
        {
            vrCamera = camera;
        }
        
        public void SetVideoDisplay(RawImage display)
        {
            videoDisplay = display;
        }
        
        public Camera GetVRCamera()
        {
            return vrCamera;
        }
        
        public RawImage GetVideoDisplay()
        {
            return videoDisplay;
        }
        
        #endregion
        
        #region Logging
        
        private void LogDebug(string message)
        {
            if (unityVerseConfig != null && unityVerseConfig.enableDebugLogging)
            {
                Debug.Log($"[UnityVerseBridge] {message}");
            }
        }
        
        private void LogWarning(string message)
        {
            Debug.LogWarning($"[UnityVerseBridge] {message}");
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[UnityVerseBridge] {message}");
        }
        
        #endregion
        
        #region Data Classes
        
        [System.Serializable]
        private class AuthRequest
        {
            public string clientId;
            public string clientType;
            public string authKey;
        }
        
        [System.Serializable]
        private class AuthResponse
        {
            public string token;
        }
        
        #endregion
    }
}