using UnityEngine;
using UnityEditor;

namespace UnityVerseBridge.Core.Editor
{
    /// <summary>
    /// Unity 메뉴에 UnityVerseBridge 관련 유틸리티를 추가합니다.
    /// </summary>
    public static class UnityVerseBridgeMenu
    {
        [MenuItem("UnityVerseBridge/Open Settings")]
        public static void OpenSettings()
        {
            Debug.Log("UnityVerseBridge Settings - Coming Soon");
        }
        
        [MenuItem("UnityVerseBridge/Documentation")]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/your-repo/UnityVerseBridge");
        }
        
        [MenuItem("UnityVerseBridge/Create Default WebRTC Configuration")]
        public static void CreateDefaultWebRtcConfiguration()
        {
            // Create default WebRTC configuration asset
            var config = ScriptableObject.CreateInstance<WebRtcConfiguration>();
            
            // Set default values
            config.iceServerUrls = new System.Collections.Generic.List<string> { 
                "stun:stun.l.google.com:19302",
                "stun:stun1.l.google.com:19302" 
            };
            config.dataChannelLabel = "sendChannel";
            
            // Create directory if it doesn't exist
            string path = "Assets/UnityVerseBridge";
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            
            // Save asset
            string assetPath = $"{path}/DefaultWebRtcConfig.asset";
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            
            // Select the created asset
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            
            Debug.Log($"[UnityVerseBridge] Created default WebRTC configuration at: {assetPath}");
        }
    }
}
