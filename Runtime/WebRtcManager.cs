using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Unity.WebRTC;
using UnityVerseBridge.Core.Signaling; // ISignalingClient 등 사용
using UnityVerseBridge.Core.Signaling.Data; // 시그널링 데이터 구조
using UnityVerseBridge.Core.DataChannel.Data; // 데이터 채널 데이터 구조
using UnityVerseBridge.Core.Common; // WebRtcConfiguration

namespace UnityVerseBridge.Core
{
    public class WebRtcManager : MonoBehaviour
    {
        [Header("Configuration")]
        // Inspector에서 설정하거나 코드로 주입할 수 있음
        [SerializeField] private WebRtcConfiguration configuration = new WebRtcConfiguration();
        [SerializeField] private string signalingServerUrl = "ws://localhost:8080"; // 기본값, Inspector에서 변경 가능

        [Header("Dependencies")]
        // 실제 구현은 숨기고 인터페이스만 노출 (Inspector에서는 직접 할당 어려움)
        private ISignalingClient signalingClient;

        [Header("WebRTC Objects")]
        private RTCPeerConnection peerConnection;
        private RTCDataChannel dataChannel;
        // TODO: Add fields for local/remote media streams/tracks

        // --- Public Events ---
        public event Action OnSignalingConnected;
        public event Action OnSignalingDisconnected;
        public event Action OnWebRtcConnected; // PeerConnection 연결 성공
        public event Action OnWebRtcDisconnected; // PeerConnection 연결 끊김
        public event Action<string> OnDataChannelOpened; // 데이터 채널 이름 전달
        public event Action OnDataChannelClosed;
        public event Action<string> OnDataChannelMessageReceived; // 수신된 메시지 전달
        // TODO: Add events for Track Received etc.

        // --- Public Properties ---
        public bool IsSignalingConnected => signalingClient?.IsConnected ?? false;
        public bool IsWebRtcConnected => peerConnection?.ConnectionState == RTCPeerConnectionState.Connected;


        // --- MonoBehaviour Methods ---

        void Awake()
        {
            // 실제 SignalingClient 구현체 생성 (나중에는 의존성 주입 프레임워크 사용 고려)
            signalingClient = new SignalingClient();
        }
        
        // WebRtcManager.cs 안에 추가
        // 테스트 코드에서 Mock 객체를 주입하기 위한 public 메서드
        public void InitializeForTest(ISignalingClient client, WebRtcConfiguration config)
        {
            // Awake나 Start에서 SignalingClient를 new로 생성하는 부분이 있다면 비활성화하거나 조건 처리 필요
            signalingClient = client; // 외부에서 주입받은 Mock 객체 사용
            configuration = config ?? new WebRtcConfiguration();

            // Start() 등에서 하던 이벤트 구독 로직을 여기서 명시적으로 호출
            SubscribeSignalingEvents();
            Debug.Log("WebRtcManager Initialized for Test.");
        }

        // 이벤트 구독 로직을 별도 메서드로 분리 (Start/Awake에서 호출되지 않도록 주의)
        private void SubscribeSignalingEvents()
        {
            if (signalingClient == null) return;
            // 중복 구독 방지를 위해 먼저 해지
            signalingClient.OnConnected -= HandleSignalingConnected;
            signalingClient.OnDisconnected -= HandleSignalingDisconnected;
            signalingClient.OnSignalingMessageReceived -= HandleSignalingMessage;
            // 다시 구독
            signalingClient.OnConnected += HandleSignalingConnected;
            signalingClient.OnDisconnected += HandleSignalingDisconnected;
            signalingClient.OnSignalingMessageReceived += HandleSignalingMessage;
        }

        void Update()
        {
            // NativeWebSocket 메시지 큐 처리
            // ISignalingClient에 DispatchMessages 메서드가 없다면 SignalingClient 타입으로 캐스팅 필요
            (signalingClient as SignalingClient)?.DispatchMessages();
        }

        void OnDestroy()
        {
            Debug.Log("WebRtcManager OnDestroy: Cleaning up...");
            Disconnect(); // 모든 연결 및 리소스 정리
        }

        // --- Public Methods ---

        public void ConnectSignaling()
        {
            if (!IsSignalingConnected)
            {
                _ = signalingClient.Connect(signalingServerUrl); // 비동기 호출 (반환값 무시)
            }
        }

