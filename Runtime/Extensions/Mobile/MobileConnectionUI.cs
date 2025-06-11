using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace UnityVerseBridge.Core.Extensions.Mobile
{
    /// <summary>
    /// 모바일 앱에서 연결을 관리하는 간단한 UI 컴포넌트
    /// </summary>
    [RequireComponent(typeof(UnityVerseBridgeManager))]
    public class MobileConnectionUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject connectionPanel;
        [SerializeField] private InputField roomIdInput;
        [SerializeField] private Button connectButton;
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject loadingIndicator;
        
        [Header("Settings")]
        [SerializeField] private bool hideUIAfterConnection = true;
        [SerializeField] private float hideDelay = 2f;
        
        private UnityVerseBridgeManager bridgeManager;
        private WebRtcManager webRtcManager;
        private bool isConnecting = false;

        void Awake()
        {
            bridgeManager = GetComponent<UnityVerseBridgeManager>();
            if (bridgeManager == null)
            {
                Debug.LogError("[MobileConnectionUI] UnityVerseBridgeManager not found!");
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
                Debug.LogWarning("[MobileConnectionUI] This component only works in Client mode. Disabling...");
                enabled = false;
                yield break;
            }

            webRtcManager = bridgeManager.WebRtcManager;
            if (webRtcManager == null)
            {
                Debug.LogError("[MobileConnectionUI] WebRtcManager not found!");
                enabled = false;
                yield break;
            }

            // Setup UI
            SetupUI();
            
            // Subscribe to events
            webRtcManager.OnSignalingConnected += OnSignalingConnected;
            webRtcManager.OnSignalingDisconnected += OnSignalingDisconnected;
            // OnSignalingError is not available in WebRtcManager
            webRtcManager.OnWebRtcConnected += OnWebRtcConnected;
            webRtcManager.OnWebRtcDisconnected += OnWebRtcDisconnected;
            
            // Set initial state
            UpdateUI(false, "Ready to connect");
        }

        void OnDestroy()
        {
            if (webRtcManager != null)
            {
                webRtcManager.OnSignalingConnected -= OnSignalingConnected;
                webRtcManager.OnSignalingDisconnected -= OnSignalingDisconnected;
                // OnSignalingError is not available in WebRtcManager
                webRtcManager.OnWebRtcConnected -= OnWebRtcConnected;
                webRtcManager.OnWebRtcDisconnected -= OnWebRtcDisconnected;
            }
        }

        private void SetupUI()
        {
            // Setup connect button
            if (connectButton != null)
            {
                connectButton.onClick.RemoveAllListeners();
                connectButton.onClick.AddListener(OnConnectButtonClicked);
            }

            // Setup room ID input
            if (roomIdInput != null && bridgeManager.ConnectionConfig != null)
            {
                roomIdInput.text = bridgeManager.ConnectionConfig.roomId;
                roomIdInput.onEndEdit.RemoveAllListeners();
                roomIdInput.onEndEdit.AddListener(OnRoomIdChanged);
            }

            // Hide loading indicator initially
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
        }

        private void OnConnectButtonClicked()
        {
            if (isConnecting) return;

            if (webRtcManager.IsSignalingConnected)
            {
                // Disconnect
                bridgeManager.Disconnect();
                UpdateUI(false, "Disconnecting...");
            }
            else
            {
                // Connect
                string roomId = roomIdInput != null ? roomIdInput.text : bridgeManager.ConnectionConfig.roomId;
                if (string.IsNullOrEmpty(roomId))
                {
                    UpdateUI(false, "Please enter a room ID");
                    return;
                }

                isConnecting = true;
                UpdateUI(true, "Connecting to signaling server...");
                
                // Update config with new room ID
                if (bridgeManager.ConnectionConfig != null)
                {
                    bridgeManager.ConnectionConfig.roomId = roomId;
                }
                
                bridgeManager.Connect();
            }
        }

        private void OnRoomIdChanged(string newRoomId)
        {
            if (bridgeManager.ConnectionConfig != null)
            {
                bridgeManager.ConnectionConfig.roomId = newRoomId;
            }
        }

        private void OnSignalingConnected()
        {
            UpdateUI(true, "Connected to server. Waiting for peer...");
            if (connectButton != null)
            {
                var buttonText = connectButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = "Disconnect";
                }
            }
        }

        private void OnSignalingDisconnected()
        {
            isConnecting = false;
            UpdateUI(false, "Disconnected");
            if (connectButton != null)
            {
                var buttonText = connectButton.GetComponentInChildren<Text>();
                if (buttonText != null)
                {
                    buttonText.text = "Connect";
                }
            }
        }

        // OnSignalingError is not available in WebRtcManager
        // Error handling is done through OnSignalingDisconnected

        private void OnWebRtcConnected()
        {
            isConnecting = false;
            UpdateUI(false, "Connected to peer!");
            
            if (hideUIAfterConnection && connectionPanel != null)
            {
                StartCoroutine(HideUIAfterDelay());
            }
        }

        private void OnWebRtcDisconnected()
        {
            UpdateUI(false, "Peer disconnected");
            
            // Show UI again if it was hidden
            if (connectionPanel != null && !connectionPanel.activeSelf)
            {
                connectionPanel.SetActive(true);
            }
        }

        private void UpdateUI(bool showLoading, string status)
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(showLoading);
            }

            if (statusText != null)
            {
                statusText.text = status;
            }

            if (roomIdInput != null)
            {
                roomIdInput.interactable = !webRtcManager.IsSignalingConnected;
            }
        }

        private IEnumerator HideUIAfterDelay()
        {
            yield return new WaitForSeconds(hideDelay);
            
            if (connectionPanel != null)
            {
                connectionPanel.SetActive(false);
            }
        }

        // Public methods
        public void ShowUI()
        {
            if (connectionPanel != null)
            {
                connectionPanel.SetActive(true);
            }
        }

        public void HideUI()
        {
            if (connectionPanel != null)
            {
                connectionPanel.SetActive(false);
            }
        }

        public void SetRoomId(string roomId)
        {
            if (roomIdInput != null)
            {
                roomIdInput.text = roomId;
            }
            
            if (bridgeManager.ConnectionConfig != null)
            {
                bridgeManager.ConnectionConfig.roomId = roomId;
            }
        }
    }
}