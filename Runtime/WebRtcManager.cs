using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Unity.WebRTC;
using UnityVerseBridge.Core.Signaling;
using UnityVerseBridge.Core.Signaling.Data;
using UnityVerseBridge.Core.Signaling.Messages;
using UnityVerseBridge.Core.Threading;
using UnityVerseBridge.Core.Utils;

namespace UnityVerseBridge.Core
{
    public class WebRtcManager : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("WebRTC 연결 설정을 담은 객체입니다.")]
        [SerializeField] private WebRtcConfiguration configuration;
        private ConnectionConfig connectionConfig; // Added for reference
        [Tooltip("접속할 시그널링 서버의 주소입니다.")]
        [SerializeField] private string signalingServerUrl = "ws://localhost:8080";
        [Tooltip("이 WebRtcManager 인스턴스가 Offer를 생성하는 역할인지 여부입니다.")]
        [SerializeField] public bool isOfferer = true; // 역할 구분 플래그
        [Tooltip("시그널링 연결 후 자동으로 PeerConnection을 시작할지 여부입니다. Register 완료 후 수동 시작이 필요한 경우 false로 설정하세요.")]
        [SerializeField] public bool autoStartPeerConnection = false; // 자동 시작 옵션
        
        [Header("Multi-Peer Configuration")]
        [Tooltip("Enable multi-peer support (1:N connections)")]
        [SerializeField] private bool enableMultiPeer = false;
        [Tooltip("Maximum number of peer connections (for multi-peer mode)")]
        [SerializeField] private int maxConnections = 5;
        [Tooltip("Current room ID for multi-peer connections")]
        [SerializeField] private string roomId = "default-room";

        [Header("State (Read-only in Inspector)")]
        [SerializeField] private bool _isSignalingConnected = false;
        [SerializeField] private RTCPeerConnectionState _peerConnectionState = RTCPeerConnectionState.New;
        [SerializeField] private RTCDataChannelState _dataChannelState = RTCDataChannelState.Closed;
        [SerializeField] private int _activeConnectionsCount = 0;
        [SerializeField] private List<string> _connectedPeerIds = new List<string>();

        // --- Private Fields ---
        private ISignalingClient signalingClient;
        
        // Single peer mode fields (backward compatibility)
        private RTCPeerConnection peerConnection;
        private RTCDataChannel dataChannel;
        private Coroutine _negotiationCoroutine;
        private bool _isNegotiationCoroutineRunning = false;
        private bool isNegotiating = false;
        private MediaStream sendStream;
        
        // Multi-peer mode fields
        private readonly Dictionary<string, WebRtcConnectionHandler> connectionHandlers = new Dictionary<string, WebRtcConnectionHandler>();
        private MediaStream sharedSendStream; // Shared stream for multi-peer broadcasting
        private readonly List<MediaStreamTrack> localTracks = new List<MediaStreamTrack>();
        
        // Thread safety
        private ThreadSafeQueue<Action> mainThreadActions;
        private readonly object stateLock = new object();
        private bool isApplicationQuitting = false;

        // --- Public Events (IWebRtcManager implementation) ---
        public event Action OnSignalingConnected;
        public event Action OnSignalingDisconnected;
        public event Action<string> OnSignalingError; // Added for UnityVerseBridgeManager
        public event Action OnWebRtcConnected;
        public event Action OnWebRtcDisconnected;
        public event Action OnPeerConnectionEstablished; // Added for UnityVerseBridgeManager
        public event Action<string> OnPeerConnectionFailed; // Added for UnityVerseBridgeManager
        public event Action<string> OnDataChannelOpened;
        public event Action OnDataChannelClosed;
        public event Action<string> OnDataChannelMessageReceived;
        public event Action<MediaStreamTrack> OnTrackReceived;
        
        // MultiPeer events
        public event Action<string> OnPeerConnected;
        public event Action<string> OnPeerDisconnected;
        public event Action<string, string> OnMultiPeerDataChannelMessageReceived;
        public event Action<string, MediaStreamTrack> OnMultiPeerVideoTrackReceived;
        public event Action<string, MediaStreamTrack> OnMultiPeerAudioTrackReceived;
        /// <summary>
        /// 원격 피어로부터 비디오 트랙을 수신했을 때 발생하는 이벤트입니다.
        /// </summary>
        public event Action<MediaStreamTrack> OnVideoTrackReceived; // IWebRtcManager interface compatibility
        /// <summary>
        /// 원격 피어로부터 오디오 트랙을 수신했을 때 발생하는 이벤트입니다.
        /// </summary>
        public event Action<MediaStreamTrack> OnAudioTrackReceived; // IWebRtcManager interface compatibility

        // --- Public Properties ---
        public bool IsSignalingConnected => _isSignalingConnected;
        public bool IsWebRtcConnected
        {
            get
            {
                if (stateLock == null) return false;
                
                lock (stateLock)
                {
                    if (enableMultiPeer)
                    {
                        return _activeConnectionsCount > 0;
                    }
                    return peerConnection?.ConnectionState == RTCPeerConnectionState.Connected;
                }
            }
        }
        public bool IsDataChannelOpen
        {
            get
            {
                lock (stateLock)
                {
                    if (enableMultiPeer)
                    {
                        return connectionHandlers.Any(h => h.Value?.IsDataChannelOpen ?? false);
                    }
                    return dataChannel?.ReadyState == RTCDataChannelState.Open;
                }
            }
        }
        public bool IsNegotiating => isNegotiating;
        public string SignalingServerUrl => signalingServerUrl;
        public int ActiveConnectionsCount => enableMultiPeer ? _activeConnectionsCount : (IsWebRtcConnected ? 1 : 0);
        public List<string> ConnectedPeerIds
        {
            get
            {
                lock (stateLock)
                {
                    if (enableMultiPeer)
                    {
                        return new List<string>(_connectedPeerIds ?? new List<string>());
                    }
                    return IsWebRtcConnected ? new List<string> { "default" } : new List<string>();
                }
            }
        }

        // --- Initialization ---
        void Awake()
        {
            // Initialize thread-safe queue
            mainThreadActions = new ThreadSafeQueue<Action>(500);
            
            // Ensure we have a configuration
            if (configuration == null)
            {
                configuration = ScriptableObject.CreateInstance<WebRtcConfiguration>();
                Debug.Log("[WebRtcManager] Created default WebRtcConfiguration");
            }
            
            // SignalingClient 인스턴스화는 SetupSignaling에서 처리
            if (enableMultiPeer && isOfferer)
            {
                sharedSendStream = new MediaStream();
            }
            
            // Delay WebRTC initialization to avoid issues in Editor
            StartCoroutine(InitializeWebRTC());
        }
        
        private IEnumerator InitializeWebRTC()
        {
            // Wait a frame to ensure proper initialization
            yield return null;
            
            // Initialize WebRTC only if not in batch mode and not exiting
            if (!Application.isBatchMode && !isApplicationQuitting)
            {
                try
                {
                    // Start coroutine for WebRTC update (required)
                    StartCoroutine(WebRTC.Update());
                    Debug.Log("[WebRtcManager] WebRTC Update coroutine started successfully");
                    
                    #if UNITY_EDITOR && UNITY_XR_MANAGEMENT
                    // Check XR status in Editor
                    if (UnityEngine.XR.XRSettings.enabled)
                    {
                        Debug.Log("[WebRtcManager] XR is enabled in Editor. Using Meta XR Simulator or other XR runtime.");
                    }
                    else
                    {
                        Debug.Log("[WebRtcManager] XR is disabled in Editor. Running in standard 3D mode.");
                    }
                    #endif
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WebRtcManager] Failed to start WebRTC Update coroutine: {e.Message}");
                }
            }
        }

        /// <summary>
        /// 외부 Initializer가 호출하여 초기화된 ISignalingClient를 주입하고
        /// 관련 이벤트 구독을 설정합니다.
        /// </summary>
        public void SetupSignaling(ISignalingClient client) // <-- 이 메서드 구현 확인!
        {
            if (this.signalingClient != null && this.signalingClient.IsConnected)
            {
                Debug.LogWarning("[WebRtcManager] SetupSignaling: Disconnecting previous client.");
                _ = DisconnectSignaling();
            }

            this.signalingClient = client ?? throw new ArgumentNullException(nameof(client));
            SubscribeSignalingEvents();

            // Setup 후 자동으로 시그널링 연결 상태를 확인하거나 연결 시도
             // ConnectSignaling(); // ConnectSignaling 로직 재검토 필요
        }

        public void SetConfiguration(WebRtcConfiguration config)
        {
             this.configuration = config ?? new WebRtcConfiguration();
        }
        
        public void SetConnectionConfig(ConnectionConfig config)
        {
            this.connectionConfig = config;
            if (config != null)
            {
                // Update relevant fields from ConnectionConfig
                this.signalingServerUrl = config.signalingServerUrl;
                this.roomId = config.roomId;
                this.isOfferer = config.clientType == ClientType.Quest;
                this.autoStartPeerConnection = false; // Don't auto-start, wait for client-ready message
                Debug.Log($"[WebRtcManager] ConnectionConfig set - URL: {signalingServerUrl}, Room: {roomId}, IsOfferer: {isOfferer}, AutoStart: {autoStartPeerConnection}");
            }
        }

        /// <summary>
        /// 테스트 코드에서 Mock Signaling Client와 설정을 주입하기 위한 메서드입니다.
        /// </summary>
        public void InitializeForTest(ISignalingClient mockClient, WebRtcConfiguration config)
        {
            SetupSignaling(mockClient); // 공통 설정 메서드 사용
            this.configuration = config ?? new WebRtcConfiguration();
            Debug.Log("WebRtcManager Initialized for Test.");
        }

        private void SubscribeSignalingEvents()
        {
            if (signalingClient == null) return;
            UnsubscribeSignalingEvents(); // Prevent duplicates
            signalingClient.OnConnected += HandleSignalingConnected;
            signalingClient.OnDisconnected += HandleSignalingDisconnected;
            signalingClient.OnSignalingMessageReceived += HandleSignalingMessage;
        }

        private void UnsubscribeSignalingEvents()
        {
            if (signalingClient == null) return;
            signalingClient.OnConnected -= HandleSignalingConnected;
            signalingClient.OnDisconnected -= HandleSignalingDisconnected;
            signalingClient.OnSignalingMessageReceived -= HandleSignalingMessage;
        }

        // --- MonoBehaviour Lifecycle ---
        void Update()
        {
            // Process main thread actions from WebRTC callbacks
            ProcessMainThreadActions();
            
            // ISignalingClient 인터페이스에 DispatchMessages 추가했으므로 직접 호출
            signalingClient?.DispatchMessages();
            
            // Update connection state for multi-peer mode
            if (enableMultiPeer)
            {
                lock (stateLock)
                {
                    _activeConnectionsCount = connectionHandlers.Count(p => p.Value?.IsConnected ?? false);
                    _connectedPeerIds = connectionHandlers.Where(p => p.Value?.IsConnected ?? false).Select(p => p.Key).ToList();
                }
            }
        }
        
        private void ProcessMainThreadActions()
        {
            if (mainThreadActions == null) return;
            
            const int maxActionsPerFrame = 10;
            int processedCount = 0;
            
            while (processedCount < maxActionsPerFrame && mainThreadActions.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WebRtcManager] Error processing main thread action: {e.Message}");
                }
                processedCount++;
            }
        }
        
        private void RunOnMainThread(Action action)
        {
            if (action != null)
            {
                mainThreadActions.Enqueue(action);
            }
        }

        void OnApplicationQuit()
        {
            isApplicationQuitting = true;
            Debug.Log("[WebRtcManager] Application is quitting");
        }

        void OnDestroy()
        {
            isApplicationQuitting = true;
            Debug.Log("[WebRtcManager] OnDestroy: Cleaning up...");
            Disconnect();
        }

        // --- Public Control Methods ---
        public Task ConnectSignaling() // 이 메서드는 이제 주로 재연결 용도로 사용될 수 있음
        {
            if (signalingClient == null)
            {
                string errorMsg = "[WebRtcManager] SignalingClient not initialized.";
                Debug.LogError(errorMsg);
                OnSignalingError?.Invoke("SignalingClient not initialized");
                return Task.CompletedTask;
            }
            if (!IsSignalingConnected)
            {
                Debug.Log($"[WebRtcManager] Attempting to connect Signaling: {signalingServerUrl}");
                // InitializeAndConnect를 다시 호출하는 것은 적절하지 않을 수 있음
                // SignalingClient에 재연결 메서드를 만들거나, 새로 Initialize해야 할 수 있음
                // 여기서는 경고만 표시하거나, Initialize를 다시 호출하도록 유도
                Debug.LogWarning("Use InitializeSignaling to connect initially or implement reconnect logic in SignalingClient.");
                // return signalingClient.Connect(signalingServerUrl); // ISignalingClient에 Connect가 남아있다면...
            }
            else
            {
                Debug.Log("[WebRtcManager] Signaling already connected.");
            }
            return Task.CompletedTask;
        }

        public void StartPeerConnection()
        {
            // Answerer는 StartPeerConnection을 호출하면 안됨
            if (!isOfferer)
            {
                Debug.LogWarning("[WebRtcManager] StartPeerConnection called but this peer is Answerer. Ignoring...");
                return;
            }
            
            if (!IsSignalingConnected)
            {
                Debug.LogError("[WebRtcManager] Cannot start peer connection: signaling is not connected.");
                return;
            }
            if (peerConnection != null && peerConnection.ConnectionState != RTCPeerConnectionState.Closed && peerConnection.ConnectionState != RTCPeerConnectionState.Failed)
            {
                Debug.LogWarning($"[WebRtcManager] Peer connection already exists or is in progress (State: {peerConnection.ConnectionState}).");
                return;
            }
            if (_isNegotiationCoroutineRunning) // _negotiationCoroutine 대신 플래그 사용
            {
                Debug.LogWarning("[WebRtcManager] Peer connection process already running.");
                return;
            }

            InternalCreatePeerConnection();
            InternalCreateDataChannel();
            StartNegotiationCoroutine(CreateOfferAndSend());
        }
        
        public RTCPeerConnectionState GetPeerConnectionState()
        {
            return peerConnection != null ? peerConnection.ConnectionState : RTCPeerConnectionState.Closed;
        }

        public void SendDataChannelMessage(object messageData)
        {
            if (messageData == null)
            {
                Debug.LogWarning("[WebRtcManager] Cannot send null message data.");
                return;
            }
            
            // Multi-peer mode: broadcast to all peers
            if (enableMultiPeer)
            {
                string jsonMessage = JsonUtility.ToJson(messageData);
                
                foreach (var kvp in connectionHandlers.Where(h => h.Value.IsDataChannelOpen))
                {
                    try
                    {
                        kvp.Value.SendDataChannelMessage(jsonMessage);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[WebRtcManager] Failed to send message to {kvp.Key}: {e.Message}");
                    }
                }
                return;
            }
            
            // Single-peer mode (legacy)
            if (!IsDataChannelOpen)
            {
                Debug.LogWarning($"[WebRtcManager] Data channel is not open (State: {dataChannel?.ReadyState}). Cannot send message.");
                return;
            }
            try
            {
                string jsonMessage = JsonUtility.ToJson(messageData);
                dataChannel.Send(jsonMessage);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebRtcManager] Failed to serialize/send data channel message: {e.Message}");
            }
        }

        public async void Disconnect()
        {
            Debug.Log("[WebRtcManager] Disconnect called. Cleaning up...");
            
            // Multi-peer mode cleanup
            if (enableMultiPeer)
            {
                // Close all connection handlers
                foreach (var handler in connectionHandlers.Values.ToList())
                {
                    handler.Close();
                }
                connectionHandlers.Clear();
                
                // Dispose all local tracks
                foreach (var track in localTracks)
                {
                    track?.Dispose();
                }
                localTracks.Clear();
                
                // Dispose shared stream
                sharedSendStream?.Dispose();
                sharedSendStream = null;
            }
            else
            {
                // Single-peer mode cleanup
                if (_negotiationCoroutine != null)
                {
                    StopCoroutine(_negotiationCoroutine);
                    _negotiationCoroutine = null;
                    _isNegotiationCoroutineRunning = false;
                }

                // Clean up tracks and senders
                CleanupTracksAndSenders();
                
                // Close data channel
                if (dataChannel != null)
                {
                    dataChannel.OnOpen = null;
                    dataChannel.OnClose = null;
                    dataChannel.OnMessage = null;
                    dataChannel.OnError = null;
                    dataChannel.Close();
                    dataChannel.Dispose();
                    dataChannel = null;
                }
                
                // Close peer connection
                if (peerConnection != null)
                {
                    peerConnection.OnIceCandidate = null;
                    peerConnection.OnIceConnectionChange = null;
                    peerConnection.OnConnectionStateChange = null;
                    peerConnection.OnDataChannel = null;
                    peerConnection.OnTrack = null;
                    peerConnection.OnNegotiationNeeded = null;
                    peerConnection.Close();
                    peerConnection.Dispose();
                    peerConnection = null;
                }
                
                // Dispose send stream
                sendStream?.Dispose();
                sendStream = null;
            }
            
            // Clear main thread action queue
            mainThreadActions.Clear();
            
            // Reset states
            _peerConnectionState = RTCPeerConnectionState.New;
            _dataChannelState = RTCDataChannelState.Closed;
            isNegotiating = false;

            await DisconnectSignaling();
            Debug.Log("[WebRtcManager] Disconnect finished.");
        }
        
        private void CleanupTracksAndSenders()
        {
            if (peerConnection != null)
            {
                // Remove all senders and dispose tracks
                foreach (var kvp in trackSenders)
                {
                    var track = kvp.Key;
                    var sender = kvp.Value;
                    
                    try
                    {
                        peerConnection.RemoveTrack(sender);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[WebRtcManager] Failed to remove track sender: {e.Message}");
                    }
                    
                    track?.Dispose();
                }
            }
            trackSenders.Clear();
        }

        // 트랙 관리를 위한 Dictionary 추가
        private readonly Dictionary<MediaStreamTrack, RTCRtpSender> trackSenders = new Dictionary<MediaStreamTrack, RTCRtpSender>();
        
        // 수신된 트랙을 추적하기 위한 HashSet
        private readonly HashSet<string> receivedTrackIds = new HashSet<string>();

        public void AddVideoTrack(VideoStreamTrack videoTrack)
        {
            if (videoTrack == null)
            {
                Debug.LogError("[WebRtcManager] Cannot add null video track.");
                return;
            }
            
            // Multi-peer mode
            if (enableMultiPeer)
            {
                Debug.Log($"[WebRtcManager] Adding video track in multi-peer mode: {videoTrack.Id}");
                localTracks.Add(videoTrack);
                
                if (isOfferer)
                {
                    // Host: Add to shared stream and all connected peers
                    sharedSendStream.AddTrack(videoTrack);
                    foreach (var kvp in connectionHandlers.Where(p => p.Value.IsConnected))
                    {
                        kvp.Value.AddVideoTrack(videoTrack);
                    }
                }
                else
                {
                    // Client: Add to host connection only
                    var hostHandler = connectionHandlers.Values.FirstOrDefault();
                    if (hostHandler != null && hostHandler.IsConnected)
                    {
                        hostHandler.AddVideoTrack(videoTrack);
                    }
                }
                return;
            }
            
            // Single-peer mode (legacy)
            if (peerConnection == null)
            {
                Debug.LogError("[WebRtcManager] PeerConnection is not initialized. Cannot add track.");
                return;
            }

            Debug.Log($"[WebRtcManager] Adding video track: {videoTrack.Id}");
            
            // Ensure track is enabled
            videoTrack.Enabled = true;
            
            // Log current state before adding track
            Debug.Log($"[WebRtcManager] Current state before adding video track - ConnectionState: {peerConnection.ConnectionState}, SignalingState: {peerConnection.SignalingState}");
            Debug.Log($"[WebRtcManager] Video track info - Enabled: {videoTrack.Enabled}, ReadyState: {videoTrack.ReadyState}");
            
            if (videoTrack.Texture != null)
            {
                Debug.Log($"[WebRtcManager] Video track texture: {videoTrack.Texture.width}x{videoTrack.Texture.height}, Format: {videoTrack.Texture.graphicsFormat}");
            }
            else
            {
                Debug.LogWarning("[WebRtcManager] Video track texture is null!");
            }
            
            RTCRtpSender sender = peerConnection.AddTrack(videoTrack);

            if (sender == null)
            {
                Debug.LogError("[WebRtcManager] Failed to add video track to PeerConnection.");
            }
            else
            {
                trackSenders[videoTrack] = sender; // 트랙과 sender 매핑 저장
                Debug.Log("[WebRtcManager] Video track added successfully to peer connection. Renegotiation will be triggered if connection is established.");
                
                // 비디오 인코딩 설정 최적화 (모바일용)
                // 텍스처가 있는 경우 해상도 정보 사용
                if (videoTrack.Texture != null)
                {
                    WebRtcCodecHelper.ConfigureVideoEncoding(sender, videoTrack.Texture.width, videoTrack.Texture.height, 30);
                }
                else
                {
                    // 기본값 사용 (640x360)
                    WebRtcCodecHelper.ConfigureVideoEncoding(sender, 640, 360, 30);
                }
                
                // Log sender info
                var parameters = sender.GetParameters();
                Debug.Log($"[WebRtcManager] Sender parameters - Encodings count: {parameters.encodings?.Length ?? 0}");
                
                // Log if we're in a state that requires renegotiation
                if (peerConnection.ConnectionState == RTCPeerConnectionState.Connected)
                {
                    Debug.Log("[WebRtcManager] Connection is already established - renegotiation will be needed");
                }
            }
        }

        public void AddAudioTrack(AudioStreamTrack audioTrack)
        {
            if (audioTrack == null)
            {
                Debug.LogError("[WebRtcManager] Cannot add null audio track.");
                return;
            }
            
            // Multi-peer mode
            if (enableMultiPeer)
            {
                Debug.Log($"[WebRtcManager] Adding audio track in multi-peer mode: {audioTrack.Id}");
                localTracks.Add(audioTrack);
                
                if (isOfferer)
                {
                    // Host: Add to shared stream and all connected peers
                    sharedSendStream.AddTrack(audioTrack);
                    foreach (var kvp in connectionHandlers.Where(p => p.Value.IsConnected))
                    {
                        kvp.Value.AddAudioTrack(audioTrack);
                    }
                }
                else
                {
                    // Client: Add to host connection only
                    var hostHandler = connectionHandlers.Values.FirstOrDefault();
                    if (hostHandler != null && hostHandler.IsConnected)
                    {
                        hostHandler.AddAudioTrack(audioTrack);
                    }
                }
                return;
            }
            
            // Single-peer mode (legacy)
            if (peerConnection == null)
            {
                Debug.LogError("[WebRtcManager] PeerConnection is not initialized. Cannot add track.");
                return;
            }

            Debug.Log($"[WebRtcManager] Adding audio track: {audioTrack.Id}");
            RTCRtpSender sender = peerConnection.AddTrack(audioTrack);

            if (sender == null)
            {
                Debug.LogError("[WebRtcManager] Failed to add audio track to PeerConnection.");
            }
            else
            {
                trackSenders[audioTrack] = sender; // 트랙과 sender 매핑 저장
                Debug.Log("[WebRtcManager] Audio track added successfully to peer connection. Renegotiation might be needed.");
            }
        }

        public void RemoveTrack(MediaStreamTrack track)
        {
            if (track == null)
            {
                Debug.LogError("[WebRtcManager] Cannot remove null track.");
                return;
            }
            
            // Multi-peer mode
            if (enableMultiPeer)
            {
                localTracks.Remove(track);
                
                if (isOfferer)
                {
                    sharedSendStream.RemoveTrack(track);
                }
                
                // Remove from all peer connections
                foreach (var handler in connectionHandlers.Values)
                {
                    handler.RemoveTrack(track);
                }
                return;
            }
            
            // Single-peer mode (legacy)
            if (peerConnection == null)
            {
                Debug.LogError("[WebRtcManager] Cannot remove track: PeerConnection is null.");
                return;
            }

            if (trackSenders.TryGetValue(track, out RTCRtpSender sender))
            {
                peerConnection.RemoveTrack(sender);
                trackSenders.Remove(track);
                Debug.Log($"[WebRtcManager] Removed track: {track.Id}");
            }
            else
            {
                Debug.LogWarning($"[WebRtcManager] Track {track.Id} not found in senders.");
            }
        }

        public async Task StartSignalingAndPeerConnection(string newServerUrl)
        {
            if (signalingClient == null)
            {
                Debug.LogError("[WebRtcManager] StartSignalingAndPeerConnection: SignalingClient is not set. Call SetupSignaling first.");
                return;
            }

            // 이미 연결 시도 중이거나 WebRTC까지 연결된 경우 중복 실행 방지
            if ( (signalingClient.IsConnected && _isSignalingConnected) || _isNegotiationCoroutineRunning || IsWebRtcConnected)
            {
                Debug.LogWarning($"[WebRtcManager] StartSignalingAndPeerConnection: Already connected or negotiation in progress. Current SignalingClient.IsConnected: {signalingClient.IsConnected}, _isSignalingConnected(ManagerFlag): {_isSignalingConnected}, CoroutineRunning: {_isNegotiationCoroutineRunning}, WebRTC Connected: {IsWebRtcConnected}");
                if(signalingClient.IsConnected && _isSignalingConnected) OnSignalingConnected?.Invoke(); // 이미 시그널링 연결된 경우, 연결 후 로직 실행 유도
                return;
            }
            
            this.signalingServerUrl = newServerUrl; // 내부 URL 업데이트
            try
            {
                // ISignalingClient의 Connect를 호출.
                // 성공하면 OnSignalingConnected 이벤트가 발생하고, HandleSignalingConnected에서 StartPeerConnection()이 호출됨.
                await signalingClient.Connect(newServerUrl);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebRtcManager] StartSignalingAndPeerConnection: Exception during signalingClient.Connect: {e.Message}");
            }
        }

        private async Task DisconnectSignaling()
        {
            if (signalingClient != null)
            {
                UnsubscribeSignalingEvents();
                await signalingClient.Disconnect();
                // Reset state after disconnect
                _isSignalingConnected = false;
                signalingClient = null; // 참조 해제하여 다음 Initialize 가능하게
                Debug.Log("[WebRtcManager] SignalingClient disconnected and cleaned up.");
            }
        }

        // --- Public WebRTC Setup Methods ---
        public void CreatePeerConnection()
        {
            if (peerConnection != null && peerConnection.ConnectionState != RTCPeerConnectionState.Closed && peerConnection.ConnectionState != RTCPeerConnectionState.Failed)
            {
                Debug.LogWarning($"[WebRtcManager] PeerConnection already exists (State: {peerConnection.ConnectionState}). Not creating new one.");
                return;
            }
            InternalCreatePeerConnection();
        }
        
        public void CreateDataChannel()
        {
            InternalCreateDataChannel();
        }
        
        public void StartNegotiation()
        {
            if (!isOfferer)
            {
                Debug.LogWarning("[WebRtcManager] StartNegotiation called but this peer is Answerer. Ignoring...");
                return;
            }
            
            if (peerConnection == null)
            {
                Debug.LogError("[WebRtcManager] Cannot start negotiation: PeerConnection is null.");
                return;
            }
            
            if (_isNegotiationCoroutineRunning)
            {
                Debug.LogWarning("[WebRtcManager] Negotiation already in progress.");
                return;
            }
            
            StartNegotiationCoroutine(CreateOfferAndSend());
        }
        
        // --- Private WebRTC Setup ---
        private void InternalCreatePeerConnection()
        {
            if (peerConnection != null)
            {
                Debug.LogWarning("PeerConnection exists. Cleaning up previous.");
                CleanupPeerConnection();
            }
            var rtcConfig = configuration.ToRTCConfiguration(); // 확장 메서드 사용
            peerConnection = new RTCPeerConnection(ref rtcConfig);
            _peerConnectionState = RTCPeerConnectionState.New;
            // 이벤트 핸들러 등록
            peerConnection.OnIceCandidate = HandleIceCandidateGenerated;
            peerConnection.OnIceConnectionChange = HandleIceConnectionChange;
            peerConnection.OnConnectionStateChange = HandleConnectionStateChange;
            peerConnection.OnDataChannel = HandleDataChannelReceived;
            peerConnection.OnTrack = HandleTrackReceived;
            peerConnection.OnNegotiationNeeded = HandleNegotiationNeeded;
            
            // 모바일 호환성을 위한 코덱 설정
            WebRtcCodecHelper.ConfigureCodecPreferences(peerConnection);
            Debug.Log("[WebRtcManager] Configured codec preferences for mobile compatibility");
        }
        
        private void CleanupPeerConnection()
        {
            if (peerConnection != null)
            {
                // Clean up tracks first
                CleanupTracksAndSenders();
                
                // Remove event handlers
                peerConnection.OnIceCandidate = null;
                peerConnection.OnIceConnectionChange = null;
                peerConnection.OnConnectionStateChange = null;
                peerConnection.OnDataChannel = null;
                peerConnection.OnTrack = null;
                peerConnection.OnNegotiationNeeded = null;
                
                // Close and dispose
                peerConnection.Close();
                peerConnection.Dispose();
                peerConnection = null;
                
                // Clear received track IDs
                receivedTrackIds.Clear();
            }
        }

        private void InternalCreateDataChannel()
        {
            if (peerConnection == null)
            {
                Debug.LogError("Cannot create DataChannel: PeerConnection is null.");
                return;
            }
            if (dataChannel != null && dataChannel.ReadyState != RTCDataChannelState.Closed)
            {
                Debug.LogWarning("DataChannel already exists and is not closed.");
                return;
            }
            try
            {
                RTCDataChannelInit options = new RTCDataChannelInit() { ordered = true };
                string channelLabel = configuration?.dataChannelLabel ?? "data";
                dataChannel = peerConnection.CreateDataChannel(channelLabel, options);
                _dataChannelState = RTCDataChannelState.Connecting;
                SetupDataChannelEvents(dataChannel);
                Debug.Log($"[WebRtcManager] Created data channel: {channelLabel}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebRtcManager] Failed to create data channel: {e.Message}");
            }
        }

        private void SetupDataChannelEvents(RTCDataChannel channel)
        {
            if (channel == null) return;
            channel.OnOpen = null;
            channel.OnClose = null;
            channel.OnMessage = null;
            channel.OnError = null; // 이전 핸들러 확실히 제거
            channel.OnOpen = () =>
            {
                _dataChannelState = RTCDataChannelState.Open;
                Debug.Log($"[WebRtcManager] Data Channel '{channel.Label}' Opened!");
                OnDataChannelOpened?.Invoke(channel.Label);
            };
            channel.OnClose = () =>
            {
                _dataChannelState = RTCDataChannelState.Closed;
                Debug.Log($"[WebRtcManager] Data Channel '{channel.Label}' Closed!");
                OnDataChannelClosed?.Invoke();
                if (dataChannel == channel) dataChannel = null;
            }; // 현재 채널이면 참조 해제
            channel.OnMessage = (bytes) =>
            {
                var message = System.Text.Encoding.UTF8.GetString(bytes);
                OnDataChannelMessageReceived?.Invoke(message);
            };
            channel.OnError = (error) =>
            {
                Debug.LogError($"[WebRtcManager] Data Channel Error: {error}");
            };
        }

        // --- Signaling Event Handlers ---
        private void HandleSignalingConnected()
        {
            _isSignalingConnected = true;
            
            // Multi-peer mode: Join room
            if (enableMultiPeer)
            {
                var joinMessage = new JoinRoomMessage(roomId, isOfferer ? "Host" : "Client", maxConnections);
                _ = signalingClient.SendMessage(joinMessage);
            }
            else if (isOfferer && autoStartPeerConnection)
            {
                StartPeerConnection(); // Single-peer mode: Offerer이고 autoStart가 true일 경우에만 자동으로 PeerConnection 시작
            }
            
            // Fire event after PeerConnection setup to ensure proper initialization order
            OnSignalingConnected?.Invoke();
        }

        private void HandleSignalingDisconnected()
        {
            _isSignalingConnected = false;
            Debug.LogWarning("[WebRtcManager] Signaling Disconnected!");
            OnSignalingDisconnected?.Invoke();
        }

        /// <summary>
        /// 시그널링 서버로부터 받은 메시지를 처리합니다.
        /// Offer/Answer/ICE candidate 등 WebRTC 연결에 필요한 정보를 교환합니다.
        /// </summary>
        private void HandleSignalingMessage(string type, string jsonData)
        {
            // Multi-peer mode messages
            if (enableMultiPeer)
            {
                switch (type)
                {
                    case "peer-joined":
                        HandleMultiPeerJoined(jsonData);
                        return;
                    case "peer-left":
                        HandleMultiPeerLeft(jsonData);
                        return;
                    case "offer":
                        HandleMultiPeerOffer(jsonData);
                        return;
                    case "answer":
                        HandleMultiPeerAnswer(jsonData);
                        return;
                    case "ice-candidate":
                        HandleMultiPeerIceCandidate(jsonData);
                        return;
                }
            }
            
            // Handle error messages
            if (type == "error")
            {
                var errorData = JsonUtility.FromJson<ErrorMessage>(jsonData);
                Debug.LogError($"[WebRtcManager] Server error: {errorData.error} (context: {errorData.context})");
                
                // If error is about room already having a host, disconnect and notify
                if (errorData.error.Contains("already has a host"))
                {
                    Debug.LogError("[WebRtcManager] Room already has a host. Please try a different room.");
                    Disconnect();
                }
                return;
            }
            
            // Handle client-ready message (sent when a client joins the room)
            if (type == "client-ready")
            {
                if (isOfferer)
                {
                    var clientReadyData = JsonUtility.FromJson<ClientReadyMessage>(jsonData);
                    Debug.Log($"[WebRtcManager] Client ready to receive video: {clientReadyData.peerId}");
                    
                    // Start peer connection process when client is ready
                    Debug.Log("[WebRtcManager] Starting peer connection for client");
                    StartPeerConnection();
                }
                return;
            }
            
            // Server-specific messages should be ignored regardless of PeerConnection state
            if (type == "joined-room" || type == "peer-joined" || type == "peer-left" || 
                type == "host-disconnected")
            {
                Debug.Log($"[WebRtcManager] Received server message '{type}' - ignoring (handled by app initializer)");
                return;
            }
            
            // Offerer는 자신이 먼저 PeerConnection을 생성하므로, offer를 받을 일이 없음
            if (peerConnection == null && type != "offer" && isOfferer)
            {
                Debug.LogWarning($"[WebRtcManager] Received '{type}' before PeerConnection init (Offerer). Ignoring.");
                return;
            }
            
            // Answerer는 상대방의 Offer를 받고 나서 PeerConnection을 생성
            if (peerConnection == null && type == "offer" && !isOfferer)
            {
                InternalCreatePeerConnection(); 
                // DataChannel은 Offer의 SDP에 포함되어 있으므로,
                // Answerer는 OnDataChannel 콜백을 통해 자동으로 받게 됨
            }

            if (_isNegotiationCoroutineRunning && (type == "offer" || type == "answer"))
            {
                Debug.LogWarning($"[WebRtcManager] Receiving '{type}' while another negotiation is running. Stopping previous.");
                if (_negotiationCoroutine != null) StopCoroutine(_negotiationCoroutine);
                _isNegotiationCoroutineRunning = false; 
                _negotiationCoroutine = null;
            }

            try
            {
                switch (type)
                {
                    case "offer":
                        if (!isOfferer) // 자신이 Answerer일 경우에만 Offer 처리
                        {
                            if (peerConnection == null) InternalCreatePeerConnection(); // 방어 코드
                            
                            // Check signaling state for renegotiation
                            if (peerConnection != null && peerConnection.SignalingState != RTCSignalingState.Stable)
                            {
                                Debug.LogWarning($"[WebRtcManager] Received offer but signaling state is not stable (Current: {peerConnection.SignalingState}). Queueing for later processing.");
                                // TODO: Implement offer queueing mechanism for when signaling state becomes stable
                                break;
                            }
                            
                            Debug.Log($"[WebRtcManager] Processing offer. Current signaling state: {peerConnection?.SignalingState}");
                            StartNegotiationCoroutine(HandleOfferAndSendAnswer(JsonUtility.FromJson<SessionDescriptionMessage>(jsonData)));
                        }
                        else
                        {
                            Debug.LogWarning("[WebRtcManager] Offerer received an Offer. Ignoring (Glare handling not implemented).");
                        }
                        break;
                    case "answer":
                        if (isOfferer) // 자신이 Offerer일 경우에만 Answer 처리
                        {
                            StartNegotiationCoroutine(HandleAnswer(JsonUtility.FromJson<SessionDescriptionMessage>(jsonData)));
                        }
                        else
                        {
                            Debug.LogWarning("[WebRtcManager] Answerer received an Answer. Ignoring.");
                        }
                        break;
                    case "ice-candidate":
                        HandleIceCandidate(JsonUtility.FromJson<IceCandidateMessage>(jsonData));
                        break;
                    default:
                        Debug.LogWarning($"[WebRtcManager] Unknown signaling message type: {type}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebRtcManager] Error handling signaling message (Type: {type}): {e.Message}\nData: {jsonData}");
                _isNegotiationCoroutineRunning = false; 
                _negotiationCoroutine = null;
            }
        }
        
        // --- Helper to manage negotiation coroutine ---
        /// <summary>
        /// SDP 협상 코루틴을 안전하게 시작합니다.
        /// 동시에 여러 협상이 실행되는 것을 방지합니다.
        /// </summary>
        /// <param name="coroutineLogic">실행할 협상 로직 (Offer/Answer 생성 등)</param>
        private void StartNegotiationCoroutine(IEnumerator coroutineLogic)
        {
            // 중복 협상 방지 - WebRTC는 동시에 하나의 협상만 진행 가능
            if (_isNegotiationCoroutineRunning)
            {
                Debug.LogWarning("[WebRtcManager] Attempted to start a new negotiation while one is already running. Aborting new one.");
                return;
            }
            _negotiationCoroutine = StartCoroutine(ExecuteNegotiation(coroutineLogic));
        }

        private IEnumerator ExecuteNegotiation(IEnumerator coroutineLogic)
        {
            _isNegotiationCoroutineRunning = true;
            yield return coroutineLogic; 
            _isNegotiationCoroutineRunning = false;
            _negotiationCoroutine = null; // 참조 해제
        }

        // --- WebRTC Logic Coroutines ---
        /// <summary>
        /// WebRTC Offer를 생성하고 시그널링 서버를 통해 전송합니다.
        /// Offerer(발신자)가 연결을 시작할 때 호출됩니다.
        /// </summary>
        private IEnumerator CreateOfferAndSend()
        {
            if (peerConnection == null)
            {
                Debug.LogError("PC null in CreateOffer");
                yield break;
            }
            var offerOp = peerConnection.CreateOffer();
            yield return offerOp;
            if (offerOp.IsError)
            {
                Debug.LogError($"Failed OfferOp: {offerOp.Error.message}");
                yield break;
            }
            var offerDesc = offerOp.Desc;
            var localDescOp = peerConnection.SetLocalDescription(ref offerDesc);
            yield return localDescOp;
            if (localDescOp.IsError)
            {
                Debug.LogError($"Failed LocalDesc(Offer): {localDescOp.Error.message}");
                yield break;
            }
            try
            {
                // 모바일 호환성을 위해 SDP 최적화
                string optimizedSdp = WebRtcCodecHelper.OptimizeSdpForMobile(offerDesc.sdp);
                var offerMsg = new SessionDescriptionMessage("offer", optimizedSdp);
                _ = signalingClient.SendMessage(offerMsg); // await 제거!
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending offer: {e.Message}");
            }
        }

        private IEnumerator HandleOfferAndSendAnswer(SessionDescriptionMessage offerMessage)
        {
            if (peerConnection == null)
            {
                Debug.LogError("PC null in HandleOffer");
                yield break;
            }
            
            // Log the current state before processing
            Debug.Log($"[WebRtcManager] HandleOfferAndSendAnswer - Current state: ConnectionState={peerConnection.ConnectionState}, SignalingState={peerConnection.SignalingState}");
            
            // Check if this is a renegotiation (connection already established)
            bool isRenegotiation = peerConnection.ConnectionState == RTCPeerConnectionState.Connected;
            if (isRenegotiation)
            {
                Debug.Log("[WebRtcManager] Processing renegotiation offer (connection already established)");
                
                // Log current receivers before processing offer
                var currentReceivers = peerConnection.GetReceivers();
                Debug.Log($"[WebRtcManager] Current receivers before renegotiation: {currentReceivers?.Count() ?? 0}");
                foreach (var receiver in currentReceivers ?? new RTCRtpReceiver[0])
                {
                    if (receiver.Track != null)
                    {
                        Debug.Log($"[WebRtcManager] Existing receiver track: Kind={receiver.Track.Kind}, ID={receiver.Track.Id}");
                    }
                }
            }
            
            RTCSessionDescription offerDesc = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = offerMessage.sdp };
            Debug.Log($"[WebRtcManager] Setting remote description (offer). SDP length: {offerMessage.sdp.Length}");
            
            var remoteDescOp = peerConnection.SetRemoteDescription(ref offerDesc);
            yield return remoteDescOp;
            if (remoteDescOp.IsError)
            {
                Debug.LogError($"Failed RemoteDesc(Offer): {remoteDescOp.Error.message}");
                yield break;
            }
            Debug.Log("[WebRtcManager] Remote description (offer) set successfully");
            
            var answerOp = peerConnection.CreateAnswer();
            yield return answerOp;
            if (answerOp.IsError)
            {
                Debug.LogError($"Failed CreateAnswer: {answerOp.Error.message}");
                yield break;
            }
            Debug.Log("[WebRtcManager] Answer created successfully");
            
            var answerDesc = answerOp.Desc;
            var localDescOp = peerConnection.SetLocalDescription(ref answerDesc);
            yield return localDescOp;
            if (localDescOp.IsError)
            {
                Debug.LogError($"Failed LocalDesc(Answer): {localDescOp.Error.message}");
                yield break;
            }
            Debug.Log("[WebRtcManager] Local description (answer) set successfully");
            
            try
            {
                // 모바일 호환성을 위해 SDP 최적화
                string optimizedSdp = WebRtcCodecHelper.OptimizeSdpForMobile(answerDesc.sdp);
                var answerMsg = new SessionDescriptionMessage("answer", optimizedSdp);
                _ = signalingClient.SendMessage(answerMsg); // await 제거!
                Debug.Log($"[WebRtcManager] Answer sent successfully. IsRenegotiation: {isRenegotiation}");
                
                // For renegotiation, check if there are any received tracks that didn't trigger OnTrack event
                if (isRenegotiation)
                {
                    Debug.Log("[WebRtcManager] Scheduling track check after renegotiation...");
                    StartCoroutine(CheckTracksAfterDelay());
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending answer: {e.Message}");
            }
        }

        private IEnumerator HandleAnswer(SessionDescriptionMessage answerMessage)
        {
            if (peerConnection == null)
            {
                Debug.LogError("PC null in HandleAnswer");
                yield break;
            }
            RTCSessionDescription answerDesc = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = answerMessage.sdp };
            var remoteDescOp = peerConnection.SetRemoteDescription(ref answerDesc);
            yield return remoteDescOp;
            // --- 구조체 null 비교 오류 수정 ---
            if (remoteDescOp.IsError)
            {
                Debug.LogError($"Failed RemoteDesc(Answer): {remoteDescOp.Error.message}");
            }

            // --- 수정 완료 ---
        }

        // --- PeerConnection Event Handlers ---
        private void HandleIceCandidate(IceCandidateMessage candidateMessage)
        {
            if (candidateMessage == null)
            {
                Debug.LogWarning("[WebRtcManager] Received null ICE candidate message.");
                return;
            }
            
            if (peerConnection == null || string.IsNullOrEmpty(candidateMessage.candidate))
            {
                Debug.LogWarning("[WebRtcManager] PeerConnection is null or candidate is empty. Cannot add ICE candidate.");
                return;
            }

            // "a=end-of-candidates"는 빈 candidate 문자열로 처리될 수 있음 (Unity WebRTC 패키지에 따라 다름)
            // 여기서는 명시적으로 빈 candidate를 허용하지 않음 (필요 시 변경)
            if (candidateMessage.candidate == "a=end-of-candidates")
            {
                 Debug.Log("[WebRtcManager] Received end-of-candidates marker. No action needed for AddIceCandidate.");
                 return;
            }

            RTCIceCandidateInit candidateInit = new RTCIceCandidateInit
            {
                candidate = candidateMessage.candidate,
                sdpMid = candidateMessage.sdpMid,
                sdpMLineIndex = candidateMessage.sdpMLineIndex
            };
            
            try
            {
                // RTCIceCandidate 객체로 변환하여 전달
                RTCIceCandidate rtcIceCandidate = new RTCIceCandidate(candidateInit);
                peerConnection.AddIceCandidate(rtcIceCandidate);
            }
            catch (Exception e)
            {
                 Debug.LogError($"[WebRtcManager] Exception adding ICE: {e.GetType().Name}, candidate:{candidateMessage.candidate}\nsdpMid:{candidateMessage.sdpMid}\nsdpMLineIndex:{candidateMessage.sdpMLineIndex}\nError: {e.Message}");
            }
        }

        private void HandleIceCandidateGenerated(RTCIceCandidate candidate)
        {
            if (signalingClient == null || !IsSignalingConnected)
            {
                Debug.LogWarning("[WebRtcManager] Signaling client is null or not connected when ICE candidate was generated. Candidate not sent.");
                return;
            }
            if (candidate != null && !string.IsNullOrEmpty(candidate.Candidate))
            {
                var iceMsg = new IceCandidateMessage(
                    candidate.Candidate,
                    candidate.SdpMid,
                    candidate.SdpMLineIndex ?? 0
                );
                _ = signalingClient.SendMessage(iceMsg);
            }
        }

        private void HandleIceConnectionChange(RTCIceConnectionState state)
        {
            _peerConnectionState = peerConnection.ConnectionState; // 상태 업데이트
            // 추가적인 상태 처리 로직 (예: 연결 실패 시 재시도 등)
        }

        private void HandleConnectionStateChange(RTCPeerConnectionState state)
        {
            _peerConnectionState = state;
            switch (state)
            {
                case RTCPeerConnectionState.Connected:
                    OnWebRtcConnected?.Invoke();
                    OnPeerConnectionEstablished?.Invoke(); // For UnityVerseBridgeManager
                    break;
                case RTCPeerConnectionState.Disconnected:
                case RTCPeerConnectionState.Failed:
                    OnWebRtcDisconnected?.Invoke();
                    OnPeerConnectionFailed?.Invoke(state.ToString()); // For UnityVerseBridgeManager
                    // 필요하다면 여기서 PeerConnection 정리 또는 재연결 시도
                    break;
                case RTCPeerConnectionState.Closed:
                    OnWebRtcDisconnected?.Invoke();
                    break;
            }
        }

        private void HandleDataChannelReceived(RTCDataChannel channel)
        {
            if (dataChannel != null && dataChannel.Label == channel.Label)
            {
                Debug.LogWarning($"[WebRtcManager] Data channel '{channel.Label}' already exists. Ignoring new one.");
                return;
            }
            dataChannel = channel;
            _dataChannelState = channel.ReadyState;
            SetupDataChannelEvents(dataChannel);
        }

        private void HandleTrackReceived(RTCTrackEvent e)
        {
            Debug.Log($"[WebRtcManager] Track Received: {e.Track.Kind}, ID: {e.Track.Id}, Enabled: {e.Track.Enabled}");
            
            // Track this received track
            receivedTrackIds.Add(e.Track.Id);
            
            // Log stream information
            if (e.Streams != null && e.Streams.Count() > 0)
            {
                Debug.Log($"[WebRtcManager] Track is part of {e.Streams.Count()} stream(s)");
                foreach (var stream in e.Streams)
                {
                    Debug.Log($"[WebRtcManager] Stream ID: {stream.Id}");
                }
            }
            
            OnTrackReceived?.Invoke(e.Track);

            if (e.Track.Kind == TrackKind.Video)
            {
                var videoTrack = e.Track as VideoStreamTrack;
                if (videoTrack != null)
                {
                    Debug.Log($"[WebRtcManager] Video track received - invoking OnVideoTrackReceived event");
                    OnVideoTrackReceived?.Invoke(videoTrack);
                }
                else
                {
                    Debug.LogError("[WebRtcManager] Failed to cast track to VideoStreamTrack");
                }
            }
            else if (e.Track.Kind == TrackKind.Audio)
            {
                var audioTrack = e.Track as AudioStreamTrack;
                if (audioTrack != null)
                {
                    Debug.Log($"[WebRtcManager] Audio track received - invoking OnAudioTrackReceived event");
                    OnAudioTrackReceived?.Invoke(audioTrack);
                }
                else
                {
                    Debug.LogError("[WebRtcManager] Failed to cast track to AudioStreamTrack");
                }
            }
        }
        
        private IEnumerator CheckTracksAfterDelay()
        {
            Debug.Log("[WebRtcManager] Starting delayed track check after renegotiation...");
            
            // Initial wait for connection to stabilize
            yield return new WaitForSeconds(0.5f);
            
            // Check multiple times to ensure track is detected
            int checkCount = 5; // Increase check count
            float checkInterval = 0.3f; // Shorter interval
            
            for (int i = 0; i < checkCount; i++)
            {
                Debug.Log($"[WebRtcManager] Track check attempt {i+1}/{checkCount}");
                CheckReceivedTracksAfterRenegotiation();
                
                // Check if we have video tracks now
                var receivers = peerConnection?.GetReceivers();
                if (receivers != null)
                {
                    var videoReceivers = receivers.Where(r => r.Track != null && r.Track.Kind == TrackKind.Video).ToList();
                    if (videoReceivers.Count > 0)
                    {
                        Debug.Log($"[WebRtcManager] Found {videoReceivers.Count} video track(s) on attempt {i+1}");
                        break;
                    }
                }
                
                // Wait before next check
                if (i < checkCount - 1)
                {
                    yield return new WaitForSeconds(checkInterval);
                }
            }
            
            Debug.Log("[WebRtcManager] Completed delayed track check");
        }
        
        private void CheckReceivedTracksAfterRenegotiation()
        {
            if (peerConnection == null)
            {
                Debug.LogWarning("[WebRtcManager] Cannot check tracks: PeerConnection is null");
                return;
            }
            
            Debug.Log("[WebRtcManager] Checking receivers after renegotiation...");
            
            var receivers = peerConnection.GetReceivers();
            if (receivers == null || receivers.Count() == 0)
            {
                Debug.LogWarning("[WebRtcManager] No receivers found after renegotiation");
                return;
            }
            
            Debug.Log($"[WebRtcManager] Found {receivers.Count()} receiver(s) after renegotiation");
            
            foreach (var receiver in receivers)
            {
                if (receiver.Track != null)
                {
                    Debug.Log($"[WebRtcManager] Receiver has track - Kind: {receiver.Track.Kind}, ID: {receiver.Track.Id}, Enabled: {receiver.Track.Enabled}, ReadyState: {receiver.Track.ReadyState}");
                    
                    // Always process video tracks during renegotiation, even if we've seen the ID before
                    // This is because Unity WebRTC might reuse track IDs during renegotiation
                    if (receiver.Track.Kind == TrackKind.Video)
                    {
                        var videoTrack = receiver.Track as VideoStreamTrack;
                        if (videoTrack != null)
                        {
                            // Check if track is ready
                            if (receiver.Track.ReadyState == TrackState.Live)
                            {
                                Debug.Log($"[WebRtcManager] Processing video track from renegotiation: {receiver.Track.Id}");
                                
                                // Always trigger the event during renegotiation to ensure UI updates
                                Debug.Log("[WebRtcManager] Triggering OnVideoTrackReceived for renegotiation track");
                                OnVideoTrackReceived?.Invoke(videoTrack);
                                OnTrackReceived?.Invoke(videoTrack);
                                
                                // Add to received tracks if not already there
                                if (!receivedTrackIds.Contains(receiver.Track.Id))
                                {
                                    receivedTrackIds.Add(receiver.Track.Id);
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"[WebRtcManager] Video track not ready yet: {receiver.Track.ReadyState}");
                            }
                        }
                    }
                    else if (receiver.Track.Kind == TrackKind.Audio)
                    {
                        var audioTrack = receiver.Track as AudioStreamTrack;
                        if (audioTrack != null && receiver.Track.ReadyState == TrackState.Live)
                        {
                            Debug.Log("[WebRtcManager] Triggering OnAudioTrackReceived for renegotiation track");
                            OnAudioTrackReceived?.Invoke(audioTrack);
                            OnTrackReceived?.Invoke(audioTrack);
                            
                            if (!receivedTrackIds.Contains(receiver.Track.Id))
                            {
                                receivedTrackIds.Add(receiver.Track.Id);
                            }
                        }
                    }
                }
                else
                {
                    Debug.Log("[WebRtcManager] Receiver found but track is null - this might be a transceiver without media yet");
                }
            }
        }

        private void HandleNegotiationNeeded()
        {
            Debug.LogWarning("[WebRtcManager] HandleNegotiationNeeded: Negotiation needed event triggered!");

            if (peerConnection == null)
            {
                Debug.LogError("[WebRtcManager] Cannot start renegotiation: PeerConnection is null.");
                return;
            }
            if (!IsSignalingConnected)
            {
                Debug.LogError("[WebRtcManager] Cannot start renegotiation: Signaling is not connected.");
                return;
            }
            
            // Log current state for debugging
            Debug.Log($"[WebRtcManager] Current state before renegotiation - ConnectionState: {peerConnection.ConnectionState}, SignalingState: {peerConnection.SignalingState}");
            
            if (peerConnection.SignalingState != RTCSignalingState.Stable)
            {
                Debug.LogError($"[WebRtcManager] Cannot start renegotiation: Signaling state is not Stable (Current: {peerConnection.SignalingState}). Waiting for Stable state.");
                // Queue renegotiation for when state becomes stable
                StartCoroutine(WaitForStableStateAndRenegotiate());
                return;
            }
            if (_isNegotiationCoroutineRunning) 
            {
                Debug.LogWarning("[WebRtcManager] Cannot start renegotiation now: Another negotiation coroutine is already running.");
                return;
            }

            if (isOfferer) // Offerer만 Offer를 생성하여 재협상을 시작
            {
                Debug.Log("[WebRtcManager] Starting renegotiation (creating and sending new offer)...");
                StartNegotiationCoroutine(CreateOfferAndSend());
            }
            else
            {
                Debug.LogWarning("[WebRtcManager] Answerer received OnNegotiationNeeded. Typically, the offerer initiates renegotiation.");
                // Answerer 측에서 OnNegotiationNeeded가 발생하면, 일반적으로는 아무것도 하지 않거나
                // 상대방에게 재협상이 필요함을 알리는 out-of-band 시그널을 보낼 수 있음 (여기서는 무시).
            }
        }
        
        private IEnumerator WaitForStableStateAndRenegotiate()
        {
            Debug.Log("[WebRtcManager] Waiting for stable state to perform renegotiation...");
            
            float timeout = 5f;
            float elapsed = 0f;
            
            while (peerConnection != null && peerConnection.SignalingState != RTCSignalingState.Stable && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }
            
            if (peerConnection == null)
            {
                Debug.LogError("[WebRtcManager] PeerConnection was destroyed while waiting for stable state");
                yield break;
            }
            
            if (elapsed >= timeout)
            {
                Debug.LogError("[WebRtcManager] Timeout waiting for stable state");
                yield break;
            }
            
            // Try renegotiation again
            Debug.Log("[WebRtcManager] Stable state reached, attempting renegotiation now");
            HandleNegotiationNeeded();
        }

        public void SetRole(bool isOffererRole)
        {
            this.isOfferer = isOffererRole;
        }
        
        // IWebRtcManager interface methods
        public void Connect(string roomId)
        {
            if (enableMultiPeer)
            {
                // Multi-peer mode uses room ID
                this.roomId = roomId;
                Debug.Log($"[WebRtcManager] Connect called in multi-peer mode with roomId: {roomId}");
                // Connection will be initiated when signaling is connected
            }
            else
            {
                // Single-peer mode - backward compatibility
                Debug.Log($"[WebRtcManager] Connect called with roomId: {roomId}");
                StartPeerConnection();
            }
        }
        
        #region Multi-Peer Methods
        
        private void HandleMultiPeerJoined(string jsonData)
        {
            var message = JsonUtility.FromJson<PeerJoinedMessage>(jsonData);
            
            if (connectionHandlers.Count >= maxConnections)
            {
                Debug.LogWarning($"[WebRtcManager] Max connections reached, rejecting peer: {message.peerId}");
                return;
            }
            
            Debug.Log($"[WebRtcManager] Peer joined: {message.peerId}");
            
            // Host creates offer for new client
            if (isOfferer)
            {
                CreateMultiPeerConnection(message.peerId, true);
            }
        }
        
        private void HandleMultiPeerLeft(string jsonData)
        {
            var message = JsonUtility.FromJson<PeerLeftMessage>(jsonData);
            Debug.Log($"[WebRtcManager] Peer left: {message.peerId}");
            DisconnectPeer(message.peerId);
        }
        
        private void HandleMultiPeerOffer(string jsonData)
        {
            var message = JsonUtility.FromJson<TargetedSessionDescriptionMessage>(jsonData);
            
            if (isOfferer)
            {
                Debug.LogWarning("[WebRtcManager] Host received offer, ignoring");
                return;
            }
            
            // Client handles offer from host
            if (!connectionHandlers.ContainsKey(message.sourcePeerId))
            {
                CreateMultiPeerConnection(message.sourcePeerId, false);
            }
            
            StartCoroutine(HandleMultiPeerOfferAndSendAnswer(message.sourcePeerId, message.sdp));
        }
        
        private void HandleMultiPeerAnswer(string jsonData)
        {
            var message = JsonUtility.FromJson<TargetedSessionDescriptionMessage>(jsonData);
            
            if (!connectionHandlers.TryGetValue(message.sourcePeerId, out var handler))
            {
                Debug.LogWarning($"[WebRtcManager] Received answer from unknown peer: {message.sourcePeerId}");
                return;
            }
            
            StartCoroutine(HandleMultiPeerAnswerCoroutine(message.sourcePeerId, message.sdp));
        }
        
        private void HandleMultiPeerIceCandidate(string jsonData)
        {
            var message = JsonUtility.FromJson<TargetedIceCandidateMessage>(jsonData);
            
            if (!connectionHandlers.TryGetValue(message.sourcePeerId, out var handler))
            {
                Debug.LogWarning($"[WebRtcManager] Received ICE candidate from unknown peer: {message.sourcePeerId}");
                return;
            }
            
            var candidateInit = new RTCIceCandidateInit
            {
                candidate = message.candidate,
                sdpMid = message.sdpMid,
                sdpMLineIndex = message.sdpMLineIndex
            };
            
            handler.AddIceCandidate(new RTCIceCandidate(candidateInit));
        }
        
        private void CreateMultiPeerConnection(string peerId, bool createOffer)
        {
            if (connectionHandlers.ContainsKey(peerId))
            {
                Debug.LogWarning($"[WebRtcManager] Peer connection already exists: {peerId}");
                return;
            }
            
            // Create WebRtcConnectionHandler
            var handler = new WebRtcConnectionHandler(peerId, isOfferer, configuration);
            
            // Setup event handlers
            handler.OnIceCandidateGenerated += (candidate) => HandleMultiPeerIceCandidateGenerated(peerId, candidate);
            handler.OnConnectionStateChanged += () => HandleMultiPeerConnectionStateChange(peerId, handler.ConnectionState);
            handler.OnVideoTrackReceived += (track) => HandleMultiPeerVideoTrackReceived(peerId, track);
            handler.OnAudioTrackReceived += (track) => HandleMultiPeerAudioTrackReceived(peerId, track);
            handler.OnDataChannelMessage += (message) => 
            {
                RaiseMultiPeerDataChannelMessageReceived(peerId, message);
                // For 1:1 compatibility
                if (_connectedPeerIds.Count == 1)
                {
                    OnDataChannelMessageReceived?.Invoke(message);
                }
            };
            handler.OnDataChannelOpen += (channel) => Debug.Log($"[WebRtcManager] DataChannel opened with {peerId}");
            handler.OnDataChannelClose += (channel) => Debug.Log($"[WebRtcManager] DataChannel closed with {peerId}");
            handler.OnNegotiationNeeded += () => HandleMultiPeerNegotiationNeeded(peerId);
            
            // Initialize handler
            handler.Initialize();
            
            // Add local tracks
            foreach (var track in localTracks)
            {
                if (track is VideoStreamTrack videoTrack)
                    handler.AddVideoTrack(videoTrack);
                else if (track is AudioStreamTrack audioTrack)
                    handler.AddAudioTrack(audioTrack);
            }
            
            connectionHandlers[peerId] = handler;
            
            if (createOffer)
            {
                StartCoroutine(CreateAndSendMultiPeerOffer(peerId));
            }
        }
        
        private IEnumerator CreateAndSendMultiPeerOffer(string peerId)
        {
            if (!connectionHandlers.TryGetValue(peerId, out var handler)) yield break;
            
            yield return handler.CreateOffer(
                onSuccess: (desc) => 
                {
                    var offerMessage = new TargetedSessionDescriptionMessage("offer", desc.sdp, peerId);
                    _ = signalingClient.SendMessage(offerMessage);
                },
                onError: (error) => 
                {
                    Debug.LogError($"[WebRtcManager] Failed to create offer for {peerId}: {error}");
                }
            );
        }
        
        private IEnumerator HandleMultiPeerOfferAndSendAnswer(string peerId, string sdp)
        {
            if (!connectionHandlers.TryGetValue(peerId, out var handler)) yield break;
            
            bool offerHandled = false;
            string offerError = null;
            
            yield return handler.HandleOffer(sdp, 
                onSuccess: () => 
                {
                    Debug.Log($"[WebRtcManager] Successfully handled offer from {peerId}");
                    offerHandled = true;
                },
                onError: (error) => 
                {
                    Debug.LogError($"[WebRtcManager] Failed to handle offer from {peerId}: {error}");
                    offerError = error;
                }
            );
            
            if (!offerHandled || !string.IsNullOrEmpty(offerError))
            {
                yield break;
            }
            
            yield return handler.CreateAnswer(
                onSuccess: (desc) => 
                {
                    var answerMessage = new TargetedSessionDescriptionMessage("answer", desc.sdp, peerId);
                    _ = signalingClient.SendMessage(answerMessage);
                },
                onError: (error) => 
                {
                    Debug.LogError($"[WebRtcManager] Failed to create answer for {peerId}: {error}");
                }
            );
        }
        
        private IEnumerator HandleMultiPeerAnswerCoroutine(string peerId, string sdp)
        {
            if (!connectionHandlers.TryGetValue(peerId, out var handler)) yield break;
            
            yield return handler.HandleAnswer(sdp,
                onSuccess: () => 
                {
                    Debug.Log($"[WebRtcManager] Successfully handled answer from {peerId}");
                },
                onError: (error) => 
                {
                    Debug.LogError($"[WebRtcManager] Failed to handle answer from {peerId}: {error}");
                }
            );
        }
        
        private void HandleMultiPeerIceCandidateGenerated(string peerId, RTCIceCandidate candidate)
        {
            if (candidate == null || string.IsNullOrEmpty(candidate.Candidate)) return;
            
            var message = new TargetedIceCandidateMessage(
                candidate.Candidate,
                candidate.SdpMid,
                candidate.SdpMLineIndex ?? 0,
                peerId
            );
            
            _ = signalingClient.SendMessage(message);
        }
        
        private void HandleMultiPeerConnectionStateChange(string peerId, RTCPeerConnectionState state)
        {
            Debug.Log($"[WebRtcManager] Peer {peerId} connection state: {state}");
            
            switch (state)
            {
                case RTCPeerConnectionState.Connected:
                    RaisePeerConnected(peerId);
                    // For 1:1 compatibility
                    if (_activeConnectionsCount == 1)
                    {
                        OnWebRtcConnected?.Invoke();
                    }
                    break;
                    
                case RTCPeerConnectionState.Disconnected:
                case RTCPeerConnectionState.Failed:
                case RTCPeerConnectionState.Closed:
                    RaisePeerDisconnected(peerId);
                    // For 1:1 compatibility
                    if (_activeConnectionsCount == 0)
                    {
                        OnWebRtcDisconnected?.Invoke();
                    }
                    break;
            }
        }
        
        private void HandleMultiPeerVideoTrackReceived(string peerId, MediaStreamTrack track)
        {
            Debug.Log($"[WebRtcManager] Video track received from {peerId}");
            RaiseMultiPeerVideoTrackReceived(peerId, track);
            
            // For 1:1 compatibility
            if (_connectedPeerIds.Count == 1)
            {
                OnVideoTrackReceived?.Invoke(track);
            }
        }
        
        private void HandleMultiPeerAudioTrackReceived(string peerId, MediaStreamTrack track)
        {
            Debug.Log($"[WebRtcManager] Audio track received from {peerId}");
            RaiseMultiPeerAudioTrackReceived(peerId, track);
            
            // For 1:1 compatibility
            if (_connectedPeerIds.Count == 1)
            {
                OnAudioTrackReceived?.Invoke(track);
            }
        }
        
        private void HandleMultiPeerNegotiationNeeded(string peerId)
        {
            if (isOfferer && connectionHandlers.ContainsKey(peerId))
            {
                Debug.Log($"[WebRtcManager] Renegotiation needed for {peerId}");
                StartCoroutine(CreateAndSendMultiPeerOffer(peerId));
            }
        }
        
        private void DisconnectPeer(string peerId)
        {
            if (connectionHandlers.TryGetValue(peerId, out var handler))
            {
                handler.Close();
                connectionHandlers.Remove(peerId);
                RaisePeerDisconnected(peerId);
            }
        }
        
        // Event raising methods
        private void RaisePeerConnected(string peerId)
        {
            OnPeerConnected?.Invoke(peerId);
        }
        
        private void RaisePeerDisconnected(string peerId)
        {
            OnPeerDisconnected?.Invoke(peerId);
        }
        
        private void RaiseMultiPeerDataChannelMessageReceived(string peerId, string message)
        {
            OnMultiPeerDataChannelMessageReceived?.Invoke(peerId, message);
        }
        
        private void RaiseMultiPeerVideoTrackReceived(string peerId, MediaStreamTrack track)
        {
            OnMultiPeerVideoTrackReceived?.Invoke(peerId, track);
        }
        
        private void RaiseMultiPeerAudioTrackReceived(string peerId, MediaStreamTrack track)
        {
            OnMultiPeerAudioTrackReceived?.Invoke(peerId, track);
        }
        
        // Public methods for multi-peer mode
        public void SendDataChannelMessage(string peerId, object messageData)
        {
            if (!enableMultiPeer)
            {
                // Single-peer mode fallback
                SendDataChannelMessage(messageData);
                return;
            }
            
            if (connectionHandlers.TryGetValue(peerId, out var handler) && handler.IsDataChannelOpen)
            {
                try
                {
                    string jsonMessage = JsonUtility.ToJson(messageData);
                    handler.SendDataChannelMessage(jsonMessage);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WebRtcManager] Failed to send message to {peerId}: {e.Message}");
                }
            }
        }
        
        public void SetMultiPeerMode(bool enable, int maxConnections = 5)
        {
            this.enableMultiPeer = enable;
            this.maxConnections = maxConnections;
            Debug.Log($"[WebRtcManager] Multi-peer mode {(enable ? "enabled" : "disabled")} with max connections: {maxConnections}");
        }
        
        #endregion
        
        #region UnityVerseBridgeManager Compatibility Methods
        
        /// <summary>
        /// Connect to signaling server with WebSocket adapter (for UnityVerseBridgeManager)
        /// </summary>
        public async Task ConnectToSignaling(IWebSocketClient adapter, string url)
        {
            try
            {
                signalingServerUrl = url;
                signalingClient = new SignalingClient();
                await signalingClient.InitializeAndConnect(adapter, url);
                
                // Set up event handlers
                signalingClient.OnConnected += HandleSignalingConnected;
                signalingClient.OnDisconnected += HandleSignalingDisconnected;
                signalingClient.OnSignalingMessageReceived += HandleSignalingMessage;
                
                _isSignalingConnected = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebRtcManager] Failed to connect to signaling: {e.Message}");
                OnSignalingError?.Invoke(e.Message);
                throw;
            }
        }
        
        /// <summary>
        /// Set debug mode for logging (for UnityVerseBridgeManager)
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            // Configure debug logging
            if (enabled)
            {
                Debug.unityLogger.filterLogType = LogType.Log;
            }
        }
        
        /// <summary>
        /// Set peer role (offerer/answerer) (for UnityVerseBridgeManager)
        /// </summary>
        public void SetPeerRole(bool isHost)
        {
            isOfferer = isHost;
            SetRole(isHost);
        }
        
        /// <summary>
        /// Send message via signaling (for UnityVerseBridgeManager)
        /// </summary>
        public void SendSignalingMessage(object message)
        {
            if (signalingClient != null && signalingClient.IsConnected)
            {
                // Convert the object to SignalingMessageBase if possible
                if (message is SignalingMessageBase signalingMessage)
                {
                    _ = signalingClient.SendMessage(signalingMessage);
                }
                else
                {
                    // For non-SignalingMessageBase objects, we need to wrap them
                    // or use the raw WebSocket send method
                    string jsonMessage = JsonUtility.ToJson(message);
                    Debug.LogWarning($"[WebRtcManager] Sending raw JSON message: {jsonMessage}");
                    // Note: This requires access to the underlying WebSocket adapter
                    // For now, we'll just log a warning
                    Debug.LogError("[WebRtcManager] Cannot send non-SignalingMessageBase objects directly. Please use proper message types.");
                }
            }
            else
            {
                Debug.LogError("[WebRtcManager] Cannot send message - signaling not connected");
                OnSignalingError?.Invoke("Signaling not connected");
            }
        }
        
        #endregion
    }
}