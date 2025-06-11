using UnityEngine;
using UnityVerseBridge.Core;
using UnityVerseBridge.Core.DataChannel.Data;
using System.Collections;

namespace UnityVerseBridge.Core.Extensions.Quest
{
    /// <summary>
    /// Quest VR에서 발생하는 다양한 상호작용 이벤트를 감지하고
    /// 해당 이벤트에 대한 햅틱 피드백을 Mobile 디바이스로 전송하는 확장 컴포넌트입니다.
    /// </summary>
    [RequireComponent(typeof(UnityVerseBridgeManager))]
    public class QuestHapticExtension : MonoBehaviour
    {
        [Header("Haptic Settings")]
        [Tooltip("컨트롤러 버튼 입력에 대한 햅틱 피드백을 활성화합니다.")]
        [SerializeField] private bool enableButtonHaptics = true;
        
        [Tooltip("트리거 입력에 대한 햅틱 피드백을 활성화합니다.")]
        [SerializeField] private bool enableTriggerHaptics = true;
        
        [Tooltip("그립 입력에 대한 햅틱 피드백을 활성화합니다.")]
        [SerializeField] private bool enableGripHaptics = true;
        
        [Tooltip("손 추적 제스처에 대한 햅틱 피드백을 활성화합니다.")]
        [SerializeField] private bool enableHandTrackingHaptics = true;
        
        [Tooltip("충돌 이벤트에 대한 햅틱 피드백을 활성화합니다.")]
        [SerializeField] private bool enableCollisionHaptics = true;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        // 입력 상태 추적
        private float lastTriggerValue = 0f;
        private float lastGripValue = 0f;
        
        private UnityVerseBridgeManager bridgeManager;
        private WebRtcManager webRtcManager;
        private bool isInitialized = false;

        void Awake()
        {
            bridgeManager = GetComponent<UnityVerseBridgeManager>();
            if (bridgeManager == null)
            {
                Debug.LogError("[QuestHapticExtension] UnityVerseBridgeManager not found!");
                enabled = false;
                return;
            }
            
            // WebRtcManager 참조는 Start에서 가져옴 (초기화 순서 보장)
        }

        void Start()
        {
            if (bridgeManager.bridgeMode != UnityVerseBridgeManager.BridgeMode.Host)
            {
                Debug.LogWarning("[QuestHapticExtension] This component only works in Host mode. Disabling...");
                enabled = false;
                return;
            }
            
            webRtcManager = bridgeManager.WebRtcManager;
            if (webRtcManager == null)
            {
                Debug.LogError("[QuestHapticExtension] WebRtcManager not found!");
                enabled = false;
                return;
            }
            
            isInitialized = true;
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

            // 트리거 입력 감지
            if (enableTriggerHaptics)
            {
                DetectTriggerInputs();
            }

            // 그립 입력 감지
            if (enableGripHaptics)
            {
                DetectGripInputs();
            }

            // 손 추적 제스처 감지
            if (enableHandTrackingHaptics && OVRPlugin.GetHandTrackingEnabled())
            {
                DetectHandGestures();
            }
#else
            // Editor나 Quest가 아닌 환경에서는 키보드 입력으로 테스트
            if (enableButtonHaptics && Input.GetKeyDown(KeyCode.Space))
            {
                RequestHapticFeedback(HapticCommandType.VibrateShort, 0.1f, 0.8f);
                if (debugMode) Debug.Log("[QuestHapticExtension] Space key pressed - sending haptic");
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

        private void DetectTriggerInputs()
        {
            float rightTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
            float leftTrigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch);
            float currentTrigger = Mathf.Max(rightTrigger, leftTrigger);
            
            // 트리거를 완전히 당겼을 때
            if (lastTriggerValue < 0.9f && currentTrigger >= 0.9f)
            {
                RequestHapticFeedback(HapticCommandType.VibrateLong, 0.2f, 1.0f);
                if (debugMode) Debug.Log("[QuestHapticExtension] Trigger fully pressed");
            }
            // 트리거를 절반 이상 당겼을 때
            else if (lastTriggerValue < 0.5f && currentTrigger >= 0.5f)
            {
                RequestHapticFeedback(HapticCommandType.VibrateShort, 0.05f, 0.3f);
                if (debugMode) Debug.Log("[QuestHapticExtension] Trigger half pressed");
            }
            
            lastTriggerValue = currentTrigger;
        }

        private void DetectGripInputs()
        {
            float rightGrip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch);
            float leftGrip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch);
            float currentGrip = Mathf.Max(rightGrip, leftGrip);
            
            // 그립 시작
            if (lastGripValue < 0.5f && currentGrip >= 0.5f)
            {
                RequestHapticFeedback(HapticCommandType.VibrateDefault, 0.1f, 0.7f);
                if (debugMode) Debug.Log("[QuestHapticExtension] Grip started");
            }
            // 그립 해제
            else if (lastGripValue >= 0.5f && currentGrip < 0.5f)
            {
                RequestHapticFeedback(HapticCommandType.VibrateShort, 0.05f, 0.4f);
                if (debugMode) Debug.Log("[QuestHapticExtension] Grip released");
            }
            
            lastGripValue = currentGrip;
        }

        private void DetectHandGestures()
        {
            // 손 추적을 사용한 제스처 감지
            // TODO: OVRHand API를 사용한 제스처 감지 구현
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