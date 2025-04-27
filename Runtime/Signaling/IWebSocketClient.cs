// core/Runtime/Signaling/IWebSocketClient.cs
using System;
using System.Threading.Tasks;

namespace UnityVerseBridge.Core.Signaling
{
    // WebSocket 구현체들이 따라야 할 규격(인터페이스) 정의
    public interface IWebSocketClient
    {
        event Action OnOpen;
        event Action<byte[]> OnMessage; // 바이트 배열로 받는 것이 일반적
        event Action<string> OnError;
        event Action<ushort> OnClose; // Close Code (ushort)

        WebSocketState State { get; } // NativeWebSocket의 State와 유사한 상태 enum 필요

        Task Connect(string url);
        Task Close();
        Task Send(byte[] bytes); // 바이트 배열로 보내는 메서드
        Task SendText(string message); // 편의상 텍스트 보내는 메서드도 추가

        // NativeWebSocket의 메시지 디스패처 역할
        void DispatchMessageQueue();
    }

    // WebSocket 상태 Enum 정의 (NativeWebSocket과 유사하게)
    public enum WebSocketState { Connecting, Open, Closing, Closed }
}