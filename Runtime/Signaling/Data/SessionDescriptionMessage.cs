using System;

namespace UnityVerseBridge.Core.Signaling.Data
{
    /// <summary>
    /// WebRTC Offer 또는 Answer 메시지에 사용되는 SDP(Session Description Protocol) 데이터를 포함하는 클래스입니다.
    /// SignalingMessageBase를 상속받으며, type은 "offer" 또는 "answer"로 설정됩니다.
    /// </summary>
    [Serializable]
    public class SessionDescriptionMessage : SignalingMessageBase
    {
        /// <summary>
        /// SDP 데이터 문자열 (RTCSessionDescription.sdp 값).
        /// </summary>
        public string sdp;

        // 생성자 (편의상 추가)
        public SessionDescriptionMessage(string type, string sdp)
        {
            if (type != "offer" && type != "answer")
            {
                throw new ArgumentException("Type must be 'offer' or 'answer'", nameof(type));
            }
            this.type = type;
            this.sdp = sdp;
        }

        // 기본 생성자 (JsonUtility 사용 시 필요할 수 있음)
        public SessionDescriptionMessage() { }
    }
}