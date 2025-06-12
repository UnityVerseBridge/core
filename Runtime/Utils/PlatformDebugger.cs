using UnityEngine;
using System.Reflection;
using System.Text;

namespace UnityVerseBridge.Core.Utils
{
    /// <summary>
    /// Comprehensive platform detection debugger for UnityVerse
    /// </summary>
    public class PlatformDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool logOnStart = true;
        [SerializeField] private bool logToFile = false;
        [SerializeField] private string logFileName = "platform_debug.log";
        
        private StringBuilder debugInfo = new StringBuilder();
        
        void Start()
        {
            if (logOnStart)
            {
                LogPlatformInfo();
            }
        }
        
        [ContextMenu("Log Platform Info")]
        public void LogPlatformInfo()
        {
            debugInfo.Clear();
            
            LogSection("Platform Detection Debug");
            
            // Basic platform info
            LogInfo("Application.platform", Application.platform.ToString());
            LogInfo("Application.isMobilePlatform", Application.isMobilePlatform);
            LogInfo("Application.isEditor", Application.isEditor);
            LogInfo("SystemInfo.deviceModel", SystemInfo.deviceModel);
            LogInfo("SystemInfo.deviceType", SystemInfo.deviceType.ToString());
            LogInfo("SystemInfo.operatingSystem", SystemInfo.operatingSystem);
            
            // Platform checks
            LogSection("Platform Checks");
            LogInfo("Is Android", Application.platform == RuntimePlatform.Android);
            LogInfo("Is iOS", Application.platform == RuntimePlatform.IPhonePlayer);
            LogInfo("Is Editor", IsEditor());
            LogInfo("Is Quest/VR Device", IsQuestDevice());
            
            // XR Settings
            LogSection("XR Settings");
            LogXRInfo();
            
            // UnityVerse specific
            LogSection("UnityVerse Configuration");
            LogUnityVerseInfo();
            
            // Conditional compilation symbols
            LogSection("Compilation Symbols");
            LogCompilationSymbols();
            
            // Output
            string output = debugInfo.ToString();
            Debug.Log($"[PlatformDebugger]\n{output}");
            
            if (logToFile)
            {
                SaveToFile(output);
            }
        }
        
        private void LogSection(string sectionName)
        {
            debugInfo.AppendLine($"\n=== {sectionName} ===");
        }
        
        private void LogInfo(string key, object value)
        {
            debugInfo.AppendLine($"{key}: {value}");
        }
        
        private void LogXRInfo()
        {
            LogInfo("XRSettings.enabled", UnityEngine.XR.XRSettings.enabled);
            LogInfo("XRSettings.isDeviceActive", UnityEngine.XR.XRSettings.isDeviceActive);
            LogInfo("XRSettings.loadedDeviceName", UnityEngine.XR.XRSettings.loadedDeviceName);
            LogInfo("XRSettings.renderViewportScale", UnityEngine.XR.XRSettings.renderViewportScale);
            
#if UNITY_XR_MANAGEMENT
            LogInfo("UNITY_XR_MANAGEMENT", "Defined");
            
            var xrSettings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            if (xrSettings != null)
            {
                LogInfo("XRGeneralSettings.Instance", "Exists");
                
                if (xrSettings.Manager != null)
                {
                    LogInfo("XRManagerSettings", "Exists");
                    
                    if (xrSettings.Manager.activeLoader != null)
                    {
                        LogInfo("Active XR Loader", xrSettings.Manager.activeLoader.GetType().Name);
                    }
                    else
                    {
                        LogInfo("Active XR Loader", "None");
                    }
                    
                    // List all loaders
                    if (xrSettings.Manager.activeLoaders != null && xrSettings.Manager.activeLoaders.Count > 0)
                    {
                        foreach (var loader in xrSettings.Manager.activeLoaders)
                        {
                            LogInfo($"Available Loader", loader.GetType().Name);
                        }
                    }
                }
            }
#else
            LogInfo("UNITY_XR_MANAGEMENT", "Not Defined");
#endif
            
            // Check for platform-specific XR
            CheckPlatformSpecificXR();
        }
        