        // 연결 시작 (Offer 생성 측에서 호출)
        public void StartPeerConnection()
        {
            if (!IsSignalingConnected)
            {
                Debug.LogError("Cannot start peer connection, signaling is not connected.");
                return;
            }
            if (peerConnection != null)
            {
                 Debug.LogWarning("Peer connection already exists or is starting.");
                 return;
            }
            Debug.Log("Starting Peer Connection process as offerer...");
            CreatePeerConnection();
            CreateDataChannel(); // Offer 생성 전에 데이터 채널 생성
            StartCoroutine(CreateOfferAndSend()); // Offer 생성 및 전송
        }

        // 데이터 채널을 통해 메시지 전송
        public void SendDataChannelMessage(object messageData)
        {
            if (dataChannel == null || dataChannel.ReadyState != RTCDataChannelState.Open)
            {
                Debug.LogWarning("Data channel is not open. Cannot send message.");
                return;
            }
            try
            {
                 // JsonUtility는 [System.Serializable] 필요, 복잡 객체는 Newtonsoft.Json 등 고려
                string jsonMessage = JsonUtility.ToJson(messageData);
                Debug.Log($"Sending Data Channel Message: {jsonMessage}");
                dataChannel.Send(jsonMessage);
            }
            catch (Exception e)
            {
                 Debug.LogError($"Failed to serialize or send data channel message: {e.Message}");
            }
        }


        public async void Disconnect()
        {
            Debug.Log("Disconnecting WebRTC and Signaling...");
            // DataChannel 닫기
            dataChannel?.Close();
            dataChannel = null;

            // PeerConnection 닫기
            peerConnection?.Close();
            peerConnection = null; // 이벤트 핸들러 등 내부 정리됨

            // Signaling 연결 끊기
            if (signalingClient != null)
            {
                await signalingClient.Disconnect();
                // 이벤트 구독 해지 (중요)
                signalingClient.OnConnected -= HandleSignalingConnected;
                signalingClient.OnDisconnected -= HandleSignalingDisconnected;
                signalingClient.OnSignalingMessageReceived -= HandleSignalingMessage;
                // signalingClient = null; // 필요시 null 처리
            }
            OnWebRtcDisconnected?.Invoke();
        }


        // --- Private Methods ---

        private void CreatePeerConnection()
        {
            Debug.Log("Creating Peer Connection...");
            var rtcConfig = configuration.ToRTCConfiguration(); // 설정 가져오기
            peerConnection = new RTCPeerConnection(ref rtcConfig);

            // 이벤트 핸들러 등록
            peerConnection.OnIceCandidate = HandleIceCandidateGenerated;
            peerConnection.OnIceConnectionChange = HandleIceConnectionChange;
            peerConnection.OnConnectionStateChange = HandleConnectionStateChange;
            peerConnection.OnDataChannel = HandleDataChannelReceived;
            peerConnection.OnTrack = HandleTrackReceived;
            peerConnection.OnNegotiationNeeded = HandleNegotiationNeeded;
        }

        private void CreateDataChannel()
        {
            if (peerConnection == null) return;
            try
            {
                Debug.Log($"Creating Data Channel: {configuration.dataChannelLabel}");
                RTCDataChannelInit options = new RTCDataChannelInit() { ordered = true }; // 필요시 옵션 설정
                dataChannel = peerConnection.CreateDataChannel(configuration.dataChannelLabel, options);
                SetupDataChannelEvents(dataChannel);
            }
            catch (Exception e)
            {
                 Debug.LogError($"Failed to create data channel: {e.Message}");
            }
        }

        private void SetupDataChannelEvents(RTCDataChannel channel)
        {
            if (channel == null) return;
            channel.OnOpen = () => {
                Debug.Log($"Data Channel '{channel.Label}' Opened!");
                OnDataChannelOpened?.Invoke(channel.Label);
            };
            channel.OnClose = () => {
                Debug.Log($"Data Channel '{channel.Label}' Closed!");
                OnDataChannelClosed?.Invoke();
            };
            channel.OnMessage = (bytes) => {
                var message = System.Text.Encoding.UTF8.GetString(bytes);
                // Debug.Log($"DataChannel Message Received: {message}"); // 너무 많으면 주석 처리
                OnDataChannelMessageReceived?.Invoke(message);
            };
            channel.OnError = (error) => {
                Debug.LogError($"Data Channel Error: {error}");
            };
        }

