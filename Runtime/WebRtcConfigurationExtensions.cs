using System.Linq; // ToArray() 사용 위해
using Unity.WebRTC; // RTCConfiguration, RTCIceServer 사용 위해
using UnityEngine; // Debug 사용

namespace UnityVerseBridge.Core // WebRtcConfiguration과 동일한 네임스페이스
{
    /// <summary>
    /// WebRtcConfiguration 클래스를 위한 확장 메서드를 제공합니다.
    /// </summary>
    public static class WebRtcConfigurationExtensions // 클래스는 반드시 static 이어야 함
    {
        /// <summary>
        /// WebRtcConfiguration 객체를 Unity WebRTC API에서 사용하는 RTCConfiguration 구조체로 변환합니다.
        /// </summary>
        /// <param name="config">변환할 WebRtcConfiguration 인스턴스 (this 키워드 필수)</param>
        /// <returns>변환된 RTCConfiguration 구조체</returns>
        public static RTCConfiguration ToRTCConfiguration(this WebRtcConfiguration config) // 메서드도 static, 첫 파라미터 앞에 this
        {
            RTCConfiguration rtcConfig = default; // Unity WebRTC 기본 설정 사용

            if (config == null)
            {
                Debug.LogWarning("WebRtcConfiguration is null, using default RTCConfiguration.");
                // 기본 STUN 서버라도 설정해주는 것이 좋을 수 있음
                rtcConfig.iceServers = new[] {
                    new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } }
                };
                return rtcConfig;
            }

            // ICE 서버 목록 복사
            var iceServers = config.GetIceServers();
            if (iceServers != null && iceServers.Count > 0)
            {
                // List<RTCIceServer>를 RTCIceServer[] 배열로 변환
                rtcConfig.iceServers = iceServers.ToArray();
            }
            else
            {
                 Debug.LogWarning("No ICE servers configured in WebRtcConfiguration. Using default STUN server.");
                 // 기본 STUN 서버 설정
                 rtcConfig.iceServers = new[] {
                     new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } }
                 };
            }

            // 필요시 다른 설정 값들도 여기서 복사
            // 예: rtcConfig.iceTransportPolicy = config.iceTransportPolicy;
            // 예: rtcConfig.bundlePolicy = config.bundlePolicy;

            return rtcConfig;
        }
    }
}