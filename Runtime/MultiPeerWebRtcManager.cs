using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using Unity.WebRTC;
using UnityVerseBridge.Core.Signaling;
using UnityVerseBridge.Core.Signaling.Data;

namespace UnityVerseBridge.Core
{
    /// <summary>
    /// 1:N 연결을 지원하는 WebRTC 매니저입니다.
    /// 하나의 호스트(Quest)가 여러 클라이언트(Mobile)와 연결할 수 있습니다.
    /// </summary>
    public class MultiPeerWebRtcManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private WebRtcConfiguration configuration = new WebRtcConfiguration();
        [SerializeField] private string signalingServerUrl = "ws://localhost:8080";
        [SerializeField] private int maxConnections = 5; // 최대 연결 수
        
        [Header("Role")]
        [SerializeField] private PeerRole peerRole = PeerRole.Host; // Host(1) or Client(N)
        [SerializeField] private string roomId = "default-room";
        [SerializeField] private bool autoStartHost = false;

        [Header("State (Read-only)")]
        [SerializeField] private bool _isSignalingConnected = false;
        [SerializeField] private int _activeConnectionsCount = 0;
        [SerializeField] private List<string> _connectedPeerIds = new List<string>();

        // 피어 연결 관리 - WebRtcConnectionHandler 사용
        private Dictionary<string, WebRtcConnectionHandler> connectionHandlers = new Dictionary<string, WebRtcConnectionHandler>();
        private ISignalingClient signalingClient;
        
        // 공유 리소스
        private MediaStream sharedSendStream; // 호스트가 모든 클라이언트에게 보낼 스트림
        private readonly List<MediaStreamTrack> localTracks = new List<MediaStreamTrack>();

        // 이벤트
        public event Action<string> OnPeerConnected;
        public event Action<string> OnPeerDisconnected;
        public event Action<string, string> OnDataChannelMessageReceived;
        public event Action<string, MediaStreamTrack> OnTrackReceived;
        public event Action<string, VideoStreamTrack> OnVideoTrackReceived;
        public event Action<string, AudioStreamTrack> OnAudioTrackReceived;
        public event Action OnSignalingConnected;
        public event Action OnSignalingDisconnected;

        public enum PeerRole
        {
            Host,   // 1 (Quest - 스트림 송신자)
            Client  // N (Mobile - 스트림 수신자)
        }


        void Awake()
        {
            // 공유 스트림 초기화 (호스트용)
            if (peerRole == PeerRole.Host)
            {
                sharedSendStream = new MediaStream();
            }
        }

        void Update()
        {
            signalingClient?.DispatchMessages();
            
            // 연결 상태 모니터링
            _activeConnectionsCount = connectionHandlers.Count(p => p.Value.IsConnected);
            _connectedPeerIds = connectionHandlers.Where(p => p.Value.IsConnected).Select(p => p.Key).ToList();
        }

        void OnDestroy()
        {
            DisconnectAll();
        }

        #region Public Methods

        public void SetupSignaling(ISignalingClient client)
        {
            if (this.signalingClient != null && this.signalingClient.IsConnected)
            {
                _ = DisconnectSignaling();
            }

            this.signalingClient = client ?? throw new ArgumentNullException(nameof(client));
            SubscribeSignalingEvents();
        }

        public async void StartAsHost(string roomId)
        {
            if (peerRole != PeerRole.Host)
            {
                Debug.LogError("[MultiPeerWebRtcManager] Cannot start as host when role is Client");
                return;
            }

            this.roomId = roomId;
            
            if (signalingClient != null && !signalingClient.IsConnected)
            {
                await signalingClient.Connect(signalingServerUrl);
            }
        }

        public async void JoinAsClient(string roomId)
        {
            if (peerRole != PeerRole.Client)
            {
                Debug.LogError("[MultiPeerWebRtcManager] Cannot join as client when role is Host");
                return;
            }

            this.roomId = roomId;
            
            if (signalingClient != null && !signalingClient.IsConnected)
            {
                await signalingClient.Connect(signalingServerUrl);
            }
        }

