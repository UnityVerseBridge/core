using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityVerseBridge.Core;
using UnityVerseBridge.Core.Extensions.Quest;
using UnityVerseBridge.Core.Extensions.Mobile;

namespace UnityVerseBridge.Core.Editor
{
    public class UnityVerseBridgePrefabCreator : EditorWindow
    {
        [MenuItem("UnityVerseBridge/Create Prefabs/Quest Host Prefab")]
        static void CreateQuestPrefab()
        {
            // Create root GameObject
            GameObject rootGO = new GameObject("UnityVerseBridge_Quest");
            
            // Add UnityVerseBridgeManager
            UnityVerseBridgeManager bridgeManager = rootGO.AddComponent<UnityVerseBridgeManager>();
            
            // Create and configure ConnectionConfig
            ConnectionConfig config = ScriptableObject.CreateInstance<ConnectionConfig>();
            config.signalingServerUrl = "ws://localhost:8080";
            config.roomId = "quest-room-001";
            
            // Save ConnectionConfig as asset
            string configPath = "Assets/Resources/Prefabs/QuestConnectionConfig.asset";
            EnsureDirectoryExists(configPath);
            AssetDatabase.CreateAsset(config, configPath);
            
            // Configure bridge manager
            bridgeManager.ConnectionConfig = config;
            
            // Use reflection to set private fields
            var bridgeType = typeof(UnityVerseBridgeManager);
            var bridgeModeField = bridgeType.GetField("bridgeMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bridgeModeField.SetValue(bridgeManager, UnityVerseBridgeManager.BridgeMode.Host);
            
            var enableVideoField = bridgeType.GetField("enableVideo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            enableVideoField.SetValue(bridgeManager, true);
            
            var enableAudioField = bridgeType.GetField("enableAudio", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            enableAudioField.SetValue(bridgeManager, true);
            
            var enableTouchField = bridgeType.GetField("enableTouch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            enableTouchField.SetValue(bridgeManager, true);
            
            var enableHapticsField = bridgeType.GetField("enableHaptics", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            enableHapticsField.SetValue(bridgeManager, true);
            
            // Add Quest-specific extensions
            #if UNITY_ANDROID || UNITY_EDITOR
            rootGO.AddComponent<QuestHapticExtension>();
            rootGO.AddComponent<QuestVideoExtension>();
            rootGO.AddComponent<QuestTouchExtension>();
            #endif
            
            // Create video stream setup
            GameObject videoStreamGO = new GameObject("VideoStreamSetup");
            videoStreamGO.transform.SetParent(rootGO.transform);
            
            // Note: User needs to assign camera and render texture manually
            
            // Save as prefab
            string prefabPath = "Assets/Prefabs/Quest/UnityVerseBridge_Quest.prefab";
            EnsureDirectoryExists(prefabPath);
            PrefabUtility.SaveAsPrefabAsset(rootGO, prefabPath);
            
            // Cleanup
            DestroyImmediate(rootGO);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"Quest Host Prefab created at: {prefabPath}");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }
        
        [MenuItem("UnityVerseBridge/Create Prefabs/Mobile Client Prefab")]
        static void CreateMobilePrefab()
        {
            // Create root GameObject
            GameObject rootGO = new GameObject("UnityVerseBridge_Mobile");
            
            // Add UnityVerseBridgeManager
            UnityVerseBridgeManager bridgeManager = rootGO.AddComponent<UnityVerseBridgeManager>();
            
            // Create and configure ConnectionConfig
            ConnectionConfig config = ScriptableObject.CreateInstance<ConnectionConfig>();
            config.signalingServerUrl = "ws://localhost:8080";
            config.roomId = "quest-room-001";
            
            // Save ConnectionConfig as asset
            string configPath = "Assets/Resources/Prefabs/MobileConnectionConfig.asset";
            EnsureDirectoryExists(configPath);
            AssetDatabase.CreateAsset(config, configPath);
            
            // Configure bridge manager
            bridgeManager.ConnectionConfig = config;
            
            // Use reflection to set private fields
            var bridgeType = typeof(UnityVerseBridgeManager);
            var bridgeModeField = bridgeType.GetField("bridgeMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bridgeModeField.SetValue(bridgeManager, UnityVerseBridgeManager.BridgeMode.Client);
            
            var enableVideoField = bridgeType.GetField("enableVideo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            enableVideoField.SetValue(bridgeManager, true);
            
            var enableAudioField = bridgeType.GetField("enableAudio", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            enableAudioField.SetValue(bridgeManager, true);
            
            var enableTouchField = bridgeType.GetField("enableTouch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            enableTouchField.SetValue(bridgeManager, true);
            
            var enableHapticsField = bridgeType.GetField("enableHaptics", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            enableHapticsField.SetValue(bridgeManager, true);
            
            var autoConnectField = bridgeType.GetField("autoConnect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            autoConnectField.SetValue(bridgeManager, false); // Manual connection for mobile
            
            // Add Mobile-specific extensions
            rootGO.AddComponent<MobileVideoExtension>();
            rootGO.AddComponent<MobileInputExtension>();
            rootGO.AddComponent<MobileHapticExtension>();
            MobileConnectionUI connectionUI = rootGO.AddComponent<MobileConnectionUI>();
            
            // Create UI Canvas
            GameObject canvasGO = new GameObject("UI Canvas");
            canvasGO.transform.SetParent(rootGO.transform);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasGO.AddComponent<GraphicRaycaster>();
            
            // Create video display
            GameObject videoDisplayGO = new GameObject("VideoDisplay");
            videoDisplayGO.transform.SetParent(canvasGO.transform, false);
            RawImage rawImage = videoDisplayGO.AddComponent<RawImage>();
            
            // Setup RectTransform for full screen
            RectTransform rectTransform = rawImage.rectTransform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            
            // Add aspect ratio fitter
            AspectRatioFitter aspectFitter = videoDisplayGO.AddComponent<AspectRatioFitter>();
            aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspectFitter.aspectRatio = 16f / 9f;
            
            // Configure MobileVideoExtension
            MobileVideoExtension videoExt = rootGO.GetComponent<MobileVideoExtension>();
            if (videoExt != null)
            {
                SerializedObject so = new SerializedObject(videoExt);
                so.FindProperty("displayImage").objectReferenceValue = rawImage;
                so.ApplyModifiedProperties();
            }
            
            // Configure MobileInputExtension
            MobileInputExtension inputExt = rootGO.GetComponent<MobileInputExtension>();
            if (inputExt != null)
            {
                SerializedObject so = new SerializedObject(inputExt);
                so.FindProperty("touchArea").objectReferenceValue = rectTransform;
                so.ApplyModifiedProperties();
            }
            
            // Create connection UI (optional)
            GameObject connectionPanel = CreateConnectionUI(canvasGO, bridgeManager);
            
            // Configure MobileConnectionUI
            if (connectionUI != null)
            {
                SerializedObject so = new SerializedObject(connectionUI);
                so.FindProperty("connectionPanel").objectReferenceValue = connectionPanel;
                so.FindProperty("roomIdInput").objectReferenceValue = connectionPanel.GetComponentInChildren<InputField>();
                so.FindProperty("connectButton").objectReferenceValue = connectionPanel.GetComponentInChildren<Button>();
                so.FindProperty("statusText").objectReferenceValue = connectionPanel.GetComponentsInChildren<Text>()[0]; // Title text
                so.ApplyModifiedProperties();
            }
            
            // Save as prefab
            string prefabPath = "Assets/Prefabs/Mobile/UnityVerseBridge_Mobile.prefab";
            EnsureDirectoryExists(prefabPath);
            PrefabUtility.SaveAsPrefabAsset(rootGO, prefabPath);
            
            // Cleanup
            DestroyImmediate(rootGO);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"Mobile Client Prefab created at: {prefabPath}");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }
        
        static GameObject CreateConnectionUI(GameObject canvasGO, UnityVerseBridgeManager bridgeManager)
        {
            // Create UI panel
            GameObject panelGO = new GameObject("ConnectionPanel");
            panelGO.transform.SetParent(canvasGO.transform, false);
            Image panelImage = panelGO.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);
            
            RectTransform panelRect = panelGO.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(400, 300);
            panelRect.anchoredPosition = Vector2.zero;
            
            // Vertical layout
            VerticalLayoutGroup layout = panelGO.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            
            // Title
            GameObject titleGO = new GameObject("Title");
            titleGO.transform.SetParent(panelGO.transform, false);
            Text titleText = titleGO.AddComponent<Text>();
            titleText.text = "UnityVerse Connection";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 24;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            
            RectTransform titleRect = titleGO.GetComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(0, 40);
            
            // Room ID Input
            GameObject inputFieldGO = new GameObject("RoomIdInput");
            inputFieldGO.transform.SetParent(panelGO.transform, false);
            InputField inputField = inputFieldGO.AddComponent<InputField>();
            inputField.textComponent = CreateTextComponent(inputFieldGO, "Text");
            inputField.placeholder = CreateTextComponent(inputFieldGO, "Placeholder", "Enter Room ID");
            
            Image inputBg = inputFieldGO.AddComponent<Image>();
            inputBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            RectTransform inputRect = inputFieldGO.GetComponent<RectTransform>();
            inputRect.sizeDelta = new Vector2(0, 40);
            
            // Connect Button
            GameObject buttonGO = new GameObject("ConnectButton");
            buttonGO.transform.SetParent(panelGO.transform, false);
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
            buttonTextRect.anchoredPosition = Vector2.zero;
            
            RectTransform buttonRect = buttonGO.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(0, 50);
            
            // Note: The actual connection logic needs to be implemented in a separate script
            
            return panelGO;
        }
        
        static Text CreateTextComponent(GameObject parent, string name, string text = "")
        {
            GameObject textGO = new GameObject(name);
            textGO.transform.SetParent(parent.transform, false);
            Text textComp = textGO.AddComponent<Text>();
            textComp.text = text;
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComp.fontSize = 16;
            textComp.color = text == "" ? Color.black : Color.gray;
            
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);
            
            return textComp;
        }
        
        static void EnsureDirectoryExists(string filePath)
        {
            string directory = System.IO.Path.GetDirectoryName(filePath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
        }
        
        [MenuItem("UnityVerseBridge/Create Prefabs/Create Both Prefabs")]
        static void CreateBothPrefabs()
        {
            CreateQuestPrefab();
            CreateMobilePrefab();
            
            EditorUtility.DisplayDialog("UnityVerseBridge", 
                "Quest and Mobile prefabs have been created successfully!\n\n" +
                "Quest: Assets/Prefabs/Quest/UnityVerseBridge_Quest.prefab\n" +
                "Mobile: Assets/Prefabs/Mobile/UnityVerseBridge_Mobile.prefab", 
                "OK");
        }
    }
}