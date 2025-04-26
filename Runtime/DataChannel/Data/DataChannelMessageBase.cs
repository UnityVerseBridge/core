using System;

namespace UnityVerseBridge.Core.DataChannel.Data // 네임스페이스는 프로젝트 구조에 맞게 조정하세요
{
    /// <summary>
    /// WebRTC 데이터 채널을 통해 전송되는 모든 메시지의 기본 클래스입니다.
    /// 모든 메시지는 어떤 종류의 메시지인지 식별하기 위한 'type' 필드를 가집니다.
    /// </summary>
    [Serializable] // JsonUtility 등으로 직렬화/역직렬화 가능하도록 설정
    public class DataChannelMessageBase
    {
        /// <summary>
        /// 메시지의 종류를 나타내는 문자열 (예: "touch", "haptic", "chat" 등)
        /// </summary>
        public string type;
    }
}