        public void AddVideoTrack(VideoStreamTrack videoTrack)
        {
            if (videoTrack == null) return;

            Debug.Log($"[MultiPeerWebRtcManager] Adding video track: {videoTrack.Id}");
            localTracks.Add(videoTrack);

            if (peerRole == PeerRole.Host)
            {
                // 호스트는 공유 스트림에 트랙 추가
                sharedSendStream.AddTrack(videoTrack);
                
                // 모든 연결된 피어에게 트랙 추가
                foreach (var kvp in connectionHandlers.Where(p => p.Value.IsConnected))
                {
                    kvp.Value.AddVideoTrack(videoTrack);
                }
            }
            else
            {
                // 클라이언트는 호스트에게만 트랙 전송
                var hostHandler = connectionHandlers.Values.FirstOrDefault();
                if (hostHandler != null && hostHandler.IsConnected)
                {
                    hostHandler.AddVideoTrack(videoTrack);
                }
            }
        }

        public void AddAudioTrack(AudioStreamTrack audioTrack)
        {
            if (audioTrack == null) return;

            Debug.Log($"[MultiPeerWebRtcManager] Adding audio track: {audioTrack.Id}");
            localTracks.Add(audioTrack);

            if (peerRole == PeerRole.Host)
            {
                // 호스트는 공유 스트림에 트랙 추가
                sharedSendStream.AddTrack(audioTrack);
                
                // 모든 연결된 피어에게 트랙 추가
                foreach (var kvp in connectionHandlers.Where(p => p.Value.IsConnected))
                {
                    kvp.Value.AddAudioTrack(audioTrack);
                }
            }
            else
            {
                // 클라이언트는 호스트에게만 트랙 전송
                var hostHandler = connectionHandlers.Values.FirstOrDefault();
                if (hostHandler != null && hostHandler.IsConnected)
                {
                    hostHandler.AddAudioTrack(audioTrack);
                }
            }
        }

        public void RemoveTrack(MediaStreamTrack track)
        {
            if (track == null) return;

            localTracks.Remove(track);

            if (peerRole == PeerRole.Host)
            {
                sharedSendStream.RemoveTrack(track);
            }

            // 모든 피어에서 트랙 제거
            foreach (var handler in connectionHandlers.Values)
            {
                handler.RemoveTrack(track);
            }
        }

