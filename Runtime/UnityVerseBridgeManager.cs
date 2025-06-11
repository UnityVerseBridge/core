using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Unity.WebRTC;
using UnityVerseBridge.Core.Signaling;
using UnityVerseBridge.Core.Signaling.Data;
using UnityVerseBridge.Core.Signaling.Adapters;
using UnityVerseBridge.Core.Signaling.Messages;
using UnityVerseBridge.Core.DataChannel.Data;

namespace UnityVerseBridge.Core
{
    /// <summary>
    /// 통합 UnityVerseBridge 매니저 컴포넌트
    /// Host(Quest VR) 또는 Client(Mobile) 모드로 동작할 수 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)] // 다른 컴포넌트보다 먼저 실행
    public class UnityVerseBridgeManager : MonoBehaviour
    {
        #region Enums
        public enum BridgeMode
        {
            Host,   // Quest VR (Offerer, Stream sender)
            Client  // Mobile (Answerer, Stream receiver)
        }

        public enum ConnectionMode
        {
            SinglePeer,  // 1:1 connection (WebRtcManager)
            MultiPeer    // 1:N connection (MultiPeerWebRtcManager)
        }
        #endregion

        #region Configuration
        [Header("Bridge Configuration")]
        [SerializeField] private BridgeMode bridgeMode = BridgeMode.Host;
        [SerializeField] private ConnectionMode connectionMode = ConnectionMode.SinglePeer;
        [SerializeField] private ConnectionConfig connectionConfig;
        [SerializeField] private WebRtcConfiguration webRtcConfiguration;
        
        [Header("Auto Configuration")]
        [SerializeField] private bool autoInitialize = true;
        [SerializeField] private bool autoConnect = true;
        
        [Header("Component Settings")]
        [SerializeField] private bool enableVideo = true;
        [SerializeField] private bool enableAudio = true;
        [SerializeField] private bool enableTouch = true;
        [SerializeField] private bool enableHaptics = true;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        #endregion

        #region Private Fields
        private WebRtcManager webRtcManager;
        private MonoBehaviour webRtcManagerBehaviour;
        private ISignalingClient signalingClient;
        private SystemWebSocketAdapter webSocketAdapter;
        
        // Feature components
        private VideoStreamHandler videoHandler;
        private AudioStreamHandler audioHandler;
        private TouchInputHandler touchHandler;
        private HapticHandler hapticHandler;
        
        private string clientId;
        private bool isInitialized = false;
        #endregion

        #region Unity Events
        [Header("Events")]
        public UnityEngine.Events.UnityEvent OnInitialized;
        public UnityEngine.Events.UnityEvent OnConnected;
        public UnityEngine.Events.UnityEvent OnDisconnected;
        public UnityEngine.Events.UnityEvent<string> OnError;
        #endregion

        #region Properties
        public bool IsInitialized => isInitialized;
        public bool IsConnected => webRtcManager?.IsWebRtcConnected ?? false;
        public BridgeMode Mode => bridgeMode;
        public WebRtcManager WebRtcManager => webRtcManager;
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            // WebRTC.Update() 코루틴 시작 (필수)
            StartCoroutine(WebRTC.Update());
            
            if (autoInitialize)
            {
                StartCoroutine(InitializeAfterDelay());
            }
        }

        void OnDestroy()
        {
            Disconnect();
            CleanupComponents();
        }
        #endregion

        #region Initialization
        private IEnumerator InitializeAfterDelay()
        {
            // 플랫폼별 초기화 지연
            if (Application.platform == RuntimePlatform.OSXEditor || 
                Application.platform == RuntimePlatform.OSXPlayer)
            {
                yield return new WaitForSeconds(0.2f);
            }
            else
            {
                yield return null;
            }
            
            Initialize();
        }

        public void Initialize()
        {
            if (isInitialized)
            {
                LogWarning("Already initialized");
                return;
            }

            try
            {
                ValidateConfiguration();
                GenerateClientId();
                CreateWebRtcManager();
                CreateFeatureComponents();
                SetupSignaling();
                
                isInitialized = true;
                OnInitialized?.Invoke();
                
                if (autoConnect && connectionConfig != null)
                {
                    Connect();
                }
                
                Log("UnityVerseBridge initialized successfully");
            }
            catch (Exception e)
            {
                LogError($"Failed to initialize: {e.Message}");
                OnError?.Invoke(e.Message);
            }
        }

