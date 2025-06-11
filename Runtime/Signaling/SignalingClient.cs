using System;
using System.Threading.Tasks;
using UnityEngine; // Debug, JsonUtility
using UnityVerseBridge.Core.Signaling.Data;

namespace UnityVerseBridge.Core.Signaling
{
    /// <summary>
    /// WebRTC 시그널링 클라이언트 구현체입니다.
    /// 
    /// 주요 역할:
    /// 1. 시그널링 서버와 WebSocket 연결 관리
    /// 2. SDP Offer/Answer 교환
    /// 3. ICE Candidate 교환
    /// 4. 룸 기반 피어 매칭
    /// 
    /// 설계 패턴: Adapter Pattern을 사용하여 플랫폼별 WebSocket 구현을 추상화
    /// </summary>
    public class SignalingClient : ISignalingClient
    {
        private IWebSocketClient webSocketAdapter; // DI: 플랫폼별 구현체 주입
        private string currentServerUrl;

        // ISignalingClient 인터페이스 이벤트 구현
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string, string> OnSignalingMessageReceived;

        // ISignalingClient 인터페이스 속성 구현
        public bool IsConnected => webSocketAdapter != null && webSocketAdapter.State == WebSocketState.Open;

        /// <summary>
        /// WebSocket 어댑터를 주입받아 시그널링 서버에 연결합니다.
        /// 
        /// 사용 예시:
        /// - Quest: SystemWebSocketAdapter 사용
        /// - Mobile: NativeWebSocketAdapter 사용
        /// </summary>
        /// <param name="adapter">플랫폼별 WebSocket 구현체</param>
        /// <param name="url">시그널링 서버 URL (ws:// 또는 wss://)</param>
        public async Task InitializeAndConnect(IWebSocketClient adapter, string url)
        {
            if (webSocketAdapter != null && webSocketAdapter.State != WebSocketState.Closed)
            {
                Debug.LogWarning("SignalingClient already initialized and possibly connected. Disconnecting first.");
                await Disconnect(); // 기존 연결 정리
            }

            webSocketAdapter = adapter ?? throw new ArgumentNullException(nameof(adapter)); // Null 체크
            currentServerUrl = url ?? throw new ArgumentNullException(nameof(url));

            // 어댑터 이벤트 구독
            webSocketAdapter.OnOpen += HandleAdapterOpen;
            webSocketAdapter.OnMessage += HandleAdapterMessage;
            webSocketAdapter.OnError += HandleAdapterError;
            webSocketAdapter.OnClose += HandleAdapterClose;

            try
            {
                await webSocketAdapter.Connect(currentServerUrl);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to connect via WebSocket adapter: {e.Message}");
                HandleAdapterError($"Connection failed: {e.Message}"); // 에러 처리
            }
        }

        // ISignalingClient 인터페이스 메서드 구현 (Connect는 InitializeAndConnect로 대체)
        public async Task Connect(string url)
        {
             // 이 메서드는 직접 사용하기보다 InitializeAndConnect를 사용하도록 유도하거나,
             // 내부적으로 기본 어댑터(예: NativeWebSocketAdapter)를 생성하여 InitializeAndConnect를 호출하도록 구현할 수도 있음.
             // 여기서는 InitializeAndConnect 사용을 가정하고 경고만 남김.
            Debug.LogWarning("Please use InitializeAndConnect with an adapter instead of calling Connect directly.");
            await Task.CompletedTask; // 혹은 예외 발생
        }


        public async Task Disconnect()
        {
            if (webSocketAdapter != null)
            {
                Debug.Log("Disconnecting WebSocket adapter...");
                // 이벤트 구독 해지 먼저 수행 (Close 처리 중 이벤트 발생 방지)
                webSocketAdapter.OnOpen -= HandleAdapterOpen;
                webSocketAdapter.OnMessage -= HandleAdapterMessage;
                webSocketAdapter.OnError -= HandleAdapterError;
                webSocketAdapter.OnClose -= HandleAdapterClose;

                if (webSocketAdapter.State != WebSocketState.Closed)
                {
                    await webSocketAdapter.Close();
                }
                webSocketAdapter = null; // 참조 해제
                // OnDisconnected 이벤트는 HandleAdapterClose에서 호출됨
            }
        }

