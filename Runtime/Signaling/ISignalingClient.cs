using System;
using System.Threading.Tasks; // 비동기 메서드를 위해 추가 (선택 사항)
using UnityVerseBridge.Core.Signaling.Data; // SignalingMessageBase 사용 위해

namespace UnityVerseBridge.Core.Signaling
{
    /// <summary>
    /// 시그널링 클라이언트가 구현해야 하는 기능에 대한 인터페이스입니다.
    /// </summary>
    public interface ISignalingClient
    {
        /// <summary>
        /// 시그널링 서버에 성공적으로 연결되었을 때 발생하는 이벤트입니다.
        /// </summary>
        event Action OnConnected;

        /// <summary>
        /// 시그널링 서버와의 연결이 끊어졌을 때 발생하는 이벤트입니다.
        /// </summary>
        event Action OnDisconnected;

        /// <summary>
        /// 시그널링 서버로부터 메시지를 수신했을 때 발생하는 이벤트입니다.
        /// 첫 번째 string은 메시지 타입, 두 번째 string은 원본 JSON 데이터입니다.
        /// </summary>
        event Action<string, string> OnSignalingMessageReceived;

        /// <summary>
        /// 지정된 URL의 시그널링 서버에 비동기적으로 연결을 시도합니다.
        /// </summary>
        /// <param name="url">접속할 서버 URL (예: "ws://localhost:8080")</param>
        Task Connect(string url); // async/await 사용 시 Task 반환, 아니면 void

        /// <summary>
        /// 시그널링 서버와의 연결을 비동기적으로 종료합니다.
        /// </summary>
        Task Disconnect(); // async/await 사용 시 Task 반환, 아니면 void

        /// <summary>
        /// 시그널링 메시지 객체를 (JSON 등으로 변환하여) 서버로 전송합니다.
        /// </summary>
        /// <typeparam name="T">SignalingMessageBase를 상속하는 메시지 타입</typeparam>
        /// <param name="message">전송할 메시지 객체</param>
        Task SendMessage<T>(T message) where T : SignalingMessageBase; // async/await 사용 시 Task 반환, 아니면 void

        // 필요시 연결 상태를 확인하는 속성 추가 가능
        bool IsConnected { get; }
    }
}