        private void ValidateConfiguration()
        {
            if (connectionConfig == null)
            {
                throw new InvalidOperationException("ConnectionConfig is not assigned");
            }
            
            if (string.IsNullOrEmpty(connectionConfig.signalingServerUrl))
            {
                throw new InvalidOperationException("Signaling server URL is not configured");
            }
        }

        private void GenerateClientId()
        {
            var deviceId = SystemInfo.deviceUniqueIdentifier;
            var prefix = bridgeMode == BridgeMode.Host ? "host" : "client";
            clientId = $"{prefix}_{GenerateHashedId(deviceId)}";
        }

        private string GenerateHashedId(string input)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16).ToLower();
            }
        }

        private void CreateWebRtcManager()
        {
            GameObject managerObject = new GameObject("WebRtcManager_Unified");
            managerObject.transform.SetParent(transform);
            
            // Always use WebRtcManager (it now supports both single and multi-peer modes)
            var manager = managerObject.AddComponent<WebRtcManager>();
            manager.SetRole(bridgeMode == BridgeMode.Host);
            manager.autoStartPeerConnection = false;
            
            if (webRtcConfiguration != null)
            {
                manager.SetConfiguration(webRtcConfiguration);
            }
            
            // Configure for multi-peer mode if needed
            if (connectionMode == ConnectionMode.MultiPeer)
            {
                int maxConnections = connectionConfig != null ? connectionConfig.maxConnections : 5;
                manager.SetMultiPeerMode(true, maxConnections);
            }
            else
            {
                manager.SetMultiPeerMode(false);
            }
            
            webRtcManagerBehaviour = manager;
            webRtcManager = manager;
            
            Log($"Created WebRtcManager in {connectionMode} mode");
        }

        private void CreateFeatureComponents()
        {
            if (enableVideo)
            {
                videoHandler = gameObject.AddComponent<VideoStreamHandler>();
                videoHandler.Initialize(this, webRtcManager, bridgeMode);
            }
            
            if (enableAudio)
            {
                audioHandler = gameObject.AddComponent<AudioStreamHandler>();
                audioHandler.Initialize(this, webRtcManager, bridgeMode);
            }
            
            if (enableTouch)
            {
                touchHandler = gameObject.AddComponent<TouchInputHandler>();
                touchHandler.Initialize(this, webRtcManager, bridgeMode);
            }
            
            if (enableHaptics)
            {
                hapticHandler = gameObject.AddComponent<HapticHandler>();
                hapticHandler.Initialize(this, webRtcManager, bridgeMode);
            }
        }

        private void SetupSignaling()
        {
            webSocketAdapter = new SystemWebSocketAdapter();
            signalingClient = new SignalingClient();
            webRtcManager.SetupSignaling(signalingClient);
        }
        #endregion

        #region Connection Management
        public async void Connect()
        {
            if (!isInitialized)
            {
                LogError("Not initialized. Call Initialize() first.");
                return;
            }

            if (IsConnected)
            {
                LogWarning("Already connected");
                return;
            }

            try
            {
                await ConnectToSignalingServer();
            }
            catch (Exception e)
            {
                LogError($"Connection failed: {e.Message}");
                OnError?.Invoke(e.Message);
            }
        }

        private async Task ConnectToSignalingServer()
        {
            string serverUrl = connectionConfig.signalingServerUrl;
            
            // Authentication if required
            if (connectionConfig.requireAuthentication)
            {
                Log("Authenticating...");
                bool authSuccess = await AuthenticationHelper.AuthenticateAsync(
                    serverUrl,
                    clientId,
                    bridgeMode == BridgeMode.Host ? "host" : "client",
                    connectionConfig.authKey
                );
                
                if (!authSuccess)
                {
                    throw new Exception("Authentication failed");
                }
                
                serverUrl = AuthenticationHelper.AppendTokenToUrl(serverUrl);
            }
            
            await signalingClient.InitializeAndConnect(webSocketAdapter, serverUrl);
            Log("Connected to signaling server");
            
            // Register client
            await RegisterClient();
            
            // Subscribe to signaling events
            signalingClient.OnSignalingMessageReceived += HandleSignalingMessage;
            
            // Additional setup for concrete WebRtcManager
            if (webRtcManagerBehaviour is WebRtcManager concreteManager && bridgeMode == BridgeMode.Host)
            {
                concreteManager.CreatePeerConnection();
                concreteManager.CreateDataChannel();
            }
            
            OnConnected?.Invoke();
        }

        private async Task RegisterClient()
        {
            var registerMessage = new RegisterMessage
            {
                peerId = clientId,
                clientType = bridgeMode == BridgeMode.Host ? "host" : "client",
                roomId = connectionConfig.GetRoomId()
            };
            
            string jsonMessage = JsonUtility.ToJson(registerMessage);
            await webSocketAdapter.SendText(jsonMessage);
            Log($"Registered as {bridgeMode} with room ID: {connectionConfig.GetRoomId()}");
            
            // For multi-peer mode, the WebRtcManager will handle room joining automatically
            // For single-peer mode, we'll start peer connection when a peer joins
        }

        public void Disconnect()
        {
            if (signalingClient != null)
            {
                signalingClient.OnSignalingMessageReceived -= HandleSignalingMessage;
            }
            
            webRtcManager?.Disconnect();
            webSocketAdapter = null;
            signalingClient = null;
            
            OnDisconnected?.Invoke();
        }
        #endregion

        #region Event Handlers
        private void HandleSignalingMessage(string type, string jsonData)
        {
            // Handle based on bridge mode
            if (bridgeMode == BridgeMode.Host)
            {
                HandleHostSignalingMessage(type, jsonData);
            }
            else
            {
                HandleClientSignalingMessage(type, jsonData);
            }
        }

        private void HandleHostSignalingMessage(string type, string jsonData)
        {
            if (type == "peer-joined")
            {
                var peerInfo = JsonUtility.FromJson<PeerJoinedMessage>(jsonData);
                if (peerInfo.role == "client" || peerInfo.role == "mobile")
                {
                    Log($"Client joined: {peerInfo.peerId}");
                    
                    // Trigger negotiation for single peer mode
                    if (webRtcManagerBehaviour is WebRtcManager manager)
                    {
                        StartCoroutine(TriggerNegotiationAfterDelay(manager));
                    }
                }
            }
        }

        private void HandleClientSignalingMessage(string type, string jsonData)
        {
            if (type == "host-disconnected")
            {
                LogWarning("Host disconnected from room");
                // Could trigger reconnection here
            }
        }

        private IEnumerator TriggerNegotiationAfterDelay(WebRtcManager manager)
        {
            yield return new WaitForSeconds(1.0f);
            
            if (!manager.IsNegotiating && manager.GetPeerConnectionState() != RTCPeerConnectionState.Closed)
            {
                manager.StartNegotiation();
            }
        }
        #endregion

        #region Update Loop
        void Update()
        {
            webSocketAdapter?.DispatchMessageQueue();
            signalingClient?.DispatchMessages();
        }
        #endregion

        #region Cleanup
        private void CleanupComponents()
        {
            if (videoHandler != null) Destroy(videoHandler);
            if (audioHandler != null) Destroy(audioHandler);
            if (touchHandler != null) Destroy(touchHandler);
            if (hapticHandler != null) Destroy(hapticHandler);
            
            if (webRtcManagerBehaviour != null)
            {
                Destroy(webRtcManagerBehaviour.gameObject);
            }
        }
        #endregion

        #region Logging
        private void Log(string message)
        {
            if (debugMode)
                Debug.Log($"[UnityVerseBridge] {message}");
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

        #region Public API
        /// <summary>
        /// 비디오 트랙 추가 (Host 모드에서 사용)
        /// </summary>
        public void AddVideoTrack(VideoStreamTrack track)
        {
            if (bridgeMode != BridgeMode.Host)
            {
                LogWarning("AddVideoTrack is only available in Host mode");
                return;
            }
            
            webRtcManager?.AddVideoTrack(track);
        }

        /// <summary>
        /// 오디오 트랙 추가 (Host 모드에서 사용)
        /// </summary>
        public void AddAudioTrack(AudioStreamTrack track)
        {
            webRtcManager?.AddAudioTrack(track);
        }

        /// <summary>
        /// 데이터 채널 메시지 전송
        /// </summary>
        public void SendDataChannelMessage(object messageData)
        {
            webRtcManager?.SendDataChannelMessage(messageData);
        }

        /// <summary>
        /// 터치 데이터 전송 (Client 모드에서 사용)
        /// </summary>
        public void SendTouchData(TouchData touchData)
        {
            if (bridgeMode != BridgeMode.Client)
            {
                LogWarning("SendTouchData is only available in Client mode");
                return;
            }
            
            SendDataChannelMessage(touchData);
        }

        /// <summary>
        /// 햅틱 명령 전송 (Host 모드에서 사용)
        /// </summary>
        public void SendHapticCommand(HapticCommand command)
        {
            if (bridgeMode != BridgeMode.Host)
            {
                LogWarning("SendHapticCommand is only available in Host mode");
                return;
            }
            
            SendDataChannelMessage(command);
        }
        #endregion
    }
}