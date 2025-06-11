using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.Events;

namespace UnityVerseBridge.Core.UI
{
    /// <summary>
    /// Generic UI component for fetching and displaying active room lists
    /// Can be used by any Unity app that needs room selection functionality
    /// </summary>
    public class RoomListUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform roomListContainer;
        [SerializeField] private GameObject roomItemPrefab;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private GameObject emptyStateObject;
        
        [Header("Settings")]
        [SerializeField] private ConnectionConfig connectionConfig;
        [SerializeField] private float autoRefreshInterval = 5f;
        [SerializeField] private bool autoRefreshEnabled = true;
        
        [Header("Events")]
        [SerializeField] private UnityEvent<string> onRoomSelected = new UnityEvent<string>();
        [SerializeField] private UnityEvent<RoomInfo[]> onRoomsUpdated = new UnityEvent<RoomInfo[]>();
        
        private Coroutine autoRefreshCoroutine;
        private bool isRefreshing = false;
        
        /// <summary>
        /// Room information structure
        /// </summary>
        [System.Serializable]
        public class RoomInfo
        {
            public string roomId;
            public string hostType;
            public long createdAt;
            public int guestCount;
            
            public string GetFormattedAge()
            {
                var age = System.DateTimeOffset.Now.ToUnixTimeMilliseconds() - createdAt;
                var seconds = age / 1000;
                if (seconds < 60) return $"{seconds}s ago";
                var minutes = seconds / 60;
                if (minutes < 60) return $"{minutes}m ago";
                var hours = minutes / 60;
                return $"{hours}h ago";
            }
        }
        
        [System.Serializable]
        private class RoomListResponse
        {
            public RoomInfo[] rooms;
            public string timestamp;
        }
        
        void Start()
        {
            if (refreshButton != null)
            {
                refreshButton.onClick.AddListener(RefreshRoomList);
            }
            
            // Start auto-refresh if enabled
            if (autoRefreshEnabled && autoRefreshInterval > 0)
            {
                autoRefreshCoroutine = StartCoroutine(AutoRefresh());
            }
            
            // Initial fetch
            RefreshRoomList();
        }
        
        void OnEnable()
        {
            if (autoRefreshEnabled && autoRefreshInterval > 0 && autoRefreshCoroutine == null)
            {
                autoRefreshCoroutine = StartCoroutine(AutoRefresh());
            }
        }
        
        void OnDisable()
        {
            if (autoRefreshCoroutine != null)
            {
                StopCoroutine(autoRefreshCoroutine);
                autoRefreshCoroutine = null;
            }
        }
        
        /// <summary>
        /// Manually refresh the room list
        /// </summary>
        public void RefreshRoomList()
        {
            if (!isRefreshing)
            {
                StartCoroutine(FetchRoomListCoroutine());
            }
        }
        
        private IEnumerator FetchRoomListCoroutine()
        {
            if (connectionConfig == null)
            {
                ShowStatus("ConnectionConfig not set", Color.red);
                yield break;
            }
            
            isRefreshing = true;
            ShowLoading(true);
            
            // Extract base URL from WebSocket URL
            string baseUrl = connectionConfig.signalingServerUrl
                .Replace("ws://", "http://")
                .Replace("wss://", "https://");
            
            // Remove trailing slash if present
            if (baseUrl.EndsWith("/"))
            {
                baseUrl = baseUrl.TrimEnd('/');
            }
            
            string roomsUrl = $"{baseUrl}/rooms";
            
            ShowStatus("Fetching room list...", Color.yellow);
            
            using (UnityWebRequest request = UnityWebRequest.Get(roomsUrl))
            {
                request.timeout = 10; // 10 second timeout
                yield return request.SendWebRequest();
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    ShowStatus($"Failed to fetch rooms: {request.error}", Color.red);
                    ShowEmptyState(true);
                }
                else
                {
                    try
                    {
                        string jsonResponse = request.downloadHandler.text;
                        RoomListResponse response = JsonUtility.FromJson<RoomListResponse>(jsonResponse);
                        
                        UpdateRoomList(response.rooms);
                        ShowStatus($"Found {response.rooms.Length} active room{(response.rooms.Length != 1 ? "s" : "")}", Color.green);
                        
                        // Invoke event
                        onRoomsUpdated?.Invoke(response.rooms);
                    }
                    catch (System.Exception e)
                    {
                        ShowStatus($"Failed to parse response: {e.Message}", Color.red);
                        ShowEmptyState(true);
                    }
                }
            }
            