        // --- Signaling Event Handlers ---
        private void HandleSignalingConnected() => OnSignalingConnected?.Invoke();
        private void HandleSignalingDisconnected() => OnSignalingDisconnected?.Invoke();

        private void HandleSignalingMessage(string type, string jsonData)
        {
            Debug.Log($"Handling Signaling Message | Type: {type}");
            // PeerConnection이 없는데 Offer 외 메시지가 오면 무시 (상황에 따라 다름)
             if (peerConnection == null && type != "offer") {
                 Debug.LogWarning($"Received signaling message '{type}' before peer connection was initialized.");
                 return;
             }

            try {
                switch (type)
                {
                    case "offer":
                        // Offer를 받으면 PeerConnection 생성 및 Answer 전송 시작
                        CreatePeerConnection(); // 주의: 이미 있다면? 상태 관리 필요
                        StartCoroutine(HandleOfferAndSendAnswer(JsonUtility.FromJson<SessionDescriptionMessage>(jsonData)));
                        break;
                    case "answer":
                        StartCoroutine(HandleAnswer(JsonUtility.FromJson<SessionDescriptionMessage>(jsonData)));
                        break;
                    case "ice-candidate":
                        HandleIceCandidate(JsonUtility.FromJson<IceCandidateMessage>(jsonData));
                        break;
                    default:
                        Debug.LogWarning($"Unknown signaling message type received: {type}");
                        break;
                }
            } catch(Exception e) {
                 Debug.LogError($"Error handling signaling message (Type: {type}): {e.Message}\nData: {jsonData}");
            }
        }

        // --- WebRTC Logic Coroutines/Methods ---

        private IEnumerator CreateOfferAndSend()
        {
            if (peerConnection == null) yield break;
            Debug.Log("Creating Offer...");
            var offerOp = peerConnection.CreateOffer();
            yield return offerOp;

            if (!offerOp.IsError)
            {
                var offerDesc = offerOp.Desc;
                var localDescOp = peerConnection.SetLocalDescription(ref offerDesc);
                yield return localDescOp;

                if (!localDescOp.IsError)
                {
                    Debug.Log("Local Description (Offer) Set. Sending Offer...");
                    var offerMsg = new SessionDescriptionMessage("offer", offerDesc.sdp);
                    _ = signalingClient.SendMessage(offerMsg); // 비동기 호출
                } else { Debug.LogError($"Failed to set local description (Offer): {localDescOp.Error.message}"); }
            } else { Debug.LogError($"Failed to create offer: {offerOp.Error.message}"); }
        }

         private IEnumerator HandleOfferAndSendAnswer(SessionDescriptionMessage offerMessage)
        {
            if (peerConnection == null) yield break; // 이미 생성되었어야 함
            Debug.Log("Received Offer, Setting Remote Description...");
            RTCSessionDescription offerDesc = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = offerMessage.sdp };
            var remoteDescOp = peerConnection.SetRemoteDescription(ref offerDesc);
            yield return remoteDescOp;

            if (!remoteDescOp.IsError)
            {
                Debug.Log("Remote Description (Offer) Set. Creating Answer...");
                var answerOp = peerConnection.CreateAnswer();
                yield return answerOp;

                if (!answerOp.IsError)
                {
                    var answerDesc = answerOp.Desc;
                    var localDescOp = peerConnection.SetLocalDescription(ref answerDesc);
                    yield return localDescOp;

                    if (!localDescOp.IsError)
                    {
                        Debug.Log("Local Description (Answer) Set. Sending Answer...");
                        var answerMsg = new SessionDescriptionMessage("answer", answerDesc.sdp);
                         _ = signalingClient.SendMessage(answerMsg);
                    } else { Debug.LogError($"Failed to set local description (Answer): {localDescOp.Error.message}"); }
                } else { Debug.LogError($"Failed to create answer: {answerOp.Error.message}"); }
            } else { Debug.LogError($"Failed to set remote description (Offer): {remoteDescOp.Error.message}"); }
        }

        private IEnumerator HandleAnswer(SessionDescriptionMessage answerMessage)
        {
            if (peerConnection == null) yield break;
             Debug.Log("Received Answer, Setting Remote Description...");
            RTCSessionDescription answerDesc = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = answerMessage.sdp };
            var remoteDescOp = peerConnection.SetRemoteDescription(ref answerDesc);
            yield return remoteDescOp;

