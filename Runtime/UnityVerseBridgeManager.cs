using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityVerseBridge.Core.Signaling;

namespace UnityVerseBridge.Core
{
    /// <summary>
    /// Unified manager for UnityVerseBridge with platform-specific settings
    /// </summary>
    [AddComponentMenu("UnityVerseBridge/UnityVerseBridge Manager")]
    public class UnityVerseBridgeManager : MonoBehaviour
    {
        /// <summary>
        /// Bridge operation mode
        /// </summary>
        public enum BridgeMode
        {
            /// <summary>
            /// Host mode - Used by Quest VR (streams video, receives touch)
            /// </summary>
            Host,
            
            /// <summary>
            /// Client mode - Used by Mobile (receives video, sends touch)
            /// </summary>
            Client
        }
        [Header("Common Settings")]
        [SerializeField] private ConnectionConfig configuration;
        [SerializeField] private WebRtcConfiguration webRtcConfiguration;
        [SerializeField] private bool enableAutoConnect = true;
        [SerializeField] private bool enableDebugLogging = true;
        
        [Header("Quest-Specific References")]
        [SerializeField] private Camera vrCamera;
        [SerializeField] private bool enableVideoStreaming = true;
        [SerializeField] private bool enableTouchReceiving = true;
        [SerializeField] private bool enableHapticFeedback = true;
        [SerializeField] private Canvas touchCanvas;
        
        [Header("Mobile-Specific References")]
        [SerializeField] private RawImage videoDisplay;
        [SerializeField] private bool enableVideoReceiving = true;
        [SerializeField] private bool enableTouchSending = true;
        [SerializeField] private bool enableHapticReceiving = true;
        [SerializeField] private GameObject connectionUI;
        
        // Runtime components
        private WebRtcManager webRtcManager;
        private GameObject bridgeComponents;
        
        // Platform detection
        public bool IsQuestPlatform => Application.platform == RuntimePlatform.Android && IsVREnabled();
        public bool IsMobilePlatform => (Application.platform == RuntimePlatform.Android && !IsVREnabled()) || 
                                       Application.platform == RuntimePlatform.IPhonePlayer;
        
        /// <summary>
        /// Gets the current bridge mode based on platform
        /// </summary>
        public BridgeMode CurrentBridgeMode => IsQuestPlatform ? BridgeMode.Host : BridgeMode.Client;
        
        /// <summary>
        /// Gets the current bridge mode (alias for CurrentBridgeMode)
        /// </summary>
        public BridgeMode Mode => CurrentBridgeMode;
        
        /// <summary>
        /// Gets whether the bridge is initialized
        /// </summary>
        public bool IsInitialized => webRtcManager != null;
        
        /// <summary>
        /// Gets the connection configuration
        /// </summary>
        public ConnectionConfig ConnectionConfig => configuration;
        
        // Quest-specific property accessors
        public Camera QuestStreamCamera => vrCamera;
        public RenderTexture QuestStreamTexture { get; set; }
        public Canvas QuestTouchCanvas => touchCanvas;
        
        // Mobile-specific property accessors
        public RawImage MobileVideoDisplay => videoDisplay;
        public RectTransform MobileTouchArea { get; set; }
        public Canvas MobileTouchFeedbackLayer { get; set; }
        
        void Awake()
        {
            if (configuration == null)
            {
                Debug.LogError("[UnityVerseBridgeManager] ConnectionConfig is required!");
                enabled = false;
                return;
            }
            
            // Auto-detect and set client type based on platform
            if (IsQuestPlatform)
            {
                configuration.clientType = ClientType.Quest;
                LogDebug("[UnityVerseBridgeManager] Detected Quest platform, setting clientType to Quest");
            }
            else if (IsMobilePlatform)
            {
                configuration.clientType = ClientType.Mobile;
                LogDebug("[UnityVerseBridgeManager] Detected Mobile platform, setting clientType to Mobile");
            }
            
            InitializeBridge();
        }
        
