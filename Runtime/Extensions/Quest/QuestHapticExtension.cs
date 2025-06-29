using UnityEngine;
using UnityVerseBridge.Core;
using UnityVerseBridge.Core.DataChannel.Data;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace UnityVerseBridge.Core.Extensions.Quest
{
    /// <summary>
    /// Quest VR에서 발생하는 다양한 상호작용 이벤트를 감지하고
    /// 해당 이벤트에 대한 햅틱 피드백을 Mobile 디바이스로 전송하는 확장 컴포넌트입니다.
    /// </summary>
    public class QuestHapticExtension : MonoBehaviour
    {
        [Header("Haptic Settings")]
        [Tooltip("컨트롤러 버튼 입력에 대한 햅틱 피드백을 활성화합니다.")]
        [SerializeField] private bool enableButtonHaptics = true;
        
        
        [Tooltip("충돌 이벤트에 대한 햅틱 피드백을 활성화합니다.")]
        [SerializeField] private bool enableCollisionHaptics = true;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        
        private UnityVerseBridgeManager bridgeManager;
        private WebRtcManager webRtcManager;
        private bool isInitialized = false;

        void Awake()
        {
            // Try to find UnityVerseBridgeManager on the same GameObject or parent
            bridgeManager = GetComponentInParent<UnityVerseBridgeManager>();
            if (bridgeManager == null)
            {
                // If not found in parent hierarchy, try to find it in the scene
                bridgeManager = FindFirstObjectByType<UnityVerseBridgeManager>();
                
                if (bridgeManager == null)
                {
                    Debug.LogError("[QuestHapticExtension] UnityVerseBridgeManager not found in parent hierarchy or scene! Please ensure UnityVerseBridgeManager exists.");
                    enabled = false;
                    return;
                }
                else
                {
                    Debug.Log("[QuestHapticExtension] Found UnityVerseBridgeManager in scene.");
                }
            }
            else
            {
                Debug.Log("[QuestHapticExtension] Found UnityVerseBridgeManager in parent hierarchy.");
            }
            
            // WebRtcManager 참조는 Start에서 가져옴 (초기화 순서 보장)
        }

        void Start()
        {
            // Wait for initialization
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
            if (bridgeManager.Mode != UnityVerseBridgeManager.BridgeMode.Host)
            {
                Debug.LogWarning("[QuestHapticExtension] This component only works in Host mode. Disabling...");
                enabled = false;
                yield break;
            }
            
            // Get WebRtcManager
            webRtcManager = bridgeManager.WebRtcManager;
            if (webRtcManager == null)
            {
                Debug.LogError("[QuestHapticExtension] WebRtcManager not found after initialization!");
                enabled = false;
                yield break;
            }
            
            isInitialized = true;
            Debug.Log("[QuestHapticExtension] Initialized");
        }

        void Update()
        {
            if (!isInitialized || webRtcManager == null || !webRtcManager.IsWebRtcConnected) return;

#if UNITY_ANDROID && !UNITY_EDITOR && QUEST_SUPPORT
            // 버튼 입력 감지
            if (enableButtonHaptics)
            {
                DetectButtonInputs();
            }
#else
            // Editor나 Quest가 아닌 환경에서는 새로운 Input System 사용
            if (enableButtonHaptics)
            {
                // Check if we have keyboard support
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                {
                    RequestHapticFeedback(HapticCommandType.VibrateShort, 0.1f, 0.8f);
                    if (debugMode) Debug.Log("[QuestHapticExtension] Space key pressed - sending haptic");
                }
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR && QUEST_SUPPORT
        private void DetectButtonInputs()
        {
            // A 버튼 (오른손)
            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
            {
                RequestHapticFeedback(HapticCommandType.VibrateShort, 0.1f, 0.8f);
                if (debugMode) Debug.Log("[QuestHapticExtension] A button pressed");
            }
            
            // B 버튼 (오른손)
            if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
            {
                RequestHapticFeedback(HapticCommandType.VibrateShort, 0.1f, 0.8f);
                if (debugMode) Debug.Log("[QuestHapticExtension] B button pressed");
            }
            
            // X 버튼 (왼손)
            if (OVRInput.GetDown(OVRInput.Button.Three, OVRInput.Controller.LTouch))
            {
                RequestHapticFeedback(HapticCommandType.VibrateShort, 0.1f, 0.8f);
                if (debugMode) Debug.Log("[QuestHapticExtension] X button pressed");
            }
            
            // Y 버튼 (왼손)
            if (OVRInput.GetDown(OVRInput.Button.Four, OVRInput.Controller.LTouch))
            {
                RequestHapticFeedback(HapticCommandType.VibrateShort, 0.1f, 0.8f);
                if (debugMode) Debug.Log("[QuestHapticExtension] Y button pressed");
            }
            
            // 조이스틱 클릭
            if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick) || 
                OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick))
            {
                RequestHapticFeedback(HapticCommandType.VibrateShort, 0.05f, 0.5f);
                if (debugMode) Debug.Log("[QuestHapticExtension] Thumbstick clicked");
            }
        }

#endif

        public void RequestHapticFeedback(HapticCommandType type, float duration = 0.1f, float intensity = 1.0f)
        {
            if (webRtcManager != null && webRtcManager.IsWebRtcConnected)
            {
                HapticCommand command = new HapticCommand(type, duration, intensity);
                if (debugMode) Debug.Log($"[QuestHapticExtension] Sending Haptic Command: {type}, Duration: {duration}s, Intensity: {intensity}");
                webRtcManager.SendDataChannelMessage(command);
            }
        }

        // 충돌 이벤트 처리
        void OnCollisionEnter(Collision collision)
        {
            if (!enableCollisionHaptics || webRtcManager == null || !webRtcManager.IsWebRtcConnected) return;
            
            // 충돌 강도에 따른 햅틱 피드백
            float impactForce = collision.relativeVelocity.magnitude;
            float normalizedForce = Mathf.Clamp01(impactForce / 10f); // 10m/s를 최대로 정규화
            
            if (normalizedForce > 0.1f) // 최소 임계값
            {
                float duration = Mathf.Lerp(0.05f, 0.3f, normalizedForce);
                RequestHapticFeedback(HapticCommandType.VibrateCustom, duration, normalizedForce);
                if (debugMode) Debug.Log($"[QuestHapticExtension] Collision haptic: Force={impactForce:F2}, Intensity={normalizedForce:F2}");
            }
        }

        // 외부에서 햅틱 요청을 할 수 있는 공개 메서드들
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
            // 텔레포트 시 특별한 햅틱 시퀀스
            RequestHapticFeedback(HapticCommandType.VibrateShort, 0.05f, 0.3f);
            yield return new WaitForSeconds(0.1f);
            RequestHapticFeedback(HapticCommandType.VibrateShort, 0.05f, 0.6f);
            yield return new WaitForSeconds(0.1f);
            RequestHapticFeedback(HapticCommandType.VibrateLong, 0.2f, 1.0f);
        }

        // Inspector에서 테스트용
        [ContextMenu("Test Short Haptic")]
        void TestShortHaptic()
        {
            RequestHapticFeedback(HapticCommandType.VibrateShort);
        }

        [ContextMenu("Test Long Haptic")]
        void TestLongHaptic()
        {
            RequestHapticFeedback(HapticCommandType.VibrateLong, 0.5f);
        }
    }
}