            ShowLoading(false);
            isRefreshing = false;
        }
        
        private void UpdateRoomList(RoomInfo[] rooms)
        {
            // Clear existing list
            if (roomListContainer != null)
            {
                foreach (Transform child in roomListContainer)
                {
                    Destroy(child.gameObject);
                }
            }
            
            ShowEmptyState(rooms.Length == 0);
            
            // Add room items
            foreach (var room in rooms)
            {
                CreateRoomItem(room);
            }
        }
        
        private void CreateRoomItem(RoomInfo room)
        {
            if (roomItemPrefab == null || roomListContainer == null) return;
            
            GameObject item = Instantiate(roomItemPrefab, roomListContainer);
            
            // Try to find and update text components
            Text[] texts = item.GetComponentsInChildren<Text>();
            if (texts.Length > 0)
            {
                texts[0].text = $"Room: {room.roomId}";
                if (texts.Length > 1)
                {
                    texts[1].text = $"Host: {room.hostType} | Guests: {room.guestCount}";
                }
                if (texts.Length > 2)
                {
                    texts[2].text = room.GetFormattedAge();
                }
            }
            
            // Add click handler
            Button button = item.GetComponent<Button>();
            if (button != null)
            {
                string roomId = room.roomId; // Capture for closure
                button.onClick.AddListener(() => SelectRoom(roomId));
            }
            
            // Alternative: Look for RoomItemUI component
            var roomItemUI = item.GetComponent<IRoomItemUI>();
            if (roomItemUI != null)
            {
                roomItemUI.SetRoomInfo(room);
                roomItemUI.OnSelected += () => SelectRoom(room.roomId);
            }
        }
        
        private void SelectRoom(string roomId)
        {
            Debug.Log($"[RoomListUI] Room selected: {roomId}");
            onRoomSelected?.Invoke(roomId);
        }
        
        private IEnumerator AutoRefresh()
        {
            while (autoRefreshEnabled)
            {
                yield return new WaitForSeconds(autoRefreshInterval);
                if (!isRefreshing)
                {
                    RefreshRoomList();
                }
            }
        }
        
        private void ShowStatus(string message, Color color)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = color;
            }
            Debug.Log($"[RoomListUI] {message}");
        }
        
        private void ShowLoading(bool show)
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(show);
            }
            
            if (refreshButton != null)
            {
                refreshButton.interactable = !show;
            }
        }
        
        private void ShowEmptyState(bool show)
        {
            if (emptyStateObject != null)
            {
                emptyStateObject.SetActive(show);
            }
        }
        
        /// <summary>
        /// Enable or disable auto-refresh
        /// </summary>
        public void SetAutoRefresh(bool enabled)
        {
            autoRefreshEnabled = enabled;
            
            if (enabled && autoRefreshCoroutine == null && gameObject.activeInHierarchy)
            {
                autoRefreshCoroutine = StartCoroutine(AutoRefresh());
            }
            else if (!enabled && autoRefreshCoroutine != null)
            {
                StopCoroutine(autoRefreshCoroutine);
                autoRefreshCoroutine = null;
            }
        }
        
        /// <summary>
        /// Set the connection configuration
        /// </summary>
        public void SetConnectionConfig(ConnectionConfig config)
        {
            connectionConfig = config;
        }
        
        void OnDestroy()
        {
            if (autoRefreshCoroutine != null)
            {
                StopCoroutine(autoRefreshCoroutine);
            }
        }
    }
    
    /// <summary>
    /// Interface for custom room item UI implementations
    /// </summary>
    public interface IRoomItemUI
    {
        void SetRoomInfo(RoomListUI.RoomInfo room);
        event System.Action OnSelected;
    }
}