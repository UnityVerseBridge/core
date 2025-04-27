using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq; // FirstOrDefault() 사용 위해 추가!
using UnityEngine;
using Unity.WebRTC;
using UnityVerseBridge.Core.Signaling;
using UnityVerseBridge.Core.Signaling.Data;
using UnityVerseBridge.Core.DataChannel.Data;
using UnityVerseBridge.Core; // WebRtcConfiguration, WebRtcConfigurationExtensions 사용 가정

namespace UnityVerseBridge.Core
{
    public class WebRtcManager : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("WebRTC 연결 설정을 담은 객체입니다.")]
        [SerializeField] private WebRtcConfiguration configuration = new WebRtcConfiguration();
        [Tooltip("접속할 시그널링 서버의 주소입니다.")]
        [SerializeField] private string signalingServerUrl = "ws://localhost:8080";

        [Header("State (Read-only in Inspector)")]
        [SerializeField] private bool _isSignalingConnected = false;
        [SerializeField] private RTCPeerConnectionState _peerConnectionState = RTCPeerConnectionState.New;
        [SerializeField] private RTCDataChannelState _dataChannelState = RTCDataChannelState.Closed;

        // --- Private Fields ---
        private ISignalingClient signalingClient;
        private RTCPeerConnection peerConnection;
        private RTCDataChannel dataChannel;
        private Coroutine _peerConnectionCoroutine;

        // --- Public Events ---
        public event Action OnSignalingConnected;
        public event Action OnSignalingDisconnected;
        public event Action OnWebRtcConnected;
        public event Action OnWebRtcDisconnected;
        public event Action<string> OnDataChannelOpened;
        public event Action OnDataChannelClosed;
        public event Action<string> OnDataChannelMessageReceived;
        public event Action<MediaStreamTrack> OnTrackReceived;

        // --- Public Properties ---
        public bool IsSignalingConnected => _isSignalingConnected;
        public bool IsWebRtcConnected => peerConnection?.ConnectionState == RTCPeerConnectionState.Connected;
        public bool IsDataChannelOpen => dataChannel?.ReadyState == RTCDataChannelState.Open;
        public string SignalingServerUrl => signalingServerUrl;

        // --- Initialization ---
        void Awake()
        {
            Debug.Log("[WebRtcManager] Awake.");
            // SignalingClient 인스턴스화는 SetupSignaling에서 처리
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
            Debug.Log($"[WebRtcManager] SignalingClient injected via SetupSignaling: {client.GetType().Name}");
            SubscribeSignalingEvents();

            // Setup 후 자동으로 시그널링 연결 상태를 확인하거나 연결 시도
             // ConnectSignaling(); // ConnectSignaling 로직 재검토 필요
        }

        public void SetConfiguration(WebRtcConfiguration config)
        {
             this.configuration = config ?? new WebRtcConfiguration();
             Debug.Log("[WebRtcManager] WebRTC Configuration updated.");
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
            Debug.Log("[WebRtcManager] Subscribed to SignalingClient events.");
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
            // ISignalingClient 인터페이스에 DispatchMessages 추가했으므로 직접 호출
            signalingClient?.DispatchMessages();
        }

        void OnDestroy()
        {
            Debug.Log("[WebRtcManager] OnDestroy: Cleaning up...");
            Disconnect();
        }

        // --- Public Control Methods ---
        public void ConnectSignaling() // 이 메서드는 이제 주로 재연결 용도로 사용될 수 있음
        {
            if (signalingClient == null)
            {
                Debug.LogError("[WebRtcManager] SignalingClient not initialized.");
                return;
            }
            if (!IsSignalingConnected)
            {
                Debug.Log($"[WebRtcManager] Attempting to connect Signaling: {signalingServerUrl}");
                // InitializeAndConnect를 다시 호출하는 것은 적절하지 않을 수 있음
                // SignalingClient에 재연결 메서드를 만들거나, 새로 Initialize해야 할 수 있음
                // 여기서는 경고만 표시하거나, Initialize를 다시 호출하도록 유도
                Debug.LogWarning("Use InitializeSignaling to connect initially or implement reconnect logic in SignalingClient.");
                // _ = signalingClient.Connect(signalingServerUrl); // ISignalingClient에 Connect가 남아있다면...
            }
            else
            {
                Debug.Log("[WebRtcManager] Signaling already connected.");
            }
        }

        public void StartPeerConnection()
        {
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
            if (_peerConnectionCoroutine != null)
            {
                Debug.LogWarning("[WebRtcManager] Peer connection process already running.");
                return;
            }

            Debug.Log("[WebRtcManager] Starting Peer Connection process as offerer...");
            CreatePeerConnection();
            CreateDataChannel();
            _peerConnectionCoroutine = StartCoroutine(CreateOfferAndSend());
        }

        public void SendDataChannelMessage(object messageData)
        {
            if (messageData == null)
            {
                Debug.LogWarning("[WebRtcManager] Cannot send null message data.");
                return;
            }
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
            if (_peerConnectionCoroutine != null)
            {
                StopCoroutine(_peerConnectionCoroutine);
                _peerConnectionCoroutine = null;
            }

            dataChannel?.Close();
            peerConnection?.Close();

            await DisconnectSignaling();
            Debug.Log("[WebRtcManager] Disconnect finished.");
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

        // --- Private WebRTC Setup ---
        private void CreatePeerConnection()
        {
            if (peerConnection != null)
            {
                Debug.LogWarning("PeerConnection exists. Closing previous.");
                peerConnection.Close();
            }
            Debug.Log("[WebRtcManager] Creating Peer Connection...");
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
            Debug.Log("[WebRtcManager] Peer Connection event handlers registered.");
        }

        private void CreateDataChannel()
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
                Debug.Log($"[WebRtcManager] Creating Data Channel: {configuration.dataChannelLabel}");
                RTCDataChannelInit options = new RTCDataChannelInit() { ordered = true };
                dataChannel = peerConnection.CreateDataChannel(configuration.dataChannelLabel, options);
                _dataChannelState = RTCDataChannelState.Connecting;
                SetupDataChannelEvents(dataChannel);
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
            Debug.Log($"[WebRtcManager] Data Channel '{channel.Label}' event handlers set up.");
        }

        // --- Signaling Event Handlers ---
        private void HandleSignalingConnected()
        {
            _isSignalingConnected = true;
            Debug.Log("[WebRtcManager] Signaling Connected!");
            OnSignalingConnected?.Invoke();

            Debug.Log("[WebRtcManager] Automatically starting Peer Connection as offerer...");
            StartPeerConnection();
        }

        private void HandleSignalingDisconnected()
        {
            _isSignalingConnected = false;
            Debug.LogWarning("[WebRtcManager] Signaling Disconnected!");
            OnSignalingDisconnected?.Invoke();
        }

        private void HandleSignalingMessage(string type, string jsonData)
        {
            if (peerConnection == null && type != "offer")
            {
                Debug.LogWarning($"[WebRtcManager] Received '{type}' before PeerConnection init. Ignoring.");
                return;
            }
            if (_peerConnectionCoroutine != null && (type == "offer" || type == "answer"))
            {
                Debug.LogWarning($"[WebRtcManager] Receiving '{type}' while another process is running. Stopping previous.");
                StopCoroutine(_peerConnectionCoroutine);
                _peerConnectionCoroutine = null;
            }

            Debug.Log($"[WebRtcManager] Handling Signaling Message | Type: {type}");
            try
            {
                switch (type)
                {
                    case "offer":
                        CreatePeerConnection();
                        _peerConnectionCoroutine = StartCoroutine(HandleOfferAndSendAnswer(JsonUtility.FromJson<SessionDescriptionMessage>(jsonData)));
                        break;
                    case "answer":
                        _peerConnectionCoroutine = StartCoroutine(HandleAnswer(JsonUtility.FromJson<SessionDescriptionMessage>(jsonData)));
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
                _peerConnectionCoroutine = null;
            }
        }

        // --- WebRTC Logic Coroutines ---
        private IEnumerator CreateOfferAndSend()
        {
            if (peerConnection == null)
            {
                Debug.LogError("PC null in CreateOffer");
                _peerConnectionCoroutine = null;
                yield break;
            }
            Debug.Log("[WebRtcManager] Creating Offer...");
            var offerOp = peerConnection.CreateOffer();
            yield return offerOp;
            if (offerOp.IsError)
            {
                Debug.LogError($"Failed OfferOp: {offerOp.Error.message}");
                _peerConnectionCoroutine = null;
                yield break;
            }
            var offerDesc = offerOp.Desc;
            Debug.Log("[WebRtcManager] Setting Local Desc (Offer)...");
            var localDescOp = peerConnection.SetLocalDescription(ref offerDesc);
            yield return localDescOp;
            if (localDescOp.IsError)
            {
                Debug.LogError($"Failed LocalDesc(Offer): {localDescOp.Error.message}");
                _peerConnectionCoroutine = null;
                yield break;
            }
            Debug.Log("[WebRtcManager] Sending Offer...");
            try
            {
                var offerMsg = new SessionDescriptionMessage("offer", offerDesc.sdp);
                _ = signalingClient.SendMessage(offerMsg); // await 제거!
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending offer: {e.Message}");
            }
            _peerConnectionCoroutine = null;
        }

        private IEnumerator HandleOfferAndSendAnswer(SessionDescriptionMessage offerMessage)
        {
            if (peerConnection == null)
            {
                Debug.LogError("PC null in HandleOffer");
                _peerConnectionCoroutine = null;
                yield break;
            }
            Debug.Log("[WebRtcManager] Setting Remote Desc (Offer)...");
            RTCSessionDescription offerDesc = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = offerMessage.sdp };
            var remoteDescOp = peerConnection.SetRemoteDescription(ref offerDesc);
            yield return remoteDescOp;
            if (remoteDescOp.IsError)
            {
                Debug.LogError($"Failed RemoteDesc(Offer): {remoteDescOp.Error.message}");
                _peerConnectionCoroutine = null;
                yield break;
            }
            Debug.Log("[WebRtcManager] Creating Answer...");
            var answerOp = peerConnection.CreateAnswer();
            yield return answerOp;
            if (answerOp.IsError)
            {
                Debug.LogError($"Failed CreateAnswer: {answerOp.Error.message}");
                _peerConnectionCoroutine = null;
                yield break;
            }
            var answerDesc = answerOp.Desc;
            Debug.Log("[WebRtcManager] Setting Local Desc (Answer)...");
            var localDescOp = peerConnection.SetLocalDescription(ref answerDesc);
            yield return localDescOp;
            if (localDescOp.IsError)
            {
                Debug.LogError($"Failed LocalDesc(Answer): {localDescOp.Error.message}");
                _peerConnectionCoroutine = null;
                yield break;
            }
            Debug.Log("[WebRtcManager] Sending Answer...");
            try
            {
                var answerMsg = new SessionDescriptionMessage("answer", answerDesc.sdp);
                _ = signalingClient.SendMessage(answerMsg); // await 제거!
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending answer: {e.Message}");
            }
            _peerConnectionCoroutine = null;
        }

        private IEnumerator HandleAnswer(SessionDescriptionMessage answerMessage)
        {
            if (peerConnection == null)
            {
                Debug.LogError("PC null in HandleAnswer");
                _peerConnectionCoroutine = null;
                yield break;
            }
            Debug.Log("[WebRtcManager] Setting Remote Desc (Answer)...");
            RTCSessionDescription answerDesc = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = answerMessage.sdp };
            var remoteDescOp = peerConnection.SetRemoteDescription(ref answerDesc);
            yield return remoteDescOp;
            // --- 구조체 null 비교 오류 수정 ---
            if (remoteDescOp.IsError)
            {
                Debug.LogError($"Failed RemoteDesc(Answer): {remoteDescOp.Error.message}");
            }
            else
            {
                Debug.Log("[WebRtcManager] Remote Description (Answer) Set Successfully.");
            }
            // --- 수정 완료 ---
            _peerConnectionCoroutine = null;
        }

        // --- WebRTC Event Handlers ---
        private void HandleIceCandidate(IceCandidateMessage candidateMessage)
        {
            if (peerConnection == null || peerConnection.SignalingState == RTCSignalingState.Closed)
            {
                Debug.LogWarning($"PC not ready/closed. Ignoring ICE candidate.");
                return;
            }
            if (candidateMessage == null || string.IsNullOrEmpty(candidateMessage.candidate))
            {
                Debug.LogWarning("Received null/empty ICE candidate msg.");
                return;
            }
            // RemoteDescription 설정 전에 후보가 도착할 수 있음에 유의 (큐잉 메커니즘 필요)
            // RTCSessionDescription은 struct라 null 비교 불가, sdp가 null/empty면 아직 설정 안 된 것으로 간주
            if (string.IsNullOrEmpty(peerConnection.RemoteDescription.sdp))
            {
                Debug.LogWarning("Remote description not set yet. Queuing ICE candidate is recommended.");
                /* return; */ // 임시로 진행
            }

            try
            {
                RTCIceCandidate candidate = candidateMessage.ToRTCIceCandidate();
                string candidatePreview = candidate.Candidate?.Substring(0, Math.Min(30, candidate.Candidate?.Length ?? 0));
                Debug.Log($"[WebRtcManager] Attempting to add ICE candidate: {candidatePreview}...");
                peerConnection.AddIceCandidate(candidate);
            }
            catch (RTCErrorException e)
            {
                Debug.LogError($"[WebRtcManager] RTCError adding ICE: {e.Message}. State: {peerConnection?.SignalingState}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebRtcManager] Exception adding ICE: {e.Message}");
            }
        }

        private void HandleIceCandidateGenerated(RTCIceCandidate candidate)
        {
            if (candidate != null && !string.IsNullOrEmpty(candidate.Candidate))
            {
                var candidateMsg = IceCandidateMessage.FromRTCIceCandidate(candidate);
                if (signalingClient != null && signalingClient.IsConnected)
                {
                    _ = signalingClient.SendMessage(candidateMsg); // await 제거됨
                }
                else
                {
                    Debug.LogWarning($"[WebRtcManager] Signaling client is null or not connected when ICE candidate was generated. Candidate not sent.");
                }
            }
        }

        private void HandleIceConnectionChange(RTCIceConnectionState state)
        {
            Debug.Log($"[WebRtcManager] ICE Connection State: {state}");
        }

        private void HandleConnectionStateChange(RTCPeerConnectionState state)
        {
            _peerConnectionState = state;
            Debug.Log($"[WebRtcManager] Peer Connection State: {state}");
            switch (state)
            {
                case RTCPeerConnectionState.Connected:
                    OnWebRtcConnected?.Invoke();
                    break;
                case RTCPeerConnectionState.Failed:
                case RTCPeerConnectionState.Disconnected:
                case RTCPeerConnectionState.Closed:
                    OnWebRtcDisconnected?.Invoke();
                    break;
            }
        }

        private void HandleDataChannelReceived(RTCDataChannel channel)
        {
            Debug.Log($"[WebRtcManager] DC Received: {channel.Label}");
            if (dataChannel == null)
            {
                dataChannel = channel;
                _dataChannelState = RTCDataChannelState.Connecting;
                SetupDataChannelEvents(dataChannel);
            }
            else if (dataChannel != channel)
            {
                Debug.LogWarning($"Another DC '{channel.Label}' received.");
                dataChannel.Close();
                dataChannel = channel;
                SetupDataChannelEvents(dataChannel);
            }
        }

        private void HandleTrackReceived(RTCTrackEvent e)
        {
            // --- IEnumerable 인덱싱 오류 수정 ---
            Debug.Log($"[WebRtcManager] Track Received: Kind={e.Track.Kind}, ID={e.Track.Id}");
            var firstStream = e.Streams.FirstOrDefault(); // Linq 사용! (using System.Linq; 필요)
            if (firstStream != null)
            {
                // GetTracks()를 사용하여 스트림 내 트랙 ID 목록 얻기 (e.Track.Id와 다를 수 있음)
                Debug.Log($" Associated Stream IDs: {firstStream.Id}, Track IDs in Stream: {string.Join(",", firstStream.GetTracks().Select(t => t.Id))}");
            }
            else
            {
                Debug.Log("[WebRtcManager] No associated streams found for this track.");
            }
            // --- 수정 완료 ---
            OnTrackReceived?.Invoke(e.Track);
        }

        private void HandleNegotiationNeeded()
        {
            Debug.LogWarning("[WebRtcManager] Negotiation Needed triggered. Implement re-negotiation if needed.");
        }
    }
}