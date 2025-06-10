using UnityVerseBridge.Core.Signaling.Data;

namespace UnityVerseBridge.Core.Signaling.Messages
{
    [System.Serializable]
    public class RegisterMessage : SignalingMessageBase
    {
        public string peerId;
        public string clientType;
        public string roomId;
        
        public RegisterMessage()
        {
            type = "register";
        }
    }
    
    [System.Serializable]
    public class PeerJoinedMessage : SignalingMessageBase
    {
        public string peerId;
        public string clientType;
        public string role; // Added to match server format
        
        public PeerJoinedMessage()
        {
            type = "peer-joined";
        }
    }
    
    [System.Serializable]
    public class ClientReadyMessage : SignalingMessageBase
    {
        public string peerId;
        
        public ClientReadyMessage()
        {
            type = "client-ready";
        }
    }
    
    [System.Serializable]
    public class ErrorMessage : SignalingMessageBase
    {
        public string error;
        public string context;
        
        public ErrorMessage()
        {
            type = "error";
        }
    }
}