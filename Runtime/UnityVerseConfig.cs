using UnityEngine;

namespace UnityVerseBridge.Core
{
    [CreateAssetMenu(fileName = "UnityVerseConfig", menuName = "UnityVerse/Configuration")]
    public class UnityVerseConfig : ScriptableObject
    {
        [Header("Connection")]
        [Tooltip("WebSocket signaling server URL")]
        public string signalingUrl = "ws://localhost:8080";
        
        [Tooltip("Room ID for peer matching")]
        public string roomId = "default-room";
        
        [Tooltip("Automatically generate random room ID")]
        public bool autoGenerateRoomId = false;
        
        [Header("Authentication")]
        [Tooltip("Enable token-based authentication")]
        public bool requireAuthentication = true;
        
        [Tooltip("Authentication key for server")]
        public string authKey = "development-key";
        
        [Header("Role Detection")]
        [Tooltip("How to determine if this is Host (Quest) or Client (Mobile)")]
        public RoleDetectionMode roleDetection = RoleDetectionMode.Automatic;
        
        [Tooltip("Manual role assignment (when roleDetection is Manual)")]
        public PeerRole manualRole = PeerRole.Host;
        
        [Header("Video Quality")]
        [Tooltip("Video streaming quality preset")]
        public VideoQualityPreset videoQuality = VideoQualityPreset.HD720p;
        
        [Tooltip("Custom resolution (when videoQuality is Custom)")]
        public Vector2Int customResolution = new Vector2Int(640, 360);
        
        [Tooltip("Target framerate for video streaming")]
        [Range(15, 60)]
        public int targetFramerate = 30;
        
        [Header("Advanced")]
        [Tooltip("Connection timeout in seconds")]
        [Range(5, 60)]
        public float connectionTimeout = 30f;
        
        [Tooltip("Maximum reconnection attempts")]
        [Range(0, 10)]
        public int maxReconnectAttempts = 3;
        
        [Tooltip("Enable detailed debug logging")]
        public bool enableDebugLogging = false;
        
        [Tooltip("Automatically start connection on scene load")]
        public bool autoConnect = true;
        
        // Computed properties
        public Vector2Int Resolution
        {
            get
            {
                switch (videoQuality)
                {
                    case VideoQualityPreset.Low:
                        return new Vector2Int(640, 360);
                    case VideoQualityPreset.HD720p:
                        return new Vector2Int(1280, 720);
                    case VideoQualityPreset.HD1080p:
                        return new Vector2Int(1920, 1080);
                    case VideoQualityPreset.Custom:
                        return customResolution;
                    default:
                        return new Vector2Int(1280, 720);
                }
            }
        }
        
        public PeerRole DetectedRole
        {
            get
            {
                if (roleDetection == RoleDetectionMode.Manual)
                    return manualRole;
                    
                return DetectRoleAutomatically();
            }
        }
        
        private PeerRole DetectRoleAutomatically()
        {
            #if UNITY_EDITOR
                // In editor, check project path or name to determine role
                string projectPath = Application.dataPath.ToLower();
                if (projectPath.Contains("quest-app") || projectPath.Contains("quest"))
                {
                    // Quest app project should always be Host in Editor
                    return PeerRole.Host;
                }
                else if (projectPath.Contains("mobile-app") || projectPath.Contains("mobile"))
                {
                    // Mobile app project should always be Client in Editor
                    return PeerRole.Client;
                }
                else
                {
                    // Fallback: check if VR is simulated
                    if (UnityEngine.XR.XRSettings.enabled)
                        return PeerRole.Host;
                    else
                        return PeerRole.Client;
                }
            #elif UNITY_ANDROID
                // Check if running on Quest/VR device
                if (UnityEngine.XR.XRSettings.enabled && IsQuestDevice())
                    return PeerRole.Host;
                else
                    return PeerRole.Client;
            #elif UNITY_IOS
                // iOS is always client (mobile)
                return PeerRole.Client;
            #else
                // Default to client for other platforms
                return PeerRole.Client;
            #endif
        }
        
        private bool IsQuestDevice()
        {
            string deviceModel = SystemInfo.deviceModel.ToLower();
            return deviceModel.Contains("quest") || 
                   deviceModel.Contains("oculus") ||
                   UnityEngine.XR.XRSettings.loadedDeviceName.ToLower().Contains("oculus");
        }
        
        // Validation
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(signalingUrl))
            {
                signalingUrl = "ws://localhost:8080";
            }
            
            if (string.IsNullOrEmpty(roomId) && !autoGenerateRoomId)
            {
                roomId = "default-room";
            }
            
            if (customResolution.x < 320) customResolution.x = 320;
            if (customResolution.y < 240) customResolution.y = 240;
            if (customResolution.x > 3840) customResolution.x = 3840;
            if (customResolution.y > 2160) customResolution.y = 2160;
        }
    }
    
    public enum RoleDetectionMode
    {
        Automatic,  // Detect based on platform/XR
        Manual      // Use manual assignment
    }
    
    public enum PeerRole
    {
        Host,       // Quest/VR - sends video, receives touch
        Client      // Mobile - receives video, sends touch
    }
    
    public enum VideoQualityPreset
    {
        Low,        // 640x360 @ 30fps
        HD720p,     // 1280x720 @ 30fps (default)
        HD1080p,    // 1920x1080 @ 30fps
        Custom      // Use custom resolution
    }
}