        private void InitializeBridge()
        {
            // Create container for bridge components
            bridgeComponents = new GameObject("BridgeComponents");
            bridgeComponents.transform.SetParent(transform);
            
            // Add WebRtcManager
            webRtcManager = bridgeComponents.AddComponent<WebRtcManager>();
            
            // Pass the connection configuration to WebRtcManager
            if (webRtcManager != null)
            {
                // Pass ConnectionConfig first
                webRtcManager.SetConnectionConfig(configuration);
                
                // Then pass WebRtcConfiguration if available
                if (webRtcConfiguration != null)
                {
                    webRtcManager.SetConfiguration(webRtcConfiguration);
                }
            }
            
            // Set debug logging
            if (enableDebugLogging)
            {
                Debug.Log("[UnityVerseBridgeManager] Debug logging is enabled");
                // Enable verbose logging for WebRTC components
                Debug.unityLogger.logEnabled = true;
                Debug.unityLogger.filterLogType = LogType.Log;
            }
            
            // Initialize based on platform
            if (IsQuestPlatform)
            {
                InitializeQuestComponents();
            }
            else if (IsMobilePlatform)
            {
                InitializeMobileComponents();
            }
            else
            {
                Debug.LogWarning("[UnityVerseBridgeManager] Platform not detected. Initializing based on assigned references.");
                
                // Initialize based on what references are assigned
                if (vrCamera != null)
                {
                    InitializeQuestComponents();
                }
                else if (videoDisplay != null)
                {
                    InitializeMobileComponents();
                }
            }
        }
        
        private void InitializeQuestComponents()
        {
            LogDebug("[UnityVerseBridgeManager] Initializing Quest components");
            
            // Add QuestVideoExtension if video streaming is enabled
            if (enableVideoStreaming)
            {
                var videoExtension = bridgeComponents.AddComponent<Extensions.Quest.QuestVideoExtension>();
                // Extension will automatically find the manager and camera
            }
            
            // Add QuestTouchExtension if touch receiving is enabled
            if (enableTouchReceiving)
            {
                var touchExtension = bridgeComponents.AddComponent<Extensions.Quest.QuestTouchExtension>();
                // Extension will automatically find the manager and canvas
            }
            
            // Add QuestHapticExtension if haptic feedback is enabled
            if (enableHapticFeedback)
            {
                var hapticExtension = bridgeComponents.AddComponent<Extensions.Quest.QuestHapticExtension>();
                // Extension will automatically find the manager
            }
            
            // Initialize WebRTC connection
            StartCoroutine(InitializeWebRtcConnection());
        }
        
        private void InitializeMobileComponents()
        {
            LogDebug("[UnityVerseBridgeManager] Initializing Mobile components");
            
            // Add MobileVideoExtension if video receiving is enabled
            if (enableVideoReceiving)
            {
                var videoExtension = bridgeComponents.AddComponent<Extensions.Mobile.MobileVideoExtension>();
                // Extension will automatically find the manager and display
            }
            
            // Add MobileInputExtension if touch sending is enabled
            if (enableTouchSending)
            {
                var inputExtension = bridgeComponents.AddComponent<Extensions.Mobile.MobileInputExtension>();
                // Extension will automatically find the manager
            }
            
            // Add MobileHapticExtension if haptic receiving is enabled
            if (enableHapticReceiving)
            {
                var hapticExtension = bridgeComponents.AddComponent<Extensions.Mobile.MobileHapticExtension>();
                // Extension will automatically find the manager
            }
            
            // Add MobileConnectionUI if provided and auto-connect is disabled
            if (connectionUI != null && !enableAutoConnect)
            {
                var connectionUIComponent = bridgeComponents.AddComponent<Extensions.Mobile.MobileConnectionUI>();
                // Extension will automatically find the manager and UI
            }
            
            // Initialize WebRTC connection
            if (enableAutoConnect)
            {
                StartCoroutine(InitializeWebRtcConnection());
            }
        }
        
        private bool IsVREnabled()
        {
            // First check if we have XR Management available
#if UNITY_XR_MANAGEMENT
            var xrSettings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            if (xrSettings != null && xrSettings.Manager != null && xrSettings.Manager.activeLoader != null)
            {
                return true;
            }
#endif
            
            // Fallback: Check for Quest-specific components using reflection
            try
            {
                // Check for OVRManager
                var ovrManagerType = System.Type.GetType("OVRManager, Oculus.VR");
                if (ovrManagerType != null)
                {
                    var instanceProperty = ovrManagerType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProperty != null)
                    {
                        var instance = instanceProperty.GetValue(null);
                        if (instance != null)
                        {
                            Debug.Log("[UnityVerseBridgeManager] Detected Quest VR through OVRManager");
                            return true;
                        }
                    }
                }
                
                // Check for XRSettings.enabled
                if (UnityEngine.XR.XRSettings.enabled)
                {
                    Debug.Log("[UnityVerseBridgeManager] Detected VR through XRSettings");
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityVerseBridgeManager] Error checking VR status: {e.Message}");
            }
            
