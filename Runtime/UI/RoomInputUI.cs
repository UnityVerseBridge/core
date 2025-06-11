using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Text.RegularExpressions;

namespace UnityVerseBridge.Core.UI
{
    /// <summary>
    /// Generic UI component for room ID input and validation
    /// Supports manual input and QR code data processing
    /// </summary>
    public class RoomInputUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private InputField roomIdInputField;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button pasteButton;
        [SerializeField] private Button scanQRButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Text validationText;
        
        [Header("Settings")]
        [SerializeField] private ConnectionConfig connectionConfig;
        [SerializeField] private bool validateRoomId = true;
        [SerializeField] private string roomIdPattern = @"^[A-Za-z0-9\-_]+$";
        [SerializeField] private int minRoomIdLength = 3;
        [SerializeField] private int maxRoomIdLength = 50;
        
        [Header("Events")]
        [SerializeField] private UnityEvent<string> m_OnRoomIdSubmitted = new UnityEvent<string>();
        [SerializeField] private UnityEvent m_OnScanQRRequested = new UnityEvent();
        
        // Public event accessors
        public UnityEvent<string> onRoomIdSubmitted => m_OnRoomIdSubmitted;
        public UnityEvent onScanQRRequested => m_OnScanQRRequested;
        
        private bool isValidInput = false;
        
        /// <summary>
        /// Data structure for QR code room connections
        /// </summary>
        [System.Serializable]
        public class RoomConnectionData
        {
            public string roomId;
            public string serverUrl;
            public string timestamp;
            public string appVersion;
        }
        
        void Start()
        {
            SetupUI();
            
            // Load default or saved room ID
            if (roomIdInputField != null && connectionConfig != null)
            {
                roomIdInputField.text = connectionConfig.roomId;
                ValidateInput(connectionConfig.roomId);
            }
        }
        
        private void SetupUI()
        {
            if (connectButton != null)
            {
                connectButton.onClick.AddListener(OnConnectButtonClicked);
            }
            
            if (pasteButton != null)
            {
                pasteButton.onClick.AddListener(OnPasteButtonClicked);
            }
            
            if (scanQRButton != null)
            {
                scanQRButton.onClick.AddListener(OnScanQRButtonClicked);
            }
            
            if (roomIdInputField != null)
            {
                roomIdInputField.onValueChanged.AddListener(OnInputValueChanged);
                roomIdInputField.onEndEdit.AddListener(OnInputEndEdit);
            }
            
            // Disable session room ID if we're using manual input
            if (connectionConfig != null)
            {
                connectionConfig.useSessionRoomId = false;
            }
        }
        
        private void OnInputValueChanged(string value)
        {
            ValidateInput(value);
        }
        
        private void OnInputEndEdit(string value)
        {
            // Trim whitespace on end edit
            string trimmed = value.Trim();
            if (trimmed != value && roomIdInputField != null)
            {
                roomIdInputField.text = trimmed;
            }
        }
        
        private void ValidateInput(string input)
        {
            if (!validateRoomId)
            {
                isValidInput = !string.IsNullOrEmpty(input);
                UpdateValidationUI(isValidInput, "");
                return;
            }
            
            string trimmed = input.Trim();
            
            // Check if empty
            if (string.IsNullOrEmpty(trimmed))
            {
                isValidInput = false;
                UpdateValidationUI(false, "Room ID is required");
                return;
            }
            
            // Check length
            if (trimmed.Length < minRoomIdLength)
            {
                isValidInput = false;
                UpdateValidationUI(false, $"Room ID must be at least {minRoomIdLength} characters");
                return;
            }
            
            if (trimmed.Length > maxRoomIdLength)
            {
                isValidInput = false;
                UpdateValidationUI(false, $"Room ID must be no more than {maxRoomIdLength} characters");
                return;
            }
            
            // Check pattern
            if (!string.IsNullOrEmpty(roomIdPattern))
            {
                var regex = new Regex(roomIdPattern);
                if (!regex.IsMatch(trimmed))
                {
                    isValidInput = false;
                    UpdateValidationUI(false, "Room ID contains invalid characters");
                    return;
                }
            }
            
            isValidInput = true;
            UpdateValidationUI(true, "Valid room ID");
        }
        
        private void UpdateValidationUI(bool isValid, string message)
        {
            if (connectButton != null)
            {
                connectButton.interactable = isValid;
            }
            
            if (validationText != null)
            {
                validationText.text = message;
                validationText.color = isValid ? Color.green : Color.red;
                validationText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            }
        }
        
