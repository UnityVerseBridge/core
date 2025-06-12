using UnityEngine;
using UnityEditor;
using UnityVerseBridge.Core;
using System.Linq;
using System.Reflection;

namespace UnityVerseBridge.Core.Editor
{
    [CustomEditor(typeof(UnityVerseBridgeManager))]
    public class UnityVerseBridgeManagerEditor : UnityEditor.Editor
    {
        private SerializedProperty unityVerseConfig;
        private SerializedProperty legacyConfig;
        private SerializedProperty showDebugUI;
        private SerializedProperty debugDisplayMode;
        private SerializedProperty enableAutoConnect;
        
        // Quest-specific properties
        private SerializedProperty vrCamera;
        private SerializedProperty enableVideoStreaming;
        private SerializedProperty enableTouchReceiving;
        private SerializedProperty enableHapticFeedback;
        private SerializedProperty touchCanvas;
        
        // Mobile-specific properties
        private SerializedProperty videoDisplay;
        private SerializedProperty enableVideoReceiving;
        private SerializedProperty enableTouchSending;
        private SerializedProperty enableHapticReceiving;
        private SerializedProperty connectionUI;
        
        // Platform detection
        private bool isQuestPlatform;
        private bool isMobilePlatform;
        private bool platformDetected;
        
        private static readonly string[] questKeywords = { "quest", "vr", "oculus", "meta" };
        private static readonly string[] mobileKeywords = { "mobile", "ios", "android", "phone", "tablet" };
        
        void OnEnable()
        {
            // Common properties
            unityVerseConfig = serializedObject.FindProperty("unityVerseConfig");
            legacyConfig = serializedObject.FindProperty("legacyConfig");
            showDebugUI = serializedObject.FindProperty("showDebugUI");
            debugDisplayMode = serializedObject.FindProperty("debugDisplayMode");
            enableAutoConnect = serializedObject.FindProperty("enableAutoConnect");
            
            // Quest-specific properties
            vrCamera = serializedObject.FindProperty("vrCamera");
            touchCanvas = serializedObject.FindProperty("questTouchCanvas");
            
            // Mobile-specific properties
            videoDisplay = serializedObject.FindProperty("videoDisplay");
            
            // These properties don't exist in UnityVerseBridgeManager, comment them out for now
            // enableVideoStreaming = serializedObject.FindProperty("enableVideoStreaming");
            // enableTouchReceiving = serializedObject.FindProperty("enableTouchReceiving");
            // enableHapticFeedback = serializedObject.FindProperty("enableHapticFeedback");
            // enableVideoReceiving = serializedObject.FindProperty("enableVideoReceiving");
            // enableTouchSending = serializedObject.FindProperty("enableTouchSending");
            // enableHapticReceiving = serializedObject.FindProperty("enableHapticReceiving");
            // connectionUI = serializedObject.FindProperty("connectionUI");
            
            DetectPlatform();
        }
        
