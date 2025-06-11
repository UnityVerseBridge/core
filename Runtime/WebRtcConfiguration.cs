// core/Runtime/Common/WebRtcConfiguration.cs
using System.Collections.Generic;
using Unity.WebRTC; // RTCIceServer 사용 위해
using UnityEngine;

namespace UnityVerseBridge.Core
{
    [CreateAssetMenu(fileName = "WebRtcConfiguration", menuName = "UnityVerseBridge/WebRTC Configuration")]
    public class WebRtcConfiguration : ScriptableObject
    {
        [Header("ICE Server Settings")]
        [Tooltip("기본 STUN 서버 설정입니다. 실제 환경에서는 TURN 서버 추가가 필요할 수 있습니다.")]
        public List<string> iceServerUrls = new List<string> {
            "stun:stun.l.google.com:19302"
        };
        
        [Header("Data Channel Settings")]
        [Tooltip("데이터 채널의 레이블입니다.")]
        public string dataChannelLabel = "sendChannel";
        
        // RTCConfiguration으로 변환하기 위한 내부 프로퍼티
        public List<RTCIceServer> GetIceServers()
        {
            var servers = new List<RTCIceServer>();
            foreach (var url in iceServerUrls)
            {
                servers.Add(new RTCIceServer { urls = new[] { url } });
            }
            return servers;
        }
    }
}