        private void OnConnectButtonClicked()
        {
            if (!isValidInput || roomIdInputField == null) return;
            
            string roomId = roomIdInputField.text.Trim();
            
            if (string.IsNullOrEmpty(roomId))
            {
                ShowStatus("Please enter a room ID", Color.red);
                return;
            }
            
            // Update connection config
            if (connectionConfig != null)
            {
                connectionConfig.roomId = roomId;
            }
            
            ShowStatus($"Connecting to room: {roomId}", Color.yellow);
            
            // Save to PlayerPrefs for next time
            PlayerPrefs.SetString("LastRoomId", roomId);
            PlayerPrefs.Save();
            
            // Invoke event
            m_OnRoomIdSubmitted?.Invoke(roomId);
        }
        
        private void OnPasteButtonClicked()
        {
            string clipboardText = GUIUtility.systemCopyBuffer;
            
            if (!string.IsNullOrEmpty(clipboardText))
            {
                // Try to parse as JSON first (QR code data)
                try
                {
                    var connectionData = JsonUtility.FromJson<RoomConnectionData>(clipboardText);
                    if (!string.IsNullOrEmpty(connectionData.roomId))
                    {
                        ProcessConnectionData(connectionData);
                        return;
                    }
                }
                catch
                {
                    // Not JSON, treat as plain room ID
                }
                
                // Use as plain room ID
                if (roomIdInputField != null)
                {
                    roomIdInputField.text = clipboardText.Trim();
                }
                
                ShowStatus("Pasted from clipboard", Color.green);
            }
            else
            {
                ShowStatus("Clipboard is empty", Color.yellow);
            }
        }
        
        private void OnScanQRButtonClicked()
        {
            ShowStatus("Opening QR scanner...", Color.yellow);
            m_OnScanQRRequested?.Invoke();
        }
        
        /// <summary>
        /// Process QR code scan result or connection data
        /// </summary>
        public void ProcessQRCodeData(string qrData)
        {
            try
            {
                var connectionData = JsonUtility.FromJson<RoomConnectionData>(qrData);
                ProcessConnectionData(connectionData);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoomInputUI] Failed to parse QR data: {e.Message}");
                ShowStatus("Invalid QR code format", Color.red);
                
                // Try to use as plain room ID
                if (!string.IsNullOrEmpty(qrData) && roomIdInputField != null)
                {
                    roomIdInputField.text = qrData.Trim();
                }
            }
        }
        
        private void ProcessConnectionData(RoomConnectionData data)
        {
            // Set room ID
            if (roomIdInputField != null)
            {
                roomIdInputField.text = data.roomId;
            }
            
            // Update server URL if provided
            if (!string.IsNullOrEmpty(data.serverUrl) && connectionConfig != null)
            {
                connectionConfig.signalingServerUrl = data.serverUrl;
                Debug.Log($"[RoomInputUI] Updated server URL from QR: {data.serverUrl}");
            }
            
            ShowStatus($"QR scan successful: {data.roomId}", Color.green);
            
            // Auto-connect if valid
            if (isValidInput)
            {
                OnConnectButtonClicked();
            }
        }
        
        /// <summary>
        /// Set the room ID programmatically
        /// </summary>
        public void SetRoomId(string roomId)
        {
            if (roomIdInputField != null)
            {
                roomIdInputField.text = roomId;
            }
        }
        
        /// <summary>
        /// Get the current room ID
        /// </summary>
        public string GetRoomId()
        {
            return roomIdInputField != null ? roomIdInputField.text.Trim() : "";
        }
        
        private void ShowStatus(string message, Color color)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = color;
            }
            Debug.Log($"[RoomInputUI] {message}");
        }
        
        /// <summary>
        /// Enable or disable the input UI
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            if (roomIdInputField != null)
                roomIdInputField.interactable = interactable;
            
            if (connectButton != null)
                connectButton.interactable = interactable && isValidInput;
            
            if (pasteButton != null)
                pasteButton.interactable = interactable;
            
            if (scanQRButton != null)
                scanQRButton.interactable = interactable;
        }
        
        /// <summary>
        /// Load the last used room ID from PlayerPrefs
        /// </summary>
        public void LoadLastRoomId()
        {
            string lastRoomId = PlayerPrefs.GetString("LastRoomId", "");
            if (!string.IsNullOrEmpty(lastRoomId))
            {
                SetRoomId(lastRoomId);
            }
        }
    }
}