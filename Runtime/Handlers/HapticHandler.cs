using System;
using System.Collections;
using UnityEngine;
using UnityVerseBridge.Core.DataChannel.Data;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace UnityVerseBridge.Core
{
    /// <summary>
    /// 햅틱 피드백을 처리하는 핸들러
    /// Host: 햅틱 명령 전송
    /// Client: 햅틱 피드백 실행
    /// </summary>
    [RequireComponent(typeof(UnityVerseBridgeManager))]
    public class HapticHandler : MonoBehaviour
    {
        [Header("Haptic Settings")]
        [SerializeField] private bool enableHaptics = true;
        [Range(0.1f, 2f)]
        [SerializeField] private float intensityMultiplier = 1f;
        
        [Header("Host Settings")]
        [SerializeField] private bool enableButtonHaptics = true;
        [SerializeField] private bool enableTriggerHaptics = true;
        [SerializeField] private bool enableGripHaptics = true;
        [SerializeField] private bool enableCollisionHaptics = true;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        
        private UnityVerseBridgeManager bridgeManager;
        private WebRtcManager webRtcManager;
        private UnityVerseBridgeManager.BridgeMode mode;
        
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject vibrator;
        private AndroidJavaClass vibrationEffectClass;
        private readonly int ANDROID_API_26 = 26;
#endif
        
        
        private bool isInitialized = false;

        public void Initialize(UnityVerseBridgeManager manager, WebRtcManager rtcManager, UnityVerseBridgeManager.BridgeMode bridgeMode)
        {
            bridgeManager = manager;
            webRtcManager = rtcManager;
            mode = bridgeMode;
            
            if (mode == UnityVerseBridgeManager.BridgeMode.Client)
            {
                InitializePlatformHaptics();
                
                // Subscribe to data channel events
                webRtcManager.OnDataChannelMessageReceived += OnDataChannelMessageReceived;
                
                // For multi-peer mode in WebRtcManager
                webRtcManager.OnMultiPeerDataChannelMessageReceived += OnMultiPeerDataChannelMessageReceived;
            }
            
            isInitialized = true;
        }

        void OnDestroy()
        {
            Cleanup();
        }

        void Update()
        {
            if (!isInitialized || mode != UnityVerseBridgeManager.BridgeMode.Host) return;
            if (!webRtcManager.IsWebRtcConnected) return;
            
            // Detect inputs and send haptic commands (Host mode)
            if (enableButtonHaptics)
            {
                DetectButtonInputs();
            }
            
            if (enableTriggerHaptics)
            {
                DetectTriggerInputs();
            }
            
            if (enableGripHaptics)
            {
                DetectGripInputs();
            }
        }

        #region Platform Initialization
        private void InitializePlatformHaptics()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    
                    int sdkInt = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
                    if (sdkInt >= ANDROID_API_26)
                    {
                        vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    }
                }
                
                if (debugMode) Debug.Log("[HapticHandler] Android vibrator initialized");
            }
            catch (Exception e)
            {
                Debug.LogError($"[HapticHandler] Failed to initialize Android vibrator: {e.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            if (debugMode) Debug.Log("[HapticHandler] iOS haptics ready");
#endif
        }
        #endregion

        #region Host Mode - Input Detection
        private void DetectButtonInputs()
        {
            // Button input detection should be implemented in platform-specific code
            // This is a placeholder for the core package
            if (debugMode) Debug.Log("[HapticHandler] DetectButtonInputs - Should be implemented in platform-specific code");
        }

        private void DetectTriggerInputs()
        {
            // Trigger input detection should be implemented in platform-specific code
            // This is a placeholder for the core package
            if (debugMode) Debug.Log("[HapticHandler] DetectTriggerInputs - Should be implemented in platform-specific code");
        }

        private void DetectGripInputs()
        {
            // Grip input detection should be implemented in platform-specific code
            // This is a placeholder for the core package
            if (debugMode) Debug.Log("[HapticHandler] DetectGripInputs - Should be implemented in platform-specific code");
        }

        public void RequestHapticFeedback(HapticCommandType type, float duration = 0.1f, float intensity = 1.0f)
        {
            if (mode != UnityVerseBridgeManager.BridgeMode.Host) return;
            if (!webRtcManager.IsWebRtcConnected) return;
            
            HapticCommand command = new HapticCommand(type, duration, intensity);
            if (debugMode) Debug.Log($"[HapticHandler] Sending Haptic Command: {type}, Duration: {duration}s, Intensity: {intensity}");
            webRtcManager.SendDataChannelMessage(command);
        }
        #endregion

        #region Client Mode - Haptic Execution
        private void OnDataChannelMessageReceived(string jsonData)
        {
            if (mode != UnityVerseBridgeManager.BridgeMode.Client) return;
            HandleHapticMessage(jsonData);
        }

        private void OnMultiPeerDataChannelMessageReceived(string peerId, string jsonData)
        {
            if (mode != UnityVerseBridgeManager.BridgeMode.Client) return;
            if (debugMode) Debug.Log($"[HapticHandler] Received message from peer {peerId}");
            HandleHapticMessage(jsonData);
        }

        private void HandleHapticMessage(string jsonData)
        {
            if (!enableHaptics || string.IsNullOrEmpty(jsonData)) return;
            
            try
            {
                DataChannelMessageBase baseMsg = JsonUtility.FromJson<DataChannelMessageBase>(jsonData);
                if (baseMsg?.type == "haptic")
                {
                    HapticCommand command = JsonUtility.FromJson<HapticCommand>(jsonData);
                    if (command != null)
                    {
                        ProcessHapticCommand(command);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[HapticHandler] Failed to parse JSON: '{jsonData}' | Error: {e.Message}");
            }
        }

        private void ProcessHapticCommand(HapticCommand command)
        {
            if (debugMode) 
                Debug.Log($"[HapticHandler] Processing Haptic: {command.commandType}, Duration: {command.duration}s, Intensity: {command.intensity}");

            float adjustedIntensity = Mathf.Clamp01(command.intensity * intensityMultiplier);
            float durationMs = command.duration * 1000f;

            switch (command.commandType)
            {
                case HapticCommandType.VibrateDefault:
                    VibrateDefault();
                    break;
                    
                case HapticCommandType.VibrateShort:
                    VibrateCustom(50f, adjustedIntensity);
                    break;
                    
                case HapticCommandType.VibrateLong:
                    VibrateCustom(500f, adjustedIntensity);
                    break;
                    
                case HapticCommandType.VibrateCustom:
                    VibrateCustom(durationMs, adjustedIntensity);
                    break;
                    
                case HapticCommandType.PlaySound:
                    if (debugMode) Debug.Log($"[HapticHandler] PlaySound not implemented: {command.soundName}");
                    VibrateDefault();
                    break;
            }
        }

        private void VibrateDefault()
        {
#if UNITY_EDITOR
            if (debugMode) Debug.Log("[HapticHandler] Editor: Vibrate (default)");
#elif UNITY_ANDROID
            AndroidVibrate(100);
#elif UNITY_IOS
            Handheld.Vibrate();
#else
            Handheld.Vibrate();
#endif
        }

        private void VibrateCustom(float durationMs, float intensity)
        {
#if UNITY_EDITOR
            if (debugMode) Debug.Log($"[HapticHandler] Editor: Vibrate {durationMs}ms at {intensity:F2} intensity");
#elif UNITY_ANDROID
            AndroidVibrateWithIntensity(durationMs, intensity);
#elif UNITY_IOS
            // iOS doesn't support custom duration/intensity with built-in API
            if (durationMs > 100)
            {
                StartCoroutine(RepeatVibration((int)(durationMs / 100f)));
            }
            else
            {
                Handheld.Vibrate();
            }
#else
            if (durationMs > 100)
            {
                StartCoroutine(RepeatVibration((int)(durationMs / 100f)));
            }
            else
            {
                Handheld.Vibrate();
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void AndroidVibrate(long milliseconds)
        {
            try
            {
                if (vibrator != null && vibrator.Call<bool>("hasVibrator"))
                {
                    vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[HapticHandler] Android vibrate error: {e.Message}");
                Handheld.Vibrate();
            }
        }

        private void AndroidVibrateWithIntensity(float durationMs, float intensity)
        {
            try
            {
                if (vibrator != null && vibrator.Call<bool>("hasVibrator"))
                {
                    int sdkInt = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
                    
                    if (sdkInt >= ANDROID_API_26 && vibrationEffectClass != null)
                    {
                        // Android 8.0+ VibrationEffect API
                        int amplitude = Mathf.RoundToInt(intensity * 255f);
                        AndroidJavaObject vibrationEffect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot", (long)durationMs, amplitude
                        );
                        vibrator.Call("vibrate", vibrationEffect);
                    }
                    else
                    {
                        // Older Android - use pattern
                        if (useCustomPatterns)
                        {
                            long[] pattern = CreateVibratePattern(durationMs, intensity);
                            vibrator.Call("vibrate", pattern, -1);
                        }
                        else
                        {
                            vibrator.Call("vibrate", (long)durationMs);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[HapticHandler] Android custom vibrate error: {e.Message}");
                AndroidVibrate((long)durationMs);
            }
        }

        private long[] CreateVibratePattern(float durationMs, float intensity)
        {
            if (intensity >= 0.8f)
            {
                // Strong vibration: continuous
                return new long[] { 0, (long)durationMs };
            }
            else if (intensity >= 0.5f)
            {
                // Medium vibration: short intervals
                int segments = Mathf.Max(1, (int)(durationMs / 50f));
                long[] pattern = new long[segments * 2];
                for (int i = 0; i < segments; i++)
                {
                    pattern[i * 2] = i == 0 ? 0 : 10; // Wait
                    pattern[i * 2 + 1] = 40; // Vibrate
                }
                return pattern;
            }
            else
            {
                // Weak vibration: long intervals
                int segments = Mathf.Max(1, (int)(durationMs / 100f));
                long[] pattern = new long[segments * 2];
                for (int i = 0; i < segments; i++)
                {
                    pattern[i * 2] = i == 0 ? 0 : 50; // Wait
                    pattern[i * 2 + 1] = 50; // Vibrate
                }
                return pattern;
            }
        }
#endif

        private IEnumerator RepeatVibration(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Handheld.Vibrate();
                yield return new WaitForSeconds(0.1f);
            }
        }
        #endregion

        #region Collision Detection (Host Mode)
        void OnCollisionEnter(Collision collision)
        {
            if (!enableCollisionHaptics || mode != UnityVerseBridgeManager.BridgeMode.Host) return;
            if (!webRtcManager.IsWebRtcConnected) return;
            
            float impactForce = collision.relativeVelocity.magnitude;
            float normalizedForce = Mathf.Clamp01(impactForce / 10f);
            
            if (normalizedForce > 0.1f)
            {
                float duration = Mathf.Lerp(0.05f, 0.3f, normalizedForce);
                RequestHapticFeedback(HapticCommandType.VibrateCustom, duration, normalizedForce);
                if (debugMode) Debug.Log($"[HapticHandler] Collision haptic: Force={impactForce:F2}, Intensity={normalizedForce:F2}");
            }
        }
        #endregion

        #region Cleanup
        private void Cleanup()
        {
            if (!isInitialized) return;
            
            if (webRtcManager != null)
            {
                webRtcManager.OnDataChannelMessageReceived -= OnDataChannelMessageReceived;
            }
            
            if (webRtcManager != null)
            {
                webRtcManager.OnMultiPeerDataChannelMessageReceived -= OnMultiPeerDataChannelMessageReceived;
            }
        }
        #endregion

        #region Public API
        public void SetHapticsEnabled(bool enabled)
        {
            enableHaptics = enabled;
            if (debugMode) Debug.Log($"[HapticHandler] Haptics {(enabled ? "enabled" : "disabled")}");
        }

        public void SetIntensityMultiplier(float multiplier)
        {
            intensityMultiplier = Mathf.Clamp(multiplier, 0.1f, 2f);
        }

        // Host mode - manual haptic triggers
        public void OnObjectGrabbed()
        {
            RequestHapticFeedback(HapticCommandType.VibrateDefault, 0.15f, 0.8f);
        }

        public void OnObjectReleased()
        {
            RequestHapticFeedback(HapticCommandType.VibrateShort, 0.05f, 0.5f);
        }

        public void OnMenuOpened()
        {
            RequestHapticFeedback(HapticCommandType.VibrateShort, 0.1f, 0.6f);
        }

        public void OnTeleport()
        {
            StartCoroutine(TeleportHapticSequence());
        }

        private IEnumerator TeleportHapticSequence()
        {
            RequestHapticFeedback(HapticCommandType.VibrateShort, 0.05f, 0.3f);
            yield return new WaitForSeconds(0.1f);
            RequestHapticFeedback(HapticCommandType.VibrateShort, 0.05f, 0.6f);
            yield return new WaitForSeconds(0.1f);
            RequestHapticFeedback(HapticCommandType.VibrateLong, 0.2f, 1.0f);
        }
        #endregion
    }
}