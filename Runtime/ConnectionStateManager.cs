using System;
using System.Collections;
using UnityEngine;
using UnityVerseBridge.Core.Signaling;

namespace UnityVerseBridge.Core
{
    /// <summary>
    /// WebRTC 연결 상태를 관리하고 재연결을 처리하는 매니저
    /// </summary>
    public class ConnectionStateManager : MonoBehaviour
    {
        [Header("재연결 설정")]
        [SerializeField] private int maxReconnectAttempts = 3;
        [SerializeField] private float reconnectDelay = 2f;
        [SerializeField] private float reconnectDelayMultiplier = 2f;
        [SerializeField] private float maxReconnectDelay = 30f;
        
        private WebRtcManager webRtcManager;
        private int currentReconnectAttempt = 0;
        private Coroutine reconnectCoroutine;
        private bool isReconnecting = false;
        
        public event Action OnReconnectStarted;
        public event Action OnReconnectSucceeded;
        public event Action<int> OnReconnectFailed; // 남은 시도 횟수
        public event Action OnReconnectExhausted;
        
        void Awake()
        {
            webRtcManager = GetComponent<WebRtcManager>();
            if (webRtcManager == null)
            {
                Debug.LogError("[ConnectionStateManager] WebRtcManager not found!");
                enabled = false;
                return;
            }
            
            // 이벤트 구독
            webRtcManager.OnSignalingDisconnected += HandleDisconnection;
            webRtcManager.OnWebRtcDisconnected += HandleDisconnection;
            webRtcManager.OnWebRtcConnected += HandleReconnectionSuccess;
        }
        
        void OnDestroy()
        {
            if (webRtcManager != null)
            {
                webRtcManager.OnSignalingDisconnected -= HandleDisconnection;
                webRtcManager.OnWebRtcDisconnected -= HandleDisconnection;
                webRtcManager.OnWebRtcConnected -= HandleReconnectionSuccess;
            }
            
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
            }
        }
        
        private void HandleDisconnection()
        {
            if (!isReconnecting && currentReconnectAttempt < maxReconnectAttempts)
            {
                Debug.Log("[ConnectionStateManager] Connection lost. Starting reconnection...");
                StartReconnection();
            }
        }
        
        private void HandleReconnectionSuccess()
        {
            if (isReconnecting)
            {
                Debug.Log("[ConnectionStateManager] Reconnection successful!");
                isReconnecting = false;
                currentReconnectAttempt = 0;
                
                if (reconnectCoroutine != null)
                {
                    StopCoroutine(reconnectCoroutine);
                    reconnectCoroutine = null;
                }
                
                OnReconnectSucceeded?.Invoke();
            }
        }
        
        private void StartReconnection()
        {
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
            }
            
            isReconnecting = true;
            OnReconnectStarted?.Invoke();
            reconnectCoroutine = StartCoroutine(ReconnectWithBackoff());
        }
        
        private IEnumerator ReconnectWithBackoff()
        {
            float currentDelay = reconnectDelay;
            
            while (currentReconnectAttempt < maxReconnectAttempts && isReconnecting)
            {
                currentReconnectAttempt++;
                Debug.Log($"[ConnectionStateManager] Reconnect attempt {currentReconnectAttempt}/{maxReconnectAttempts}");
                
                // 재연결 시도
                yield return AttemptReconnection();
                
                if (webRtcManager.IsWebRtcConnected)
                {
                    // 성공
                    yield break;
                }
                
                // 실패 - 다음 시도까지 대기
                int remainingAttempts = maxReconnectAttempts - currentReconnectAttempt;
                OnReconnectFailed?.Invoke(remainingAttempts);
                
                if (remainingAttempts > 0)
                {
                    Debug.Log($"[ConnectionStateManager] Waiting {currentDelay}s before next attempt...");
                    yield return new WaitForSeconds(currentDelay);
                    
                    // Exponential backoff
                    currentDelay = Mathf.Min(currentDelay * reconnectDelayMultiplier, maxReconnectDelay);
                }
            }
            
            // 모든 시도 실패
            Debug.LogError("[ConnectionStateManager] All reconnection attempts exhausted.");
            isReconnecting = false;
            currentReconnectAttempt = 0;
            OnReconnectExhausted?.Invoke();
        }
        
        private IEnumerator AttemptReconnection()
        {
            // 기존 연결 정리
            webRtcManager.Disconnect();
            yield return new WaitForSeconds(0.5f);
            
            // 재연결 시도
            var reconnectTask = webRtcManager.StartSignalingAndPeerConnection(webRtcManager.SignalingServerUrl);
            yield return new WaitUntil(() => reconnectTask.IsCompleted);
            
            // 연결 상태 확인을 위한 추가 대기
            yield return new WaitForSeconds(1f);
        }
        
        public void ResetReconnectionAttempts()
        {
            currentReconnectAttempt = 0;
            isReconnecting = false;
            
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }
        }
    }
}
