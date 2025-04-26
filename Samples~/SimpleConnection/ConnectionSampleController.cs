// Assets/_SampleCreationTemp/ConnectionSampleController.cs 에 작성될 내용
using UnityEngine;
using UnityEngine.UI; // Standard UI 사용 시
using TMPro;         // TextMeshPro 사용 시
using UnityVerseBridge.Core; // WebRtcManager 접근 위해
using System;
using System.Collections;

// 네임스페이스는 샘플임을 명확히 하기 위해 지정 (선택 사항)
namespace UnityVerseBridge.Core.Samples.SimpleConnection
{
    public class ConnectionSampleController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Scene에 있는 WebRtcManager 컴포넌트를 할당해주세요.")]
        [SerializeField] private WebRtcManager webRtcManager;

        [Header("UI References")]
        [SerializeField] private TMP_Text signalingStatusText;
        [SerializeField] private Button connectSignalingButton;
        [SerializeField] private TMP_Text peerConnectionStatusText;
        [SerializeField] private Button startPeerConnectionButton;
        [SerializeField] private TMP_InputField messageInputField;
        [SerializeField] private Button sendMessageButton;
        [SerializeField] private TMP_Text receivedMessagesText;
        [SerializeField] private ScrollRect receivedMessagesScrollRect; // 메시지 많을 때 스크롤

        [Serializable]
        private class ChatMessage
        {
            public string type;
            public string text;
        }

        void Start()
        {
            if (webRtcManager == null)
            {
                Debug.LogError("WebRtcManager가 Inspector에 할당되지 않았습니다!");
                enabled = false; // 컴포넌트 비활성화
                return;
            }

            // 버튼 리스너 추가
            connectSignalingButton?.onClick.AddListener(webRtcManager.ConnectSignaling); // 직접 연결
            startPeerConnectionButton?.onClick.AddListener(webRtcManager.StartPeerConnection); // 직접 연결
            sendMessageButton?.onClick.AddListener(SendMessageFromInput);

            // WebRtcManager 이벤트 구독
            webRtcManager.OnSignalingConnected += UpdateSignalingStatusUI;
            webRtcManager.OnSignalingDisconnected += UpdateSignalingStatusUI;
            webRtcManager.OnWebRtcConnected += UpdatePeerConnectionStatusUI;
            webRtcManager.OnWebRtcDisconnected += UpdatePeerConnectionStatusUI;
            webRtcManager.OnDataChannelOpened += HandleDataChannelOpened;
            webRtcManager.OnDataChannelClosed += HandleDataChannelClosed;
            webRtcManager.OnDataChannelMessageReceived += HandleDataChannelMessageReceived;

            // 초기 UI 상태 업데이트
            UpdateSignalingStatusUI();
            UpdatePeerConnectionStatusUI();
            receivedMessagesText.text = "[Received Messages]\n";
        }

        void OnDestroy()
        {
            // 메모리 누수 방지를 위해 이벤트 구독 해지
            if (webRtcManager != null)
            {
                webRtcManager.OnSignalingConnected -= UpdateSignalingStatusUI;
                webRtcManager.OnSignalingDisconnected -= UpdateSignalingStatusUI;
                webRtcManager.OnWebRtcConnected -= UpdatePeerConnectionStatusUI;
                webRtcManager.OnWebRtcDisconnected -= UpdatePeerConnectionStatusUI;
                webRtcManager.OnDataChannelOpened -= HandleDataChannelOpened;
                webRtcManager.OnDataChannelClosed -= HandleDataChannelClosed;
                webRtcManager.OnDataChannelMessageReceived -= HandleDataChannelMessageReceived;
            }
        }

        private void SendMessageFromInput()
        {
            string message = messageInputField.text;
            if (!string.IsNullOrEmpty(message) && webRtcManager != null)
            {
                webRtcManager.SendDataChannelMessage(new ChatMessage { type = "chat", text = message });
                messageInputField.text = string.Empty; // 입력 필드 비우기
            }
        }

        // --- Event Handlers & UI Updaters ---
        private void UpdateSignalingStatusUI() => signalingStatusText.text = $"Signaling: {(webRtcManager.IsSignalingConnected ? "Connected" : "Disconnected")}";
        private void UpdatePeerConnectionStatusUI() => peerConnectionStatusText.text = $"P2P Status: {(webRtcManager.IsWebRtcConnected ? "Connected" : "Disconnected")}"; // TODO: WebRtcManager에서 더 상세한 상태 제공 필요
        private void HandleDataChannelOpened(string label) => UpdatePeerConnectionStatusUI(); // 또는 데이터 채널 상태 별도 표시
        private void HandleDataChannelClosed() => UpdatePeerConnectionStatusUI();

        private void HandleDataChannelMessageReceived(string message)
        {
            string formattedMessage = $"> {message}\n";
            Debug.Log($"Sample received: {formattedMessage}");

            var chatMsg = JsonUtility.FromJson<ChatMessage>(message);
            receivedMessagesText.text += $"[{chatMsg.type}]: {chatMsg.text}\n";

            // 스크롤 자동 내리기 (선택 사항)
            Canvas.ForceUpdateCanvases(); // 강제 업데이트 후 스크롤 조정
            if (receivedMessagesScrollRect != null && receivedMessagesScrollRect.verticalScrollbar != null)
                receivedMessagesScrollRect.verticalScrollbar.SetValueWithoutNotify(0f);
            StartCoroutine(ScrollToBottom()); // 다음 프레임에 스크롤 조정
        }

        private IEnumerator ScrollToBottom()
        {
            yield return null; // 한 프레임 대기 (UI 업데이트 후)

            if (receivedMessagesScrollRect) 
                receivedMessagesScrollRect.verticalNormalizedPosition = 0f;
        }
    }
}