using System;
using Unity.WebRTC; // RTCIceCandidate 관련 타입 정보를 간접적으로 반영

namespace UnityVerseBridge.Core.Signaling.Data
{
    /// <summary>
    /// WebRTC ICE Candidate 정보를 포함하는 클래스입니다.
    /// SignalingMessageBase를 상속받으며, type은 "ice-candidate"로 설정됩니다.
    /// </summary>
    [Serializable]
    public class IceCandidateMessage : SignalingMessageBase
    {
        /// <summary>
        /// ICE Candidate 문자열 데이터 (RTCIceCandidate.Candidate 값).
        /// </summary>
        public string candidate;

        /// <summary>
        /// 미디어 디스크립션 식별자 (RTCIceCandidate.SdpMid 값).
        /// </summary>
        public string sdpMid;

        /// <summary>
        /// 미디어 디스크립션 라인 인덱스 (RTCIceCandidate.SdpMLineIndex 값).
        /// Unity의 RTCIceCandidate.SdpMLineIndex는 nullable int (int?) 이지만,
        /// JsonUtility 호환성을 위해 non-nullable int로 저장하고, 생성/변환 시 null 처리를 합니다.
        /// </summary>
        public int sdpMLineIndex;

        // 생성자 (편의상 추가)
        public IceCandidateMessage(string candidate, string sdpMid, int? sdpMLineIndex) // nullable int로 받음
        {
            this.type = "ice-candidate"; // 메시지 타입 설정
            this.candidate = candidate;
            this.sdpMid = sdpMid;
            // Nullable int를 int로 변환 (null일 경우 기본값 0 사용 또는 예외 처리)
            this.sdpMLineIndex = sdpMLineIndex ?? 0;
        }

         // 기본 생성자 (JsonUtility 사용 시 필요할 수 있음)
        public IceCandidateMessage()
        {
            this.type = "ice-candidate";
        }

        // (선택 사항) Unity의 RTCIceCandidate 객체로 변환하는 헬퍼 메서드
        public RTCIceCandidate ToRTCIceCandidate()
        {
            RTCIceCandidateInit init = new RTCIceCandidateInit
            {
                candidate = this.candidate,
                sdpMid = this.sdpMid,
                sdpMLineIndex = this.sdpMLineIndex
                // usernameFragment 는 보통 candidate 문자열 안에 포함됨
            };
            return new RTCIceCandidate(init);
        }

        // (선택 사항) Unity의 RTCIceCandidate 객체로부터 생성하는 팩토리 메서드
        public static IceCandidateMessage FromRTCIceCandidate(RTCIceCandidate rtcCandidate)
        {
            return new IceCandidateMessage(
                rtcCandidate.Candidate,
                rtcCandidate.SdpMid,
                rtcCandidate.SdpMLineIndex
            );
        }
    }
}