        private void CheckPlatformSpecificXR()
        {
            // Check for Oculus/Meta
            try
            {
                var ovrManagerType = System.Type.GetType("OVRManager, Oculus.VR");
                if (ovrManagerType != null)
                {
                    LogInfo("OVRManager", "Found");
                    
                    var instanceProperty = ovrManagerType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProperty != null)
                    {
                        var instance = instanceProperty.GetValue(null);
                        LogInfo("OVRManager.instance", instance != null ? "Exists" : "Null");
                    }
                }
                else
                {
                    LogInfo("OVRManager", "Not Found");
                }
                
                // Check for OVRCameraRig
                var cameraRigType = System.Type.GetType("OVRCameraRig, Oculus.VR");
                if (cameraRigType != null)
                {
                    var cameraRig = FindObjectOfType(cameraRigType);
                    LogInfo("OVRCameraRig", cameraRig != null ? "Found in scene" : "Not in scene");
                }
            }
            catch (System.Exception e)
            {
                LogInfo("OVR Check Error", e.Message);
            }
            
            // Check for OpenXR
#if UNITY_OPENXR
            LogInfo("OpenXR", "Enabled");
#else
            LogInfo("OpenXR", "Not Enabled");
#endif
        }
        
        private void LogUnityVerseInfo()
        {
            // Check for UnityVerseBridgeManager
            var bridge = FindObjectOfType<UnityVerseBridgeManager>();
            if (bridge != null)
            {
                LogInfo("UnityVerseBridgeManager", "Found");
                
                if (bridge.Configuration != null)
                {
                    var config = bridge.Configuration;
                    LogInfo("Config.roleDetection", config.roleDetection);
                    LogInfo("Config.DetectedRole", config.DetectedRole);
                    LogInfo("Config.signalingUrl", config.signalingUrl);
                    LogInfo("Config.roomId", config.roomId);
                }
                else if (bridge.ConnectionConfig != null)
                {
                    // Legacy ConnectionConfig
                    var config = bridge.ConnectionConfig;
                    LogInfo("ConnectionConfig.clientType", config.clientType);
                    LogInfo("ConnectionConfig.signalingServerUrl", config.signalingServerUrl);
                    LogInfo("ConnectionConfig.roomId", config.GetRoomId());
                }
            }
            else
            {
                LogInfo("UnityVerseBridgeManager", "Not Found");
            }
        }
        
        private void LogCompilationSymbols()
        {
#if UNITY_EDITOR
            LogInfo("UNITY_EDITOR", "Defined");
#endif
#if UNITY_ANDROID
            LogInfo("UNITY_ANDROID", "Defined");
#endif
#if UNITY_IOS
            LogInfo("UNITY_IOS", "Defined");
#endif
#if UNITY_STANDALONE
            LogInfo("UNITY_STANDALONE", "Defined");
#endif
#if UNITY_XR
            LogInfo("UNITY_XR", "Defined");
#endif
#if OCULUS_XR
            LogInfo("OCULUS_XR", "Defined");
#endif
#if META_XR
            LogInfo("META_XR", "Defined");
#endif
        }
        
        private bool IsEditor()
        {
            return Application.platform == RuntimePlatform.WindowsEditor ||
                   Application.platform == RuntimePlatform.OSXEditor ||
                   Application.platform == RuntimePlatform.LinuxEditor;
        }
        
        private bool IsQuestDevice()
        {
            string deviceModel = SystemInfo.deviceModel.ToLower();
            string osName = SystemInfo.operatingSystem.ToLower();
            
            return deviceModel.Contains("quest") || 
                   deviceModel.Contains("oculus") ||
                   osName.Contains("quest") ||
                   (Application.platform == RuntimePlatform.Android && 
                    UnityEngine.XR.XRSettings.loadedDeviceName.ToLower().Contains("oculus"));
        }
        
        private void SaveToFile(string content)
        {
            try
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath, logFileName);
                System.IO.File.WriteAllText(path, content);
                Debug.Log($"[PlatformDebugger] Debug info saved to: {path}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PlatformDebugger] Failed to save debug info: {e.Message}");
            }
        }
    }
}