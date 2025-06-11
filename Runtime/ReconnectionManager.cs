using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityVerseBridge.Core
{
    /// <summary>
    /// Manages automatic reconnection with exponential backoff
    /// </summary>
    public class ReconnectionManager : MonoBehaviour
    {
        [Header("Reconnection Settings")]
        [SerializeField] private int maxReconnectAttempts = 5;
        [SerializeField] private float initialDelaySeconds = 1f;
        [SerializeField] private float maxDelaySeconds = 30f;
        [SerializeField] private float backoffMultiplier = 2f;
        [SerializeField] private bool enableAutoReconnect = true;
        
        [Header("Connection Lost Behavior")]
        // [SerializeField] private bool preserveDataOnDisconnect = true; // Reserved for future use
        [SerializeField] private float connectionTimeoutSeconds = 10f;
        
        // Events
        public event Action OnReconnecting;
        public event Action<int> OnReconnectAttempt; // attempt number
        public event Action OnReconnectSuccess;
        public event Action<string> OnReconnectFailed; // error message
        public event Action OnConnectionLost;
        
        // State
        private bool isReconnecting = false;
        private int currentAttempt = 0;
        private Coroutine reconnectCoroutine;
        
        // Connection delegates
        private Func<Task<bool>> connectAsyncFunc;
        private Func<bool> isConnectedFunc;
        private Action disconnectAction;
        
        public bool IsReconnecting => isReconnecting;
        public int CurrentAttempt => currentAttempt;
        public bool EnableAutoReconnect
        {
            get => enableAutoReconnect;
            set => enableAutoReconnect = value;
        }
        
        /// <summary>
        /// Initialize the reconnection manager with connection functions
        /// </summary>
        public void Initialize(
            Func<Task<bool>> connectAsync,
            Func<bool> isConnected,
            Action disconnect)
        {
            connectAsyncFunc = connectAsync ?? throw new ArgumentNullException(nameof(connectAsync));
            isConnectedFunc = isConnected ?? throw new ArgumentNullException(nameof(isConnected));
            disconnectAction = disconnect ?? throw new ArgumentNullException(nameof(disconnect));
        }
        
        /// <summary>
        /// Start monitoring connection and handle disconnections
        /// </summary>
        public void StartMonitoring()
        {
            if (!enableAutoReconnect) return;
            
            StopMonitoring();
            StartCoroutine(MonitorConnection());
        }
        
        /// <summary>
        /// Stop monitoring connection
        /// </summary>
        public void StopMonitoring()
        {
            StopAllCoroutines();
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }
            isReconnecting = false;
            currentAttempt = 0;
        }
        
        /// <summary>
        /// Manually trigger reconnection
        /// </summary>
        public void TriggerReconnect()
        {
            if (!isReconnecting)
            {
                StartReconnection();
            }
        }
        
        private IEnumerator MonitorConnection()
        {
            while (enableAutoReconnect)
            {
                yield return new WaitForSeconds(1f);
                
                if (!isReconnecting && isConnectedFunc != null && !isConnectedFunc())
                {
                    Debug.Log("[ReconnectionManager] Connection lost detected");
                    OnConnectionLost?.Invoke();
                    StartReconnection();
                }
            }
        }
        
        private void StartReconnection()
        {
            if (isReconnecting) return;
            
            isReconnecting = true;
            currentAttempt = 0;
            OnReconnecting?.Invoke();
            
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
            }
            
            reconnectCoroutine = StartCoroutine(ReconnectWithBackoff());
        }
        
        private IEnumerator ReconnectWithBackoff()
        {
            while (currentAttempt < maxReconnectAttempts && isReconnecting)
            {
                currentAttempt++;
                
                // Calculate delay with exponential backoff
                float delay = CalculateDelay(currentAttempt);
                
                Debug.Log($"[ReconnectionManager] Reconnect attempt {currentAttempt}/{maxReconnectAttempts} in {delay:F1}s");
                OnReconnectAttempt?.Invoke(currentAttempt);
                
                // Wait before attempting
                yield return new WaitForSeconds(delay);
                
                // Ensure we're still supposed to be reconnecting
                if (!isReconnecting || !enableAutoReconnect) break;
                
                // Disconnect any existing connection
                try
                {
                    disconnectAction?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ReconnectionManager] Error during disconnect: {e.Message}");
                }
                
                // Wait a bit after disconnect
                yield return new WaitForSeconds(0.5f);
                
                // Attempt connection
                bool success = false;
                var connectionTask = AttemptConnection();
                
                // Wait for connection with timeout
                float timeoutTimer = 0f;
                while (!connectionTask.IsCompleted && timeoutTimer < connectionTimeoutSeconds)
                {
                    yield return null;
                    timeoutTimer += Time.deltaTime;
                }
                
                if (connectionTask.IsCompleted && !connectionTask.IsFaulted)
                {
                    success = connectionTask.Result;
                }
                
                if (success && isConnectedFunc())
                {
                    Debug.Log("[ReconnectionManager] Reconnection successful!");
                    OnReconnectSuccess?.Invoke();
                    isReconnecting = false;
                    currentAttempt = 0;
                    yield break;
                }
                
                Debug.LogWarning($"[ReconnectionManager] Reconnect attempt {currentAttempt} failed");
            }
            
            // All attempts failed
            string errorMsg = $"Failed to reconnect after {currentAttempt} attempts";
            Debug.LogError($"[ReconnectionManager] {errorMsg}");
            OnReconnectFailed?.Invoke(errorMsg);
            isReconnecting = false;
            currentAttempt = 0;
        }
        
        private async Task<bool> AttemptConnection()
        {
            try
            {
                if (connectAsyncFunc != null)
                {
                    return await connectAsyncFunc();
                }
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReconnectionManager] Connection attempt failed: {e.Message}");
                return false;
            }
        }
        
        private float CalculateDelay(int attemptNumber)
        {
            // Exponential backoff: delay = initial * (multiplier ^ (attempt - 1))
            float delay = initialDelaySeconds * Mathf.Pow(backoffMultiplier, attemptNumber - 1);
            
            // Add jitter (±10%) to prevent thundering herd
            float jitter = UnityEngine.Random.Range(-0.1f, 0.1f);
            delay *= (1f + jitter);
            
            // Clamp to max delay
            return Mathf.Min(delay, maxDelaySeconds);
        }
        
        void OnDestroy()
        {
            StopMonitoring();
        }
        
        void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus && enableAutoReconnect)
            {
                // App resumed, check connection
                if (isConnectedFunc != null && !isConnectedFunc())
                {
                    Debug.Log("[ReconnectionManager] App resumed, triggering reconnect");
                    StartReconnection();
                }
            }
        }
        
        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && enableAutoReconnect && Application.platform != RuntimePlatform.IPhonePlayer)
            {
                // App regained focus (not iOS which uses OnApplicationPause)
                if (isConnectedFunc != null && !isConnectedFunc())
                {
                    Debug.Log("[ReconnectionManager] App regained focus, triggering reconnect");
                    StartReconnection();
                }
            }
        }
    }
}