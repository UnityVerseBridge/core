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
    
    // PeerJoinedMessage and ClientReadyMessage moved to RoomMessages.cs to avoid duplication
    
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