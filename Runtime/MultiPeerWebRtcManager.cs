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

        // 피어 연결 관리
        private Dictionary<string, PeerConnectionContext> peerConnections = new Dictionary<string, PeerConnectionContext>();
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

        private class PeerConnectionContext
        {
            public string PeerId { get; set; }
            public RTCPeerConnection Connection { get; set; }
            public RTCDataChannel DataChannel { get; set; }
            public MediaStream ReceiveStream { get; set; }
            public bool IsConnected { get; set; }
            public DateTime LastActivity { get; set; }
            public Dictionary<MediaStreamTrack, RTCRtpSender> TrackSenders { get; set; } = new Dictionary<MediaStreamTrack, RTCRtpSender>();
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
            _activeConnectionsCount = peerConnections.Count(p => p.Value.IsConnected);
            _connectedPeerIds = peerConnections.Where(p => p.Value.IsConnected).Select(p => p.Key).ToList();
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
                foreach (var peer in peerConnections.Values.Where(p => p.IsConnected))
                {
                    AddTrackToPeer(peer, videoTrack);
                }
            }
            else
            {
                // 클라이언트는 호스트에게만 트랙 전송
                var hostPeer = peerConnections.Values.FirstOrDefault();
                if (hostPeer != null && hostPeer.IsConnected)
                {
                    AddTrackToPeer(hostPeer, videoTrack);
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
                foreach (var peer in peerConnections.Values.Where(p => p.IsConnected))
                {
                    AddTrackToPeer(peer, audioTrack);
                }
            }
            else
            {
                // 클라이언트는 호스트에게만 트랙 전송
                var hostPeer = peerConnections.Values.FirstOrDefault();
                if (hostPeer != null && hostPeer.IsConnected)
                {
                    AddTrackToPeer(hostPeer, audioTrack);
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
            foreach (var peer in peerConnections.Values)
            {
                RemoveTrackFromPeer(peer, track);
            }
        }

        public void SendDataChannelMessage(string peerId, object messageData)
        {
            if (peerConnections.TryGetValue(peerId, out var peer) && peer.DataChannel?.ReadyState == RTCDataChannelState.Open)
            {
                try
                {
                    string jsonMessage = JsonUtility.ToJson(messageData);
                    peer.DataChannel.Send(jsonMessage);
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
            
            foreach (var peer in peerConnections.Values.Where(p => p.DataChannel?.ReadyState == RTCDataChannelState.Open))
            {
                try
                {
                    peer.DataChannel.Send(jsonMessage);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MultiPeerWebRtcManager] Failed to broadcast to {peer.PeerId}: {e.Message}");
                }
            }
        }

        public void DisconnectPeer(string peerId)
        {
            if (peerConnections.TryGetValue(peerId, out var peer))
            {
                CleanupPeerConnection(peer);
                peerConnections.Remove(peerId);
                OnPeerDisconnected?.Invoke(peerId);
            }
        }

        public void DisconnectAll()
        {
            foreach (var peer in peerConnections.Values.ToList())
            {
                CleanupPeerConnection(peer);
            }
            peerConnections.Clear();
            
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
            
            if (peerConnections.Count >= maxConnections)
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
            if (peerConnections.ContainsKey(peerId))
            {
                Debug.LogWarning($"[MultiPeerWebRtcManager] Peer connection already exists: {peerId}");
                return;
            }

            var context = new PeerConnectionContext
            {
                PeerId = peerId,
                LastActivity = DateTime.Now
            };

            // RTCPeerConnection 생성
            var rtcConfig = configuration.ToRTCConfiguration();
            context.Connection = new RTCPeerConnection(ref rtcConfig);
            
            // 이벤트 핸들러 설정
            context.Connection.OnIceCandidate = (candidate) => HandleIceCandidateGenerated(peerId, candidate);
            context.Connection.OnIceConnectionChange = (state) => HandleIceConnectionChange(peerId, state);
            context.Connection.OnConnectionStateChange = (state) => HandleConnectionStateChange(peerId, state);
            context.Connection.OnTrack = (e) => HandleTrackReceived(peerId, e);
            context.Connection.OnDataChannel = (channel) => HandleDataChannelReceived(peerId, channel);
            context.Connection.OnNegotiationNeeded = () => HandleNegotiationNeeded(peerId);

            // DataChannel 생성 (호스트만)
            if (peerRole == PeerRole.Host)
            {
                var dataChannelInit = new RTCDataChannelInit { ordered = true };
                context.DataChannel = context.Connection.CreateDataChannel($"data-{peerId}", dataChannelInit);
                SetupDataChannelEvents(context.DataChannel, peerId);
            }

            // 로컬 트랙 추가
            foreach (var track in localTracks)
            {
                AddTrackToPeer(context, track);
            }

            peerConnections[peerId] = context;

            if (createOffer)
            {
                StartCoroutine(CreateAndSendOffer(peerId));
            }
        }

        private void AddTrackToPeer(PeerConnectionContext peer, MediaStreamTrack track)
        {
            if (peer.Connection == null || track == null) return;

            var sender = peer.Connection.AddTrack(track);
            if (sender != null)
            {
                peer.TrackSenders[track] = sender;
                Debug.Log($"[MultiPeerWebRtcManager] Added track {track.Id} to peer {peer.PeerId}");
            }
        }

        private void RemoveTrackFromPeer(PeerConnectionContext peer, MediaStreamTrack track)
        {
            if (peer.TrackSenders.TryGetValue(track, out var sender))
            {
                peer.Connection.RemoveTrack(sender);
                peer.TrackSenders.Remove(track);
                Debug.Log($"[MultiPeerWebRtcManager] Removed track {track.Id} from peer {peer.PeerId}");
            }
        }

        private IEnumerator CreateAndSendOffer(string peerId)
        {
            if (!peerConnections.TryGetValue(peerId, out var peer)) yield break;

            var offerOp = peer.Connection.CreateOffer();
            yield return offerOp;

            if (offerOp.IsError)
            {
                Debug.LogError($"[MultiPeerWebRtcManager] Failed to create offer for {peerId}: {offerOp.Error.message}");
                yield break;
            }

            var offer = offerOp.Desc;
            var setLocalOp = peer.Connection.SetLocalDescription(ref offer);
            yield return setLocalOp;

            if (setLocalOp.IsError)
            {
                Debug.LogError($"[MultiPeerWebRtcManager] Failed to set local description for {peerId}: {setLocalOp.Error.message}");
                yield break;
            }

            // Offer 전송
            var offerMessage = new TargetedSessionDescriptionMessage("offer", offer.sdp, peerId);
            _ = signalingClient.SendMessage(offerMessage);
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
            if (!peerConnections.ContainsKey(message.sourcePeerId))
            {
                CreatePeerConnection(message.sourcePeerId, false);
            }

            StartCoroutine(HandleOfferAndSendAnswer(message.sourcePeerId, message.sdp));
        }

        private IEnumerator HandleOfferAndSendAnswer(string peerId, string sdp)
        {
            if (!peerConnections.TryGetValue(peerId, out var peer)) yield break;

            var offer = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = sdp };
            var setRemoteOp = peer.Connection.SetRemoteDescription(ref offer);
            yield return setRemoteOp;

            if (setRemoteOp.IsError)
            {
                Debug.LogError($"[MultiPeerWebRtcManager] Failed to set remote description for {peerId}: {setRemoteOp.Error.message}");
                yield break;
            }

            var answerOp = peer.Connection.CreateAnswer();
            yield return answerOp;

            if (answerOp.IsError)
            {
                Debug.LogError($"[MultiPeerWebRtcManager] Failed to create answer for {peerId}: {answerOp.Error.message}");
                yield break;
            }

            var answer = answerOp.Desc;
            var setLocalOp = peer.Connection.SetLocalDescription(ref answer);
            yield return setLocalOp;

            if (setLocalOp.IsError)
            {
                Debug.LogError($"[MultiPeerWebRtcManager] Failed to set local description for {peerId}: {setLocalOp.Error.message}");
                yield break;
            }

            // Answer 전송
            var answerMessage = new TargetedSessionDescriptionMessage("answer", answer.sdp, peerId);
            _ = signalingClient.SendMessage(answerMessage);
        }

        private void HandleAnswer(string jsonData)
        {
            var message = JsonUtility.FromJson<TargetedSessionDescriptionMessage>(jsonData);
            
            if (!peerConnections.TryGetValue(message.sourcePeerId, out var peer))
            {
                Debug.LogWarning($"[MultiPeerWebRtcManager] Received answer from unknown peer: {message.sourcePeerId}");
                return;
            }

            StartCoroutine(SetRemoteDescription(message.sourcePeerId, message.sdp, RTCSdpType.Answer));
        }

        private IEnumerator SetRemoteDescription(string peerId, string sdp, RTCSdpType type)
        {
            if (!peerConnections.TryGetValue(peerId, out var peer)) yield break;

            var desc = new RTCSessionDescription { type = type, sdp = sdp };
            var op = peer.Connection.SetRemoteDescription(ref desc);
            yield return op;

            if (op.IsError)
            {
                Debug.LogError($"[MultiPeerWebRtcManager] Failed to set remote description for {peerId}: {op.Error.message}");
            }
        }

        private void HandleIceCandidate(string jsonData)
        {
            var message = JsonUtility.FromJson<TargetedIceCandidateMessage>(jsonData);
            
            if (!peerConnections.TryGetValue(message.sourcePeerId, out var peer))
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

            peer.Connection.AddIceCandidate(new RTCIceCandidate(candidateInit));
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
            if (!peerConnections.TryGetValue(peerId, out var peer)) return;

            Debug.Log($"[MultiPeerWebRtcManager] Peer {peerId} connection state: {state}");

            switch (state)
            {
                case RTCPeerConnectionState.Connected:
                    peer.IsConnected = true;
                    peer.LastActivity = DateTime.Now;
                    OnPeerConnected?.Invoke(peerId);
                    break;

                case RTCPeerConnectionState.Disconnected:
                case RTCPeerConnectionState.Failed:
                case RTCPeerConnectionState.Closed:
                    peer.IsConnected = false;
                    OnPeerDisconnected?.Invoke(peerId);
                    break;
            }
        }

        private void HandleIceConnectionChange(string peerId, RTCIceConnectionState state)
        {
            Debug.Log($"[MultiPeerWebRtcManager] Peer {peerId} ICE state: {state}");
        }

        private void HandleTrackReceived(string peerId, RTCTrackEvent e)
        {
            if (!peerConnections.TryGetValue(peerId, out var peer)) return;

            Debug.Log($"[MultiPeerWebRtcManager] Track received from {peerId}: {e.Track.Kind}");
            
            OnTrackReceived?.Invoke(peerId, e.Track);

            if (e.Track.Kind == TrackKind.Video)
            {
                OnVideoTrackReceived?.Invoke(peerId, e.Track as VideoStreamTrack);
            }
            else if (e.Track.Kind == TrackKind.Audio)
            {
                OnAudioTrackReceived?.Invoke(peerId, e.Track as AudioStreamTrack);
            }
        }

        private void HandleDataChannelReceived(string peerId, RTCDataChannel channel)
        {
            if (!peerConnections.TryGetValue(peerId, out var peer)) return;

            peer.DataChannel = channel;
            SetupDataChannelEvents(channel, peerId);
        }

        private void SetupDataChannelEvents(RTCDataChannel channel, string peerId)
        {
            channel.OnOpen = () =>
            {
                Debug.Log($"[MultiPeerWebRtcManager] DataChannel opened with {peerId}");
            };

            channel.OnClose = () =>
            {
                Debug.Log($"[MultiPeerWebRtcManager] DataChannel closed with {peerId}");
            };

            channel.OnMessage = (bytes) =>
            {
                var message = System.Text.Encoding.UTF8.GetString(bytes);
                OnDataChannelMessageReceived?.Invoke(peerId, message);
            };
        }

        private void HandleNegotiationNeeded(string peerId)
        {
            if (peerRole == PeerRole.Host && peerConnections.ContainsKey(peerId))
            {
                Debug.Log($"[MultiPeerWebRtcManager] Renegotiation needed for {peerId}");
                StartCoroutine(CreateAndSendOffer(peerId));
            }
        }

        private void CleanupPeerConnection(PeerConnectionContext peer)
        {
            peer.DataChannel?.Close();
            peer.Connection?.Close();
            peer.ReceiveStream?.Dispose();
            peer.TrackSenders.Clear();
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