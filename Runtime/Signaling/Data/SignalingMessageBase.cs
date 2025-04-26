using System;

namespace UnityVerseBridge.Core.Signaling.Data // 네임스페이스 주의: Signaling 관련 경로
{
    /// <summary>
    /// WebRTC 시그널링을 위해 WebSocket을 통해 전송되는 모든 메시지의 기본 클래스입니다.
    /// 모든 메시지는 어떤 종류의 메시지인지 식별하기 위한 'type' 필드를 가집니다.
    /// </summary>
    [Serializable] // JsonUtility 등으로 직렬화/역직렬화 가능하도록 설정
    public class SignalingMessageBase
    {
        /// <summary>
        /// 시그널링 메시지의 종류를 나타내는 문자열 (예: "offer", "answer", "ice-candidate")
        /// </summary>
        public string type;
    }
}