            return false;
        }
        
        // Public methods
        public void Connect()
        {
            if (!IsConnected)
            {
                StartCoroutine(InitializeWebRtcConnection());
            }
        }
        
        public void Disconnect()
        {
            if (webRtcManager != null)
            {
                webRtcManager.Disconnect();
            }
        }
        
        public void SetRoomId(string roomId)
        {
            if (configuration != null)
            {
                configuration.roomId = roomId;
                // If using session room ID, reset it to use the new room ID
                if (configuration.useSessionRoomId)
                {
                    configuration.ResetSessionRoomId();
                }
                LogDebug($"[UnityVerseBridgeManager] Room ID set to: {roomId}");
            }
        }
        
        // Properties
        public bool IsConnected => webRtcManager != null && webRtcManager.IsWebRtcConnected;
        public WebRtcManager WebRtcManager => webRtcManager;
        
        // WebRTC Connection initialization coroutine
        private System.Collections.IEnumerator InitializeWebRtcConnection()
        {
            yield return new WaitForSeconds(0.5f); // Wait for components to initialize
            
            if (webRtcManager != null && configuration != null)
            {
                LogDebug("[UnityVerseBridgeManager] Starting WebRTC connection...");
                
                // Create signaling client based on platform
                ISignalingClient signalingClient = null;
                
#if UNITY_WEBGL && !UNITY_EDITOR
                // WebGL would use a different adapter
                LogDebug("[UnityVerseBridgeManager] WebGL platform not supported yet");
                yield break;
#else
                // Use SystemWebSocketAdapter for most platforms
                var adapter = new Signaling.Adapters.SystemWebSocketAdapter();
                signalingClient = new SignalingClient();
#endif
                
                if (signalingClient != null)
                {
                    webRtcManager.SetupSignaling(signalingClient);
                    
                    // Determine client type first
                    string clientType = IsQuestPlatform ? "quest" : "mobile";
                    
                    // Connect to signaling server
                    // Add query parameters for better debugging
                    string serverUrl = configuration.signalingServerUrl;
                    if (!serverUrl.Contains("?"))
                    {
                        serverUrl += "?";
                    }
                    else
                    {
                        serverUrl += "&";
                    }
                    serverUrl += $"clientType={clientType}&roomId={configuration.GetRoomId()}";
                    
                    LogDebug($"[UnityVerseBridgeManager] Connecting to: {serverUrl}");
                    var connectTask = signalingClient.InitializeAndConnect(adapter, serverUrl);
                    yield return new WaitUntil(() => connectTask.IsCompleted);
                    
                    if (connectTask.Exception != null)
                    {
                        Debug.LogError($"[UnityVerseBridgeManager] Failed to connect: {connectTask.Exception}");
                        yield break;
                    }
                    
                    // Register client
                    string peerId = $"{clientType}_{System.Guid.NewGuid().ToString().Substring(0, 16)}";
                    
                    // Get room ID
                    string roomId = configuration.GetRoomId();
                    if (string.IsNullOrEmpty(roomId))
                    {
                        Debug.LogError("[UnityVerseBridgeManager] Room ID is empty! Using default.");
                        roomId = "default-room";
                    }
                    
                    var registerMessage = new Signaling.Messages.RegisterMessage
                    {
                        peerId = peerId,
                        clientType = clientType,
                        roomId = roomId
                    };
                    
                    LogDebug($"[UnityVerseBridgeManager] Sending register message: peerId={peerId}, clientType={clientType}, roomId={roomId}");
                    signalingClient.SendMessage(registerMessage);
                    LogDebug($"[UnityVerseBridgeManager] Register message sent successfully");
                    
                    // For Quest (Offerer), create peer connection immediately
                    if (IsQuestPlatform)
                    {
                        yield return new WaitForSeconds(0.5f);
                        webRtcManager.StartPeerConnection();
                    }
                }
            }
        }
        
        // Debug logging helper
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log(message);
            }
        }
        
        // Helper methods for reflection
        private Type GetTypeByName(string typeName, string namespaceName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType($"{namespaceName}.{typeName}");
                if (type != null) return type;
            }
            Debug.LogWarning($"[UnityVerseBridgeManager] Type {namespaceName}.{typeName} not found. Make sure the required packages are imported.");
            return null;
        }
        
        private void SetFieldValue(Component component, string fieldName, object value)
        {
            if (component == null) return;
            
            var field = component.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(component, value);
            }
            else
            {
                var property = component.GetType().GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(component, value);
                }
            }
        }
    }
}