        public void SendDataChannelMessage(string peerId, object messageData)
        {
            if (connectionHandlers.TryGetValue(peerId, out var handler) && handler.IsDataChannelOpen)
            {
                try
                {
                    string jsonMessage = JsonUtility.ToJson(messageData);
                    handler.SendDataChannelMessage(jsonMessage);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MultiPeerWebRtcManager] Failed to send message to {peerId}: {e.Message}");
                }
            }
        }

        public void BroadcastDataChannelMessage(object messageData)
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
                    Debug.LogError($"[MultiPeerWebRtcManager] Failed to broadcast to {kvp.Key}: {e.Message}");
                }
            }
        }

        public void DisconnectPeer(string peerId)
        {
            if (connectionHandlers.TryGetValue(peerId, out var handler))
            {
                handler.Close();
                connectionHandlers.Remove(peerId);
                OnPeerDisconnected?.Invoke(peerId);
            }
        }

        public void DisconnectAll()
        {
            foreach (var handler in connectionHandlers.Values.ToList())
            {
                handler.Close();
            }
            connectionHandlers.Clear();
            
            _ = DisconnectSignaling();
        }

        #endregion

        #region Private Methods

        private void SubscribeSignalingEvents()
        {
            if (signalingClient == null) return;
            
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

        private async System.Threading.Tasks.Task DisconnectSignaling()
        {
            if (signalingClient != null)
            {
                UnsubscribeSignalingEvents();
                await signalingClient.Disconnect();
                _isSignalingConnected = false;
            }
        }

        private void HandleSignalingConnected()
        {
            _isSignalingConnected = true;
            OnSignalingConnected?.Invoke();
            
            // 룸에 참가
            var joinMessage = new JoinRoomMessage(roomId, peerRole.ToString(), maxConnections);
            _ = signalingClient.SendMessage(joinMessage);
        }

        private void HandleSignalingDisconnected()
        {
            _isSignalingConnected = false;
            OnSignalingDisconnected?.Invoke();
        }

        private void HandleSignalingMessage(string type, string jsonData)
        {
            try
            {
                switch (type)
                {
                    case "peer-joined":
                        HandlePeerJoined(jsonData);
                        break;
                    case "peer-left":
                        HandlePeerLeft(jsonData);
                        break;
                    case "offer":
                        HandleOffer(jsonData);
                        break;
                    case "answer":
                        HandleAnswer(jsonData);
                        break;
                    case "ice-candidate":
                        HandleIceCandidate(jsonData);
                        break;
                    default:
                        Debug.LogWarning($"[MultiPeerWebRtcManager] Unknown message type: {type}");
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiPeerWebRtcManager] Error handling message {type}: {e.Message}");
            }
        }

        private void HandlePeerJoined(string jsonData)
        {
            var message = JsonUtility.FromJson<PeerJoinedMessage>(jsonData);
            
            if (connectionHandlers.Count >= maxConnections)
            {
                Debug.LogWarning($"[MultiPeerWebRtcManager] Max connections reached, rejecting peer: {message.peerId}");
                return;
            }
            
            Debug.Log($"[MultiPeerWebRtcManager] Peer joined: {message.peerId}");
            
            // 호스트는 새 클라이언트에게 Offer를 보냄
            if (peerRole == PeerRole.Host)
            {
                CreatePeerConnection(message.peerId, true);
            }
        }

        private void HandlePeerLeft(string jsonData)
        {
            var message = JsonUtility.FromJson<PeerLeftMessage>(jsonData);
            Debug.Log($"[MultiPeerWebRtcManager] Peer left: {message.peerId}");
            DisconnectPeer(message.peerId);
        }

        private void CreatePeerConnection(string peerId, bool createOffer)
        {
            if (connectionHandlers.ContainsKey(peerId))
            {
                Debug.LogWarning($"[MultiPeerWebRtcManager] Peer connection already exists: {peerId}");
                return;
            }

            // WebRtcConnectionHandler 생성
            var isOfferer = peerRole == PeerRole.Host;
            var handler = new WebRtcConnectionHandler(peerId, isOfferer, configuration);
            
            // 이벤트 핸들러 설정
            handler.OnIceCandidateGenerated += (candidate) => HandleIceCandidateGenerated(peerId, candidate);
            handler.OnConnectionStateChanged += () => HandleConnectionStateChange(peerId, handler.ConnectionState);
            handler.OnVideoTrackReceived += (track) => HandleVideoTrackReceived(peerId, track);
            handler.OnAudioTrackReceived += (track) => HandleAudioTrackReceived(peerId, track);
            handler.OnDataChannelMessage += (message) => OnDataChannelMessageReceived?.Invoke(peerId, message);
            handler.OnDataChannelOpen += (channel) => Debug.Log($"[MultiPeerWebRtcManager] DataChannel opened with {peerId}");
            handler.OnDataChannelClose += (channel) => Debug.Log($"[MultiPeerWebRtcManager] DataChannel closed with {peerId}");
            handler.OnNegotiationNeeded += () => HandleNegotiationNeeded(peerId);

            // 핸들러 초기화
            handler.Initialize();

            // 로컬 트랙 추가
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
                StartCoroutine(CreateAndSendOffer(peerId));
            }
        }


        private IEnumerator CreateAndSendOffer(string peerId)
        {
            if (!connectionHandlers.TryGetValue(peerId, out var handler)) yield break;

            yield return handler.CreateOffer(
                onSuccess: (desc) => 
                {
                    // Offer 전송
                    var offerMessage = new TargetedSessionDescriptionMessage("offer", desc.sdp, peerId);
                    _ = signalingClient.SendMessage(offerMessage);
                },
                onError: (error) => 
                {
                    Debug.LogError($"[MultiPeerWebRtcManager] Failed to create offer for {peerId}: {error}");
                }
            );
        }

        private void HandleOffer(string jsonData)
        {
            var message = JsonUtility.FromJson<TargetedSessionDescriptionMessage>(jsonData);
            
            if (peerRole == PeerRole.Host)
            {
                Debug.LogWarning("[MultiPeerWebRtcManager] Host received offer, ignoring");
                return;
            }

            // 클라이언트는 호스트로부터의 Offer만 처리
            if (!connectionHandlers.ContainsKey(message.sourcePeerId))
            {
                CreatePeerConnection(message.sourcePeerId, false);
            }

            StartCoroutine(HandleOfferAndSendAnswer(message.sourcePeerId, message.sdp));
        }

        private IEnumerator HandleOfferAndSendAnswer(string peerId, string sdp)
        {
            if (!connectionHandlers.TryGetValue(peerId, out var handler)) yield break;

            bool offerHandled = false;
            string offerError = null;

            // Offer 처리
            yield return handler.HandleOffer(sdp, 
                onSuccess: () => 
                {
                    Debug.Log($"[MultiPeerWebRtcManager] Successfully handled offer from {peerId}");
                    offerHandled = true;
                },
                onError: (error) => 
                {
                    Debug.LogError($"[MultiPeerWebRtcManager] Failed to handle offer from {peerId}: {error}");
                    offerError = error;
                }
            );

            // Offer 처리 실패 시 중단
            if (!offerHandled || !string.IsNullOrEmpty(offerError))
            {
                yield break;
            }

            // Answer 생성 및 전송
            yield return handler.CreateAnswer(
                onSuccess: (desc) => 
                {
                    var answerMessage = new TargetedSessionDescriptionMessage("answer", desc.sdp, peerId);
                    _ = signalingClient.SendMessage(answerMessage);
                },
                onError: (error) => 
                {
                    Debug.LogError($"[MultiPeerWebRtcManager] Failed to create answer for {peerId}: {error}");
                }
            );
        }

        private void HandleAnswer(string jsonData)
        {
            var message = JsonUtility.FromJson<TargetedSessionDescriptionMessage>(jsonData);
            
            if (!connectionHandlers.TryGetValue(message.sourcePeerId, out var handler))
            {
                Debug.LogWarning($"[MultiPeerWebRtcManager] Received answer from unknown peer: {message.sourcePeerId}");
                return;
            }

            StartCoroutine(HandleAnswerCoroutine(message.sourcePeerId, message.sdp));
        }

        private IEnumerator HandleAnswerCoroutine(string peerId, string sdp)
        {
            if (!connectionHandlers.TryGetValue(peerId, out var handler)) yield break;

            yield return handler.HandleAnswer(sdp,
                onSuccess: () => 
                {
                    Debug.Log($"[MultiPeerWebRtcManager] Successfully handled answer from {peerId}");
                },
                onError: (error) => 
                {
                    Debug.LogError($"[MultiPeerWebRtcManager] Failed to handle answer from {peerId}: {error}");
                }
            );
        }

        private void HandleIceCandidate(string jsonData)
        {
            var message = JsonUtility.FromJson<TargetedIceCandidateMessage>(jsonData);
            
            if (!connectionHandlers.TryGetValue(message.sourcePeerId, out var handler))
            {
                Debug.LogWarning($"[MultiPeerWebRtcManager] Received ICE candidate from unknown peer: {message.sourcePeerId}");
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

        private void HandleIceCandidateGenerated(string peerId, RTCIceCandidate candidate)
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

        private void HandleConnectionStateChange(string peerId, RTCPeerConnectionState state)
        {
            if (!connectionHandlers.TryGetValue(peerId, out var handler)) return;

            Debug.Log($"[MultiPeerWebRtcManager] Peer {peerId} connection state: {state}");

            switch (state)
            {
                case RTCPeerConnectionState.Connected:
                    OnPeerConnected?.Invoke(peerId);
                    break;

                case RTCPeerConnectionState.Disconnected:
                case RTCPeerConnectionState.Failed:
                case RTCPeerConnectionState.Closed:
                    OnPeerDisconnected?.Invoke(peerId);
                    break;
            }
        }


        private void HandleVideoTrackReceived(string peerId, MediaStreamTrack track)
        {
            Debug.Log($"[MultiPeerWebRtcManager] Video track received from {peerId}");
            OnTrackReceived?.Invoke(peerId, track);
            OnVideoTrackReceived?.Invoke(peerId, track as VideoStreamTrack);
        }

        private void HandleAudioTrackReceived(string peerId, MediaStreamTrack track)
        {
            Debug.Log($"[MultiPeerWebRtcManager] Audio track received from {peerId}");
            OnTrackReceived?.Invoke(peerId, track);
            OnAudioTrackReceived?.Invoke(peerId, track as AudioStreamTrack);
        }

        private void HandleNegotiationNeeded(string peerId)
        {
            if (peerRole == PeerRole.Host && connectionHandlers.ContainsKey(peerId))
            {
                Debug.Log($"[MultiPeerWebRtcManager] Renegotiation needed for {peerId}");
                StartCoroutine(CreateAndSendOffer(peerId));
            }
        }


        #endregion

        #region Public Properties

        public bool IsSignalingConnected => _isSignalingConnected;
        public int ActiveConnectionsCount => _activeConnectionsCount;
        public List<string> ConnectedPeerIds => new List<string>(_connectedPeerIds);
        public PeerRole Role => peerRole;
        public string RoomId => roomId;

        #endregion
    }
}