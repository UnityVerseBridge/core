using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

namespace UnityVerseBridge.Core.Editor
{
    /// <summary>
    /// Unity 메뉴에 UnityVerseBridge 관련 유틸리티를 추가합니다.
    /// </summary>
    public static class UnityVerseBridgeMenu
    {
        private const string ASSETS_PATH = "Assets/UnityVerseBridge";
        private const string QUEST_CONFIG_PATH = ASSETS_PATH + "/QuestConfig.asset";
        private const string MOBILE_CONFIG_PATH = ASSETS_PATH + "/MobileConfig.asset";
        
        [MenuItem("GameObject/UnityVerseBridge/Quest Setup", false, 10)]
        public static void CreateQuestSetup()
        {
            // Create GameObject
            GameObject questGO = new GameObject("UnityVerseBridge_Quest");
            
            // Add UnityVerseBridgeManager component
            var manager = questGO.AddComponent<UnityVerseBridgeManager>();
            
            // Create or get Quest configuration
            var config = GetOrCreateQuestConfig();
            
            // Set configuration via reflection (since it's private)
            var configField = typeof(UnityVerseBridgeManager).GetField("unityVerseConfig", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            configField?.SetValue(manager, config);
            
            // Find VR Camera if available
            Camera vrCamera = FindVRCamera();
            if (vrCamera != null)
            {
                var vrCameraField = typeof(UnityVerseBridgeManager).GetField("vrCamera", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                vrCameraField?.SetValue(manager, vrCamera);
                Debug.Log("[UnityVerseBridge] VR Camera found and assigned");
            }
            
            // Add XR Session Monitor for Editor testing
            #if UNITY_EDITOR
            questGO.AddComponent<UnityVerseBridge.Core.Utils.XRSessionMonitor>();
            Debug.Log("[UnityVerseBridge] Added XRSessionMonitor for Editor XR testing");
            #endif
            
            // Select the created object
            Selection.activeGameObject = questGO;
            EditorGUIUtility.PingObject(questGO);
            
            Debug.Log("[UnityVerseBridge] Quest setup created successfully");
        }
        
        [MenuItem("GameObject/UnityVerseBridge/Mobile Setup", false, 11)]
        public static void CreateMobileSetup()
        {
            // Create GameObject
            GameObject mobileGO = new GameObject("UnityVerseBridge_Mobile");
            
            // Add UnityVerseBridgeManager component
            var manager = mobileGO.AddComponent<UnityVerseBridgeManager>();
            
            // Create or get Mobile configuration
            var config = GetOrCreateMobileConfig();
            
            // Set configuration via reflection
            var configField = typeof(UnityVerseBridgeManager).GetField("unityVerseConfig", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            configField?.SetValue(manager, config);
            
            // Create UI Canvas if it doesn't exist
            Canvas canvas = GameObject.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("UnityVerseBridge_Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                
                // Configure CanvasScaler for mobile screens
                CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                
                canvasGO.AddComponent<GraphicRaycaster>();
                
                // Add a tag or component to identify this as UnityVerseBridge-created
                canvasGO.tag = "Untagged"; // Don't use special tag to avoid tag errors
            }
            
            // Create RawImage for video display
            GameObject videoDisplayGO = new GameObject("VideoDisplay");
            videoDisplayGO.transform.SetParent(canvas.transform, false);
            
            RawImage videoDisplay = videoDisplayGO.AddComponent<RawImage>();
            videoDisplay.color = Color.white; // Changed from black to white
            
            // Set full screen with proper anchoring
            RectTransform rect = videoDisplay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            
            // Ensure the image fills the screen properly
            videoDisplay.raycastTarget = true;
            videoDisplay.maskable = true;
            
            // Add AspectRatioFitter for proper video display
            AspectRatioFitter aspectFitter = videoDisplayGO.AddComponent<AspectRatioFitter>();
            aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspectFitter.aspectRatio = 16f / 9f; // Default 16:9, will be updated when video is received
            
            // Assign video display
            var videoDisplayField = typeof(UnityVerseBridgeManager).GetField("videoDisplay", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            videoDisplayField?.SetValue(manager, videoDisplay);
            
            // Select the created object
            Selection.activeGameObject = mobileGO;
            EditorGUIUtility.PingObject(mobileGO);
            
            Debug.Log("[UnityVerseBridge] Mobile setup created successfully");
        }
        
        // Top-level menu items for UnityVerseBridge
        [MenuItem("UnityVerseBridge/Create Quest Setup", false, 10)]
        public static void CreateQuestSetupFromMenu()
        {
            CreateQuestSetup();
        }
        
        [MenuItem("UnityVerseBridge/Create Mobile Setup", false, 11)]
        public static void CreateMobileSetupFromMenu()
        {
            CreateMobileSetup();
        }
        
        [MenuItem("UnityVerseBridge/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/HardCodingMan/UnityVerse");
        }
        
        private static UnityVerseConfig GetOrCreateQuestConfig()
        {
            // Check if config already exists
            var existingConfig = AssetDatabase.LoadAssetAtPath<UnityVerseConfig>(QUEST_CONFIG_PATH);
            if (existingConfig != null)
            {
                return existingConfig;
            }
            
            // Create new config
            var config = ScriptableObject.CreateInstance<UnityVerseConfig>();
            
            // Set Quest-specific defaults
            config.signalingUrl = "ws://localhost:8080";
            config.roomId = "quest-room";
            config.roleDetection = RoleDetectionMode.Automatic;
            config.manualRole = PeerRole.Host;
            config.autoConnect = true;
            config.autoGenerateRoomId = false;
            config.enableDebugLogging = true;
            config.requireAuthentication = false;
            config.connectionTimeout = 30f;
            config.maxReconnectAttempts = 3;
            
            // Ensure directory exists
            if (!Directory.Exists(ASSETS_PATH))
            {
                Directory.CreateDirectory(ASSETS_PATH);
            }
            
            // Save asset
            AssetDatabase.CreateAsset(config, QUEST_CONFIG_PATH);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[UnityVerseBridge] Created Quest configuration at: {QUEST_CONFIG_PATH}");
            return config;
        }
        
        private static UnityVerseConfig GetOrCreateMobileConfig()
        {
            // Check if config already exists
            var existingConfig = AssetDatabase.LoadAssetAtPath<UnityVerseConfig>(MOBILE_CONFIG_PATH);
            if (existingConfig != null)
            {
                return existingConfig;
            }
            
            // Create new config
            var config = ScriptableObject.CreateInstance<UnityVerseConfig>();
            
            // Set Mobile-specific defaults
            config.signalingUrl = "ws://localhost:8080";
            config.roomId = "quest-room"; // Same as Quest for pairing
            config.roleDetection = RoleDetectionMode.Automatic;
            config.manualRole = PeerRole.Client;
            config.autoConnect = true;
            config.autoGenerateRoomId = false;
            config.enableDebugLogging = true;
            config.requireAuthentication = false;
            config.connectionTimeout = 30f;
            config.maxReconnectAttempts = 3;
            
            // Ensure directory exists
            if (!Directory.Exists(ASSETS_PATH))
            {
                Directory.CreateDirectory(ASSETS_PATH);
            }
            
            // Save asset
            AssetDatabase.CreateAsset(config, MOBILE_CONFIG_PATH);
            AssetDatabase.SaveAssets();
            
            Debug.Log($"[UnityVerseBridge] Created Mobile configuration at: {MOBILE_CONFIG_PATH}");
            return config;
        }
        
        private static Camera FindVRCamera()
        {
            // Look for common VR camera setups
            string[] vrCameraNames = { "CenterEyeAnchor", "Main Camera", "Head", "Camera (eye)" };
            
            foreach (string name in vrCameraNames)
            {
                GameObject cameraObj = GameObject.Find(name);
                if (cameraObj != null)
                {
                    Camera cam = cameraObj.GetComponent<Camera>();
                    if (cam != null)
                    {
                        return cam;
                    }
                }
            }
            
            // Check for OVRCameraRig
            var allCameras = GameObject.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in allCameras)
            {
                if (cam.transform.parent != null && 
                    (cam.transform.parent.name.Contains("CameraRig") || 
                     cam.transform.parent.name.Contains("XRRig")))
                {
                    return cam;
                }
            }
            
            return Camera.main;
        }
    }
}
