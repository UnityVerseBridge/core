using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace UnityVerseBridge.Core.Editor
{
    public static class UnityVerseBridgePrefabCreator
    {
        [MenuItem("UnityVerseBridge/Create Quest Bridge Prefab", false, 11)]
        public static void CreateQuestBridgePrefab()
        {
            var go = CreateUnityVerseBridgeManager("UnityVerseBridge_Quest");
            if (go != null)
            {
                var manager = go.GetComponent<UnityVerseBridgeManager>();
                if (manager != null)
                {
                    // Quest 전용 설정
                    SerializedObject so = new SerializedObject(manager);
                    so.FindProperty("enableVideoStreaming").boolValue = true;
                    so.FindProperty("enableTouchReceiving").boolValue = true;
                    so.FindProperty("enableHapticFeedback").boolValue = true;
                    so.FindProperty("enableVideoReceiving").boolValue = false;
                    so.FindProperty("enableTouchSending").boolValue = false;
                    so.FindProperty("enableHapticReceiving").boolValue = false;
                    so.ApplyModifiedProperties();
                    
                    Debug.Log("[UnityVerseBridge] Quest Bridge created. Please assign:");
                    Debug.Log("  1. VR Camera (required)");
                    Debug.Log("  2. Connection Config (required)");
                    Debug.Log("  3. Touch Canvas (optional - will be created automatically)");
                }
            }
        }

        [MenuItem("UnityVerseBridge/Create Mobile Bridge Prefab", false, 12)]
        public static void CreateMobileBridgePrefab()
        {
            var go = CreateUnityVerseBridgeManager("UnityVerseBridge_Mobile");
            if (go != null)
            {
                var manager = go.GetComponent<UnityVerseBridgeManager>();
                if (manager != null)
                {
                    // Mobile 전용 설정
                    SerializedObject so = new SerializedObject(manager);
                    so.FindProperty("enableVideoStreaming").boolValue = false;
                    so.FindProperty("enableTouchReceiving").boolValue = false;
                    so.FindProperty("enableHapticFeedback").boolValue = false;
                    so.FindProperty("enableVideoReceiving").boolValue = true;
                    so.FindProperty("enableTouchSending").boolValue = true;
                    so.FindProperty("enableHapticReceiving").boolValue = true;
                    so.FindProperty("enableAutoConnect").boolValue = false; // Manual connection for mobile
                    so.ApplyModifiedProperties();
                    
                    // Create UI elements
                    CreateMobileUI(go);
                    
                    Debug.Log("[UnityVerseBridge] Mobile Bridge created. Please assign:");
                    Debug.Log("  1. Connection Config (required)");
                    Debug.Log("  2. Video Display is already connected to the created RawImage");
                }
            }
        }

        private static GameObject CreateUnityVerseBridgeManager(string name)
        {
            GameObject go = new GameObject(name);
            var manager = go.AddComponent<UnityVerseBridgeManager>();
            
            // Find or create default WebRTC config
            var webRtcConfig = FindOrCreateDefaultWebRtcConfig();
            if (webRtcConfig != null)
            {
                SerializedObject so = new SerializedObject(manager);
                so.FindProperty("webRtcConfiguration").objectReferenceValue = webRtcConfig;
                so.ApplyModifiedProperties();
            }
            
            // Create default connection config
            var connectionConfig = CreateDefaultConnectionConfig(name);
            if (connectionConfig != null)
            {
                SerializedObject so = new SerializedObject(manager);
                so.FindProperty("configuration").objectReferenceValue = connectionConfig;
                so.ApplyModifiedProperties();
            }
            
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            
            return go;
        }
        
        private static void CreateMobileUI(GameObject parent)
        {
            // Find existing Canvas or create new one
            Canvas canvas = GameObject.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            
            // Create RawImage for video display
            GameObject rawImageGO = new GameObject("VideoDisplay");
            rawImageGO.transform.SetParent(canvas.transform, false);
            RawImage rawImage = rawImageGO.AddComponent<RawImage>();
            RectTransform rt = rawImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rawImage.color = Color.black;
            
            // Create connection UI panel
            GameObject connectionPanel = CreateConnectionUI(canvas);
            
            // Assign to manager
            var manager = parent.GetComponent<UnityVerseBridgeManager>();
            if (manager != null)
            {
                SerializedObject so = new SerializedObject(manager);
                so.FindProperty("videoDisplay").objectReferenceValue = rawImage;
                so.FindProperty("connectionUI").objectReferenceValue = connectionPanel;
                so.ApplyModifiedProperties();
            }
        }
        
        private static GameObject CreateConnectionUI(Canvas canvas)
        {
            // Create panel
            GameObject panel = new GameObject("ConnectionPanel");
            panel.transform.SetParent(canvas.transform, false);
            
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(400, 250);
            
            // Add vertical layout
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 15;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            
            // Title
            GameObject titleGO = new GameObject("Title");
            titleGO.transform.SetParent(panel.transform, false);
            Text title = titleGO.AddComponent<Text>();
            title.text = "UnityVerse Connection";
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 24;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            
            LayoutElement titleLayout = titleGO.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 40;
            
            // Room ID input
            GameObject inputGO = new GameObject("RoomInput");
            inputGO.transform.SetParent(panel.transform, false);
            InputField input = inputGO.AddComponent<InputField>();
            Image inputBg = inputGO.AddComponent<Image>();
            inputBg.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            
            GameObject inputTextGO = new GameObject("Text");
            inputTextGO.transform.SetParent(inputGO.transform, false);
            Text inputText = inputTextGO.AddComponent<Text>();
            inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            inputText.fontSize = 18;
            inputText.color = Color.white;
            inputText.supportRichText = false;
            
            RectTransform inputTextRect = inputTextGO.GetComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = new Vector2(10, 0);
            inputTextRect.offsetMax = new Vector2(-10, 0);
            
            GameObject placeholderGO = new GameObject("Placeholder");
            placeholderGO.transform.SetParent(inputGO.transform, false);
            Text placeholder = placeholderGO.AddComponent<Text>();
            placeholder.text = "Enter Room ID...";
            placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeholder.fontSize = 18;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            
            RectTransform placeholderRect = placeholderGO.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(10, 0);
            placeholderRect.offsetMax = new Vector2(-10, 0);
            
            input.targetGraphic = inputBg;
            input.textComponent = inputText;
            input.placeholder = placeholder;
            
            LayoutElement inputLayout = inputGO.AddComponent<LayoutElement>();
            inputLayout.preferredHeight = 40;
            
            // Connect button
            GameObject buttonGO = new GameObject("ConnectButton");
            buttonGO.transform.SetParent(panel.transform, false);
            Button button = buttonGO.AddComponent<Button>();
            Image buttonImage = buttonGO.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.6f, 1f, 1f);
            
            GameObject buttonTextGO = new GameObject("Text");
            buttonTextGO.transform.SetParent(buttonGO.transform, false);
            Text buttonText = buttonTextGO.AddComponent<Text>();
            buttonText.text = "Connect";
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontSize = 20;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.white;
            
            RectTransform buttonTextRect = buttonTextGO.GetComponent<RectTransform>();
            buttonTextRect.anchorMin = Vector2.zero;
            buttonTextRect.anchorMax = Vector2.one;
            buttonTextRect.sizeDelta = Vector2.zero;
            
            LayoutElement buttonLayout = buttonGO.AddComponent<LayoutElement>();
            buttonLayout.preferredHeight = 45;
            
            // Status text
            GameObject statusGO = new GameObject("StatusText");
            statusGO.transform.SetParent(panel.transform, false);
            Text status = statusGO.AddComponent<Text>();
            status.text = "Not connected";
            status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            status.fontSize = 16;
            status.alignment = TextAnchor.MiddleCenter;
            status.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            
            LayoutElement statusLayout = statusGO.AddComponent<LayoutElement>();
            statusLayout.preferredHeight = 30;
            
            return panel;
        }
        
        private static WebRtcConfiguration FindOrCreateDefaultWebRtcConfig()
        {
            // Try to find existing
            string[] guids = AssetDatabase.FindAssets("t:WebRtcConfiguration DefaultWebRtcConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<WebRtcConfiguration>(path);
            }
            
            // Create new one using menu command
            UnityVerseBridgeMenu.CreateDefaultWebRtcConfiguration();
            
            // Try to find again
            AssetDatabase.Refresh();
            guids = AssetDatabase.FindAssets("t:WebRtcConfiguration DefaultWebRtcConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<WebRtcConfiguration>(path);
            }
            
            return null;
        }
        
        private static ConnectionConfig CreateDefaultConnectionConfig(string prefabName)
        {
            string configName = prefabName.Contains("Quest") ? "QuestConnectionConfig" : "MobileConnectionConfig";
            string path = $"Assets/UnityVerseBridge/{configName}.asset";
            
            // Check if already exists
            var existing = AssetDatabase.LoadAssetAtPath<ConnectionConfig>(path);
            if (existing != null)
            {
                return existing;
            }
            
            // Create new
            var config = ScriptableObject.CreateInstance<ConnectionConfig>();
            config.signalingServerUrl = "ws://localhost:8080";
            config.roomId = "test-room";
            config.clientType = prefabName.Contains("Quest") ? ClientType.Quest : ClientType.Mobile;
            config.autoGenerateRoomId = false;
            
            // Ensure directory exists
            string dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            
            return config;
        }
    }
}