        public async Task SendMessage<T>(T message) where T : SignalingMessageBase
        {
            if (message == null)
            {
                Debug.LogWarning("[SignalingClient] Cannot send null message.");
                return;
            }
            
            if (!IsConnected)
            {
                Debug.LogWarning("[SignalingClient] Cannot send message, WebSocket adapter is not connected or open.");
                return; // Task<bool> 등으로 실패 반환 고려
            }
            
            if (webSocketAdapter == null)
            {
                Debug.LogError("[SignalingClient] WebSocket adapter is null.");
                return;
            }

            try
            {
                string jsonMessage = JsonUtility.ToJson(message);
                // 어댑터를 통해 텍스트 전송
                await webSocketAdapter.SendText(jsonMessage);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SignalingClient] Failed to serialize or send message via adapter: {e.Message}");
            }
        }

        /// <summary>
        /// WebRtcManager의 Update 등에서 주기적으로 호출되어 수신 메시지 큐를 처리한다.
        /// </summary>
        public void DispatchMessages()
        {
            webSocketAdapter?.DispatchMessageQueue();
        }

        // --- Adapter Event Handlers ---
        private void HandleAdapterOpen()
        {
            OnConnected?.Invoke();
        }

        private void HandleAdapterMessage(byte[] bytes)
        {
            try 
            {
                var messageJson = System.Text.Encoding.UTF8.GetString(bytes);
                
                // 메시지가 비어있거나 null인 경우 처리
                if (string.IsNullOrWhiteSpace(messageJson))
                {
                    Debug.LogWarning("[SignalingClient] Received empty message");
                    return;
                }
                
                Debug.Log($"[SignalingClient] Received message: {messageJson}");
                
                // JSON 형식 기본 검증
                // WebSocket 메시지는 다양한 형식일 수 있으므로 기본 검증만 수행
                messageJson = messageJson.Trim();
                if (!messageJson.StartsWith("{") || !messageJson.EndsWith("}"))
                {
                    Debug.LogWarning($"[SignalingClient] Invalid JSON format: {messageJson.Substring(0, Math.Min(messageJson.Length, 100))}");
                    return;
                }
                
                var baseMessage = JsonUtility.FromJson<SignalingMessageBase>(messageJson);
                if (baseMessage != null && !string.IsNullOrEmpty(baseMessage.type)) 
                {
                    Debug.Log($"[SignalingClient] Message type: {baseMessage.type}");
                    OnSignalingMessageReceived?.Invoke(baseMessage.type, messageJson);
                } 
                else 
                { 
                    Debug.LogWarning($"[SignalingClient] Received message without type: {messageJson.Substring(0, Math.Min(messageJson.Length, 200))}"); 
                }
            } 
            catch (Exception e) 
            { 
                Debug.LogError($"[SignalingClient] Error parsing message: {e.Message}"); 
            }
        }

        private void HandleAdapterError(string errorMsg)
        {
            Debug.LogError($"[SignalingClient] WebSocket Adapter Error: {errorMsg}");
            // 필요시 별도 에러 이벤트 정의 및 발생
            // HandleAdapterClose(1006); // 에러 시 Close 처리 (Close Code는 예시)
        }

        private void HandleAdapterClose(ushort code)
        {
            Debug.Log($"[SignalingClient] WebSocket closed with code: {code}");
            
            // Common close codes:
            // 1000 - Normal closure
            // 1001 - Going away
            // 1002 - Protocol error
            // 1003 - Unsupported data
            // 1006 - Abnormal closure
            // 1008 - Policy violation (auth failure)
            // 1009 - Message too big
            // 1011 - Internal server error
            
            string reason = code switch
            {
                1000 => "Normal closure",
                1001 => "Going away",
                1002 => "Protocol error",
                1003 => "Unsupported data",
                1006 => "Abnormal closure",
                1008 => "Policy violation (possibly authentication required)",
                1009 => "Message too big",
                1011 => "Internal server error",
                _ => "Unknown reason"
            };
            
            Debug.Log($"[SignalingClient] Close reason: {reason}");
            
            // Disconnect 메서드에서 이벤트 구독 해지를 먼저 하므로, 여기서는 이벤트 발생만 처리
            OnDisconnected?.Invoke();
        }
    }
}