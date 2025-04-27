// core/Runtime/Common/WebRtcConfiguration.cs
using System.Collections.Generic;
using Unity.WebRTC; // RTCIceServer 사용 위해

namespace UnityVerseBridge.Core
{
    [System.Serializable] // Inspector에서 편하게 보거나 Json으로 다루려면
    public class WebRtcConfiguration
    {
        public List<RTCIceServer> iceServers = new List<RTCIceServer> {
            // 기본 STUN 서버 예시 (실제 환경에서는 TURN 서버 추가 필요 가능성 높음)
            new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } }
        };
        // 필요시 다른 설정 추가 (예: DataChannel 이름)
        public string dataChannelLabel = "sendChannel";
    }
}