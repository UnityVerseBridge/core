using UnityEngine;

namespace UnityVerseBridge.Core
{
    [CreateAssetMenu(fileName = "ConnectionConfig", menuName = "UnityVerseBridge/Connection Config")]
    public class ConnectionConfig : ScriptableObject
    {
        [Header("Server Settings")]
        public string signalingServerUrl = "ws://localhost:8080";
        public bool requireAuthentication = false;
        public string authKey = "development-key";
        
        [Header("Room Settings")]
        public string roomId = "room_123";
        public bool autoGenerateRoomId = false;
        
        [Header("Connection Settings")]
        public float connectionTimeout = 30f;
        public int maxReconnectAttempts = 5;
        
        [Header("Debug")]
        public bool enableDetailedLogging = true;
        
        public string GetRoomId()
        {
            if (autoGenerateRoomId)
            {
                return System.Guid.NewGuid().ToString().Substring(0, 8);
            }
            return roomId;
        }
    }
}
