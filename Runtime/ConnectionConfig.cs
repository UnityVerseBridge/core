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
        public bool useSessionRoomId = false; // 세션별로 다른 room ID 사용 (테스트를 위해 비활성화)
        private string sessionRoomId = null; // 현재 세션의 room ID 캐시
        
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
            
            // 세션별 room ID 사용 (앱 실행마다 새로운 ID)
            if (useSessionRoomId)
            {
                if (string.IsNullOrEmpty(sessionRoomId))
                {
                    // 타임스탬프 기반 room ID 생성
                    sessionRoomId = $"{roomId}_{System.DateTime.Now:HHmmss}";
                    Debug.Log($"[ConnectionConfig] Generated session room ID: {sessionRoomId}");
                }
                return sessionRoomId;
            }
            
            return roomId;
        }
        
        // 세션 room ID 재설정 (필요시 호출)
        public void ResetSessionRoomId()
        {
            sessionRoomId = null;
        }
    }
}
