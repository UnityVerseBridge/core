using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Events;

namespace UnityVerseBridge.Core.Networking
{
    /// <summary>
    /// Discovers and manages available rooms from the signaling server
    /// </summary>
    public class RoomDiscovery : MonoBehaviour
    {
        [System.Serializable]
        public class RoomInfo
        {
            public string roomId;
            public string hostType;
            public long createdAt;
            public int guestCount;
            
            public DateTime CreatedTime => DateTimeOffset.FromUnixTimeMilliseconds(createdAt).DateTime;
            public bool IsQuestHost => hostType?.ToLower().Contains("quest") ?? false;
        }
        
        [System.Serializable]
        public class RoomListResponse
        {
            public RoomInfo[] rooms;
            public string timestamp;
        }
        
        [System.Serializable]
        public class RoomListEvent : UnityEvent<RoomInfo[]> { }
        
        [System.Serializable]
        public class RoomSelectedEvent : UnityEvent<string> { }
        
        [Header("Configuration")]
        [SerializeField] private string serverUrl;
        [SerializeField] private bool autoRefresh = true;
        [SerializeField] private float refreshInterval = 5f;
        [SerializeField] private bool sortByCreationTime = true;
        
        [Header("Events")]
        public RoomListEvent OnRoomsUpdated = new RoomListEvent();
        public RoomSelectedEvent OnRoomSelected = new RoomSelectedEvent();
        public UnityEvent OnRefreshStarted = new UnityEvent();
        public UnityEvent OnRefreshCompleted = new UnityEvent();
        public UnityEvent<string> OnError = new UnityEvent<string>();
        
        private List<RoomInfo> cachedRooms = new List<RoomInfo>();
        private Coroutine refreshCoroutine;
        private bool isRefreshing = false;
        
        public List<RoomInfo> CachedRooms => new List<RoomInfo>(cachedRooms);
        public bool IsRefreshing => isRefreshing;
        
        void Start()
        {
            // If no server URL provided, try to get from UnityVerseBridgeManager
            if (string.IsNullOrEmpty(serverUrl))
            {
                var bridge = FindFirstObjectByType<UnityVerseBridgeManager>();
                if (bridge != null)
                {
                    if (bridge.Configuration != null)
                    {
                        serverUrl = bridge.Configuration.signalingUrl;
                    }
                    else if (bridge.ConnectionConfig != null)
                    {
                        serverUrl = bridge.ConnectionConfig.signalingServerUrl;
                    }
                }
            }
            
            if (autoRefresh)
            {
                StartAutoRefresh();
            }
            else
            {
                RefreshRoomList();
            }
        }
        
        void OnDestroy()
        {
            StopAutoRefresh();
        }
        
        public void StartAutoRefresh()
        {
            StopAutoRefresh();
            refreshCoroutine = StartCoroutine(AutoRefreshCoroutine());
        }
        
        public void StopAutoRefresh()
        {
            if (refreshCoroutine != null)
            {
                StopCoroutine(refreshCoroutine);
                refreshCoroutine = null;
            }
        }
        
        public void RefreshRoomList()
        {
            if (!isRefreshing)
            {
                StartCoroutine(FetchRoomListCoroutine());
            }
        }
        
        public void SelectRoom(string roomId)
        {
            OnRoomSelected?.Invoke(roomId);
            
            // Update bridge manager if available
            var bridge = FindFirstObjectByType<UnityVerseBridgeManager>();
            if (bridge != null)
            {
                bridge.SetRoomId(roomId);
                bridge.Connect();
            }
        }
        
        private IEnumerator AutoRefreshCoroutine()
        {
            while (true)
            {
                yield return StartCoroutine(FetchRoomListCoroutine());
                yield return new WaitForSeconds(refreshInterval);
            }
        }
        
        private IEnumerator FetchRoomListCoroutine()
        {
            isRefreshing = true;
            OnRefreshStarted?.Invoke();
            
            string url = GetRoomsEndpointUrl();
            if (string.IsNullOrEmpty(url))
            {
                OnError?.Invoke("Server URL not configured");
                isRefreshing = false;
                OnRefreshCompleted?.Invoke();
                yield break;
            }
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 10;
                
                yield return request.SendWebRequest();
                
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string error = $"Failed to fetch rooms: {request.error}";
                    Debug.LogError($"[RoomDiscovery] {error}");
                    OnError?.Invoke(error);
                }
                else
                {
                    try
                    {
                        string json = request.downloadHandler.text;
                        RoomListResponse response = JsonUtility.FromJson<RoomListResponse>(json);
                        
                        ProcessRoomList(response.rooms);
                        Debug.Log($"[RoomDiscovery] Found {response.rooms.Length} active rooms");
                    }
                    catch (Exception e)
                    {
                        string error = $"Failed to parse room list: {e.Message}";
                        Debug.LogError($"[RoomDiscovery] {error}");
                        OnError?.Invoke(error);
                    }
                }
            }
            
            isRefreshing = false;
            OnRefreshCompleted?.Invoke();
        }
        
        private void ProcessRoomList(RoomInfo[] rooms)
        {
            cachedRooms.Clear();
            
            if (rooms != null)
            {
                cachedRooms.AddRange(rooms);
                
                // Sort if requested
                if (sortByCreationTime)
                {
                    cachedRooms.Sort((a, b) => b.createdAt.CompareTo(a.createdAt));
                }
            }
            
            OnRoomsUpdated?.Invoke(cachedRooms.ToArray());
        }
        
        private string GetRoomsEndpointUrl()
        {
            if (string.IsNullOrEmpty(serverUrl))
                return null;
            
            // Convert WebSocket URL to HTTP
            string httpUrl = serverUrl
                .Replace("ws://", "http://")
                .Replace("wss://", "https://");
            
            // Remove any path or query parameters
            Uri uri = new Uri(httpUrl);
            string baseUrl = $"{uri.Scheme}://{uri.Authority}";
            
            return $"{baseUrl}/rooms";
        }
        
        public void SetServerUrl(string url)
        {
            serverUrl = url;
            RefreshRoomList();
        }
        
        /// <summary>
        /// Get rooms filtered by host type
        /// </summary>
        public List<RoomInfo> GetRoomsByHostType(string hostType)
        {
            return cachedRooms.FindAll(room => 
                room.hostType.Equals(hostType, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Get Quest-hosted rooms only
        /// </summary>
        public List<RoomInfo> GetQuestRooms()
        {
            return cachedRooms.FindAll(room => room.IsQuestHost);
        }
        
        /// <summary>
        /// Find a specific room by ID
        /// </summary>
        public RoomInfo FindRoom(string roomId)
        {
            return cachedRooms.Find(room => room.roomId == roomId);
        }
    }
}