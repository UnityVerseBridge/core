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
        private SerializedProperty configuration;
        private SerializedProperty webRtcConfiguration;
        private SerializedProperty enableAutoConnect;
        private SerializedProperty enableDebugLogging;
        
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
            configuration = serializedObject.FindProperty("configuration");
            webRtcConfiguration = serializedObject.FindProperty("webRtcConfiguration");
            enableAutoConnect = serializedObject.FindProperty("enableAutoConnect");
            enableDebugLogging = serializedObject.FindProperty("enableDebugLogging");
            
            // Quest-specific properties
            vrCamera = serializedObject.FindProperty("vrCamera");
            enableVideoStreaming = serializedObject.FindProperty("enableVideoStreaming");
            enableTouchReceiving = serializedObject.FindProperty("enableTouchReceiving");
            enableHapticFeedback = serializedObject.FindProperty("enableHapticFeedback");
            touchCanvas = serializedObject.FindProperty("touchCanvas");
            
            // Mobile-specific properties
            videoDisplay = serializedObject.FindProperty("videoDisplay");
            enableVideoReceiving = serializedObject.FindProperty("enableVideoReceiving");
            enableTouchSending = serializedObject.FindProperty("enableTouchSending");
            enableHapticReceiving = serializedObject.FindProperty("enableHapticReceiving");
            connectionUI = serializedObject.FindProperty("connectionUI");
            
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
            EditorGUILayout.PropertyField(configuration);
            EditorGUILayout.PropertyField(webRtcConfiguration);
            EditorGUILayout.PropertyField(enableAutoConnect);
            EditorGUILayout.PropertyField(enableDebugLogging);
            
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
            EditorGUILayout.PropertyField(enableVideoStreaming);
            EditorGUILayout.PropertyField(enableTouchReceiving);
            EditorGUILayout.PropertyField(enableHapticFeedback);
            
            // Optional
            if (enableTouchReceiving.boolValue)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Optional", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(touchCanvas, new GUIContent("Touch Canvas"));
                
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
            EditorGUILayout.PropertyField(enableVideoReceiving);
            EditorGUILayout.PropertyField(enableTouchSending);
            EditorGUILayout.PropertyField(enableHapticReceiving);
            
            // Optional
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Optional", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(connectionUI, new GUIContent("Connection UI"));
            
            if (connectionUI.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Connection UI can be used for room ID input and connection status", MessageType.Info);
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
            EditorGUILayout.PropertyField(enableVideoStreaming);
            EditorGUILayout.PropertyField(enableTouchReceiving);
            EditorGUILayout.PropertyField(enableHapticFeedback);
            EditorGUILayout.PropertyField(touchCanvas);
            
            EditorGUILayout.EndVertical();
            
            // Mobile column
            EditorGUILayout.BeginVertical("box", GUILayout.Width(EditorGUIUtility.currentViewWidth / 2 - 10));
            EditorGUILayout.LabelField("Mobile Settings", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(videoDisplay, new GUIContent("Video Display"));
            EditorGUILayout.PropertyField(enableVideoReceiving);
            EditorGUILayout.PropertyField(enableTouchSending);
            EditorGUILayout.PropertyField(enableHapticReceiving);
            EditorGUILayout.PropertyField(connectionUI);
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
        }
    }
}