using System;

namespace UnityVerseBridge.Core.Signaling.Data
{
    /// <summary>
    /// 룸 참가 메시지
    /// </summary>
    [Serializable]
    public class JoinRoomMessage : SignalingMessageBase
    {
        public string roomId;
        public string role;
        public int maxConnections;
        public string peerId;

        public JoinRoomMessage() 
        { 
            type = "join-room";
        }

        public JoinRoomMessage(string roomId, string role, int maxConnections = 10)
        {
            type = "join-room";
            this.roomId = roomId;
            this.role = role;
            this.maxConnections = maxConnections;
        }
    }

    /// <summary>
    /// 피어 참가 알림 메시지
    /// </summary>
    [Serializable]
    public class PeerJoinedMessage : SignalingMessageBase
    {
        public string peerId;
        public string role;

        public PeerJoinedMessage() 
        { 
            type = "peer-joined";
        }
    }

    /// <summary>
    /// 피어 퇴장 알림 메시지
    /// </summary>
    [Serializable]
    public class PeerLeftMessage : SignalingMessageBase
    {
        public string peerId;
        public string role;

        public PeerLeftMessage() 
        { 
            type = "peer-left";
        }
    }

    /// <summary>
    /// 클라이언트 준비 알림 메시지
    /// </summary>
    [Serializable]
    public class ClientReadyMessage : SignalingMessageBase
    {
        public string peerId;

        public ClientReadyMessage() 
        { 
            type = "client-ready";
        }
    }

    /// <summary>
    /// 타겟 피어가 있는 WebRTC 시그널링 메시지
    /// </summary>
    [Serializable]
    public class TargetedSessionDescriptionMessage : SessionDescriptionMessage
    {
        public string sourcePeerId;
        public string targetPeerId;

        public TargetedSessionDescriptionMessage() { }
        
        public TargetedSessionDescriptionMessage(string type, string sdp, string targetPeerId) : base(type, sdp)
        {
            this.targetPeerId = targetPeerId;
        }
    }

    /// <summary>
    /// 타겟 피어가 있는 ICE 후보 메시지
    /// </summary>
    [Serializable]
    public class TargetedIceCandidateMessage : IceCandidateMessage
    {
        public string sourcePeerId;
        public string targetPeerId;

        public TargetedIceCandidateMessage() { }
        
        public TargetedIceCandidateMessage(string candidate, string sdpMid, int sdpMLineIndex, string targetPeerId) 
            : base(candidate, sdpMid, sdpMLineIndex)
        {
            this.targetPeerId = targetPeerId;
        }
    }
}