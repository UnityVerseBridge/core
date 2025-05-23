using System;
using System.Threading.Tasks;
using UnityEngine; // Debug, JsonUtility
using UnityVerseBridge.Core.Signaling.Data;

namespace UnityVerseBridge.Core.Signaling
{
    /// <summary>
    /// IWebSocketClient 인터페이스를 사용하여 실제 WebSocket 통신을 수행하고,
    /// WebRTC 시그널링 메시지 처리를 담당하는 클래스.
    /// ISignalingClient 인터페이스를 구현한다.
    /// </summary>
    public class SignalingClient : ISignalingClient
    {
        private IWebSocketClient webSocketAdapter; // 구체적인 구현 대신 인터페이스에 의존!
        private string currentServerUrl;

        // ISignalingClient 인터페이스 이벤트 구현
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string, string> OnSignalingMessageReceived;

        // ISignalingClient 인터페이스 속성 구현
        public bool IsConnected => webSocketAdapter != null && webSocketAdapter.State == WebSocketState.Open;

        /// <summary>
        /// 외부에서 WebSocket 어댑터와 서버 URL을 받아 초기화하고 연결을 시도한다.
        /// </summary>
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

            Debug.Log($"SignalingClient Initialized with {adapter.GetType().Name}. Connecting to {url}...");
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
            if (!IsConnected)
            {
                Debug.LogWarning("Cannot send message, WebSocket adapter is not connected or open.");
                return; // Task<bool> 등으로 실패 반환 고려
            }

            try
            {
                string jsonMessage = JsonUtility.ToJson(message);
                Debug.Log($"[SignalingClient] Sending Message (Type: {message.type}): {jsonMessage}");
                // 어댑터를 통해 텍스트 전송
                await webSocketAdapter.SendText(jsonMessage);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to serialize or send message via adapter: {e.Message}");
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
            Debug.Log("[SignalingClient] WebSocket Adapter Connected.");
            OnConnected?.Invoke();
        }

        private void HandleAdapterMessage(byte[] bytes)
        {
             // 이 부분은 이전과 동일 (JSON 파싱 및 OnSignalingMessageReceived 호출)
            try {
                var messageJson = System.Text.Encoding.UTF8.GetString(bytes);
                Debug.Log($"[SignalingClient] Raw message received: {messageJson}");
                var baseMessage = JsonUtility.FromJson<SignalingMessageBase>(messageJson);
                if (baseMessage != null && !string.IsNullOrEmpty(baseMessage.type)) {
                    Debug.Log($"[SignalingClient] Message type: {baseMessage.type}");
                    OnSignalingMessageReceived?.Invoke(baseMessage.type, messageJson);
                } else { Debug.LogWarning($"[SignalingClient] Received message without type: {messageJson}"); }
            } catch (Exception e) { Debug.LogError($"[SignalingClient] Error parsing message: {e.Message}"); }
        }

        private void HandleAdapterError(string errorMsg)
        {
            Debug.LogError($"[SignalingClient] WebSocket Adapter Error: {errorMsg}");
            // 필요시 별도 에러 이벤트 정의 및 발생
            // HandleAdapterClose(1006); // 에러 시 Close 처리 (Close Code는 예시)
        }

        private void HandleAdapterClose(ushort code)
        {
            Debug.Log($"[SignalingClient] WebSocket Adapter Closed. Code: {code}");
            // Disconnect 메서드에서 이벤트 구독 해지를 먼저 하므로, 여기서는 이벤트 발생만 처리
            OnDisconnected?.Invoke();
             // 필요시 webSocketAdapter = null; 처리 (Disconnect에서 이미 할 수 있음)
        }
    }
}