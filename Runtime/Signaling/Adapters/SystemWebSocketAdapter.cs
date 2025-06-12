using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using CoreWebSocketState = UnityVerseBridge.Core.Signaling.WebSocketState;
using NetWebSocketState = System.Net.WebSockets.WebSocketState;

namespace UnityVerseBridge.Core.Signaling.Adapters
{
    /// <summary>
    /// System.Net.WebSockets를 사용하는 IWebSocketClient 구현체
    /// Unity Editor와 모든 플랫폼에서 작동
    /// </summary>
    public class SystemWebSocketAdapter : IWebSocketClient
    {
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _receiveTask;
        private CoreWebSocketState _state = CoreWebSocketState.Closed;
        private readonly Queue<Action> _messageQueue = new Queue<Action>();

        // IWebSocketClient 이벤트
        public event Action OnOpen;
        public event Action<byte[]> OnMessage;
        public event Action<string> OnError;
        public event Action<ushort> OnClose;

        public CoreWebSocketState State => _state;

        public async Task Connect(string url)
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _webSocket = new ClientWebSocket();
                _state = CoreWebSocketState.Connecting;
                
                Debug.Log($"[SystemWebSocket] Connecting to {url}");
            
            // Parse URL to check for authentication
            var uri = new Uri(url);
            if (!string.IsNullOrEmpty(uri.Query))
            {
                Debug.Log($"[SystemWebSocket] URL contains query parameters: {uri.Query}");
            }
                
                await _webSocket.ConnectAsync(new Uri(url), _cancellationTokenSource.Token);
                
                _state = CoreWebSocketState.Open;
                Debug.Log("[SystemWebSocket] Connected!");
                
                // 수신 태스크 시작
                _receiveTask = ReceiveLoop();
                
                // OnOpen 이벤트를 메시지 큐에 추가
                lock (_messageQueue)
                {
                    _messageQueue.Enqueue(() => OnOpen?.Invoke());
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SystemWebSocket] Connect failed: {ex.Message}");
                _state = CoreWebSocketState.Closed;
                OnError?.Invoke(ex.Message);
                throw;
            }
        }

        public async Task Close()
        {
            try
            {
                _state = CoreWebSocketState.Closing;
                
                if (_webSocket?.State == NetWebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                
                _cancellationTokenSource?.Cancel();
                
                if (_receiveTask != null)
                {
                    try { await _receiveTask; } catch { }
                }
                
                _webSocket?.Dispose();
                _webSocket = null;
                _state = CoreWebSocketState.Closed;
                
                Debug.Log("[SystemWebSocket] Closed");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SystemWebSocket] Close error: {ex.Message}");
            }
        }

        public async Task Send(byte[] bytes)
        {
            if (_state != CoreWebSocketState.Open)
            {
                Debug.LogError("[SystemWebSocket] Cannot send - not open");
                return;
            }

            try
            {
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Binary,
                    true,
                    _cancellationTokenSource.Token
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SystemWebSocket] Send failed: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        public async Task SendText(string message)
        {
            if (_state != CoreWebSocketState.Open)
            {
                Debug.LogError($"[SystemWebSocket] Cannot send - not open. State: {_state}");
                return;
            }

            try
            {
                Debug.Log($"[SystemWebSocket] Sending text: {message}");
                var bytes = Encoding.UTF8.GetBytes(message);
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cancellationTokenSource.Token
                );
                Debug.Log("[SystemWebSocket] Text sent successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SystemWebSocket] SendText failed: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        public void DispatchMessageQueue()
        {
            lock (_messageQueue)
            {
                while (_messageQueue.Count > 0)
                {
                    _messageQueue.Dequeue()?.Invoke();
                }
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new ArraySegment<byte>(new byte[65536]); // 64KB buffer for large SDP messages
            
            try
            {
                while (_webSocket.State == NetWebSocketState.Open && 
                       !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Handle potentially fragmented messages
                    using (var ms = new System.IO.MemoryStream())
                    {
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await _webSocket.ReceiveAsync(buffer, _cancellationTokenSource.Token);
                            
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                Debug.Log("[SystemWebSocket] Close message received");
                                return;
                            }
                            
                            ms.Write(buffer.Array, buffer.Offset, result.Count);
                        }
                        while (!result.EndOfMessage);
                        
                        if (result.MessageType == WebSocketMessageType.Text || 
                            result.MessageType == WebSocketMessageType.Binary)
                        {
                            var messageBytes = ms.ToArray();
                            
                            // Log received text messages for debugging
                            if (result.MessageType == WebSocketMessageType.Text)
                            {
                                var text = System.Text.Encoding.UTF8.GetString(messageBytes);
                                Debug.Log($"[SystemWebSocket] Received text: {text}");
                            }
                            
                            lock (_messageQueue)
                            {
                                _messageQueue.Enqueue(() => OnMessage?.Invoke(messageBytes));
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[SystemWebSocket] Receive loop cancelled");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SystemWebSocket] Receive error: {ex.Message}");
                lock (_messageQueue)
                {
                    _messageQueue.Enqueue(() => OnError?.Invoke(ex.Message));
                }
            }
            finally
            {
                _state = CoreWebSocketState.Closed;
                lock (_messageQueue)
                {
                    _messageQueue.Enqueue(() => OnClose?.Invoke(1000)); // Normal closure
                }
            }
        }
    }
}