            if (remoteDescOp.IsError) { Debug.LogError($"Failed to set remote description (Answer): {remoteDescOp.Error.message}"); }
            else { Debug.Log("Remote Description (Answer) Set Successfully."); }
        }

        private void HandleIceCandidate(IceCandidateMessage candidateMessage)
        {
            if (peerConnection == null) return;
             try {
                 RTCIceCandidate candidate = candidateMessage.ToRTCIceCandidate();
                 peerConnection.AddIceCandidate(candidate);
                 // Debug.Log("ICE Candidate Added."); // 너무 자주 로깅될 수 있음
             } catch (Exception e) {
                 Debug.LogError($"Error adding received ICE candidate: {e.Message}");
             }
        }

        private void HandleIceCandidateGenerated(RTCIceCandidate candidate)
        {
            if (candidate != null && !string.IsNullOrEmpty(candidate.Candidate))
            {
                // Debug.Log($"ICE Candidate Generated: {candidate.Candidate}"); // 너무 자주 로깅될 수 있음
                var candidateMsg = IceCandidateMessage.FromRTCIceCandidate(candidate);
                _ = signalingClient.SendMessage(candidateMsg);
            }
        }

        // --- PeerConnection Event Handlers ---
        private void HandleIceConnectionChange(RTCIceConnectionState state) { Debug.Log($"ICE Connection State: {state}"); }

        private void HandleConnectionStateChange(RTCPeerConnectionState state) {
            Debug.Log($"Peer Connection State: {state}");
            if (state == RTCPeerConnectionState.Connected) { OnWebRtcConnected?.Invoke(); }
            else if (state == RTCPeerConnectionState.Failed || state == RTCPeerConnectionState.Disconnected || state == RTCPeerConnectionState.Closed) {
                OnWebRtcDisconnected?.Invoke();
                // 연결 실패/종료 시 정리 작업 고려
                // Disconnect(); // 필요시 자동 재연결 로직 등 고려
            }
        }

        private void HandleDataChannelReceived(RTCDataChannel channel)
        {
            Debug.Log($"Data Channel Received: {channel.Label}");
            if (dataChannel == null) // 이미 생성하지 않았다면 (상대방이 먼저 만들었다면)
            {
                dataChannel = channel;
                SetupDataChannelEvents(dataChannel);
            }
            else
            {
                 Debug.LogWarning($"Another data channel '{channel.Label}' received, but one already exists.");
                 // 필요시 추가 채널 처리 로직
            }
        }

        private void HandleTrackReceived(RTCTrackEvent e)
        {
            Debug.Log($"Track Received: Kind={e.Track.Kind}, ID={e.Track.Id}");
            // TODO: 수신된 비디오/오디오 트랙 처리 로직 (예: RawImage에 비디오 표시)
            // if (e.Track.Kind == TrackKind.Video) { ... }
        }

        private void HandleNegotiationNeeded()
        {
            Debug.Log("Negotiation Needed event triggered.");
            // TODO: 필요시 재협상 로직 구현 (예: 스트림 추가/제거 후 Offer 다시 생성)
            // StartCoroutine(CreateOfferAndSend()); // 단순 예시, 실제로는 더 복잡한 상태 관리 필요
        }

        // --- Helper ---
        // WebRtcConfiguration 클래스에 이 메서드를 넣는 것이 더 적합할 수 있음
        // private RTCConfiguration ToRTCConfiguration(WebRtcConfiguration config) { ... }
    }
}

// WebRtcConfiguration 클래스의 ToRTCConfiguration 헬퍼 예시
namespace UnityVerseBridge.Core.Common
{
    using System.Collections.Generic;
    using Unity.WebRTC;

    public static class WebRtcConfigurationExtensions
    {
        // WebRtcConfiguration 객체를 Unity WebRTC의 RTCConfiguration으로 변환
        public static RTCConfiguration ToRTCConfiguration(this WebRtcConfiguration config)
        {
             RTCConfiguration rtcConfig = default; // 기본값 사용
             if (config?.iceServers != null && config.iceServers.Count > 0) {
                 rtcConfig.iceServers = config.iceServers.ToArray();
             }
             // 필요시 다른 설정 추가 (예: iceTransportPolicy)
             // rtcConfig.iceTransportPolicy = RTCIceTransportPolicy.All; // 예시
             return rtcConfig;
        }
    }
}