        void DetectPlatform()
        {
            platformDetected = false;
            isQuestPlatform = false;
            isMobilePlatform = false;
            
            // Check build target
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            
            // Check by build settings
            if (buildTarget == BuildTarget.Android)
            {
                // Try to check XR settings using reflection to avoid compile errors
                try
                {
                    // Try to get XRGeneralSettingsPerBuildTarget type
                    var xrSettingsType = System.Type.GetType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget, Unity.XR.Management.Editor");
                    if (xrSettingsType != null)
                    {
                        // Get XRGeneralSettingsForBuildTarget method
                        var method = xrSettingsType.GetMethod("XRGeneralSettingsForBuildTarget", BindingFlags.Public | BindingFlags.Static);
                        if (method != null)
                        {
                            var xrSettings = method.Invoke(null, new object[] { buildTargetGroup });
                            if (xrSettings != null)
                            {
                                // Get Manager property
                                var managerProp = xrSettings.GetType().GetProperty("Manager");
                                if (managerProp != null)
                                {
                                    var manager = managerProp.GetValue(xrSettings);
                                    if (manager != null)
                                    {
                                        // Get activeLoaders property
                                        var loadersProp = manager.GetType().GetProperty("activeLoaders");
                                        if (loadersProp != null)
                                        {
                                            var loaders = loadersProp.GetValue(manager) as System.Collections.IEnumerable;
                                            if (loaders != null)
                                            {
                                                foreach (var loader in loaders)
                                                {
                                                    var loaderTypeName = loader.GetType().Name;
                                                    if (loaderTypeName.Contains("Oculus") || loaderTypeName.Contains("OpenXR") || loaderTypeName.Contains("Meta"))
                                                    {
                                                        isQuestPlatform = true;
                                                        platformDetected = true;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.Exception)
                {
                    // XR Management not available, try other detection methods
                }
                
                // If not detected yet, try simpler detection
                if (!platformDetected)
                {
                    // Check if any XR package is present by looking for XR types
                    var hasXR = System.Type.GetType("UnityEngine.XR.XRSettings, UnityEngine.XRModule") != null;
                    if (hasXR)
                    {
                        // Check for Oculus/Meta specific types
                        var hasOculus = System.Type.GetType("Unity.XR.Oculus.OculusLoader, Unity.XR.Oculus") != null ||
                                       System.Type.GetType("Unity.XR.MetaOpenXR.MetaOpenXRLoader, Unity.XR.MetaOpenXR") != null;
                        if (hasOculus)
                        {
                            isQuestPlatform = true;
                            platformDetected = true;
                        }
                    }
                }
                
                if (!platformDetected)
                {
                    // If no XR, assume mobile Android
                    isMobilePlatform = true;
                    platformDetected = true;
                }
            }
            else if (buildTarget == BuildTarget.iOS)
            {
                isMobilePlatform = true;
                platformDetected = true;
            }
            
            // If still not detected, check by project path/name
            if (!platformDetected)
            {
                string projectPath = Application.dataPath.ToLower();
                string productName = Application.productName.ToLower();
                
                foreach (var keyword in questKeywords)
                {
                    if (projectPath.Contains(keyword) || productName.Contains(keyword))
                    {
                        isQuestPlatform = true;
                        platformDetected = true;
                        break;
                    }
                }
                
                if (!platformDetected)
                {
                    foreach (var keyword in mobileKeywords)
                    {
                        if (projectPath.Contains(keyword) || productName.Contains(keyword))
                        {
                            isMobilePlatform = true;
                            platformDetected = true;
                            break;
                        }
                    }
                }
            }
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // Header
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UnityVerseBridge Manager", EditorStyles.boldLabel);
            
            // Platform detection info
            DrawPlatformInfo();
            
            EditorGUILayout.Space();
            
            // Common Settings
            EditorGUILayout.LabelField("Common Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(unityVerseConfig);
            
            // Legacy config - only show if it has a value
            if (legacyConfig.objectReferenceValue != null)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.HelpBox("Legacy configuration detected. Consider migrating to UnityVerseConfig.", MessageType.Warning);
                EditorGUILayout.PropertyField(legacyConfig, new GUIContent("Legacy Config (Deprecated)"));
                if (GUILayout.Button("Clear Legacy Config"))
                {
                    legacyConfig.objectReferenceValue = null;
                }
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.PropertyField(enableAutoConnect);
            EditorGUILayout.PropertyField(showDebugUI);
            
            // Add tooltip for Debug Display Mode
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(debugDisplayMode);
            if (GUILayout.Button("?", GUILayout.Width(20)))
            {
                EditorUtility.DisplayDialog("Debug Display Mode", 
                    "GUI: Uses Unity's OnGUI system. Works everywhere including VR headsets. Renders on top of everything.\n\n" +
                    "UI: Uses Unity's UI Canvas system. Better for mobile/AR. Can be styled and positioned more easily.\n\n" +
                    "Both: Shows debug logs in both systems simultaneously.", 
                    "OK");
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Platform-specific settings
            if (isQuestPlatform)
            {
                DrawQuestSettings();
            }
            else if (isMobilePlatform)
            {
                DrawMobileSettings();
            }
            else
            {
                DrawBothPlatformSettings();
            }
            
            serializedObject.ApplyModifiedProperties();
        }
        
        private void DrawPlatformInfo()
        {
            Color originalColor = GUI.backgroundColor;
            
            if (platformDetected)
            {
                if (isQuestPlatform)
                {
                    GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
                    EditorGUILayout.HelpBox("Quest Platform Detected - Showing Quest-specific settings", MessageType.Info);
                }
                else if (isMobilePlatform)
                {
                    GUI.backgroundColor = new Color(0.8f, 0.8f, 1f);
                    EditorGUILayout.HelpBox("Mobile Platform Detected - Showing Mobile-specific settings", MessageType.Info);
                }
            }
            else
            {
                GUI.backgroundColor = new Color(1f, 1f, 0.8f);
                EditorGUILayout.HelpBox("Platform not detected - Showing all settings. Switch platform in Build Settings for platform-specific UI.", MessageType.Warning);
            }
            
            GUI.backgroundColor = originalColor;
            
            // Manual platform override
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Override Platform:", GUILayout.Width(100));
            
            if (GUILayout.Button("Quest", GUILayout.Width(60)))
            {
                isQuestPlatform = true;
                isMobilePlatform = false;
                platformDetected = true;
            }
            
            if (GUILayout.Button("Mobile", GUILayout.Width(60)))
            {
                isQuestPlatform = false;
                isMobilePlatform = true;
                platformDetected = true;
            }
            
            if (GUILayout.Button("Auto", GUILayout.Width(60)))
            {
                DetectPlatform();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawQuestSettings()
        {
            EditorGUILayout.LabelField("Quest-Specific References", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical("box");
            
            // Required
            EditorGUILayout.LabelField("Required", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(vrCamera, new GUIContent("VR Camera*"));
            
            if (vrCamera.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("VR Camera is required for video streaming", MessageType.Error);
            }
            
            EditorGUILayout.Space(5);
            
            // Features
            EditorGUILayout.LabelField("Features", EditorStyles.miniBoldLabel);
            if (enableVideoStreaming != null) EditorGUILayout.PropertyField(enableVideoStreaming);
            if (enableTouchReceiving != null) EditorGUILayout.PropertyField(enableTouchReceiving);
            if (enableHapticFeedback != null) EditorGUILayout.PropertyField(enableHapticFeedback);
            
            // Optional
            if (enableTouchReceiving != null && enableTouchReceiving.boolValue)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Optional", EditorStyles.miniBoldLabel);
                if (touchCanvas != null) EditorGUILayout.PropertyField(touchCanvas, new GUIContent("Touch Canvas"));
                
                if (touchCanvas.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("Touch Canvas will be created automatically if not assigned", MessageType.Info);
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawMobileSettings()
        {
            EditorGUILayout.LabelField("Mobile-Specific References", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical("box");
            
            // Required
            EditorGUILayout.LabelField("Required", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(videoDisplay, new GUIContent("Video Display*"));
            
            if (videoDisplay.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Video Display (RawImage) is required to show video stream", MessageType.Error);
            }
            
            EditorGUILayout.Space(5);
            
            // Features
            EditorGUILayout.LabelField("Features", EditorStyles.miniBoldLabel);
            if (enableVideoReceiving != null) EditorGUILayout.PropertyField(enableVideoReceiving);
            if (enableTouchSending != null) EditorGUILayout.PropertyField(enableTouchSending);
            if (enableHapticReceiving != null) EditorGUILayout.PropertyField(enableHapticReceiving);
            
            // Optional
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Optional", EditorStyles.miniBoldLabel);
            if (connectionUI != null) 
            {
                EditorGUILayout.PropertyField(connectionUI, new GUIContent("Connection UI"));
                
                if (connectionUI.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("Connection UI can be used for room ID input and connection status", MessageType.Info);
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawBothPlatformSettings()
        {
            // Show both in a tabbed interface
            EditorGUILayout.LabelField("Platform-Specific References", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            // Quest column
            EditorGUILayout.BeginVertical("box", GUILayout.Width(EditorGUIUtility.currentViewWidth / 2 - 10));
            EditorGUILayout.LabelField("Quest Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(vrCamera, new GUIContent("VR Camera"));
            if (enableVideoStreaming != null) EditorGUILayout.PropertyField(enableVideoStreaming);
            if (enableTouchReceiving != null) EditorGUILayout.PropertyField(enableTouchReceiving);
            if (enableHapticFeedback != null) EditorGUILayout.PropertyField(enableHapticFeedback);
            if (touchCanvas != null) EditorGUILayout.PropertyField(touchCanvas);
            
            EditorGUILayout.EndVertical();
            
            // Mobile column
            EditorGUILayout.BeginVertical("box", GUILayout.Width(EditorGUIUtility.currentViewWidth / 2 - 10));
            EditorGUILayout.LabelField("Mobile Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(videoDisplay, new GUIContent("Video Display"));
            if (enableVideoReceiving != null) EditorGUILayout.PropertyField(enableVideoReceiving);
            if (enableTouchSending != null) EditorGUILayout.PropertyField(enableTouchSending);
            if (enableHapticReceiving != null) EditorGUILayout.PropertyField(enableHapticReceiving);
            if (connectionUI != null) EditorGUILayout.PropertyField(connectionUI);
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }
    }
}