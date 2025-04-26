using System;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine; // Debug, JsonUtility
using UnityVerseBridge.Core.Signaling.Data; // SignalingMessageBase

namespace UnityVerseBridge.Core.Signaling
{
    public class SignalingClient : ISignalingClient
    {
        private WebSocket websocket;
        private string serverUrl;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string, string> OnSignalingMessageReceived; // type, jsonData

        public bool IsConnected => websocket != null && websocket.State == WebSocketState.Open;

        public async Task Connect(string url)
        {
            if (IsConnected || websocket?.State == WebSocketState.Connecting)
            {
                Debug.LogWarning($"WebSocket is already connected or connecting to {serverUrl}.");
                return;
            }

            serverUrl = url;
            websocket = new WebSocket(serverUrl);

            websocket.OnOpen += HandleOpen;
            websocket.OnError += HandleError;
            websocket.OnClose += HandleClose;
            websocket.OnMessage += HandleMessage;

            Debug.Log($"Attempting to connect to Signaling Server: {serverUrl}");
            await websocket.Connect();
        }

        public async Task Disconnect()
        {
            if (websocket != null && websocket.State != WebSocketState.Closed)
            {
                Debug.Log("Disconnecting from Signaling Server...");
                await websocket.Close();
                // HandleClose에서 이벤트 핸들러 제거 및 정리 수행
            }
            else
            {
                CleanupWebSocket(); // 이미 닫혔거나 없는 경우 정리만 수행
            }
        }

        public async Task SendMessage<T>(T message) where T : SignalingMessageBase
        {
            if (!IsConnected)
            {
                Debug.LogWarning("Cannot send message, WebSocket is not connected.");
                return; // Task<bool> 등을 반환하여 성공 여부 알릴 수도 있음
            }

            try
            {
                string jsonMessage = JsonUtility.ToJson(message);
                Debug.Log($"Sending Signaling Message (Type: {message.type}): {jsonMessage}");
                await websocket.SendText(jsonMessage);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to serialize or send message: {e.Message}");
                // 적절한 에러 처리/이벤트 발생
            }
        }

        // WebRtcManager의 Update 등에서 주기적으로 호출되어야 함
        public void DispatchMessages()
        {
            websocket?.DispatchMessageQueue();
        }

        // --- Private Event Handlers ---

        private void HandleOpen()
        {
            Debug.Log("WebSocket Connection Opened!");
            OnConnected?.Invoke();
        }

        private void HandleMessage(byte[] bytes)
        {
            try
            {
                var messageJson = System.Text.Encoding.UTF8.GetString(bytes);
                Debug.Log($"Raw Signaling Message Received: {messageJson}");
                var baseMessage = JsonUtility.FromJson<SignalingMessageBase>(messageJson);

                if (baseMessage != null && !string.IsNullOrEmpty(baseMessage.type))
                {
                    OnSignalingMessageReceived?.Invoke(baseMessage.type, messageJson);
                }
                else
                {
                    Debug.LogWarning($"Received message without 'type' field or failed to parse base: {messageJson}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error handling received message: {e.Message}");
            }
        }

        private void HandleError(string errorMsg)
        {
            Debug.LogError($"WebSocket Error: {errorMsg}");
            // 필요시 에러 이벤트 발생
            HandleClose(WebSocketCloseCode.Abnormal); // 에러 시에도 연결 종료 처리
        }

        private void HandleClose(WebSocketCloseCode closeCode)
        {
            Debug.Log($"WebSocket Connection Closed. Code: {closeCode}");
            if (websocket != null) // 중복 호출 방지
            {
                 OnDisconnected?.Invoke();
                 CleanupWebSocket();
            }
        }

        private void CleanupWebSocket()
        {
             if (websocket == null) return;

             // 이벤트 핸들러 제거 (메모리 누수 방지)
             websocket.OnOpen -= HandleOpen;
             websocket.OnError -= HandleError;
             websocket.OnClose -= HandleClose;
             websocket.OnMessage -= HandleMessage;

             websocket = null; // 참조 제거
             Debug.Log("WebSocket resources cleaned up.");
        }
    }
}