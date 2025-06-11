using UnityEngine;
using UnityVerseBridge.Core;
using UnityVerseBridge.Core.DataChannel.Data;
using System;
using System.Collections;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace UnityVerseBridge.Core.Extensions.Mobile
{
    /// <summary>
    /// 모바일 디바이스에서 Quest VR로부터 햅틱 명령을 수신하여
    /// 플랫폼별 진동 피드백을 실행하는 확장 컴포넌트입니다.
    /// HapticHandler를 보완하는 모바일 특화 기능을 제공합니다.
    /// </summary>
    public class MobileHapticExtension : MonoBehaviour
    {
        [Header("Haptic Settings")]
        [Tooltip("햅틱 피드백을 활성화합니다.")]
        [SerializeField] private bool enableHaptics = true;
        
        
        [Tooltip("진동 강도 배율입니다.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float intensityMultiplier = 1f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject vibrator;
        private AndroidJavaClass vibrationEffectClass;
        private AndroidJavaClass vibrationAttributesClass;
        private readonly int ANDROID_API_26 = 26; // Android O (8.0)
#endif

        private UnityVerseBridgeManager bridgeManager;
        private WebRtcManager webRtcManager;
        #pragma warning disable 0414
        private bool isInitialized = false;
        #pragma warning restore 0414

        void Awake()
        {
            // Try to find manager in parent first (for child components)
            bridgeManager = GetComponentInParent<UnityVerseBridgeManager>();
            
            // If not found in parent, search the scene
            if (bridgeManager == null)
            {
                bridgeManager = FindFirstObjectByType<UnityVerseBridgeManager>();
            }
            
            if (bridgeManager == null)
            {
                Debug.LogError("[MobileHapticExtension] UnityVerseBridgeManager not found in parent or scene!");
                enabled = false;
                return;
            }
        }

        void Start()
        {
            StartCoroutine(WaitForInitialization());
        }

        private IEnumerator WaitForInitialization()
        {
            // Wait for UnityVerseBridgeManager to be initialized
            while (!bridgeManager.IsInitialized)
            {
                yield return null;
            }

            // Check mode after initialization
            if (bridgeManager.Mode != UnityVerseBridgeManager.BridgeMode.Client)
            {
                Debug.LogWarning("[MobileHapticExtension] This component only works in Client mode. Disabling...");
                enabled = false;
                yield break;
            }
            
            webRtcManager = bridgeManager.WebRtcManager;
            if (webRtcManager == null)
            {
                Debug.LogError("[MobileHapticExtension] WebRtcManager not found!");
                enabled = false;
                yield break;
            }

            InitializePlatformHaptics();
            
            // 이벤트 구독
            webRtcManager.OnDataChannelMessageReceived += HandleDataChannelMessageReceived;
            webRtcManager.OnMultiPeerDataChannelMessageReceived += HandleMultiPeerDataChannelMessageReceived;
            
            isInitialized = true;
        }

        void OnDestroy()
        {
            if (webRtcManager != null)
            {
                webRtcManager.OnDataChannelMessageReceived -= HandleDataChannelMessageReceived;
                webRtcManager.OnMultiPeerDataChannelMessageReceived -= HandleMultiPeerDataChannelMessageReceived;
            }
        }

        private void InitializePlatformHaptics()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    
                    // Android API 26+ 확인
                    int sdkInt = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
                    if (sdkInt >= ANDROID_API_26)
                    {
                        vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                        vibrationAttributesClass = new AndroidJavaClass("android.os.VibrationAttributes");
                    }
                }
                
                if (debugMode) Debug.Log("[MobileHapticExtension] Android vibrator initialized");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MobileHapticExtension] Failed to initialize Android vibrator: {e.Message}");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS는 별도의 초기화가 필요 없음
            if (debugMode) Debug.Log("[MobileHapticExtension] iOS haptics ready");
#endif
        }

        private void HandleDataChannelMessageReceived(string jsonData)
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
                Debug.LogError($"[MobileHapticExtension] Failed to parse JSON: '{jsonData}' | Error: {e.Message}");
            }
        }
        
        private void HandleMultiPeerDataChannelMessageReceived(string peerId, string jsonData)
        {
            if (!enableHaptics || string.IsNullOrEmpty(jsonData)) return;
            
            if (debugMode) 
                Debug.Log($"[MobileHapticExtension] Received message from peer {peerId}");
            
            // MultiPeer의 경우에도 동일한 처리
            HandleDataChannelMessageReceived(jsonData);
        }

        private void ProcessHapticCommand(HapticCommand command)
        {
            if (debugMode) 
                Debug.Log($"[MobileHapticExtension] Processing Haptic: {command.commandType}, Duration: {command.duration}s, Intensity: {command.intensity}");

            float adjustedIntensity = Mathf.Clamp01(command.intensity * intensityMultiplier);
            float durationMs = command.duration * 1000f; // 초를 밀리초로 변환

            switch (command.commandType)
            {
                case HapticCommandType.VibrateDefault:
                    VibrateDefault();
                    break;
                    
                case HapticCommandType.VibrateShort:
                    VibrateCustom(50f, adjustedIntensity); // 50ms
                    break;
                    
                case HapticCommandType.VibrateLong:
                    VibrateCustom(500f, adjustedIntensity); // 500ms
                    break;
                    
                case HapticCommandType.VibrateCustom:
                    VibrateCustom(durationMs, adjustedIntensity);
                    break;
                    
                case HapticCommandType.PlaySound:
                    // TODO: 사운드와 함께 진동
                    if (debugMode) Debug.Log($"[MobileHapticExtension] PlaySound not implemented: {command.soundName}");
                    VibrateDefault(); // 임시로 기본 진동
                    break;
            }
        }

        private void VibrateDefault()
        {
#if UNITY_EDITOR
            if (debugMode) Debug.Log("[MobileHapticExtension] Editor: Vibrate (default)");
#elif UNITY_ANDROID
            AndroidVibrate(100); // 100ms 기본 진동
#elif UNITY_IOS
            IOSVibrate(IOSHapticType.Selection);
#else
            Handheld.Vibrate();
#endif
        }

        private void VibrateCustom(float durationMs, float intensity)
        {
#if UNITY_EDITOR
            if (debugMode) Debug.Log($"[MobileHapticExtension] Editor: Vibrate {durationMs}ms at {intensity:F2} intensity");
#elif UNITY_ANDROID
            AndroidVibrateWithIntensity(durationMs, intensity);
#elif UNITY_IOS
            IOSVibrateCustom(durationMs, intensity);
#else
            // 기본 플랫폼은 Unity의 기본 진동 사용
            if (durationMs > 100) // 긴 진동
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
                Debug.LogError($"[MobileHapticExtension] Android vibrate error: {e.Message}");
                Handheld.Vibrate(); // 폴백
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
                        // Android 8.0+ VibrationEffect API 사용
                        int amplitude = Mathf.RoundToInt(intensity * 255f);
                        AndroidJavaObject vibrationEffect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot", (long)durationMs, amplitude
                        );
                        vibrator.Call("vibrate", vibrationEffect);
                    }
                    else
                    {
                        // 구형 Android는 패턴으로 강도 시뮬레이션
                        // 구형 Android에서는 항상 패턴 사용
                        if (true)
                        {
                            long[] pattern = CreateVibratePattern(durationMs, intensity);
                            vibrator.Call("vibrate", pattern, -1);
                        }
                        else
                        {
                            // 단순 진동
                            vibrator.Call("vibrate", (long)durationMs);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MobileHapticExtension] Android custom vibrate error: {e.Message}");
                AndroidVibrate((long)durationMs); // 폴백
            }
        }

        private long[] CreateVibratePattern(float durationMs, float intensity)
        {
            // 강도를 온/오프 패턴으로 시뮬레이션
            if (intensity >= 0.8f)
            {
                // 강한 진동: 연속
                return new long[] { 0, (long)durationMs };
            }
            else if (intensity >= 0.5f)
            {
                // 중간 진동: 짧은 간격
                int segments = Mathf.Max(1, (int)(durationMs / 50f));
                long[] pattern = new long[segments * 2];
                for (int i = 0; i < segments; i++)
                {
                    pattern[i * 2] = i == 0 ? 0 : 10; // 대기
                    pattern[i * 2 + 1] = 40; // 진동
                }
                return pattern;
            }
            else
            {
                // 약한 진동: 긴 간격
                int segments = Mathf.Max(1, (int)(durationMs / 100f));
                long[] pattern = new long[segments * 2];
                for (int i = 0; i < segments; i++)
                {
                    pattern[i * 2] = i == 0 ? 0 : 50; // 대기
                    pattern[i * 2 + 1] = 50; // 진동
                }
                return pattern;
            }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        // iOS 햅틱 타입
        private enum IOSHapticType
        {
            Selection,      // 가벼운 탭
            ImpactLight,    // 가벼운 충격
            ImpactMedium,   // 중간 충격
            ImpactHeavy,    // 강한 충격
            Success,        // 성공 피드백
            Warning,        // 경고 피드백
            Error          // 오류 피드백
        }

        [DllImport("__Internal")]
        private static extern void _PlayHaptic(string type);

        [DllImport("__Internal")]
        private static extern void _PlayCustomHaptic(float intensity, float sharpness, float duration);

        private void IOSVibrate(IOSHapticType type)
        {
            #if !UNITY_EDITOR
            _PlayHaptic(type.ToString());
            #endif
        }

        private void IOSVibrateCustom(float durationMs, float intensity)
        {
            #if !UNITY_EDITOR
            // iOS는 Core Haptics를 통해 커스텀 햅틱 지원
            float sharpness = intensity > 0.5f ? 0.8f : 0.3f; // 강도에 따른 선명도
            _PlayCustomHaptic(intensity, sharpness, durationMs / 1000f);
            #endif
        }
#endif

        // 기본 플랫폼용 반복 진동
        private IEnumerator RepeatVibration(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Handheld.Vibrate();
                yield return new WaitForSeconds(0.1f);
            }
        }

        // 햅틱 활성화/비활성화
        public void SetHapticsEnabled(bool enabled)
        {
            enableHaptics = enabled;
            if (debugMode) Debug.Log($"[MobileHapticExtension] Haptics {(enabled ? "enabled" : "disabled")}");
        }

        // 강도 배율 설정
        public void SetIntensityMultiplier(float multiplier)
        {
            intensityMultiplier = Mathf.Clamp(multiplier, 0.1f, 2f);
        }

        // 테스트용 메서드
        [ContextMenu("Test Default Vibration")]
        public void TestDefaultVibration()
        {
            ProcessHapticCommand(new HapticCommand(HapticCommandType.VibrateDefault, 0.1f, 1f));
        }

        [ContextMenu("Test Custom Vibration")]
        public void TestCustomVibration()
        {
            ProcessHapticCommand(new HapticCommand(HapticCommandType.VibrateCustom, 0.5f, 0.7f));
        }
    }
}