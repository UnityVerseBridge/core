using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityVerseBridge.Core.Threading
{
    /// <summary>
    /// Unity 메인 스레드에서 작업을 실행하기 위한 디스패처
    /// 싱글톤 패턴으로 구현되어 어디서든 접근 가능
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private readonly Queue<Action> _executionQueue = new Queue<Action>();
        private readonly object _queueLock = new object();

        public static UnityMainThreadDispatcher Instance()
        {
            if (_instance == null)
            {
                var go = new GameObject("[UnityMainThreadDispatcher]");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }

        public void Enqueue(Action action)
        {
            if (action == null) return;
            
            lock (_queueLock)
            {
                _executionQueue.Enqueue(action);
            }
        }

        void Update()
        {
            lock (_queueLock)
            {
                while (_executionQueue.Count > 0)
                {
                    try
                    {
                        _executionQueue.Dequeue()?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[UnityMainThreadDispatcher] Exception in queued action: {e}");
                    }
                }
            }
        }

        void OnDestroy()
        {
            _instance = null;
        }
    }
}
