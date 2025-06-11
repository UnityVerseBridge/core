using System;
using Unity.WebRTC;
using UnityEngine.Events;
using UnityVerseBridge.Core.Signaling;

namespace UnityVerseBridge.Core
{
    /// <summary>
    /// WebRTC 매니저들의 공통 인터페이스
    /// 1:1 연결(WebRtcManager)과 1:N 연결(MultiPeerWebRtcManager) 모두 지원
    /// </summary>
    public interface IWebRtcManager
    {
        // 연결 상태
        bool IsSignalingConnected { get; }
        bool IsWebRtcConnected { get; }
        
        // 설정
        void SetupSignaling(ISignalingClient signalingClient);
        void SetConfiguration(WebRtcConfiguration configuration);
        
        // 연결 관리
        void Connect(string roomId);
        void Disconnect();
        
        // 미디어 트랙 관리
        void AddVideoTrack(VideoStreamTrack track);
        void AddAudioTrack(AudioStreamTrack track);
        void RemoveTrack(MediaStreamTrack track);
        
        // 데이터 채널 메시지
        void SendDataChannelMessage(object messageData);
        
        // 이벤트 - 1:1 연결용
        event Action OnSignalingConnected;
        event Action OnSignalingDisconnected;
        event Action OnWebRtcConnected;
        event Action OnWebRtcDisconnected;
        event Action<string> OnDataChannelMessageReceived;
        event UnityAction<MediaStreamTrack> OnVideoTrackReceived;
        event UnityAction<MediaStreamTrack> OnAudioTrackReceived;
        
        // 이벤트 - 1:N 연결용 (MultiPeer에서만 사용)
        event Action<string> OnPeerConnected;
        event Action<string> OnPeerDisconnected;
        event Action<string, string> OnMultiPeerDataChannelMessageReceived;
        event Action<string, MediaStreamTrack> OnMultiPeerVideoTrackReceived;
        event Action<string, MediaStreamTrack> OnMultiPeerAudioTrackReceived;
    }
}